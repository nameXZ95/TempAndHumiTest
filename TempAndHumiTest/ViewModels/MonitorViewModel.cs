using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.ImageFilters;
using Serilog.Context;
using SkiaSharp;
using TempAndHumiTest.Common;
using TempAndHumiTest.Models;
using TempAndHumiTest.Services;

namespace TempAndHumiTest.ViewModels
{
    /// <summary>
    /// 监控页面。核心是 <see cref="PollLoopAsync"/> 这条采集循环，规则只有四条：
    /// <list type="number">
    /// <item>整个程序只有一条循环：重复点开始不会起第二条；</item>
    /// <item>循环必须有独立的 <see cref="CancellationTokenSource"/>，停止时「取消 → 等它退出 → 再 Dispose」；</item>
    /// <item>串口读是阻塞 IO，只能跑在线程池上，界面更新必须切回 UI 线程；</item>
    /// <item>任何异常都不能让循环退出，只标记该从站的状态。</item>
    /// </list>
    /// </summary>
    public partial class MonitorViewModel : BaseViewModel, IDisposable
    {
        private readonly ISensorReader _reader;
        private readonly SensorOptions _options;
        private readonly Dispatcher? _dispatcher;

        private CancellationTokenSource? _cts;
        private Task? _pollTask;
        private bool _disposed;

        private readonly SKColor blue = new SKColor(25, 118, 210);
        private readonly SKColor red = new SKColor(229, 57, 53);

        private readonly ILogService<MonitorViewModel> _logService;
        private readonly IBusinessLogService _businessLogService;

        [ObservableProperty]
        private TempAndHumiLimtModel _tempAndHumiLimtModel = new TempAndHumiLimtModel();


        public MonitorViewModel(ISensorReader reader, SensorOptions options, ILogService<MonitorViewModel> logService, IBusinessLogService businessLogService)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            this._logService = logService;
            this._businessLogService = businessLogService;

            // 构造发生在 UI 线程，这里拿到的就是界面线程的 Dispatcher。
            // 用 Application.Current 兜一下，方便单元测试时不依赖 Application。
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

            var slaves = options.SlaveIds.Count > 0 ? options.SlaveIds : new byte[] { 1 };
            Slave = new MonitorModel(1, options);

            SampleIntervalText = $"每 {options.PollIntervalMs} ms 采集一次";
            // 注册消息监听
            WeakReferenceMessenger.Default.Register<MainBackgroundChangedMessage>(this, (r, m) =>
            {
                TempAndHumiLimtModel = m.LimtModel; // 收到消息后更新属性
            });
            // 线的数据
            Series = new ObservableCollection<ISeries>
            {
                new LineSeries<DateTimePoint>
                {
                    LineSmoothness = 1,
                    Name = "temp",
                    Values = temperatures,
                    Stroke = new SolidColorPaint(blue, 2),
                    GeometryStroke = new SolidColorPaint(blue, 2),
                    Fill = null,
                    // it will be scaled at the Axis[0] instance (see YAxes property below)
                    ScalesYAt = 0
                },

                new LineSeries<DateTimePoint>
                {
                    Name = "humi",
                    Values = humidities,
                    Stroke = new SolidColorPaint(red, 2),
                    GeometryStroke = new SolidColorPaint(red, 2),
                    Fill = null,
                    // it will be scaled at the YAxes[1] instance (see YAxes property below)
                    ScalesYAt = 1
                }
            };
            // 线的规格
            YAxes = new List<Axis>
            {
                new Axis // the "units" and "tens" series will be scaled on this axis
                {
                    LabelsPaint = new SolidColorPaint(blue),
                    MaxLimit = 70,
                    MinLimit = -40
                    
                },
                new Axis // the "hundreds" series will be scaled on this axis
                {
                    LabelsPaint = new SolidColorPaint(red),
                    ShowSeparatorLines = false,
                    Position = LiveChartsCore.Measure.AxisPosition.End,
                    MaxLimit = 80,
                    MinLimit = 20
                },
            };
            // x轴的展示
            xAxes = new List<Axis>
            {
                new DateTimeAxis(TimeSpan.FromSeconds(1), d => d.ToString("mm:ss"))
                {
                    LabelsPaint = new SolidColorPaint(new SKColor(0x87, 0xE8, 0xEC)),
                    TextSize = 11
                }
            };
        }

        /// <summary>界面上的从站卡片，一一对应 <see cref="SensorOptions.SlaveIds"/>。</summary>
        public MonitorModel Slave { get; }

        /// <summary>是否正在采集。界面上的开关直接绑它。</summary>
        [ObservableProperty] private bool isStartMonitor;

        /// <summary>底部状态栏文案，例如「COM3 · 2/2 从站在线 · 14:03:21」。</summary>
        [ObservableProperty] private string connectionStatus = "未启动";

        [ObservableProperty] private string summaryMessage = "待机";

        [ObservableProperty] private Brush summaryColor = Brushes.SkyBlue;

        [ObservableProperty] private string sampleIntervalText = string.Empty;


        private ObservableCollection<ISeries> series = new ObservableCollection<ISeries>();

        public ObservableCollection<ISeries> Series
        {
            get { return series; }
            set { series = value; }
        }

        private List<Axis> yAxes = new List<Axis>();

        public List<Axis> YAxes
        {
            get { return yAxes; }
            set { yAxes = value; }
        }

        private List<Axis> xAxes;

        public List<Axis> XAxes
        {
            get { return xAxes; }
            set { xAxes = value; }
        }


        [ObservableProperty] private ObservableCollection<DateTimePoint> temperatures = new();

        [ObservableProperty] private ObservableCollection<DateTimePoint> humidities = [];
        [ObservableProperty] private ObservableCollection<DateTimeOffset> timeOffsets = new();

        public bool IsRunning => _cts is { IsCancellationRequested: false };

        /// <summary>
        /// 开关按钮。这是<b>唯一</b>的启停入口：不要再用「IsChecked 双向绑定 + Command」两套状态，
        /// 那样 ToggleButton 会先自己翻转 IsChecked，命令里读到的状态和界面显示的状态就不是一回事了。
        /// </summary>
        [RelayCommand(AllowConcurrentExecutions = false)]
        private async Task ToggleMonitorAsync()
        {
            if (IsRunning)
            {
                await StopAsync();
            }
            else
            {
                Start();
            }

            // ToggleButton 点击时会先自己翻转一次 IsChecked。
            // 命令结束时强制通知一次，让单向绑定把界面拉回真实状态，
            // 避免「命令被并发保护挡掉」时按钮停在错误的位置上。
            OnPropertyChanged(nameof(IsStartMonitor));
        }

        /// <summary>
        /// 开始采集温湿度
        /// </summary>
        private void Start()
        {
            if (_disposed)
            {
                return;
            }

            // 防重入：上一轮还没结束就不要再起一条。
            if (_pollTask is { IsCompleted: false }) // if (_pollTask != null && _pollTask.IsCompleted == false)
            {
                return;
            }
            LogModel logModel = new()
            {
                SlaveId = $"{Slave.SlaveId}号从站",
                Created = DateTime.Now,
                Temperature = 0.0,
                Humidity = 0.0,
                Remark = $"{Slave.SlaveId}号从站开始采集 {_options.PortName}",
                LogLevel = LogLevel.Info,
                State = (int)SlaveState.Running
            };
            _businessLogService.TryAddLogAsync(logModel).ConfigureAwait(false);

            // 每一轮启动都必须用全新的 CTS：
            // 上一轮 StopAsync 已经 Dispose 掉旧的了，复用会在 Register 时抛 ObjectDisposedException。
            _cts = new CancellationTokenSource();


            Slave.Reset();


            IsStartMonitor = true;
            ConnectionStatus = $"正在采集 {_options.PortName}…";
            SummaryColor = Brushes.SkyBlue;
            SummaryMessage = "正在读取…";

            var token = _cts.Token;
            using var activity =
                new Activity("Modbus.ReadRegisters").Start();
            var operationId = activity.SpanId.ToString();
            using (LogContext.PushProperty(
                       "OperationId", operationId))
            {
                // 串口读是同步阻塞 IO，必须放到线程池：在 UI 线程上等它会把整个界面冻住。
                _pollTask = Task.Run(() => PollLoopAsync(token), token);
            }
        }

        /// <summary>
        /// 采集循环。注意它是 <c>Task</c> 而不是 <c>async void</c>：
        /// async void 的异常无法被观察，而且调用方拿不到「循环什么时候真的结束」。
        /// </summary>
        private async Task PollLoopAsync(CancellationToken token)
        {
            var threadId = Environment.CurrentManagedThreadId;
            _logService.Info("线程{threadId}开始采集数据", threadId);
            var interval = TimeSpan.FromMilliseconds(Math.Max(200, _options.PollIntervalMs));

            // 用 PeriodicTimer 而不是「读完再 Task.Delay(1000)」：
            // 后者会把周期变成「读取耗时 + 1000ms」，总线一慢就越跑越慢；
            // PeriodicTimer 按固定节拍触发，单次超时也不会拖累后面的节奏。
            using var timer = new PeriodicTimer(interval);

            try
            {
                do
                {
                    // 循环读取
                    // ConfigureAwait(false)这个方法的意思是防止任务执行完成之后又回到ui线程了，所以直接继续跑
                    // 判断口诀："await 之后要不要碰 UI？" 要 → 不写；不要 → 写 .ConfigureAwait(false)。
                    await PollOnceAsync(token).ConfigureAwait(false);
                } while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false));
            }
            catch (OperationCanceledException)
            {
                // 正常停止路径，什么都不用做。
                _logService.Alarm( "发生异常，跳过");
            }
            catch (Exception ex)
            {
                _logService.Error("发生异常,异常信息为{Message}", ex, ex.Message);
                LogModel logModel = new()
                {
                    SlaveId=$"{Slave.SlaveId}号从站",
                    Created=DateTime.Now,
                    Temperature=0.0,
                    Humidity=0.0,
                    Remark=$"发生异常,异常信息为{ex.Message}",
                    LogLevel=LogLevel.Error,
                    State=(int)SlaveState.Running
                };
                await _businessLogService.TryAddLogAsync(logModel, token).ConfigureAwait(false);
                await PostToUiAsync(() =>
                {
                    _logService.Error("采集循环异常退出,异常信息为{message}", ex, ex.Message);
                    ConnectionStatus = "采集循环异常退出";
                    SummaryColor = Brushes.OrangeRed;
                    SummaryMessage = ex.Message;
                }).ConfigureAwait(false);
            }
        }

        /// <summary>采集一轮：依次读所有从站。单个从站失败不影响其他从站。</summary>
        /// 这里的Task不是说返回值的，是需要等待任务执行完成才会结束任务的意思。
        /// 如果需要返回值的话就是Task<T>这样的形式了
        private async Task PollOnceAsync(CancellationToken token)
        {
            var onlineCount = 0;

            // 判断是否被停止了，如果已经停止的了话就不会继续进行串口通信，所以写在前面
            token.ThrowIfCancellationRequested();

            try
            {
                _logService.Info("开始读取数据");
                var reading = await _reader.ReadAsync(Slave.SlaveId, token).ConfigureAwait(false);
                onlineCount++;
                _logService.Info("读取数据成功");
                // 在后台线程改 ObservableObject 的属性，界面要么收不到、要么在错误的线程上刷新控件，
                // 所以统一切回 UI 线程再赋值。
                AddPoint(reading);
                await PostToUiAsync(() => Slave.ApplyReading(reading)).ConfigureAwait(false);
                await TrackTransition(reading, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 超时、CRC 错、从站异常、串口被拔……都走这里。
                // 吞掉异常是对的（不能让循环退出），但要留下可见的状态，不能像原来那样静默。
                _logService.Error("发生异常,异常信息为：{message}" ,ex, ex.Message);
                LogModel logModel = new()
                {
                    SlaveId = $"{Slave.SlaveId}号从站",
                    Created = DateTime.Now,
                    Temperature = 0.0,
                    Humidity = 0.0,
                    Remark = $"发生异常,异常信息为{ex.Message}",
                    LogLevel = LogLevel.Error,
                    State = (int)SlaveState.Running
                };
                await _businessLogService.TryAddLogAsync(logModel, token).ConfigureAwait(false);
                await PostToUiAsync(() => Slave.ApplyFailure(ex)).ConfigureAwait(false);
            }


            await PostToUiAsync(() =>
            {
                var message = onlineCount > 0 ? "从站在线" : "从站离线";
                ConnectionStatus = $"{_options.PortName} · {message} · {DateTime.Now:HH:mm:ss}";
                UpdateSummary();
                
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// 温湿度读取成功之后的判定以及日志的插入
        /// </summary>
        /// <param name="reading"></param>
        private async Task TrackTransition(TemperatureHumidityReading reading, CancellationToken token)
        {
            string remark = string.Empty;
            LogLevel logLevel = LogLevel.Info;
            SlaveState state = SlaveState.Running;
            if (Slave.HasAlarm)
            {
                remark = $"{Slave.Title}：{Slave.AlarmMessage}";
                logLevel = LogLevel.Warning;
            }
           
            LogModel logModel = new()
            {
                SlaveId =$"{Slave.SlaveId}号从站",
                Temperature = reading.Temperature,
                Humidity = reading.Humidity,
                Remark = remark,
                Created=DateTime.Now,
                LogLevel = logLevel,
                State = (int)state,
            };
            await _businessLogService.TryAddLogAsync(logModel, token).ConfigureAwait(false);
            
        }

        private void AddPoint(TemperatureHumidityReading reading)
        {
            Humidities.Add(new DateTimePoint(DateTime.Now, reading.Temperature));
            Temperatures.Add(new DateTimePoint(DateTime.Now, reading.Humidity));
            if (Humidities.Count > 100)
            {
                Humidities.RemoveAt(0);
                Temperatures.RemoveAt(0);
            }
        }

        /// <summary>把各从站状态汇总成页头的一句话。</summary>
        private void UpdateSummary()
        {

            if (Slave.SlaveId == 0)
            {
                SummaryColor = Brushes.Gray;
                SummaryMessage = "未配置从站";
                _logService.Alarm(SummaryMessage);
                return;
            }

            var offline = Slave.IsOnline;
            if (!offline)
            {
                SummaryColor = Brushes.OrangeRed;
                SummaryMessage = "从站通信异常";
                _logService.Error(SummaryMessage);
                return;
            }

            var alarmed = Slave.HasAlarm;
            if (alarmed)
            {
                SummaryColor = Brushes.OrangeRed;
                SummaryMessage = $"{Slave.Title}：{Slave.AlarmMessage}";
                _logService.Alarm(SummaryMessage);
                return;
            }

            SummaryColor = Brushes.LimeGreen;
            SummaryMessage = "一切正常";
        }

        /// <summary>
        /// 停止并等待循环真正退出。顺序不能反：
        /// 先 Cancel → 等 Task 结束 → 最后 Dispose CTS。
        /// 原代码是 Cancel 之后立刻 Dispose，循环里的 token 变成已释放对象，
        /// Task.Delay / ThrowIfCancellationRequested 会抛 ObjectDisposedException（async void 里就是直接崩进程）。
        /// </summary>
        public async Task StopAsync()
        {
            _logService.Info("进行停止采集");
            LogModel logModel = new()
            {
                SlaveId = $"{Slave.SlaveId}号从站",
                Created = DateTime.Now,
                Temperature = 0.0,
                Humidity = 0.0,
                Remark = $"{Slave.SlaveId}号从站停止采集",
                LogLevel = LogLevel.Info,
                State = (int)SlaveState.Running
            };
            await _businessLogService.TryAddLogAsync(logModel).ConfigureAwait(false);
            var cts = _cts;
            var task = _pollTask;

            if (cts is null)
            {
                IsStartMonitor = false;
                return;
            }

            IsStartMonitor = false;
            cts.Cancel();

            if (task is not null)
            {
                try
                {
                    // 这里故意不 ConfigureAwait(false)：StopAsync 从 UI 命令进来，
                    // 后面的 UI 赋值需要回到 UI 线程。
                    await task;
                }
                catch (OperationCanceledException)
                {
                    // 正常取消
                }
            }

            cts.Dispose();
            _cts = null;
            _pollTask = null;

            ConnectionStatus = "已停止";
            SummaryColor = Brushes.Gray;
            SummaryMessage = "已停止采集";
            _logService.Info("已停止采集");
        }

        /// <summary>
        /// 只发取消信号、不等结果。给「窗口正在关闭」这种没时间 await 的场合用：
        /// 取消后循环会在下一个检查点退出，进程结束时由系统回收串口句柄。
        /// </summary>
        public void Cancel()
        {
            IsStartMonitor = false;
            _cts?.Cancel();
        }

        /// <summary>在 UI 线程执行；已经在 UI 线程就直接跑，避免多一次调度。</summary>
        private Task PostToUiAsync(Action action)
        {
            var dispatcher = _dispatcher;

            if (dispatcher is null || dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            // 界面已经在关：不再往里塞更新，否则 InvokeAsync 可能永远等不到执行。
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action).Task;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Cancel();
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TempAndHumiTest.Services;

namespace TempAndHumiTest.Models
{
    /// <summary>
    /// 一个从站在界面上的状态：实时值、通信状态、报警文案。
    /// 所有赋值都由 <c>MonitorViewModel</c> 切回 UI 线程后执行，所以这里不需要加锁。
    /// </summary>
    public partial class MonitorModel : ObservableObject
    {
        private static readonly Brush NormalBrush = CreateBrush(0x4C, 0xAF, 0x50);
        private static readonly Brush AlarmBrush = CreateBrush(0xF4, 0x43, 0x36);
        private static readonly Brush OfflineBrush = CreateBrush(0x9E, 0x9E, 0x9E);

        private readonly SensorOptions _options;

        public MonitorModel(byte slaveId, SensorOptions options)
        {
            SlaveId = slaveId;
            _options = options ?? throw new ArgumentNullException(nameof(options));
            Title = $"{slaveId} 号从站";
           
        }

        public byte SlaveId { get; }

        public string Title { get; }

        [ObservableProperty]
        private double temperature;

        [ObservableProperty]
        private double humidity;

        [ObservableProperty]
        private bool isOnline;

        /// <summary>通信状态文案，例如「在线 · 14:03:21」「通信异常 · 连续 3 次」。</summary>
        [ObservableProperty]
        private string statusText = "未采集";

        /// <summary>状态颜色：绿色正常 / 红色报警 / 灰色离线。</summary>
        [ObservableProperty]
        private Brush statusColor = OfflineBrush;

        /// <summary>报警文案，例如「温度偏高 38.2℃」。</summary>
        [ObservableProperty]
        private string alarmMessage = "待机";

        [ObservableProperty]
        private bool hasAlarm;

        [ObservableProperty]
        private DateTimeOffset? lastUpdated;

        /// <summary>连续失败次数。恢复成功后自动归零。</summary>
        [ObservableProperty]
        private int failureCount;


        /// <summary>采集成功：更新数值并重新判定报警。</summary>
        public void ApplyReading(TemperatureHumidityReading reading)
        {
            Temperature = reading.Temperature;
            Humidity = reading.Humidity;
            LastUpdated = reading.Timestamp;
            FailureCount = 0;
            IsOnline = true;
            StatusText = $"在线 · {reading.Timestamp:HH:mm:ss}";
           
            EvaluateAlarm();
        }

        /// <summary>采集失败：保留上一次的数值（不要让仪表瞬间掉到 0），只把状态标红。</summary>
        public void ApplyFailure(Exception error)
        {
            FailureCount++;
            IsOnline = false;
            HasAlarm = false;
            StatusColor = OfflineBrush;
            StatusText = FailureCount == 1
                ? "通信异常"
                : $"通信异常 · 连续 {FailureCount} 次";
            AlarmMessage = Describe(error);
        }

        /// <summary>开始新一轮监控前把状态清空。</summary>
        public void Reset()
        {
            FailureCount = 0;
            IsOnline = false;
            HasAlarm = false;
            StatusColor = OfflineBrush;
            StatusText = "未采集";
            AlarmMessage = "待机";
            LastUpdated = null;
        }

        private void EvaluateAlarm()
        {
            var problems = new List<string>(2);

            if (Temperature < _options.TemperatureLow)
            {
                problems.Add($"温度偏低 {Temperature:0.0}℃");
            }
            else if (Temperature > _options.TemperatureHigh)
            {
                problems.Add($"温度偏高 {Temperature:0.0}℃");
            }

            if (Humidity < _options.HumidityLow)
            {
                problems.Add($"湿度偏低 {Humidity:0.0}%");
            }
            else if (Humidity > _options.HumidityHigh)
            {
                problems.Add($"湿度偏高 {Humidity:0.0}%");
            }

            HasAlarm = problems.Count > 0;

            if (HasAlarm)
            {
                StatusColor = AlarmBrush;
                AlarmMessage = string.Join("，", problems);
            }
            else
            {
                StatusColor = NormalBrush;
                AlarmMessage = "一切正常";
            }
        }

        private static string Describe(Exception error)
        {
            if (error.InnerException is NModbus.SlaveException)
            {
                return "从站返回异常码，请核对寄存器地址";
            }

            return error switch
            {
                TimeoutException => "传感器无响应（超时）",
                UnauthorizedAccessException => "串口被占用，请关掉其他串口工具",
                IOException => "串口读写失败，请检查接线与驱动",
                ObjectDisposedException => "串口已释放",
                _ => "通信失败",
            };
        }

        private static Brush CreateBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}

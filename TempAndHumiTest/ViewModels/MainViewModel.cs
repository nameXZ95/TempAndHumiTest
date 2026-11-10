using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using TempAndHumiTest.Common;
using TempAndHumiTest.Models;
using TempAndHumiTest.Services;
using TempAndHumiTest.Views;

namespace TempAndHumiTest.ViewModels
{
    /// <summary>
    /// 主窗口：负责页面切换、时钟、退出。
    /// </summary>
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly IApplicationService _applicationService;
        private readonly IDialogService _dialogService;
        private readonly MonitorViewModel _monitorViewModel;
        private readonly Dictionary<ViewEnum, BaseViewModel> _pages = new Dictionary<ViewEnum, BaseViewModel>();
        private readonly DispatcherTimer _timer;
        private bool _disposed;
        private IBusinessLogService _businessLogService;

        private SettingsViewModel _settingsViewModel;

        [ObservableProperty]
        private Brush backgroundBrush = (SolidColorBrush)new BrushConverter().ConvertFrom("#1A1E27")!; // 默认背景

        [ObservableProperty]
        private BaseViewModel currentViewModel;

        [ObservableProperty]
        private bool _settingsWindowOpen;

        [ObservableProperty]
        private DateTime currentTime = DateTime.Now;

        public IRelayCommand<object?> NavigateCommand { get; }

        public IRelayCommand ExitCommand { get; }

        public IRelayCommand OpenSettings { get; }

        public UserModel LoginUser { get; set; } = new UserModel();

        // 直接返回 Visibility 枚举
        public Visibility AdminVisibility => LoginUser.Role == OperateRoleEnum.Admin ? Visibility.Visible : Visibility.Collapsed;

        public MainViewModel(
            IApplicationService applicationService,
            IDialogService dialogService,
            MonitorViewModel monitorViewModel,
            LogViewModel logViewModel,
            DataViewModel dataViewModel,
            UserManageViewModel userManageViewModel,
            IBusinessLogService businessLogService,
            SettingsViewModel settingsViewModel)
        {
            this._settingsViewModel = settingsViewModel;
            _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _monitorViewModel = monitorViewModel ?? throw new ArgumentNullException(nameof(monitorViewModel));

            

            // 注册消息监听
            //WeakReferenceMessenger.Default.Register<MainBackgroundChangedMessage>(this, (r, m) =>
            //{
            //    BackgroundBrush = m.NewBackground; // 收到消息后更新属性
            //});
            // 登陆成功之后才触发
            WeakReferenceMessenger.Default.Register<MainBackgroundChangedMessage>(this, (r, m) =>
            {
                if (m.User is null) return;
                
                LoginUser = m.User;
                // 页面只创建一次。原来每次点导航都 new 一个 MonitorViewModel，
                // 结果是：切一次页面就多一条采集循环，几条循环同时抢 COM3，谁读到什么全凭运气。
                OnPropertyChanged(nameof(AdminVisibility)); // 改变的时候要通知一下

                InitializePages(monitorViewModel, logViewModel, dataViewModel, userManageViewModel);
            });

            currentViewModel = monitorViewModel;

            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1),
            };
            _timer.Tick += OnTimerTick;
            _timer.Start();

            NavigateCommand = new RelayCommand<object?>(OnNavigate);
            ExitCommand = new RelayCommand(Exit);
            OpenSettings = new RelayCommand(OpenSetting);
            _businessLogService = businessLogService;

        }

        private void InitializePages(MonitorViewModel monitorViewModel, LogViewModel logViewModel, DataViewModel dataViewModel, UserManageViewModel userManageViewModel)
        {
            if (LoginUser.Role == OperateRoleEnum.Admin)
            {
                this._pages.Clear();
                this._pages.Add(ViewEnum.MonitorView1, monitorViewModel);

                this._pages.Add(ViewEnum.LogView, logViewModel);
                this._pages.Add(ViewEnum.DataView, dataViewModel);
                this._pages.Add(ViewEnum.UserManageView, userManageViewModel);
                
            }
            else if (LoginUser.Role == OperateRoleEnum.Operator)
            {
                this._pages.Clear();
                this._pages.Add(ViewEnum.MonitorView1, monitorViewModel);

                this._pages.Add(ViewEnum.UserManageView, userManageViewModel);
                
            }
            else if (LoginUser.Role == OperateRoleEnum.Visitor)
            {
                this._pages.Clear();
                this._pages.Add(ViewEnum.MonitorView1, monitorViewModel);

            }
        }

        private void OpenSetting()
        {
            _settingsViewModel.IsOpenDialog = false;      // 每次打开前复位，供窗口内部使用
            // 打开弹窗需要创建一下，不然的话就不会弹出来
            var window = new Components.Settings(_settingsViewModel)
            {
                // 这里不写owner的话就不会成为弹窗，而是一个嵌入页面的窗体
                Owner = System.Windows.Application.Current.MainWindow,  // CenterOwner 需要它
            };
            
            window.ShowDialog();


        }

        private void OnTimerTick(object? sender, EventArgs e) => CurrentTime = DateTime.Now;

        private void OnNavigate(object? parameter)
        {
            if (parameter is not string name)
            {
                return;
            }

            // 用 TryParse 而不是 Enum.Parse：参数写错时不应该抛异常打断界面。
            if (!Enum.TryParse<ViewEnum>(name, ignoreCase: true, out var view))
            {
                return;
            }

            if (_pages.TryGetValue(view, out var page))
            {
                CurrentViewModel = page;
            }
        }

        private void Exit()
        {
            if (_dialogService.ShowConfirm("确定要退出吗？") != DialogResult.Yes)
            {
                return;
            }
            LogModel logModel = new()
            {
                SlaveId = string.Empty,
                Created = DateTime.Now,
                Temperature = 0.0,
                Humidity = 0.0,
                Remark = "退出程序",
                LogLevel = LogLevel.Info,
                State = (int)SlaveState.Stop
            };
            try
            {
                _ = _businessLogService.TryAddLogAsync(logModel);   // 显式丢弃 Task
            }
            catch (Exception logEx)
            {
                // async 方法在第一个 await 之前的同步异常会在这里抛出
                Debug.WriteLine($"[日志写入失败] {logEx}");
            }

            PrepareShutdown();
            _applicationService.Shutdown();
        }

        /// <summary>
        /// 窗口关闭时调用：只发取消信号，不等待。
        /// 在 UI 线程上同步等一条可能卡在串口读上的循环，是典型的关窗卡死写法。
        /// </summary>
        public void PrepareShutdown() => _monitorViewModel.Cancel();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _timer.Stop();
            _timer.Tick -= OnTimerTick;
            PrepareShutdown();
        }
    }
}

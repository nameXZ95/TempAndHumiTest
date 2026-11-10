using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TempAndHumiTest.Common;
using TempAndHumiTest.Services;

namespace TempAndHumiTest.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        public IApplicationService _applicationService { get; set; }
        public IDialogService _dialogService { get; set; }
        private readonly DispatcherTimer _timer; // 时间

        private BaseViewModel _currentViewModel;
        public BaseViewModel CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                _currentViewModel = value;
                OnPropertyChanged();
            } // OnPropertyChanged 由基类实现
        }

        public ICommand NavigateCommand { get; }

        [ObservableProperty]
        private DateTime currentTime = DateTime.Now;

        public ICommand ExitCommand { get; }

        public MainViewModel(IApplicationService applicationService, IDialogService dialogService)
        {
            _applicationService = applicationService;
            _dialogService = dialogService;

            CurrentViewModel = new MonitorViewModel();

            _timer = new DispatcherTimer() { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => CurrentTime = DateTime.Now;
            _timer.Start();

            ExitCommand = new RelayCommand(() =>
            {
                var result = _dialogService.ShowConfirm("确定要退出吗？");
                if (result == Services.DialogResult.Yes)
                {
                    _applicationService.Shutdown();
                }
            });

            NavigateCommand = new RelayCommand<object>(OnNav);
        }

        private void OnNav(object obj)
        {
            var view = Enum.Parse(typeof(ViewEnum), (string)obj, true);
            switch (view)
            {
                case ViewEnum.MonitorView:
                    CurrentViewModel = new MonitorViewModel();
                    break;
                case ViewEnum.LogView:
                    CurrentViewModel = new LogViewModel();
                    break;
                case ViewEnum.DataView:
                    CurrentViewModel = new DataViewModel();
                    break;
                case ViewEnum.UserManageView:
                    CurrentViewModel = new UserManageViewModel();
                    break;
                default:
                    CurrentViewModel = new MonitorViewModel();
                    break;
            }
        }
    }
}

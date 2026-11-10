using System;
using System.Windows;
using TempAndHumiTest.ViewModels;

namespace TempAndHumiTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = viewModel;
        }

        protected override void OnClosed(EventArgs e)
        {
            // 先发取消信号让采集循环退出，再让 App.OnExit 释放服务容器。
            // 这里只「通知」不「等待」，否则关窗会卡在一次正在进行的串口读上。
            _viewModel.PrepareShutdown();

            base.OnClosed(e);
        }
    }
}

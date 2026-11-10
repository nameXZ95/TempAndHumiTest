using System.ComponentModel.Design;
using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TempAndHumiTest.Services;
using TempAndHumiTest.ViewModels;

namespace TempAndHumiTest
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider Service { get; set; }
        public App() {

            ServiceCollection collection = new ServiceCollection();

            // 注册服务
            collection.AddSingleton<IApplicationService, ApplicationService>();
            collection.AddSingleton<IDialogService,DialogService>();

            collection.AddSingleton<MonitorViewModel>();
            collection.AddSingleton<LogViewModel>();
            collection.AddSingleton<DataViewModel>();
            collection.AddSingleton<UserManageViewModel>();
            // 注册viewModel
            collection.AddSingleton<MainViewModel>();
            // 注册mainwindow
            collection.AddSingleton<MainWindow>();

            Service = collection.BuildServiceProvider();

        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            var mainWindow = Service.GetRequiredService<MainWindow>();

            MainWindow = mainWindow;
            mainWindow.Show();
        }



    }

}

using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using TempAndHumiTest.Common;
using TempAndHumiTest.Data;
using TempAndHumiTest.Models;
using TempAndHumiTest.Services;
using TempAndHumiTest.ViewModels;
using TempAndHumiTest.Views;

namespace TempAndHumiTest
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider Services { get; }

        
        const string Template = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} " +
                            "[{Level:u3}] [OperationId:{OperationId}] {SourceContext} - " +
                            "{Message:lj}{NewLine}{Exception}";

        public App()
        {
            var configuration = new ConfigurationBuilder()
                                .SetBasePath(AppContext.BaseDirectory)      // 指向 exe 所在目录
                                .AddJsonFile("AppConfiguration.json", optional: false, reloadOnChange: true)
                                .Build();
            string dataDir = "logs";
            if (!Directory.Exists(dataDir)) 
            { 
                Directory.CreateDirectory(dataDir);
            }

            string logPath = Path.Combine(dataDir, "log.db");
            var connectionStr = new SqliteConnectionStringBuilder()
            {
                DataSource = logPath,
            }.ToString();

            var collection = new ServiceCollection();

            // 采集参数（串口 / 从站 / 寄存器 / 报警阈值）集中在一个对象里，换硬件只改这里。
            collection.AddSingleton(new SensorOptions());
            collection.AddSingleton(new AppSettings());

            // 串口主站与采集层：单例，整个程序共用同一个 COM 口。
            collection.AddSingleton<ModbusService>();
            collection.AddSingleton<ISensorReader, SensorReader>();

            // 服务
            collection.AddSingleton<IApplicationService, ApplicationService>();
            collection.AddSingleton<IDialogService, DialogService>();
            collection.AddSingleton<IBusinessLogService, BusinessLogService>();
            collection.AddSingleton<IAuthService, AuthService>();
            
            // 数据库的db文件地址
            collection.AddSingleton(new DatabaseInitializer(connectionStr));

            // 日志
            collection.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(new LoggerConfiguration()
                    .MinimumLevel.Debug() // 日志级别
                    .Enrich.FromLogContext() //  LogContext.PushProperty 生效
                    .Enrich.WithProperty("OperationId", "-") // 兜底的防止数据是空的
                    .WriteTo.File("logs/app-.log",
                    rollingInterval: RollingInterval.Day,
                        // 最多保留 30 个滚动日志文件
                        retainedFileCountLimit: 30,

                        // 单个文件达到约 10 MB 时也进行滚动
                        fileSizeLimitBytes: 10 * 1024 * 1024,
                        rollOnFileSizeLimit: true,
                        // 日志格式
                        outputTemplate: Template
                    )// 数据库日志：批量写入 SQLite 待优化
                    //.WriteTo.SQLite(
                    //    sqliteDbPath: "logs/logs-.db",
                    //    tableName: "Logs",
                    //    storeTimestampInUtc: false,
                    //    retentionPeriod:
                    //        TimeSpan.FromDays(30),
                    //    batchSize: 10,
                    //    maxDatabaseSize: 500,
                    //    rollOver: true)
                    .WriteTo.Debug(outputTemplate: Template)     // VS「输出」窗口，调试用
                    .WriteTo.Console(outputTemplate: Template)   // 有控制台时
                    .CreateLogger(), dispose: true); // dispose: true 确保退出时 flush

            });
            collection.AddSingleton(typeof(ILogService<>), typeof(LogService<>));
            // 带参数的注入方式
            collection.AddSingleton<IBusinessLogRepository>(sp =>
                 new BusinessLogRepository(
                     connectionString: connectionStr,
                     logService: sp.GetRequiredService<ILogService<BusinessLogService>>()
                 ));

            collection.AddSingleton<IAuthRepository, AuthRepository>();

            // ViewModel：注意 MonitorViewModel 是单例，
            // 页面切换复用它，不会每次导航都新起一条采集循环。
            collection.AddSingleton<MonitorViewModel>();
            collection.AddSingleton<LogViewModel>();
            collection.AddSingleton<DataViewModel>();
            collection.AddSingleton<UserManageViewModel>();
            collection.AddSingleton<MainViewModel>();
            collection.AddSingleton<SettingsViewModel>();
            collection.AddSingleton<LoginViewModel>();

            // 窗口
            collection.AddSingleton<MainWindow>();
            collection.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"))
                .LogTo(msg => System.Diagnostics.Debug.WriteLine(msg), Microsoft.Extensions.Logging.LogLevel.Information); // 输出到控制台;
            });

            Services = collection.BuildServiceProvider();
            
            // 兜底：别让一个没接住的异常直接把窗口吃掉，什么都不提示。
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            // 数据库先初始化
            var databaseInitializer = Services.GetRequiredService<DatabaseInitializer>();
            databaseInitializer.Initialize();
            var loginViewModel = Services.GetRequiredService<LoginViewModel>();
            var login = new LoginWindow(loginViewModel);
            var mainWindow = Services.GetRequiredService<MainWindow>();

            MainWindow = mainWindow;
            if (login.ShowDialog() == true)
            {
                // 这个是登陆成功的逻辑
                mainWindow.Show();
            }
            else
            {
                Shutdown(); // 用户直接关了登录窗或点击取消，程序退出
            }
            //mainWindow.Show();

            var _businessLogService = Services.GetService<IBusinessLogService>();
            if (_businessLogService is not null)
            {
                var logModel = new LogModel
                {
                    SlaveId = string.Empty,
                    Created = DateTime.Now,
                    Temperature = 0.0,
                    Humidity = 0.0,
                    Remark = "开启程序",
                    LogLevel = Common.LogLevel.Info,
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
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Debug.WriteLine($"[UI 未处理异常] {e.Exception}");
            e.Handled = true;
            var _businessLogService = Services.GetService<IBusinessLogService>();
            if (_businessLogService is not null)
            {
                var logModel = new LogModel
                {
                    SlaveId = string.Empty,
                    Created = DateTime.Now,
                    Temperature = 0.0,
                    Humidity = 0.0,
                    Remark = $"程序遇到未处理的异常：\n\n{e.Exception.Message}",
                    LogLevel = Common.LogLevel.Alarm,
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
            }
            else
            {
                Debug.WriteLine("[日志跳过] IBusinessLogService 未解析到（容器可能已释放或初始化失败）");
            }
            MessageBox.Show(
                $"程序遇到未处理的异常：\n\n{e.Exception.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            // 后台任务忘 await 时异常会落到这里，标记为已观察，避免进程被终结。
            Debug.WriteLine($"[Task 未观察异常] {e.Exception}");
            e.SetObserved();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 先叫停采集循环，再释放容器（串口在容器里）。
            Services.GetService<MonitorViewModel>()?.Cancel();

            // 容器会顺带 Dispose 掉 MainViewModel / SensorReader / ModbusService。
            (Services as IDisposable)?.Dispose();
            Log.CloseAndFlush(); // 日志缓冲
            base.OnExit(e);
        }
    }
}

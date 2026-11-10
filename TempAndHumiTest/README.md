**此项目是练手项目，所以有些部分未完成，学习上位机里面的wpf、mvvm、di、Entity Framework Core等知识以及一些页面xaml的编写。**

├─ TempAndHumiTest\                         # WPF 应用项目（.NET 9）
   ├─ Assets\
   │  ├─ Fonts\
   │  ├─ Images\
   │  └─ Styles\
   ├─ Common\
   ├─ Data\                                 # 数据库上下文、初始化及仓储相关
   ├─ Models\
   ├─ Services\                             # Modbus、认证、日志、对话框等服务
   ├─ ViewModels\
   ├─ Views\
   │  ├─ LoginWindow.xaml
   │  ├─ MonitorView.xaml
   │  ├─ LogView.xaml
   │  ├─ DataView.xaml
   │  └─ UserManageView.xaml
   ├─ App.xaml
   ├─ App.xaml.cs
   ├─ AppConfiguration.json
   ├─ MainWindow.xaml
   ├─ MainWindow.xaml.cs
   ├─ TempAndHumiTest.csproj
   └─ README.md

# TempAndHumiTest

基于 WPF 和 MVVM 的温湿度监控桌面应用，使用 Modbus RTU（RS-485）采集传感器数据，并提供监控、曲线展示、用户登录和日志等功能。

## 技术栈

- .NET 9、WPF
- MVVM：CommunityToolkit.Mvvm
- Modbus RTU：NModbus、SerialPort
- 图表：LiveCharts2
- 用户数据：Entity Framework Core、SQL Server
- 业务日志：SQLite
- 应用运行日志：Serilog
- 第三方工具： handycontrol,使用里面的DateTimePicker,但是项目的一些输入框以及选择框的样式都被改了

## 功能概览

- 通过串口读取 Modbus RTU 设备的温度和湿度寄存器。
- 支持周期采集、超时、重试、通信异常恢复和停止采集。
- 使用 LiveCharts 展示温湿度趋势。
- 提供登录认证，并根据用户角色控制页面导航。
- 提供业务日志和应用运行日志。
- 项目包含监控、日志、数据、用户管理和设置相关页面；各页面的业务完整度以当前实现为准。

## 项目结构

- `Assets/`：字体、图片及样式资源。
- `Common/`：通用功能，例如业务日志数据库初始化。
- `Data/`：EF Core 数据库上下文等数据访问组件。
- `Models/`：用户、监控及其他业务模型。
- `Services/`：Modbus、传感器读取、认证、日志和应用服务。
- `ViewModels/`：页面状态、命令及业务协调逻辑。
- `Views/`：WPF 页面和窗口。
- `App.xaml`、`App.xaml.cs`：应用资源、依赖注入及启动配置。
- `MainWindow.xaml`、`MainWindow.xaml.cs`：主窗口。
- `AppConfiguration.json`：应用配置文件。
- `TempAndHumiTest.csproj`：项目及 NuGet 包配置。

## 采集流程

`MonitorViewModel` 管理采集循环和启停(已弃用，不太好看)；`SensorReader` 负责读取并解析寄存器值；`ModbusService` 负责串口通信、IO 串行化、重试和连接重建。

    MonitorViewModel
      └─ SensorReader
          └─ ModbusService
              └─ NModbus RTU Master / SerialPort

采集参数由 `Services/SensorOptions.cs` 集中管理。当前默认配置包括 `COM3`、9600-8-N-1、从站地址 1 和 2，以及 1000 ms 采集间隔。寄存器地址按 0 基填写；实际使用前请依据传感器手册核对串口参数、地址和数值缩放规则。

## 配置

### SQL Server 用户数据

`AppConfiguration.json` 中的 `ConnectionStrings:DefaultConnection` 用于配置 SQL Server 连接。运行前请确认：

- SQL Server 实例可访问，连接字符串正确。
- 数据库及用户表已按项目模型准备好。
- 运行环境具有相应的数据库访问权限。

不要将生产环境凭据提交到版本控制。

### SQLite 业务日志

业务日志数据库使用 `logs/log.db`。应用会在工作目录下使用 `logs/` 保存相关文件，请确保进程具有读写权限。

### 应用运行日志

Serilog 会写入 `logs/app-*.log`，并配置 Debug 输出和控制台输出。日志文件按日期滚动。

## 构建与运行

需要安装 .NET 9 SDK，并在 Windows 环境中运行 WPF 应用。在仓库根目录执行：

    dotnet restore .\TempAndHumiTest\TempAndHumiTest.csproj
    dotnet build .\TempAndHumiTest\TempAndHumiTest.csproj
    dotnet run --project .\TempAndHumiTest\TempAndHumiTest.csproj

要使用设备采集功能，还需要 RS-485 转换器、已连接的 Modbus RTU 传感器，以及与 `SensorOptions` 相匹配的设备配置。

## 注意事项

- 连接设备前核对从站地址、寄存器地址、波特率、校验位及寄存器缩放系数。
- 当前 SQL Server 用户数据和 SQLite 业务日志是两套独立存储。
- 当前代码中的数据库初始化调用没有等待异步操作完成；建议后续改为可等待的异步启动流程，并在初始化完成后再显示登录窗口。
- 项目未在此文档中假定存在默认用户、自动数据库迁移或默认设备配置；部署前应检查实际数据库和硬件环境。
- 当前项目还有监控页面、用户管理页面、设置页面等未完成，只是当作是练手的项目，用来学习wpf、mvvm、di以及数据库相关的操作的。

## 学习笔记
- 在使用MouseLeftButtonDown的时候需要给选中的标签加上背景色，不然的话实现的函数就不会生效
- 在xaml里面写一些输入框或者是选择框的样式，其实本质上就是给这个框重新选个模板
- c#里面的枚举实际上就是一个变量，不是像Java里面的那样是一个对象
- 在使用mvvm的时候需要灵活使用，当有一些是可以使用code-behind的时候可以选择使用，案例可以看EnterKeyDownEvent，就是监听按键的，这种事件写在code-behind更方便
- 所有的引用都要导包，要是发现引用没有生效就看下路径是不是不对
- 绑定的几种模式在我这个项目里面使用的都是按照默认的来的，但是我看官网上面说默认的并不就是twoway，所以如果有需要的话还是看下具体的场景来具体写上绑定模式
- 在使用多线程的Task+async/await的时候，有一个地方需要注意，那就是当我们的任务执行完成之后是否需要返回ui线程来执行ui线程，如果说我们不需要执行ui线程的话-也就是不需要更新ui的操作，那么就加上ConfigureAwait(false);这一块，可以让线程继续执行下去。
- 为什么使用async/await的时候需要在方法上面把返回值设为Task而不是void呢？因为使用void 的话那么异常就会被掩盖掉了，而使用Task的话那么异常就会在调用方await的时候被抛出来，这样链路就会很清晰了。
- CancellationToken就是取消令牌，在使用的时候如果是被取消了，那么这个令牌的链路上会被监听到，那么后面的链路就不会执行
        
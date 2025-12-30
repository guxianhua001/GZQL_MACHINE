using AxisConfiguration.Services;
using Core.Abstraction;
using Core.Abstraction.Factories;
using Core.Abstractions.IConfiguration;
using Core.Abstractions.Plugins;
using Core.Configuration;
using Core.Services;
using Core.Utilities;
using Framework.ViewModels;
using Framework.Views;
using HSMS;
using Interfaces;
using Interfaces.Services;
using LogViewer;
using MaterialDesignThemes.Wpf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModuleCore.ViewModels;
using ModuleCore.Views;
using NLog;
using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Services.Dialogs;
using Prism.Unity;
using Recipe;
using Recipe.Interfaces;
using Recipe.Plugin;
using Recipe.Services;
using SmarterMotion;
using Stations;
using Stations.Service;
using Stations.Services;
using Stations.ViewModels;
using Stations.Views;
using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using TCPLib.Factories;
using TCPLib.Services;
using Unity;

namespace MainApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        // =============================================
        // 字段声明
        // =============================================
        private IConfiguration _configuration;
        private NeedleViewModel _needleViewModelRef;
        private EquipmentStatus _deviceConfigViewModelRef;
        private IntPtr _prevExceptionFilter;

        // =============================================
        // 构造函数
        // =============================================
        public App()
        {
            // 托管异常捕获
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            this.Exit += App_Exit;
            this.Startup += App_Startup;

            // Windows SetUnhandledExceptionFilter API
            InitializeNativeExceptionHandling();

            // 添加诊断日志
            LogStartupInfo();
        }

        // =============================================
        // 应用生命周期方法
        // =============================================
        protected override Window CreateShell()
        {
            return Container.Resolve<Views.MainWindow>();
        }

        protected override void Initialize()
        {
            BuildConfiguration(); // 初始化配置
            base.Initialize();
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            // 初始化服务依赖关系
            InitializeServiceDependencies();
            // 初始化配置
            InitializeConfiguration();
            // 初始化 TCP 系统
            InitializeTCPSystem();
            // 初始化视觉系统
            InitializeVisionSystem();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 获取原始实例并保存引用
            var vm = Container.Resolve<NeedleViewModel>();
            _needleViewModelRef = vm; // 保存为字段
            var vmDevice = Container.Resolve<EquipmentStatus>();
            _deviceConfigViewModelRef = vmDevice;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _needleViewModelRef?.SaveData(); // 使用保存的引用
            _deviceConfigViewModelRef?.StopMonitoring(); // 使用保存的引用
            base.OnExit(e);
        }

        // =============================================
        // 配置管理
        // =============================================
        private void BuildConfiguration()
        {
            // 获取基路径（应用程序执行目录）
            var basePath = Directory.GetCurrentDirectory();

            // 组合完整路径 -> {执行目录}/Properties/launchSettings.json
            var configPath = Path.Combine(basePath, "Properties", "appsettings.json");
            // 构建配置
            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile(configPath, optional: false, reloadOnChange: true);
            _configuration = builder.Build();
        }

        private void InitializeConfiguration()
        {
            try
            {
                var appConfig = Container.Resolve<IAppConfig>();
                appConfig.Load();

                IMessage.Logger.Info("应用程序配置加载完成");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"配置初始化失败: {ex.Message}");
            }
        }

        // =============================================
        // 服务注册方法
        // =============================================
        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 基础服务注册
            RegisterCoreServices(containerRegistry);

            // 数据库相关注册
            RegisterDatabaseServices(containerRegistry);

            // 业务服务注册
            RegisterBusinessServices(containerRegistry);

            // 站点服务注册
            RegisterStationServices(containerRegistry);

            // 配置服务注册
            RegisterConfigurationServices(containerRegistry);

            // TCP服务注册
            RegisterTCPServices(containerRegistry);

            // 配方插件服务注册
            RegisterRecipePluginServices(containerRegistry);

            // 视图和对话框注册
            RegisterViewsAndDialogs(containerRegistry);

            // 视觉数据服务注册
            RegisterVisionDataServices(containerRegistry);
        }

        private void RegisterCoreServices(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterInstance<ILogger>(LogManager.GetCurrentClassLogger());
            containerRegistry.RegisterInstance<IConfiguration>(_configuration);
            containerRegistry.RegisterSingleton<IAppConfig, AppConfig>();
            containerRegistry.RegisterSingleton<Core.Abstractions.Storages.IGenericStorage, JsonRecipeFileStorage>();
            containerRegistry.RegisterSingleton<TaskInstanceManager>();
            containerRegistry.RegisterSingleton<RegisterTask>();
            containerRegistry.RegisterSingleton<IConfigurationService, JsonConfigurationService>();
            containerRegistry.RegisterSingleton<ILoggerService, LoggerService>();
            containerRegistry.RegisterSingleton<IFileService, FileDialogService>();
            containerRegistry.RegisterSingleton<ISnackbarMessageQueue, SnackbarMessageQueue>();
            containerRegistry.RegisterSingleton<IAxisConfigService, AxisConfigService>();
            containerRegistry.RegisterSingleton<ClampJawViewModel>();
            containerRegistry.RegisterSingleton<ISecsGemService, SecsGemService>();
            containerRegistry.Register<IUICoordinator, UICoordinator>();
            containerRegistry.RegisterSingleton<NeedleCalibrationService>();
            containerRegistry.RegisterSingleton<IStationCancelOperationService, DispenserStationService>();
            containerRegistry.RegisterSingleton<ICompensationService, CompensationService>();
            containerRegistry.RegisterSingleton<DmcMotionService>();
            containerRegistry.RegisterSingleton<IH2HeightDataService, H2HeightDataService>();
            //containerRegistry.RegisterSingleton<StationCoordinator>();
        }

        private void RegisterDatabaseServices(IContainerRegistry containerRegistry)
        {
            // 直接从注册的 IConfiguration 获取连接字符串
            var config = containerRegistry.GetContainer().Resolve<IConfiguration>();
            var connectionString = config.GetConnectionString("AlarmDb")
                                   ?? throw new ConfigurationErrorsException("缺少 AlarmDb 连接字符串");

            // 注册 DbContextOptions（确保选项正确传递）
            containerRegistry.Register<DbContextOptions<AlarmDbContext>>(() =>
            {
                return new DbContextOptionsBuilder<AlarmDbContext>()
                    .UseSqlServer(connectionString)  // [!] 包含连接字符串
                    .EnableSensitiveDataLogging()    // 调试时可临时启用
                    .Options;
            });

            // 注册 IDbContextFactory（通过工厂模式）
            containerRegistry.Register<IDbContextFactory<AlarmDbContext>>(provider =>
            {
                var options = provider.Resolve<DbContextOptions<AlarmDbContext>>();
                return new AlarmDbContextFactory(options); // 自定义工厂
            });

            // （可选）直接注册 DbContext 的便捷访问
            containerRegistry.Register<AlarmDbContext>(provider =>
                provider.Resolve<IDbContextFactory<AlarmDbContext>>().CreateDbContext());

            // 仓库注册（确保使用具体类型和接口直接绑定）
            containerRegistry.Register<IAlarmRepository, AlarmRepository>();
            containerRegistry.Register<IAlarmService, AlarmService>();
        }

        private void RegisterBusinessServices(IContainerRegistry containerRegistry)
        {
            // 业务服务注册
        }

        private void RegisterStationServices(IContainerRegistry containerRegistry)
        {
            containerRegistry.Register<LoadingStation>(() =>
            {
                // 从容器获取所需的依赖项
                var dialogService = Container.Resolve<IDialogService>();
                var eventAggregator = Container.Resolve<IEventAggregator>();
                var container = Container.Resolve<IContainerExtension>();
                var parameters = Container.Resolve<IParameterEditor>();
                var parameterStorage = Container.Resolve<IParameterStorage>();
                var recipeManager = Container.Resolve<IRecipeManager>();
                var recipeStorage = Container.Resolve<IRecipeStorage>();
                var logger = Container.Resolve<ILoggerService>();
                var axisConfigService = Container.Resolve<IAxisConfigService>();
                var appConfig = Container.Resolve<IAppConfig>();
                var recipePoolManager = Container.Resolve<RecipePoolManager>();
                // 初始化为默认ID（稍后会被覆盖）
                return new LoadingStation(-1, dialogService, eventAggregator, container,
                    parameters, parameterStorage, axisConfigService, recipeManager, recipeStorage, logger, appConfig, recipePoolManager);
            });

            containerRegistry.Register<DispenserStation>(() =>
            {
                // 从容器获取所需的依赖项
                var dialogService = Container.Resolve<IDialogService>();
                var eventAggregator = Container.Resolve<IEventAggregator>();
                var container = Container.Resolve<IContainerExtension>();
                var parameters = Container.Resolve<IParameterEditor>();
                var parameterStorage = Container.Resolve<IParameterStorage>();
                var recipeManager = Container.Resolve<IRecipeManager>();
                var recipeStorage = Container.Resolve<IRecipeStorage>();
                var logger = Container.Resolve<ILoggerService>();
                var axisConfigService = Container.Resolve<IAxisConfigService>();
                var appConfig = Container.Resolve<IAppConfig>();
                var recipePoolManager = Container.Resolve<RecipePoolManager>();
                var taskManager = Container.Resolve<TaskInstanceManager>();
                var motionService = Container.Resolve<DmcMotionService>();
                var tCPEvent = Container.Resolve<ITCPEventService>();
                var visionDataService = Container.Resolve<IVisionDataService>();
                var cameraEventProcessor = Container.Resolve<ICameraController>();
                var ICompensationService = Container.Resolve<ICompensationService>();
                // 初始化为默认ID（稍后会被覆盖）
                return new DispenserStation(-1, dialogService, eventAggregator, container,
                    parameters, parameterStorage, axisConfigService, recipeManager, recipeStorage,
                    logger, appConfig, recipePoolManager, taskManager, motionService, cameraEventProcessor, tCPEvent, visionDataService,
                    ICompensationService);
            });

            containerRegistry.Register<AssemblyStation>(() =>
            {
                // 从容器获取所需的依赖项
                var dialogService = Container.Resolve<IDialogService>();
                var eventAggregator = Container.Resolve<IEventAggregator>();
                var container = Container.Resolve<IContainerExtension>();
                var parameters = Container.Resolve<IParameterEditor>();
                var parameterStorage = Container.Resolve<IParameterStorage>();
                var logger = Container.Resolve<ILoggerService>();
                var axisConfigService = Container.Resolve<IAxisConfigService>();
                var recipeManager = Container.Resolve<IRecipeManager>();
                var recipeStorage = Container.Resolve<IRecipeStorage>();
                var appConfig = Container.Resolve<IAppConfig>();
                var recipePoolManager = Container.Resolve<RecipePoolManager>();
                var tCPEvent = Container.Resolve<ITCPEventService>();
                var taskManager = Container.Resolve<TaskInstanceManager>();
                var visionDataService = Container.Resolve<IVisionDataService>();
                var cameraEventService = Container.Resolve<ICameraEventProcessor>();
                var cameraEventProcessor = Container.Resolve<ICameraController>();
                var ICompensationService = Container.Resolve<ICompensationService>();
                // 初始化为默认ID（稍后会被覆盖）
                return new AssemblyStation(-1, dialogService, eventAggregator, container, parameters, parameterStorage, 
                    axisConfigService, recipeManager,recipeStorage, logger, appConfig, recipePoolManager, tCPEvent,
                    visionDataService, cameraEventService, ICompensationService, cameraEventProcessor,taskManager);
            });
        }

        private void RegisterConfigurationServices(IContainerRegistry containerRegistry)
        {
            try
            {
                // 配置路径
                var configPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Config",
                    "AppConfig.xml");

                // 注册配置提供器
                var logger = Container.Resolve<ILoggerService>();
                containerRegistry.RegisterSingleton<IConfigurationProvider>(() =>
                    new XmlConfigurationProvider(configPath, logger));

                logger.Info("配置服务注册完成");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"配置服务注册失败: {ex.Message}");
                throw;
            }
        }

        private void RegisterTCPServices(IContainerRegistry containerRegistry)
        {
            try
            {
                // 注册工厂
                containerRegistry.RegisterSingleton<ITCPClientFactory, TCPClientFactory>();
                containerRegistry.RegisterSingleton<ITCPServerFactory, TCPServerFactory>();

                // 注册管理器服务
                containerRegistry.RegisterSingleton<ITCPClientManagerService, TCPClientManagerService>();

                // 注册事件服务
                containerRegistry.RegisterSingleton<ITCPEventService, TCPEventService>();

                var logger = Container.Resolve<ILoggerService>();
                logger.Info("TCP服务注册完成");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"TCP服务注册失败: {ex.Message}");
                throw;
            }
        }

        private void RegisterRecipePluginServices(IContainerRegistry containerRegistry)
        {
            try
            {
                // 注册插件管理器
                containerRegistry.RegisterSingleton<IPluginManager, PluginManager>();

                // 注册配方插件
                var recipePlugin = new RecipeManagementPlugin();
                containerRegistry.RegisterInstance<IPlugin>(recipePlugin);

                // 注册配方存储服务
                containerRegistry.RegisterSingleton<IRecipeStorage, RecipeStorage>();

                // 注册配方管理器服务
                containerRegistry.RegisterSingleton<IRecipeManager, RecipeManager>();

                // 注册插件配置服务
                containerRegistry.RegisterSingleton<IPluginConfiguration, JsonPluginConfiguration>();

                // 注册配方池管理器
                containerRegistry.RegisterSingleton<RecipePoolManager>();

                // 注册保存参数协调器
                containerRegistry.RegisterSingleton<SaveParametersCoordinator>();

                IMessage.Logger.Info("配方插件服务注册完成");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"配方插件服务注册失败: {ex.Message}");
                throw;
            }
        }

        private void RegisterViewsAndDialogs(IContainerRegistry containerRegistry)
        {
            // 注册配方管理视图
            containerRegistry.RegisterForNavigation<RecipeManagerView, Framework.ViewModels.RecipeManagerViewModel>();
            // 注册对话框
            containerRegistry.RegisterDialog<RecipeEditorDialog, RecipeEditorDialogViewModel>("RecipeEditorDialog");
            // 注册针头校准视图
            containerRegistry.RegisterForNavigation<NeedleAlignerView, NeedleAlignerViewModel>();
        }

        // 注册视觉数据服务
        private void RegisterVisionDataServices(IContainerRegistry containerRegistry)
        {
            try
            {
                // 先注册 ICameraEventProcessor（不需要 IVisionDataService）
                containerRegistry.RegisterSingleton<ICameraEventProcessor>(provider =>
                {
                    var logger = provider.Resolve<ILoggerService>();
                    return new CameraEventProcessor(logger);
                });

                containerRegistry.RegisterSingleton<ICameraController>(provider =>
                    provider.Resolve<ICameraEventProcessor>());

                // 注册 IVisionDataService
                containerRegistry.RegisterSingleton<IVisionDataService>(provider =>
                {
                    var logger = provider.Resolve<ILoggerService>();
                    var cameraEventProcessor = provider.Resolve<ICameraEventProcessor>();
                    return new VisionDataService(logger, cameraEventProcessor);
                });

                var logger = Container.Resolve<ILoggerService>();
                logger.Info("视觉数据服务注册完成");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"视觉数据服务注册失败: {ex.Message}");
                throw;
            }
        }

        // =============================================
        // 模块配置
        // =============================================
        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            _ = moduleCatalog.AddModule<LogViewerModule>();
            _ = moduleCatalog.AddModule<Framework.FrameworkModule>();
            _ = moduleCatalog.AddModule<ModuleCore.CoreModule>();
            _ = moduleCatalog.AddModule<Module.PrimModel>();
        }

        // =============================================
        // 系统初始化方法
        // =============================================
        private void InitializeServiceDependencies()
        {
            try
            {
                IMessage.Logger.Info("开始初始化服务依赖...");
                IMessage.Logger.Info("服务依赖初始化完成");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Fatal($"服务依赖初始化失败: {ex}");
                throw;
            }
        }

        private void InitializeTCPSystem()
        {
            try
            {
                var appConfig = Container.Resolve<IAppConfig>();
                var tcpEventService = Container.Resolve<ITCPEventService>();

                // 初始化事件服务
                tcpEventService.Initialize();

                // 启动 TCP 服务器
                tcpEventService.StartServer(appConfig.ServerConfig);

                // 动态添加所有配置的客户端
                foreach (var clientConfig in appConfig.Clients.Where(c => c.IsEnabled))
                {
                    try
                    {
                        tcpEventService.AddClient(clientConfig.ClientName, clientConfig);
                        IMessage.Logger.Info($"TCP客户端 '{clientConfig.ClientName}' 初始化完成: {clientConfig.IP}:{clientConfig.Port}");
                    }
                    catch (Exception ex)
                    {
                        IMessage.Logger.Error($"初始化TCP客户端 '{clientConfig.ClientName}' 失败: {ex.Message}");
                    }
                }

                IMessage.Logger.Info($"TCP系统初始化完成，共 {appConfig.Clients.Count(c => c.IsEnabled)} 个客户端");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"TCP系统初始化失败: {ex.Message}");
            }
        }
        private void InitializeVisionSystem()
        {
            try
            {
                var tcpEventService = Container.Resolve<ITCPEventService>();
                var visionDataService = Container.Resolve<IVisionDataService>();
                var cameraEventProcessor = Container.Resolve<ICameraEventProcessor>();

                // 初始化依赖关系
                cameraEventProcessor.Initialize(tcpEventService, visionDataService);
                cameraEventProcessor.Start();
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"视觉系统初始化失败: {ex.Message}");
            }
        }


        // =============================================
        // 异常处理相关
        // =============================================
        #region 全局异常处理和诊断

        [DllImport("kernel32.dll")]
        private static extern IntPtr SetUnhandledExceptionFilter(IntPtr lpTopLevelExceptionFilter);

        [DllImport("kernel32.dll")]
        private static extern int GetLastError();

        [DllImport("kernel32.dll")]
        private static extern uint SetErrorMode(uint uMode);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("DbgHelp.dll", SetLastError = true)]
        static extern bool MiniDumpWriteDump(
            IntPtr hProcess,
            uint ProcessId,
            IntPtr hFile,
            MINIDUMP_TYPE DumpType,
            ref MINIDUMP_EXCEPTION_INFORMATION ExceptionParam,
            IntPtr UserStreamParam,
            IntPtr CallbackParam);

        private void InitializeNativeExceptionHandling()
        {
            // 防止系统显示错误对话框
            SetErrorMode(SetErrorMode(0) | 0x0002); // SEM_NOGPFAULTERRORBOX

            // 设置未处理异常过滤器
            _prevExceptionFilter = SetUnhandledExceptionFilter(Marshal.GetFunctionPointerForDelegate(
                new UnhandledExceptionFilterDelegate(UnhandledExceptionFilter)));
        }

        private int UnhandledExceptionFilter(IntPtr exceptionPointers)
        {
            try
            {
                // 生成崩溃报告
                string dumpPath = GenerateCrashDump(exceptionPointers);
                IMessage.Logger.Fatal($"未处理非托管异常! 崩溃信息已保存: {dumpPath}");
            }
            catch
            {
                // 即使在这里出错也要继续关闭
            }
            finally
            {
                // 安全关闭应用程序
                ShutdownGracefully(-1);
            }
            return 1; // EXCEPTION_EXECUTE_HANDLER
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int UnhandledExceptionFilterDelegate(IntPtr exceptionPointers);

        private string GenerateCrashDump(IntPtr exceptionPointers)
        {
            try
            {
                string dumpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CrashDumps");
                Directory.CreateDirectory(dumpDir);
                string dumpFile = Path.Combine(dumpDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.dmp");

                // 使用MiniDumpWriteDump生成转储文件
                uint pid = (uint)Process.GetCurrentProcess().Id;
                using (var fs = new FileStream(dumpFile, FileMode.Create))
                {
                    MINIDUMP_EXCEPTION_INFORMATION mei = new MINIDUMP_EXCEPTION_INFORMATION
                    {
                        ThreadId = GetCurrentThreadId(),
                        ExceptionPointers = exceptionPointers,
                        ClientPointers = 0
                    };

                    if (!MiniDumpWriteDump(
                        Process.GetCurrentProcess().Handle,
                        pid,
                        fs.SafeFileHandle.DangerousGetHandle(),
                        MINIDUMP_TYPE.MiniDumpWithFullMemory,
                        ref mei,
                        IntPtr.Zero,
                        IntPtr.Zero))
                    {
                        int error = Marshal.GetLastWin32Error();
                        IMessage.Logger.Error($"生成转储文件失败，错误代码: {error}");
                    }
                }

                return dumpFile;
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error(ex, "生成崩溃转储文件时出错");
                return string.Empty;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct MINIDUMP_EXCEPTION_INFORMATION
        {
            public uint ThreadId;
            public IntPtr ExceptionPointers;
            public int ClientPointers;
        }

        [Flags]
        private enum MINIDUMP_TYPE
        {
            MiniDumpNormal = 0x00000000,
            MiniDumpWithFullMemory = 0x00000002,
        }

        private void LogStartupInfo()
        {
            string appInfo = $"""
                应用程序启动信息:
                  版本: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}
                  工作目录: {Environment.CurrentDirectory}
                  系统版本: {Environment.OSVersion}
                  内存状态: {(double)(GC.GetTotalMemory(false)) / 1024 / 1024:F2} MB
                  处理器数: {Environment.ProcessorCount}
                  命令行参数: {string.Join(" ", Environment.GetCommandLineArgs())}
                """;
            IMessage.Logger.Info(appInfo);
        }

        private void MonitorMemoryUsage()
        {
            // 每分钟记录一次内存使用情况
            var memoryMonitor = new System.Timers.Timer(60000);
            memoryMonitor.Elapsed += (s, e) => {
                float currentMemory = (float)Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;
                IMessage.Logger.Debug($"当前内存使用量: {currentMemory:F2} MB");
            };
            memoryMonitor.Start();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            HandleException(e.Exception, true);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleException(ex, false, e.IsTerminating);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            HandleException(e.Exception, false);
            e.SetObserved();
        }

        private void HandleException(Exception ex, bool isUIShread, bool isTerminating = false)
        {
            try
            {
                string threadContext = isUIShread ? "UI线程" : "后台线程";
                IMessage.Logger.Fatal(ex, $"{threadContext}未处理异常{(isTerminating ? "【将导致应用终止】" : "")}");

                // 生成错误报告
                GenerateErrorReport(ex);

                // 显示友好错误提示
                ShowFriendlyError();
            }
            finally
            {
                ShutdownGracefully(-1);
            }
        }

        private void GenerateErrorReport(Exception ex)
        {
            try
            {
                // 生成详细错误报告
                StringBuilder report = new StringBuilder();
                report.AppendLine($"错误时间: {DateTime.Now}");
                report.AppendLine($"错误消息: {ex.Message}");
                report.AppendLine($"错误类型: {ex.GetType().FullName}");
                report.AppendLine($"调用堆栈: {ex.StackTrace}");
                report.AppendLine();

                // 显示加载模块
                report.AppendLine("已加载模块:");
                foreach (var module in AppDomain.CurrentDomain.GetAssemblies())
                {
                    report.AppendLine($" - {module.FullName} @ {module.Location}");
                }

                // 保存报告
                string reportFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorReports", $"error_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(reportFile, report.ToString());
            }
            catch
            {
                // 忽略生成错误报告时发生的错误
            }
        }

        private void ShowFriendlyError()
        {
            try
            {
                // 在主UI线程显示友好错误
                Application.Current.Dispatcher.Invoke(() => {
                    MessageBox.Show(
                        "应用程序遇到严重错误并将关闭。错误报告已保存到程序目录下的ErrorReports文件夹中。",
                        "系统崩溃",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
            catch
            {
                // 如果UI线程已不可用，简单退出
            }
        }

        private void ShutdownGracefully(int exitCode)
        {
            LogShutdownInfo(exitCode);

            try
            {
                // 尝试保存所有未保存的数据
                _needleViewModelRef?.SaveData();
                _deviceConfigViewModelRef?.StopMonitoring();

                Environment.Exit(exitCode);
            }
            catch
            {
                // 终极尝试退出
                try
                {
                    Process.GetCurrentProcess().Kill();
                }
                catch
                {
                }
            }
        }

        private void LogShutdownInfo(int exitCode)
        {
            try
            {
                string shutdownInfo = $"""
                    应用程序关闭信息:
                      时间: {DateTime.Now}
                      退出代码: {exitCode}
                      内存状态: {(double)(GC.GetTotalMemory(false)) / 1024 / 1024:F2} MB
                      线程数: {Process.GetCurrentProcess().Threads.Count}
                    """;
                IMessage.Logger.Info(shutdownInfo);
            }
            catch
            {
                // 如果日志系统已经不可用，忽略
            }
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            // 监视内存使用情况
            MonitorMemoryUsage();
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            IMessage.Logger.Info("应用程序正常退出");
        }

        #endregion
    }
}
using Core;
using Core.Abstraction;
using Core.Configuration;
using Core.Extensions;
using Core.Services;
using Core.Utilities;
using LogViewer;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Configuration;
using ModuleCore.ViewModels;
using Modules.Language;
using MotionControl;
using NLog;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Unity;
using Recipe;
using StationTasks;
using System;
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
using Unity;

namespace MainApp
{
    public partial class App
    {
        private IConfiguration _configuration;
        private IAppSettingService _appSettingService;
        private IntPtr _prevExceptionFilter;
        private ILoggerService _logger;
        private ILocalizationService _localization;
        private static readonly Logger _nlogLogger = LogManager.GetCurrentClassLogger();

        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            this.Exit += App_Exit;
            this.Startup += App_Startup;

            InitializeNativeExceptionHandling();
            LogStartupInfo();
        }

        protected override Window CreateShell()
        {
            return Container.Resolve<Views.MainWindow>();
        }

        protected override void Initialize()
        {
            BuildConfiguration();
            try
            {
                base.Initialize();
            }
            catch (Exception ex)
            {
                // 捕获 Prism 引导过程异常（CreateShell/InitializeModules 等），_nlogLogger 始终可用
                _nlogLogger.Fatal(ex, "[Initialize] Prism base.Initialize() failed");
                throw;
            }
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            InitializeServiceDependencies();
            InitializeConfiguration();
            InitializeTCPSystem();
            InitializeVisionSystem();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _appSettingService?.Save();
            Task.Delay(500).Wait();
            base.OnExit(e);
        }

        private void BuildConfiguration()
        {
            var basePath = Directory.GetCurrentDirectory();
            var configPath = Path.Combine(basePath, "Properties", "appsettings.json");
            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile(configPath, optional: false, reloadOnChange: true);
            _configuration = builder.Build();
        }

        private void InitializeConfiguration()
        {
            try
            {
                _appSettingService = Container.Resolve<IAppSettingService>();
                _appSettingService.Load();
                _logger?.Info(_localization.GetResourceOrDefault("App_Log_ConfigLoaded", "应用程序配置加载完成"));
            }
            catch (Exception ex)
            {
                _logger?.Error(string.Format(_localization.GetResourceOrDefault("App_Log_ConfigInitFailed", "配置初始化失败: {0}"), ex.Message));
            }
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            RegisterCoreServices(containerRegistry);
            RegisterDatabaseServices(containerRegistry);
            RegisterBusinessServices(containerRegistry);
            RegisterStationServices(containerRegistry);
            RegisterConfigurationServices(containerRegistry);
            RegisterTCPServices(containerRegistry);
            RegisterVisionDataServices(containerRegistry);
        }

        private void RegisterCoreServices(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterInstance<ILogger>(LogManager.GetCurrentClassLogger());
            containerRegistry.RegisterInstance<IConfiguration>(_configuration);
            containerRegistry.RegisterSingleton<IConfigurationService, JsonConfigurationService>();
            containerRegistry.RegisterSingleton<ILoggerService, LoggerService>();
            containerRegistry.RegisterSingleton<ISnackbarMessageQueue, SnackbarMessageQueue>();
            containerRegistry.RegisterSingleton<ILocalizationService, Core.Services.LocalizationService>();
            containerRegistry.RegisterSingleton<IAppSettingService, ConfigurationService>();
            containerRegistry.RegisterSingleton<IStationRegistry, StationRegistry>();
            containerRegistry.RegisterSingleton<IDispenseSegmentStore, DispenseSegmentStore>();
            containerRegistry.RegisterSingleton<IDispenseSegmentSourceService, Core.Services.DispenseSegmentSourceService>();
            // 配置文件保留策略服务：按文件夹最大文件数量清理旧文件
            containerRegistry.RegisterSingleton<Core.Abstraction.IConfigFileRetentionService, Core.Services.ConfigFileRetentionService>();
            // ZScanConfigService 需注入 IConfigFileRetentionService 以支持按数量清理
            containerRegistry.RegisterSingleton<Core.Abstraction.IZScanConfigService>(() =>
            {
                var basePath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Config", "ZScan");
                var retentionService = Container.Resolve<Core.Abstraction.IConfigFileRetentionService>();
                return new Core.Services.ZScanConfigService(basePath, retentionService);
            });

            _logger = Container.Resolve<ILoggerService>();
            _localization = Container.Resolve<ILocalizationService>();
        }

        private void RegisterDatabaseServices(IContainerRegistry containerRegistry) { }

        private void RegisterBusinessServices(IContainerRegistry containerRegistry) { }

        private void RegisterStationServices(IContainerRegistry containerRegistry) { }

        private void RegisterConfigurationServices(IContainerRegistry containerRegistry)
        {
            try
            {
                var configPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Config",
                    "appconfig.xml");

                var logger = Container.Resolve<ILoggerService>();
                containerRegistry.RegisterSingleton<Core.Abstraction.IConfigurationProvider>(() =>
                    new XmlConfigurationProvider(configPath, logger, Container.Resolve<ILocalizationService>()));

                logger.Info(_localization.GetResourceOrDefault("App_Log_ConfigServiceRegistered", "配置服务注册完成"));
            }
            catch (Exception ex)
            {
                _nlogLogger.Error(string.Format(_localization.GetResourceOrDefault("App_Log_ConfigServiceRegisterFailed", "配置服务注册失败: {0}"), ex.Message));
                throw;
            }
        }

        private void RegisterTCPServices(IContainerRegistry containerRegistry) { }

        private void RegisterVisionDataServices(IContainerRegistry containerRegistry)
        {
            try
            {
                var logger = Container.Resolve<ILoggerService>();
                logger.Info(_localization.GetResourceOrDefault("App_Log_VisionDataServiceRegistered", "视觉数据服务注册完成"));
            }
            catch (Exception ex)
            {
                _nlogLogger.Error(string.Format(_localization.GetResourceOrDefault("App_Log_VisionDataServiceRegisterFailed", "视觉数据服务注册失败: {0}"), ex.Message));
                throw;
            }
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            _ = moduleCatalog.AddModule<LogViewerModule>();
            _ = moduleCatalog.AddModule<LanguageModule>();
            _ = moduleCatalog.AddModule<Framework.FrameworkModule>();
            _ = moduleCatalog.AddModule<AlarmModule.AlarmModule>();
            _ = moduleCatalog.AddModule<MotionControlModule>();
            _ = moduleCatalog.AddModule<RecipeModule>();
            _ = moduleCatalog.AddModule<StationTasksModule>();
            _ = moduleCatalog.AddModule<ModuleCore.CoreModule>();
            _ = moduleCatalog.AddModule<Module.PrimModel>();
            _ = moduleCatalog.AddModule<TCPIPModule.TCPIPModule>();
        }

        private void InitializeServiceDependencies()
        {
            try
            {
                _logger?.Info(_localization.GetResourceOrDefault("App_Log_ServiceDepsInitStart", "开始初始化服务依赖..."));
                _logger?.Info(_localization.GetResourceOrDefault("App_Log_ServiceDepsInitDone", "服务依赖初始化完成"));
            }
            catch (Exception ex)
            {
                _nlogLogger.Fatal(string.Format(_localization.GetResourceOrDefault("App_Log_ServiceDepsInitFailed", "服务依赖初始化失败: {0}"), ex));
                throw;
            }
        }

        private void InitializeTCPSystem() { }
        private void InitializeVisionSystem() { }

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
            SetErrorMode(SetErrorMode(0) | 0x0002);
            _prevExceptionFilter = SetUnhandledExceptionFilter(Marshal.GetFunctionPointerForDelegate(
                new UnhandledExceptionFilterDelegate(UnhandledExceptionFilter)));
        }

        private int UnhandledExceptionFilter(IntPtr exceptionPointers)
        {
            try
            {
                string dumpPath = GenerateCrashDump(exceptionPointers);
                _nlogLogger.Fatal(string.Format(_localization?.GetResourceOrDefault("App_Log_UnmanagedException", "未处理非托管异常! 崩溃信息已保存: {0}") ?? "未处理非托管异常! 崩溃信息已保存: {0}", dumpPath));
            }
            catch { }
            finally
            {
                ShutdownGracefully(-1);
            }
            return 1;
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
                        _nlogLogger.Error(string.Format(_localization.GetResourceOrDefault("App_Log_DumpGenerationFailed", "生成转储文件失败，错误代码: {0}"), error));
                    }
                }

                return dumpFile;
            }
            catch (Exception ex)
            {
                _nlogLogger.Error(ex, _localization.GetResourceOrDefault("App_Log_DumpGenerationError", "生成崩溃转储文件时出错"));
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
            try
            {
                string appInfo = $"""
                    {ResourceHelper.GetString("App_Log_StartupInfoHeader")}
                      {ResourceHelper.GetString("App_Log_StartupVersion", Assembly.GetExecutingAssembly().GetName().Version)}
                      {ResourceHelper.GetString("App_Log_StartupWorkDir", Environment.CurrentDirectory)}
                      {ResourceHelper.GetString("App_Log_StartupOSVersion", Environment.OSVersion)}
                      {ResourceHelper.GetString("App_Log_StartupMemory", (double)(GC.GetTotalMemory(false)) / 1024 / 1024)}
                      {ResourceHelper.GetString("App_Log_StartupProcessorCount", Environment.ProcessorCount)}
                      {ResourceHelper.GetString("App_Log_StartupCmdArgs", string.Join(" ", Environment.GetCommandLineArgs()))}
                    """;
                _nlogLogger.Info(appInfo);
            }
            catch { }
        }

        private void MonitorMemoryUsage()
        {
            var memoryMonitor = new System.Timers.Timer(60000);
            memoryMonitor.Elapsed += (s, e) => {
                float currentMemory = (float)Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024;
                _logger?.Debug(string.Format(_localization.GetResourceOrDefault("App_Log_CurrentMemoryUsage", "当前内存使用量: {0:F2} MB"), currentMemory));
            };
            memoryMonitor.Start();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (IsShutdownException(e.Exception))
            {
                e.Handled = true;
                return;
            }
            HandleException(e.Exception, true);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                if (IsShutdownException(ex))
                    return;
                HandleException(ex, false, e.IsTerminating);
            }
        }

        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception?.InnerException != null && IsShutdownException(e.Exception.InnerException))
            {
                e.SetObserved();
                return;
            }
            if (e.Exception != null && IsShutdownException(e.Exception))
            {
                e.SetObserved();
                return;
            }
            _nlogLogger.Fatal(e.Exception, _localization.GetResourceOrDefault("App_Log_UnobservedTaskException", "未观察到的任务异常"));
            e.SetObserved();
        }

        /// <summary>
        /// 判断异常是否为应用关闭期间 Dispatcher 已关闭导致的 TaskCanceledException
        /// 此类异常在正常关闭流程中是预期行为，不应触发致命错误路径
        /// </summary>
        private bool IsShutdownException(Exception ex)
        {
            if (ex is TaskCanceledException || ex is OperationCanceledException)
                return true;

            if (ex is AggregateException agg)
                return agg.InnerExceptions.Any(IsShutdownException);

            if (ex.InnerException != null)
                return IsShutdownException(ex.InnerException);

            return false;
        }

        private void HandleException(Exception ex, bool isUIShread, bool isTerminating = false)
        {
            try
            {
                // 先用 _nlogLogger 直接记录异常（_localization 可能为 null，避免 NRE 吞掉真正的异常）
                _nlogLogger.Fatal(ex, $"[HandleException] isUIThread={isUIShread}, isTerminating={isTerminating}");

                string threadContext = isUIShread
                    ? (_localization?.GetResourceOrDefault("App_Log_UIThread", "UI线程") ?? "UI线程")
                    : (_localization?.GetResourceOrDefault("App_Log_BackgroundThread", "后台线程") ?? "后台线程");
                string terminatingSuffix = isTerminating
                    ? (_localization?.GetResourceOrDefault("App_Log_TerminatingSuffix", "【将导致应用终止】") ?? "【将导致应用终止】")
                    : string.Empty;
                _nlogLogger.Fatal(ex, string.Format(_localization?.GetResourceOrDefault("App_Log_UnhandledException", "{0}未处理异常{1}") ?? "{0}未处理异常{1}", threadContext, terminatingSuffix));

                GenerateErrorReport(ex);
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
                StringBuilder report = new StringBuilder();
                report.AppendLine(string.Format(_localization.GetResourceOrDefault("App_Log_ErrorTime", "错误时间: {0}"), DateTime.Now));
                report.AppendLine(string.Format(_localization.GetResourceOrDefault("App_Log_ErrorMessage", "错误消息: {0}"), ex.Message));
                report.AppendLine(string.Format(_localization.GetResourceOrDefault("App_Log_ErrorType", "错误类型: {0}"), ex.GetType().FullName));
                report.AppendLine(string.Format(_localization.GetResourceOrDefault("App_Log_CallStack", "调用堆栈: {0}"), ex.StackTrace));
                report.AppendLine();

                report.AppendLine(_localization.GetResourceOrDefault("App_Log_LoadedModules", "已加载模块:"));
                foreach (var module in AppDomain.CurrentDomain.GetAssemblies())
                {
                    report.AppendLine($" - {module.FullName} @ {module.Location}");
                }

                string reportFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorReports", $"error_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(reportFile, report.ToString());
            }
            catch { }
        }

        private void ShowFriendlyError()
        {
            try
            {
                if (Application.Current?.Dispatcher == null || Application.Current.Dispatcher.HasShutdownStarted)
                    return;
                Application.Current.Dispatcher.Invoke(() => {
                    MessageBox.Show(
                        _localization.GetResourceOrDefault("App_Log_FatalErrorMessage", "应用程序遇到严重错误并将关闭。错误报告已保存到程序目录下的ErrorReports文件夹中。"),
                        _localization.GetResourceOrDefault("App_Log_FatalErrorTitle", "系统崩溃"),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                });
            }
            catch { }
        }

        private void ShutdownGracefully(int exitCode)
        {
            LogShutdownInfo(exitCode);

            try
            {
                Environment.Exit(exitCode);
            }
            catch
            {
                try { Process.GetCurrentProcess().Kill(); } catch { }
            }
        }

        private void LogShutdownInfo(int exitCode)
        {
            try
            {
                string shutdownInfo = $"""
                    {ResourceHelper.GetString("App_Log_ShutdownInfoHeader")}
                      {ResourceHelper.GetString("App_Log_ShutdownTime", DateTime.Now)}
                      {ResourceHelper.GetString("App_Log_ShutdownExitCode", exitCode)}
                      {ResourceHelper.GetString("App_Log_ShutdownMemory", (double)(GC.GetTotalMemory(false)) / 1024 / 1024)}
                      {ResourceHelper.GetString("App_Log_ShutdownThreadCount", Process.GetCurrentProcess().Threads.Count)}
                    """;
                _nlogLogger.Info(shutdownInfo);
            }
            catch { }
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            MonitorMemoryUsage();
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            _logger?.Info(_localization.GetResourceOrDefault("App_Log_NormalExit", "应用程序正常退出"));
        }

        #endregion
    }
}

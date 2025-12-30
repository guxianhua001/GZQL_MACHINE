using ModuleCore.Common.Authority;
using ModuleCore.Models;
using ModuleCore.Services;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using NLog.Fluent;
using NLog;
using Interfaces;
using System.ComponentModel;
using Stations;
using Interfaces.Mvvm;
using System.Collections.Concurrent;
using System.Windows;
using TCPLib.TCPHelper;
using System.Net.Sockets;
using ModuleCore.Views;
using MaterialDesignThemes.Wpf;
using HSMS;
using System.Windows.Media;
using System.IO;
using Interfaces.Services;
using System.Reflection;
using System.Threading;
using Interfaces.Views;
using Interfaces.Events;
using HandyControl.Controls;
using SmarterMotion;
using Framework.Mvvm;
using Core.Abstraction;
using Core.Services;
using Core.Abstractions.IConfiguration;
using Recipe.Events;

namespace ModuleCore.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private string _title = "JQS";

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }
        private string _appVersion = "v1.0.0";
        public string AppVersion
        {
            get => _appVersion;
            set => SetProperty(ref _appVersion, value);
        }

        private string _recipeName = "未选择配方";
        public string RecipeName
        {
            get => _recipeName;
            set => SetProperty(ref _recipeName, value);
        }
        // SEC/GEM 状态相关属性
        private string _secsStatusText = "断开";
        public string SecsStatusText
        {
            get => _secsStatusText;
            set => SetProperty(ref _secsStatusText, value);
        }
        private Brush _secsStatusColor = Brushes.Red;
        public Brush SecsStatusColor
        {
            get => _secsStatusColor;
            set => SetProperty(ref _secsStatusColor, value);
        }
        public LoginModel Model { get; set; }
        public NavigateModel Navigate { get; set; }
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ISecsGemService _secsGemService;
        private readonly IAxisConfigService _configService;
        private readonly IAppConfig _appConfig;
        private SubscriptionToken _refreshToken;
        private SubscriptionToken _secsCommandToken;
        private readonly ClampJawViewModel _clampJawViewModel;
        private readonly TaskInstanceManager _taskManager;
        public MainWindowViewModel(IDialogService dialogService,
                                   IRegionManager regionManager,
                                   IContainerExtension container,
                                   IEventAggregator eventAggregator,
                                   IAppConfig appConfig,
                                   ClampJawViewModel clampJawViewModel,
                                   TaskInstanceManager taskManager,
                                   IAxisConfigService configService)
        {
            _regionManager = regionManager;
            _dialogService = dialogService;
            _appConfig = appConfig;
            Model = container.Resolve<LoginModel>();
            Navigate = container.Resolve<NavigateModel>();
            RecipeName = "当前配方: " + _appConfig.Name;
            _eventAggregator = container.Resolve<IEventAggregator>();
            _clampJawViewModel = clampJawViewModel;
            _taskManager = taskManager;
            //注册发送给errLog的消息
            _eventAggregator.GetEvent<MessageEvent>().Subscribe(
                MessageReceived,
                ThreadOption.UIThread,
                false,
                (filter) => filter.Target.Contains("errLog"));

            // 订阅刷新事件
            _refreshToken = _eventAggregator
                .GetEvent<RecipeChangedEvent>()
                .Subscribe(OnProductNeedRefresh);

            _eventAggregator.GetEvent<ThresholdWarningEvent>()
                 .Subscribe(ShowWarningDialog, ThreadOption.UIThread);

            // 订阅SECS命令事件
            _secsCommandToken = _eventAggregator.GetEvent<SecsCommandEvent>()
                .Subscribe(OnSecsCommandReceived);
            // 订阅SECS/GEM连接状态变化
            //_secsGemService.ConnectionStatusChanged += OnSecsConnectionStatusChanged;
            // 获取程序集版本
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            AppVersion = $"v{version.Major}.{version.Minor}.{version.Build}";
            InitializeHardware(container); // 初始化硬件
            InitializeSoftware(); // 初始化软件配置
            // 发布系统初始化完成事件
            _eventAggregator.GetEvent<SystemInitializedEvent>().Publish();
            InitializeCommands(); // 初始化命令
            LoadDefaultView(appConfig, container); // 加载默认视图
            _configService = configService;
        }
        private void OnSecsCommandReceived(SecsCommandParameter param)
        {
            IMessage.Logger.Warn(param.LogMessage); // 记录原始命令日志

            Application.Current.Dispatcher.Invoke(() =>
            {
                switch (param.CommandType)
                {
                    case SecsCommandType.Hold:
                        ExecutePause(); // 执行暂停命令
                        IMessage.Logger.Info("【远程命令】设备暂停");
                        break;
                    case SecsCommandType.Release:
                        ExecuteContinue(); // 执行继续命令
                        IMessage.Logger.Info("【远程命令】设备继续");
                        break;
                    case SecsCommandType.Stop:
                        ExecuteStop(); // 执行停止命令
                        IMessage.Logger.Info("【远程命令】设备停止");
                        break;
                    case SecsCommandType.Start:
                        ExecuteStart(); // 执行启动命令
                        IMessage.Logger.Info("【远程命令】设备启动");
                        break;
                }
            });
        }
        private void OnSecsConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs e)
        {
            // 在UI线程更新状态
            Application.Current.Dispatcher.Invoke(() =>
            {
                SecsStatusText = e.StatusText;
                SecsStatusColor = e.IsConnected ? Brushes.Green : Brushes.Red;

                IMessage.Logger.Info($"SECS/GEM状态更新: {e.StatusText}");
            });
        }
        private void ShowWarningDialog(ThresholdWarningNotification notification)
        {
            // 使用新的用户控件方式显示
            var dialogContent = new ThresholdWarningNotificationView
            {
                DataContext = notification
            };
            DialogHost.Show(dialogContent, "MainDialogHost");
            if (notification.IsBlocked)
            {
                // 执行阻塞操作，例如暂停或停止设备
                ExecutePause();
                IMessage.Logger.Info($"【WarningDialog】{notification.FormattedMessage}");
            }
        }
        private void HandleDialogClose(object sender, DialogClosingEventArgs args)
        {
            if (args.Parameter is bool confirmed && confirmed)
            {
                // 处理确认操作
            }
        }

        private void OnProductNeedRefresh(string recipeName)
        {
            // 带条件刷新的智能重载
            if (!string.IsNullOrEmpty(recipeName))
            {
                RecipeName = "当前配方: " + recipeName;
            }
        }
        /// <summary>
        /// 初始化硬件和任务，此处显式调用是为了确保在程序启动时立即进行硬件的初始化。
        /// </summary>
        private void InitializeHardware(IContainerExtension container)
        {
            var registerTask = container.Resolve<RegisterTask>();
            registerTask.InitializeHardware(); // 显式调用初始化
        }
        /// <summary>
        /// 初始化设备配置
        /// </summary>
        private void InitializeSoftware()
        {
            // 初始化软件配置
            var deviceConfig = DeviceConfigService.LoadDeviceConfig();
            XMachine.Instance.DoorEnabled = deviceConfig.EnableSafetyGate;
            XMachine.Instance.BuzzerEnabled = deviceConfig.EnableBuzzer;
            //_secsGemService.IsEnableSecs = deviceConfig.EnableSecsGem;
            XStationManager.Instance.FindStationById(1).IsEnableBuzzer = deviceConfig.EnableBuzzer;
            //if (_secsGemService.IsEnableSecs)
            //    _secsGemService.controlMode = 1;
            //else
            //    _secsGemService.controlMode = 0;
        }

        public void IsAdmin(object sender, CanExecuteRoutedEventArgs e)
        {
            if ((int)Model.LoginUser.Authority >= 2)
            {
                e.CanExecute = true;
            }
            else
            {
                e.CanExecute = false;
            }
            //避免事件继续向上传递而降低程序性能
            e.Handled = true;
        }

        private void MessageReceived(Interfaces.Mvvm.Message message)
        {
            var msg = message.Content;
            AddErr(new ErrModel() { ErrMsg = msg });
        }

        private NavigateItem _NavigateTarget;

        public NavigateItem NavigateTarget
        {
            get { return _NavigateTarget; }
            set
            {
                SetProperty(ref _NavigateTarget, value);
                if (value is not null)
                    NavigateCommand.Execute(value.ViewName);
            }
        }

        /// <summary>
        /// 延迟加载默认工作视图
        /// </summary>
        private async void LoadDefaultView(IAppConfig appConfig, IContainerExtension container)
        {
            await Task.Delay(1000);//延迟加载，否则不显示主视图
            ViewDisplayLoad();
            var defaultView = Navigate.DefaultView;
            NavigateCommand.Execute(defaultView);
            ShowLogViewer();
            //InitialSecs(); // 初始化SECS/GEM连接
            //ShowLoginDialog(); // 显示登录对话框
        }
        private void InitialSecs()
        {
            if (_secsGemService.Initialize(5000, "0"))
            {
                SecsStatusText = "已连接";
                SecsStatusColor = Brushes.Green;
                IMessage.Logger.Info("SECS/GEM初始化成功");
            }
            else
            {
                SecsStatusText = "断开";
                SecsStatusColor = Brushes.Red;
                IMessage.Logger.Error("SECS/GEM初始化失败");
            }
        }
        // 重新连接命令
        public ICommand ReconnectSecsCommand => new RelayCommand(() =>
        {
            InitialSecs();
        });

        private DataTable dt;
        private List<string> ShowList = new();
        private void ViewDisplayLoad()
        {
            string _ViewConfigPath = Path.Combine(
                   AppDomain.CurrentDomain.BaseDirectory,
                   "Config",
                   "ViewConfig.json");
            dt = JsonService.DataTableFromFile(_ViewConfigPath);
            if (dt == null)
            {
                return;
            }
            else
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    var viewname = dt.Rows[i]["ViewName"].ToString();
                    if (!ShowList.Contains(viewname))
                    {
                        ShowList.Add(viewname);
                    }
                }

                foreach (var item in Navigate.NavigateList)
                {
                    if (ShowList.Contains(item.ViewName))
                    {
                        item.Display = true;
                    }
                    else
                    {
                        item.Display = false;
                    }
                }
                ShowNavigateMenu(Model.LoginUser.Authority);
            }
        }

        private DelegateCommand<string> _NavigateCommand;

        public DelegateCommand<string> NavigateCommand =>
             _NavigateCommand ??= new DelegateCommand<string>(ExecuteNavigateCommand);

        private void ExecuteNavigateCommand(string navigatePath)
        {
            if (string.IsNullOrEmpty(navigatePath))
                return;

            //设置时检查权限
            if (navigatePath == "Setting" && (int)Model.LoginUser.Authority < 2)
            {
                AddErr(new ErrModel() { ErrMsg = "请登录管理员权限" });
                return;
            }
            _regionManager.RequestNavigate("ContentRegionCore", navigatePath);
        }

        //导航
        private readonly IRegionManager _regionManager;

        //对话框
        private DelegateCommand _AboutDialog;

        public DelegateCommand AboutDialog =>
            _AboutDialog ??= new DelegateCommand(ExecuteAboutDialog);

        private void ExecuteAboutDialog()
        {
            _dialogService.ShowDialog("AlertDialog", new DialogParameters($"message={"message:"}"), r =>
            {
                if (r.Result == ButtonResult.Yes)
                    Title = "YIJI";
            });
        }

        private ObservableCollection<ErrModel> _Errors = new();

        public ObservableCollection<ErrModel> Errors
        {
            get { return _Errors; }
            set { SetProperty(ref _Errors, value); }
        }

        /// <summary>
        /// AddErr( new ErrModel() { ErrMsg = "ErrMsg" }); 添加浮动报警
        /// </summary>
        /// <param name="err"></param>
        public void AddErr(ErrModel err)
        {
            Errors.Add(err);
            err.Confirmed += ClearConfirmed;
        }

        private void ClearConfirmed()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, new Action(() =>
            {
                var re = Errors.Where(x => x.ErrMsg.Length == 0).FirstOrDefault();
                Errors.Remove(re);
            }));
        }

        private DelegateCommand _Login;

        public DelegateCommand Login =>
             _Login ??= new DelegateCommand(ExecuteLogin);

        private void ExecuteLogin()
        {
            _dialogService.ShowDialog("LoginView", new DialogParameters($"message={"message:"}"), r =>
            {
                if (r.Result == ButtonResult.Yes)
                {
                    ShowNavigateMenu(Model.LoginUser.Authority);
                }

                if (r.Result == ButtonResult.Retry)
                {
                    _dialogService.ShowDialog("UserManage", new DialogParameters($"message={"message:"}"), r => { });
                }
            });
        }
        private void ShowLoginDialog()
        {
            // 使用模态对话框确保必须先登录
            _dialogService.ShowDialog("LoginView", new DialogParameters($"message={"message:"}"), r =>
            {
                if (r.Result == ButtonResult.Yes)
                {
                    ShowNavigateMenu(Model.LoginUser.Authority);
                }
                else if (r.Result == ButtonResult.No)
                {
                    // 访客登录处理
                    Model.LoginUser = Model.UserList.FirstOrDefault(u => u.Name == "Guest");
                    ShowNavigateMenu(Model.LoginUser?.Authority ?? 0);
                }
                else if (r.Result == ButtonResult.Retry)
                {
                    // 用户管理处理
                    _dialogService.ShowDialog("UserManage", new DialogParameters(), dr => { });
                }
                else
                {
                    // 如果用户直接关闭窗口（应该被拦截），再重试显示
                    if (!Model.HasPermission(Authority.Operator) || Model.LoginUser == null)
                    {
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(ShowLoginDialog);
                    }
                }
            });
        }
        private void ShowNavigateMenu(Authority authority)
        {
            Navigate.NavigateShowList.Clear();

            foreach (var item in Navigate.NavigateList)
            {
                if (item.UserLevel <= (int)authority && item.Display)
                    Navigate.NavigateShowList.Add(item);
            }
        }
        private void ShowLogViewer()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var parameters = new DialogParameters {
                    { "dialogWidth", 1200 },
                    { "dialogHeight", 800 },
                    { "sizeToContent", SizeToContent.WidthAndHeight }, // 关键修改
                    { "resizeMode", ResizeMode.NoResize },
                    { "windowStyle", WindowStyle.SingleBorderWindow },
                    { "windowStartupLocation", WindowStartupLocation.CenterOwner }
                };
                _dialogService.Show("LogViewer", parameters, r => { });
            }, DispatcherPriority.Background);
        }


        private DelegateCommand _Start;

        public DelegateCommand Start =>
             _Start ??= new DelegateCommand(ExecuteStart);

        private async void ExecuteStart()
        {
            foreach (var item in XStationManager.Instance.Stations)
            {
                if(item.Value.State != XStationState.WAITRUN)
                {
                    AddErr(new ErrModel() { ErrMsg = "请先初始化设备" });
                    return;
                }
            }
            if (XMachine.Instance.CheckStartCondition() == false)
            {
                AddErr(new ErrModel() { ErrMsg = "启动设备条件不满足" });
                return;
            }
            if( XMachine.Instance.HostToEqpHoldMachine == true)
            {
                AddErr(new ErrModel() { ErrMsg = "设备已被远程锁定，请解锁后启动" });
                return;
            }
            foreach (var item in XStationManager.Instance.Stations)
            {
                item.Value.Start("Auto");
            }
            XMachine.Instance.SetGreenBtnLight();
            IMessage.Logger.Log(LogLevel.Info, $"【按钮】设备启动");
        }

        private DelegateCommand _Pause;

        public DelegateCommand Pause =>
             _Pause ??= new DelegateCommand(ExecutePause);

        private void ExecutePause()
        {
            foreach (var station in XStationManager.Instance.Stations)
            {
                station.Value.Pause();
                station.Value.ResetBuzz();
            }
            IMessage.Logger.Log(LogLevel.Info, $"【按钮】设备暂停");
        }

        private DelegateCommand _Continue;

        public DelegateCommand Continue =>
             _Continue ??= new DelegateCommand(ExecuteContinue);

        private void ExecuteContinue()
        {
            if (XMachine.Instance.HostToEqpHoldMachine == true)
            {
                AddErr(new ErrModel() { ErrMsg = "设备已被远程锁定，请解锁后继续" });
                return;
            }
            foreach (var station in XStationManager.Instance.Stations)
            {
                station.Value.Continue();
            }
            IMessage.Logger.Log(LogLevel.Info, $"【按钮】设备继续");
        }

        private DelegateCommand _Stop;

        public DelegateCommand Stop =>
             _Stop ??= new DelegateCommand(ExecuteStop);

        private void ExecuteStop()
        {
            foreach (var item in XStationManager.Instance.Stations)
            {
                item.Value.Stop();
            }
            XMachine.Instance.SetRedBtnLight();
            IMessage.Logger.Log(LogLevel.Info, $"【按钮】设备停止");
        }
        private DelegateCommand _Initialize;

        public DelegateCommand Initialize =>
             _Initialize ??= new DelegateCommand(ExecuteInitialize);

        private async void ExecuteInitialize()
        {
            foreach (var task in XTaskManager.Instance.Tasks.Values)
            {
                if (task.Station.State == StationState.Running || task.Station.State == StationState.Pause)
                {
                    AddErr(new ErrModel() { ErrMsg = "设备运行中,不能初始化" });
                    return;
                }
            }
            if (XMachine.Instance.CheckResetCondition() == false)
            {
                AddErr(new ErrModel() { ErrMsg = "初始化条件不满足" });
                return;
            }
            if (XMachine.Instance.CheckStartCondition() == false)
            {
                AddErr(new ErrModel() { ErrMsg = "复位设备条件不满足" });
                return;
            }

            if (!ShowExecuteInitializeDialog())
            {
                return;
            }
            foreach (var task in XTaskManager.Instance.Tasks.Values)
            {
                task.IsMaterialInitialization = false;
            }
            // 步骤 1: 下载所有轴参数
            await _configService.DownloadAllParametersAsync(null);
            // 步骤 2: 应用所有插补系参数
            //await Task.Run(() =>
            //{
            //    foreach (var system in _configService.LoadInterpolationSystems())
            //    {
            //        _configService.ApplyInterpolationParameters(system);
            //    }
            //});
            foreach (var item in XStationManager.Instance.Stations)
            {
                item.Value.Reset();
            }
            XMachine.Instance.SetResetBtnLight();
            IMessage.Logger.Log(LogLevel.Info, $"【按钮】设备初始化");
        }
        public void CloseSecsGemService()
        {
            //_secsGemService.CloseSECS();
            IMessage.Logger.Log(LogLevel.Info, "SECS/GEM服务已关闭");
        }
        private bool ShowExecuteInitializeDialog()
        {
            var result = Framework.Services.DialogService.ShowBlockingDialog(
                title: "警告",
                message: $"请确认设备处在安全状态下,是否执行初始化? \n\n",
                yesButtonText: "是",
                noButtonText: "否",
                showYesButton: true,
                showNoButton: true,
                icon: PackIconKind.ClockAlert
            );

            if ((int)result == 0) // YES
            {
                IMessage.Logger.Info("用户选择执行初始化操作");
                return true;
            }
            else
            {
                IMessage.Logger.Info("用户选择取消初始化操作");
            }
            return false;
        }

        #region 任务监控窗口相关
        public DelegateCommand ShowTaskMonitorCommand { get; private set; }

        private void InitializeCommands()
        {
            loadingStation = _taskManager.GetTask<LoadingStation>();
            dispenserStation = _taskManager.GetTask<DispenserStation>();
            assemblyStation = _taskManager.GetTask<AssemblyStation>();
            // 添加显示任务监控窗口的命令
            ShowTaskMonitorCommand = new DelegateCommand(ExecuteShowTaskMonitor);
        }
        private LoadingStation loadingStation;
        private DispenserStation dispenserStation;
        private AssemblyStation assemblyStation;
        // 显示任务监控窗口
        private void ExecuteShowTaskMonitor()
        {
            try
            {
                // 创建任务监控视图模型
                var viewModel = new TaskMonitorViewModel(new SnackbarMessageQueue(), _eventAggregator);

                // 添加要监控的任务
                viewModel.AddTaskToMonitor(loadingStation);
                viewModel.AddTaskToMonitor(dispenserStation);
                viewModel.AddTaskToMonitor(assemblyStation);

                // 准备窗口容器
                var content = new TaskMonitorView
                {
                    DataContext = viewModel,
                    Width = 800,
                    Height = 600
                };

                // 创建窗口并显示
                new System.Windows.Window
                {
                    Title = "任务执行监控",
                    Content = content,
                    Width = 900,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Application.Current.MainWindow
                }.Show();
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"打开任务监控窗口失败: {ex.Message}");
            }
        }

        #endregion
    }
}
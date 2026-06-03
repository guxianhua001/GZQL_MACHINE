using ModuleCore.Common.Authority;
using ModuleCore.Models;
using MotionControl.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
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
using Core.Utilities;
using Core.Events;
using System.Collections.Concurrent;
using System.Windows;
using System.Net.Sockets;
using ModuleCore.Views;
using MaterialDesignThemes.Wpf;
using System.Windows.Media;
using System.IO;
using System.Reflection;
using Core.Services;
using Core.Abstraction;
using Framework.Mvvm;
using Recipe.Events;
using Core.ViewModels;

namespace ModuleCore.ViewModels
{
    public class MainWindowViewModel : LocalizedViewModelBase
    {
        private string _title;

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

        private string _recipeName;
        public string RecipeName
        {
            get => _recipeName;
            set => SetProperty(ref _recipeName, value);
        }
        // SEC/GEM 状态相关属性
        private string _secsStatusText;
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

        // EtherCAT 总线状态（MainWindow 底部状态栏）
        private string _etherCatStatusText = string.Empty;
        public string EtherCatStatusText
        {
            get => _etherCatStatusText;
            set => SetProperty(ref _etherCatStatusText, value);
        }

        private Brush _etherCatStatusColor = Brushes.Gray;
        public Brush EtherCatStatusColor
        {
            get => _etherCatStatusColor;
            set => SetProperty(ref _etherCatStatusColor, value);
        }

        public LoginModel Model { get; set; }
        public NavigateModel Navigate { get; set; }
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppSettingService _appConfig;
        private readonly ILoggerService _logger;
        private readonly IMotionService _motionService;
        private readonly IAxisOperationPanelState _axisPanelState;
        private SubscriptionToken _refreshToken;
        private SubscriptionToken _secsCommandToken;
        public MainWindowViewModel(IDialogService dialogService,
                                   IRegionManager regionManager,
                                   IContainerExtension container,
                                   IEventAggregator eventAggregator,
                                   IAppSettingService appConfig,
                                   ILoggerService logger,
                                   IMotionService motionService,
                                   IAxisOperationPanelState axisPanelState,
                                   ILocalizationService localizationService)
            : base(localizationService, eventAggregator)
        {
            _regionManager = regionManager;
            _dialogService = dialogService;
            _appConfig = appConfig;
            _logger = logger;
            _motionService = motionService;
            _axisPanelState = axisPanelState;
            Model = container.Resolve<LoginModel>();
            Navigate = container.Resolve<NavigateModel>();
            RecipeName = L("MainWindow_RecipePoolPrefix") + _appConfig.RecipeName;
            Title = L("MainWindow_ApplicationTitle");
            _eventAggregator = eventAggregator;
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
            // 获取程序集版本
            var version = Assembly.GetEntryAssembly().GetName().Version;
            AppVersion = $"v{version.Major}.{version.Minor}.{version.Build}";

            // 记录系统启动日志
            _logger.Info("========================================");
            _logger.Info($"系统启动 - 版本 {AppVersion}");
            _logger.Info($"配方: {_appConfig.RecipeName}");
            _logger.Info("========================================");

            // 发布系统初始化完成事件
            _eventAggregator.GetEvent<SystemInitializedEvent>().Publish();

            // 初始化工业控制按钮组命令（通过EventAggregator与OverViewModel通信）
            InitializeCommand = new DelegateCommand(OnInitializeFromMain);
            StartCommand = new DelegateCommand(OnStartFromMain);
            PauseCommand = new DelegateCommand(OnPauseFromMain);
            ResumeCommand = new DelegateCommand(OnResumeFromMain);
            StopCommand = new DelegateCommand(OnStopFromMain);

            // 订阅系统状态变化事件，驱动IsSystemRunning属性更新
            _eventAggregator.GetEvent<StationStateChangedEvent>().Subscribe(OnStationStateChanged, ThreadOption.PublisherThread, false);

            // EtherCAT 总线状态（MotionService 轮询发布；构造时主动拉取，避免错过 InitializeAsync 事件）
            _eventAggregator.GetEvent<EtherCatBusStatusChangedEvent>().Subscribe(OnEtherCatBusStatusChanged, ThreadOption.UIThread);
            RefreshEtherCatBusStatus();
            Application.Current?.Dispatcher.BeginInvoke(RefreshEtherCatBusStatus, DispatcherPriority.Loaded);

            InitializeCommands(); // 初始化命令
            LoadDefaultView(appConfig, container); // 加载默认视图
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
                _logger.Info($"【WarningDialog】{notification.FormattedMessage}");
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
                RecipeName = L("MainWindow_CurrentRecipePrefix") + recipeName;
            }
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

        private void MessageReceived(Core.Events.Message message)
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
        private async void LoadDefaultView(IAppSettingService appConfig, IContainerExtension container)
        {
            await Task.Delay(1000);//延迟加载，否则不显示主视图
            ViewDisplayLoad();
            var defaultView = Navigate.DefaultView;
            NavigateCommand.Execute(defaultView);
            ShowLogViewer();
            //ShowLoginDialog(); // 显示登录对话框
        }
        private void InitialSecs()
        {
            //if (_secsGemService.Initialize(5000, "0"))
            //{
            //    SecsStatusText = "已连接";
            //    SecsStatusColor = Brushes.Green;
            //    IMessage.Logger.Info("SECS/GEM初始化成功");
            //}
            //else
            //{
                SecsStatusText = L("MainWindow_SecsOffline");
                SecsStatusColor = Brushes.Red;
            //}
        }
        // 重新连接命令
        public ICommand ReconnectSecsCommand => new DelegateCommand(() =>
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
                AddErr(new ErrModel() { ErrMsg = L("MainWindow_ErrAdminRequired") });
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
                    Title = L("MainWindow_ApplicationTitle");
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
                _dialogService.Show("LogView", parameters, r => { });
            }, DispatcherPriority.Background);
        }


        private DelegateCommand _Start;

        public DelegateCommand Start =>
             _Start ??= new DelegateCommand(ExecuteStart);

        private async void ExecuteStart()
        {

        }

        private DelegateCommand _Pause;

        public DelegateCommand Pause =>
             _Pause ??= new DelegateCommand(ExecutePause);

        private void ExecutePause()
        {

        }

        private DelegateCommand _Continue;

        public DelegateCommand Continue =>
             _Continue ??= new DelegateCommand(ExecuteContinue);

        private void ExecuteContinue()
        {

        }

        private DelegateCommand _Stop;

        public DelegateCommand Stop =>
             _Stop ??= new DelegateCommand(ExecuteStop);

        private void ExecuteStop()
        {

        }
        private DelegateCommand _Initialize;

        public DelegateCommand Initialize =>
             _Initialize ??= new DelegateCommand(ExecuteInitialize);

        private async void ExecuteInitialize()
        {

        }
        public void CloseSecsGemService()
        {

        }
        private bool ShowExecuteInitializeDialog()
        {
            var result = Framework.Services.DialogService.ShowBlockingDialog(
                title: L("MainWindow_InitConfirmTitle"),
                message: L("MainWindow_InitConfirmMsg") + "\n\n",
                yesButtonText: L("MainWindow_InitConfirmYes"),
                noButtonText: L("MainWindow_InitConfirmNo"),
                showYesButton: true,
                showNoButton: true,
                icon: PackIconKind.ClockAlert
            );

            if ((int)result == 0) // YES
            {
                _logger.Info("用户选择执行初始化操作");
                return true;
            }
            else
            {
                _logger.Info("用户选择取消初始化操作");
            }
            return false;
        }

        #region 任务监控窗口相关
        public DelegateCommand ShowTaskMonitorCommand { get; private set; }

        /// <summary>
        /// 轴控制面板是否打开（绑定到右侧 Drawer）
        /// </summary>
        private bool _isAxisPanelOpen;
        public bool IsAxisPanelOpen
        {
            get => _isAxisPanelOpen;
            set
            {
                if (SetProperty(ref _isAxisPanelOpen, value))
                    _axisPanelState?.SetPanelOpen(value);
            }
        }

        #region 工业控制按钮组 - 命令与状态属性

        private bool _isSystemRunning = false;
        public bool IsSystemRunning
        {
            get => _isSystemRunning;
            set => SetProperty(ref _isSystemRunning, value);
        }

        public DelegateCommand InitializeCommand { get; }
        public DelegateCommand StartCommand { get; }
        public DelegateCommand PauseCommand { get; }
        public DelegateCommand ResumeCommand { get; }
        public DelegateCommand StopCommand { get; }

        #endregion

        private void InitializeCommands()
        {
            // 添加显示任务监控窗口的命令
            OpenAxisOperationCommand = new DelegateCommand(OpenAxisOperation);
        }

        #endregion

        public ICommand OpenAxisOperationCommand { get; private set; }
        private void OpenAxisOperation()
        {
            // 新实现：切换轴控制面板（替代旧对话框）
            IsAxisPanelOpen = !IsAxisPanelOpen;

            // 保留旧对话框作为备用（已废弃）
            //_dialogService.ShowDialog("AxisOperationView", null, result =>
            //{
            //    if (result.Result == ButtonResult.OK)
            //    {
            //        // 处理确认后的逻辑
            //    }
            //});
        }

        #region 工业控制按钮组 - 命令实现

        /// <summary>
        /// 初始化按钮点击事件 - 发布初始化请求事件
        /// </summary>
        private void OnInitializeFromMain()
        {
            _logger.Info("【MainWindow】用户点击初始化按钮");
            _eventAggregator.GetEvent<ControlButtonClickedEvent>().Publish("Initialize");
        }

        /// <summary>
        /// 启动按钮点击事件 - 发布启动请求事件
        /// </summary>
        private void OnStartFromMain()
        {
            _logger.Info("【MainWindow】用户点击启动按钮");
            _eventAggregator.GetEvent<ControlButtonClickedEvent>().Publish("Start");
        }

        /// <summary>
        /// 暂停按钮点击事件 - 发布暂停请求事件
        /// </summary>
        private void OnPauseFromMain()
        {
            _logger.Info("【MainWindow】用户点击暂停按钮");
            _eventAggregator.GetEvent<ControlButtonClickedEvent>().Publish("Pause");
        }

        /// <summary>
        /// 恢复按钮点击事件 - 发布恢复请求事件
        /// </summary>
        private void OnResumeFromMain()
        {
            _logger.Info("【MainWindow】用户点击恢复按钮");
            _eventAggregator.GetEvent<ControlButtonClickedEvent>().Publish("Resume");
        }

        /// <summary>
        /// 停止按钮点击事件 - 发布停止请求事件
        /// </summary>
        private void OnStopFromMain()
        {
            _logger.Info("【MainWindow】用户点击停止按钮");
            _eventAggregator.GetEvent<ControlButtonClickedEvent>().Publish("Stop");
        }

        /// <summary>
        /// 系统状态变化回调 - 更新IsSystemRunning属性
        /// </summary>
        private void OnStationStateChanged(StationStatePayload payload)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
                return;

            try
            {
                dispatcher.Invoke(() =>
                {
                    IsSystemRunning = payload.State == StationState.RUNNING;
                });
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
        }

        #endregion

        /// <summary>EtherCAT 总线状态变更：更新底部状态栏</summary>
        private void OnEtherCatBusStatusChanged(EtherCatBusStatusPayload payload)
        {
            UpdateEtherCatStatusDisplay(payload);
        }

        /// <summary>从 MotionService 读取当前总线状态（真实硬件时 IsSimulation=false）</summary>
        private void RefreshEtherCatBusStatus()
        {
            if (_motionService == null)
                return;

            UpdateEtherCatStatusDisplay(new EtherCatBusStatusPayload
            {
                ErrorCode = _motionService.GetEtherCatBusErrorCode(),
                IsSimulation = _motionService.IsSimulationMode
            });
        }

        private void UpdateEtherCatStatusDisplay(EtherCatBusStatusPayload payload)
        {
            _lastEtherCatErrorCode = payload.ErrorCode;
            _etherCatIsSimulation = payload.IsSimulation;

            if (payload.IsSimulation)
            {
                EtherCatStatusText = L("MainWindow_EtherCatSimulated");
                EtherCatStatusColor = Brushes.Gray;
                return;
            }

            if (payload.ErrorCode == 0)
            {
                EtherCatStatusText = L("MainWindow_EtherCatNormal");
                EtherCatStatusColor = Brushes.LimeGreen;
            }
            else
            {
                EtherCatStatusText = string.Format(L("MainWindow_EtherCatError"), payload.ErrorCode);
                EtherCatStatusColor = Brushes.Red;
            }
        }

        /// <summary>
        /// 语言切换时刷新导航菜单 DisplayName 及其他依赖多语言的属性
        /// </summary>
        protected override void OnLanguageChanged()
        {
            base.OnLanguageChanged();

            foreach (var item in Navigate.NavigateList)
            {
                if (!string.IsNullOrEmpty(item.DisplayNameKey))
                {
                    item.DisplayName = L(item.DisplayNameKey);
                }
            }

            foreach (var item in Navigate.NavigateShowList)
            {
                if (!string.IsNullOrEmpty(item.DisplayNameKey))
                {
                    item.DisplayName = L(item.DisplayNameKey);
                }
            }

            RecipeName = L("MainWindow_RecipePoolPrefix") + _appConfig.RecipeName;
            SecsStatusText = L("MainWindow_SecsOffline");

            // 刷新 EtherCAT 文案（保留当前错误码）
            UpdateEtherCatStatusDisplay(new EtherCatBusStatusPayload
            {
                ErrorCode = _lastEtherCatErrorCode,
                IsSimulation = _etherCatIsSimulation
            });
        }

        private int _lastEtherCatErrorCode;
        private bool _etherCatIsSimulation;
    }
}
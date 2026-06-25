using System;
using System.Windows.Input;
using System.Windows.Media;
using Core.Abstraction;
using Core.Utilities;
using MaterialDesignThemes.Wpf;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Ioc;
using Prism.Services.Dialogs;

namespace Module.UserControls.Grippers
{
    /// <summary>
    /// 夹爪控制面板 ViewModel
    /// 实现 IDialogCloseable 以支持 BaseDialogService 统一弹窗关闭机制
    /// </summary>
    public class GripperControlViewModel : BindableBase, IDialogCloseable, IDisposable
    {
        private readonly IGripperService _gripperService;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private readonly IContainerProvider _containerProvider;

        /// <summary>
        /// 获取多语言文本（便捷方法）
        /// </summary>
        private string L(string key) => _containerProvider.Resolve<ILocalizationService>().GetResource(key);

        #region UI绑定属性

        private double _targetPosition = 500;
        public double TargetPosition
        {
            get => _targetPosition;
            set => SetProperty(ref _targetPosition, value);
        }

        /// <summary>电爪运动速度（1-100%），用于移动、寸动、夹紧、释放</summary>
        private double _speed;
        public double Speed
        {
            get => _speed;
            set
            {
                if (SetProperty(ref _speed, value))
                    _gripperService.ManualOperationSpeed = value;
            }
        }

        private double _jogStep = 5;
        public double JogStep
        {
            get => _jogStep;
            set => SetProperty(ref _jogStep, value);
        }

        private string _torquePercentage = "50";
        public string TorquePercentage
        {
            get => _torquePercentage;
            set
            {
                if (SetProperty(ref _torquePercentage, value))
                    RaisePropertyChanged(nameof(TorqueDisplay));
            }
        }

        public string TorqueDisplay
        {
            get
            {
                if (double.TryParse(TorquePercentage, out double pct))
                    return $"{pct * 0.15:F1} N";
                return "0.0 N";
            }
        }

        private double _currentPosition;
        public double CurrentPosition
        {
            get => _currentPosition;
            set => SetProperty(ref _currentPosition, value);
        }

        private GripperStatus _status = GripperStatus.Unknown;
        public GripperStatus Status
        {
            get => _status;
            set 
            { 
                if (SetProperty(ref _status, value))
                    RaisePropertyChanged(nameof(StatusBrush));
            }
        }

        public Brush StatusBrush => _status switch
        {
            GripperStatus.Unknown => Brushes.Gray,
            GripperStatus.Idle => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            GripperStatus.Moving => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            GripperStatus.Clamping => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
            GripperStatus.Clamped => new SolidColorBrush(Color.FromRgb(230, 81, 0)),
            GripperStatus.Releasing => new SolidColorBrush(Color.FromRgb(156, 39, 176)),
            GripperStatus.Error => Brushes.Red,
            GripperStatus.Homing => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            _ => Brushes.Gray
        };

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage ?? (_statusMessage = L("Gripper_Status_Uninitialized"));
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        #endregion

        #region 命令定义

        public ICommand ClampCommand { get; }
        public ICommand ReleaseCommand { get; }
        public ICommand MoveToTargetCommand { get; }
        public ICommand JogLeftCommand { get; }
        public ICommand JogRightCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand SetTorqueCommand { get; }
        public ICommand HomeCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand CloseCommand { get; }

        #endregion

        public GripperControlViewModel(
            IGripperService gripperService,
            ILoggerService logger,
            IDialogService dialogService,
            IContainerProvider containerProvider)
        {
            _gripperService = gripperService;
            _logger = logger;
            _dialogService = dialogService;
            _containerProvider = containerProvider;

            ClampCommand = new DelegateCommand(async () => await ExecuteClamp());
            ReleaseCommand = new DelegateCommand(async () => await ExecuteRelease());
            MoveToTargetCommand = new DelegateCommand(async () => await ExecuteMoveToTarget());
            JogLeftCommand = new DelegateCommand(async () => await ExecuteJogLeft());
            JogRightCommand = new DelegateCommand(async () => await ExecuteJogRight());
            StopCommand = new DelegateCommand(ExecuteStop);
            SetTorqueCommand = new DelegateCommand(ExecuteSetTorque);
            HomeCommand = new DelegateCommand(async () => await ExecuteHome());
            ResetCommand = new DelegateCommand(ExecuteReset);
            CloseCommand = new DelegateCommand(OnClose);
        }

        #region 命令实现

        private async System.Threading.Tasks.Task ExecuteClamp()
        {
            if (!CheckSafety()) return;
            try
            {
                StatusMessage = L("Gripper_Clamping_InProgress");
                var clampPos = ExternalClampPosition ?? TargetPosition;
                await _gripperService.ClampAsync(clampPos);
                StatusMessage = string.Format(L("Gripper_Clamp_Done"), clampPos);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private async System.Threading.Tasks.Task ExecuteRelease()
        {
            if (!CheckSafety()) return;
            try
            {
                StatusMessage = L("Gripper_Releasing_InProgress");
                var releasePos = ExternalReleasePosition ?? TargetPosition;
                await _gripperService.ReleaseAsync(releasePos);
                StatusMessage = string.Format(L("Gripper_Release_Done"), releasePos);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private async System.Threading.Tasks.Task ExecuteMoveToTarget()
        {
            if (!CheckSafety()) return;
            try
            {
                StatusMessage = string.Format(L("Gripper_Moving_To"), TargetPosition);
                await _gripperService.MoveToPositionAsync(TargetPosition, Speed);
                StatusMessage = L("Gripper_Move_Done");
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private async System.Threading.Tasks.Task ExecuteJogLeft()
        {
            if (!CheckSafety()) return;
            try
            {
                await _gripperService.JogLeftAsync(JogStep, Speed);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private async System.Threading.Tasks.Task ExecuteJogRight()
        {
            if (!CheckSafety()) return;
            try
            {
                await _gripperService.JogRightAsync(JogStep, Speed);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private void ExecuteStop()
        {
            _gripperService.Stop();
            StatusMessage = L("Gripper_Stopped");
        }

        private void ExecuteSetTorque()
        {
            if (!double.TryParse(TorquePercentage, out double pct))
            {
                ShowDialog(L("Gripper_Dialog_InputError_Title"), L("Gripper_Dialog_InputError_Msg"), PackIconKind.AlertCircle);
                return;
            }
            if (pct < 0 || pct > 100)
            {
                ShowDialog(L("Gripper_Dialog_ParamError_Title"), L("Gripper_Dialog_ParamError_Msg"), PackIconKind.AlertCircle);
                return;
            }

            _gripperService.SetTorque(pct);
            _logger.Info(string.Format(L("Gripper_Log_TorqueSet"), pct, (pct * 0.15).ToString("F1")));
            StatusMessage = string.Format(L("Gripper_Torque_Set"), pct);
        }

        private async System.Threading.Tasks.Task ExecuteHome()
        {
            if (!CheckSafety()) return;
            try
            {
                StatusMessage = L("Gripper_Homing_InProgress");
                await _gripperService.HomeAsync();
                StatusMessage = L("Gripper_Home_Done");
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
        }

        private void ExecuteReset()
        {
            // TODO: 电夹爪暂无报警清除功能，留空
        }

        private void OnClose()
        {
            RequestClose?.Invoke(true);
        }

        #endregion

        #region 外部参数支持（供 PickDetailView 传入）

        public double? ExternalClampPosition { get; set; }
        public double? ExternalReleasePosition { get; set; }

        public void SetExternalPositions(double clampPos, double releasePos)
        {
            ExternalClampPosition = clampPos;
            ExternalReleasePosition = releasePos;
            RaisePropertyChanged(nameof(ExternalClampPosition));
        }

        #endregion

        #region 安全检查和错误处理

        private bool CheckSafety()
        {
            if (!_gripperService.IsInitialized)
            {
                ShowDialog(L("Gripper_Error_ServiceNotInit_Title"), L("Gripper_Error_ServiceNotInit_Msg"), PackIconKind.AlertCircle);
                return false;
            }
            return true;
        }

        private void HandleError(Exception ex)
        {
            _logger.Error(string.Format(L("Gripper_Log_OperationFailed"), ex.Message));
            StatusMessage = string.Format(L("Gripper_Error_Status"), ex.Message);
            ShowDialog(L("Gripper_Dialog_OperationFailed_Title"), ex.Message, PackIconKind.AlertCircle);
        }

        private void ShowDialog(string title, string message, PackIconKind icon)
        {
            _dialogService.ShowDialog("NotificationDialog", new DialogParameters
            {
                { "title", title },
                { "message", message },
                { "icon", icon }
            }, result => { });
        }

        #endregion

        #region IDialogCloseable 实现

        /// <summary>UI 定时器：定时刷新夹爪状态</summary>
        private System.Windows.Threading.DispatcherTimer _uiUpdateTimer;

        /// <summary>请求关闭对话框时触发（BaseDialogService 订阅）</summary>
        public event Action<object> RequestClose;

        /// <summary>是否可以关闭对话框</summary>
        public bool CanCloseDialog() => true;

        /// <summary>
        /// 初始化夹爪控制面板（替代 IDialogAware.OnDialogOpened）
        /// 启动 UI 定时器和夹爪状态监控
        /// </summary>
        /// <param name="clampPos">外部夹紧位置（可选）</param>
        /// <param name="releasePos">外部释放位置（可选）</param>
        public void Initialize(double? clampPos = null, double? releasePos = null)
        {
            if (clampPos.HasValue)
                ExternalClampPosition = clampPos;
            if (releasePos.HasValue)
                ExternalReleasePosition = releasePos;

            // 从服务读取上次面板设置的速度，供本面板及外部快捷按钮共用
            _speed = _gripperService.ManualOperationSpeed;
            RaisePropertyChanged(nameof(Speed));

            _uiUpdateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _uiUpdateTimer.Tick += (s, e) =>
            {
                try
                {
                    if (_gripperService.IsInitialized)
                    {
                        var state = _gripperService.GetState();
                        CurrentPosition = state.CurrentPosition;
                        Status = state.Status;
                        IsConnected = true;

                        if (StatusMessage == L("Gripper_Status_Uninitialized"))
                            StatusMessage = L("Gripper_Status_Ready");
                    }
                }
                catch { }
            };
            _uiUpdateTimer.Start();

            _gripperService.StartMonitoring(200);
            _logger.Info(L("Gripper_Log_DialogOpened"));
        }

        #endregion

        public void Dispose()
        {
            // 关闭面板时持久化速度，供 Pick 详情页快捷夹紧/释放使用
            _gripperService.ManualOperationSpeed = Speed;
            _uiUpdateTimer?.Stop();
            _uiUpdateTimer = null;
            _gripperService?.StopMonitoring();
        }
    }
}

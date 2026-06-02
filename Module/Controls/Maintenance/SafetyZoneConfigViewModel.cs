using Core.Services;
using Core.Utilities;
using MotionControl.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace Module.ViewModels
{
    public class SafetyZoneConfigViewModel : BindableBase
    {
        private readonly ISafetyZoneMonitor _safetyZoneMonitor;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;

        /// <summary>定时刷新间隔（毫秒），用于实时显示轴位置</summary>
        private const int RefreshIntervalMs = 500;

        /// <summary>配置文件存储路径</summary>
        private static string ConfigFilePath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Config", "SafetyZoneConfig.json");

        private SafetyZoneConfig _config = new();

        #region 配置属性（绑定到UI参数控件）

        private double _safeHeightZ1;
        /// <summary>Z₁安全高度阈值（mm），低于此值触发互锁保护</summary>
        public double SafeHeightZ1
        {
            get => _safeHeightZ1;
            set
            {
                if (SetProperty(ref _safeHeightZ1, value))
                {
                    _config.SafeHeightZ1 = value;
                    SyncConfigToMonitor();
                }
            }
        }

        private double _dangerZoneXMin;
        /// <summary>X轴危险区下限（mm）</summary>
        public double DangerZoneXMin
        {
            get => _dangerZoneXMin;
            set
            {
                if (SetProperty(ref _dangerZoneXMin, value))
                {
                    _config.DangerZoneXMin = value;
                    SyncConfigToMonitor();
                }
            }
        }

        private double _dangerZoneXMax;
        /// <summary>X轴危险区上限（mm）</summary>
        public double DangerZoneXMax
        {
            get => _dangerZoneXMax;
            set
            {
                if (SetProperty(ref _dangerZoneXMax, value))
                {
                    _config.DangerZoneXMax = value;
                    SyncConfigToMonitor();
                }
            }
        }

        private double _dangerZoneYMin;
        /// <summary>Y轴危险区下限（mm）</summary>
        public double DangerZoneYMin
        {
            get => _dangerZoneYMin;
            set
            {
                if (SetProperty(ref _dangerZoneYMin, value))
                {
                    _config.DangerZoneYMin = value;
                    SyncConfigToMonitor();
                }
            }
        }

        private double _dangerZoneYMax;
        /// <summary>Y轴危险区上限（mm）</summary>
        public double DangerZoneYMax
        {
            get => _dangerZoneYMax;
            set
            {
                if (SetProperty(ref _dangerZoneYMax, value))
                {
                    _config.DangerZoneYMax = value;
                    SyncConfigToMonitor();
                }
            }
        }

        private bool _enabled;
        /// <summary>是否启用安全互锁功能</summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (SetProperty(ref _enabled, value))
                {
                    _config.Enabled = value;
                    SyncConfigToMonitor();
                }
            }
        }

        #endregion

        #region 实时显示属性（从GetSafetyStatus()获取）

        private double _currentX;
        /// <summary>X轴当前位置（实时刷新）</summary>
        public double CurrentX
        {
            get => _currentX;
            set => SetProperty(ref _currentX, value);
        }

        private double _currentY;
        /// <summary>Y轴当前位置（实时刷新）</summary>
        public double CurrentY
        {
            get => _currentY;
            set => SetProperty(ref _currentY, value);
        }

        private double _currentZ1;
        /// <summary>Z₁轴当前位置（实时刷新）</summary>
        public double CurrentZ1
        {
            get => _currentZ1;
            set => SetProperty(ref _currentZ1, value);
        }

        private bool _isXInDanger;
        /// <summary>X轴是否处于危险区域内</summary>
        public bool IsXInDanger
        {
            get => _isXInDanger;
            set => SetProperty(ref _isXInDanger, value);
        }

        private bool _isYInDanger;
        /// <summary>Y轴是否处于危险区域内</summary>
        public bool IsYInDanger
        {
            get => _isYInDanger;
            set => SetProperty(ref _isYInDanger, value);
        }

        private bool _isZ1BelowSafe;
        /// <summary>Z₁是否低于安全高度阈值</summary>
        public bool IsZ1BelowSafe
        {
            get => _isZ1BelowSafe;
            set => SetProperty(ref _isZ1BelowSafe, value);
        }

        #endregion

        #region 告警属性

        private string _alarmMessage = string.Empty;
        /// <summary>违规告警文本，无告警时为空字符串</summary>
        public string AlarmMessage
        {
            get => _alarmMessage;
            set => SetProperty(ref _alarmMessage, value);
        }

        private Visibility _isAlarmVisibility = Visibility.Collapsed;
        /// <summary>告警区域可见性：正常时Collapsed，违规时Visible</summary>
        public Visibility IsAlarmVisible
        {
            get => _isAlarmVisibility;
            set => SetProperty(ref _isAlarmVisibility, value);
        }

        #endregion

        #region 命令

        /// <summary>保存配置命令：将当前参数序列化为JSON并写入文件，弹出成功对话框</summary>
        public DelegateCommand SaveCommand { get; }

        /// <summary>关闭/清除告警命令：清空告警文本并隐藏告警栏</summary>
        public DelegateCommand DismissAlarmCommand { get; }

        #endregion

        /// <summary>定时器：每500ms刷新一次轴位置和危险状态</summary>
        private Timer _refreshTimer;

        public SafetyZoneConfigViewModel(
            ISafetyZoneMonitor safetyZoneMonitor,
            IEventAggregator eventAggregator,
            ILoggerService logger,
            IDialogService dialogService)
        {
            _safetyZoneMonitor = safetyZoneMonitor ?? throw new ArgumentNullException(nameof(safetyZoneMonitor));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            SaveCommand = new DelegateCommand(ExecuteSave);
            DismissAlarmCommand = new DelegateCommand(ExecuteDismissAlarm);

            Initialize();
        }

        /// <summary>
        /// 初始化：加载JSON配置文件、订阅安全违规事件、启动位置刷新定时器
        /// </summary>
        public void Initialize()
        {
            LoadConfigFromJson();

            // 订阅安全违规事件，在UI线程接收并更新告警信息
            _eventAggregator.GetEvent<SafetyViolationEvent>().Subscribe(
                OnSafetyViolation,
                ThreadOption.UIThread);

            // 启动500ms周期定时器，实时刷新各轴位置与危险状态
            _refreshTimer = new Timer(
                _ => Application.Current?.Dispatcher.BeginInvoke((Action)RefreshStatus),
                null,
                RefreshIntervalMs,
                RefreshIntervalMs);

            // 立即执行一次刷新，避免初始空白
            RefreshStatus();
        }

        /// <summary>
        /// 安全违规事件处理：更新告警文本并将告警区域设为可见
        /// </summary>
        private void OnSafetyViolation(SafetyViolationEvent e)
        {
            if (e == null) return;

            AlarmMessage = $"{e.Timestamp:HH:mm:ss} | {e.Reason}";
            IsAlarmVisible = Visibility.Visible;

            _logger.Warn($"[安全区域] 违规告警 | 轴:{e.AxisName}(#{e.AxisId}) | 原因:{e.Reason}");
        }

        /// <summary>
        /// 刷新实时位置与危险状态：调用GetSafetyStatus()并更新所有显示属性
        /// 运动控制场景需保证快速响应，此处仅做数据读取不做阻塞操作
        /// </summary>
        private void RefreshStatus()
        {
            try
            {
                var status = _safetyZoneMonitor.GetSafetyStatus();
                if (status == null) return;

                // 提取Dx/Dy/Dz₁轴的实时位置
                status.CurrentPositions.TryGetValue("Dx", out var x);
                status.CurrentPositions.TryGetValue("Dy", out var y);
                // Z₁轴名称可能为 Dz₁ 或 Dz3
                if (!status.CurrentPositions.TryGetValue("Dz₁", out var z1))
                    status.CurrentPositions.TryGetValue("Dz3", out z1);

                CurrentX = x;
                CurrentY = y;
                CurrentZ1 = z1;

                // 更新危险区标志
                status.DangerZoneFlags.TryGetValue("Dx", out var xDanger);
                status.DangerZoneFlags.TryGetValue("Dy", out var yDanger);
                IsXInDanger = xDanger;
                IsYInDanger = yDanger;
                IsZ1BelowSafe = status.IsZ1BelowSafeHeight;
            }
            catch (Exception ex)
            {
                _logger.Warn($"[安全区域] 刷新状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存配置：将当前参数写入JSON文件并通过对话框通知操作员
        /// </summary>
        private void ExecuteSave()
        {
            try
            {
                EnsureConfigDirectory();
                var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, json);

                // 同步更新到安全监控服务
                SyncConfigToMonitor();

                _logger.Info("[安全区域] 配置已保存");

                _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                {
                    { "title", "SafetyZone_SaveSuccess" },
                    { "message", "SafetyZone_SaveSuccessMessage" },
                    { "icon", MaterialDesignThemes.Wpf.PackIconKind.CheckCircle },
                    { "color", "#43A047" }
                }, null);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "[安全区域] 保存配置失败");
                _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                {
                    { "title", "Error" },
                    { "message", ex.Message },
                    { "icon", MaterialDesignThemes.Wpf.PackIconKind.AlertCircle },
                    { "color", "#E53935" }
                }, null);
            }
        }

        /// <summary>
        /// 关闭/清除告警：清空告警文本并将告警区域折叠
        /// </summary>
        private void ExecuteDismissAlarm()
        {
            AlarmMessage = string.Empty;
            IsAlarmVisible = Visibility.Collapsed;
        }

        /// <summary>
        /// 从JSON文件加载配置，文件不存在时使用默认值
        /// </summary>
        private void LoadConfigFromJson()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    _logger.Info("[安全区域] 未找到配置文件，使用默认值");
                    ApplyConfigToProperties(new SafetyZoneConfig());
                    return;
                }

                var json = File.ReadAllText(ConfigFilePath);
                var loaded = JsonConvert.DeserializeObject<SafetyZoneConfig>(json);
                if (loaded != null)
                {
                    _config = loaded;
                    ApplyConfigToProperties(_config);
                    SyncConfigToMonitor();
                    _logger.Info("[安全区域] 配置已从文件加载");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"[安全区域] 加载配置文件失败: {ex.Message}，使用默认值");
                ApplyConfigToProperties(new SafetyZoneConfig());
            }
        }

        /// <summary>
        /// 将内部配置对象属性值同步到ViewModel绑定属性，触发UI更新
        /// </summary>
        private void ApplyConfigToProperties(SafetyZoneConfig cfg)
        {
            SafeHeightZ1 = cfg.SafeHeightZ1;
            DangerZoneXMin = cfg.DangerZoneXMin;
            DangerZoneXMax = cfg.DangerZoneXMax;
            DangerZoneYMin = cfg.DangerZoneYMin;
            DangerZoneYMax = cfg.DangerZoneYMax;
            Enabled = cfg.Enabled;
        }

        /// <summary>
        /// 将当前配置同步到ISafetyZoneMonitor服务，使运行时安全检查立即生效
        /// UI修改参数后需即时推送到监控服务，确保运动控制的快速响应性
        /// </summary>
        private void SyncConfigToMonitor()
        {
            try
            {
                _safetyZoneMonitor.UpdateConfig(_config);
            }
            catch (Exception ex)
            {
                _logger.Warn($"[安全区域] 同步配置到监控服务失败: {ex.Message}");
            }
        }

        /// <summary>确保配置文件所在目录存在</summary>
        private static void EnsureConfigDirectory()
        {
            var dir = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}

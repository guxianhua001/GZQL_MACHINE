using Core.Utilities;
using MotionControl.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace Module.ViewModels
{
    /// <summary>
    /// 安全区域配置 ViewModel：编辑 JSON 规则（高度轴锁平面轴），实时显示互锁状态
    /// </summary>
    public class SafetyZoneConfigViewModel : BindableBase
    {
        private readonly ISafetyZoneMonitor _safetyZoneMonitor;
        private readonly ISafetyZoneConfigLoader _configLoader;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;

        /// <summary>定时刷新间隔（毫秒）</summary>
        private const int RefreshIntervalMs = 500;

        private const string AxisDz1 = "Dz₁";
        private const string AxisDz2 = "Dz₂";
        private const string AxisDz3 = "Dz₃";

        private SafetyZoneConfig _config = SafetyZoneConfig.CreateDefaultForCurrentMachine();

        #region 配置属性

        private double _safeHeightZ1;
        public double SafeHeightZ1
        {
            get => _safeHeightZ1;
            set
            {
                if (SetProperty(ref _safeHeightZ1, value))
                {
                    _config.SetSafeHeightForAxis(AxisDz1, value);
                    SyncConfigToMonitor();
                }
            }
        }

        private double _safeHeightZ2;
        public double SafeHeightZ2
        {
            get => _safeHeightZ2;
            set
            {
                if (SetProperty(ref _safeHeightZ2, value))
                {
                    _config.SetSafeHeightForAxis(AxisDz2, value);
                    SyncConfigToMonitor();
                }
            }
        }

        private double _safeHeightZ3;
        public double SafeHeightZ3
        {
            get => _safeHeightZ3;
            set
            {
                if (SetProperty(ref _safeHeightZ3, value))
                {
                    _config.SetSafeHeightForAxis(AxisDz3, value);
                    SyncConfigToMonitor();
                }
            }
        }

        private double _dangerZoneXMin;
        public double DangerZoneXMin
        {
            get => _dangerZoneXMin;
            set
            {
                if (SetProperty(ref _dangerZoneXMin, value))
                {
                    SetDangerZone("Dx", min: value);
                    SyncConfigToMonitor();
                }
            }
        }

        private double _dangerZoneXMax;
        public double DangerZoneXMax
        {
            get => _dangerZoneXMax;
            set
            {
                if (SetProperty(ref _dangerZoneXMax, value))
                {
                    SetDangerZone("Dx", max: value);
                    SyncConfigToMonitor();
                }
            }
        }

        private double _dangerZoneYMin;
        public double DangerZoneYMin
        {
            get => _dangerZoneYMin;
            set
            {
                if (SetProperty(ref _dangerZoneYMin, value))
                {
                    SetDangerZone("Dy", min: value);
                    SyncConfigToMonitor();
                }
            }
        }

        private double _dangerZoneYMax;
        public double DangerZoneYMax
        {
            get => _dangerZoneYMax;
            set
            {
                if (SetProperty(ref _dangerZoneYMax, value))
                {
                    SetDangerZone("Dy", max: value);
                    SyncConfigToMonitor();
                }
            }
        }

        private bool _enabled;
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

        #region 实时显示

        public double CurrentX { get => _currentX; set => SetProperty(ref _currentX, value); }
        private double _currentX;
        public double CurrentY { get => _currentY; set => SetProperty(ref _currentY, value); }
        private double _currentY;
        public double CurrentZ1 { get => _currentZ1; set => SetProperty(ref _currentZ1, value); }
        private double _currentZ1;
        public double CurrentZ2 { get => _currentZ2; set => SetProperty(ref _currentZ2, value); }
        private double _currentZ2;
        public double CurrentZ3 { get => _currentZ3; set => SetProperty(ref _currentZ3, value); }
        private double _currentZ3;

        public bool IsXInDanger { get => _isXInDanger; set => SetProperty(ref _isXInDanger, value); }
        private bool _isXInDanger;
        public bool IsYInDanger { get => _isYInDanger; set => SetProperty(ref _isYInDanger, value); }
        private bool _isYInDanger;

        /// <summary>Dz₁ 未达安全高度</summary>
        public bool IsZ1BelowSafe { get => _isZ1BelowSafe; set => SetProperty(ref _isZ1BelowSafe, value); }
        private bool _isZ1BelowSafe;
        public bool IsZ2BelowSafe { get => _isZ2BelowSafe; set => SetProperty(ref _isZ2BelowSafe, value); }
        private bool _isZ2BelowSafe;
        public bool IsZ3BelowSafe { get => _isZ3BelowSafe; set => SetProperty(ref _isZ3BelowSafe, value); }
        private bool _isZ3BelowSafe;

        /// <summary>任一高度轴未在安全区，Dx/Dy 被互锁</summary>
        public bool IsPlaneMovementLocked { get => _isPlaneMovementLocked; set => SetProperty(ref _isPlaneMovementLocked, value); }
        private bool _isPlaneMovementLocked;

        #endregion

        #region 告警

        public string AlarmMessage { get => _alarmMessage; set => SetProperty(ref _alarmMessage, value); }
        private string _alarmMessage = string.Empty;
        public Visibility IsAlarmVisible { get => _isAlarmVisibility; set => SetProperty(ref _isAlarmVisibility, value); }
        private Visibility _isAlarmVisibility = Visibility.Collapsed;

        #endregion

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand DismissAlarmCommand { get; }

        private Timer _refreshTimer;

        public SafetyZoneConfigViewModel(
            ISafetyZoneMonitor safetyZoneMonitor,
            ISafetyZoneConfigLoader configLoader,
            IEventAggregator eventAggregator,
            ILoggerService logger,
            IDialogService dialogService)
        {
            _safetyZoneMonitor = safetyZoneMonitor ?? throw new ArgumentNullException(nameof(safetyZoneMonitor));
            _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            SaveCommand = new DelegateCommand(ExecuteSave);
            DismissAlarmCommand = new DelegateCommand(ExecuteDismissAlarm);
            Initialize();
        }

        public void Initialize()
        {
            LoadConfig();

            _eventAggregator.GetEvent<SafetyViolationEvent>().Subscribe(
                OnSafetyViolation,
                ThreadOption.UIThread);

            _refreshTimer = new Timer(
                _ => Application.Current?.Dispatcher.BeginInvoke((Action)RefreshStatus),
                null,
                RefreshIntervalMs,
                RefreshIntervalMs);

            RefreshStatus();
        }

        private void OnSafetyViolation(SafetyViolationEvent e)
        {
            if (e == null) return;
            AlarmMessage = $"{e.Timestamp:HH:mm:ss} | {e.Reason}";
            IsAlarmVisible = Visibility.Visible;
            _logger.Warn($"[安全区域] 违规 | 轴:{e.AxisName}(#{e.AxisId}) | {e.Reason}");
        }

        private void RefreshStatus()
        {
            try
            {
                var status = _safetyZoneMonitor.GetSafetyStatus();
                if (status == null) return;

                status.CurrentPositions.TryGetValue("Dx", out var x);
                status.CurrentPositions.TryGetValue("Dy", out var y);
                status.CurrentPositions.TryGetValue(AxisDz1, out var z1);
                status.CurrentPositions.TryGetValue(AxisDz2, out var z2);
                status.CurrentPositions.TryGetValue(AxisDz3, out var z3);

                CurrentX = x;
                CurrentY = y;
                CurrentZ1 = z1;
                CurrentZ2 = z2;
                CurrentZ3 = z3;

                status.DangerZoneFlags.TryGetValue("Dx", out var xDanger);
                status.DangerZoneFlags.TryGetValue("Dy", out var yDanger);
                IsXInDanger = xDanger;
                IsYInDanger = yDanger;

                var low = status.LowHeightAxisNames ?? new System.Collections.Generic.List<string>();
                IsZ1BelowSafe = low.Contains(AxisDz1);
                IsZ2BelowSafe = low.Contains(AxisDz2);
                IsZ3BelowSafe = low.Contains(AxisDz3);
                IsPlaneMovementLocked = status.IsPlaneMovementLocked;
            }
            catch (Exception ex)
            {
                _logger.Warn($"[安全区域] 刷新状态失败: {ex.Message}");
            }
        }

        private void ExecuteSave()
        {
            try
            {
                _configLoader.Save(_config.Clone());
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

        private void ExecuteDismissAlarm()
        {
            AlarmMessage = string.Empty;
            IsAlarmVisible = Visibility.Collapsed;
        }

        private void LoadConfig()
        {
            _config = _configLoader.Load().Clone();
            ApplyConfigToProperties(_config);
            SyncConfigToMonitor();
        }

        private void ApplyConfigToProperties(SafetyZoneConfig cfg)
        {
            _config = cfg;
            SafeHeightZ1 = cfg.GetSafeHeightForAxis(AxisDz1);
            SafeHeightZ2 = cfg.GetSafeHeightForAxis(AxisDz2);
            SafeHeightZ3 = cfg.GetSafeHeightForAxis(AxisDz3);

            var dx = cfg.DangerZones.FirstOrDefault(z => z.AxisName == "Dx");
            var dy = cfg.DangerZones.FirstOrDefault(z => z.AxisName == "Dy");
            DangerZoneXMin = dx?.Min ?? 0;
            DangerZoneXMax = dx?.Max ?? 200;
            DangerZoneYMin = dy?.Min ?? 0;
            DangerZoneYMax = dy?.Max ?? 200;
            Enabled = cfg.Enabled;
        }

        private void SetDangerZone(string axisName, double? min = null, double? max = null)
        {
            var zone = _config.DangerZones.FirstOrDefault(z => z.AxisName == axisName);
            if (zone == null)
            {
                zone = new AxisDangerZoneConfig { AxisName = axisName };
                _config.DangerZones.Add(zone);
            }
            if (min.HasValue) zone.Min = min.Value;
            if (max.HasValue) zone.Max = max.Value;
        }

        /// <summary>深拷贝推送到单例监控器，Jog/运动立即使用最新 Enabled 与 Z 阈值</summary>
        private void SyncConfigToMonitor()
        {
            try
            {
                _safetyZoneMonitor.UpdateConfig(_config.Clone());
            }
            catch (Exception ex)
            {
                _logger.Warn($"[安全区域] 同步配置失败: {ex.Message}");
            }
        }
    }
}

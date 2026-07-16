using Core.Abstraction;
using Core.Utilities;
using MotionControl.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;

namespace MotionControl.ViewModels
{
    /// <summary>
    /// 安全区域配置 ViewModel：编辑 JSON 规则（高度轴锁平面轴），实时显示互锁状态。
    /// 高度轴（Z）与平面锁定轴（X/Y）均为动态列表，轴名称从当前机型 hwcfg 中选择，
    /// 不同设备可配置不同数量/名称的轴，无需修改代码即可复用本页面（通用安全互锁框架）。
    /// </summary>
    public class SafetyZoneConfigViewModel : BindableBase, IDisposable
    {
        private readonly ISafetyZoneMonitor _safetyZoneMonitor;
        private readonly ISafetyZoneConfigLoader _configLoader;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;
        private readonly IDialogService _dialogService;
        private readonly IMotionService _motionService;

        /// <summary>定时刷新间隔（毫秒）</summary>
        private const int RefreshIntervalMs = 500;

        private SafetyZoneConfig _config = SafetyZoneConfig.CreateDefaultForCurrentMachine();

        #region 全局设置

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

        /// <summary>当前机型硬件轴名称列表，供轴选择下拉框使用（避免手动输入拼写错误）</summary>
        public ObservableCollection<string> AvailableAxisNames { get; } = new();

        #endregion

        #region 高度轴（Z）动态列表

        /// <summary>高度轴配置行：动态数量，每行可单独启用/禁用是否参与互锁判断</summary>
        public ObservableCollection<HeightAxisRowViewModel> HeightAxisRows { get; } = new();

        public DelegateCommand AddHeightAxisCommand { get; }
        public DelegateCommand<HeightAxisRowViewModel> RemoveHeightAxisCommand { get; }

        #endregion

        #region 平面锁定轴（X/Y 等）动态列表

        /// <summary>平面锁定轴配置行：动态数量，2D 可视化画布取前两个轴展示</summary>
        public ObservableCollection<PlaneAxisRowViewModel> PlaneAxisRows { get; } = new();

        public DelegateCommand AddPlaneAxisCommand { get; }
        public DelegateCommand<PlaneAxisRowViewModel> RemovePlaneAxisCommand { get; }

        /// <summary>画布可视化第一个平面轴（通常对应传统意义上的 X）</summary>
        public PlaneAxisRowViewModel PlaneAxis0 => PlaneAxisRows.Count > 0 ? PlaneAxisRows[0] : null;

        /// <summary>画布可视化第二个平面轴（通常对应传统意义上的 Y）</summary>
        public PlaneAxisRowViewModel PlaneAxis1 => PlaneAxisRows.Count > 1 ? PlaneAxisRows[1] : null;

        #endregion

        #region 实时显示

        /// <summary>任一配置的高度轴未在安全区，平面轴被互锁</summary>
        public bool IsPlaneMovementLocked { get => _isPlaneMovementLocked; set => SetProperty(ref _isPlaneMovementLocked, value); }
        private bool _isPlaneMovementLocked;

        /// <summary>画布第一轴（水平）行程范围下限/上限（可视化显示用，用户可设置）</summary>
        public double XRangeMin { get => _xRangeMin; set => SetProperty(ref _xRangeMin, value); }
        private double _xRangeMin = -50;
        public double XRangeMax { get => _xRangeMax; set => SetProperty(ref _xRangeMax, value); }
        private double _xRangeMax = 250;

        /// <summary>画布第二轴（垂直）行程范围下限/上限（可视化显示用，用户可设置）</summary>
        public double YRangeMin { get => _yRangeMin; set => SetProperty(ref _yRangeMin, value); }
        private double _yRangeMin = -50;
        public double YRangeMax { get => _yRangeMax; set => SetProperty(ref _yRangeMax, value); }
        private double _yRangeMax = 250;

        /// <summary>画布尺寸（供MultiBinding使用）</summary>
        public double CanvasWidth => 400;
        public double CanvasHeight => 300;

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
            ILocalizationService localization,
            IDialogService dialogService,
            IMotionService motionService)
        {
            _safetyZoneMonitor = safetyZoneMonitor ?? throw new ArgumentNullException(nameof(safetyZoneMonitor));
            _configLoader = configLoader ?? throw new ArgumentNullException(nameof(configLoader));
            _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _localization = localization;
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _motionService = motionService ?? throw new ArgumentNullException(nameof(motionService));

            SaveCommand = new DelegateCommand(ExecuteSave);
            DismissAlarmCommand = new DelegateCommand(ExecuteDismissAlarm);
            AddHeightAxisCommand = new DelegateCommand(ExecuteAddHeightAxis);
            RemoveHeightAxisCommand = new DelegateCommand<HeightAxisRowViewModel>(ExecuteRemoveHeightAxis);
            AddPlaneAxisCommand = new DelegateCommand(ExecuteAddPlaneAxis);
            RemovePlaneAxisCommand = new DelegateCommand<PlaneAxisRowViewModel>(ExecuteRemovePlaneAxis);

            Initialize();
        }

        public void Initialize()
        {
            LoadAvailableAxisNames();
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

        /// <summary>从当前机型 hwcfg 读取硬件轴名称，供轴选择下拉框使用</summary>
        private void LoadAvailableAxisNames()
        {
            AvailableAxisNames.Clear();
            try
            {
                foreach (var axis in _motionService.GetAxisConfigurations())
                {
                    if (!string.IsNullOrWhiteSpace(axis.Name))
                        AvailableAxisNames.Add(axis.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("SZCfgVM_Log_LoadAxisNamesFailed", "[安全区域] 读取硬件轴列表失败: {0}"), ex.Message));
            }
        }

        private void OnSafetyViolation(SafetyViolationEvent e)
        {
            if (e == null) return;
            AlarmMessage = $"{e.Timestamp:HH:mm:ss} | {e.Reason}";
            IsAlarmVisible = Visibility.Visible;
            _logger.Warn(string.Format(_localization.GetResourceOrDefault("SZCfgVM_Log_Violation", "[安全区域] 违规 | 轴:{0}(#{1}) | {2}"), e.AxisName, e.AxisId, e.Reason));
        }

        private void RefreshStatus()
        {
            try
            {
                var status = _safetyZoneMonitor.GetSafetyStatus();
                if (status == null) return;

                foreach (var row in HeightAxisRows)
                {
                    status.CurrentPositions.TryGetValue(row.AxisName, out var pos);
                    row.CurrentPosition = pos;
                    row.IsBelowSafe = status.LowHeightAxisNames.Contains(row.AxisName);
                }

                foreach (var row in PlaneAxisRows)
                {
                    status.CurrentPositions.TryGetValue(row.AxisName, out var pos);
                    row.CurrentPosition = pos;
                    status.DangerZoneFlags.TryGetValue(row.AxisName, out var inDanger);
                    row.IsInDanger = inDanger;
                }

                IsPlaneMovementLocked = status.IsPlaneMovementLocked;
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("SZCfgVM_Log_RefreshStatusFailed", "[安全区域] 刷新状态失败: {0}"), ex.Message));
            }
        }

        private void ExecuteSave()
        {
            try
            {
                _config.CanvasRangeX = new AxisDangerZoneConfig { AxisName = "CanvasX", Min = XRangeMin, Max = XRangeMax };
                _config.CanvasRangeY = new AxisDangerZoneConfig { AxisName = "CanvasY", Min = YRangeMin, Max = YRangeMax };

                _configLoader.Save(_config.Clone());
                SyncConfigToMonitor();
                _logger.Info(_localization.GetResourceOrDefault("SZCfgVM_Log_ConfigSaved", "[安全区域] 配置已保存"));
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
                _logger.Error(ex, _localization.GetResourceOrDefault("SZCfgVM_Log_SaveConfigFailed", "[安全区域] 保存配置失败"));
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
            Enabled = cfg.Enabled;

            BuildHeightAxisRows(cfg);
            BuildPlaneAxisRows(cfg);

            var rangeX = cfg.CanvasRangeX;
            var rangeY = cfg.CanvasRangeY;
            if (rangeX != null)
            {
                XRangeMin = rangeX.Min;
                XRangeMax = rangeX.Max;
            }
            if (rangeY != null)
            {
                YRangeMin = rangeY.Min;
                YRangeMax = rangeY.Max;
            }
            if (rangeX == null || rangeY == null)
                AutoCalcCanvasRange();
        }

        private void BuildHeightAxisRows(SafetyZoneConfig cfg)
        {
            HeightAxisRows.Clear();
            var rule = cfg.GetOrCreateHeightLockPlaneRule();
            foreach (var ha in rule.HeightAxes)
                HeightAxisRows.Add(new HeightAxisRowViewModel(ha, SyncConfigToMonitor));
        }

        private void BuildPlaneAxisRows(SafetyZoneConfig cfg)
        {
            PlaneAxisRows.Clear();
            var rule = cfg.GetOrCreateHeightLockPlaneRule();
            foreach (var axisName in rule.LockedAxes)
            {
                var zone = cfg.DangerZones.FirstOrDefault(z => string.Equals(z.AxisName, axisName, StringComparison.Ordinal));
                if (zone == null)
                {
                    zone = new AxisDangerZoneConfig { AxisName = axisName, Min = 0, Max = 200 };
                    cfg.DangerZones.Add(zone);
                }
                PlaneAxisRows.Add(new PlaneAxisRowViewModel(zone, OnPlaneAxisRenamed, SyncConfigToMonitor));
            }
            RaisePlaneAxisRowsChanged();
        }

        private void OnPlaneAxisRenamed(string oldName, string newName)
        {
            _config.RenameLockedPlaneAxis(oldName, newName);
        }

        private void RaisePlaneAxisRowsChanged()
        {
            RaisePropertyChanged(nameof(PlaneAxis0));
            RaisePropertyChanged(nameof(PlaneAxis1));
        }

        private void ExecuteAddHeightAxis()
        {
            string axisName = PickUnusedAxisName(HeightAxisRows.Select(r => r.AxisName));
            var entry = _config.AddHeightAxis(axisName);
            HeightAxisRows.Add(new HeightAxisRowViewModel(entry, SyncConfigToMonitor));
            SyncConfigToMonitor();
        }

        private void ExecuteRemoveHeightAxis(HeightAxisRowViewModel row)
        {
            if (row == null) return;
            _config.RemoveHeightAxis(row.Model);
            HeightAxisRows.Remove(row);
            SyncConfigToMonitor();
        }

        private void ExecuteAddPlaneAxis()
        {
            string axisName = PickUnusedAxisName(PlaneAxisRows.Select(r => r.AxisName));
            var zone = _config.AddLockedPlaneAxis(axisName);
            PlaneAxisRows.Add(new PlaneAxisRowViewModel(zone, OnPlaneAxisRenamed, SyncConfigToMonitor));
            RaisePlaneAxisRowsChanged();
            AutoCalcCanvasRange();
            SyncConfigToMonitor();
        }

        private void ExecuteRemovePlaneAxis(PlaneAxisRowViewModel row)
        {
            if (row == null) return;
            _config.RemoveLockedPlaneAxis(row.AxisName);
            PlaneAxisRows.Remove(row);
            RaisePlaneAxisRowsChanged();
            SyncConfigToMonitor();
        }

        /// <summary>从硬件轴列表中挑选一个尚未被占用的轴名，减少新增行后仍需手动改名的步骤</summary>
        private string PickUnusedAxisName(IEnumerable<string> usedNames)
        {
            var used = new HashSet<string>(usedNames, StringComparer.Ordinal);
            return AvailableAxisNames.FirstOrDefault(n => !used.Contains(n))
                ?? AvailableAxisNames.FirstOrDefault()
                ?? string.Empty;
        }

        /// <summary>
        /// 根据前两个平面轴的危险区配置自动计算画布行程范围，扩展20%边距
        /// </summary>
        private void AutoCalcCanvasRange()
        {
            if (PlaneAxis0 != null)
            {
                double xSpan = PlaneAxis0.DangerMax - PlaneAxis0.DangerMin;
                double xMargin = Math.Max(xSpan * 0.2, 20);
                XRangeMin = PlaneAxis0.DangerMin - xMargin;
                XRangeMax = PlaneAxis0.DangerMax + xMargin;
            }

            if (PlaneAxis1 != null)
            {
                double ySpan = PlaneAxis1.DangerMax - PlaneAxis1.DangerMin;
                double yMargin = Math.Max(ySpan * 0.2, 20);
                YRangeMin = PlaneAxis1.DangerMin - yMargin;
                YRangeMax = PlaneAxis1.DangerMax + yMargin;
            }
        }

        /// <summary>深拷贝推送到单例监控器，Jog/运动立即使用最新 Enabled 与轴配置</summary>
        private void SyncConfigToMonitor()
        {
            try
            {
                _safetyZoneMonitor.UpdateConfig(_config.Clone());
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("SZCfgVM_Log_SyncConfigFailed", "[安全区域] 同步配置失败: {0}"), ex.Message));
            }
        }

        /// <summary>
        /// 离开页面时停止刷新轴位置，释放定时器和事件订阅
        /// </summary>
        public void Dispose()
        {
            _refreshTimer?.Dispose();
            _refreshTimer = null;

            _eventAggregator.GetEvent<SafetyViolationEvent>().Unsubscribe(OnSafetyViolation);
        }
    }
}

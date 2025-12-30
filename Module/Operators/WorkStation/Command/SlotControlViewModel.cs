using Core.Abstraction;
using Core.Events;
using Core.Utilities;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Stations;
using Stations.Event;
using Stations.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Module.ViewModels
{
    public class SlotControlViewModel : BindableBase
    {
        private AssemblyStation _assemblyStation;
        private bool _isInitializing = false;
        private SubscriptionToken _h2HeightSubscription;
        private Dictionary<int, double> _h2Heights = new Dictionary<int, double>();

        private readonly ILoggerService _logger;
        private readonly IParameterStorage _parameterStorage;
        private readonly IEventAggregator _eventAggregator;
        private readonly ICompensationService _compensationService;    // 补偿服务
        private readonly IH2HeightDataService _h2HeightDataService;

        #region 新增属性 - 动态拨片启用控制
        private bool _enableDynamicStripping = true;
        public bool EnableDynamicStripping
        {
            get => _enableDynamicStripping;
            set => SetProperty(ref _enableDynamicStripping, value);
        }

        private string _dynamicDistanceAlarmMessage = "";
        public string DynamicDistanceAlarmMessage
        {
            get => _dynamicDistanceAlarmMessage;
            set => SetProperty(ref _dynamicDistanceAlarmMessage, value);
        }

        private bool _hasDynamicDistanceAlarm = false;
        public bool HasDynamicDistanceAlarm
        {
            get => _hasDynamicDistanceAlarm;
            set => SetProperty(ref _hasDynamicDistanceAlarm, value);
        }
        #endregion

        #region 属性
        private int _selectedTabIndex = 1;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }
        private ObservableCollection<TabStrippingDistance> _tabStrippingDistances = new ObservableCollection<TabStrippingDistance>();
        public ObservableCollection<TabStrippingDistance> TabStrippingDistances
        {
            get => _tabStrippingDistances;
            set => SetProperty(ref _tabStrippingDistances, value);
        }
        private string _calculateStatus = "";
        public string CalculateStatus
        {
            get => _calculateStatus;
            set => SetProperty(ref _calculateStatus, value);
        }

        private Dictionary<int, double> _h2HeightsCache = new Dictionary<int, double>(); // 缓存每个Tab的H2Height

        private string _slotOperationStatus = "就绪";
        public string SlotOperationStatus
        {
            get => _slotOperationStatus;
            set => SetProperty(ref _slotOperationStatus, value);
        }

        private double _strippingDistance = 0.6;
        public double StrippingDistance
        {
            get => _strippingDistance;
            set => SetProperty(ref _strippingDistance, value);
        }

        private double _distanceLowerLimit = 0.5;
        public double DistanceLowerLimit
        {
            get => _distanceLowerLimit;
            set => SetProperty(ref _distanceLowerLimit, value);
        }

        private int _autoStrippingCount = 1;
        public int AutoStrippingCount
        {
            get => _autoStrippingCount;
            set => SetProperty(ref _autoStrippingCount, value);
        }

        private int _alarmThreshold = 3;
        public int AlarmThreshold
        {
            get => _alarmThreshold;
            set => SetProperty(ref _alarmThreshold, value);
        }

        private int _currentStrippingCount = 0;
        public int CurrentStrippingCount
        {
            get => _currentStrippingCount;
            set => SetProperty(ref _currentStrippingCount, value);
        }

        private int _continuousFailureCount = 0;
        public int ContinuousFailureCount
        {
            get => _continuousFailureCount;
            set => SetProperty(ref _continuousFailureCount, value);
        }

        private bool _hasAlarm = false;
        public bool HasAlarm
        {
            get => _hasAlarm;
            set => SetProperty(ref _hasAlarm, value);
        }

        private string _alarmMessage = "";
        public string AlarmMessage
        {
            get => _alarmMessage;
            set => SetProperty(ref _alarmMessage, value);
        }

        private ObservableCollection<SlotData> _slotDataCollection = new ObservableCollection<SlotData>();
        public ObservableCollection<SlotData> SlotDataCollection
        {
            get => _slotDataCollection;
            set => SetProperty(ref _slotDataCollection, value);
        }

        private ObservableCollection<SlotDataDisplay> _slotDataRows = new ObservableCollection<SlotDataDisplay>();
        public ObservableCollection<SlotDataDisplay> SlotDataRows
        {
            get => _slotDataRows;
            set => SetProperty(ref _slotDataRows, value);
        }
        // 过压量属性
        private double _globalOverPressure = 0.15;
        public double GlobalOverPressure
        {
            get => _globalOverPressure;
            set
            {
                if (SetProperty(ref _globalOverPressure, value))
                {
                    // 更新所有Tab的过压量
                    UpdateAllTabsOverPressure(value);
                }
            }
        }
        // 更新所有Tab的过压量
        private void UpdateAllTabsOverPressure(double overPressure)
        {
            foreach (var tab in TabStrippingDistances)
            {
                tab.OverPressure = overPressure;
            }
        }
        #endregion

        #region 命令

        public DelegateCommand GoToAdjustSlotPositionCommand { get; private set; }
        public DelegateCommand GoToPickSlotPositionCommand { get; private set; }
        public DelegateCommand AutoPickSlotCommand { get; private set; }
        public DelegateCommand AutoInspectionSlotCommand { get; private set; }
        public DelegateCommand SaveParametersCommand { get; private set; }
        public DelegateCommand LoadParametersCommand { get; private set; }
        public DelegateCommand ResetParametersCommand { get; private set; }
        public DelegateCommand ClearDataCommand { get; private set; }
        public DelegateCommand ApplyCompensationCommand { get; private set; }
        public DelegateCommand CalculateDynamicDistanceCommand { get; private set; }
        public DelegateCommand CalculateAllTabsDistanceCommand { get; private set; }

        #endregion

        public SlotControlViewModel(
            TaskInstanceManager taskManager,
            ILoggerService logger,
            IParameterStorage parameterStorage = null,
            IEventAggregator eventAggregator = null,
            ICompensationService compensationService = null,
            IH2HeightDataService h2HeightDataService = null)
        {
            _assemblyStation = taskManager.GetTask<AssemblyStation>();
            _logger = logger;
            _parameterStorage = parameterStorage;
            _eventAggregator = eventAggregator;
            _compensationService = compensationService;
            _h2HeightDataService = h2HeightDataService;
            InitializeCommands();
            LoadStoredParameters();

            if (_assemblyStation != null)
            {
                _assemblyStation.OnPhotoCompleted += OnOffsetUpdated;
            }
            LoadExistingH2Heights();
            // 订阅H2Height更新事件
            if (_eventAggregator != null)
            {
                _h2HeightSubscription = _eventAggregator.GetEvent<H2HeightUpdatedEvent>()
                    .Subscribe(OnH2HeightUpdated, ThreadOption.UIThread);
            }
            // 订阅数据服务事件
            if (_h2HeightDataService != null)
            {
                _h2HeightDataService.H2HeightUpdated += OnH2HeightDataServiceUpdated;
            }
            // 初始化Tab索引下拉框数据
            InitializeTabStrippingDistances();
        }

        private void InitializeDefaultData()
        {
            _isInitializing = true;

            if (SlotDataCollection.Count == 0)
            {
                var defaultData = new SlotData
                {
                    BaseX = 0.0,
                    BaseY = 0.0,
                    BaseU = 0.0,
                    BaseDistance = 0.0,
                    CurrentX = 0.0,
                    CurrentY = 0.0,
                    CurrentU = 0.0,
                    CurrentDistance = 0.0,
                    DeviationX = 0.0,
                    DeviationY = 0.0,
                    DeviationU = 0.0,
                    DeviationDistance = 0.0,
                    CompensationX = 0.0,
                    CompensationY = 0.0,
                    CompensationU = 0.0,
                    CompensationDistance = 0.0,
                    DeviationXColor = Colors.Green,
                    DeviationYColor = Colors.Green,
                    DeviationUColor = Colors.Green,
                    DeviationDistanceColor = Colors.Green,
                    IsDistanceBelowLimit = false,

                    // 第二组默认值
                    BaseX2 = 0.0,
                    BaseY2 = 0.0,
                    BaseU2 = 0.0,
                    BaseDistance2 = 0.0,
                    CurrentX2 = 0.0,
                    CurrentY2 = 0.0,
                    CurrentU2 = 0.0,
                    CurrentDistance2 = 0.0,
                    DeviationX2 = 0.0,
                    DeviationY2 = 0.0,
                    DeviationU2 = 0.0,
                    DeviationDistance2 = 0.0,
                    CompensationX2 = 0.0,
                    CompensationY2 = 0.0,
                    CompensationU2 = 0.0,
                    CompensationDistance2 = 0.0,
                    DeviationX2Color = Colors.Green,
                    DeviationY2Color = Colors.Green,
                    DeviationU2Color = Colors.Green,
                    DeviationDistance2Color = Colors.Green,
                };

                SlotDataCollection.Add(defaultData);
            }

            UpdateSlotDataRows();
            _isInitializing = false;
        }

        private void InitializeCommands()
        {
            GoToAdjustSlotPositionCommand = new DelegateCommand(async () => await ExecuteGoToAdjustSlotPosition());
            GoToPickSlotPositionCommand = new DelegateCommand(async () => await ExecuteGoToPickSlotPosition());
            AutoPickSlotCommand = new DelegateCommand(async () => await ExecuteAutoPickSlot());
            AutoInspectionSlotCommand = new DelegateCommand(async () => await ExecuteAutoInspectionSlot());
            SaveParametersCommand = new DelegateCommand(SaveParameters);
            LoadParametersCommand = new DelegateCommand(LoadStoredParameters);
            ResetParametersCommand = new DelegateCommand(ResetParameters);
            ClearDataCommand = new DelegateCommand(ClearData);
            ApplyCompensationCommand = new DelegateCommand(OnApplyCompensation);
            CalculateDynamicDistanceCommand = new DelegateCommand(() =>
                Task.Run(() => CalculateDynamicStrippingDistanceForSelectedTab()));

            CalculateAllTabsDistanceCommand = new DelegateCommand(() =>
                Task.Run(() => CalculateDynamicStrippingDistanceForAllTabs()));
        }
        private void LoadExistingH2Heights()
        {
            if (_h2HeightDataService == null) return;

            var allHeights = _h2HeightDataService.GetAllH2Heights();
            foreach (var kvp in allHeights)
            {
                _h2HeightsCache[kvp.Key] = kvp.Value;
                _logger?.Info($"从数据服务加载Tab{kvp.Key}的H2Height: {kvp.Value:F3}mm");
            }
        }

        private void OnH2HeightUpdated(H2HeightData h2HeightData)
        {
            try
            {
                _logger.Info($"收到Tab{h2HeightData.TabIndex}的H2Height更新: {h2HeightData.H2Height:F3}mm");

                // 存储H2Height
                _h2HeightsCache[h2HeightData.TabIndex] = h2HeightData.H2Height;

                // 更新SlotDataCollection中的BaseDistance2
                var lastData = SlotDataCollection.LastOrDefault();
                if (lastData != null)
                {
                    lastData.BaseDistance2 = h2HeightData.H2Height;

                    // 重新计算显示行
                    UpdateSlotDataRows();

                    _logger.Info($"已更新BaseDistance2为H2Height: {h2HeightData.H2Height:F3}mm");
                    SlotOperationStatus = $"收到Tab{h2HeightData.TabIndex}的H2Height: {h2HeightData.H2Height:F3}mm";
                }
                else
                {
                    _logger.Warn("没有可用的SlotData，无法更新BaseDistance2");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"处理H2Height更新失败: {ex.Message}");
            }
        }
        private void OnH2HeightDataServiceUpdated(int tabIndex, double h2Height)
        {
            // 在UI线程执行
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                try
                {
                    _logger?.Info($"从数据服务收到Tab{tabIndex}的H2Height更新: {h2Height:F3}mm");
                    _h2HeightsCache[tabIndex] = h2Height;

                    // 更新SlotDataCollection中的BaseDistance2
                    var lastData = SlotDataCollection.LastOrDefault();
                    if (lastData != null)
                    {
                        lastData.BaseDistance2 = h2Height;
                        UpdateSlotDataRows();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Error($"处理数据服务H2Height更新失败: {ex.Message}");
                }
            });
        }

        // 拨动距离计算方法 - 已废弃，使用新的CalculateDynamicDistanceForSelectedTab
        private double CalculateDynamicStrippingDistance()
        {
            try
            {
                var lastData = SlotDataCollection.LastOrDefault();
                if (lastData == null)
                {
                    _logger.Warn("没有可用的数据来计算拨动距离");
                    return double.NaN;
                }

                // 获取H2Height（优先从事件缓存获取，其次从BaseDistance2获取）
                double h2Height = double.NaN;

                if (_h2Heights.TryGetValue(_selectedTabIndex, out double cachedH2Height)) // 假设使用Tab1
                {
                    h2Height = cachedH2Height;
                }
                else if (!double.IsNaN(lastData.BaseDistance2))
                {
                    h2Height = lastData.BaseDistance2;
                }

                if (double.IsNaN(h2Height))
                {
                    _logger.Warn("无法获取H2Height值");
                    return double.NaN;
                }

                // 获取CurrentDistance2
                double currentDistance2 = lastData.CurrentDistance2;
                if (double.IsNaN(currentDistance2))
                {
                    _logger.Warn("CurrentDistance2值为NaN");
                    return double.NaN;
                }

                // 计算拨动距离
                double strippingDistance;
                string directionInfo;

                if (h2Height > currentDistance2)
                {
                    // H2Height > CurrentDistance2，轴向下运动，slot往上拨
                    strippingDistance = h2Height - currentDistance2;
                    directionInfo = $"轴向下运动({strippingDistance:F3}mm)，slot往上拨";
                }
                else
                {
                    // H2Height <= CurrentDistance2，轴向上运动，slot往下拨
                    strippingDistance = currentDistance2 - h2Height;
                    // 负值表示向上运动
                    strippingDistance = -strippingDistance;
                    directionInfo = $"轴向上运动({-strippingDistance:F3}mm)，slot往下拨";
                }

                _logger.Info($"计算拨动距离: H2Height={h2Height:F3}mm, CurrentDistance2={currentDistance2:F3}mm, {directionInfo}");

                // 检查是否超出合理范围
                if (Math.Abs(strippingDistance) > 5.0)
                {
                    _logger.Warn($"计算出的拨动距离{strippingDistance:F3}mm可能超出合理范围，请检查数据");
                }

                return Math.Round(strippingDistance, 3);
            }
            catch (Exception ex)
            {
                _logger.Error($"计算动态拨动距离失败: {ex.Message}");
                return double.NaN;
            }
        }

        private void InitializeTabStrippingDistances()
        {
            TabStrippingDistances.Clear();
            for (int i = 1; i <= 6; i++)
            {
                TabStrippingDistances.Add(new TabStrippingDistance
                {
                    TabIndex = i,
                    H2Height = double.NaN,
                    CurrentDistance2 = double.NaN,
                    StrippingDistance = 0.0,
                    DirectionDescription = "未计算",
                    CalculationTime = DateTime.MinValue
                });
            }
        }

        // 为选中的Tab计算拨动距离
        private void CalculateDynamicStrippingDistanceForSelectedTab()
        {
            try
            {
                if (SelectedTabIndex < 1 || SelectedTabIndex > 6)
                {
                    CalculateStatus = $"Tab索引必须在1-6之间，当前值: {SelectedTabIndex}";
                    return;
                }

                CalculateStatus = $"正在计算Tab{SelectedTabIndex}的拨动距离和下压高度...";

                // 获取H2Height
                double h2Height = GetH2HeightForTab(SelectedTabIndex);

                // 获取CurrentDistance2（从当前数据）
                double currentDistance2 = GetCurrentDistance2ForTab(SelectedTabIndex);

                if (double.IsNaN(h2Height))
                {
                    CalculateStatus = $"无法获取Tab{SelectedTabIndex}的H2Height值";
                    _logger.Warn(CalculateStatus);
                    return;
                }

                if (double.IsNaN(currentDistance2))
                {
                    CalculateStatus = $"无法获取Tab{SelectedTabIndex}的CurrentDistance2值";
                    _logger.Warn(CalculateStatus);
                    return;
                }

                // 计算拨动距离和下压高度
                var result = CalculateStrippingDistance(h2Height, currentDistance2);

                // 检查H4拨动距离是否小于0
                if (result.Distance < 0)
                {
                    HasDynamicDistanceAlarm = true;
                    DynamicDistanceAlarmMessage = $"警告：Tab{SelectedTabIndex}的H4拨动距离为负值({result.Distance:F3}mm)，不满足装配条件！";
                    _logger.Warn(DynamicDistanceAlarmMessage);
                }
                else
                {
                    HasDynamicDistanceAlarm = false;
                    DynamicDistanceAlarmMessage = "";
                }

                // 更新显示
                var tabDistance = TabStrippingDistances.FirstOrDefault(t => t.TabIndex == SelectedTabIndex);
                if (tabDistance != null)
                {
                    tabDistance.H2Height = Math.Round(h2Height, 3);
                    tabDistance.CurrentDistance2 = Math.Round(currentDistance2, 3);
                    tabDistance.StrippingDistance = Math.Round(result.Distance, 3);
                    tabDistance.PressHeight = Math.Round(result.PressHeight, 3);
                    tabDistance.DirectionDescription = result.Direction;
                    tabDistance.CalculationTime = DateTime.Now;
                    tabDistance.BaseDistance2 = Math.Round(h2Height, 3);
                    // 设置过压量
                    tabDistance.OverPressure = GlobalOverPressure;

                    // 触发UI更新
                    RaisePropertyChanged(nameof(TabStrippingDistances));

                    UpdateSlotDataRowsBaseDistance2(h2Height);
                }

                CalculateStatus = $"Tab{SelectedTabIndex}计算完成: 拨动距离={result.Distance:F3}mm, 下压高度={result.PressHeight:F3}mm ({result.Direction})";
                _logger.Info(CalculateStatus);
            }
            catch (Exception ex)
            {
                CalculateStatus = $"计算Tab{SelectedTabIndex}失败: {ex.Message}";
                _logger.Error($"计算失败: {ex.Message}");
            }
        }

        // 为所有Tab计算拨动距离
        private void CalculateDynamicStrippingDistanceForAllTabs()
        {
            try
            {
                CalculateStatus = "正在计算所有Tab的拨动距离和下压高度...";

                bool hasError = false;
                bool hasNegativeDistance = false;
                List<string> negativeTabs = new List<string>();

                for (int i = 1; i <= 6; i++)
                {
                    try
                    {
                        // 获取H2Height
                        double h2Height = GetH2HeightForTab(i);

                        // 获取CurrentDistance2
                        double currentDistance2 = GetCurrentDistance2ForTab(i);

                        if (double.IsNaN(h2Height) || double.IsNaN(currentDistance2))
                        {
                            _logger.Warn($"Tab{i}: 无法获取H2Height或CurrentDistance2值");
                            hasError = true;
                            continue;
                        }

                        // 计算拨动距离和下压高度
                        var result = CalculateStrippingDistance(h2Height, currentDistance2);

                        // 检查负值
                        if (result.Distance < 0)
                        {
                            hasNegativeDistance = true;
                            negativeTabs.Add($"Tab{i}({result.Distance:F3}mm)");
                        }

                        // 更新显示
                        var tabDistance = TabStrippingDistances.FirstOrDefault(t => t.TabIndex == i);
                        if (tabDistance != null)
                        {
                            tabDistance.H2Height = Math.Round(h2Height, 3);
                            tabDistance.CurrentDistance2 = Math.Round(currentDistance2, 3);
                            tabDistance.StrippingDistance = Math.Round(result.Distance, 3);
                            tabDistance.PressHeight = Math.Round(result.PressHeight, 3);
                            tabDistance.DirectionDescription = result.Direction;
                            tabDistance.CalculationTime = DateTime.Now;
                            tabDistance.OverPressure = GlobalOverPressure;
                        }

                        _logger.Info($"Tab{i}: H2Height={h2Height:F3}, CurrentDistance2={currentDistance2:F3}, " +
                                    $"拨动距离={result.Distance:F3}mm, 下压高度={result.PressHeight:F3}mm, {result.Direction}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"计算Tab{i}失败: {ex.Message}");
                        hasError = true;
                    }
                }

                // 触发UI更新
                RaisePropertyChanged(nameof(TabStrippingDistances));

                // 检查报警
                if (hasNegativeDistance)
                {
                    HasDynamicDistanceAlarm = true;
                    DynamicDistanceAlarmMessage = $"警告：以下Tab的H4拨动距离为负值，不满足装配条件：{string.Join(", ", negativeTabs)}";
                    _logger.Warn(DynamicDistanceAlarmMessage);
                }
                else
                {
                    HasDynamicDistanceAlarm = false;
                    DynamicDistanceAlarmMessage = "";
                }

                if (hasError)
                {
                    CalculateStatus = "所有Tab计算完成，部分Tab计算有错误";
                }
                else
                {
                    CalculateStatus = "所有Tab计算完成";
                }
            }
            catch (Exception ex)
            {
                CalculateStatus = $"计算所有Tab失败: {ex.Message}";
                _logger.Error($"计算所有Tab失败: {ex.Message}");
            }
        }

        // 获取指定Tab的H2Height
        private double GetH2HeightForTab(int tabIndex)
        {
            // 从缓存获取
            if (_h2HeightsCache.ContainsKey(tabIndex))
            {
                return _h2HeightsCache[tabIndex];
            }

            return double.NaN;
        }

        // 获取指定Tab的CurrentDistance2
        private double GetCurrentDistance2ForTab(int tabIndex)
        {
            try
            {
                // 从SlotDataCollection获取最新的CurrentDistance2
                var lastData = SlotDataCollection.LastOrDefault();
                if (lastData != null)
                {
                    return lastData.CurrentDistance2;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"获取Tab{tabIndex}的CurrentDistance2失败: {ex.Message}");
            }

            return double.NaN;
        }

        // 核心计算方法
        private (double Distance, string Direction, double PressHeight) CalculateStrippingDistance(double h2Height, double currentDistance2)
        {
            double strippingDistance;
            string direction;

            if (h2Height > currentDistance2)
            {
                strippingDistance = h2Height - currentDistance2;
                direction = $"正值({strippingDistance:F3}mm)，轴向下运动，slot往上拨";
            }
            else
            {
                strippingDistance = currentDistance2 - h2Height;
                strippingDistance = -strippingDistance;
                direction = $"负值({-strippingDistance:F3}mm)，轴向上运动，slot往下拨";
            }

            // 计算下压高度：H4 + 0.27 + 过压量
            double pressHeight = strippingDistance + 0.27 + GlobalOverPressure;

            return (strippingDistance, direction, pressHeight);
        }

        // 更新 SlotDataRows 中的 BaseDistance2，同时更新 SlotDataCollection
        private void UpdateSlotDataRowsBaseDistance2(double h2Height)
        {
            try
            {
                if (SlotDataRows == null || !SlotDataRows.Any() || SlotDataCollection == null || !SlotDataCollection.Any())
                {
                    _logger.Warn("SlotDataRows或SlotDataCollection为空，无法更新BaseDistance2");
                    return;
                }

                // 更新SlotDataCollection中的BaseDistance2
                var lastData = SlotDataCollection.LastOrDefault();
                if (lastData != null)
                {
                    lastData.BaseDistance2 = Math.Round(h2Height, 3);
                    _logger.Info($"已更新SlotDataCollection最新数据的BaseDistance2为: {h2Height:F3}mm");
                }

                // 更新SlotDataRows中的BaseDistance2
                var lastRow = SlotDataRows.LastOrDefault();
                if (lastRow != null)
                {
                    lastRow.BaseDistance2 = Math.Round(h2Height, 3);
                    _logger.Info($"已更新SlotDataRows最新行的BaseDistance2为: {h2Height:F3}mm");
                }
                else
                {
                    _logger.Warn("SlotDataRows中没有数据行");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"更新BaseDistance2失败: {ex.Message}");
            }
        }

        // 更新H2Height缓存的方法（可以由ExtensionParametersViewModel调用）
        public void UpdateH2Height(int tabIndex, double h2Height)
        {
            try
            {
                _h2HeightsCache[tabIndex] = h2Height;
                _logger.Info($"更新Tab{tabIndex}的H2Height缓存: {h2Height:F3}mm");
            }
            catch (Exception ex)
            {
                _logger.Error($"更新H2Height缓存失败: {ex.Message}");
            }
        }

        #region 命令实现

        private async Task ExecuteGoToAdjustSlotPosition()
        {
            try
            {
                SlotOperationStatus = "Slot角度纠正中...";
                bool success = await _assemblyStation.AlignSlotAngleAsync();

                if (success)
                {
                    SlotOperationStatus = "Slot角度纠正完成";
                    _logger.Info("Slot角度纠正完成");
                }
                else
                {
                    SlotOperationStatus = "Slot角度纠正失败";
                    _logger.Error("Slot角度纠正失败");
                }
            }
            catch (Exception ex)
            {
                SlotOperationStatus = $"Slot角度纠正异常: {ex.Message}";
                _logger.Error($"Slot角度纠正异常: {ex.Message}");
            }
        }

        private async Task ExecuteGoToPickSlotPosition()
        {
            try
            {
                SlotOperationStatus = "移动到拨片位置...";
                SyncDisplayRowsToCollection();

                var lastData = SlotDataCollection.LastOrDefault();
                if (lastData != null)
                {
                    bool success = await _assemblyStation.MoveAxesToSlotPosition(
                        lastData.CurrentX + lastData.CompensationX,
                        lastData.CurrentY + lastData.CompensationY);

                    if (success)
                    {
                        SlotOperationStatus = "已到达拨片位置";
                        _logger.Info($"已到达拨片位置，补偿值X={lastData.CompensationX:F3}, Y={lastData.CompensationY:F3}");
                    }
                    else
                    {
                        SlotOperationStatus = "移动到拨片位置失败";
                        _logger.Error("移动到拨片位置失败");
                    }
                }
                else
                {
                    SlotOperationStatus = "无补偿数据，请先拍照获取Offset";
                    _logger.Warn("无补偿数据，无法移动到拨片位置");
                }
            }
            catch (Exception ex)
            {
                SlotOperationStatus = $"移动异常: {ex.Message}";
                _logger.Error($"移动到拨片位置异常: {ex.Message}");
            }
        }

        private async Task ExecuteAutoPickSlot()
        {
            try
            {
                SlotOperationStatus = "开始自动拨片...";
                CurrentStrippingCount = 0;
                ContinuousFailureCount = 0;
                HasAlarm = false;
                HasDynamicDistanceAlarm = false;
                DynamicDistanceAlarmMessage = "";

                bool overallSuccess = true;

                for (int i = 0; i < AutoStrippingCount; i++)
                {
                    SlotOperationStatus = $"第{i + 1}次拨片...";
                    CurrentStrippingCount = i + 1;

                    double strippingDistance;

                    // 判断是否启用动态拨片
                    if (EnableDynamicStripping)
                    {
                        // 计算动态拨动距离
                        strippingDistance = CalculateDynamicDistanceForSelectedTab();

                        if (double.IsNaN(strippingDistance))
                        {
                            // 如果计算失败，使用固定距离
                            strippingDistance = StrippingDistance;
                            _logger.Warn($"动态拨动距离计算失败，使用固定距离: {strippingDistance:F3}mm");
                        }
                        else
                        {
                            _logger.Info($"使用动态拨动距离: {strippingDistance:F3}mm");

                            // 检查H4拨动距离是否小于0
                            if (strippingDistance < 0)
                            {
                                HasDynamicDistanceAlarm = true;
                                DynamicDistanceAlarmMessage = $"警告：第{i + 1}次拨片，H4拨动距离为负值({strippingDistance:F3}mm)，不满足装配条件！";
                                _logger.Warn(DynamicDistanceAlarmMessage);

                                // 继续使用负值拨片，但记录报警
                            }
                        }
                    }
                    else
                    {
                        // 使用固定距离
                        strippingDistance = StrippingDistance;
                        _logger.Info($"使用固定拨动距离: {strippingDistance:F3}mm");
                    }

                    // 执行拨片动作
                    bool success = await _assemblyStation.ExecuteStripperSlotAction(strippingDistance);

                    if (!success)
                    {
                        overallSuccess = false;
                        ContinuousFailureCount++;

                        if (ContinuousFailureCount >= AlarmThreshold)
                        {
                            HasAlarm = true;
                            AlarmMessage = $"连续{ContinuousFailureCount}次拨片不合格，请检查设备！";
                            SlotOperationStatus = "拨片异常，已触发报警";
                            _logger.Error(AlarmMessage);
                            break;
                        }
                    }
                    else
                    {
                        ContinuousFailureCount = 0;
                    }

                    await Task.Delay(500);
                }

                if (overallSuccess && !HasAlarm)
                {
                    SlotOperationStatus = $"自动拨片完成，共{AutoStrippingCount}次";
                    _logger.Info($"自动拨片完成，共{AutoStrippingCount}次");
                }
                else if (!HasAlarm)
                {
                    SlotOperationStatus = "拨片完成，部分失败";
                    _logger.Warn($"拨片完成，部分失败。连续失败次数：{ContinuousFailureCount}");
                }
            }
            catch (Exception ex)
            {
                SlotOperationStatus = $"自动拨片异常: {ex.Message}";
                _logger.Error($"自动拨片异常: {ex.Message}");
            }
        }

        // 新增方法：计算当前选中Tab的动态距离
        private double CalculateDynamicDistanceForSelectedTab()
        {
            try
            {
                if (SelectedTabIndex < 1 || SelectedTabIndex > 6)
                {
                    _logger.Warn($"Tab索引必须在1-6之间，当前值: {SelectedTabIndex}");
                    return double.NaN;
                }

                // 获取H2Height
                double h2Height = GetH2HeightForTab(SelectedTabIndex);

                // 获取CurrentDistance2（从当前数据）
                double currentDistance2 = GetCurrentDistance2ForTab(SelectedTabIndex);

                if (double.IsNaN(h2Height))
                {
                    _logger.Warn($"无法获取Tab{SelectedTabIndex}的H2Height值");
                    return double.NaN;
                }

                if (double.IsNaN(currentDistance2))
                {
                    _logger.Warn($"无法获取Tab{SelectedTabIndex}的CurrentDistance2值");
                    return double.NaN;
                }

                // 计算拨动距离
                var result = CalculateStrippingDistance(h2Height, currentDistance2);
                return result.Distance;
            }
            catch (Exception ex)
            {
                _logger.Error($"计算Tab{SelectedTabIndex}的动态距离失败: {ex.Message}");
                return double.NaN;
            }
        }

        private async Task ExecuteAutoInspectionSlot()
        {
            try
            {
                SlotOperationStatus = "开始拨片复检...";

                var result = await _assemblyStation.PerformSideCameraRecheckAsync();

                if (result.success)
                {
                    SlotOperationStatus = $"拨片复检完成" + $"偏移量: X={result.offsetX2:F3}, Y={result.offsetY2:F3}";

                    
                    var newData = new SlotData
                    {
                        // 第一组数据（复检结果）
                        CurrentX = result.offsetX,
                        CurrentY = result.offsetY,
                        CurrentU = result.offsetU,
                        CurrentDistance = result.offsetH,

                        // 第二组数据（复检结果）
                        CurrentX2 = result.offsetX2,
                        CurrentY2 = result.offsetY2,
                        CurrentU2 = result.offsetU2,
                        CurrentDistance2 = result.offsetH2,
                    };

                    CalculateDeviationsAndCompensations(newData);

                    if (newData.CurrentDistance <= DistanceLowerLimit)
                    {
                        newData.IsDistanceBelowLimit = true;
                        ContinuousFailureCount++;

                        if (ContinuousFailureCount >= AlarmThreshold)
                        {
                            HasAlarm = true;
                            AlarmMessage = $"连续{ContinuousFailureCount}次距离低于下限({DistanceLowerLimit}mm)！";
                            _logger.Error(AlarmMessage);
                        }
                    }
                    else
                    {
                        ContinuousFailureCount = 0;
                        HasAlarm = false;
                    }

                    SlotDataCollection.Add(newData);

                    // 限制只保留最新的3行数据
                    while (SlotDataCollection.Count > 1)
                    {
                        SlotDataCollection.RemoveAt(0);
                    }

                    UpdateSlotDataRows();

                    _logger.Info($"拨片复检完成，偏移量: X={result.offsetX2:F3}, Y={result.offsetY2:F3}");
                }
                else
                {
                    SlotOperationStatus = "拨片复检失败";
                    _logger.Error("拨片复检失败");
                }
            }
            catch (Exception ex)
            {
                SlotOperationStatus = $"复检异常: {ex.Message}";
                _logger.Error($"拨片复检异常: {ex.Message}");
            }
        }
        //"Camera=SideCamera;VISION_RESULT:SUCCESS:offsetX=0.346999999999946,offsetY=0.121000000000001,offsetU=0.622,offsetH=2.1132,offsetX2=0.053,offsetY2=-0.021,offsetU2=0,offsetH2=2.1132"
        private void OnOffsetUpdated(object sender, PhotoCompletedEventArgs e)
        {
            if (e.Success && !string.IsNullOrEmpty(e.Data))
            {
                //ParseOffsetAndUpdateData(e.Data);
            }
        }

        private void ParseOffsetAndUpdateData(string data)
        {
            try
            {
                // 第一组数据
                double offsetX = 0.0, offsetY = 0.0, offsetU = 0.0, offsetH = 0.0;
                // 第二组数据
                double offsetX2 = 0.0, offsetY2 = 0.0, offsetU2 = 0.0, offsetH2 = 0.0;

                // 检查是否包含第二组数据
                bool hasSecondGroup = data.Contains("offsetX2");

                // 直接解析整个字符串
                var parts = data.Split(';');

                foreach (var part in parts)
                {
                    // 查找视觉结果部分
                    if (part.Contains("VISION_RESULT:SUCCESS:"))
                    {
                        // 提取偏移数据部分
                        var offsetData = part.Replace("VISION_RESULT:SUCCESS:", "");

                        // 分割成键值对
                        var keyValuePairs = offsetData.Split(',');

                        foreach (var pair in keyValuePairs)
                        {
                            var keyValue = pair.Split('=');
                            if (keyValue.Length == 2)
                            {
                                var key = keyValue[0].Trim();
                                var value = keyValue[1].Trim();

                                // 处理空值情况（如offsetH2可能为空）
                                if (string.IsNullOrEmpty(value))
                                {
                                    continue;
                                }

                                if (double.TryParse(value, out double doubleValue))
                                {
                                    // 解析所有可能的键
                                    switch (key)
                                    {
                                        case "offsetX":
                                            offsetX = doubleValue;
                                            break;
                                        case "offsetY":
                                            offsetY = doubleValue;
                                            break;
                                        case "offsetU":
                                            offsetU = doubleValue;
                                            break;
                                        case "offsetH":
                                            offsetH = doubleValue;
                                            break;
                                        case "offsetX2":
                                            offsetX2 = doubleValue;
                                            break;
                                        case "offsetY2":
                                            offsetY2 = doubleValue;
                                            break;
                                        case "offsetU2":
                                            offsetU2 = doubleValue;
                                            break;
                                        case "offsetH2":
                                            offsetH2 = doubleValue;
                                            break;
                                    }
                                }
                                else
                                {
                                    _logger.Warn($"无法解析数值: {key}={value}");
                                }
                            }
                        }
                        break;
                    }
                }

                // 创建新的数据记录
                var newData = new SlotData
                {
                    CurrentX = offsetX,
                    CurrentY = offsetY,
                    CurrentU = offsetU,
                    CurrentDistance = offsetH,
                    CurrentX2 = offsetX2,
                    CurrentY2 = offsetY2,
                    CurrentU2 = offsetU2,
                    CurrentDistance2 = offsetH2
                };

                // 如果是第一条数据，设置基准值
                if (SlotDataCollection.Count == 0)
                {
                    // 第一组基准值
                    newData.BaseX = newData.CurrentX;
                    newData.BaseY = newData.CurrentY;
                    newData.BaseU = newData.CurrentU;
                    newData.BaseDistance = newData.CurrentDistance;

                    // 第二组基准值
                    newData.BaseX2 = newData.CurrentX2;
                    newData.BaseY2 = newData.CurrentY2;
                    newData.BaseU2 = newData.CurrentU2;
                    newData.BaseDistance2 = newData.CurrentDistance2;

                    // 第一条数据的补偿值初始化为0
                    newData.CompensationX = 0;
                    newData.CompensationY = 0;
                    newData.CompensationU = 0;
                    newData.CompensationDistance = 0;
                    newData.CompensationX2 = 0;
                    newData.CompensationY2 = 0;
                    newData.CompensationU2 = 0;
                    newData.CompensationDistance2 = 0;

                    // 添加新数据
                    SlotDataCollection.Add(newData);
                }
                else
                {
                    // 使用第一条数据的基准值
                    var firstData = SlotDataCollection.First();

                    if (Math.Abs(firstData.BaseDistance2) < 0.001 && Math.Abs(offsetH2) > 0.001)
                    {
                        _logger.Warn($"检测到BaseDistance2为0，将更新为当前拍照值: {offsetH2:F3}");
                        firstData.BaseDistance2 = offsetH2;
                        CalculateDeviationsAndCompensations(firstData);
                    }

                    newData.BaseX = firstData.BaseX;
                    newData.BaseY = firstData.BaseY;
                    newData.BaseU = firstData.BaseU;
                    newData.BaseDistance = firstData.BaseDistance;

                    newData.BaseX2 = firstData.BaseX2;
                    newData.BaseY2 = firstData.BaseY2;
                    newData.BaseU2 = firstData.BaseU2;
                    newData.BaseDistance2 = firstData.BaseDistance2;

                    // 获取最新的补偿值（如果有的话），否则使用0
                    var lastData = SlotDataCollection.Last();
                    // 保持原有的补偿值（手动输入的）
                    newData.CompensationX = lastData.CompensationX;
                    newData.CompensationY = lastData.CompensationY;
                    newData.CompensationU = lastData.CompensationU;
                    newData.CompensationDistance = lastData.CompensationDistance;

                    newData.CompensationX2 = lastData.CompensationX2;
                    newData.CompensationY2 = lastData.CompensationY2;
                    newData.CompensationU2 = lastData.CompensationU2;
                    newData.CompensationDistance2 = lastData.CompensationDistance2;

                    // 添加新数据
                    SlotDataCollection.Add(newData);

                    // 如果数据超过3行，只保留最新的3行
                    while (SlotDataCollection.Count > 1)
                    {
                        SlotDataCollection.RemoveAt(0);
                    }
                }

                // 计算偏差
                CalculateDeviationsAndCompensations(newData);
                _logger.Info($"计算偏差 - CurrentDistance2: {newData.CurrentDistance2:F3}, BaseDistance2: {newData.BaseDistance2:F3}, DeviationDistance2: {newData.DeviationDistance2:F3}");
               
                // 更新显示行
                UpdateSlotDataRows();
                // 强制刷新UI
                RaisePropertyChanged(nameof(SlotDataRows));
                RaisePropertyChanged(nameof(SlotDataCollection));
                // 记录解析结果
                string logMessage = $"成功解析Offset数据: 第一组 X={offsetX:F3}, Y={offsetY:F3}, U={offsetU:F3}, H={offsetH:F3}";
                if (hasSecondGroup)
                {
                    logMessage += $", 第二组 X2={offsetX2:F3}, Y2={offsetY2:F3}, U2={offsetU2:F3}, H2={offsetH2:F3}";
                }
                _logger.Info(logMessage);

                // 更新操作状态
                SlotOperationStatus = $"Offset数据解析完成 ({DateTime.Now.ToString("HH:mm:ss")})";
            }
            catch (Exception ex)
            {
                _logger.Error($"解析Offset数据失败: {ex.Message}");
                SlotOperationStatus = $"解析Offset数据失败: {ex.Message}";
            }
        }

        private void CalculateDeviationsAndCompensations(SlotData data)
        {
            // 第一组数据计算（使用自己的基准值）
            data.DeviationX = data.CurrentX - data.BaseX;
            data.DeviationY = data.CurrentY - data.BaseY;
            data.DeviationU = data.CurrentU - data.BaseU;
            data.DeviationDistance = data.CurrentDistance - data.BaseDistance;

            data.DeviationXColor = GetDeviationColor(data.DeviationX);
            data.DeviationYColor = GetDeviationColor(data.DeviationY);
            data.DeviationUColor = GetDeviationColor(data.DeviationU);
            data.DeviationDistanceColor = GetDeviationColor(data.DeviationDistance);

            // 第二组数据计算（使用自己的基准值）
            data.DeviationX2 = data.CurrentX2 - data.BaseX2;
            data.DeviationY2 = data.CurrentY2 - data.BaseY2;
            data.DeviationU2 = data.CurrentU2 - data.BaseU2;
            data.DeviationDistance2 = data.CurrentDistance2 - data.BaseDistance2;

            data.DeviationX2Color = GetDeviationColor(data.DeviationX2);
            data.DeviationY2Color = GetDeviationColor(data.DeviationY2);
            data.DeviationU2Color = GetDeviationColor(data.DeviationU2);
            data.DeviationDistance2Color = GetDeviationColor(data.DeviationDistance2);

            // 检查是否需要设置补偿值
            bool compensationNotSet = true;// Math.Abs(data.CompensationX) < 0.001 &&
                                           //Math.Abs(data.CompensationY) < 0.001 &&
                                           //Math.Abs(data.CompensationU) < 0.001 &&
                                           //Math.Abs(data.CompensationDistance) < 0.001 &&
                                           //Math.Abs(data.CompensationX2) < 0.001 &&
                                           //Math.Abs(data.CompensationY2) < 0.001 &&
                                           //Math.Abs(data.CompensationU2) < 0.001 &&
                                           //Math.Abs(data.CompensationDistance2) < 0.001;

            if (compensationNotSet)
            {
                //data.CompensationX = -data.DeviationX;
                //data.CompensationY = -data.DeviationY;
                //data.CompensationU = -data.DeviationU;
                //data.CompensationDistance = -data.DeviationDistance;

                //data.CompensationX2 = -data.DeviationX2;
                //data.CompensationY2 = -data.DeviationY2;
                data.CompensationU2 = -data.DeviationU2;
                //data.CompensationDistance2 = -data.DeviationDistance2;
            }
            else
            {
                _logger.Debug($"使用已设置的补偿值: 第一组 X={data.CompensationX:F3}, Y={data.CompensationY:F3}, 第二组 X2={data.CompensationX2:F3}, Y2={data.CompensationY2:F3}");
            }
        }

        private Color GetDeviationColor(double deviation)
        {
            double absDeviation = Math.Abs(deviation);

            if (absDeviation < 0.1)
                return Colors.Green;
            else if (absDeviation < 0.5)
                return Colors.Orange;
            else
                return Colors.Red;
        }

        private double CalculateCurrentDistance()
        {
            return 0.0;
        }

        private void UpdateSlotDataRows()
        {
            try
            {
                if (System.Windows.Application.Current == null ||
                    System.Windows.Application.Current.Dispatcher == null)
                {
                    UpdateSlotDataRowsInternal();
                    return;
                }

                if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    UpdateSlotDataRowsInternal();
                }
                else
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        UpdateSlotDataRowsInternal();
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"更新SlotDataRows失败: {ex.Message}");
            }
        }

        private void UpdateSlotDataRowsInternal()
        {
            _isInitializing = true;

            try
            {
                foreach (var display in SlotDataRows)
                {
                    display.DataValueChanged -= OnDataValueChanged;
                }

                SlotDataRows.Clear();

                foreach (var slotData in SlotDataCollection)
                {
                    var display = new SlotDataDisplay(slotData, this);
                    display.DataValueChanged += OnDataValueChanged;
                    SlotDataRows.Add(display);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"UpdateSlotDataRowsInternal失败: {ex.Message}");
                throw;
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private void OnDataValueChanged(object sender, EventArgs e)
        {
            if (_isInitializing) return;
            SyncDisplayRowsToCollection();
        }

        private void OnApplyCompensation()
        {
            try
            {
                SyncDisplayRowsToCollection();
                _logger.Info("补偿值已应用");
                SlotOperationStatus = "补偿值已应用";
            }
            catch (Exception ex)
            {
                _logger.Error($"应用补偿失败: {ex.Message}");
                SlotOperationStatus = $"应用补偿失败: {ex.Message}";
            }
        }

        private void SyncDisplayRowsToCollection()
        {
            if (_isInitializing) return;

            for (int i = 0; i < SlotDataRows.Count && i < SlotDataCollection.Count; i++)
            {
                var rowData = SlotDataRows[i].ToSlotData();
                var collectionData = SlotDataCollection[i];

                // 更新基准值
                collectionData.BaseX = rowData.BaseX;
                collectionData.BaseY = rowData.BaseY;
                collectionData.BaseU = rowData.BaseU;
                collectionData.BaseDistance = rowData.BaseDistance;

                // 更新补偿值
                collectionData.CompensationX = rowData.CompensationX;
                collectionData.CompensationY = rowData.CompensationY;
                collectionData.CompensationU = rowData.CompensationU;
                collectionData.CompensationDistance = rowData.CompensationDistance;

                // 更新基准值
                collectionData.BaseX2 = rowData.BaseX2;
                collectionData.BaseY2 = rowData.BaseY2;
                collectionData.BaseU2 = rowData.BaseU2;
                collectionData.BaseDistance2 = rowData.BaseDistance2;

                // 更新补偿值
                collectionData.CompensationX2 = rowData.CompensationX2;
                collectionData.CompensationY2 = rowData.CompensationY2;
                collectionData.CompensationU2 = rowData.CompensationU2;
                collectionData.CompensationDistance2 = rowData.CompensationDistance2;

                // 重新计算偏差和补偿
                //CalculateDeviationsAndCompensations(collectionData);
            }
        }

        #endregion

        #region 参数管理

        private void SaveParameters()
        {
            try
            {
                SyncDisplayRowsToCollection();

                string customDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                        "Config",
                                        "Parameters");

                var slotParams = new SlotParameters
                {
                    StrippingDistance = StrippingDistance,
                    DistanceLowerLimit = DistanceLowerLimit,
                    AutoStrippingCount = AutoStrippingCount,
                    AlarmThreshold = AlarmThreshold,
                    EnableDynamicStripping = EnableDynamicStripping, // 新增：保存动态拨片启用状态
                    SlotDataList = SlotDataCollection.ToList()
                };

                if (_parameterStorage == null)
                {
                    SaveToFileSystem(slotParams);
                }
                else
                {
                    _parameterStorage.Save("SlotParameters", slotParams, customDirectory);
                }

                _logger.Info("拨片参数和位置数据保存成功");
                SlotOperationStatus = $"参数保存成功" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
            catch (Exception ex)
            {
                _logger.Error($"保存拨片参数失败: {ex.Message}");
                SlotOperationStatus = $"参数保存失败: {ex.Message}";
            }
        }

        private void SaveToFileSystem(SlotParameters parameters)
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                "Config", "Parameters", "SlotParameters.json");
                string directory = Path.GetDirectoryName(filePath);

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonConvert.SerializeObject(parameters, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"保存到文件系统失败: {ex.Message}", ex);
            }
        }

        private void LoadStoredParameters()
        {
            try
            {
                string customDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                        "Config",
                                        "Parameters");

                SlotParameters storedParams = null;

                if (_parameterStorage != null)
                {
                    storedParams = _parameterStorage.Load<SlotParameters>("SlotParameters", customDirectory);
                }

                if (storedParams == null)
                {
                    storedParams = LoadFromFileSystem();
                }

                if (storedParams != null)
                {
                    StrippingDistance = storedParams.StrippingDistance;
                    DistanceLowerLimit = storedParams.DistanceLowerLimit;
                    AutoStrippingCount = storedParams.AutoStrippingCount;
                    AlarmThreshold = storedParams.AlarmThreshold;
                    EnableDynamicStripping = storedParams.EnableDynamicStripping; //加载动态拨片启用状态

                    if (storedParams.SlotDataList != null && storedParams.SlotDataList.Count > 0)
                    {
                        SlotDataCollection.Clear();
                        SlotDataRows.Clear();

                        foreach (var savedData in storedParams.SlotDataList)
                        {
                            var slotData = new SlotData
                            {
                                BaseX = savedData.BaseX,
                                BaseY = savedData.BaseY,
                                BaseU = savedData.BaseU,
                                BaseDistance = savedData.BaseDistance,
                                CurrentX = savedData.CurrentX,
                                CurrentY = savedData.CurrentY,
                                CurrentU = savedData.CurrentU,
                                CurrentDistance = savedData.CurrentDistance,
                                CompensationX = savedData.CompensationX,
                                CompensationY = savedData.CompensationY,
                                CompensationU = savedData.CompensationU,
                                CompensationDistance = savedData.CompensationDistance,
                                IsDistanceBelowLimit = savedData.IsDistanceBelowLimit,
                                BaseX2 = savedData.BaseX2,
                                BaseY2 = savedData.BaseY2,
                                BaseU2 = savedData.BaseU2,
                                BaseDistance2 = savedData.BaseDistance2,
                                CurrentX2 = savedData.CurrentX2,
                                CurrentY2 = savedData.CurrentY2,
                                CurrentU2 = savedData.CurrentU2,
                                CurrentDistance2 = savedData.CurrentDistance2,
                                CompensationX2 = savedData.CompensationX2,
                                CompensationY2 = savedData.CompensationY2,
                                CompensationU2 = savedData.CompensationU2,
                                CompensationDistance2 = savedData.CompensationDistance2
                            };

                            CalculateDeviationsAndCompensations(slotData);
                            SlotDataCollection.Add(slotData);
                        }

                        UpdateSlotDataRows();

                        _logger.Info($"从参数文件加载了 {SlotDataCollection.Count} 条数据");
                    }
                    else
                    {
                        // 不初始化默认数据，等待拍照获取真实数据
                        _logger.Info("参数文件中没有位置数据，等待拍照获取基准值");
                        SlotOperationStatus = "无历史位置数据，请先拍照获取基准值";
                        // 不调用 InitializeDefaultData()
                    }

                    _logger.Info("拨片参数加载成功");
                    SlotOperationStatus = "参数加载成功";
                }
                else
                {
                    // 没有参数文件，也不初始化默认数据
                    _logger.Info("未找到参数文件，等待拍照获取基准值");
                    SlotOperationStatus = "未找到参数文件，请先拍照获取基准值";
                    // 不调用 InitializeDefaultData()
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"加载拨片参数失败: {ex.Message}");
                SlotOperationStatus = $"参数加载失败: {ex.Message}";
                // 失败时也不初始化默认数据
                // 不调用 InitializeDefaultData()
            }
        }
        private SlotParameters LoadFromFileSystem()
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                "Config", "Parameters", "SlotParameters.json");

                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    return JsonConvert.DeserializeObject<SlotParameters>(json);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private void ResetParameters()
        {
            StrippingDistance = 2.0;
            DistanceLowerLimit = 0.5;
            AutoStrippingCount = 3;
            AlarmThreshold = 3;
            EnableDynamicStripping = true; // 新增：重置为启用动态拨片
            CurrentStrippingCount = 0;
            ContinuousFailureCount = 0;
            HasAlarm = false;
            AlarmMessage = "";
            HasDynamicDistanceAlarm = false;
            DynamicDistanceAlarmMessage = "";

            _logger.Info("拨片参数已重置");
            SlotOperationStatus = "参数已重置";
        }

        private void ClearData()
        {
            SlotDataCollection.Clear();
            SlotDataRows.Clear();
            CurrentStrippingCount = 0;
            ContinuousFailureCount = 0;
            HasAlarm = false;
            AlarmMessage = "";
            HasDynamicDistanceAlarm = false;
            DynamicDistanceAlarmMessage = "";

            InitializeDefaultData();

            _logger.Info("拨片数据已清除");
            SlotOperationStatus = "数据已清除";
        }

        #endregion
    }

    #region 数据模型

    public class SlotData
    {
        // 第一组数据
        public double BaseX { get; set; }
        public double BaseY { get; set; }
        public double BaseU { get; set; }
        public double BaseDistance { get; set; }

        public double CurrentX { get; set; }
        public double CurrentY { get; set; }
        public double CurrentU { get; set; }
        public double CurrentDistance { get; set; }

        public double DeviationX { get; set; }
        public double DeviationY { get; set; }
        public double DeviationU { get; set; }
        public double DeviationDistance { get; set; }

        public double CompensationX { get; set; }
        public double CompensationY { get; set; }
        public double CompensationU { get; set; }
        public double CompensationDistance { get; set; }

        public Color DeviationXColor { get; set; }
        public Color DeviationYColor { get; set; }
        public Color DeviationUColor { get; set; }
        public Color DeviationDistanceColor { get; set; }

        // 第二组数据
        public double BaseX2 { get; set; }
        public double BaseY2 { get; set; }
        public double BaseU2 { get; set; }
        public double BaseDistance2 { get; set; }
        public double CurrentX2 { get; set; }
        public double CurrentY2 { get; set; }
        public double CurrentU2 { get; set; }
        public double CurrentDistance2 { get; set; }

        public double DeviationX2 { get; set; }
        public double DeviationY2 { get; set; }
        public double DeviationU2 { get; set; }
        public double DeviationDistance2 { get; set; }

        public double CompensationX2 { get; set; }
        public double CompensationY2 { get; set; }
        public double CompensationU2 { get; set; }
        public double CompensationDistance2 { get; set; }

        public Color DeviationX2Color { get; set; }
        public Color DeviationY2Color { get; set; }
        public Color DeviationU2Color { get; set; }
        public Color DeviationDistance2Color { get; set; }

        public bool IsDeviationXExceeded => Math.Abs(DeviationX) > 0.5;
        public bool IsDistanceBelowLimit { get; set; }
    }

    public class SlotParameters
    {
        public double StrippingDistance { get; set; }
        public double DistanceLowerLimit { get; set; }
        public int AutoStrippingCount { get; set; }
        public int AlarmThreshold { get; set; }
        public bool EnableDynamicStripping { get; set; } = true; // 新增：动态拨片启用状态
        public List<SlotData> SlotDataList { get; set; }
    }

    public class SlotDataDisplay : BindableBase
    {
        private readonly SlotControlViewModel _viewModel;
        private double _baseX;
        private double _baseY;
        private double _baseU;
        private double _baseDistance;
        private double _currentX;
        private double _currentY;
        private double _currentU;
        private double _currentDistance;
        private double _deviationX;
        private double _deviationY;
        private double _deviationU;
        private double _deviationDistance;
        private double _compensationX;
        private double _compensationY;
        private double _compensationU;
        private double _compensationDistance;
        private Color _deviationXColor;
        private Color _deviationYColor;
        private Color _deviationUColor;
        private Color _deviationDistanceColor;
        private bool _isDistanceBelowLimit;

        // 第二组数据属性
        private double _baseX2;
        private double _baseY2;
        private double _baseU2;
        private double _baseDistance2;
        private double _currentX2;
        private double _currentY2;
        private double _currentU2;
        private double _currentDistance2;
        private double _deviationX2;
        private double _deviationY2;
        private double _deviationU2;
        private double _deviationDistance2;
        private double _compensationX2;
        private double _compensationY2;
        private double _compensationU2;
        private double _compensationDistance2;
        private Color _deviationX2Color;
        private Color _deviationY2Color;
        private Color _deviationU2Color;
        private Color _deviationDistance2Color;
        private bool _isDistance2BelowLimit;

        // 将事件名改为DataValueChanged
        public event EventHandler DataValueChanged;

        // 第一组数据属性
        public double BaseX
        {
            get => _baseX;
            set
            {
                if (SetProperty(ref _baseX, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        public double BaseY
        {
            get => _baseY;
            set
            {
                if (SetProperty(ref _baseY, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        public double BaseU
        {
            get => _baseU;
            set
            {
                if (SetProperty(ref _baseU, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        public double BaseDistance
        {
            get => _baseDistance;
            set
            {
                if (SetProperty(ref _baseDistance, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        // 当前值
        public double CurrentX
        {
            get => _currentX;
            set
            {
                if (SetProperty(ref _currentX, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        public double CurrentY
        {
            get => _currentY;
            set
            {
                if (SetProperty(ref _currentY, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        public double CurrentU
        {
            get => _currentU;
            set
            {
                if (SetProperty(ref _currentU, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        public double CurrentDistance
        {
            get => _currentDistance;
            set
            {
                if (SetProperty(ref _currentDistance, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        // 偏差值
        public double DeviationX
        {
            get => _deviationX;
            private set => SetProperty(ref _deviationX, value);
        }

        public double DeviationY
        {
            get => _deviationY;
            private set => SetProperty(ref _deviationY, value);
        }

        public double DeviationU
        {
            get => _deviationU;
            private set => SetProperty(ref _deviationU, value);
        }

        public double DeviationDistance
        {
            get => _deviationDistance;
            private set => SetProperty(ref _deviationDistance, value);
        }

        // 补偿值
        public double CompensationX
        {
            get => _compensationX;
            set
            {
                if (SetProperty(ref _compensationX, value))
                {
                    RaisePropertyChanged(nameof(CompensationXForeground));
                    OnDataValueChanged();
                }
            }
        }

        public double CompensationY
        {
            get => _compensationY;
            set
            {
                if (SetProperty(ref _compensationY, value))
                {
                    RaisePropertyChanged(nameof(CompensationYForeground));
                    OnDataValueChanged();
                }
            }
        }

        public double CompensationU
        {
            get => _compensationU;
            set
            {
                if (SetProperty(ref _compensationU, value))
                {
                    RaisePropertyChanged(nameof(CompensationUForeground));
                    OnDataValueChanged();
                }
            }
        }

        public double CompensationDistance
        {
            get => _compensationDistance;
            set
            {
                if (SetProperty(ref _compensationDistance, value))
                {
                    RaisePropertyChanged(nameof(CompensationDistanceForeground));
                    OnDataValueChanged();
                }
            }
        }

        // 偏差颜色
        public Color DeviationXColor
        {
            get => _deviationXColor;
            private set => SetProperty(ref _deviationXColor, value);
        }

        public Color DeviationYColor
        {
            get => _deviationYColor;
            private set => SetProperty(ref _deviationYColor, value);
        }

        public Color DeviationUColor
        {
            get => _deviationUColor;
            private set => SetProperty(ref _deviationUColor, value);
        }

        public Color DeviationDistanceColor
        {
            get => _deviationDistanceColor;
            private set => SetProperty(ref _deviationDistanceColor, value);
        }

        // 状态
        public bool IsDistanceBelowLimit
        {
            get => _isDistanceBelowLimit;
            private set => SetProperty(ref _isDistanceBelowLimit, value);
        }

        // 显示颜色属性
        public Brush CurrentXForeground => new SolidColorBrush(DeviationXColor);
        public Brush CurrentYForeground => new SolidColorBrush(DeviationYColor);
        public Brush CurrentUForeground => new SolidColorBrush(DeviationUColor);
        public Brush CurrentDistanceForeground => CurrentDistance <= (_viewModel?.DistanceLowerLimit ?? 0.5) ? Brushes.Green : Brushes.Red;
        public Brush CompensationXForeground => GetCompensationColor(CompensationX);
        public Brush CompensationYForeground => GetCompensationColor(CompensationY);
        public Brush CompensationUForeground => GetCompensationColor(CompensationU);
        public Brush CompensationDistanceForeground => GetCompensationColor(CompensationDistance);
        public Brush DistanceBackground => IsDistanceBelowLimit ? Brushes.LightPink : Brushes.Transparent;

        // 第二组数据属性
        public double BaseX2
        {
            get => _baseX2;
            set
            {
                if (SetProperty(ref _baseX2, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        public double BaseY2
        {
            get => _baseY2;
            set
            {
                if (SetProperty(ref _baseY2, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        public double BaseU2
        {
            get => _baseU2;
            set
            {
                if (SetProperty(ref _baseU2, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        public double BaseDistance2
        {
            get => _baseDistance2;
            set
            {
                if (SetProperty(ref _baseDistance2, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        public double CurrentX2
        {
            get => _currentX2;
            set
            {
                if (SetProperty(ref _currentX2, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        public double CurrentY2
        {
            get => _currentY2;
            set
            {
                if (SetProperty(ref _currentY2, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        public double CurrentU2
        {
            get => _currentU2;
            set
            {
                if (SetProperty(ref _currentU2, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        public double CurrentDistance2
        {
            get => _currentDistance2;
            set
            {
                if (SetProperty(ref _currentDistance2, value))
                {
                    Recalculate();
                    OnDataValueChanged();
                }
            }
        }

        public double DeviationX2
        {
            get => _deviationX2;
            private set => SetProperty(ref _deviationX2, value);
        }

        public double DeviationY2
        {
            get => _deviationY2;
            private set => SetProperty(ref _deviationY2, value);
        }

        public double DeviationU2
        {
            get => _deviationU2;
            private set => SetProperty(ref _deviationU2, value);
        }

        public double DeviationDistance2
        {
            get => _deviationDistance2;
            private set => SetProperty(ref _deviationDistance2, value);
        }

        public double CompensationX2
        {
            get => _compensationX2;
            set
            {
                if (SetProperty(ref _compensationX2, value))
                {
                    RaisePropertyChanged(nameof(CompensationX2Foreground));
                    OnDataValueChanged();
                }
            }
        }

        public double CompensationY2
        {
            get => _compensationY2;
            set
            {
                if (SetProperty(ref _compensationY2, value))
                {
                    RaisePropertyChanged(nameof(CompensationY2Foreground));
                    OnDataValueChanged();
                }
            }
        }

        public double CompensationU2
        {
            get => _compensationU2;
            set
            {
                if (SetProperty(ref _compensationU2, value))
                {
                    RaisePropertyChanged(nameof(CompensationU2Foreground));
                    OnDataValueChanged();
                }
            }
        }

        public double CompensationDistance2
        {
            get => _compensationDistance2;
            set
            {
                if (SetProperty(ref _compensationDistance2, value))
                {
                    RaisePropertyChanged(nameof(CompensationDistance2Foreground));
                    OnDataValueChanged();
                }
            }
        }

        public Color DeviationX2Color
        {
            get => _deviationX2Color;
            private set => SetProperty(ref _deviationX2Color, value);
        }

        public Color DeviationY2Color
        {
            get => _deviationY2Color;
            private set => SetProperty(ref _deviationY2Color, value);
        }

        public Color DeviationU2Color
        {
            get => _deviationU2Color;
            private set => SetProperty(ref _deviationU2Color, value);
        }

        public Color DeviationDistance2Color
        {
            get => _deviationDistance2Color;
            private set => SetProperty(ref _deviationDistance2Color, value);
        }

        public bool IsDistance2BelowLimit
        {
            get => _isDistance2BelowLimit;
            private set => SetProperty(ref _isDistance2BelowLimit, value);
        }

        // 显示颜色属性
        public Brush CurrentX2Foreground => new SolidColorBrush(DeviationX2Color);
        public Brush CurrentY2Foreground => new SolidColorBrush(DeviationY2Color);
        public Brush CurrentU2Foreground => new SolidColorBrush(DeviationU2Color);
        public Brush CurrentDistance2Foreground => CurrentDistance2 <= (_viewModel?.DistanceLowerLimit ?? 0.5) ? Brushes.Green : Brushes.Red;
        public Brush CompensationX2Foreground => GetCompensationColor(CompensationX2);
        public Brush CompensationY2Foreground => GetCompensationColor(CompensationY2);
        public Brush CompensationU2Foreground => GetCompensationColor(CompensationU2);
        public Brush CompensationDistance2Foreground => GetCompensationColor(CompensationDistance2);
        public Brush Distance2Background => IsDistance2BelowLimit ? Brushes.LightPink : Brushes.Transparent;

        // 构造函数
        public SlotDataDisplay() { }

        public SlotDataDisplay(SlotData slotData, SlotControlViewModel viewModel = null)
        {
            _viewModel = viewModel;

            _baseX = slotData.BaseX;
            _baseY = slotData.BaseY;
            _baseU = slotData.BaseU;
            _baseDistance = slotData.BaseDistance;
            _currentX = slotData.CurrentX;
            _currentY = slotData.CurrentY;
            _currentU = slotData.CurrentU;
            _currentDistance = slotData.CurrentDistance;
            _deviationX = slotData.DeviationX;
            _deviationY = slotData.DeviationY;
            _deviationU = slotData.DeviationU;
            _deviationDistance = slotData.DeviationDistance;
            _compensationX = slotData.CompensationX;
            _compensationY = slotData.CompensationY;
            _compensationU = slotData.CompensationU;
            _compensationDistance = slotData.CompensationDistance;
            _deviationXColor = slotData.DeviationXColor;
            _deviationYColor = slotData.DeviationYColor;
            _deviationUColor = slotData.DeviationUColor;
            _deviationDistanceColor = slotData.DeviationDistanceColor;
            _isDistanceBelowLimit = slotData.IsDistanceBelowLimit;

            _baseX2 = slotData.BaseX2;
            _baseY2 = slotData.BaseY2;
            _baseU2 = slotData.BaseU2;
            _baseDistance2 = slotData.BaseDistance2;
            _currentX2 = slotData.CurrentX2;
            _currentY2 = slotData.CurrentY2;
            _currentU2 = slotData.CurrentU2;
            _currentDistance2 = slotData.CurrentDistance2;
            _deviationX2 = slotData.DeviationX2;
            _deviationY2 = slotData.DeviationY2;
            _deviationU2 = slotData.DeviationU2;
            _deviationDistance2 = slotData.DeviationDistance2;
            _compensationX2 = slotData.CompensationX2;
            _compensationY2 = slotData.CompensationY2;
            _compensationU2 = slotData.CompensationU2;
            _compensationDistance2 = slotData.CompensationDistance2;
            _deviationX2Color = slotData.DeviationX2Color;
            _deviationY2Color = slotData.DeviationY2Color;
            _deviationU2Color = slotData.DeviationU2Color;
            _deviationDistance2Color = slotData.DeviationDistance2Color;
        }

        public SlotData ToSlotData()
        {
            return new SlotData
            {
                BaseX = BaseX,
                BaseY = BaseY,
                BaseU = BaseU,
                BaseDistance = BaseDistance,
                CurrentX = CurrentX,
                CurrentY = CurrentY,
                CurrentU = CurrentU,
                CurrentDistance = CurrentDistance,
                DeviationX = DeviationX,
                DeviationY = DeviationY,
                DeviationU = DeviationU,
                DeviationDistance = DeviationDistance,
                CompensationX = CompensationX,
                CompensationY = CompensationY,
                CompensationU = CompensationU,
                CompensationDistance = CompensationDistance,
                DeviationXColor = DeviationXColor,
                DeviationYColor = DeviationYColor,
                DeviationUColor = DeviationUColor,
                DeviationDistanceColor = DeviationDistanceColor,
                IsDistanceBelowLimit = IsDistanceBelowLimit,

                // 第二组数据
                BaseX2 = BaseX2,
                BaseY2 = BaseY2,
                BaseU2 = BaseU2,
                BaseDistance2 = BaseDistance2,
                CurrentX2 = CurrentX2,
                CurrentY2 = CurrentY2,
                CurrentU2 = CurrentU2,
                CurrentDistance2 = CurrentDistance2,
                DeviationX2 = DeviationX2,
                DeviationY2 = DeviationY2,
                DeviationU2 = DeviationU2,
                DeviationDistance2 = DeviationDistance2,
                CompensationX2 = CompensationX2,
                CompensationY2 = CompensationY2,
                CompensationU2 = CompensationU2,
                CompensationDistance2 = CompensationDistance2,
                DeviationX2Color = DeviationX2Color,
                DeviationY2Color = DeviationY2Color,
                DeviationU2Color = DeviationU2Color,
                DeviationDistance2Color = DeviationDistance2Color,
            };
        }

        private void Recalculate()
        {
            DeviationX = CurrentX - BaseX;
            DeviationY = CurrentY - BaseY;
            DeviationU = CurrentU - BaseU;
            DeviationDistance = CurrentDistance - BaseDistance;

            DeviationXColor = GetDeviationColor(DeviationX);
            DeviationYColor = GetDeviationColor(DeviationY);
            DeviationUColor = GetDeviationColor(DeviationU);
            DeviationDistanceColor = GetDeviationColor(DeviationDistance);

            IsDistanceBelowLimit = CurrentDistance <= (_viewModel?.DistanceLowerLimit ?? 0.5);

            //if (Math.Abs(CompensationX) < 0.001 && Math.Abs(CompensationY) < 0.001 &&
            //    Math.Abs(CompensationU) < 0.001 && Math.Abs(CompensationDistance) < 0.001)
            //{
            //    CompensationX = -DeviationX;
            //    CompensationY = -DeviationY;
            //    CompensationU = -DeviationU;
            //    CompensationDistance = -DeviationDistance;
            //}

            RaisePropertyChanged(nameof(CurrentXForeground));
            RaisePropertyChanged(nameof(CurrentYForeground));
            RaisePropertyChanged(nameof(CurrentUForeground));
            RaisePropertyChanged(nameof(CurrentDistanceForeground));
            RaisePropertyChanged(nameof(DistanceBackground));
            RaisePropertyChanged(nameof(CompensationXForeground));
            RaisePropertyChanged(nameof(CompensationYForeground));
            RaisePropertyChanged(nameof(CompensationUForeground));
            RaisePropertyChanged(nameof(CompensationDistanceForeground));

            // 第二组计算
            DeviationX2 = CurrentX2 - BaseX2;
            DeviationY2 = CurrentY2 - BaseY2;
            DeviationU2 = CurrentU2 - BaseU2;
            DeviationDistance2 = CurrentDistance2 - BaseDistance2;

            DeviationX2Color = GetDeviationColor(DeviationX2);
            DeviationY2Color = GetDeviationColor(DeviationY2);
            DeviationU2Color = GetDeviationColor(DeviationU2);
            DeviationDistance2Color = GetDeviationColor(DeviationDistance2);
            IsDistanceBelowLimit = CurrentDistance <= (_viewModel?.DistanceLowerLimit ?? 0.5);
            IsDistance2BelowLimit = CurrentDistance2 <= (_viewModel?.DistanceLowerLimit ?? 0.5);

            // 自动设置补偿值（如果未设置）
            //if (Math.Abs(CompensationX) < 0.001 && Math.Abs(CompensationY) < 0.001 &&
            //    Math.Abs(CompensationU) < 0.001 && Math.Abs(CompensationDistance) < 0.001 &&
            //    Math.Abs(CompensationX2) < 0.001 && Math.Abs(CompensationY2) < 0.001 &&
            //    Math.Abs(CompensationU2) < 0.001 && Math.Abs(CompensationDistance2) < 0.001)
            //{
            //    CompensationX = -DeviationX;
            //    CompensationY = -DeviationY;
            //    CompensationU = -DeviationU;
            //    CompensationDistance = -DeviationDistance;

            //    CompensationX2 = -DeviationX2;
            //    CompensationY2 = -DeviationY2;
            //    CompensationU2 = -DeviationU2;
            //    CompensationDistance2 = -DeviationDistance2;
            //}
            // 更新UI绑定
            RaisePropertyChanged(nameof(CurrentX2Foreground));
            RaisePropertyChanged(nameof(CurrentY2Foreground));
            RaisePropertyChanged(nameof(CurrentU2Foreground));
            RaisePropertyChanged(nameof(CurrentDistance2Foreground));
            RaisePropertyChanged(nameof(Distance2Background));
            RaisePropertyChanged(nameof(CompensationX2Foreground));
            RaisePropertyChanged(nameof(CompensationY2Foreground));
            RaisePropertyChanged(nameof(CompensationU2Foreground));
            RaisePropertyChanged(nameof(CompensationDistance2Foreground));
        }

        private Color GetDeviationColor(double deviation)
        {
            double absDeviation = Math.Abs(deviation);

            if (absDeviation < 0.1)
                return Colors.Green;
            else if (absDeviation < 0.5)
                return Colors.Orange;
            else
                return Colors.Red;
        }

        private Brush GetCompensationColor(double compensation)
        {
            double absCompensation = Math.Abs(compensation);

            if (absCompensation < 0.1)
                return Brushes.Green;
            else if (absCompensation < 0.5)
                return Brushes.Orange;
            else
                return Brushes.Red;
        }

        private void OnDataValueChanged()
        {
            DataValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public class TabStrippingDistance : BindableBase
    {
        private int _tabIndex;
        public int TabIndex
        {
            get => _tabIndex;
            set => SetProperty(ref _tabIndex, value);
        }

        private double _h2Height;
        public double H2Height
        {
            get => _h2Height;
            set => SetProperty(ref _h2Height, value);
        }
        private double _baseDistance2;
        public double BaseDistance2
        {
            get => _baseDistance2;
            set
            {
                if (SetProperty(ref _baseDistance2, value))
                {
                    // 触发UI更新
                    RaisePropertyChanged(nameof(BaseDistance2Display));
                }
            }
        }
        public string BaseDistance2Display =>
                  double.IsNaN(BaseDistance2) ? "N/A" : BaseDistance2.ToString("F3") + "mm";
        private double _currentDistance2;
        public double CurrentDistance2
        {
            get => _currentDistance2;
            set => SetProperty(ref _currentDistance2, value);
        }

        private double _strippingDistance;
        public double StrippingDistance
        {
            get => _strippingDistance;
            set
            {
                if (SetProperty(ref _strippingDistance, value))
                {
                    // 当拨动距离变化时，重新计算下压高度
                    CalculatePressHeight();
                    RaisePropertyChanged(nameof(StrippingDistanceForeground));
                }
            }
        }
        private double _pressHeight;
        public double PressHeight
        {
            get => _pressHeight;
            set => SetProperty(ref _pressHeight, value);
        }
        private double _overPressure = 0.15; // 默认过压量，可以从配方获取
        public double OverPressure
        {
            get => _overPressure;
            set
            {
                if (SetProperty(ref _overPressure, value))
                {
                    CalculatePressHeight();
                }
            }
        }
        private string _directionDescription = "";
        public string DirectionDescription
        {
            get => _directionDescription;
            set => SetProperty(ref _directionDescription, value);
        }

        private DateTime _calculationTime;
        public DateTime CalculationTime
        {
            get => _calculationTime;
            set => SetProperty(ref _calculationTime, value);
        }

        // 显示格式化后的时间
        public string CalculationTimeDisplay =>
            CalculationTime == DateTime.MinValue ? "未计算" : CalculationTime.ToString("HH:mm:ss");

        // 下压高度计算公式: H4 + 0.27 + 过压量
        private void CalculatePressHeight()
        {
            if (double.IsNaN(StrippingDistance))
            {
                PressHeight = double.NaN;
            }
            else
            {
                // 下压高度 = H4拨动距离 + 0.27 + 过压量
                PressHeight = StrippingDistance + 0.27 + OverPressure;
            }
            RaisePropertyChanged(nameof(PressHeightForeground));
        }
        // 过压量设置方法（可以从外部配方系统获取）
        public void SetOverPressureFromRecipe(double overPressure)
        {
            OverPressure = overPressure;
        }
        // 拨动距离显示样式
        public Brush StrippingDistanceForeground
        {
            get
            {
                if (double.IsNaN(StrippingDistance))
                    return Brushes.Gray;

                if (StrippingDistance > 0)
                    return Brushes.Blue; // 正值，蓝色
                else if (StrippingDistance < 0)
                    return Brushes.Red;  // 负值，红色
                else
                    return Brushes.Green; // 零值，绿色
            }
        }
        // 下压高度显示样式
        public Brush PressHeightForeground
        {
            get
            {
                if (double.IsNaN(PressHeight))
                    return Brushes.Gray;

                if (PressHeight > 0)
                    return Brushes.DarkGreen; // 正值，深绿色
                else if (PressHeight < 0)
                    return Brushes.DarkRed;   // 负值，深红色
                else
                    return Brushes.Black;     // 零值，黑色
            }
        }
    }
    #endregion
}

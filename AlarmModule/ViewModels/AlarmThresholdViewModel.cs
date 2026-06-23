using AlarmModule.Interfaces;
using AlarmModule.Models;
using Core.Abstraction;
using Core.Utilities;
using Framework.Mvvm;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace AlarmModule.ViewModels
{
    /// <summary>
    /// 报警阈值配置视图模型：管理报警阈值配置的增删改查、输入校验、重复检测、搜索筛选与批量操作
    /// </summary>
    public class AlarmThresholdViewModel : ViewModelBase
    {
        private readonly IAlarmRepository _repository;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localizationService;

        /// <summary>全量配置源（未过滤），筛选时基于此集合派生 ThresholdConfigs</summary>
        private List<AlarmThresholdConfig> _allConfigs = new List<AlarmThresholdConfig>();

        /// <summary>当前编辑的配置主键Id；0表示新增，>0表示编辑已有项</summary>
        private int _editingId;

        // 编辑字段
        private string _editAlarmCode = string.Empty;
        private string _editAlarmSource = string.Empty;
        private double _editThresholdValue;
        private AlarmLevel _editAlarmLevel = AlarmLevel.General;
        private AlarmType _editAlarmType = AlarmType.ParameterOutOfLimit;
        private int _editSuppressionWindowSeconds = 60;
        private bool _editIsEnabled = true;
        private bool _isEditing;

        // 筛选字段
        private string _searchKeyword = string.Empty;
        private AlarmLevel? _filterLevel;
        private AlarmType? _filterType;
        private EnabledFilterOption _filterEnabled = EnabledFilterOption.All;

        /// <summary>
        /// 阈值配置集合（经筛选后展示）
        /// </summary>
        public ObservableCollection<AlarmThresholdConfig> ThresholdConfigs { get; } = new ObservableCollection<AlarmThresholdConfig>();

        /// <summary>
        /// 当前选中的配置项（单选，用于编辑按钮定位）
        /// </summary>
        public AlarmThresholdConfig? SelectedConfig
        {
            get => _selectedConfig;
            set => SetProperty(ref _selectedConfig, value);
        }
        private AlarmThresholdConfig? _selectedConfig;

        /// <summary>
        /// 编辑中的报警代码
        /// </summary>
        public string EditAlarmCode
        {
            get => _editAlarmCode;
            set => SetProperty(ref _editAlarmCode, value);
        }

        /// <summary>
        /// 编辑中的报警来源
        /// </summary>
        public string EditAlarmSource
        {
            get => _editAlarmSource;
            set => SetProperty(ref _editAlarmSource, value);
        }

        /// <summary>
        /// 编辑中的阈值
        /// </summary>
        public double EditThresholdValue
        {
            get => _editThresholdValue;
            set => SetProperty(ref _editThresholdValue, value);
        }

        /// <summary>
        /// 编辑中的报警等级
        /// </summary>
        public AlarmLevel EditAlarmLevel
        {
            get => _editAlarmLevel;
            set => SetProperty(ref _editAlarmLevel, value);
        }

        /// <summary>
        /// 编辑中的报警类型
        /// </summary>
        public AlarmType EditAlarmType
        {
            get => _editAlarmType;
            set => SetProperty(ref _editAlarmType, value);
        }

        /// <summary>
        /// 编辑中的防抖窗口（秒）
        /// </summary>
        public int EditSuppressionWindowSeconds
        {
            get => _editSuppressionWindowSeconds;
            set => SetProperty(ref _editSuppressionWindowSeconds, value);
        }

        /// <summary>
        /// 编辑中的启用状态
        /// </summary>
        public bool EditIsEnabled
        {
            get => _editIsEnabled;
            set => SetProperty(ref _editIsEnabled, value);
        }

        /// <summary>
        /// 是否处于编辑模式
        /// </summary>
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetProperty(ref _isEditing, value))
                {
                    SaveCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 编辑面板标题（新增/编辑）
        /// </summary>
        public string EditPanelTitle => _editingId > 0
            ? _localizationService.GetResourceOrDefault("AlarmThresholdEditConfig", "编辑阈值配置")
            : _localizationService.GetResourceOrDefault("AlarmThresholdNewConfig", "新增阈值配置");

        /// <summary>
        /// 搜索关键字（按AlarmCode/AlarmSource模糊匹配）
        /// </summary>
        public string SearchKeyword
        {
            get => _searchKeyword;
            set
            {
                if (SetProperty(ref _searchKeyword, value))
                    ApplyFilter();
            }
        }

        /// <summary>
        /// 等级筛选条件（null表示全部）
        /// </summary>
        public AlarmLevel? FilterLevel
        {
            get => _filterLevel;
            set
            {
                if (SetProperty(ref _filterLevel, value))
                    ApplyFilter();
            }
        }

        /// <summary>
        /// 类型筛选条件（null表示全部）
        /// </summary>
        public AlarmType? FilterType
        {
            get => _filterType;
            set
            {
                if (SetProperty(ref _filterType, value))
                    ApplyFilter();
            }
        }

        /// <summary>
        /// 启用状态筛选条件
        /// </summary>
        public EnabledFilterOption FilterEnabled
        {
            get => _filterEnabled;
            set
            {
                if (SetProperty(ref _filterEnabled, value))
                    ApplyFilter();
            }
        }

        /// <summary>
        /// 当前筛选结果是否全部选中（用于表头全选复选框 ThreeState）
        /// </summary>
        public bool? IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (SetProperty(ref _isAllSelected, value))
                    OnSelectAllChanged(value);
            }
        }
        private bool? _isAllSelected = false;

        /// <summary>
        /// 已选数量文本（用于状态栏显示）
        /// </summary>
        public string SelectedCountText => string.Format(
            _localizationService.GetResourceOrDefault("SelectedCountPrefix", "已选 ") + "{0}" +
            _localizationService.GetResourceOrDefault("SelectedCountSuffix", " 项"),
            ThresholdConfigs.Count(c => c.IsSelected));

        /// <summary>
        /// 总数量文本（用于状态栏显示）
        /// </summary>
        public string TotalCountText => string.Format(
            _localizationService.GetResourceOrDefault("TotalCountPrefix", "共 ") + "{0}" +
            _localizationService.GetResourceOrDefault("TotalCountSuffix", " 项配置"),
            ThresholdConfigs.Count);

        /// <summary>报警等级选项列表（含空选项表示"全部"）</summary>
        public List<AlarmLevel?> AlarmLevelFilters { get; } =
            new List<AlarmLevel?> { null, AlarmLevel.Emergency, AlarmLevel.Serious, AlarmLevel.General, AlarmLevel.Prompt };

        /// <summary>报警类型选项列表（含空选项表示"全部"）</summary>
        public List<AlarmType?> AlarmTypeFilters { get; } =
            new List<AlarmType?> { null, AlarmType.HardwareFault, AlarmType.ParameterOutOfLimit, AlarmType.CommunicationError, AlarmType.ProcessError };

        /// <summary>启用状态筛选选项列表</summary>
        public List<EnabledFilterOption> EnabledFilters { get; } =
            Enum.GetValues(typeof(EnabledFilterOption)).Cast<EnabledFilterOption>().ToList();

        /// <summary>报警等级选项列表（编辑表单用，不含空选项）</summary>
        public List<AlarmLevel> AlarmLevels { get; } =
            Enum.GetValues(typeof(AlarmLevel)).Cast<AlarmLevel>().ToList();

        /// <summary>报警类型选项列表（编辑表单用，不含空选项）</summary>
        public List<AlarmType> AlarmTypes { get; } =
            Enum.GetValues(typeof(AlarmType)).Cast<AlarmType>().ToList();

        /// <summary>新增配置命令</summary>
        public DelegateCommand AddCommand { get; }

        /// <summary>编辑配置命令</summary>
        public DelegateCommand<AlarmThresholdConfig> EditCommand { get; }

        /// <summary>删除配置命令</summary>
        public DelegateCommand<AlarmThresholdConfig> DeleteCommand { get; }

        /// <summary>保存配置命令</summary>
        public DelegateCommand SaveCommand { get; }

        /// <summary>取消编辑命令</summary>
        public DelegateCommand CancelCommand { get; }

        /// <summary>刷新配置列表命令</summary>
        public DelegateCommand RefreshCommand { get; }

        /// <summary>批量启用命令</summary>
        public DelegateCommand BatchEnableCommand { get; }

        /// <summary>批量禁用命令</summary>
        public DelegateCommand BatchDisableCommand { get; }

        /// <summary>批量删除命令</summary>
        public DelegateCommand BatchDeleteCommand { get; }

        /// <summary>
        /// 构造函数：注入报警仓储、日志服务、本地化服务，初始化命令并加载数据
        /// </summary>
        public AlarmThresholdViewModel(IAlarmRepository repository, ILoggerService logger, ILocalizationService localizationService)
        {
            _repository = repository;
            _logger = logger;
            _localizationService = localizationService;

            AddCommand = new DelegateCommand(OnAdd);
            EditCommand = new DelegateCommand<AlarmThresholdConfig>(OnEdit);
            DeleteCommand = new DelegateCommand<AlarmThresholdConfig>(OnDelete);
            SaveCommand = new DelegateCommand(OnSave, () => IsEditing);
            CancelCommand = new DelegateCommand(OnCancel, () => IsEditing);
            RefreshCommand = new DelegateCommand(OnRefresh);
            BatchEnableCommand = new DelegateCommand(OnBatchEnable, HasSelection);
            BatchDisableCommand = new DelegateCommand(OnBatchDisable, HasSelection);
            BatchDeleteCommand = new DelegateCommand(OnBatchDelete, HasSelection);

            RefreshCommand.Execute();
        }

        /// <summary>
        /// 是否存在选中项（批量命令可用条件）
        /// </summary>
        private bool HasSelection() => ThresholdConfigs.Any(c => c.IsSelected);

        /// <summary>
        /// 订阅配置项的属性变更，IsSelected变化时刷新批量命令与计数
        /// </summary>
        private void SubscribeConfig(AlarmThresholdConfig config)
        {
            config.PropertyChanged += OnConfigPropertyChanged;
        }

        /// <summary>
        /// 配置项属性变更回调：IsSelected变化时刷新命令可用性与计数文本
        /// </summary>
        private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AlarmThresholdConfig.IsSelected))
            {
                BatchEnableCommand.RaiseCanExecuteChanged();
                BatchDisableCommand.RaiseCanExecuteChanged();
                BatchDeleteCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(SelectedCountText));
                UpdateSelectAllState();
            }
            else if (e.PropertyName == nameof(AlarmThresholdConfig.IsEnabled))
            {
                // 启用状态变更后，若当前按启用状态筛选则需重新应用筛选
                if (FilterEnabled != EnabledFilterOption.All)
                    ApplyFilter();
            }
        }

        /// <summary>
        /// 根据当前选中状态更新表头全选复选框状态（不触发回调）
        /// </summary>
        private void UpdateSelectAllState()
        {
            var selectedCount = ThresholdConfigs.Count(c => c.IsSelected);
            bool? newState;
            if (selectedCount == 0)
                newState = false;
            else if (selectedCount == ThresholdConfigs.Count)
                newState = true;
            else
                newState = null;

            if (_isAllSelected != newState)
            {
                _isAllSelected = newState;
                RaisePropertyChanged(nameof(IsAllSelected));
            }
        }

        /// <summary>
        /// 表头全选切换：设置所有可见项的IsSelected
        /// </summary>
        private void OnSelectAllChanged(bool? state)
        {
            if (!state.HasValue) return;
            foreach (var config in ThresholdConfigs)
                config.IsSelected = state.Value;
        }

        /// <summary>
        /// 新增配置：清空编辑字段并进入编辑模式
        /// </summary>
        private void OnAdd()
        {
            _editingId = 0;
            EditAlarmCode = string.Empty;
            EditAlarmSource = string.Empty;
            EditThresholdValue = 0;
            EditAlarmLevel = AlarmLevel.General;
            EditAlarmType = AlarmType.ParameterOutOfLimit;
            EditSuppressionWindowSeconds = 60;
            EditIsEnabled = true;
            IsEditing = true;
            RaisePropertyChanged(nameof(EditPanelTitle));
        }

        /// <summary>
        /// 编辑配置：将选中配置加载到编辑字段
        /// </summary>
        private void OnEdit(AlarmThresholdConfig? config)
        {
            if (config == null) return;

            _editingId = config.Id;
            EditAlarmCode = config.AlarmCode;
            EditAlarmSource = config.AlarmSource ?? string.Empty;
            EditThresholdValue = config.ThresholdValue;
            EditAlarmLevel = config.AlarmLevel;
            EditAlarmType = config.AlarmType;
            EditSuppressionWindowSeconds = config.SuppressionWindowSeconds;
            EditIsEnabled = config.IsEnabled;
            IsEditing = true;
            RaisePropertyChanged(nameof(EditPanelTitle));
        }

        /// <summary>
        /// 取消编辑：退出编辑模式并清空编辑态
        /// </summary>
        private void OnCancel()
        {
            IsEditing = false;
            _editingId = 0;
        }

        /// <summary>
        /// 删除配置：确认后删除选中项
        /// </summary>
        private async void OnDelete(AlarmThresholdConfig? config)
        {
            if (config == null) return;

            try
            {
                var result = MessageBox.Show(
                    string.Format(_localizationService.GetResourceOrDefault("AlarmDeleteConfirm", "确定要删除报警代码 '{0}' 的阈值配置吗？"), config.AlarmCode),
                    _localizationService.GetResourceOrDefault("AlarmDeleteConfirmTitle", "确认删除"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await _repository.DeleteThresholdConfigAsync(config.Id);
                    _allConfigs.Remove(config);
                    ApplyFilter();
                    _logger.Info($"已删除阈值配置：{config.AlarmCode}@{config.AlarmSource}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"删除阈值配置失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 保存配置：执行输入校验、重复检测后新增或更新阈值配置
        /// </summary>
        private async void OnSave()
        {
            // 输入校验
            if (!ValidateInput(out string errorMessage))
            {
                MessageBox.Show(errorMessage,
                    _localizationService.GetResourceOrDefault("SaveFailed", "保存失败"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var normalizedSource = string.IsNullOrWhiteSpace(EditAlarmSource) ? null : EditAlarmSource.Trim();

                // 重复检测：查询是否已存在相同 AlarmCode+AlarmSource 的配置
                var existing = await _repository.GetThresholdConfigAsync(EditAlarmCode.Trim(), normalizedSource);
                if (existing != null && existing.Id != _editingId)
                {
                    MessageBox.Show(
                        _localizationService.GetResourceOrDefault("DuplicateThresholdConfig", "已存在相同报警代码和来源的阈值配置"),
                        _localizationService.GetResourceOrDefault("SaveFailed", "保存失败"),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var config = new AlarmThresholdConfig
                {
                    Id = _editingId,
                    AlarmCode = EditAlarmCode.Trim(),
                    AlarmSource = normalizedSource,
                    ThresholdValue = EditThresholdValue,
                    AlarmLevel = EditAlarmLevel,
                    AlarmType = EditAlarmType,
                    SuppressionWindowSeconds = EditSuppressionWindowSeconds,
                    IsEnabled = EditIsEnabled
                };

                await _repository.SaveThresholdConfigAsync(config);
                IsEditing = false;
                _editingId = 0;
                await LoadConfigsAsync();
                _logger.Info($"已保存阈值配置：{config.AlarmCode}@{config.AlarmSource}");
            }
            catch (Exception ex)
            {
                _logger.Error($"保存阈值配置失败：{ex.Message}");
                MessageBox.Show(ex.Message,
                    _localizationService.GetResourceOrDefault("SaveFailed", "保存失败"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 输入校验：AlarmCode必填、防抖窗口范围、阈值有效性
        /// </summary>
        private bool ValidateInput(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(EditAlarmCode))
            {
                errorMessage = _localizationService.GetResourceOrDefault("AlarmCodeRequired", "报警代码不能为空");
                return false;
            }

            if (double.IsNaN(EditThresholdValue))
            {
                errorMessage = _localizationService.GetResourceOrDefault("ThresholdValueInvalid", "阈值必须为有效数值");
                return false;
            }

            if (EditSuppressionWindowSeconds < 0 || EditSuppressionWindowSeconds > 3600)
            {
                errorMessage = _localizationService.GetResourceOrDefault("DebounceRangeError", "防抖时间必须在 0-3600 秒之间");
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// 批量启用选中项
        /// </summary>
        private async void OnBatchEnable()
        {
            var selected = ThresholdConfigs.Where(c => c.IsSelected).ToList();
            if (selected.Count == 0)
            {
                ShowNothingSelectedWarning();
                return;
            }

            try
            {
                var ids = selected.Select(c => c.Id).ToList();
                await _repository.BatchUpdateEnabledAsync(ids, true);
                foreach (var c in selected) c.IsEnabled = true;
                _logger.Info($"批量启用 {ids.Count} 项阈值配置");
            }
            catch (Exception ex)
            {
                _logger.Error($"批量启用失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 批量禁用选中项
        /// </summary>
        private async void OnBatchDisable()
        {
            var selected = ThresholdConfigs.Where(c => c.IsSelected).ToList();
            if (selected.Count == 0)
            {
                ShowNothingSelectedWarning();
                return;
            }

            try
            {
                var ids = selected.Select(c => c.Id).ToList();
                await _repository.BatchUpdateEnabledAsync(ids, false);
                foreach (var c in selected) c.IsEnabled = false;
                _logger.Info($"批量禁用 {ids.Count} 项阈值配置");
            }
            catch (Exception ex)
            {
                _logger.Error($"批量禁用失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 批量删除选中项
        /// </summary>
        private async void OnBatchDelete()
        {
            var selected = ThresholdConfigs.Where(c => c.IsSelected).ToList();
            if (selected.Count == 0)
            {
                ShowNothingSelectedWarning();
                return;
            }

            var result = MessageBox.Show(
                string.Format(_localizationService.GetResourceOrDefault("BatchDeleteConfirm", "确定要删除选中的 {0} 项阈值配置吗？"), selected.Count),
                _localizationService.GetResourceOrDefault("BatchDeleteConfirmTitle", "确认批量删除"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var ids = selected.Select(c => c.Id).ToList();
                await _repository.BatchDeleteAsync(ids);

                foreach (var c in selected)
                    _allConfigs.Remove(c);

                ApplyFilter();
                _logger.Info($"批量删除 {ids.Count} 项阈值配置");
            }
            catch (Exception ex)
            {
                _logger.Error($"批量删除失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 提示未选中任何项
        /// </summary>
        private void ShowNothingSelectedWarning()
        {
            MessageBox.Show(
                _localizationService.GetResourceOrDefault("BatchOperationNothingSelected", "请先选择要操作的配置项"),
                _localizationService.GetResourceOrDefault("SaveFailed", "保存失败"),
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 刷新配置列表
        /// </summary>
        private async void OnRefresh()
        {
            try
            {
                await LoadConfigsAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"刷新阈值配置失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 从数据库加载阈值配置列表并应用当前筛选
        /// </summary>
        private async System.Threading.Tasks.Task LoadConfigsAsync()
        {
            _allConfigs = await _repository.GetAllThresholdConfigsAsync();
            ApplyFilter();
        }

        /// <summary>
        /// 根据筛选条件从全量集合派生展示集合
        /// </summary>
        private void ApplyFilter()
        {
            IEnumerable<AlarmThresholdConfig> result = _allConfigs;

            // 关键字模糊匹配 AlarmCode / AlarmSource
            if (!string.IsNullOrWhiteSpace(SearchKeyword))
            {
                var kw = SearchKeyword.Trim();
                result = result.Where(c =>
                    c.AlarmCode.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (c.AlarmSource != null && c.AlarmSource.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            if (FilterLevel.HasValue)
                result = result.Where(c => c.AlarmLevel == FilterLevel.Value);

            if (FilterType.HasValue)
                result = result.Where(c => c.AlarmType == FilterType.Value);

            result = FilterEnabled switch
            {
                EnabledFilterOption.EnabledOnly => result.Where(c => c.IsEnabled),
                EnabledFilterOption.DisabledOnly => result.Where(c => !c.IsEnabled),
                _ => result
            };

            ThresholdConfigs.Clear();
            foreach (var config in result)
            {
                SubscribeConfig(config);
                ThresholdConfigs.Add(config);
            }

            RaisePropertyChanged(nameof(TotalCountText));
            RaisePropertyChanged(nameof(SelectedCountText));
            UpdateSelectAllState();
            BatchEnableCommand.RaiseCanExecuteChanged();
            BatchDisableCommand.RaiseCanExecuteChanged();
            BatchDeleteCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// 启用状态筛选选项
    /// </summary>
    public enum EnabledFilterOption
    {
        /// <summary>全部</summary>
        All = 0,
        /// <summary>仅启用</summary>
        EnabledOnly = 1,
        /// <summary>仅禁用</summary>
        DisabledOnly = 2
    }
}

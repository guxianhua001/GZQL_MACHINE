using AlarmModule.Interfaces;
using AlarmModule.Models;
using Core.Abstraction;
using Core.Utilities;
using Framework.Mvvm;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace AlarmModule.ViewModels
{
    /// <summary>
    /// 报警阈值配置视图模型：管理报警阈值配置的增删改查操作
    /// </summary>
    public class AlarmThresholdViewModel : ViewModelBase
    {
        private readonly IAlarmRepository _repository;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localizationService;

        private ObservableCollection<AlarmThresholdConfig> _thresholdConfigs = new ObservableCollection<AlarmThresholdConfig>();
        private AlarmThresholdConfig? _selectedConfig;
        private string _editAlarmCode = string.Empty;
        private string _editAlarmSource = string.Empty;
        private double _editThresholdValue;
        private AlarmLevel _editAlarmLevel = AlarmLevel.General;
        private AlarmType _editAlarmType = AlarmType.ParameterOutOfLimit;
        private int _editSuppressionWindowSeconds = 60;
        private bool _editIsEnabled = true;
        private bool _isEditing;

        /// <summary>
        /// 阈值配置集合
        /// </summary>
        public ObservableCollection<AlarmThresholdConfig> ThresholdConfigs
        {
            get => _thresholdConfigs;
            set => SetProperty(ref _thresholdConfigs, value);
        }

        /// <summary>
        /// 当前选中的配置项
        /// </summary>
        public AlarmThresholdConfig? SelectedConfig
        {
            get => _selectedConfig;
            set => SetProperty(ref _selectedConfig, value);
        }

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
            set => SetProperty(ref _isEditing, value);
        }

        /// <summary>
        /// 报警等级选项列表
        /// </summary>
        public List<AlarmLevel> AlarmLevels { get; } =
            Enum.GetValues(typeof(AlarmLevel)).Cast<AlarmLevel>().ToList();

        /// <summary>
        /// 报警类型选项列表
        /// </summary>
        public List<AlarmType> AlarmTypes { get; } =
            Enum.GetValues(typeof(AlarmType)).Cast<AlarmType>().ToList();

        /// <summary>
        /// 新增配置命令
        /// </summary>
        public DelegateCommand AddCommand { get; }

        /// <summary>
        /// 编辑配置命令
        /// </summary>
        public DelegateCommand<AlarmThresholdConfig> EditCommand { get; }

        /// <summary>
        /// 删除配置命令
        /// </summary>
        public DelegateCommand<AlarmThresholdConfig> DeleteCommand { get; }

        /// <summary>
        /// 保存配置命令
        /// </summary>
        public DelegateCommand SaveCommand { get; }

        /// <summary>
        /// 刷新配置列表命令
        /// </summary>
        public DelegateCommand RefreshCommand { get; }

        /// <summary>
        /// 构造函数：注入报警仓储和日志服务，初始化命令并加载数据
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
            RefreshCommand = new DelegateCommand(OnRefresh);

            RefreshCommand.Execute();
        }

        /// <summary>
        /// 新增配置：清空编辑字段并进入编辑模式
        /// </summary>
        private void OnAdd()
        {
            EditAlarmCode = string.Empty;
            EditAlarmSource = string.Empty;
            EditThresholdValue = 0;
            EditAlarmLevel = AlarmLevel.General;
            EditAlarmType = AlarmType.ParameterOutOfLimit;
            EditSuppressionWindowSeconds = 60;
            EditIsEnabled = true;
            IsEditing = true;
            SaveCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 编辑配置：将选中配置加载到编辑字段
        /// </summary>
        private void OnEdit(AlarmThresholdConfig? config)
        {
            if (config == null) return;

            EditAlarmCode = config.AlarmCode;
            EditAlarmSource = config.AlarmSource ?? string.Empty;
            EditThresholdValue = config.ThresholdValue;
            EditAlarmLevel = config.AlarmLevel;
            EditAlarmType = config.AlarmType;
            EditSuppressionWindowSeconds = config.SuppressionWindowSeconds;
            EditIsEnabled = config.IsEnabled;
            IsEditing = true;
            SaveCommand.RaiseCanExecuteChanged();
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
                    ThresholdConfigs.Remove(config);
                    _logger.Info($"已删除阈值配置：{config.AlarmCode}@{config.AlarmSource}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"删除阈值配置失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 保存配置：新增或更新阈值配置
        /// </summary>
        private async void OnSave()
        {
            try
            {
                var config = new AlarmThresholdConfig
                {
                    AlarmCode = EditAlarmCode,
                    AlarmSource = string.IsNullOrWhiteSpace(EditAlarmSource) ? null : EditAlarmSource,
                    ThresholdValue = EditThresholdValue,
                    AlarmLevel = EditAlarmLevel,
                    AlarmType = EditAlarmType,
                    SuppressionWindowSeconds = EditSuppressionWindowSeconds,
                    IsEnabled = EditIsEnabled
                };

                await _repository.SaveThresholdConfigAsync(config);
                IsEditing = false;
                SaveCommand.RaiseCanExecuteChanged();
                await LoadConfigsAsync();
                _logger.Info($"已保存阈值配置：{config.AlarmCode}@{config.AlarmSource}");
            }
            catch (Exception ex)
            {
                _logger.Error($"保存阈值配置失败：{ex.Message}");
            }
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
        /// 从数据库加载阈值配置列表
        /// </summary>
        private async System.Threading.Tasks.Task LoadConfigsAsync()
        {
            var configs = await _repository.GetAllThresholdConfigsAsync();
            ThresholdConfigs.Clear();
            foreach (var config in configs)
            {
                ThresholdConfigs.Add(config);
            }
        }
    }
}

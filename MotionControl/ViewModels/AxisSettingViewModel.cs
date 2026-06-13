using Core.Abstraction;
using MotionControl.Models;
using MotionControl.Interfaces;
using MotionControl.Services;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using MotionControl.Dialogs;

namespace MotionControl.ViewModels
{
    public class AxisSettingViewModel : BindableBase
    {
        private readonly IAxisParameterService _parameterService;
        private readonly IMotionCardFactory _cardFactory;
        private readonly ILocalizationService _loc;
        private AxisParams _currentAxisParams;

        public ObservableCollection<AxisInfo> Axes { get; }

        public ObservableCollection<LogicLevel> LogicLevels { get; }
            = new ObservableCollection<LogicLevel> { LogicLevel.Low, LogicLevel.High };

        public ObservableCollection<MappedIO> MappedIOs { get; } = new ObservableCollection<MappedIO>();
        public ObservableCollection<int> HomingModes { get; } = new ObservableCollection<int>();
        public ObservableCollection<CardInfo> Cards { get; } = new ObservableCollection<CardInfo>();

        private int _viewModeIndex = 0;
        public int ViewModeIndex
        {
            get => _viewModeIndex;
            set
            {
                SetProperty(ref _viewModeIndex, value);
                RaisePropertyChanged(nameof(IsSingleAxisMode));
                RaisePropertyChanged(nameof(IsSystemMode));
                OnViewModeChanged();
            }
        }

        public bool IsSingleAxisMode => ViewModeIndex == 0;
        public bool IsSystemMode => ViewModeIndex == 1;

        public ObservableCollection<InterpolationSystem> InterpolationSystems { get; }
            = new ObservableCollection<InterpolationSystem>();

        private InterpolationSystem _selectedSystem;
        public InterpolationSystem SelectedSystem
        {
            get => _selectedSystem;
            set
            {
                if (SetProperty(ref _selectedSystem, value))
                {
                    UpdateAxesInSystem();
                }
            }
        }

        private ObservableCollection<AxisInfo> _availableAxesForSystem = new ObservableCollection<AxisInfo>();
        public ObservableCollection<AxisInfo> AvailableAxesForSystem
        {
            get => _availableAxesForSystem;
            set => SetProperty(ref _availableAxesForSystem, value);
        }

        private AxisInfo _selectedAvailableAxis;
        public AxisInfo SelectedAvailableAxis
        {
            get => _selectedAvailableAxis;
            set { if (SetProperty(ref _selectedAvailableAxis, value)) AddAxisToSystemCommand?.RaiseCanExecuteChanged(); }
        }

        private AxisInSystem _selectedAxisInSystem;
        public AxisInSystem SelectedAxisInSystem
        {
            get => _selectedAxisInSystem;
            set { if (SetProperty(ref _selectedAxisInSystem, value)) RemoveAxisFromSystemCommand?.RaiseCanExecuteChanged(); }
        }

        private ObservableCollection<AxisInSystem> _selectedAxesInSystem
            = new ObservableCollection<AxisInSystem>();
        public ObservableCollection<AxisInSystem> SelectedAxesInSystem
        {
            get => _selectedAxesInSystem;
            set => SetProperty(ref _selectedAxesInSystem, value);
        }

        public DelegateCommand UploadParamsCommand { get; }
        public DelegateCommand DownloadParamsCommand { get; }
        public DelegateCommand DownloadAllParametersCommand { get; }
        public DelegateCommand ReadAllFromCardCommand { get; }
        public DelegateCommand ManageSystemAxesCommand { get; }
        public DelegateCommand AddSystemCommand { get; }
        public DelegateCommand DeleteSystemCommand { get; }
        public DelegateCommand SaveSystemParamsCommand { get; }
        public DelegateCommand ApplySystemParamsCommand { get; }
        public DelegateCommand LoadSystemParamsCommand { get; }
        public DelegateCommand AddAxisToSystemCommand { get; }
        public DelegateCommand RemoveAxisFromSystemCommand { get; }
        public DelegateCommand SaveParamsCommand { get; }
        public DelegateCommand LoadParamsCommand { get; }

        private AxisInfo _selectedAxis;
        public AxisInfo SelectedAxis
        {
            get => _selectedAxis;
            set => SetSelectedAxis(value);
        }

        /// <summary>
        /// 设置当前选中的轴并更新参数绑定
        /// </summary>
        private void SetSelectedAxis(AxisInfo value)
        {
            if (SetProperty(ref _selectedAxis, value))
            {
                if (_currentAxisParams != null)
                {
                    _currentAxisParams.PropertyChanged -= OnAxisParamsChanged;
                }

                _currentAxisParams = value?.Params;

                if (_currentAxisParams != null)
                {
                    _currentAxisParams.PropertyChanged += OnAxisParamsChanged;
                }

                if (CurrentAxisParams?.EmergencyStop?.MappedIO != null)
                {
                    var matchedIO = MappedIOs.FirstOrDefault(io =>
                        io.SetId == CurrentAxisParams.EmergencyStop.MappedIO.SetId &&
                        io.PortName == CurrentAxisParams.EmergencyStop.MappedIO.PortName);

                    if (matchedIO != null)
                    {
                        CurrentAxisParams.EmergencyStop.MappedIO = matchedIO;
                    }
                }
                RaisePropertyChanged(nameof(SelectedAxis));
                RaisePropertyChanged(nameof(CurrentAxisParams));
                ParametersChanged = false;

                RaisePropertyChanged(nameof(MappedIOs));
                if (_currentAxisParams?.EmergencyStop != null)
                {
                    RaisePropertyChanged(nameof(CurrentAxisParams.EmergencyStop.MappedIO));
                }
            }
        }

        /// <summary>
        /// 轴参数变更事件处理
        /// </summary>
        private void OnAxisParamsChanged(object sender, PropertyChangedEventArgs e)
        {
            ParametersChanged = true;
        }

        public AxisParams CurrentAxisParams
        {
            get => _currentAxisParams;
            set => SetProperty(ref _currentAxisParams, value);
        }

        private bool _parametersChanged;
        public bool ParametersChanged
        {
            get => _parametersChanged;
            set => SetProperty(ref _parametersChanged, value);
        }

        /// <summary>
        /// 构造函数：初始化轴设置视图模型
        /// </summary>
        public AxisSettingViewModel(IAxisParameterService parameterService, IMotionCardFactory cardFactory, ILocalizationService loc)
        {
            _parameterService = parameterService;
            _cardFactory = cardFactory;
            _loc = loc;

            for (int i = -3; i <= 37; i++) HomingModes.Add(i);
            HomingModes.Add(65533);
            InitializeMappedIOs();

            Axes = new ObservableCollection<AxisInfo>(_parameterService.LoadAllAxes());

            foreach (var axis in Axes)
            {
                if (axis.Params != null)
                {
                    axis.Params.PropertyChanged += (s, e) => ParametersChanged = true;
                }
            }

            if (Axes.Any()) SelectedAxis = Axes[0];

            var systems = _parameterService.LoadInterpolationSystems().ToList();
            foreach (var system in systems)
            {
                InterpolationSystems.Add(system);
            }

            InitializeCards();
            UpdateAxesInSystem();

            DownloadParamsCommand = new DelegateCommand(WriteToCard);
            UploadParamsCommand = new DelegateCommand(ReadFromCard);
            DownloadAllParametersCommand = new DelegateCommand(WriteAllToCard);
            ReadAllFromCardCommand = new DelegateCommand(ReadAllFromCard);
            ManageSystemAxesCommand = new DelegateCommand(OnManageSystemAxes);
            AddSystemCommand = new DelegateCommand(OnAddSystem);
            DeleteSystemCommand = new DelegateCommand(OnDeleteSystem);
            SaveSystemParamsCommand = new DelegateCommand(OnSaveSystemParams);
            ApplySystemParamsCommand = new DelegateCommand(OnApplyInterpolationSystem);
            LoadSystemParamsCommand = new DelegateCommand(LoadSystemConfigurations);
            SaveParamsCommand = new DelegateCommand(OnSaveParams);
            LoadParamsCommand = new DelegateCommand(OnLoadParams);
            AddAxisToSystemCommand = new DelegateCommand(OnAddAxisToSystem, CanAddAxisToSystem);
            RemoveAxisFromSystemCommand = new DelegateCommand(OnRemoveAxisFromSystem, CanRemoveAxisFromSystem);
        }

        /// <summary>
        /// 初始化控制卡列表
        /// </summary>
        private void InitializeCards()
        {
            Cards.Clear();

            int cardCount = _cardFactory.CardCount;

            for (int i = 0; i < cardCount; i++)
            {
                Cards.Add(new CardInfo
                {
                    CardId = i,
                    Description = $"控制卡 {i}"
                });
            }

            if (!Cards.Any())
            {
                Cards.Add(new CardInfo { CardId = 0, Description = "默认控制卡" });
            }
        }

        /// <summary>
        /// 更新插补系中的轴显示，同时刷新可用轴列表
        /// </summary>
        private void UpdateAxesInSystem()
        {
            if (SelectedSystem == null) return;

            SelectedAxesInSystem.Clear();

            foreach (var axisId in SelectedSystem.Axes)
            {
                var parts = axisId.Split('-');
                if (parts.Length != 2) continue;

                int setCardId = int.Parse(parts[0]);
                int setAxisId = int.Parse(parts[1]);

                var axisConfig = Axes.FirstOrDefault(a =>
                    a.CardId == setCardId &&
                    a.AxisId == setAxisId);

                if (axisConfig != null)
                {
                    SelectedAxesInSystem.Add(new AxisInSystem
                    {
                        Name = axisConfig.Name,
                        ConfigId = axisConfig.Name,
                        SetCardId = setCardId,
                        SetAxisId = setAxisId
                    });
                }
                else
                {
                    SelectedAxesInSystem.Add(new AxisInSystem
                    {
                        Name = axisId,
                        ConfigId = axisId,
                        SetCardId = setCardId,
                        SetAxisId = setAxisId
                    });
                }
            }

            RefreshAvailableAxes();
        }

        /// <summary>
        /// 刷新可用轴列表（排除已在插补系中的轴）
        /// </summary>
        private void RefreshAvailableAxes()
        {
            AvailableAxesForSystem.Clear();

            if (SelectedSystem == null) return;

            var usedAxes = new HashSet<string>(SelectedSystem.Axes);

            foreach (var axis in Axes)
            {
                if (!usedAxes.Contains(axis.ConfigId))
                {
                    AvailableAxesForSystem.Add(axis);
                }
            }
        }

        /// <summary>
        /// 初始化IO映射集合
        /// </summary>
        private void InitializeMappedIOs()
        {
            for (int i = 0; i < 20; i++)
            {
                MappedIOs.Add(new MappedIO
                {
                    MapIoIndex = (short)i,
                    PortName = $"DI-{i}",
                    Description = $"急停映射端口 {i}",
                    IoType = 3,
                    MapIoType = 6
                });
            }
        }

        /// <summary>
        /// 写入到卡：将选中轴参数写入控制卡
        /// </summary>
        private async void WriteToCard()
        {
            if (SelectedAxis == null) return;

            try
            {
                await _parameterService.WriteToCardAsync(SelectedAxis);
                ParametersChanged = false;
                MessageBox.Show(
                    _loc.GetResource("AxisSetting_WriteToCardSuccess"),
                    _loc.GetResource("AxisSetting_WriteToCardToolTip"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(_loc.GetResource("AxisSetting_WriteToCardFailed"), ex.Message),
                    _loc.GetResource("AxisSetting_Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 从卡读取：从控制卡读取选中轴参数
        /// </summary>
        private async void ReadFromCard()
        {
            if (SelectedAxis == null) return;

            try
            {
                await _parameterService.ReadFromCardAsync(SelectedAxis);
                CurrentAxisParams = SelectedAxis.Params;
                RaisePropertyChanged(nameof(CurrentAxisParams));
                ParametersChanged = false;
                MessageBox.Show(
                    _loc.GetResource("AxisSetting_ReadFromCardSuccess"),
                    _loc.GetResource("AxisSetting_ReadFromCardToolTip"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(_loc.GetResource("AxisSetting_ReadFromCardFailed"), ex.Message),
                    _loc.GetResource("AxisSetting_Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 写入所有轴参数到控制卡
        /// </summary>
        private async void WriteAllToCard()
        {
            var dialog = new ParameterProgressDialog(_loc.GetResource("AxisSetting_WritingAllAxes"));

            try
            {
                if (Application.Current.MainWindow != null &&
                    Application.Current.MainWindow.IsLoaded)
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                else
                {
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                dialog.Show();

                await _parameterService.WriteAllToCardAsync(new ProgressReporterAdapter(dialog));
                ParametersChanged = false;
                dialog.Close();
                MessageBox.Show(
                    _loc.GetResource("AxisSetting_WriteAllToCardSuccess"),
                    _loc.GetResource("AxisSetting_SetAllAxesToolTip"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                dialog.Close();
                MessageBox.Show(
                    string.Format(_loc.GetResource("AxisSetting_WriteToCardFailed"), ex.Message),
                    _loc.GetResource("AxisSetting_Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 从控制卡读取所有轴参数
        /// </summary>
        private async void ReadAllFromCard()
        {
            var dialog = new ParameterProgressDialog(_loc.GetResource("AxisSetting_ReadingAllAxes"));

            try
            {
                if (Application.Current.MainWindow != null &&
                    Application.Current.MainWindow.IsLoaded)
                {
                    dialog.Owner = Application.Current.MainWindow;
                }
                else
                {
                    dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                dialog.Show();

                await _parameterService.ReadAllFromCardAsync(new ProgressReporterAdapter(dialog));
                ParametersChanged = false;
                dialog.Close();

                if (SelectedAxis != null)
                {
                    CurrentAxisParams = SelectedAxis.Params;
                    RaisePropertyChanged(nameof(CurrentAxisParams));
                }

                MessageBox.Show(
                    _loc.GetResource("AxisSetting_ReadAllFromCardSuccess"),
                    _loc.GetResource("AxisSetting_ReadAllFromCardToolTip"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                dialog.Close();
                MessageBox.Show(
                    string.Format(_loc.GetResource("AxisSetting_ReadFromCardFailed"), ex.Message),
                    _loc.GetResource("AxisSetting_Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 视图模式切换处理
        /// </summary>
        private void OnViewModeChanged()
        {
            if (IsSystemMode && SelectedSystem == null &&
                InterpolationSystems.Any())
            {
                SelectedSystem = InterpolationSystems.First();
            }
        }

        /// <summary>
        /// 打开轴管理对话框
        /// </summary>
        private void OnManageSystemAxes()
        {
            if (SelectedSystem == null)
            {
                MessageBox.Show("请先选择一个插补系", "提示",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        /// <summary>
        /// 添加新插补系
        /// </summary>
        private void OnAddSystem()
        {
            if (InterpolationSystems.Count >= 10) return;

            int newId = InterpolationSystems.Max(s => s.CoordId) + 1;
            InterpolationSystems.Add(new InterpolationSystem
            {
                CoordId = newId,
                ActCardId = 0,
                Axes = new List<string>(),
                Params = new InterpolationParams()
            });

            SelectedSystem = InterpolationSystems.Last();
        }

        /// <summary>
        /// 删除选中的插补系
        /// </summary>
        private void OnDeleteSystem()
        {
            if (SelectedSystem != null)
            {
                if (InterpolationSystems.Count <= 1)
                {
                    MessageBox.Show("必须至少保留一个插补系", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var result = MessageBox.Show($"确定要删除插补系 {SelectedSystem.CoordId} 吗?",
                    "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    int index = InterpolationSystems.IndexOf(SelectedSystem);
                    InterpolationSystems.Remove(SelectedSystem);
                    if (InterpolationSystems.Count > 0)
                    {
                        if (index >= InterpolationSystems.Count)
                            index = InterpolationSystems.Count - 1;
                        SelectedSystem = InterpolationSystems[index];
                    }
                }
            }
        }

        /// <summary>
        /// 应用插补系：将插补系参数写入控制卡并保存到配置文件
        /// </summary>
        private void OnApplyInterpolationSystem()
        {
            if (SelectedSystem != null)
            {
                try
                {
                    _parameterService.WriteInterpolationToCard(SelectedSystem);
                    _parameterService.SaveAllInterpolationSystems(InterpolationSystems);
                    MessageBox.Show("插补系参数已写入控制卡并保存",
                                   "应用插补系", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"应用失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 从配置文件加载插补系参数
        /// </summary>
        private void OnSaveSystemParams()
        {
            try
            {
                var savedSystems = _parameterService.LoadAllInterpolationSystems();
                foreach (var savedSystem in savedSystems)
                {
                    var system = InterpolationSystems.FirstOrDefault(s =>
                        s.CoordId == savedSystem.CoordId && s.ActCardId == savedSystem.ActCardId);
                    if (system != null)
                    {
                        system.Axes = savedSystem.Axes ?? new List<string>();
                        system.Params = savedSystem.Params ?? new InterpolationParams();

                        if (SelectedSystem?.CoordId == savedSystem.CoordId)
                        {
                            UpdateAxesInSystem();
                        }
                    }
                }

                MessageBox.Show(
                    _loc.GetResource("AxisSetting_LoadInterpolationSuccess"),
                    _loc.GetResource("AxisSetting_LoadInterpolationToolTip"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(_loc.GetResource("AxisSetting_LoadInterpolationFailed"), ex.Message),
                    _loc.GetResource("AxisSetting_Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 保存所有参数（轴参数+插补系参数）到文件
        /// </summary>
        private void OnSaveParams()
        {
            try
            {
                _parameterService.SaveAllAxisParameters(Axes);
                _parameterService.SaveAllInterpolationSystems(InterpolationSystems);
                ParametersChanged = false;
                MessageBox.Show(
                    _loc.GetResource("AxisSetting_SaveParamsSuccess"),
                    _loc.GetResource("AxisSetting_SaveToFileToolTip"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(_loc.GetResource("AxisSetting_SaveParamsFailed"), ex.Message),
                    _loc.GetResource("AxisSetting_Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 从文件载入所有参数（轴参数+插补系参数）
        /// </summary>
        private void OnLoadParams()
        {
            try
            {
                var savedParams = _parameterService.LoadAllAxisParameters();
                foreach (var axis in Axes)
                {
                    string key = $"{axis.CardId}-{axis.AxisId}";
                    if (savedParams.ContainsKey(key))
                    {
                        axis.Params = savedParams[key];
                        axis.Params.PropertyChanged += (s, e) => ParametersChanged = true;
                    }
                }

                if (SelectedAxis != null)
                {
                    string selectedKey = $"{SelectedAxis.CardId}-{SelectedAxis.AxisId}";
                    if (savedParams.ContainsKey(selectedKey))
                    {
                        CurrentAxisParams = SelectedAxis.Params;
                        RaisePropertyChanged(nameof(CurrentAxisParams));
                    }
                }

                LoadSystemConfigurations();

                ParametersChanged = false;
                MessageBox.Show(
                    _loc.GetResource("AxisSetting_LoadParamsSuccess"),
                    _loc.GetResource("AxisSetting_LoadFromFileToolTip"),
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    string.Format(_loc.GetResource("AxisSetting_LoadParamsFailed"), ex.Message),
                    _loc.GetResource("AxisSetting_Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 从配置文件加载插补系参数
        /// </summary>
        private void LoadSystemConfigurations()
        {
            try
            {
                var savedSystems = _parameterService.LoadAllInterpolationSystems();
                foreach (var savedSystem in savedSystems)
                {
                    var system = InterpolationSystems.FirstOrDefault(s =>
                        s.CoordId == savedSystem.CoordId && s.ActCardId == savedSystem.ActCardId);
                    if (system != null)
                    {
                        system.Axes = savedSystem.Axes ?? new List<string>();
                        system.Params = savedSystem.Params ?? new InterpolationParams();

                        if (SelectedSystem?.CoordId == savedSystem.CoordId)
                        {
                            UpdateAxesInSystem();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载插补系配置失败: {ex.Message}",
                    "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 添加选中轴到当前插补系
        /// </summary>
        private void OnAddAxisToSystem()
        {
            if (SelectedSystem == null || SelectedAvailableAxis == null) return;

            string configId = SelectedAvailableAxis.ConfigId;
            if (!SelectedSystem.Axes.Contains(configId))
            {
                SelectedSystem.Axes.Add(configId);
                UpdateAxesInSystem();
                SyncSystemAxesToHwConfig();
                AddAxisToSystemCommand.RaiseCanExecuteChanged();
                RemoveAxisFromSystemCommand.RaiseCanExecuteChanged();
            }
        }

        private bool CanAddAxisToSystem() => SelectedSystem != null && SelectedAvailableAxis != null;

        /// <summary>
        /// 从当前插补系移除选中轴
        /// </summary>
        private void OnRemoveAxisFromSystem()
        {
            if (SelectedSystem == null || SelectedAxisInSystem == null) return;

            string configId = $"{SelectedAxisInSystem.SetCardId}-{SelectedAxisInSystem.SetAxisId}";
            if (SelectedSystem.Axes.Contains(configId))
            {
                SelectedSystem.Axes.Remove(configId);
                UpdateAxesInSystem();
                SyncSystemAxesToHwConfig();
                AddAxisToSystemCommand.RaiseCanExecuteChanged();
                RemoveAxisFromSystemCommand.RaiseCanExecuteChanged();
            }
        }

        private bool CanRemoveAxisFromSystem() => SelectedSystem != null && SelectedAxisInSystem != null;

        /// <summary>
        /// 同步插补系轴配置到hwcfg.xml
        /// </summary>
        private void SyncSystemAxesToHwConfig()
        {
            try
            {
                _parameterService.SyncInterpolationAxesToHwConfig(InterpolationSystems);
                _parameterService.SaveAllInterpolationSystems(InterpolationSystems);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"同步到配置文件失败: {ex.Message}", "警告",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 进度报告适配器
        /// </summary>
        private class ProgressReporterAdapter : IProgressReporter
        {
            private readonly ParameterProgressDialog _dialog;

            public ProgressReporterAdapter(ParameterProgressDialog dialog)
            {
                _dialog = dialog;
            }

            public void Report(double progress, string statusMessage = null)
            {
                _dialog.Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrEmpty(statusMessage))
                    {
                        _dialog.SetStatus(statusMessage);
                    }
                    _dialog.SetProgress(progress);
                });
            }
        }
    }
}

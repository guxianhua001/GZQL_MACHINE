
using AxisConfiguration.Models;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Newtonsoft.Json;
using System.Linq;
using System.Windows.Controls;
using System;
using Interfaces;
using System.Collections.Generic;
using ModuleCore.ViewModels;

namespace AxisConfiguration.ViewModels
{
    public class AxisSettingViewModel : BindableBase
    {
        private readonly IAxisConfigService _configService;
        private AxisParams _currentAxisParams;
        public ObservableCollection<AxisInfo> Axes { get; }
        public ObservableCollection<LogicLevel> LogicLevels { get; }
            = new ObservableCollection<LogicLevel> { LogicLevel.Low, LogicLevel.High };

        public ObservableCollection<MappedIO> MappedIOs { get; } = new ObservableCollection<MappedIO>();
        public ObservableCollection<int> HomingModes { get; } = new ObservableCollection<int>();
        public ObservableCollection<CardInfo> Cards { get; } = new ObservableCollection<CardInfo>();
        // 视图模式切换属性
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

        // 插补系列表
        public ObservableCollection<InterpolationSystem> InterpolationSystems { get; }
            = new ObservableCollection<InterpolationSystem>();

        // 当前选中的插补系
        private InterpolationSystem _selectedSystem;
        public InterpolationSystem SelectedSystem
        {
            get => _selectedSystem;
            set
            {
                if (SetProperty(ref _selectedSystem, value))
                {
                    // 选中的插补系变化时
                    UpdateAxesInSystem();
                }
            }
        }
        // 当前插补系包含的轴
        private ObservableCollection<AxisInSystem> _selectedAxesInSystem
            = new ObservableCollection<AxisInSystem>();
        public ObservableCollection<AxisInSystem> SelectedAxesInSystem
        {
            get => _selectedAxesInSystem;
            set => SetProperty(ref _selectedAxesInSystem, value);
        }
        // 命令
        public DelegateCommand UploadParamsCommand { get; }
        public DelegateCommand DownloadParamsCommand { get; }
        public DelegateCommand ImportCommand { get; }
        public DelegateCommand ExportCommand { get; }
        public DelegateCommand SaveParamsCommand { get; }
        public DelegateCommand LoadParamsCommand { get; }
        public DelegateCommand DownloadAllParametersCommand { get; }
        public DelegateCommand ManageSystemAxesCommand { get; }
        public DelegateCommand AddSystemCommand { get; }
        public DelegateCommand DeleteSystemCommand { get; }
        public DelegateCommand SaveSystemParamsCommand { get; }
        public DelegateCommand ApplySystemParamsCommand { get; }
        public DelegateCommand LoadSystemParamsCommand { get; }
        private AxisInfo _selectedAxis;
        public AxisInfo SelectedAxis
        {
            get => _selectedAxis;
            set => SetSelectedAxis(value);
        }

        private void SetSelectedAxis(AxisInfo value)
        {
            if (SetProperty(ref _selectedAxis, value))
            {
                // 取消旧参数的监听
                if (_currentAxisParams != null)
                {
                    _currentAxisParams.PropertyChanged -= OnAxisParamsChanged;
                }

                // 设置新参数
                _currentAxisParams = value?.Params;

                // 监听新参数变更
                if (_currentAxisParams != null)
                {
                    _currentAxisParams.PropertyChanged += OnAxisParamsChanged;
                }
                // 急停IO映射对象引用 不加这句 会导致急停IO映射不显示当前选中项
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

        public AxisSettingViewModel(IAxisConfigService configService)
        {
            _configService = configService;

            // 初始化回零模式
            for (int i = -3; i <= 37; i++) HomingModes.Add(i);

            // 初始化IO映射
            InitializeMappedIOs();

            // 从配置加载轴
            Axes = new ObservableCollection<AxisInfo>(_configService.LoadAllAxes());

            // 添加监听
            foreach (var axis in Axes)
            {
                if (axis.Params != null)
                {
                    axis.Params.PropertyChanged += (s, e) => ParametersChanged = true;
                }
            }
            // 选中第一个轴
            if (Axes.Any()) SelectedAxis = Axes[0];
            // 加载插补系
            var systems = _configService.LoadInterpolationSystems().ToList();
            foreach (var system in systems)
            {
                InterpolationSystems.Add(system);
            }
            InitializeCards();
            UpdateAxesInSystem();
            // 初始化命令
            DownloadParamsCommand = new DelegateCommand(DownloadSelectedAxis);
            UploadParamsCommand = new DelegateCommand(UploadSelectedAxis);
            SaveParamsCommand = new DelegateCommand(SaveParams);
            ImportCommand = new DelegateCommand(ImportJson);
            ExportCommand = new DelegateCommand(ExportJson);
            LoadParamsCommand = new DelegateCommand(LoadFromFile);
            DownloadAllParametersCommand = new DelegateCommand(DownloadAllParameters);
            ManageSystemAxesCommand = new DelegateCommand(OnManageSystemAxes);
            AddSystemCommand = new DelegateCommand(OnAddSystem);
            DeleteSystemCommand = new DelegateCommand(OnDeleteSystem);
            SaveSystemParamsCommand = new DelegateCommand(OnSaveSystemParams);
            ApplySystemParamsCommand = new DelegateCommand(OnApplySystemParams);
            LoadSystemParamsCommand = new DelegateCommand(LoadSystemConfigurations);
        }
        private void InitializeCards()
        {
            Cards.Clear();
            // 获取所有实际存在的控制卡ID
            var cardIds = new HashSet<int>();

            // 从轴配置获取卡片ID
            foreach (var axis in Axes)
            {
                cardIds.Add(axis.CardId);
            }

            // 从插补系配置获取卡片ID
            foreach (var system in InterpolationSystems)
            {
                if (system.ActCardId >= 0)
                {
                    cardIds.Add(system.ActCardId);
                }
            }

            // 添加所有卡片
            foreach (var cardId in cardIds.OrderBy(id => id))
            {
                Cards.Add(new CardInfo
                {
                    CardId = cardId,
                    Description = $"控制卡 {cardId}"
                });
            }

            // 确保至少有一个可选卡片
            if (!Cards.Any())
            {
                Cards.Add(new CardInfo { CardId = 0, Description = "默认控制卡" });
            }
        }

        // 更新插补系中的轴显示
        private void UpdateAxesInSystem()
        {
            if (SelectedSystem == null) return;

            SelectedAxesInSystem.Clear();

            foreach (var axisId in SelectedSystem.Axes)
            {
                // 使用 Split 解析卡号和轴号
                var parts = axisId.Split('-');
                if (parts.Length != 2) continue;

                int setCardId = int.Parse(parts[0]);
                int setAxisId = int.Parse(parts[1]);

                // 使用实际的轴信息（从Axes集合）
                var axisConfig = Axes.FirstOrDefault(a =>
                    a.CardId == setCardId &&
                    a.AxisId == setAxisId);

                if (axisConfig != null)
                {
                    SelectedAxesInSystem.Add(new AxisInSystem
                    {
                        Name = axisConfig.Name,
                        ConfigId = axisConfig.Name, // 或使用实际标志
                        SetCardId = setCardId,
                        SetAxisId = setAxisId
                    });
                }
                else
                {
                    // 配置错误时，添加占位符信息
                    SelectedAxesInSystem.Add(new AxisInSystem
                    {
                        Name = axisId,
                        ConfigId = axisId,
                        SetCardId = setCardId,
                        SetAxisId = setAxisId
                    });
                }
            }
        }

        private void InitializeMappedIOs()
        {
            // 模拟IO映射数据
            for (int i = 0; i < 20; i++)
            {
                MappedIOs.Add(new MappedIO
                {
                    MapIoIndex = (short)i,
                    PortName = $"DI-{i}",
                    Description = $"急停映射端口 {i}",
                    IoType = 3,      // 设置为急停类型
                    MapIoType = 6    // 设置为通用输入端口
                });
            }
        }

        private async void DownloadSelectedAxis()
        {
            if (SelectedAxis == null) return;

            try
            {
                await _configService.DownloadSingleAxisAsync(SelectedAxis);
                MessageBox.Show("参数设置完成!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UploadSelectedAxis()
        {
            if (SelectedAxis == null) return;
            _configService.UploadParameters(SelectedAxis);
        }

        private void SaveParams()
        {
            if (SelectedAxis != null)
            {
                _configService.SaveAxisParameters(SelectedAxis);
                ParametersChanged = false;
            }
        }

        private async void DownloadAllParameters()
        {
            var dialog = new ProgressDialog("设置所有轴参数");

            try
            {
                // 安全设置 Owner: 只有在主窗口已加载且显示时才设置
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

                await _configService.DownloadAllParametersAsync(new ProgressReporterAdapter(dialog));
                ParametersChanged = false;
                dialog.Close();
                MessageBox.Show("所有轴参数设置完成!");
            }
            catch (Exception ex)
            {
                dialog.Close();
                MessageBox.Show($"设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnViewModeChanged()
        {
            if (IsSystemMode && SelectedSystem == null &&
                InterpolationSystems.Any())
            {
                SelectedSystem = InterpolationSystems.First();
            }
        }

        // 打开轴管理对话框
        private void OnManageSystemAxes()
        {
            if (SelectedSystem == null)
            {
                MessageBox.Show("请先选择一个插补系", "提示",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            //// 创建管理对话框并传递当前插补系的轴信息
            //var dialog = new ManageSystemAxesDialog
            //{
            //    DataContext = new ManageSystemAxesViewModel
            //    {
            //        AllAxes = this.Axes.ToList(),
            //        SelectedAxisIds = new ObservableCollection<string>(SelectedSystem.Axes)
            //    }
            //};

            //if (dialog.ShowDialog() == true)
            //{
            //    // 应用选择的轴
            //    SelectedSystem.Axes =
            //        ((ManageSystemAxesViewModel)dialog.DataContext).SelectedAxisIds.ToList();

            //    // 刷新显示
            //    UpdateSelectedAxesInSystem();

            //    MessageBox.Show("插补系配置已更新", "成功",
            //                    MessageBoxButton.OK, MessageBoxImage.Information);
            //}
        }


        private void OnAddSystem()
        {
            // 添加新插补系（最大数量限制）
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
        // 删除插补系
        private void OnDeleteSystem()
        {
            if (SelectedSystem != null)
            {
                // 至少保留一个插补系
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
                    // 找到当前选中项的索引
                    int index = InterpolationSystems.IndexOf(SelectedSystem);
                    InterpolationSystems.Remove(SelectedSystem);
                    // 选择新的当前项
                    if (InterpolationSystems.Count > 0)
                    {
                        if (index >= InterpolationSystems.Count)
                            index = InterpolationSystems.Count - 1;
                        SelectedSystem = InterpolationSystems[index];
                    }
                }
            }
        }
        private void OnApplySystemParams()
        {
            if (SelectedSystem != null)
            {
                try
                {
                    _configService.ApplyInterpolationParameters(SelectedSystem);
                    MessageBox.Show("插补系参数已成功应用到控制卡",
                                   "应用成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"应用失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void OnSaveSystemParams()
        {
            if (SelectedSystem != null)
            {
                try
                {
                    _configService.SaveInterpolationSystem(SelectedSystem);
                    MessageBox.Show($"插补系 {SelectedSystem.CoordId} 配置已保存",
                      "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"应用失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void ExportJson()
        {
            if (SelectedAxis == null) return;

            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON文件 (*.json)|*.json",
                DefaultExt = "json",
                FileName = $"{SelectedAxis.Name}_参数配置.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    string json = JsonConvert.SerializeObject(SelectedAxis.Params, Formatting.Indented);
                    File.WriteAllText(saveFileDialog.FileName, json);

                    MessageBox.Show("参数导出成功", "导出", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void ImportJson()
        {
            if (SelectedAxis == null) return;
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON文件 (*.json)|*.json"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(openFileDialog.FileName);
                    var settings = JsonConvert.DeserializeObject<AxisParams>(json);

                    // 修复急停IO映射对象引用
                    if (settings?.EmergencyStop?.MappedIO != null)
                    {
                        var matchedIO = MappedIOs.FirstOrDefault(io =>
                            io.SetId == settings.EmergencyStop.MappedIO.SetId &&
                            io.PortName == settings.EmergencyStop.MappedIO.PortName);

                        if (matchedIO != null)
                        {
                            settings.EmergencyStop.MappedIO = matchedIO;
                        }
                    }
                    SelectedAxis.Params = settings;
                    CurrentAxisParams = settings;
                    RaisePropertyChanged(nameof(CurrentAxisParams));
                    MessageBox.Show("参数导入成功", "导入", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void LoadFromFile()
        {
            if (SelectedAxis == null) return;
            try
            {
                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "AxisSettings");
                string configPath = Path.Combine(configDir, $"Axis_{SelectedAxis.CardId}_{SelectedAxis.AxisId}.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var settings = JsonConvert.DeserializeObject<AxisParams>(json);

                    // 急停IO映射对象引用
                    if (settings?.EmergencyStop?.MappedIO != null)
                    {
                        var matchedIO = MappedIOs.FirstOrDefault(io =>
                            io.SetId == settings.EmergencyStop.MappedIO.SetId &&
                            io.PortName == settings.EmergencyStop.MappedIO.PortName);

                        if (matchedIO != null)
                        {
                            settings.EmergencyStop.MappedIO = matchedIO;
                        }
                    }
                    SelectedAxis.Params = settings;
                    CurrentAxisParams = settings;
                    RaisePropertyChanged(nameof(CurrentAxisParams));
                    ParametersChanged = true;
                    MessageBox.Show($"参数已从 {configPath} 加载", "加载成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"找不到配置文件: {configPath}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LoadSystemConfigurations()
        {
            string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Systems");

            if (Directory.Exists(configDir))
            {
                foreach (var file in Directory.GetFiles(configDir, "System_*.json"))
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var data = JsonConvert.DeserializeObject<dynamic>(json);

                        int coordId = data.CoordId;
                        if (int.TryParse(Path.GetFileNameWithoutExtension(file).Split('_').Last(), out coordId))
                        {
                            var system = InterpolationSystems.FirstOrDefault(s => s.CoordId == coordId);
                            if (system != null)
                            {
                                system.ActCardId = data.ActCardId;
                                system.Axes = data.Axes?.ToObject<List<string>>() ?? new List<string>();
                                system.Params = data.Params?.ToObject<InterpolationParams>();

                                // 更新UI显示
                                if (SelectedSystem?.CoordId == coordId)
                                {
                                    UpdateAxesInSystem();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"加载插补系配置 {file} 失败: {ex.Message}",
                            "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

    }

    // 适配器类，将ProgressDialog转换为IProgressReporter
    public class ProgressReporterAdapter : IProgressReporter
    {
        private readonly ProgressDialog _dialog;

        public ProgressReporterAdapter(ProgressDialog dialog)
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

// 在View层或独立的文件夹中
public class ProgressDialog : Window, IProgressReporter
{
    private ProgressBar _progressBar;
    private TextBlock _statusText;
    private TextBlock _progressText;

    public ProgressDialog(string title)
    {
        this.Title = title;
        this.Width = 450;
        this.Height = 200;
        this.ResizeMode = ResizeMode.NoResize;

        InitializeComponents();
    }
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);

        // 如果没有设置Owner，确保居中显示
        if (Owner == null)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
    }
    private void InitializeComponents()
    {
        var stackPanel = new StackPanel
        {
            Margin = new Thickness(20),
            VerticalAlignment = VerticalAlignment.Center
        };

        _statusText = new TextBlock
        {
            Text = "准备开始...",
            Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap
        };

        _progressBar = new ProgressBar
        {
            Height = 20,
            Minimum = 0,
            Maximum = 1,
            Value = 0
        };

        _progressText = new TextBlock
        {
            Text = "0%",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 5, 0, 0)
        };

        stackPanel.Children.Add(_statusText);
        stackPanel.Children.Add(_progressBar);
        stackPanel.Children.Add(_progressText);

        this.Content = stackPanel;
    }

    public void SetStatus(string status)
    {
        _statusText.Text = status;
    }

    public void SetProgress(double value)
    {
        _progressBar.Value = value;
        _progressText.Text = $"{value * 100:0}%";
    }

    // IProgressReporter 实现
    public void Report(double progress, string statusMessage = null)
    {
        Dispatcher.Invoke(() =>
        {
            if (!string.IsNullOrEmpty(statusMessage))
            {
                SetStatus(statusMessage);
            }
            SetProgress(progress);
        });
    }
}

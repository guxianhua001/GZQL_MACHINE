using Core.Abstraction;
using Core.Models;
using Core.Services;
using Core.Utilities;
using Framework.Dialogs;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TCPIPModule.Interfaces;

namespace Module.ViewModels
{
    /// <summary>
    /// N点标定页面ViewModel——管理标定配置、点位数据、自动标定流程、TCP视觉数据接收、文件操作
    /// </summary>
    public class NPointCalibrationViewModel : BindableBase
    {
        private readonly INPointCalibrationService _calibService;
        private readonly IPositionMotionController _motionController;
        private readonly IAxisConfigurationService _axisConfigService;
        private readonly ITCPEventService _tcpEventService;
        private readonly ITCPClientManagerService _tcpClientManager;
        private readonly IParameterStorage _parameterStorage;
        private readonly IFileDialogService _fileDialogService;
        private readonly ILocalizationService _localization;
        private readonly ILoggerService _logger;

        /// <summary>默认配置文件路径</summary>
        private static readonly string ConfigDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Config", "Calibration");

        /// <summary>自动标定取消令牌源</summary>
        private CancellationTokenSource? _autoCalibCts;

        #region 属性

        private ObservableCollection<string> _stationIdentifiers = new() { "DispenserStation", "LoadingStation", "AssemblyStation" };
        /// <summary>可选工站标识列表</summary>
        public ObservableCollection<string> StationIdentifiers { get => _stationIdentifiers; set => SetProperty(ref _stationIdentifiers, value); }

        private string _selectedStationIdentifier = "DispenserStation";
        /// <summary>选中的工站标识</summary>
        public string SelectedStationIdentifier
        {
            get => _selectedStationIdentifier;
            set
            {
                if (SetProperty(ref _selectedStationIdentifier, value))
                    OnStationChanged();
            }
        }

        private ObservableCollection<string> _availableAxes = new();
        /// <summary>当前工站可用轴名列表</summary>
        public ObservableCollection<string> AvailableAxes { get => _availableAxes; set => SetProperty(ref _availableAxes, value); }

        private string _axisNameX = "X";
        /// <summary>X轴名称</summary>
        public string AxisNameX { get => _axisNameX; set => SetProperty(ref _axisNameX, value); }

        private string _axisNameY = "Y";
        /// <summary>Y轴名称</summary>
        public string AxisNameY { get => _axisNameY; set => SetProperty(ref _axisNameY, value); }

        private bool _enableAxisX = true;
        /// <summary>启用X轴</summary>
        public bool EnableAxisX { get => _enableAxisX; set => SetProperty(ref _enableAxisX, value); }

        private bool _enableAxisY = true;
        /// <summary>启用Y轴</summary>
        public bool EnableAxisY { get => _enableAxisY; set => SetProperty(ref _enableAxisY, value); }

        private int _pointCount = 9;
        /// <summary>标定点数</summary>
        public int PointCount
        {
            get => _pointCount;
            set
            {
                if (SetProperty(ref _pointCount, value))
                    UpdatePointsCollection();
            }
        }

        private bool _enableVisionData = true;
        /// <summary>接收视觉数据</summary>
        public bool EnableVisionData { get => _enableVisionData; set => SetProperty(ref _enableVisionData, value); }

        private ObservableCollection<string> _tcpConnections = new();
        /// <summary>TCP连接名列表</summary>
        public ObservableCollection<string> TcpConnections { get => _tcpConnections; set => SetProperty(ref _tcpConnections, value); }

        private string _selectedTcpConnection = string.Empty;
        /// <summary>选中的TCP连接名</summary>
        public string SelectedTcpConnection { get => _selectedTcpConnection; set => SetProperty(ref _selectedTcpConnection, value); }

        private string _triggerCommand = string.Empty;
        /// <summary>触发视觉拍照命令</summary>
        public string TriggerCommand { get => _triggerCommand; set => SetProperty(ref _triggerCommand, value); }

        private int _autoCalibDelayMs = 500;
        /// <summary>自动标定延时(ms)</summary>
        public int AutoCalibDelayMs { get => _autoCalibDelayMs; set => SetProperty(ref _autoCalibDelayMs, value); }

        private bool _isAutoCalibrating;
        /// <summary>是否正在自动标定</summary>
        public bool IsAutoCalibrating
        {
            get => _isAutoCalibrating;
            set
            {
                if (SetProperty(ref _isAutoCalibrating, value))
                {
                    StartAutoCalibCommand.RaiseCanExecuteChanged();
                    StopAutoCalibCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private ObservableCollection<NPointCalibrationPoint> _points = new();
        /// <summary>标定点集合</summary>
        public ObservableCollection<NPointCalibrationPoint> Points { get => _points; set => SetProperty(ref _points, value); }

        private AffineCalibrationResult? _calibrationResult;
        /// <summary>仿射标定结果</summary>
        public AffineCalibrationResult? CalibrationResult
        {
            get => _calibrationResult;
            set => SetProperty(ref _calibrationResult, value);
        }

        private string _currentFileName = string.Empty;
        /// <summary>当前加载的文件名（仅文件名，不含路径）</summary>
        public string CurrentFileName
        {
            get => _currentFileName;
            set => SetProperty(ref _currentFileName, value);
        }

        private string _statusText = string.Empty;
        /// <summary>状态栏文本</summary>
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        private Brush _statusColor = Brushes.LightGray;
        /// <summary>状态栏颜色</summary>
        public Brush StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }

        #endregion

        #region 命令

        public DelegateCommand StartAutoCalibCommand { get; }
        public DelegateCommand StopAutoCalibCommand { get; }
        public DelegateCommand<NPointCalibrationPoint> TeachPointCommand { get; }
        public DelegateCommand<NPointCalibrationPoint> MoveToPointCommand { get; }
        public DelegateCommand<NPointCalibrationPoint> DeletePointCommand { get; }
        public DelegateCommand AddPointCommand { get; }
        public DelegateCommand ComputeCalibrationCommand { get; }
        public DelegateCommand SaveConfigCommand { get; }
        public DelegateCommand SaveAsConfigCommand { get; }
        public DelegateCommand ImportConfigCommand { get; }
        public DelegateCommand ExportConfigCommand { get; }

        #endregion

        public NPointCalibrationViewModel(
            INPointCalibrationService calibService,
            IPositionMotionController motionController,
            IAxisConfigurationService axisConfigService,
            ITCPEventService tcpEventService,
            ITCPClientManagerService tcpClientManager,
            IParameterStorage parameterStorage,
            IFileDialogService fileDialogService,
            ILocalizationService localization,
            ILoggerService logger)
        {
            _calibService = calibService;
            _motionController = motionController;
            _axisConfigService = axisConfigService;
            _tcpEventService = tcpEventService;
            _tcpClientManager = tcpClientManager;
            _parameterStorage = parameterStorage;
            _fileDialogService = fileDialogService;
            _localization = localization;
            _logger = logger;

            // 初始化命令
            StartAutoCalibCommand = new DelegateCommand(ExecuteStartAutoCalib, () => !IsAutoCalibrating);
            StopAutoCalibCommand = new DelegateCommand(ExecuteStopAutoCalib, () => IsAutoCalibrating);
            TeachPointCommand = new DelegateCommand<NPointCalibrationPoint>(async p => await ExecuteTeachPointAsync(p));
            MoveToPointCommand = new DelegateCommand<NPointCalibrationPoint>(async p => await ExecuteMoveToPointAsync(p));
            DeletePointCommand = new DelegateCommand<NPointCalibrationPoint>(ExecuteDeletePoint);
            AddPointCommand = new DelegateCommand(ExecuteAddPoint);
            ComputeCalibrationCommand = new DelegateCommand(ExecuteComputeCalibration, () => Points.Count(p => p.IsCalibrated) >= 3);
            SaveConfigCommand = new DelegateCommand(async () => await ExecuteSaveConfigAsync());
            SaveAsConfigCommand = new DelegateCommand(async () => await ExecuteSaveAsConfigAsync());
            ImportConfigCommand = new DelegateCommand(async () => await ExecuteImportConfigAsync());
            ExportConfigCommand = new DelegateCommand(async () => await ExecuteExportConfigAsync());

            // 订阅服务事件
            _calibService.PointCalibrated += OnPointCalibrated;
            _calibService.VisionDataReceived += OnVisionDataReceived;
            _calibService.CalibrationCompleted += OnCalibrationCompleted;
            _calibService.CalibrationError += OnCalibrationError;

            // 初始化点位
            UpdatePointsCollection();

            // 异步初始化
            _ = InitializeAsync();
        }

        /// <summary>初始化：加载轴列表、TCP连接列表，自动加载上次配置</summary>
        private async Task InitializeAsync()
        {
            RefreshAvailableAxes();
            await LoadTcpConnectionsAsync();
            await TryAutoLoadConfigAsync();
            UpdateStatus(L("NPointCalib_Idle", "空闲"), Brushes.LightGray);
        }

        /// <summary>工站切换时刷新可用轴列表</summary>
        private void OnStationChanged()
        {
            RefreshAvailableAxes();
            // 重置轴名选择
            AxisNameX = AvailableAxes.FirstOrDefault() ?? "X";
            AxisNameY = AvailableAxes.Skip(1).FirstOrDefault() ?? "Y";
        }

        /// <summary>从IAxisConfigurationService获取当前工站的可用轴名</summary>
        private void RefreshAvailableAxes()
        {
            try
            {
                var axes = _axisConfigService.GetAxesForStation(SelectedStationIdentifier);
                AvailableAxes = new ObservableCollection<string>(axes.Select(a => a.Name));

                if (AvailableAxes.Count == 0)
                {
                    _logger.Warn($"N点标定: 工站 {SelectedStationIdentifier} 无可用轴");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"N点标定: 获取工站 {SelectedStationIdentifier} 轴列表失败 - {ex.Message}");
                AvailableAxes = new ObservableCollection<string>();
            }
        }

        #region 自动标定

        private async void ExecuteStartAutoCalib()
        {
            if (IsAutoCalibrating) return;

            IsAutoCalibrating = true;
            _autoCalibCts = new CancellationTokenSource();

            try
            {
                UpdateStatus(L("NPointCalib_Calibrating", "标定中..."), Brushes.Orange);

                await _calibService.StartAutoCalibrationAsync(
                    Points.ToList(),
                    AutoCalibDelayMs,
                    EnableVisionData,
                    SelectedTcpConnection,
                    TriggerCommand,
                    SelectedStationIdentifier,
                    AxisNameX,
                    AxisNameY,
                    EnableAxisX,
                    EnableAxisY,
                    _autoCalibCts.Token);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "N点标定: 自动标定启动失败");
                UpdateStatus($"{L("NPointCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        private void ExecuteStopAutoCalib()
        {
            _calibService.StopAutoCalibration();
            _autoCalibCts?.Cancel();
            IsAutoCalibrating = false;
            UpdateStatus(L("NPointCalib_Idle", "空闲"), Brushes.LightGray);
        }

        #endregion

        #region 单点操作

        private async Task ExecuteTeachPointAsync(NPointCalibrationPoint? point)
        {
            if (point == null) return;
            try
            {
                var result = await _calibService.TeachPointAsync(SelectedStationIdentifier, AxisNameX, AxisNameY, EnableAxisX, EnableAxisY);
                point.MachineX = result.MachineX;
                point.MachineY = result.MachineY;
                point.IsCalibrated = true;
                ComputeCalibrationCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "N点标定: 示教失败");
                UpdateStatus($"{L("NPointCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        private async Task ExecuteMoveToPointAsync(NPointCalibrationPoint? point)
        {
            if (point == null) return;
            try
            {
                await _calibService.MoveToPointAsync(point, SelectedStationIdentifier, AxisNameX, AxisNameY, EnableAxisX, EnableAxisY);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "N点标定: 移动失败");
                UpdateStatus($"{L("NPointCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        private void ExecuteDeletePoint(NPointCalibrationPoint? point)
        {
            if (point == null) return;
            Points.Remove(point);
            // 重新编号
            for (int i = 0; i < Points.Count; i++)
            {
                Points[i].Index = i + 1;
                Points[i].Name = $"P{i + 1}";
            }
            ComputeCalibrationCommand.RaiseCanExecuteChanged();
        }

        private void ExecuteAddPoint()
        {
            var newPoint = new NPointCalibrationPoint
            {
                Index = Points.Count + 1,
                Name = $"P{Points.Count + 1}"
            };
            Points.Add(newPoint);
            PointCount = Points.Count;
            ComputeCalibrationCommand.RaiseCanExecuteChanged();
        }

        #endregion

        #region 仿射计算

        private void ExecuteComputeCalibration()
        {
            try
            {
                var calibratedPoints = Points.Where(p => p.IsCalibrated).ToList();
                if (calibratedPoints.Count < 3)
                {
                    UpdateStatus(L("NPointCalib_MinPointsRequired", "标定至少需要3个点"), Brushes.Orange);
                    return;
                }

                var result = _calibService.ComputeCalibration(calibratedPoints);
                CalibrationResult = result;
                UpdateStatus($"{L("NPointCalib_Completed", "标定完成")} - RMS: {result.RmsError:F6}",
                    result.RmsError < 0.05 ? Brushes.LightGreen : Brushes.Orange);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "N点标定: 计算失败");
                UpdateStatus($"{L("NPointCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        #endregion

        #region 文件操作

        /// <summary>保存配置到当前文件（或默认路径）</summary>
        private async Task ExecuteSaveConfigAsync()
        {
            try
            {
                var data = BuildCurrentData();
                Directory.CreateDirectory(ConfigDirectory);

                var fileName = CurrentFileName;
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = $"Calibration_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                }

                var filePath = Path.Combine(ConfigDirectory, fileName);
                await Task.Run(() =>
                {
                    var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                });

                data.Config.LastFileName = fileName;
                CurrentFileName = fileName;

                _parameterStorage.Save("NPointCalibration_Default", data.Config, ConfigDirectory);

                UpdateStatus($"{L("NPointCalib_SaveSuccess", "保存成功")}: {fileName}", Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "N点标定: 保存失败");
                UpdateStatus($"{L("NPointCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>另存为</summary>
        private async Task ExecuteSaveAsConfigAsync()
        {
            try
            {
                var defaultName = $"Calibration_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = _fileDialogService.ShowSaveFileDialog(
                    filter: "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    title: L("NPointCalib_SaveAs", "另存为"),
                    defaultFileName: defaultName);

                if (string.IsNullOrEmpty(filePath)) return;

                var data = BuildCurrentData();
                await Task.Run(() =>
                {
                    var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                });

                CurrentFileName = Path.GetFileName(filePath);
                data.Config.LastFileName = CurrentFileName;

                _parameterStorage.Save("NPointCalibration_Default", data.Config, ConfigDirectory);

                UpdateStatus($"{L("NPointCalib_SaveSuccess", "保存成功")}: {CurrentFileName}", Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "N点标定: 另存为失败");
                UpdateStatus($"{L("NPointCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>导入配置</summary>
        private async Task ExecuteImportConfigAsync()
        {
            try
            {
                var filePath = _fileDialogService.ShowOpenFileDialog(
                    filter: "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    title: L("NPointCalib_Import", "导入"),
                    initialDirectory: Directory.Exists(ConfigDirectory) ? ConfigDirectory : null);

                if (string.IsNullOrEmpty(filePath)) return;

                await LoadFromFileAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "N点标定: 导入失败");
                UpdateStatus($"{L("NPointCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>导出配置</summary>
        private async Task ExecuteExportConfigAsync()
        {
            try
            {
                var defaultName = $"Calibration_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = _fileDialogService.ShowSaveFileDialog(
                    filter: "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    title: L("NPointCalib_Export", "导出"),
                    defaultFileName: defaultName);

                if (string.IsNullOrEmpty(filePath)) return;

                var data = BuildCurrentData();
                await Task.Run(() =>
                {
                    var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                });

                UpdateStatus($"{L("NPointCalib_SaveSuccess", "保存成功")}: {Path.GetFileName(filePath)}", Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "N点标定: 导出失败");
                UpdateStatus($"{L("NPointCalib_Error", "标定错误")}: {ex.Message}", Brushes.Red);
            }
        }

        #endregion

        #region 自动加载

        /// <summary>尝试自动加载上次使用的配置文件</summary>
        private async Task TryAutoLoadConfigAsync()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);

                var defaultConfig = _parameterStorage.Load<NPointCalibrationConfig>(
                    "NPointCalibration_Default", ConfigDirectory);

                if (defaultConfig != null && !string.IsNullOrEmpty(defaultConfig.LastFileName))
                {
                    var filePath = Path.Combine(ConfigDirectory, defaultConfig.LastFileName);
                    if (File.Exists(filePath))
                    {
                        await LoadFromFileAsync(filePath);
                        return;
                    }
                }

                if (Directory.Exists(ConfigDirectory))
                {
                    var latestFile = Directory.GetFiles(ConfigDirectory, "Calibration_*.json")
                        .OrderByDescending(File.GetLastWriteTime)
                        .FirstOrDefault();

                    if (latestFile != null)
                    {
                        await LoadFromFileAsync(latestFile);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"N点标定: 自动加载失败 - {ex.Message}");
            }
        }

        /// <summary>从文件加载标定数据</summary>
        private async Task LoadFromFileAsync(string filePath)
        {
            var data = await Task.Run(() =>
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<NPointCalibrationData>(json);
            });

            if (data == null) return;

            // 应用配置
            SelectedStationIdentifier = data.Config.StationIdentifier;
            AxisNameX = data.Config.AxisNameX;
            AxisNameY = data.Config.AxisNameY;
            EnableAxisX = data.Config.EnableAxisX;
            EnableAxisY = data.Config.EnableAxisY;
            PointCount = data.Config.PointCount;
            EnableVisionData = data.Config.EnableVisionData;
            SelectedTcpConnection = data.Config.TcpConnectionName;
            TriggerCommand = data.Config.TriggerCommand;
            AutoCalibDelayMs = data.Config.AutoCalibDelayMs;

            // 应用点位数据
            Points.Clear();
            foreach (var point in data.Points)
            {
                Points.Add(point);
            }

            // 应用标定结果
            CalibrationResult = data.CalibrationResult;

            // 更新文件名（仅显示文件名）
            CurrentFileName = Path.GetFileName(filePath);

            UpdateStatus($"{L("NPointCalib_LoadSuccess", "加载成功")}: {CurrentFileName}", Brushes.LightGreen);
        }

        #endregion

        #region TCP连接

        /// <summary>加载TCP连接名列表</summary>
        private async Task LoadTcpConnectionsAsync()
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    TcpConnections.Clear();

                    if (_tcpClientManager?.Clients != null)
                    {
                        foreach (var kvp in _tcpClientManager.Clients)
                        {
                            TcpConnections.Add(kvp.Key);
                        }
                    }

                    var serverNames = _tcpEventService?.GetServerNames();
                    if (serverNames != null)
                    {
                        foreach (var name in serverNames)
                        {
                            if (!TcpConnections.Contains(name))
                                TcpConnections.Add(name);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Warn($"N点标定: 加载TCP连接列表失败 - {ex.Message}");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>根据PointCount更新Points集合</summary>
        private void UpdatePointsCollection()
        {
            while (Points.Count < PointCount)
            {
                Points.Add(new NPointCalibrationPoint
                {
                    Index = Points.Count + 1,
                    Name = $"P{Points.Count + 1}"
                });
            }
            while (Points.Count > PointCount && PointCount >= 1)
            {
                Points.RemoveAt(Points.Count - 1);
            }
            ComputeCalibrationCommand.RaiseCanExecuteChanged();
        }

        /// <summary>构建当前标定数据对象</summary>
        private NPointCalibrationData BuildCurrentData()
        {
            return new NPointCalibrationData
            {
                Config = new NPointCalibrationConfig
                {
                    StationIdentifier = SelectedStationIdentifier,
                    AxisNameX = AxisNameX,
                    AxisNameY = AxisNameY,
                    EnableAxisX = EnableAxisX,
                    EnableAxisY = EnableAxisY,
                    PointCount = PointCount,
                    EnableVisionData = EnableVisionData,
                    TcpConnectionName = SelectedTcpConnection,
                    TriggerCommand = TriggerCommand,
                    AutoCalibDelayMs = AutoCalibDelayMs,
                    LastFileName = CurrentFileName
                },
                Points = Points.ToList(),
                CalibrationResult = CalibrationResult
            };
        }

        /// <summary>更新状态栏</summary>
        private void UpdateStatus(string text, Brush color)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                StatusText = text;
                StatusColor = color;
            });
        }

        /// <summary>获取本地化字符串</summary>
        private string L(string key, string defaultValue = "")
        {
            return _localization?.GetResourceOrDefault(key, defaultValue) ?? defaultValue;
        }

        #endregion

        #region 服务事件处理

        private void OnPointCalibrated(int index, NPointCalibrationPoint point)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (index >= 0 && index < Points.Count)
                {
                    Points[index].MachineX = point.MachineX;
                    Points[index].MachineY = point.MachineY;
                    Points[index].IsCalibrated = true;
                }
                ComputeCalibrationCommand.RaiseCanExecuteChanged();
                UpdateStatus(string.Format(L("NPointCalib_PointCalibrated", "点位 {0} 标定完成"), point.Name),
                    Brushes.LightGreen);
            });
        }

        private void OnVisionDataReceived(NPointCalibrationPoint point)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                UpdateStatus(L("NPointCalib_VisionDataReceived", "视觉数据已接收"), Brushes.LightGreen);
            });
        }

        private void OnCalibrationCompleted(AffineCalibrationResult result)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                CalibrationResult = result;
                IsAutoCalibrating = false;
                UpdateStatus($"{L("NPointCalib_Completed", "标定完成")} - RMS: {result.RmsError:F6}",
                    result.RmsError < 0.05 ? Brushes.LightGreen : Brushes.Orange);
            });
        }

        private void OnCalibrationError(string error)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsAutoCalibrating = false;
                UpdateStatus($"{L("NPointCalib_Error", "标定错误")}: {error}", Brushes.Red);
            });
        }

        #endregion
    }
}

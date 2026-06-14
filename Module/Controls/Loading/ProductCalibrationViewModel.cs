using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using Framework.Dialogs;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Recipe.Interfaces;
using Recipe.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TCPIPModule.Interfaces;

namespace Module.ViewModels
{
    /// <summary>
    /// 载台校准ViewModel——管理基准位移动、两次拍照位、偏差计算、全局变量链接、旋转校正、参数加载保存
    /// 流程：1.载台Rx/Rz移到基准位 → 2.相机移到拍照位1拍照 → 3.相机移到拍照位2拍照 → 4.计算deltaX/deltaY/角度 → 5.旋转校正
    /// </summary>
    public class ProductCalibrationViewModel : BindableBase
    {
        private readonly IStageCalibrationService _calibService;
        private readonly ITCPEventService _tcpEventService;
        private readonly ITCPClientManagerService _tcpClientManager;
        private readonly IParameterStorage _parameterStorage;
        private readonly IFileDialogService _fileDialogService;
        private readonly ILocalizationService _localization;
        private readonly ILoggerService _logger;
        private readonly IEventAggregator _eventAggregator;
        private readonly IRecipePoolService? _recipePoolService;

        /// <summary>默认配置文件路径</summary>
        private static readonly string ConfigDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Config", "StageCalibration");

        #region 属性 — 基准位

        private double _refRx;
        /// <summary>基准位Rx角度</summary>
        public double RefRx { get => _refRx; set => SetProperty(ref _refRx, value); }

        private double _refRz;
        /// <summary>基准位Rz角度</summary>
        public double RefRz { get => _refRz; set => SetProperty(ref _refRz, value); }

        #endregion

        #region 属性 — 拍照位1

        private double _photo1Dx;
        public double Photo1Dx { get => _photo1Dx; set => SetProperty(ref _photo1Dx, value); }

        private double _photo1Dy;
        public double Photo1Dy { get => _photo1Dy; set => SetProperty(ref _photo1Dy, value); }

        private double _photo1Dz;
        public double Photo1Dz { get => _photo1Dz; set => SetProperty(ref _photo1Dz, value); }

        private double _photo1VisionX;
        /// <summary>拍照位1视觉返回X</summary>
        public double Photo1VisionX { get => _photo1VisionX; set => SetProperty(ref _photo1VisionX, value); }

        private double _photo1VisionY;
        /// <summary>拍照位1视觉返回Y</summary>
        public double Photo1VisionY { get => _photo1VisionY; set => SetProperty(ref _photo1VisionY, value); }

        private bool _photo1Captured;
        /// <summary>拍照位1是否已拍照</summary>
        public bool Photo1Captured { get => _photo1Captured; set => SetProperty(ref _photo1Captured, value); }

        #endregion

        #region 属性 — 拍照位2

        private double _photo2Dx;
        public double Photo2Dx { get => _photo2Dx; set => SetProperty(ref _photo2Dx, value); }

        private double _photo2Dy;
        public double Photo2Dy { get => _photo2Dy; set => SetProperty(ref _photo2Dy, value); }

        private double _photo2Dz;
        public double Photo2Dz { get => _photo2Dz; set => SetProperty(ref _photo2Dz, value); }

        private double _photo2VisionX;
        public double Photo2VisionX { get => _photo2VisionX; set => SetProperty(ref _photo2VisionX, value); }

        private double _photo2VisionY;
        public double Photo2VisionY { get => _photo2VisionY; set => SetProperty(ref _photo2VisionY, value); }

        private bool _photo2Captured;
        public bool Photo2Captured { get => _photo2Captured; set => SetProperty(ref _photo2Captured, value); }

        #endregion

        #region 属性 — 偏差结果

        private double _deltaX;
        /// <summary>X偏差（拍照位2机械坐标 - 拍照位1机械坐标）</summary>
        public double DeltaX { get => _deltaX; set => SetProperty(ref _deltaX, value); }

        private double _deltaY;
        /// <summary>Y偏差</summary>
        public double DeltaY { get => _deltaY; set => SetProperty(ref _deltaY, value); }

        private double _deltaAngle;
        /// <summary>角度偏差（由两次拍照位视觉结果计算）</summary>
        public double DeltaAngle { get => _deltaAngle; set => SetProperty(ref _deltaAngle, value); }

        private double _offsetX;
        /// <summary>相机与基准点的X偏差</summary>
        public double OffsetX { get => _offsetX; set => SetProperty(ref _offsetX, value); }

        private double _offsetY;
        /// <summary>相机与基准点的Y偏差</summary>
        public double OffsetY { get => _offsetY; set => SetProperty(ref _offsetY, value); }

        #endregion

        #region 属性 — TCP配置

        private ObservableCollection<string> _tcpConnections = new();
        public ObservableCollection<string> TcpConnections { get => _tcpConnections; set => SetProperty(ref _tcpConnections, value); }

        private string _selectedTcpConnection = string.Empty;
        public string SelectedTcpConnection { get => _selectedTcpConnection; set => SetProperty(ref _selectedTcpConnection, value); }

        private string _triggerCommand = string.Empty;
        public string TriggerCommand { get => _triggerCommand; set => SetProperty(ref _triggerCommand, value); }

        private int _captureTimeoutMs = 5000;
        public int CaptureTimeoutMs { get => _captureTimeoutMs; set => SetProperty(ref _captureTimeoutMs, value); }

        #endregion

        #region 属性 — 全局变量链接

        private ObservableCollection<GlobalVariable> _linkableGlobalVariables = new();
        public ObservableCollection<GlobalVariable> LinkableGlobalVariables
        {
            get => _linkableGlobalVariables;
            set => SetProperty(ref _linkableGlobalVariables, value);
        }

        private string _deltaXLinkedVar = string.Empty;
        public string DeltaXLinkedVar
        {
            get => _deltaXLinkedVar;
            set { SetProperty(ref _deltaXLinkedVar, value); RaisePropertyChanged(nameof(IsDeltaXLinked)); }
        }

        private string _deltaYLinkedVar = string.Empty;
        public string DeltaYLinkedVar
        {
            get => _deltaYLinkedVar;
            set { SetProperty(ref _deltaYLinkedVar, value); RaisePropertyChanged(nameof(IsDeltaYLinked)); }
        }

        private string _deltaAngleLinkedVar = string.Empty;
        public string DeltaAngleLinkedVar
        {
            get => _deltaAngleLinkedVar;
            set { SetProperty(ref _deltaAngleLinkedVar, value); RaisePropertyChanged(nameof(IsDeltaAngleLinked)); }
        }

        public bool IsDeltaXLinked => !string.IsNullOrEmpty(DeltaXLinkedVar);
        public bool IsDeltaYLinked => !string.IsNullOrEmpty(DeltaYLinkedVar);
        public bool IsDeltaAngleLinked => !string.IsNullOrEmpty(DeltaAngleLinkedVar);

        #endregion

        #region 属性 — 文件操作

        private string _currentFileName = string.Empty;
        public string CurrentFileName { get => _currentFileName; set => SetProperty(ref _currentFileName, value); }

        private string _statusText = string.Empty;
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        private Brush _statusColor = Brushes.LightGray;
        public Brush StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }

        #endregion

        #region 命令

        public DelegateCommand MoveToReferenceCommand { get; }
        public DelegateCommand TeachReferenceCommand { get; }
        public DelegateCommand MoveToPhoto1Command { get; }
        public DelegateCommand TeachPhoto1Command { get; }
        public DelegateCommand Capture1Command { get; }
        public DelegateCommand MoveToPhoto2Command { get; }
        public DelegateCommand TeachPhoto2Command { get; }
        public DelegateCommand Capture2Command { get; }
        public DelegateCommand RotateCommand { get; }
        public DelegateCommand SaveConfigCommand { get; }
        public DelegateCommand LoadConfigCommand { get; }
        public DelegateCommand ImportConfigCommand { get; }
        public DelegateCommand ExportConfigCommand { get; }
        public DelegateCommand UnlinkDeltaXCommand { get; }
        public DelegateCommand UnlinkDeltaYCommand { get; }
        public DelegateCommand UnlinkDeltaAngleCommand { get; }

        #endregion

        public ProductCalibrationViewModel(
            IStageCalibrationService calibService,
            ITCPEventService tcpEventService,
            ITCPClientManagerService tcpClientManager,
            IParameterStorage parameterStorage,
            IFileDialogService fileDialogService,
            ILocalizationService localization,
            ILoggerService logger,
            IEventAggregator eventAggregator)
        {
            _calibService = calibService;
            _tcpEventService = tcpEventService;
            _tcpClientManager = tcpClientManager;
            _parameterStorage = parameterStorage;
            _fileDialogService = fileDialogService;
            _localization = localization;
            _logger = logger;
            _eventAggregator = eventAggregator;

            // 命令初始化
            MoveToReferenceCommand = new DelegateCommand(async () => await ExecuteMoveToReferenceAsync());
            TeachReferenceCommand = new DelegateCommand(async () => await ExecuteTeachReferenceAsync());
            MoveToPhoto1Command = new DelegateCommand(async () => await ExecuteMoveToPhoto1Async());
            TeachPhoto1Command = new DelegateCommand(async () => await ExecuteTeachPhoto1Async());
            Capture1Command = new DelegateCommand(async () => await ExecuteCapture1Async());
            MoveToPhoto2Command = new DelegateCommand(async () => await ExecuteMoveToPhoto2Async());
            TeachPhoto2Command = new DelegateCommand(async () => await ExecuteTeachPhoto2Async());
            Capture2Command = new DelegateCommand(async () => await ExecuteCapture2Async());
            RotateCommand = new DelegateCommand(async () => await ExecuteRotateAsync(), () => Photo1Captured && Photo2Captured);
            SaveConfigCommand = new DelegateCommand(async () => await ExecuteSaveConfigAsync());
            LoadConfigCommand = new DelegateCommand(async () => await ExecuteLoadConfigAsync());
            ImportConfigCommand = new DelegateCommand(async () => await ExecuteImportConfigAsync());
            ExportConfigCommand = new DelegateCommand(async () => await ExecuteExportConfigAsync());
            UnlinkDeltaXCommand = new DelegateCommand(() => DeltaXLinkedVar = null);
            UnlinkDeltaYCommand = new DelegateCommand(() => DeltaYLinkedVar = null);
            UnlinkDeltaAngleCommand = new DelegateCommand(() => DeltaAngleLinkedVar = null);

            // 初始化
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadTcpConnectionsAsync();
            await LoadGlobalVariablesAsync();
            await TryAutoLoadConfigAsync();
            UpdateStatus(L("ProductCalib_Idle", "空闲"), Brushes.LightGray);
        }

        #region 基准位操作

        /// <summary>移动载台Rx/Rz到基准位</summary>
        private async Task ExecuteMoveToReferenceAsync()
        {
            try
            {
                UpdateStatus(L("ProductCalib_MovingToRef", "移动到基准位..."), Brushes.Orange);
                await _calibService.MoveToReferencePositionAsync(RefRx, RefRz);
                UpdateStatus(L("ProductCalib_ArrivedAtRef", "已到达基准位"), Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "载台校准: 移动到基准位失败");
                UpdateStatus($"{L("ProductCalib_Error", "错误")}: {ex.Message}", Brushes.Red);
            }
        }

        /// <summary>示教当前Rx/Rz为基准位</summary>
        private async Task ExecuteTeachReferenceAsync()
        {
            try
            {
                var pos = await _calibService.ReadCurrentPositionsAsync();
                RefRx = pos.Rx;
                RefRz = pos.Rz;
                UpdateStatus(L("ProductCalib_TeachRefDone", "基准位示教完成"), Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "载台校准: 示教基准位失败");
                UpdateStatus($"{L("ProductCalib_Error", "错误")}: {ex.Message}", Brushes.Red);
            }
        }

        #endregion

        #region 拍照位1操作

        private async Task ExecuteMoveToPhoto1Async()
        {
            try
            {
                UpdateStatus(L("ProductCalib_MovingToPhoto1", "移动到拍照位1..."), Brushes.Orange);
                await _calibService.MoveCameraToPhotoPositionAsync(Photo1Dx, Photo1Dy, Photo1Dz);
                UpdateStatus(L("ProductCalib_ArrivedAtPhoto1", "已到达拍照位1"), Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "载台校准: 移动到拍照位1失败");
                UpdateStatus($"{L("ProductCalib_Error", "错误")}: {ex.Message}", Brushes.Red);
            }
        }

        private async Task ExecuteTeachPhoto1Async()
        {
            try
            {
                var pos = await _calibService.ReadCurrentPositionsAsync();
                Photo1Dx = pos.Dx;
                Photo1Dy = pos.Dy;
                Photo1Dz = pos.Dz;
                UpdateStatus(L("ProductCalib_TeachPhoto1Done", "拍照位1示教完成"), Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "载台校准: 示教拍照位1失败");
                UpdateStatus($"{L("ProductCalib_Error", "错误")}: {ex.Message}", Brushes.Red);
            }
        }

        private async Task ExecuteCapture1Async()
        {
            try
            {
                UpdateStatus(L("ProductCalib_Capturing1", "拍照位1拍照中..."), Brushes.Orange);
                var result = await _calibService.TriggerCaptureAsync(SelectedTcpConnection, TriggerCommand, CaptureTimeoutMs);
                if (result.Success)
                {
                    Photo1VisionX = result.X;
                    Photo1VisionY = result.Y;
                    Photo1Captured = true;
                    RotateCommand.RaiseCanExecuteChanged();
                    UpdateStatus(L("ProductCalib_Capture1Done", "拍照位1拍照完成"), Brushes.LightGreen);
                }
                else
                {
                    UpdateStatus($"{L("ProductCalib_CaptureFailed", "拍照失败")}: {result.ErrorMessage}", Brushes.Red);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "载台校准: 拍照位1拍照失败");
                UpdateStatus($"{L("ProductCalib_Error", "错误")}: {ex.Message}", Brushes.Red);
            }
        }

        #endregion

        #region 拍照位2操作

        private async Task ExecuteMoveToPhoto2Async()
        {
            try
            {
                UpdateStatus(L("ProductCalib_MovingToPhoto2", "移动到拍照位2..."), Brushes.Orange);
                await _calibService.MoveCameraToPhotoPositionAsync(Photo2Dx, Photo2Dy, Photo2Dz);
                UpdateStatus(L("ProductCalib_ArrivedAtPhoto2", "已到达拍照位2"), Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "载台校准: 移动到拍照位2失败");
                UpdateStatus($"{L("ProductCalib_Error", "错误")}: {ex.Message}", Brushes.Red);
            }
        }

        private async Task ExecuteTeachPhoto2Async()
        {
            try
            {
                var pos = await _calibService.ReadCurrentPositionsAsync();
                Photo2Dx = pos.Dx;
                Photo2Dy = pos.Dy;
                Photo2Dz = pos.Dz;
                UpdateStatus(L("ProductCalib_TeachPhoto2Done", "拍照位2示教完成"), Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "载台校准: 示教拍照位2失败");
                UpdateStatus($"{L("ProductCalib_Error", "错误")}: {ex.Message}", Brushes.Red);
            }
        }

        private async Task ExecuteCapture2Async()
        {
            try
            {
                UpdateStatus(L("ProductCalib_Capturing2", "拍照位2拍照中..."), Brushes.Orange);
                var result = await _calibService.TriggerCaptureAsync(SelectedTcpConnection, TriggerCommand, CaptureTimeoutMs);
                if (result.Success)
                {
                    Photo2VisionX = result.X;
                    Photo2VisionY = result.Y;
                    Photo2Captured = true;
                    RotateCommand.RaiseCanExecuteChanged();

                    // 计算偏差
                    CalculateDeviations();

                    // 写入全局变量
                    await WriteToGlobalVariablesAsync();

                    UpdateStatus(L("ProductCalib_Capture2Done", "拍照位2拍照完成，偏差已计算"), Brushes.LightGreen);
                }
                else
                {
                    UpdateStatus($"{L("ProductCalib_CaptureFailed", "拍照失败")}: {result.ErrorMessage}", Brushes.Red);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "载台校准: 拍照位2拍照失败");
                UpdateStatus($"{L("ProductCalib_Error", "错误")}: {ex.Message}", Brushes.Red);
            }
        }

        #endregion

        #region 偏差计算

        /// <summary>计算两次拍照的机械偏差和角度偏差</summary>
        private void CalculateDeviations()
        {
            // 机械偏差：拍照位2 - 拍照位1
            DeltaX = Photo2Dx - Photo1Dx;
            DeltaY = Photo2Dy - Photo1Dy;

            // 视觉偏差：用于计算角度偏差
            var visionDx = Photo2VisionX - Photo1VisionX;
            var visionDy = Photo2VisionY - Photo1VisionY;

            // 计算角度偏差（atan2）
            DeltaAngle = Math.Atan2(visionDy, visionDx) * 180.0 / Math.PI;

            // 相机与基准点的偏差
            OffsetX = Photo1VisionX;
            OffsetY = Photo1VisionY;
        }

        #endregion

        #region 旋转校正

        /// <summary>旋转Rz轴到基准角度（当前角度+偏差角度）</summary>
        private async Task ExecuteRotateAsync()
        {
            try
            {
                UpdateStatus(L("ProductCalib_Rotating", "旋转校正中..."), Brushes.Orange);
                var pos = await _calibService.ReadCurrentPositionsAsync();
                await _calibService.RotateToReferenceAngleAsync(pos.Rz, -DeltaAngle);
                UpdateStatus(L("ProductCalib_RotateDone", "旋转校正完成"), Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "载台校准: 旋转校正失败");
                UpdateStatus($"{L("ProductCalib_Error", "错误")}: {ex.Message}", Brushes.Red);
            }
        }

        #endregion

        #region 文件操作

        private async Task ExecuteSaveConfigAsync()
        {
            try
            {
                var config = BuildCurrentConfig();
                Directory.CreateDirectory(ConfigDirectory);

                var fileName = CurrentFileName;
                if (string.IsNullOrEmpty(fileName))
                    fileName = $"StageCalib_{DateTime.Now:yyyyMMdd_HHmmss}.json";

                var filePath = Path.Combine(ConfigDirectory, fileName);
                await Task.Run(() =>
                {
                    var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                });

                config.LastFileName = fileName;
                CurrentFileName = fileName;

                // 保存默认配置记录
                _parameterStorage.Save("StageCalibration_Default", config, ConfigDirectory);

                UpdateStatus($"{L("ProductCalib_SaveSuccess", "保存成功")}: {fileName}", Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "载台校准: 保存失败");
                UpdateStatus($"{L("ProductCalib_Error", "错误")}: {ex.Message}", Brushes.Red);
            }
        }

        private async Task ExecuteLoadConfigAsync()
        {
            try
            {
                var filePath = _fileDialogService.ShowOpenFileDialog(
                    filter: "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    title: L("ProductCalib_LoadData", "加载"),
                    initialDirectory: Directory.Exists(ConfigDirectory) ? ConfigDirectory : null);

                if (string.IsNullOrEmpty(filePath)) return;
                await LoadConfigFromFileAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "载台校准: 加载失败");
                UpdateStatus($"{L("ProductCalib_Error", "错误")}: {ex.Message}", Brushes.Red);
            }
        }

        private async Task ExecuteImportConfigAsync()
        {
            try
            {
                var filePath = _fileDialogService.ShowOpenFileDialog(
                    filter: "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    title: L("ProductCalib_Import", "导入"),
                    initialDirectory: null);

                if (string.IsNullOrEmpty(filePath)) return;
                await LoadConfigFromFileAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "载台校准: 导入失败");
                UpdateStatus($"{L("ProductCalib_Error", "错误")}: {ex.Message}", Brushes.Red);
            }
        }

        private async Task ExecuteExportConfigAsync()
        {
            try
            {
                var defaultName = $"StageCalib_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = _fileDialogService.ShowSaveFileDialog(
                    filter: "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    title: L("ProductCalib_Export", "导出"),
                    defaultFileName: defaultName);

                if (string.IsNullOrEmpty(filePath)) return;

                var config = BuildCurrentConfig();
                await Task.Run(() =>
                {
                    var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                });

                UpdateStatus($"{L("ProductCalib_SaveSuccess", "保存成功")}: {Path.GetFileName(filePath)}", Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "载台校准: 导出失败");
                UpdateStatus($"{L("ProductCalib_Error", "错误")}: {ex.Message}", Brushes.Red);
            }
        }

        private async Task TryAutoLoadConfigAsync()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);

                var defaultConfig = _parameterStorage.Load<StageCalibrationConfig>(
                    "StageCalibration_Default", ConfigDirectory);

                if (defaultConfig != null && !string.IsNullOrEmpty(defaultConfig.LastFileName))
                {
                    var filePath = Path.Combine(ConfigDirectory, defaultConfig.LastFileName);
                    if (File.Exists(filePath))
                    {
                        await LoadConfigFromFileAsync(filePath);
                        return;
                    }
                }

                if (Directory.Exists(ConfigDirectory))
                {
                    var latestFile = Directory.GetFiles(ConfigDirectory, "StageCalib_*.json")
                        .OrderByDescending(File.GetLastWriteTime)
                        .FirstOrDefault();

                    if (latestFile != null)
                        await LoadConfigFromFileAsync(latestFile);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"载台校准: 自动加载失败 - {ex.Message}");
            }
        }

        private async Task LoadConfigFromFileAsync(string filePath)
        {
            var config = await Task.Run(() =>
            {
                var json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<StageCalibrationConfig>(json);
            });

            if (config == null) return;

            // 应用配置
            RefRx = config.ReferencePosition?.Rx ?? 0;
            RefRz = config.ReferencePosition?.Rz ?? 0;

            Photo1Dx = config.PhotoPosition1?.Dx ?? 0;
            Photo1Dy = config.PhotoPosition1?.Dy ?? 0;
            Photo1Dz = config.PhotoPosition1?.Dz ?? 0;

            Photo2Dx = config.PhotoPosition2?.Dx ?? 0;
            Photo2Dy = config.PhotoPosition2?.Dy ?? 0;
            Photo2Dz = config.PhotoPosition2?.Dz ?? 0;

            SelectedTcpConnection = config.TcpConnectionName;
            TriggerCommand = config.TriggerCommand;
            CaptureTimeoutMs = config.CaptureTimeoutMs;
            DeltaXLinkedVar = config.DeltaXLinkedVar;
            DeltaYLinkedVar = config.DeltaYLinkedVar;
            DeltaAngleLinkedVar = config.DeltaAngleLinkedVar;

            CurrentFileName = Path.GetFileName(filePath);
            UpdateStatus($"{L("ProductCalib_LoadSuccess", "加载成功")}: {CurrentFileName}", Brushes.LightGreen);
        }

        private StageCalibrationConfig BuildCurrentConfig()
        {
            return new StageCalibrationConfig
            {
                ReferencePosition = new StageReferencePosition { Rx = RefRx, Rz = RefRz },
                PhotoPosition1 = new StagePhotoPosition { Name = "Photo1", Dx = Photo1Dx, Dy = Photo1Dy, Dz = Photo1Dz },
                PhotoPosition2 = new StagePhotoPosition { Name = "Photo2", Dx = Photo2Dx, Dy = Photo2Dy, Dz = Photo2Dz },
                TcpConnectionName = SelectedTcpConnection,
                TriggerCommand = TriggerCommand,
                CaptureTimeoutMs = CaptureTimeoutMs,
                DeltaXLinkedVar = DeltaXLinkedVar,
                DeltaYLinkedVar = DeltaYLinkedVar,
                DeltaAngleLinkedVar = DeltaAngleLinkedVar,
                LastFileName = CurrentFileName
            };
        }

        #endregion

        #region TCP连接

        private async Task LoadTcpConnectionsAsync()
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    TcpConnections.Clear();
                    if (_tcpClientManager?.Clients != null)
                        foreach (var kvp in _tcpClientManager.Clients)
                            TcpConnections.Add(kvp.Key);

                    var serverNames = _tcpEventService?.GetServerNames();
                    if (serverNames != null)
                        foreach (var name in serverNames)
                            if (!TcpConnections.Contains(name))
                                TcpConnections.Add(name);
                });
            }
            catch (Exception ex)
            {
                _logger.Warn($"载台校准: 加载TCP连接列表失败 - {ex.Message}");
            }
        }

        #endregion

        #region 全局变量

        private async Task LoadGlobalVariablesAsync()
        {
            try
            {
                if (_recipePoolService == null) return;
                var variables = await _recipePoolService.LoadGlobalVariablesAsync(_recipePoolService.CurrentPoolId);
                var doubleVars = variables.Where(v => v.Type == GlobalVariableType.Double).ToList();
                LinkableGlobalVariables = new ObservableCollection<GlobalVariable>(doubleVars);
            }
            catch (Exception ex)
            {
                _logger.Warn($"载台校准: 加载全局变量失败 - {ex.Message}");
            }
        }

        /// <summary>将偏差值写入链接的全局变量</summary>
        private async Task WriteToGlobalVariablesAsync()
        {
            try
            {
                if (_recipePoolService == null) return;
                var variables = await _recipePoolService.LoadGlobalVariablesAsync(_recipePoolService.CurrentPoolId);

                if (!string.IsNullOrEmpty(DeltaXLinkedVar))
                {
                    var v = variables.FirstOrDefault(g => g.Name == DeltaXLinkedVar);
                    if (v != null) v.Value = DeltaX.ToString("F3");
                }
                if (!string.IsNullOrEmpty(DeltaYLinkedVar))
                {
                    var v = variables.FirstOrDefault(g => g.Name == DeltaYLinkedVar);
                    if (v != null) v.Value = DeltaY.ToString("F3");
                }
                if (!string.IsNullOrEmpty(DeltaAngleLinkedVar))
                {
                    var v = variables.FirstOrDefault(g => g.Name == DeltaAngleLinkedVar);
                    if (v != null) v.Value = DeltaAngle.ToString("F3");
                }

                await _recipePoolService.SaveGlobalVariablesAsync(_recipePoolService.CurrentPoolId, variables);
            }
            catch (Exception ex)
            {
                _logger.Warn($"载台校准: 写入全局变量失败 - {ex.Message}");
            }
        }

        #endregion

        #region 辅助方法

        private void UpdateStatus(string text, Brush color)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                StatusText = text;
                StatusColor = color;
            });
        }

        private string L(string key, string defaultValue = "")
        {
            return _localization?.GetResourceOrDefault(key, defaultValue) ?? defaultValue;
        }

        #endregion
    }
}

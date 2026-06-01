using Core.Abstraction;
using Core.Constants;
using Core.Models;
using Core.Utilities;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Module.ViewModels
{
    public class NeedleCameraAlignmentViewModel : BindableBase
    {
        private readonly IPositionMotionController _motionController;
        private readonly IParameterStorage _parameterStorage;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IRecipePoolService _recipePoolService;

        private const string StationIdentifier = "DispenserStation";
        /// <summary>配置文件保留天数</summary>
        private const int ConfigRetentionDays = 30;

        private int _selectedSystemNumber = 1;
        private double _cameraCenterX;
        private double _cameraCenterY;
        private double _needleTipX;
        private double _needleTipY;
        private double _needleTipZ;
        private double _calibrationDeltaX;
        private double _calibrationDeltaY;
        private double _compensationX;
        private double _compensationY;
        private string _compensationXExpression;
        private string _compensationYExpression;
        private string _calibrationStatusMessage;
        private Brush _calibrationStatusColor = Brushes.LightGray;
        private ObservableCollection<GlobalVariable> _availableGlobalVariables = new();
        private ObservableCollection<GlobalVariable> _linkableGlobalVariables = new();
        private string _compXLinkedVar;
        private string _compYLinkedVar;
        private string _currentFilePath;
        private string _currentFileName;

        public NeedleCameraAlignmentViewModel(
            IPositionMotionController motionController,
            IParameterStorage parameterStorage,
            ILoggerService logger,
            ILocalizationService localization,
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IRecipePoolService recipePoolService)
        {
            _motionController = motionController;
            _parameterStorage = parameterStorage;
            _logger = logger;
            _localization = localization;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _recipePoolService = recipePoolService;

            TeachCameraCenterCommand = new DelegateCommand(ExecuteTeachCameraCenter);
            TeachNeedleTipCommand = new DelegateCommand(ExecuteTeachNeedleTip);
            SaveParametersCommand = new DelegateCommand(async () => await ExecuteSaveParametersAsync());
            LoadParametersCommand = new DelegateCommand(async () => await ExecuteLoadParametersAsync());
            ResetParametersCommand = new DelegateCommand(ExecuteResetParameters);
            UnlinkCompXCommand = new DelegateCommand(() => CompXLinkedVar = null);
            UnlinkCompYCommand = new DelegateCommand(() => CompYLinkedVar = null);

            _eventAggregator.GetEvent<Recipe.Events.GlobalVariablesChangedEvent>().Subscribe(OnGlobalVariablesChanged, ThreadOption.UIThread);

            _ = InitializeAsync().ConfigureAwait(false);
        }

        /// <summary>初始化：确保默认全局变量存在，加载变量列表，再自动加载 JSON 配置</summary>
        private async Task InitializeAsync()
        {
            await EnsureDefaultCompGlobalVariablesAsync(1, 2);
            await LoadGlobalVariablesAsync();
            await TryAutoLoadConfigAsync();
        }

        #region 属性

        /// <summary>当前选择的系统编号（1或2）</summary>
        public int SelectedSystemNumber
        {
            get => _selectedSystemNumber;
            set
            {
                if (SetProperty(ref _selectedSystemNumber, value))
                {
                    RaisePropertyChanged(nameof(NeedleTipZAxisLabel));
                    _ = TryAutoLoadConfigAsync().ConfigureAwait(false);
                    UpdateStatus(
                        _localization.GetResource("NeedleCamera_Status_SystemSwitched", _selectedSystemNumber),
                        Brushes.LightBlue);
                }
            }
        }

        /// <summary>当前系统针尖 Z 轴显示名：系统1=Dz₂，系统2=Dz₃</summary>
        public string NeedleTipZAxisLabel =>
            _selectedSystemNumber == 1
                ? _localization.GetResource("NeedleCamera_Axis_Dz2")
                : _localization.GetResource("NeedleCamera_Axis_Dz3");

        public double CameraCenterX
        {
            get => _cameraCenterX;
            set
            {
                if (SetProperty(ref _cameraCenterX, value))
                    CalculateCalibrationDelta();
            }
        }

        public double CameraCenterY
        {
            get => _cameraCenterY;
            set
            {
                if (SetProperty(ref _cameraCenterY, value))
                    CalculateCalibrationDelta();
            }
        }

        public double NeedleTipX
        {
            get => _needleTipX;
            set
            {
                SetProperty(ref _needleTipX, value);
                CalculateCalibrationDelta();
            }
        }

        public double NeedleTipY
        {
            get => _needleTipY;
            set
            {
                SetProperty(ref _needleTipY, value);
                CalculateCalibrationDelta();
            }
        }

        public double NeedleTipZ
        {
            get => _needleTipZ;
            set => SetProperty(ref _needleTipZ, value);
        }

        public double CalibrationDeltaX
        {
            get => _calibrationDeltaX;
            set
            {
                if (SetProperty(ref _calibrationDeltaX, value))
                    RaisePropertyChanged(nameof(CalculatedCompX));
            }
        }

        public double CalibrationDeltaY
        {
            get => _calibrationDeltaY;
            set
            {
                if (SetProperty(ref _calibrationDeltaY, value))
                    RaisePropertyChanged(nameof(CalculatedCompY));
            }
        }

        /// <summary>可选全局变量列表（全部）</summary>
        public ObservableCollection<GlobalVariable> AvailableGlobalVariables
        {
            get => _availableGlobalVariables;
            set => SetProperty(ref _availableGlobalVariables, value);
        }

        /// <summary>可链接的全局变量列表（仅Double类型，供GlobalVariableLinkControl使用）</summary>
        public ObservableCollection<GlobalVariable> LinkableGlobalVariables
        {
            get => _linkableGlobalVariables;
            set => SetProperty(ref _linkableGlobalVariables, value);
        }

        /// <summary>X轴补偿链接的全局变量名</summary>
        public string CompXLinkedVar
        {
            get => _compXLinkedVar;
            set
            {
                if (SetProperty(ref _compXLinkedVar, value))
                    RaisePropertyChanged(nameof(IsCompXLinked));
            }
        }

        /// <summary>Y轴补偿链接的全局变量名</summary>
        public string CompYLinkedVar
        {
            get => _compYLinkedVar;
            set
            {
                if (SetProperty(ref _compYLinkedVar, value))
                    RaisePropertyChanged(nameof(IsCompYLinked));
            }
        }

        /// <summary>X轴补偿是否已链接全局变量</summary>
        public bool IsCompXLinked => !string.IsNullOrEmpty(CompXLinkedVar);

        /// <summary>Y轴补偿是否已链接全局变量</summary>
        public bool IsCompYLinked => !string.IsNullOrEmpty(CompYLinkedVar);

        public double CompensationX
        {
            get => _compensationX;
            set
            {
                if (SetProperty(ref _compensationX, value))
                    RaisePropertyChanged(nameof(CalculatedCompX));
            }
        }

        public double CompensationY
        {
            get => _compensationY;
            set
            {
                if (SetProperty(ref _compensationY, value))
                    RaisePropertyChanged(nameof(CalculatedCompY));
            }
        }

        public string CompensationXExpression
        {
            get => _compensationXExpression;
            set
            {
                if (SetProperty(ref _compensationXExpression, value))
                    RaisePropertyChanged(nameof(CalculatedCompX));
            }
        }

        public string CompensationYExpression
        {
            get => _compensationYExpression;
            set
            {
                if (SetProperty(ref _compensationYExpression, value))
                    RaisePropertyChanged(nameof(CalculatedCompY));
            }
        }

        /// <summary>计算后的CompX = CalibrationDeltaX + CompensationX + 表达式结果</summary>
        public double CalculatedCompX => CalibrationDeltaX + CompensationX + EvaluateExpression(CompensationXExpression);

        /// <summary>计算后的CompY = CalibrationDeltaY + CompensationY + 表达式结果</summary>
        public double CalculatedCompY => CalibrationDeltaY + CompensationY + EvaluateExpression(CompensationYExpression);

        public string CalibrationStatusMessage
        {
            get => _calibrationStatusMessage;
            set => SetProperty(ref _calibrationStatusMessage, value);
        }

        public Brush CalibrationStatusColor
        {
            get => _calibrationStatusColor;
            set => SetProperty(ref _calibrationStatusColor, value);
        }

        /// <summary>当前加载的配置文件完整路径</summary>
        public string CurrentFilePath
        {
            get => _currentFilePath;
            set => SetProperty(ref _currentFilePath, value);
        }

        /// <summary>当前加载的配置文件名（显示用）</summary>
        public string CurrentFileName
        {
            get => _currentFileName;
            set => SetProperty(ref _currentFileName, value);
        }

        #endregion

        #region 命令

        public DelegateCommand TeachCameraCenterCommand { get; }
        public DelegateCommand TeachNeedleTipCommand { get; }
        public DelegateCommand SaveParametersCommand { get; }
        public DelegateCommand LoadParametersCommand { get; }
        public DelegateCommand ResetParametersCommand { get; }
        /// <summary>解除X轴补偿的全局变量链接</summary>
        public DelegateCommand UnlinkCompXCommand { get; }
        /// <summary>解除Y轴补偿的全局变量链接</summary>
        public DelegateCommand UnlinkCompYCommand { get; }

        #endregion

        #region 命令实现

        /// <summary>示教相机中心：读取 Dx 和 Dy 轴位置</summary>
        private async void ExecuteTeachCameraCenter()
        {
            try
            {
                var positions = await _motionController.TeachAsync(StationIdentifier);

                if (TryGetAxisPosition(positions, out double dx, "Dx"))
                    CameraCenterX = dx;
                if (TryGetAxisPosition(positions, out double dy, "Dy"))
                    CameraCenterY = dy;

                CalculateCalibrationDelta();

                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_CameraCenterTaught", CameraCenterX, CameraCenterY),
                    Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error($"TeachCameraCenter异常: {ex.Message}");
                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_TeachFailed", ex.Message),
                    Brushes.Red);
            }
        }

        /// <summary>示教针尖位置：系统1读取 Dx/Dy/Dz₂，系统2读取 Dx/Dy/Dz₃</summary>
        private async void ExecuteTeachNeedleTip()
        {
            try
            {
                var positions = await _motionController.TeachAsync(StationIdentifier);

                if (TryGetAxisPosition(positions, out double dx, "Dx"))
                    NeedleTipX = dx;
                if (TryGetAxisPosition(positions, out double dy, "Dy"))
                    NeedleTipY = dy;

                var zAxisNames = GetNeedleTipZAxisNames(_selectedSystemNumber);
                if (TryGetAxisPosition(positions, out double dz, zAxisNames))
                    NeedleTipZ = dz;
                else
                {
                    _logger.Warn($"[NeedleCamera] 系统{_selectedSystemNumber}未读取到针尖Z轴 ({string.Join("/", zAxisNames)})");
                }

                CalculateCalibrationDelta();

                UpdateStatus(
                    _localization.GetResource(
                        "NeedleCamera_Status_NeedleTipTaught",
                        _selectedSystemNumber,
                        NeedleTipX,
                        NeedleTipY,
                        NeedleTipZ,
                        NeedleTipZAxisLabel),
                    Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                _logger.Error($"TeachNeedleTip异常: {ex.Message}");
                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_TeachFailed", ex.Message),
                    Brushes.Red);
            }
        }

        private async Task ExecuteSaveParametersAsync()
        {
            try
            {
                var configDir = GetConfigDirectory(_selectedSystemNumber);
                var fileName = $"NeedleCalibration_System{_selectedSystemNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = Path.Combine(configDir, fileName);

                var parameters = BuildCurrentParams();
                var json = JsonConvert.SerializeObject(parameters, Formatting.Indented);
                await File.WriteAllTextAsync(filePath, json);

                CurrentFilePath = filePath;
                CurrentFileName = fileName;
                await SaveCurrentFileToRecipePoolAsync();
                await WriteCompToGlobalVariablesAsync();

                QueueCleanupOldConfigFiles(configDir, filePath, _selectedSystemNumber);

                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_SaveSuccess"),
                    Brushes.LightGreen);
                _logger.Info($"[NeedleCamera] 系统{_selectedSystemNumber}参数保存: {filePath}");
            }
            catch (Exception ex)
            {
                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_SaveFailed", ex.Message),
                    Brushes.Red);
                _logger.Error($"[NeedleCamera] 保存参数异常: {ex.Message}");
            }
        }

        private async Task ExecuteLoadParametersAsync()
        {
            try
            {
                var configDir = GetConfigDirectory(_selectedSystemNumber);
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    InitialDirectory = configDir
                };

                if (dialog.ShowDialog() != true) return;

                await LoadConfigFromPathAsync(dialog.FileName);
            }
            catch (Exception ex)
            {
                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_LoadFailed", ex.Message),
                    Brushes.Red);
                _logger.Error($"[NeedleCamera] 加载参数异常: {ex.Message}");
            }
        }

        private void ExecuteResetParameters()
        {
            CameraCenterX = 0;
            CameraCenterY = 0;
            NeedleTipX = 0;
            NeedleTipY = 0;
            NeedleTipZ = 0;
            CalibrationDeltaX = 0;
            CalibrationDeltaY = 0;
            CompensationX = 0;
            CompensationY = 0;
            CompensationXExpression = null;
            CompensationYExpression = null;
            _ = ApplyDefaultLinkedVariablesAsync(_selectedSystemNumber);

            UpdateStatus(
                _localization.GetResource("NeedleCamera_Status_ParametersReset"),
                Brushes.LightGreen);
            _logger.Info($"[NeedleCamera] 系统{_selectedSystemNumber}参数已重置");
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 计算相机中心与针尖的 XY 校准差值。
        /// 针尖 Z 按系统分别示教（Dz₂/Dz₃），不参与 XY 差值计算。
        /// </summary>
        private void CalculateCalibrationDelta()
        {
            try
            {
                bool hasCamera = Math.Abs(CameraCenterX) > 0.001 || Math.Abs(CameraCenterY) > 0.001;
                bool hasNeedle = Math.Abs(NeedleTipX) > 0.001 || Math.Abs(NeedleTipY) > 0.001;

                if (hasCamera && hasNeedle)
                {
                    CalibrationDeltaX = NeedleTipX - CameraCenterX;
                    CalibrationDeltaY = NeedleTipY - CameraCenterY;
                }
                else if (!hasCamera && !hasNeedle)
                {
                    CalibrationDeltaX = 0;
                    CalibrationDeltaY = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"CalculateCalibrationDelta异常: {ex.Message}");
            }
        }

        /// <summary>获取当前系统针尖 Z 轴名称候选（兼容 Unicode/ASCII 命名）</summary>
        private static string[] GetNeedleTipZAxisNames(int systemNumber) =>
            systemNumber == 1
                ? new[] { "Dz₂"}
                : new[] { "Dz₃"};

        /// <summary>从示教结果中按候选轴名顺序读取位置</summary>
        private static bool TryGetAxisPosition(IReadOnlyDictionary<string, double> positions, out double value, params string[] axisNames)
        {
            foreach (var name in axisNames)
            {
                if (positions.TryGetValue(name, out value))
                    return true;
            }

            value = 0;
            return false;
        }

        /// <summary>安全计算数学表达式，如 "0.1+0.2+0.3"，失败返回0</summary>
        private static double EvaluateExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return 0;
            try
            {
                var result = new DataTable().Compute(expression, null);
                return Convert.ToDouble(result);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>更新状态栏信息</summary>
        private void UpdateStatus(string message, Brush color)
        {
            CalibrationStatusMessage = message;
            CalibrationStatusColor = color;
        }

        /// <summary>获取系统配置目录：Config/NeedleSystems/System{N}</summary>
        private static string GetConfigDirectory(int systemNumber)
        {
            var dir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Config", "NeedleSystems", $"System{systemNumber}");
            Directory.CreateDirectory(dir);
            return dir;
        }

        /// <summary>从指定文件路径加载配置并应用到ViewModel</summary>
        private async Task LoadConfigFromPathAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                UpdateStatus(_localization.GetResource("NeedleCamera_Status_NoSavedParams"), Brushes.Orange);
                return;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var parameters = JsonConvert.DeserializeObject<NeedleCameraCalibrationParams>(json);
            if (parameters == null) return;

            ApplyParams(parameters);
            await EnsureLinkedCompVariablesExistAsync(CompXLinkedVar, CompYLinkedVar);
            await LoadGlobalVariablesAsync();

            CurrentFilePath = filePath;
            CurrentFileName = Path.GetFileName(filePath);

            UpdateStatus(_localization.GetResource("NeedleCamera_Status_LoadSuccess"), Brushes.LightGreen);
            _logger.Info($"[NeedleCamera] 系统{_selectedSystemNumber}配置已加载: {filePath}");
        }

        /// <summary>从参数对象应用到 ViewModel；链接名为空时使用当前系统默认全局变量</summary>
        private void ApplyParams(NeedleCameraCalibrationParams p)
        {
            var systemNumber = p.SystemNumber > 0 ? p.SystemNumber : _selectedSystemNumber;

            CameraCenterX = p.CameraCenterX;
            CameraCenterY = p.CameraCenterY;
            NeedleTipX = p.NeedleTipX;
            NeedleTipY = p.NeedleTipY;
            NeedleTipZ = p.NeedleTipZ;
            CalibrationDeltaX = p.CalibrationDeltaX;
            CalibrationDeltaY = p.CalibrationDeltaY;
            CompensationX = p.CompensationX;
            CompensationY = p.CompensationY;
            CompensationXExpression = p.CompensationXExpression;
            CompensationYExpression = p.CompensationYExpression;
            CompXLinkedVar = ResolveCompXLinkedVar(p.CompXLinkedVar, systemNumber);
            CompYLinkedVar = ResolveCompYLinkedVar(p.CompYLinkedVar, systemNumber);
        }

        private static string ResolveCompXLinkedVar(string linkedVarFromJson, int systemNumber) =>
            string.IsNullOrWhiteSpace(linkedVarFromJson)
                ? NeedleCameraGlobalVariableNames.GetDefaultCompXLinkedVar(systemNumber)
                : linkedVarFromJson;

        private static string ResolveCompYLinkedVar(string linkedVarFromJson, int systemNumber) =>
            string.IsNullOrWhiteSpace(linkedVarFromJson)
                ? NeedleCameraGlobalVariableNames.GetDefaultCompYLinkedVar(systemNumber)
                : linkedVarFromJson;

        /// <summary>无配置文件时，应用当前系统的默认补偿链接目标</summary>
        private async Task ApplyDefaultLinkedVariablesAsync(int systemNumber)
        {
            await EnsureDefaultCompGlobalVariablesAsync(systemNumber);
            await LoadGlobalVariablesAsync();

            CompXLinkedVar = NeedleCameraGlobalVariableNames.GetDefaultCompXLinkedVar(systemNumber);
            CompYLinkedVar = NeedleCameraGlobalVariableNames.GetDefaultCompYLinkedVar(systemNumber);
        }

        /// <summary>
        /// 在配方池全局变量中创建默认 Double 补偿变量（若不存在）。
        /// 系统1/2 各 X、Y 共 4 个：NeedleCamera_System{N}_CompX/Y_LinkedVar
        /// </summary>
        private async Task EnsureDefaultCompGlobalVariablesAsync(params int[] systemNumbers)
        {
            if (systemNumbers == null || systemNumbers.Length == 0)
                systemNumbers = new[] { _selectedSystemNumber };

            try
            {
                var poolId = _recipePoolService?.CurrentPoolName ?? "Default";
                var variables = (await _recipePoolService.LoadGlobalVariablesAsync(poolId)).ToList();
                var changed = false;

                foreach (var systemNumber in systemNumbers.Distinct())
                {
                    changed |= EnsureDoubleGlobalVariable(variables,
                        NeedleCameraGlobalVariableNames.GetDefaultCompXLinkedVar(systemNumber),
                        $"针头相机系统{systemNumber} X补偿（默认）");
                    changed |= EnsureDoubleGlobalVariable(variables,
                        NeedleCameraGlobalVariableNames.GetDefaultCompYLinkedVar(systemNumber),
                        $"针头相机系统{systemNumber} Y补偿（默认）");
                }

                if (!changed)
                    return;

                for (int i = 0; i < variables.Count; i++)
                    variables[i].Index = i + 1;

                await _recipePoolService.SaveGlobalVariablesAsync(poolId, variables);
                _eventAggregator?.GetEvent<Recipe.Events.GlobalVariablesChangedEvent>()?.Publish(poolId);
                _logger.Info("[NeedleCamera] 已创建默认补偿全局变量");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleCamera] 创建默认补偿全局变量失败: {ex.Message}");
            }
        }

        /// <summary>全局变量池中不存在时添加 Double 变量，初始值 0</summary>
        private static bool EnsureDoubleGlobalVariable(List<GlobalVariable> variables, string name, string comment)
        {
            if (variables.Any(v => v.Name == name))
                return false;

            variables.Add(new GlobalVariable
            {
                Name = name,
                Type = GlobalVariableType.Double,
                Value = "0",
                Comment = comment
            });
            return true;
        }

        /// <summary>确保 JSON 中指定的链接目标在全局变量池中存在（Double，初始 0）</summary>
        private async Task EnsureLinkedCompVariablesExistAsync(params string[] linkedVarNames)
        {
            var names = linkedVarNames?.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToArray();
            if (names == null || names.Length == 0)
                return;

            try
            {
                var poolId = _recipePoolService?.CurrentPoolName ?? "Default";
                var variables = (await _recipePoolService.LoadGlobalVariablesAsync(poolId)).ToList();
                var changed = false;

                foreach (var name in names)
                    changed |= EnsureDoubleGlobalVariable(variables, name, "针头相机补偿链接变量");

                if (!changed)
                    return;

                for (int i = 0; i < variables.Count; i++)
                    variables[i].Index = i + 1;

                await _recipePoolService.SaveGlobalVariablesAsync(poolId, variables);
                _eventAggregator?.GetEvent<Recipe.Events.GlobalVariablesChangedEvent>()?.Publish(poolId);
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleCamera] 确保链接变量存在失败: {ex.Message}");
            }
        }

        /// <summary>从ViewModel当前状态构建参数对象</summary>
        private NeedleCameraCalibrationParams BuildCurrentParams()
        {
            return new NeedleCameraCalibrationParams
            {
                CameraCenterX = CameraCenterX,
                CameraCenterY = CameraCenterY,
                NeedleTipX = NeedleTipX,
                NeedleTipY = NeedleTipY,
                NeedleTipZ = NeedleTipZ,
                CalibrationDeltaX = CalibrationDeltaX,
                CalibrationDeltaY = CalibrationDeltaY,
                CompensationX = CompensationX,
                CompensationY = CompensationY,
                CompensationXExpression = CompensationXExpression,
                CompensationYExpression = CompensationYExpression,
                CompXLinkedVar = CompXLinkedVar,
                CompYLinkedVar = CompYLinkedVar,
                LastCalibrated = DateTime.Now,
                SystemNumber = _selectedSystemNumber
            };
        }

        /// <summary>尝试从配方池记录自动加载最近使用的配置文件</summary>
        private async Task TryAutoLoadConfigAsync()
        {
            try
            {
                var poolName = _recipePoolService?.CurrentPoolName ?? "Default";
                var extKey = $"NeedleCamera_CurrentFile_System{_selectedSystemNumber}";
                var extData = await _recipePoolService.GetExtensionDataAsync<NeedleCameraFileRecord>(poolName, extKey);

                if (extData?.FilePath != null && File.Exists(extData.FilePath))
                {
                    _logger.Info($"[NeedleCamera] 从配方池记录加载: {extData.FilePath}");
                    await LoadConfigFromPathAsync(extData.FilePath);
                    return;
                }

                // 回退：加载目录中最新的配置文件
                var configDir = GetConfigDirectory(_selectedSystemNumber);
                var latest = Directory
                    .EnumerateFiles(configDir, $"NeedleCalibration_System{_selectedSystemNumber}_*.json")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault();

                if (latest != null)
                {
                    _logger.Info($"[NeedleCamera] 配方池无记录，加载最新文件: {latest}");
                    await LoadConfigFromPathAsync(latest);
                    return;
                }

                await ApplyDefaultLinkedVariablesAsync(_selectedSystemNumber);
                _logger.Info($"[NeedleCamera] 系统{_selectedSystemNumber}无可加载的配置文件，已应用默认补偿链接");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleCamera] 自动加载配置失败: {ex.Message}");
            }
        }

        /// <summary>将当前文件路径保存到配方池ExtensionData</summary>
        private async Task SaveCurrentFileToRecipePoolAsync()
        {
            try
            {
                var poolName = _recipePoolService?.CurrentPoolName ?? "Default";
                await _recipePoolService.SetExtensionDataAsync(poolName,
                    $"NeedleCamera_CurrentFile_System{_selectedSystemNumber}",
                    new NeedleCameraFileRecord { FilePath = CurrentFilePath });
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleCamera] 保存文件记录到配方池失败: {ex.Message}");
            }
        }

        /// <summary>后台异步清理过期配置文件，避免阻塞UI线程</summary>
        private void QueueCleanupOldConfigFiles(string configDir, string currentFilePath, int systemNumber)
        {
            _ = Task.Run(() => CleanupOldConfigFiles(configDir, currentFilePath, systemNumber));
        }

        /// <summary>清理超过保留天数的旧配置文件（后台执行）</summary>
        private void CleanupOldConfigFiles(string configDir, string currentFilePath, int systemNumber)
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-ConfigRetentionDays);
                var cleanedCount = 0;

                foreach (var file in Directory.EnumerateFiles(configDir, $"NeedleCalibration_System{systemNumber}_*.json"))
                {
                    if (string.Equals(file, currentFilePath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        if (File.GetLastWriteTime(file) >= cutoff)
                            continue;

                        File.Delete(file);
                        cleanedCount++;
                        _logger.Info($"[NeedleCamera] 已清理过期配置文件: {file}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"[NeedleCamera] 清理文件失败: {file}, {ex.Message}");
                    }
                }

                if (cleanedCount > 0)
                    _logger.Info($"[NeedleCamera] 本次清理了 {cleanedCount} 个过期配置文件 (保留{ConfigRetentionDays}天)");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleCamera] 清理旧配置文件异常: {ex.Message}");
            }
        }

        /// <summary>从配方池加载全局变量列表，并刷新可链接变量集合（链接关系仅从 JSON 恢复）</summary>
        private async Task LoadGlobalVariablesAsync()
        {
            try
            {
                var poolId = _recipePoolService?.CurrentPoolName ?? "Default";
                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
                AvailableGlobalVariables = new ObservableCollection<GlobalVariable>(variables);

                RefreshLinkableGlobalVariables();

                RaisePropertyChanged(nameof(IsCompXLinked));
                RaisePropertyChanged(nameof(IsCompYLinked));
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleCamera] 加载全局变量失败: {ex.Message}");
            }
        }

        /// <summary>外部全局变量变更时重新加载，同步下拉列表和链接变量值</summary>
        private async void OnGlobalVariablesChanged(string poolId)
        {
            try
            {
                var currentPoolId = _recipePoolService?.CurrentPoolName ?? "Default";
                if (!string.Equals(poolId, currentPoolId, StringComparison.OrdinalIgnoreCase))
                    return;

                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);

                AvailableGlobalVariables.Clear();
                foreach (var v in variables)
                    AvailableGlobalVariables.Add(v);

                RefreshLinkableGlobalVariables();

                // 同步已链接变量的最新值
                if (IsCompXLinked)
                    RaisePropertyChanged(nameof(CalculatedCompX));
                if (IsCompYLinked)
                    RaisePropertyChanged(nameof(CalculatedCompY));
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleCamera] 全局变量变更同步失败: {ex.Message}");
            }
        }

        /// <summary>刷新可链接的全局变量列表（仅保留 Double 类型）</summary>
        private void RefreshLinkableGlobalVariables()
        {
            var linkable = AvailableGlobalVariables
                .Where(v => v.Type == GlobalVariableType.Double)
                .ToList();
            LinkableGlobalVariables = new ObservableCollection<GlobalVariable>(linkable);
            RaisePropertyChanged(nameof(IsCompXLinked));
            RaisePropertyChanged(nameof(IsCompYLinked));
        }

        /// <summary>将 CalculatedComp 写入用户链接的 Double 全局变量（链接名仅保存在 JSON）</summary>
        private async Task WriteCompToGlobalVariablesAsync()
        {
            try
            {
                var poolId = _recipePoolService?.CurrentPoolName ?? "Default";
                var variables = (await _recipePoolService.LoadGlobalVariablesAsync(poolId)).ToList();

                RemoveLegacyLinkMetadataVariables(variables);

                if (!string.IsNullOrEmpty(CompXLinkedVar))
                    UpdateOrAddGlobalVariable(variables, CompXLinkedVar, CalculatedCompX.ToString("F6"),
                        $"针头相机系统{_selectedSystemNumber} X补偿", GlobalVariableType.Double);
                if (!string.IsNullOrEmpty(CompYLinkedVar))
                    UpdateOrAddGlobalVariable(variables, CompYLinkedVar, CalculatedCompY.ToString("F6"),
                        $"针头相机系统{_selectedSystemNumber} Y补偿", GlobalVariableType.Double);

                for (int i = 0; i < variables.Count; i++)
                    variables[i].Index = i + 1;

                await _recipePoolService.SaveGlobalVariablesAsync(poolId, variables);
                _eventAggregator?.GetEvent<Recipe.Events.GlobalVariablesChangedEvent>()?.Publish(poolId);
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleCamera] 写入补偿值到全局变量失败: {ex.Message}");
            }
        }

        /// <summary>移除旧版在全局变量池中重复存储链接关系的 String 元数据项</summary>
        private static void RemoveLegacyLinkMetadataVariables(List<GlobalVariable> variables)
        {
            variables.RemoveAll(v =>
                v.Type == GlobalVariableType.String &&
                (v.Name == NeedleCameraGlobalVariableNames.LegacyCompXLinkMetadataKey ||
                 v.Name == NeedleCameraGlobalVariableNames.LegacyCompYLinkMetadataKey));
        }

        /// <summary>更新或添加全局变量，支持指定类型</summary>
        private static void UpdateOrAddGlobalVariable(List<GlobalVariable> variables, string name, string value, string comment, GlobalVariableType type)
        {
            var existing = variables.FirstOrDefault(v => v.Name == name);
            if (existing != null)
            {
                existing.Value = value;
                existing.Type = type;
            }
            else
            {
                variables.Add(new GlobalVariable
                {
                    Name = name,
                    Type = type,
                    Value = value,
                    Comment = comment
                });
            }
        }

        #endregion
    }

    /// <summary>记录最后使用的参数文件路径</summary>
    public class NeedleCameraFileRecord
    {
        public string FilePath { get; set; }
    }
}

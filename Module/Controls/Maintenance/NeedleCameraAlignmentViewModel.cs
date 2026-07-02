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
        private readonly IProtectedFileProvider _protectedFileProvider;

        private const string StationIdentifier = "DispenserStation";
        /// <summary>配置文件保留天数</summary>
        private const int ConfigRetentionDays = 30;

        /// <summary>各系统参数快照缓存（含文件路径），切换系统时保留未保存的编辑</summary>
        private readonly Dictionary<int, NeedleCameraSystemState> _systemStateCache = new();

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
            IRecipePoolService recipePoolService,
            IProtectedFileProvider protectedFileProvider = null)
        {
            _motionController = motionController;
            _parameterStorage = parameterStorage;
            _logger = logger;
            _localization = localization;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _recipePoolService = recipePoolService;
            _protectedFileProvider = protectedFileProvider;

            TeachCameraCenterCommand = new DelegateCommand(ExecuteTeachCameraCenter);
            TeachNeedleTipCommand = new DelegateCommand(ExecuteTeachNeedleTip);
            SaveParametersCommand = new DelegateCommand(async () => await ExecuteSaveParametersAsync());
            LoadParametersCommand = new DelegateCommand(async () => await ExecuteLoadParametersAsync());
            ResetParametersCommand = new DelegateCommand(ExecuteResetParameters);
            UnlinkCompXCommand = new DelegateCommand(() => CompXLinkedVar = null);
            UnlinkCompYCommand = new DelegateCommand(() => CompYLinkedVar = null);

            _eventAggregator.GetEvent<Recipe.Events.GlobalVariablesChangedEvent>().Subscribe(OnGlobalVariablesChanged, ThreadOption.UIThread);

            // 订阅配方池切换事件：切换池时清空系统状态缓存并从新池 ExtensionData 重新加载（参考 ZScanDetailViewModel 模式）
            _eventAggregator.GetEvent<Recipe.Events.RecipePoolChangedEvent>().Subscribe(OnRecipePoolChanged, ThreadOption.UIThread);

            _ = InitializeAsync().ConfigureAwait(false);
        }

        /// <summary>配方池切换时清空系统状态缓存，从新池 ExtensionData 重新加载系统1/2参数并应用到当前系统UI</summary>
        private async void OnRecipePoolChanged(string poolName)
        {
            try
            {
                // 清空旧池的系统状态缓存，强制从新池 ExtensionData 重新加载
                _systemStateCache.Clear();
                await EnsureSystemCachedAsync(1);
                await EnsureSystemCachedAsync(2);
                if (_systemStateCache.TryGetValue(_selectedSystemNumber, out var state))
                    ApplySystemState(state, _selectedSystemNumber);
                _logger.Info(string.Format(_localization.GetResourceOrDefault("NCA_Log_RecipePoolSwitchedReload",
                    "[NeedleCamera] 配方池切换，已从新池重新加载系统参数（池={0}）"), poolName));
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("NCA_Log_RecipePoolSwitchedReloadFailed",
                    "[NeedleCamera] 配方池切换重新加载失败: {0}"), ex.Message));
            }
        }

        /// <summary>初始化：确保默认全局变量存在，预加载系统1/2参数，再应用当前系统到 UI</summary>
        private async Task InitializeAsync()
        {
            await EnsureDefaultCompGlobalVariablesAsync(1, 2);
            await LoadGlobalVariablesAsync();
            await EnsureSystemCachedAsync(1);
            await EnsureSystemCachedAsync(2);

            if (_systemStateCache.TryGetValue(_selectedSystemNumber, out var state))
                ApplySystemState(state, _selectedSystemNumber);
        }

        #region 属性

        /// <summary>当前选择的系统编号（1或2），切换时缓存旧系统并加载新系统独立参数</summary>
        public int SelectedSystemNumber
        {
            get => _selectedSystemNumber;
            set
            {
                if (_selectedSystemNumber == value) return;
                var previous = _selectedSystemNumber;
                if (SetProperty(ref _selectedSystemNumber, value))
                {
                    RaisePropertyChanged(nameof(NeedleTipZAxisLabel));
                    _ = SwitchSystemAsync(previous, value);
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
                _logger.Error(string.Format(_localization.GetResourceOrDefault("NCA_Log_TeachCameraCenterException", "TeachCameraCenter异常: {0}"), ex.Message));
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
                    _logger.Warn(string.Format(_localization.GetResourceOrDefault("NCA_Log_NeedleTipZAxisNotFound", "[NeedleCamera] 系统{0}未读取到针尖Z轴 ({1})"), _selectedSystemNumber, string.Join("/", zAxisNames)));
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
                _logger.Error(string.Format(_localization.GetResourceOrDefault("NCA_Log_TeachNeedleTipException", "TeachNeedleTip异常: {0}"), ex.Message));
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
                StashCurrentSystemState(_selectedSystemNumber);
                await SaveCurrentFileToRecipePoolAsync();
                await WriteCompToGlobalVariablesAsync();

                QueueCleanupOldConfigFiles(configDir, filePath, _selectedSystemNumber);

                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_SaveSuccess"),
                    Brushes.LightGreen);
                _logger.Info(string.Format(_localization.GetResourceOrDefault("NCA_Log_ParametersSaved", "[NeedleCamera] 系统{0}参数保存: {1}"), _selectedSystemNumber, filePath));
            }
            catch (Exception ex)
            {
                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_SaveFailed", ex.Message),
                    Brushes.Red);
                _logger.Error(string.Format(_localization.GetResourceOrDefault("NCA_Log_SaveParametersException", "[NeedleCamera] 保存参数异常: {0}"), ex.Message));
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
                _logger.Error(string.Format(_localization.GetResourceOrDefault("NCA_Log_LoadParametersException", "[NeedleCamera] 加载参数异常: {0}"), ex.Message));
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
            CompXLinkedVar = NeedleCameraGlobalVariableNames.GetDefaultCompXLinkedVar(_selectedSystemNumber);
            CompYLinkedVar = NeedleCameraGlobalVariableNames.GetDefaultCompYLinkedVar(_selectedSystemNumber);
            StashCurrentSystemState(_selectedSystemNumber);
            _ = EnsureDefaultCompGlobalVariablesAsync(_selectedSystemNumber);

            UpdateStatus(
                _localization.GetResource("NeedleCamera_Status_ParametersReset"),
                Brushes.LightGreen);
            _logger.Info(string.Format(_localization.GetResourceOrDefault("NCA_Log_ParametersReset", "[NeedleCamera] 系统{0}参数已重置"), _selectedSystemNumber));
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
                _logger.Error(string.Format(_localization.GetResourceOrDefault("NCA_Log_CalculateCalibrationDeltaException", "CalculateCalibrationDelta异常: {0}"), ex.Message));
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
            StashCurrentSystemState(_selectedSystemNumber);

            UpdateStatus(_localization.GetResource("NeedleCamera_Status_LoadSuccess"), Brushes.LightGreen);
            _logger.Info(string.Format(_localization.GetResourceOrDefault("NCA_Log_ConfigLoaded", "[NeedleCamera] 系统{0}配置已加载: {1}"), _selectedSystemNumber, filePath));
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
                _logger.Info(_localization.GetResourceOrDefault("NCA_Log_DefaultCompGlobalVarsCreated", "[NeedleCamera] 已创建默认补偿全局变量"));
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("NCA_Log_CreateDefaultCompGlobalVarsFailed", "[NeedleCamera] 创建默认补偿全局变量失败: {0}"), ex.Message));
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
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("NCA_Log_EnsureLinkedVarsExistFailed", "[NeedleCamera] 确保链接变量存在失败: {0}"), ex.Message));
            }
        }

        /// <summary>切换系统：缓存旧系统完整状态，加载新系统缓存或磁盘配置</summary>
        private async Task SwitchSystemAsync(int previousSystem, int newSystem)
        {
            try
            {
                StashCurrentSystemState(previousSystem);

                if (_systemStateCache.TryGetValue(newSystem, out var cached))
                {
                    ApplySystemState(cached, newSystem);
                    UpdateStatus(
                        _localization.GetResource("NeedleCamera_Status_SystemSwitched", newSystem),
                        Brushes.LightBlue);
                    return;
                }

                var loaded = await LoadSystemStateFromDiskAsync(newSystem);
                _systemStateCache[newSystem] = loaded;
                ApplySystemState(loaded, newSystem);
                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_SystemSwitched", newSystem),
                    Brushes.LightBlue);
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("NCA_Log_SwitchSystemFailed", "[NeedleCamera] 切换系统失败: {0}"), ex.Message));
            }
        }

        /// <summary>缓存当前系统的参数与文件信息（深拷贝，避免切换后互相污染）</summary>
        private void StashCurrentSystemState(int systemNumber)
        {
            var parameters = BuildCurrentParams();
            parameters.SystemNumber = systemNumber;
            _systemStateCache[systemNumber] = new NeedleCameraSystemState
            {
                Parameters = parameters.Clone(),
                CurrentFilePath = CurrentFilePath,
                CurrentFileName = CurrentFileName
            };
        }

        /// <summary>应用指定系统的完整状态到 UI</summary>
        private void ApplySystemState(NeedleCameraSystemState state, int systemNumber)
        {
            if (state?.Parameters == null)
                state = CreateDefaultSystemState(systemNumber);

            ApplyParams(state.Parameters);
            CurrentFilePath = state.CurrentFilePath;
            CurrentFileName = state.CurrentFileName;
            RaisePropertyChanged(nameof(CalculatedCompX));
            RaisePropertyChanged(nameof(CalculatedCompY));
        }

        /// <summary>创建指定系统的默认参数集（全零 + 默认补偿链接）</summary>
        private static NeedleCameraSystemState CreateDefaultSystemState(int systemNumber) =>
            new()
            {
                Parameters = new NeedleCameraCalibrationParams
                {
                    SystemNumber = systemNumber,
                    CompXLinkedVar = NeedleCameraGlobalVariableNames.GetDefaultCompXLinkedVar(systemNumber),
                    CompYLinkedVar = NeedleCameraGlobalVariableNames.GetDefaultCompYLinkedVar(systemNumber)
                }
            };

        /// <summary>预加载另一系统参数到缓存（不切换 UI）</summary>
        private async Task EnsureSystemCachedAsync(int systemNumber)
        {
            if (_systemStateCache.ContainsKey(systemNumber))
                return;

            _systemStateCache[systemNumber] = await LoadSystemStateFromDiskAsync(systemNumber);
        }

        /// <summary>从配方池记录或目录加载指定系统的参数快照</summary>
        private async Task<NeedleCameraSystemState> LoadSystemStateFromDiskAsync(int systemNumber)
        {
            try
            {
                var poolName = _recipePoolService?.CurrentPoolName ?? "Default";
                var extKey = $"NeedleCamera_CurrentFile_System{systemNumber}";
                var extData = await _recipePoolService.GetExtensionDataAsync<NeedleCameraFileRecord>(poolName, extKey);

                if (extData?.FilePath != null && File.Exists(extData.FilePath))
                {
                    var json = await File.ReadAllTextAsync(extData.FilePath);
                    var parameters = JsonConvert.DeserializeObject<NeedleCameraCalibrationParams>(json);
                    if (parameters != null)
                    {
                        _logger.Info(string.Format(_localization.GetResourceOrDefault("NCA_Log_LoadFromRecipePool", "[NeedleCamera] 系统{0}从配方池记录加载: {1}"), systemNumber, extData.FilePath));
                        return new NeedleCameraSystemState
                        {
                            Parameters = parameters,
                            CurrentFilePath = extData.FilePath,
                            CurrentFileName = Path.GetFileName(extData.FilePath)
                        };
                    }
                }

                var configDir = GetConfigDirectory(systemNumber);
                var latest = Directory
                    .EnumerateFiles(configDir, $"NeedleCalibration_System{systemNumber}_*.json")
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault();

                if (latest != null)
                {
                    var json = await File.ReadAllTextAsync(latest);
                    var parameters = JsonConvert.DeserializeObject<NeedleCameraCalibrationParams>(json);
                    if (parameters != null)
                    {
                        _logger.Info(string.Format(_localization.GetResourceOrDefault("NCA_Log_LoadLatestFile", "[NeedleCamera] 系统{0}加载最新文件: {1}"), systemNumber, latest));
                        return new NeedleCameraSystemState
                        {
                            Parameters = parameters,
                            CurrentFilePath = latest,
                            CurrentFileName = Path.GetFileName(latest)
                        };
                    }
                }

                _logger.Info(string.Format(_localization.GetResourceOrDefault("NCA_Log_NoConfigUseDefault", "[NeedleCamera] 系统{0}无可加载的配置文件，使用默认参数"), systemNumber));
                return CreateDefaultSystemState(systemNumber);
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("NCA_Log_LoadSystemConfigFailed", "[NeedleCamera] 加载系统{0}配置失败: {1}"), systemNumber, ex.Message));
                return CreateDefaultSystemState(systemNumber);
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
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("NCA_Log_SaveFileRecordToRecipePoolFailed", "[NeedleCamera] 保存文件记录到配方池失败: {0}"), ex.Message));
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
                var skippedProtected = 0;

                // 获取受配方池引用的文件路径，清理时跳过（防止切换池后配置丢失）
                HashSet<string> protectedPaths = null;
                try
                {
                    protectedPaths = _protectedFileProvider?.GetProtectedFilePaths();
                }
                catch { /* 获取失败时按无保护处理，不阻塞清理 */ }

                foreach (var file in Directory.EnumerateFiles(configDir, $"NeedleCalibration_System{systemNumber}_*.json"))
                {
                    if (string.Equals(file, currentFilePath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // 跳过受配方池引用的文件
                    if (protectedPaths != null && protectedPaths.Contains(file))
                    {
                        skippedProtected++;
                        continue;
                    }

                    try
                    {
                        if (File.GetLastWriteTime(file) >= cutoff)
                            continue;

                        File.Delete(file);
                        cleanedCount++;
                        _logger.Info(string.Format(_localization.GetResourceOrDefault("NCA_Log_CleanedExpiredConfigFile", "[NeedleCamera] 已清理过期配置文件: {0}"), file));
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(string.Format(_localization.GetResourceOrDefault("NCA_Log_CleanFileFailed", "[NeedleCamera] 清理文件失败: {0}, {1}"), file, ex.Message));
                    }
                }

                if (cleanedCount > 0 || skippedProtected > 0)
                    _logger.Info(string.Format(_localization.GetResourceOrDefault("NCA_Log_CleanupSummary", "[NeedleCamera] 本次清理了 {0} 个过期配置文件 (保留{1}天, 跳过{2}个受保护文件)"), cleanedCount, ConfigRetentionDays, skippedProtected));
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("NCA_Log_CleanupOldConfigFilesException", "[NeedleCamera] 清理旧配置文件异常: {0}"), ex.Message));
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
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("NCA_Log_LoadGlobalVarsFailed", "[NeedleCamera] 加载全局变量失败: {0}"), ex.Message));
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
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("NCA_Log_GlobalVarChangeSyncFailed", "[NeedleCamera] 全局变量变更同步失败: {0}"), ex.Message));
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
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("NCA_Log_WriteCompToGlobalVarsFailed", "[NeedleCamera] 写入补偿值到全局变量失败: {0}"), ex.Message));
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

    /// <summary>单系统针头相机标定状态（参数 + 文件路径）</summary>
    internal sealed class NeedleCameraSystemState
    {
        public NeedleCameraCalibrationParams Parameters { get; init; }
        public string CurrentFilePath { get; init; }
        public string CurrentFileName { get; init; }
    }

    /// <summary>记录最后使用的参数文件路径</summary>
    public class NeedleCameraFileRecord
    {
        public string FilePath { get; set; }
    }
}

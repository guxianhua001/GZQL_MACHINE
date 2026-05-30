using Core.Abstraction;
using Core.Models;
using Core.Utilities;
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

        private const string StationIdentifier = "Dispenser";

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
        private double _compensationZ;
        private string _compensationXExpression;
        private string _compensationYExpression;
        private string _compensationZExpression;
        private string _calibrationStatusMessage;
        private Brush _calibrationStatusColor = Brushes.LightGray;
        private ObservableCollection<GlobalVariable> _availableGlobalVariables = new();
        private string _deltaXLinkedVar;
        private string _deltaYLinkedVar;

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
            SaveParametersCommand = new DelegateCommand(ExecuteSaveParameters);
            LoadParametersCommand = new DelegateCommand(ExecuteLoadParameters);
            ResetParametersCommand = new DelegateCommand(ExecuteResetParameters);

            LoadCalibrationParameters(_selectedSystemNumber);

            _ = TryAutoLoadFromRecipePoolAsync().ConfigureAwait(false);
            _ = LoadGlobalVariablesAsync().ConfigureAwait(false);
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
                    LoadCalibrationParameters(_selectedSystemNumber);
                    UpdateStatus(
                        _localization.GetResource("NeedleCamera_Status_SystemSwitched", _selectedSystemNumber),
                        Brushes.LightBlue);
                }
            }
        }

        public double CameraCenterX
        {
            get => _cameraCenterX;
            set => SetProperty(ref _cameraCenterX, value);
        }

        public double CameraCenterY
        {
            get => _cameraCenterY;
            set => SetProperty(ref _cameraCenterY, value);
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
            set => SetProperty(ref _calibrationDeltaX, value);
        }

        public double CalibrationDeltaY
        {
            get => _calibrationDeltaY;
            set => SetProperty(ref _calibrationDeltaY, value);
        }

        /// <summary>可选全局变量列表</summary>
        public ObservableCollection<GlobalVariable> AvailableGlobalVariables
        {
            get => _availableGlobalVariables;
            set => SetProperty(ref _availableGlobalVariables, value);
        }

        /// <summary>X轴增量链接的全局变量名</summary>
        public string DeltaXLinkedVar
        {
            get => _deltaXLinkedVar;
            set
            {
                if (SetProperty(ref _deltaXLinkedVar, value))
                    RaisePropertyChanged(nameof(IsDeltaXLinked));
            }
        }

        /// <summary>Y轴增量链接的全局变量名</summary>
        public string DeltaYLinkedVar
        {
            get => _deltaYLinkedVar;
            set
            {
                if (SetProperty(ref _deltaYLinkedVar, value))
                    RaisePropertyChanged(nameof(IsDeltaYLinked));
            }
        }

        /// <summary>X轴增量是否已链接全局变量</summary>
        public bool IsDeltaXLinked => !string.IsNullOrEmpty(DeltaXLinkedVar);

        /// <summary>Y轴增量是否已链接全局变量</summary>
        public bool IsDeltaYLinked => !string.IsNullOrEmpty(DeltaYLinkedVar);

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

        public double CompensationZ
        {
            get => _compensationZ;
            set
            {
                if (SetProperty(ref _compensationZ, value))
                    RaisePropertyChanged(nameof(CalculatedCompZ));
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

        public string CompensationZExpression
        {
            get => _compensationZExpression;
            set
            {
                if (SetProperty(ref _compensationZExpression, value))
                    RaisePropertyChanged(nameof(CalculatedCompZ));
            }
        }

        /// <summary>计算后的CompX = CompensationX + 表达式结果</summary>
        public double CalculatedCompX => CompensationX + EvaluateExpression(CompensationXExpression);

        /// <summary>计算后的CompY = CompensationY + 表达式结果</summary>
        public double CalculatedCompY => CompensationY + EvaluateExpression(CompensationYExpression);

        /// <summary>计算后的CompZ = CompensationZ + 表达式结果</summary>
        public double CalculatedCompZ => CompensationZ + EvaluateExpression(CompensationZExpression);

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

        #endregion

        #region 命令

        public DelegateCommand TeachCameraCenterCommand { get; }
        public DelegateCommand TeachNeedleTipCommand { get; }
        public DelegateCommand SaveParametersCommand { get; }
        public DelegateCommand LoadParametersCommand { get; }
        public DelegateCommand ResetParametersCommand { get; }

        #endregion

        #region 命令实现

        /// <summary>示教相机中心：读取DispX和GantryY轴位置</summary>
        private async void ExecuteTeachCameraCenter()
        {
            try
            {
                var positions = await _motionController.TeachAsync(StationIdentifier);

                if (positions.TryGetValue("DispX", out double dispX))
                    CameraCenterX = dispX;
                if (positions.TryGetValue("GantryY", out double gantryY))
                    CameraCenterY = gantryY;

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

        /// <summary>示教针尖位置：读取DispX、GantryY和DispZ轴位置</summary>
        private async void ExecuteTeachNeedleTip()
        {
            try
            {
                var positions = await _motionController.TeachAsync(StationIdentifier);

                if (positions.TryGetValue("DispX", out double dispX))
                    NeedleTipX = dispX;
                if (positions.TryGetValue("GantryY", out double gantryY))
                    NeedleTipY = gantryY;
                if (positions.TryGetValue("DispZ", out double dispZ))
                    NeedleTipZ = dispZ;

                CalculateCalibrationDelta();

                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_NeedleTipTaught", NeedleTipX, NeedleTipY, NeedleTipZ),
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

        private void ExecuteSaveParameters()
        {
            SaveCalibrationParameters(_selectedSystemNumber);
            _ = SaveCurrentFileToRecipePoolAsync().ConfigureAwait(false);
            _ = WriteDeltaToGlobalVariablesAsync().ConfigureAwait(false);
        }

        private void ExecuteLoadParameters()
        {
            LoadCalibrationParameters(_selectedSystemNumber);
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
            CompensationZ = 0;
            CompensationXExpression = null;
            CompensationYExpression = null;
            CompensationZExpression = null;
            DeltaXLinkedVar = null;
            DeltaYLinkedVar = null;

            UpdateStatus(
                _localization.GetResource("NeedleCamera_Status_ParametersReset"),
                Brushes.LightGreen);
            _logger.Info($"系统{_selectedSystemNumber}参数已重置");
        }

        #endregion

        #region 私有方法

        /// <summary>计算相机与针尖的校准差值</summary>
        private void CalculateCalibrationDelta()
        {
            try
            {
                if (Math.Abs(CameraCenterX) > 0.001 || Math.Abs(CameraCenterY) > 0.001 ||
                    Math.Abs(NeedleTipX) > 0.001 || Math.Abs(NeedleTipY) > 0.001)
                {
                    CalibrationDeltaX = NeedleTipX - CameraCenterX;
                    CalibrationDeltaY = NeedleTipY - CameraCenterY;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"CalculateCalibrationDelta异常: {ex.Message}");
            }
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

        /// <summary>获取保存目录：Config/NeedleSystems</summary>
        private static string GetNeedleSystemsDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "NeedleSystems");
        }

        /// <summary>从存储加载标定参数</summary>
        private void LoadCalibrationParameters(int systemNumber)
        {
            try
            {
                string fileKey = systemNumber == 1
                    ? "NeedleCalibration_System1"
                    : "NeedleCalibration_System2";

                var parameters = _parameterStorage?.Load<NeedleCameraCalibrationParams>(fileKey, GetNeedleSystemsDirectory());

                if (parameters != null)
                {
                    CameraCenterX = parameters.CameraCenterX;
                    CameraCenterY = parameters.CameraCenterY;
                    NeedleTipX = parameters.NeedleTipX;
                    NeedleTipY = parameters.NeedleTipY;
                    NeedleTipZ = parameters.NeedleTipZ;
                    CalibrationDeltaX = parameters.CalibrationDeltaX;
                    CalibrationDeltaY = parameters.CalibrationDeltaY;
                    CompensationX = parameters.CompensationX;
                    CompensationY = parameters.CompensationY;
                    CompensationZ = parameters.CompensationZ;
                    CompensationXExpression = parameters.CompensationXExpression;
                    CompensationYExpression = parameters.CompensationYExpression;
                    CompensationZExpression = parameters.CompensationZExpression;
                    DeltaXLinkedVar = parameters.DeltaXLinkedVar;
                    DeltaYLinkedVar = parameters.DeltaYLinkedVar;

                    UpdateStatus(
                        _localization.GetResource("NeedleCamera_Status_LoadSuccess"),
                        Brushes.LightGreen);
                    _logger.Info($"系统{systemNumber}参数从文件加载成功");
                }
                else
                {
                    UpdateStatus(
                        _localization.GetResource("NeedleCamera_Status_NoSavedParams"),
                        Brushes.Orange);
                    _logger.Warn($"系统{systemNumber}参数文件不存在");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_LoadFailed", ex.Message),
                    Brushes.Red);
                _logger.Error($"LoadCalibrationParameters异常: {ex.Message}");
            }
        }

        /// <summary>保存标定参数到 Config/NeedleSystems 目录</summary>
        private void SaveCalibrationParameters(int systemNumber)
        {
            try
            {
                var parameters = new NeedleCameraCalibrationParams
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
                    CompensationZ = CompensationZ,
                    CompensationXExpression = CompensationXExpression,
                    CompensationYExpression = CompensationYExpression,
                    CompensationZExpression = CompensationZExpression,
                    DeltaXLinkedVar = DeltaXLinkedVar,
                    DeltaYLinkedVar = DeltaYLinkedVar,
                    LastCalibrated = DateTime.Now,
                    SystemNumber = systemNumber
                };

                string fileKey = systemNumber == 1
                    ? "NeedleCalibration_System1"
                    : "NeedleCalibration_System2";

                _parameterStorage?.Save(fileKey, parameters, GetNeedleSystemsDirectory());

                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_SaveSuccess"),
                    Brushes.LightGreen);
                _logger.Info($"系统{systemNumber}参数保存到Config/NeedleSystems");
            }
            catch (Exception ex)
            {
                UpdateStatus(
                    _localization.GetResource("NeedleCamera_Status_SaveFailed", ex.Message),
                    Brushes.Red);
                _logger.Error($"SaveCalibrationParameters异常: {ex.Message}");
            }
        }

        /// <summary>从配方池自动加载上次使用的参数文件</summary>
        private async Task TryAutoLoadFromRecipePoolAsync()
        {
            try
            {
                var poolName = _recipePoolService?.CurrentPoolName ?? "Default";
                var extData = await _recipePoolService.GetExtensionDataAsync<NeedleCameraFileRecord>(
                    poolName, $"NeedleCamera_CurrentFile_System{_selectedSystemNumber}");

                if (extData?.FilePath != null && File.Exists(extData.FilePath))
                {
                    _logger.Info($"[NeedleCamera] 从配方池记录加载: {extData.FilePath}");
                    LoadCalibrationParameters(_selectedSystemNumber);
                    return;
                }

                var defaultDir = GetNeedleSystemsDirectory();
                var defaultPath = Path.Combine(defaultDir, $"NeedleCalibration_System{_selectedSystemNumber}.json");
                if (File.Exists(defaultPath))
                {
                    _logger.Info($"[NeedleCamera] 配方池无记录，从默认路径加载: {defaultPath}");
                    LoadCalibrationParameters(_selectedSystemNumber);
                    return;
                }

                _logger.Info("[NeedleCamera] 无可加载的参数文件");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleCamera] 自动加载参数失败: {ex.Message}");
            }
        }

        /// <summary>将当前文件路径保存到配方池ExtensionData</summary>
        private async Task SaveCurrentFileToRecipePoolAsync()
        {
            try
            {
                var poolName = _recipePoolService?.CurrentPoolName ?? "Default";
                var filePath = Path.Combine(GetNeedleSystemsDirectory(), $"NeedleCalibration_System{_selectedSystemNumber}.json");
                await _recipePoolService.SetExtensionDataAsync(poolName,
                    $"NeedleCamera_CurrentFile_System{_selectedSystemNumber}",
                    new NeedleCameraFileRecord { FilePath = filePath });
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleCamera] 保存文件记录到配方池失败: {ex.Message}");
            }
        }

        /// <summary>从配方池加载全局变量列表</summary>
        private async Task LoadGlobalVariablesAsync()
        {
            try
            {
                var poolId = _recipePoolService?.CurrentPoolName ?? "Default";
                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
                AvailableGlobalVariables = new ObservableCollection<GlobalVariable>(variables);

                var dxLink = variables.FirstOrDefault(v => v.Name == "NeedleCamera_DeltaX_LinkedVar");
                var dyLink = variables.FirstOrDefault(v => v.Name == "NeedleCamera_DeltaY_LinkedVar");
                DeltaXLinkedVar = dxLink?.Value;
                DeltaYLinkedVar = dyLink?.Value;
                RaisePropertyChanged(nameof(IsDeltaXLinked));
                RaisePropertyChanged(nameof(IsDeltaYLinked));
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleCamera] 加载全局变量失败: {ex.Message}");
            }
        }

        /// <summary>将Delta值写入链接的全局变量（仅写入方向）</summary>
        private async Task WriteDeltaToGlobalVariablesAsync()
        {
            try
            {
                var poolId = _recipePoolService?.CurrentPoolName ?? "Default";
                var variables = (await _recipePoolService.LoadGlobalVariablesAsync(poolId)).ToList();

                if (!string.IsNullOrEmpty(DeltaXLinkedVar))
                    UpdateOrAddGlobalVariable(variables, DeltaXLinkedVar, CalibrationDeltaX.ToString("F6"), "针头相机X增量");
                if (!string.IsNullOrEmpty(DeltaYLinkedVar))
                    UpdateOrAddGlobalVariable(variables, DeltaYLinkedVar, CalibrationDeltaY.ToString("F6"), "针头相机Y增量");

                UpdateOrAddGlobalVariable(variables, "NeedleCamera_DeltaX_LinkedVar", DeltaXLinkedVar ?? "", "针头相机X增量链接的全局变量名");
                UpdateOrAddGlobalVariable(variables, "NeedleCamera_DeltaY_LinkedVar", DeltaYLinkedVar ?? "", "针头相机Y增量链接的全局变量名");

                for (int i = 0; i < variables.Count; i++)
                    variables[i].Index = i + 1;

                await _recipePoolService.SaveGlobalVariablesAsync(poolId, variables);
                _eventAggregator?.GetEvent<Recipe.Events.GlobalVariablesChangedEvent>()?.Publish(poolId);
            }
            catch (Exception ex)
            {
                _logger.Warn($"[NeedleCamera] 写入Delta到全局变量失败: {ex.Message}");
            }
        }

        /// <summary>更新或添加全局变量</summary>
        private static void UpdateOrAddGlobalVariable(List<GlobalVariable> variables, string name, string value, string comment)
        {
            var existing = variables.FirstOrDefault(v => v.Name == name);
            if (existing != null)
            {
                existing.Value = value;
            }
            else
            {
                variables.Add(new GlobalVariable
                {
                    Name = name,
                    Type = GlobalVariableType.Double,
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

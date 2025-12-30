using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using Prism.Commands;
using Prism.Mvvm;
using Stations;
using Stations.Services;
using System;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Module.ViewModels
{
    /// <summary>
    /// 针头标定ViewModel，用于处理针头标定相关的逻辑。
    /// </summary>
    public class NeedleCalibrationViewModel : BindableBase
    {
        private readonly DispenserStation _dispenserStation;
        private readonly IParameterStorage _parameterStorage;
        private readonly ILoggerService _logger;
        private readonly TaskInstanceManager _taskManager;
        // 针头校准属性
        private double _cameraCenterX;
        private double _cameraCenterY;
        private double _needleTipX;
        private double _needleTipY;
        private double _needleTipZ;
        private double _basePlaneZ;
        private double _targetPlaneZ;
        private double _currentNeedleHeight;
        private double _calibrationDeltaX;
        private double _calibrationDeltaY;
        private double _compensationX;
        private double _compensationY;
        private double _compensationZ;

        // 状态属性
        private string _calibrationStatusMessage = "就绪";
        private Brush _calibrationStatusColor = Brushes.LightGray;

        public NeedleCalibrationViewModel(
            TaskInstanceManager taskManager,
            IParameterStorage parameterStorage,
            ILoggerService loggerService)
        {
            _taskManager = taskManager;
            _parameterStorage = parameterStorage;
            _logger = loggerService;
            _dispenserStation = _taskManager.GetTask<DispenserStation>();
            InitializeCommands();
            LoadCalibrationParameters();
        }

        #region 属性
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
            set => SetProperty(ref _needleTipX, value);
        }

        public double NeedleTipY
        {
            get => _needleTipY;
            set => SetProperty(ref _needleTipY, value);
        }

        public double NeedleTipZ
        {
            get => _needleTipZ;
            set => SetProperty(ref _needleTipZ, value);
        }

        public double BasePlaneZ
        {
            get => _basePlaneZ;
            set => SetProperty(ref _basePlaneZ, value);
        }

        public double TargetPlaneZ
        {
            get => _targetPlaneZ;
            set => SetProperty(ref _targetPlaneZ, value);
        }

        public double CurrentNeedleHeight
        {
            get => _currentNeedleHeight;
            set => SetProperty(ref _currentNeedleHeight, value);
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

        public double CompensationX
        {
            get => _compensationX;
            set => SetProperty(ref _compensationX, value);
        }

        public double CompensationY
        {
            get => _compensationY;
            set => SetProperty(ref _compensationY, value);
        }

        public double CompensationZ
        {
            get => _compensationZ;
            set => SetProperty(ref _compensationZ, value);
        }

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
        public DelegateCommand TeachCameraCenterCommand { get; private set; }
        public DelegateCommand TeachNeedleTipCommand { get; private set; }
        public DelegateCommand TeachNeedleTipZCommand { get; private set; }
        public DelegateCommand CalculateCurrentNeedleHeightCommand { get; private set; }
        public DelegateCommand ApplyCompensationCommand { get; private set; }
        public DelegateCommand AutoCalculateCompensationCommand { get; private set; }
        public DelegateCommand LoadCalibrationParametersCommand { get; private set; }
        public DelegateCommand SaveCalibrationParametersCommand { get; private set; }
        public DelegateCommand ResetCalibrationCommand { get; private set; }
        #endregion

        #region 初始化方法
        private void InitializeCommands()
        {
            TeachCameraCenterCommand = new DelegateCommand(TeachCameraCenter);
            TeachNeedleTipCommand = new DelegateCommand(TeachNeedleTip);
            TeachNeedleTipZCommand = new DelegateCommand(TeachNeedleTipZ);
            CalculateCurrentNeedleHeightCommand = new DelegateCommand(CalculateCurrentNeedleHeight);
            ApplyCompensationCommand = new DelegateCommand(ApplyCompensation);
            AutoCalculateCompensationCommand = new DelegateCommand(AutoCalculateCompensation);
            LoadCalibrationParametersCommand = new DelegateCommand(LoadCalibrationParameters);
            SaveCalibrationParametersCommand = new DelegateCommand(SaveCalibrationParameters);
            ResetCalibrationCommand = new DelegateCommand(ResetCalibration);
        }
        #endregion

        #region 命令实现方法
        private void TeachCameraCenter()
        {
            try
            {
                // 获取当前DispX和PlatY轴的坐标
                double dispX = _dispenserStation.GetAxisPosition(_dispenserStation.DispX.ActId);
                double platY = _dispenserStation.GetAxisPosition(_dispenserStation.DispY_1.ActId);

                CameraCenterX = dispX;
                CameraCenterY = platY;

                UpdateStatus($"示教相机中心坐标: DispX={dispX:F3}, PlatY={platY:F3}", Brushes.LightGreen);
                CalculateCalibrationDelta();
            }
            catch (Exception ex)
            {
                UpdateStatus($"示教相机中心异常: {ex.Message}", Brushes.Red);
                _logger.Error($"TeachCameraCenter异常: {ex.Message}");
            }
        }

        private void TeachNeedleTip()
        {
            try
            {
                // 获取当前DispX、PlatY和DispZ2轴的坐标
                double dispX = _dispenserStation.GetAxisPosition(_dispenserStation.DispX.ActId);
                double platY = _dispenserStation.GetAxisPosition(_dispenserStation.DispY_1.ActId);
                double dispZ2 = _dispenserStation.GetAxisPosition(_dispenserStation.DispZ2.ActId);

                NeedleTipX = dispX;
                NeedleTipY = platY;
                NeedleTipZ = dispZ2;

                UpdateStatus($"示教针尖坐标: DispX={dispX:F3}, PlatY={platY:F3}, DispZ2={dispZ2:F3}", Brushes.LightGreen);
                CalculateCalibrationDelta();
            }
            catch (Exception ex)
            {
                UpdateStatus($"示教针尖位置异常: {ex.Message}", Brushes.Red);
                _logger.Error($"TeachNeedleTip异常: {ex.Message}");
            }
        }

        private void TeachNeedleTipZ()
        {
            try
            {
                // 获取当前DispZ2轴的坐标作为针尖高度
                double dispZ2 = _dispenserStation.GetAxisPosition(_dispenserStation.DispZ2.ActId);
                NeedleTipZ = dispZ2;

                UpdateStatus($"示教针尖高度: {dispZ2:F3}mm", Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                UpdateStatus($"示教针尖高度异常: {ex.Message}", Brushes.Red);
                _logger.Error($"TeachNeedleTipZ异常: {ex.Message}");
            }
        }

        private void CalculateCurrentNeedleHeight()
        {
            try
            {
                if (Math.Abs(BasePlaneZ) < 0.001 || Math.Abs(TargetPlaneZ) < 0.001)
                {
                    UpdateStatus("请先设置基准面高度和目标平面高度", Brushes.Orange);
                    return;
                }

                // 计算方法：
                // 当前针头高度 = 目标平面高度 + (针尖高度 - 基准面高度)
                // 即：当前高度会随着目标平面高度变化而变化
                double heightDifference = TargetPlaneZ - BasePlaneZ ;
                CurrentNeedleHeight = NeedleTipZ - heightDifference + CompensationZ;

                //UpdateStatus($"计算完成: 当前针头高度 = {CurrentNeedleHeight:F3}mm (目标{Z - F3} + 差值{heightDifference:F3})", Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                UpdateStatus($"计算当前针头高度异常: {ex.Message}", Brushes.Red);
                _logger.Error($"CalculateCurrentNeedleHeight异常: {ex.Message}");
            }
        }

        private void CalculateCalibrationDelta()
        {
            try
            {
                if (CameraCenterX != 0 && CameraCenterY != 0 && NeedleTipX != 0 && NeedleTipY != 0)
                {
                    CalibrationDeltaX = NeedleTipX - CameraCenterX;
                    CalibrationDeltaY = NeedleTipY - CameraCenterY;

                    UpdateStatus($"计算相机与针尖距离: ΔX={CalibrationDeltaX:F3}, ΔY={CalibrationDeltaY:F3}", Brushes.LightGreen);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"计算校准差值异常: {ex.Message}", Brushes.Red);
                _logger.Error($"CalculateCalibrationDelta异常: {ex.Message}");
            }
        }

        private void ApplyCompensation()
        {
            try
            {
                // 应用补偿值到运动控制
                // 这里调用实际的运动控制接口应用补偿
                // ApplyCompensationToMotionControl(CompensationX, CompensationY, CompensationZ);

                UpdateStatus($"应用补偿值: X={CompensationX:F3}, Y={CompensationY:F3}, Z={CompensationZ:F3}", Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                UpdateStatus($"应用补偿异常: {ex.Message}", Brushes.Red);
                _logger.Error($"ApplyCompensation异常: {ex.Message}");
            }
        }

        private void AutoCalculateCompensation()
        {
            try
            {
                // 自动计算补偿值（基于校准数据）
                CompensationX = -CalibrationDeltaX;
                CompensationY = -CalibrationDeltaY;
                CompensationZ = 0; // Z补偿通常需要单独设置

                UpdateStatus($"自动计算补偿值: X={CompensationX:F3}, Y={CompensationY:F3}", Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                UpdateStatus($"自动计算补偿异常: {ex.Message}", Brushes.Red);
                _logger.Error($"AutoCalculateCompensation异常: {ex.Message}");
            }
        }

        private void LoadCalibrationParameters()
        {
            try
            {
                // 使用支持自定义路径的重载方法
                string customDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                        "Config",
                                        "Calibration");
                var parameters = _parameterStorage?.Load<NeedleCalibrationParameters>(
                    "NeedleCalibration",
                    customDirectory);

                if (parameters != null)
                {
                    CameraCenterX = parameters.CameraCenterX;
                    CameraCenterY = parameters.CameraCenterY;
                    NeedleTipX = parameters.NeedleTipX;
                    NeedleTipY = parameters.NeedleTipY;
                    NeedleTipZ = parameters.NeedleTipZ;
                    BasePlaneZ = parameters.BasePlaneZ;
                    TargetPlaneZ = parameters.TargetPlaneZ;
                    CompensationX = parameters.CompensationX;
                    CompensationY = parameters.CompensationY;
                    CompensationZ = parameters.CompensationZ;
                    CalibrationDeltaX = parameters.CalibrationDeltaX;
                    CalibrationDeltaY = parameters.CalibrationDeltaY;

                    // 加载后自动计算当前针头高度
                    if (Math.Abs(BasePlaneZ) > 0.001 && Math.Abs(TargetPlaneZ) > 0.001)
                    {
                        double heightDifference = NeedleTipZ - BasePlaneZ;
                        CurrentNeedleHeight = TargetPlaneZ + heightDifference;
                    }

                    CalculateCalibrationDelta();
                    UpdateStatus("针头校准参数加载成功", Brushes.LightGreen);
                }
                else
                {
                    UpdateStatus("未找到针头校准参数，使用默认值", Brushes.Orange);
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"加载针头校准参数异常: {ex.Message}", Brushes.Red);
                _logger.Error($"LoadCalibrationParameters异常: {ex.Message}");
            }
        }

        private void SaveCalibrationParameters()
        {
            try
            {
                var parameters = new NeedleCalibrationParameters
                {
                    CameraCenterX = CameraCenterX,
                    CameraCenterY = CameraCenterY,
                    NeedleTipX = NeedleTipX,
                    NeedleTipY = NeedleTipY,
                    NeedleTipZ = NeedleTipZ,
                    BasePlaneZ = BasePlaneZ,
                    TargetPlaneZ = TargetPlaneZ,
                    CompensationX = CompensationX,
                    CompensationY = CompensationY,
                    CompensationZ = CompensationZ,
                    CalibrationDeltaX = CalibrationDeltaX,
                    CalibrationDeltaY = CalibrationDeltaY,
                    LastCalibrated = DateTime.Now
                };

                // 使用支持自定义路径的重载方法
                string customDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                                        "Config",
                                                        "Calibration");
                _parameterStorage?.Save("NeedleCalibration", parameters, customDirectory);
                UpdateStatus("针头校准参数保存成功", Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                UpdateStatus($"保存针头校准参数异常: {ex.Message}", Brushes.Red);
                _logger.Error($"SaveCalibrationParameters异常: {ex.Message}");
            }
        }

        private void ResetCalibration()
        {
            try
            {
                CameraCenterX = 0;
                CameraCenterY = 0;
                NeedleTipX = 0;
                NeedleTipY = 0;
                NeedleTipZ = 0;
                BasePlaneZ = 0;
                TargetPlaneZ = 0;
                CurrentNeedleHeight = 0;
                CalibrationDeltaX = 0;
                CalibrationDeltaY = 0;
                CompensationX = 0;
                CompensationY = 0;
                CompensationZ = 0;

                UpdateStatus("针头校准数据已重置", Brushes.LightGreen);
            }
            catch (Exception ex)
            {
                UpdateStatus($"重置校准数据异常: {ex.Message}", Brushes.Red);
                _logger.Error($"ResetCalibration异常: {ex.Message}");
            }
        }
        #endregion

        #region 辅助方法
        private void UpdateStatus(string message, Brush color)
        {
            CalibrationStatusMessage = message;
            CalibrationStatusColor = color;
        }
        #endregion
    }

    #region 数据模型
    public class NeedleCalibrationParameters
    {
        public double CameraCenterX { get; set; }
        public double CameraCenterY { get; set; }
        public double NeedleTipX { get; set; }
        public double NeedleTipY { get; set; }
        public double NeedleTipZ { get; set; }
        public double BasePlaneZ { get; set; }
        public double TargetPlaneZ { get; set; }
        public double CompensationX { get; set; }
        public double CompensationY { get; set; }
        public double CompensationZ { get; set; }
        public double CalibrationDeltaX { get; set; }
        public double CalibrationDeltaY { get; set; }
        public DateTime LastCalibrated { get; set; }
    }
    #endregion
}
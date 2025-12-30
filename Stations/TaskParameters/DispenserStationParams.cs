
using Core.Abstraction;
using Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Stations.TaskParameters
{
    public class DispenserStationParams : TaskParametersBase, INotifyPropertyChanged
    {
        public override string Identifier => "DispenserStation";

        private string _taskName = "Dispenser Station";
        [Category("基本信息")]
        [DisplayName("任务名称")]
        [Description("当前任务的名称")]
        public override string TaskName
        {
            get => _taskName;
            set => SetProperty(ref _taskName, value);
        }

        private int _taskId = 2;
        [Category("基本信息")]
        [DisplayName("任务ID")]
        [Description("当前任务的唯一ID")]
        [DisplayFormat(DataFormatString = "F0")]
        [ReadOnly(true)]
        public override int TaskId
        {
            get => _taskId;
            set => SetProperty(ref _taskId, value);
        }

        // 3D相机参数
        private double _cameraExposureTime = 50.0;
        private double _cameraGain = 1.5;
        private int _cameraTriggerDelay = 100;
        private double _cameraZOffset = 0.0;
        private double _cameraFOVLength = 100.0;
        private double _cameraFOVHeight = 80.0;

        // 点胶阀参数
        private double _dispensePressure = 0.3;
        private int _dispenseTime = 500;
        private double _suckBackPressure = -0.1;
        private int _suckBackTime = 100;
        private double _valveOpenTime = 20.0;
        private double _valveCloseTime = 15.0;
        private int _dispenseCycleCount = 1;
        private int _cleaningTime = 100;

        // 保持原属性但添加适当的特性
        [Category("点胶路径")]
        [DisplayName("点胶路径")]
        [Description("点胶路径点集合")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public List<PointF> DispensingPath { get; set; } = new List<PointF>();

        // 激光对针传感器参数
        private double _laserThreshold = 2.5;
        private int _laserStableTime = 50;
        private double _needleHeightOffset = 5.0;
        private double _laserDetectionRange = 10.0;

        // 运动控制参数
        private double _dispenseSpeed = 10.0;
        private double _dispenseAcceleration = 0.5;
        private double _safeZHeight = 30.0;
        private double _approachHeight = 5.0;
        private double _dispenseHeight = 1.0;
        private double _xyClearance = 2.0;
        private int _dispensingInterval = 1;

        // 工艺参数
        private double _glueDotDiameter = 1.0;
        private double _glueDotHeight = 0.5;
        private int _qualityCheckInterval = 10;
        private bool _enableAutoCalibration = true;
        private int _calibrationInterval = 1000;
        private int _uvFixTime = 20; // UV灯开启时间

        // 3D标定参数
        private double _rStepAngle = 10.0;
        private int _rScanCount = 36;
        private double _uStepAngle = 5.0;
        private int _uScanCountPerSide = 5;
        private double _calibrationScanSpeed = 10.0;
        private double _calibrationStableTime = 200.0;
        private bool _enableCalibrationValidation = true;
        private double _calibrationTolerance = 0.1;

        // 针头校准参数
        //private NeedleCalibrationParams _needleCalibrationParams = new NeedleCalibrationParams();

        public event PropertyChangedEventHandler PropertyChanged;

        // 在类中添加以下方法来解决序列化问题
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string DispensingPathSerialized
        {
            get
            {
                if (DispensingPath == null || DispensingPath.Count == 0)
                    return string.Empty;

                return string.Join(";", DispensingPath.Select(p => $"{p.X:F3},{p.Y:F3}"));
            }
            set
            {
                DispensingPath.Clear();
                if (!string.IsNullOrEmpty(value))
                {
                    var points = value.Split(';');
                    foreach (var point in points)
                    {
                        var coords = point.Split(',');
                        if (coords.Length == 2 &&
                            float.TryParse(coords[0], out float x) &&
                            float.TryParse(coords[1], out float y))
                        {
                            DispensingPath.Add(new PointF(x, y));
                        }
                    }
                }
            }
        }

        #region 3D相机参数
        [Category("3D相机设置")]
        [DisplayName("曝光时间 (ms)")]
        [Description("3D相机曝光时间")]
        [Range(1.0, 500.0)]
        public double CameraExposureTime
        {
            get => _cameraExposureTime;
            set => SetProperty(ref _cameraExposureTime, value);
        }

        [Category("3D相机设置")]
        [DisplayName("增益")]
        [Description("3D相机增益系数")]
        [Range(1.0, 10.0)]
        public double CameraGain
        {
            get => _cameraGain;
            set => SetProperty(ref _cameraGain, value);
        }

        [Category("3D相机设置")]
        [DisplayName("触发延迟 (ms)")]
        [Description("相机触发延迟时间")]
        [Range(0, 1000)]
        public int CameraTriggerDelay
        {
            get => _cameraTriggerDelay;
            set => SetProperty(ref _cameraTriggerDelay, value);
        }

        [Category("3D相机设置")]
        [DisplayName("Z轴偏移 (mm)")]
        [Description("相机Z轴测量偏移量")]
        [Range(-10.0, 10.0)]
        public double CameraZOffset
        {
            get => _cameraZOffset;
            set => SetProperty(ref _cameraZOffset, value);
        }

        [Category("3D相机设置")]
        [DisplayName("视野长度 (mm)")]
        [Description("相机扫描视野长度")]
        [Range(50.0, 200.0)]
        public double CameraFOVLength
        {
            get => _cameraFOVLength;
            set => SetProperty(ref _cameraFOVLength, value);
        }

        [Category("3D相机设置")]
        [DisplayName("视野高度 (mm)")]
        [Description("相机视野高度")]
        [Range(40.0, 150.0)]
        public double CameraFOVHeight
        {
            get => _cameraFOVHeight;
            set => SetProperty(ref _cameraFOVHeight, value);
        }
        #endregion

        #region 点胶阀参数
        [Category("点胶阀控制")]
        [DisplayName("点胶压力 (MPa)")]
        [Description("点胶时气压设定值")]
        [Range(0.1, 1.0)]
        public double DispensingPressure
        {
            get => _dispensePressure;
            set => SetProperty(ref _dispensePressure, value);
        }

        [Category("点胶阀控制")]
        [DisplayName("点胶时间 (ms)")]
        [Description("单次点胶持续时间")]
        [Range(10, 2000)]
        public int DispensingTime
        {
            get => _dispenseTime;
            set => SetProperty(ref _dispenseTime, value);
        }
        [Category("点胶阀控制")]
        [DisplayName("点胶移动速度 (mm/s)")]
        [Description("点胶运动速度时间")]
        [Range(10, 100)]
        public double DispensingMoveSpeed
        {
            get => _dispenseSpeed;
            set => SetProperty(ref _dispenseSpeed, value);
        }
        [Category("点胶阀控制")]
        [DisplayName("回吸压力 (MPa)")]
        [Description("防止滴胶的回吸压力")]
        [Range(-0.5, 0.0)]
        public double DispensingVacuum
        {
            get => _suckBackPressure;
            set => SetProperty(ref _suckBackPressure, value);
        }

        [Category("点胶阀控制")]
        [DisplayName("回吸时间 (ms)")]
        [Description("回吸动作持续时间")]
        [Range(10, 500)]
        public int SuckBackTime
        {
            get => _suckBackTime;
            set => SetProperty(ref _suckBackTime, value);
        }

        [Category("点胶阀控制")]
        [DisplayName("阀开启时间 (ms)")]
        [Description("点胶阀开启响应时间")]
        [Range(5.0, 50.0)]
        public double ValveOpenTime
        {
            get => _valveOpenTime;
            set => SetProperty(ref _valveOpenTime, value);
        }

        [Category("点胶阀控制")]
        [DisplayName("阀关闭时间 (ms)")]
        [Description("点胶阀关闭响应时间")]
        [Range(5.0, 50.0)]
        public double ValveCloseTime
        {
            get => _valveCloseTime;
            set => SetProperty(ref _valveCloseTime, value);
        }

        [Category("点胶阀控制")]
        [DisplayName("点胶循环次数")]
        [Description("每个点的点胶循环次数")]
        [Range(1, 10)]
        public int DispenseCycleCount
        {
            get => _dispenseCycleCount;
            set => SetProperty(ref _dispenseCycleCount, value);
        }
        [Category("点胶阀控制")]
        [DisplayName("点胶清洁时间 (ms)")]
        [Description("点胶阀清洁时间，用于清除残留胶")]
        public int CleaningTime
        {
            get => _cleaningTime;
            set => SetProperty(ref _cleaningTime, value);
        }

        #endregion

        #region 激光对针传感器参数
        [Category("激光对针传感器")]
        [DisplayName("触发阈值 (V)")]
        [Description("激光传感器触发阈值电压")]
        [Range(0.1, 5.0)]
        public double LaserThreshold
        {
            get => _laserThreshold;
            set => SetProperty(ref _laserThreshold, value);
        }

        [Category("激光对针传感器")]
        [DisplayName("稳定时间 (ms)")]
        [Description("激光信号稳定检测时间")]
        [Range(10, 200)]
        public int LaserStableTime
        {
            get => _laserStableTime;
            set => SetProperty(ref _laserStableTime, value);
        }

        [Category("激光对针传感器")]
        [DisplayName("针头高度偏移 (mm)")]
        [Description("针头相对于激光焦点的偏移高度")]
        [Range(0.0, 20.0)]
        public double NeedleHeightOffset
        {
            get => _needleHeightOffset;
            set => SetProperty(ref _needleHeightOffset, value);
        }

        [Category("激光对针传感器")]
        [DisplayName("检测范围 (mm)")]
        [Description("激光有效检测范围")]
        [Range(1.0, 50.0)]
        public double LaserDetectionRange
        {
            get => _laserDetectionRange;
            set => SetProperty(ref _laserDetectionRange, value);
        }
        //[DisplayName("针头校准设置")]
        //[Description("针头校准相关参数配置")]
        //[Category("设备校准")]  
        //public NeedleCalibrationParams NeedleCalibration
        //{
        //    get => _needleCalibrationParams;
        //    set => SetProperty(ref _needleCalibrationParams, value);
        //}
        #endregion

        #region 运动控制参数
        [Category("运动控制")]
        [DisplayName("点胶速度 (mm/s)")]
        [Description("点胶过程中的移动速度")]
        [Range(1.0, 50.0)]
        public double DispenseSpeed
        {
            get => _dispenseSpeed;
            set => SetProperty(ref _dispenseSpeed, value);
        }

        [Category("运动控制")]
        [DisplayName("加速度 (mm/s²)")]
        [Description("运动轴加速度")]
        [Range(0.1, 5.0)]
        public double DispenseAcceleration
        {
            get => _dispenseAcceleration;
            set => SetProperty(ref _dispenseAcceleration, value);
        }

        [Category("运动控制")]
        [DisplayName("接近高度 (mm)")]
        [Description("接近工件的高度")]
        [Range(1.0, 20.0)]
        public double ApproachHeight
        {
            get => _approachHeight;
            set => SetProperty(ref _approachHeight, value);
        }

        [Category("运动控制")]
        [DisplayName("点胶高度 (mm)")]
        [Description("点胶时针头离工件表面的高度")]
        [Range(0.1, 5.0)]
        public double DispenseHeight
        {
            get => _dispenseHeight;
            set => SetProperty(ref _dispenseHeight, value);
        }

        [Category("运动控制")]
        [DisplayName("XY安全间隙 (mm)")]
        [Description("XY方向上的安全移动间隙")]
        [Range(0.5, 10.0)]
        public double XYClearance
        {
            get => _xyClearance;
            set => SetProperty(ref _xyClearance, value);
        }
        [Category("运动控制")]
        [DisplayName("点胶间隔时间 (ms)")]
        [Description("XY方向上的安全移动间隔时间")]
        public int DispensingInterval
        {
            get => _dispensingInterval;
            set => SetProperty(ref _dispensingInterval, value);
        }
        // ipqc第1次拍照z轴高度
        private double _ipqcfirstphotozheight = 1.0;
        [Category("运动控制")]
        [DisplayName("IPQC第一次拍照Z轴高度 (mm)")]
        [Description("IPQC第一次拍照时，针头相对于工件表面的高度")]
        [Range(0.1, 35.0)]
        public double IPQCFirstPhotoZHeight
        {
            get => _ipqcfirstphotozheight;
            set => SetProperty(ref _ipqcfirstphotozheight, value);
        }
        // ipqc第2次拍照z轴高度
        private double _ipqctwophotozheight = 1.0;
        [Category("运动控制")]
        [DisplayName("IPQC第二次拍照Z轴高度 (mm)")]
        [Description("IPQC第二次拍照时，针头相对于工件表面的高度")]
        [Range(0.1, 35.0)]
        public double IPQCSecondPhotoZHeight
        {
            get => _ipqctwophotozheight;
            set => SetProperty(ref _ipqctwophotozheight, value);
        }
        // Pillar的基准角度补偿值
        private double _pillarbaseanglecompensation = 0.3;
        [Category("运动控制")]
        [DisplayName("Pillar基准角度补偿值 (°)")]
        [Description("Pillar的基准角度补偿值，用于调整针头相对于工件的角度")]
        [Range(-180.0, 180.0)]
        public double PillarBaseAngleCompensation
        {
            get => _pillarbaseanglecompensation;
            set => SetProperty(ref _pillarbaseanglecompensation, value);
        }
        #endregion

        #region 工艺参数
        [Category("工艺参数")]
        [DisplayName("胶点直径 (mm)")]
        [Description("目标胶点直径")]
        [Range(0.1, 5.0)]
        public double GlueDotDiameter
        {
            get => _glueDotDiameter;
            set => SetProperty(ref _glueDotDiameter, value);
        }

        [Category("工艺参数")]
        [DisplayName("胶点高度 (mm)")]
        [Description("目标胶点高度")]
        [Range(0.1, 3.0)]
        public double GlueDotHeight
        {
            get => _glueDotHeight;
            set => SetProperty(ref _glueDotHeight, value);
        }

        [Category("工艺参数")]
        [DisplayName("质量检测间隔")]
        [Description("每多少个点进行一次质量检测")]
        [Range(1, 100)]
        public int QualityCheckInterval
        {
            get => _qualityCheckInterval;
            set => SetProperty(ref _qualityCheckInterval, value);
        }

        [Category("工艺参数")]
        [DisplayName("启用自动校准")]
        [Description("是否启用自动校准功能")]
        public bool EnableAutoCalibration
        {
            get => _enableAutoCalibration;
            set => SetProperty(ref _enableAutoCalibration, value);
        }

        [Category("工艺参数")]
        [DisplayName("校准间隔")]
        [Description("自动校准的执行间隔（点胶次数）")]
        [Range(100, 10000)]
        public int CalibrationInterval
        {
            get => _calibrationInterval;
            set => SetProperty(ref _calibrationInterval, value);
        }
        /// <summary>
        /// UV固化时间 (ms)
        /// </summary>
        [Category("工艺参数")]
        [DisplayName("UV固化时间 (ms)")]
        [Description("胶点固化所需的时间")]
        [Range(0, 100)]
        public int UVFixTime
        {
            get => _uvFixTime;
            set => SetProperty(ref _uvFixTime, value);
        }
        // 在 DispenserStationParams.cs 中添加一个只读的显示属性
        [Category("点胶路径")]
        [DisplayName("路径点数")]
        [Description("点胶路径中的点数")]
        [ReadOnly(true)]
        public int DispensingPathPointCount
        {
            get => DispensingPath?.Count ?? 0;
        }

        [Category("点胶路径")]
        [DisplayName("路径范围")]
        [Description("点胶路径的坐标范围")]
        [ReadOnly(true)]
        public string DispensingPathRange
        {
            get
            {
                if (DispensingPath == null || DispensingPath.Count == 0)
                    return "无路径";

                var minX = DispensingPath.Min(p => p.X);
                var maxX = DispensingPath.Max(p => p.X);
                var minY = DispensingPath.Min(p => p.Y);
                var maxY = DispensingPath.Max(p => p.Y);

                return $"X: {minX:F1}~{maxX:F1}, Y: {minY:F1}~{maxY:F1}";
            }
        }
        // Pillar的基准角度
        private double _pillarBaseAngle = 0.3;
        [Category("装配精度")]
        [DisplayName("Pillar基准角度 (°)")]
        [Description("Pillar的安装基准角度")]
        [Range(-180.0, 180.0)]
        public double PillarBaseAngle
        {
            get => _pillarBaseAngle;
            set => SetProperty(ref _pillarBaseAngle, value);
        }
        // Pillar角度允许的误差范围
        private double _pillarAngleTolerance = 0.5;
        [Category("装配精度")]
        [DisplayName("Pillar角度允许误差 (°)")]
        [Description("Pillar的安装允许偏差")]
        [Range(0.01, 10.0)]
        public double PillarAngleTolerance
        {
            get => _pillarAngleTolerance;
            set => SetProperty(ref _pillarAngleTolerance, value);
        }

        private double _tabMaxOffsetX = 0.5;
        [Category("装配精度")]
        [DisplayName("Tab最大偏移量 (mm)")]
        [Description("Tab在X轴方向上的最大允许偏差")]
        [Range(0.1, 5.0)]
        public double TabMaxOffsetX
        {
            get => _tabMaxOffsetX;
            set => SetProperty(ref _tabMaxOffsetX, value);
        }
        private double _tabMaxOffsetY = 0.5;
        [Category("装配精度")]
        [DisplayName("Tab最大偏移量 (mm)")]
        [Description("Tab在Y轴方向上的最大允许偏差")]
        [Range(0.1, 5.0)]
        public double TabMaxOffsetY
        {
            get => _tabMaxOffsetY;
            set => SetProperty(ref _tabMaxOffsetY, value);
        }
        private double _tabMaxOffsetZ = 0.5;
        [Category("装配精度")]
        [DisplayName("Tab最大偏移量 (mm)")]
        [Description("Tab在Z轴方向上的最大允许偏差")]
        [Range(0.1, 5.0)]
        public double TabMaxOffsetZ
        {
            get => _tabMaxOffsetZ;
            set => SetProperty(ref _tabMaxOffsetZ, value);
        }
        // 最小补偿阈值
        private double _minTabCompensationThreshold = 0.02;
        [Category("装配精度")]
        [DisplayName("最小补偿阈值 (mm)")]
        [Description("当偏差小于此值时，不进行补偿")]
        [Range(0.01, 1.0)]
        public double MinTabCompensationThreshold
        {
            get => _minTabCompensationThreshold;
            set => SetProperty(ref _minTabCompensationThreshold, value);
        }
        private double _maxAllowableTabError = 0.5;
        [Category("装配精度")]
        [DisplayName("最大允许Tab误差 (mm)")]
        [Description("Tab在XYZ方向上的最大允许偏差")]
        [Range(0.1, 5.0)]
        public double MaxAllowableTabError
        {
            get => _maxAllowableTabError;
            set => SetProperty(ref _maxAllowableTabError, value);
        }
        // IPQCTolerance
        private double _ipqcTolerance = 0.05;
        [Category("装配精度")]
        [DisplayName("IPQ   允许误差 (mm)")]
        [Description("IPQ在Z轴方向上的允许偏差")]
        [Range(0.01, 0.5)]
        public double IPQCTolerance
        {
            get => _ipqcTolerance;
            set => SetProperty(ref _ipqcTolerance, value);
        }
        // IPQCRelativeTolerance
        private double _ipqcRelativeTolerance = 0.05;
        [Category("装配精度")]
        [DisplayName("IPQC相对允许误差 (mm)")]
        [Description("相对于标准位置的IPQC的Z轴方向上的允许偏差")]
        [Range(0.01, 0.5)]
        public double IPQCRelativeTolerance
        {
            get => _ipqcRelativeTolerance;
            set => SetProperty(ref _ipqcRelativeTolerance, value);
        }
        // StopOnIPQCFailure
        private bool _stopOnIPQCFailure = true;
        [Category("装配精度")]
        [DisplayName("IPQC失败时停止")]
        [Description("当IPQC检测到偏差超过允许范围时，是否立即停止操作")]
        public bool StopOnIPQCFailure
        {
            get => _stopOnIPQCFailure;
            set => SetProperty(ref _stopOnIPQCFailure, value);
        }
        #endregion

        #region 3D标定参数
        [Category("3D标定设置")]
        [DisplayName("R轴步进角度 (°)")]
        [Description("R轴每次旋转的角度")]
        [Range(1.0, 30.0)]
        public double RStepAngle
        {
            get => _rStepAngle;
            set => SetProperty(ref _rStepAngle, value);
        }

        [Category("3D标定设置")]
        [DisplayName("R轴扫描次数")]
        [Description("R轴完整扫描的拍照次数")]
        [Range(12, 72)]
        public int RScanCount
        {
            get => _rScanCount;
            set => SetProperty(ref _rScanCount, value);
        }

        [Category("3D标定设置")]
        [DisplayName("U轴步进角度 (°)")]
        [Description("U轴每次旋转的角度")]
        [Range(1.0, 15.0)]
        public double UStepAngle
        {
            get => _uStepAngle;
            set => SetProperty(ref _uStepAngle, value);
        }

        [Category("3D标定设置")]
        [DisplayName("U轴单边扫描次数")]
        [Description("U轴每边（正向/负向）的扫描次数")]
        [Range(1, 10)]
        public int UScanCountPerSide
        {
            get => _uScanCountPerSide;
            set => SetProperty(ref _uScanCountPerSide, value);
        }

        [Category("3D标定设置")]
        [DisplayName("标定扫描速度 (°/s)")]
        [Description("标定过程中轴的旋转速度")]
        [Range(1.0, 50.0)]
        public double CalibrationScanSpeed
        {
            get => _calibrationScanSpeed;
            set => SetProperty(ref _calibrationScanSpeed, value);
        }

        [Category("3D标定设置")]
        [DisplayName("稳定等待时间 (ms)")]
        [Description("每次移动后的稳定等待时间")]
        [Range(50, 1000)]
        public double CalibrationStableTime
        {
            get => _calibrationStableTime;
            set => SetProperty(ref _calibrationStableTime, value);
        }

        [Category("3D标定设置")]
        [DisplayName("启用标定验证")]
        [Description("是否在标定完成后进行验证")]
        public bool EnableCalibrationValidation
        {
            get => _enableCalibrationValidation;
            set => SetProperty(ref _enableCalibrationValidation, value);
        }

        [Category("3D标定设置")]
        [DisplayName("标定容差 (°)")]
        [Description("标定位置精度容差")]
        [Range(0.01, 1.0)]
        public double CalibrationTolerance
        {
            get => _calibrationTolerance;
            set => SetProperty(ref _calibrationTolerance, value);
        }
        #endregion

        protected virtual void SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return;

            storage = value;
            OnPropertyChanged(propertyName);
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}

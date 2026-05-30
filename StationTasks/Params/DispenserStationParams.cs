
using Core.Abstraction;
using Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.CompilerServices;

namespace StationTasks.Params
{
    public class DispenserStationParams : TaskParametersBase, INotifyPropertyChanged
    {
        public override string Identifier => "DispenserStation";

        private string _taskName = "Dispenser Station";
        [Category("Basic Information")]
        [DisplayName("Task Name")]
        [Description("Name of the current task")]
        public override string TaskName
        {
            get => _taskName;
            set => SetProperty(ref _taskName, value);
        }

        private int _taskId = 2;
        [Category("Basic Information")]
        [DisplayName("Task ID")]
        [Description("Unique ID of the current task")]
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
        [Category("Dispensing path")]
        [DisplayName("Dispensing path")]
        [Description("Dispensing Path Point Set")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public List<PointF> DispensingPath { get; set; } = new List<PointF>();

        // 轨迹段集合——包含离散化采样点和工艺参数
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public List<DispenseSegment> Segments { get; set; } = new List<DispenseSegment>();

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

        /// <summary>
        /// 轨迹段集合的 JSON 序列化字符串——用于配方系统的 JSON 持久化
        /// 写入时将 Segments 列表序列化为 JSON 字符串
        /// 读取时将 JSON 字符串反序列化为 Segments 列表
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string SegmentsSerialized
        {
            get
            {
                if (Segments == null || Segments.Count == 0)
                    return string.Empty;
                return System.Text.Json.JsonSerializer.Serialize(Segments, _segmentsJsonOptions);
            }
            set
            {
                Segments = new List<DispenseSegment>();
                if (!string.IsNullOrEmpty(value))
                {
                    try
                    {
                        var deserialized = System.Text.Json.JsonSerializer.Deserialize<List<DispenseSegment>>(value, _segmentsJsonOptions);
                        if (deserialized != null)
                            Segments = deserialized;
                    }
                    catch (System.Text.Json.JsonException ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DispenserStationParams] Segments 反序列化失败: {ex.Message}");
                    }
                }
            }
        }

        private static readonly System.Text.Json.JsonSerializerOptions _segmentsJsonOptions = new()
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private string _lastSegmentConfigPath = string.Empty;

        [Category("Dispensing path")]
        [DisplayName("Last Segment Config Path")]
        [Description("最后一次加载/保存轨迹段的配置文件路径")]
        [Browsable(false)]
        public string LastSegmentConfigPath
        {
            get => _lastSegmentConfigPath;
            set => SetProperty(ref _lastSegmentConfigPath, value);
        }

        #region 3D相机参数

        [Category("3D Camera Settings")]
        [DisplayName("Field of view length (mm)")]
        [Description("Camera scan field of view length")]
        [Range(50.0, 200.0)]
        public double CameraFOVLength
        {
            get => _cameraFOVLength;
            set => SetProperty(ref _cameraFOVLength, value);
        }

        [Category("3D Camera Settings")]
        [DisplayName("Scan Height (mm)")]
        [Description("Camera scanning height")]
        [Range(40.0, 150.0)]
        public double CameraFOVHeight
        {
            get => _cameraFOVHeight;
            set => SetProperty(ref _cameraFOVHeight, value);
        }
        #endregion

        #region 点胶阀参数
        [Category("Dispensing Valve Control")]
        [DisplayName("Dispensing pressure (MPa)")]
        [Description("点胶时气压设定值")]
        [Range(0.1, 1.0)]
        public double DispensingPressure
        {
            get => _dispensePressure;
            set => SetProperty(ref _dispensePressure, value);
        }

        [Category("Dispensing Valve Control")]
        [DisplayName("Dispensing time (ms)")]
        [Description("单次点胶持续时间")]
        [Range(10, 2000)]
        public int DispensingTime
        {
            get => _dispenseTime;
            set => SetProperty(ref _dispenseTime, value);
        }
        [Category("Dispensing Valve Control")]
        [DisplayName("Dispensing Movement Speed (mm/s)")]
        [Description("点胶运动速度时间")]
        [Range(10, 100)]
        public double DispensingMoveSpeed
        {
            get => _dispenseSpeed;
            set => SetProperty(ref _dispenseSpeed, value);
        }
        [Category("Dispensing Valve Control")]
        [DisplayName("Back Pressure (MPa)")]
        [Description("防止滴胶的回吸压力")]
        [Range(-0.5, 0.0)]
        public double DispensingVacuum
        {
            get => _suckBackPressure;
            set => SetProperty(ref _suckBackPressure, value);
        }

        [Category("Dispensing Valve Control")]
        [DisplayName("Back-suction time (ms)")]
        [Description("回吸动作持续时间")]
        [Range(10, 500)]
        public int SuckBackTime
        {
            get => _suckBackTime;
            set => SetProperty(ref _suckBackTime, value);
        }

        [Category("Dispensing Valve Control")]
        [DisplayName("Valve opening time (ms)")]
        [Description("点胶阀开启响应时间")]
        [Range(5.0, 50.0)]
        public double ValveOpenTime
        {
            get => _valveOpenTime;
            set => SetProperty(ref _valveOpenTime, value);
        }

        [Category("Dispensing Valve Control")]
        [DisplayName("Valve closing time (ms)")]
        [Description("点胶阀关闭响应时间")]
        [Range(5.0, 50.0)]
        public double ValveCloseTime
        {
            get => _valveCloseTime;
            set => SetProperty(ref _valveCloseTime, value);
        }

        [Category("Dispensing Valve Control")]
        [DisplayName("Dispensing cycle count")]
        [Description("每个点的点胶循环次数")]
        [Range(1, 10)]
        public int DispenseCycleCount
        {
            get => _dispenseCycleCount;
            set => SetProperty(ref _dispenseCycleCount, value);
        }
        [Category("Dispensing Valve Control")]
        [DisplayName("Dispensing Cleaning Time (ms)")]
        [Description("点胶阀清洁时间，用于清除残留胶")]
        public int CleaningTime
        {
            get => _cleaningTime;
            set => SetProperty(ref _cleaningTime, value);
        }

        #endregion

        #region 运动控制参数
        [Category("Motion Control")]
        [DisplayName("Dispensing speed (mm/s)")]
        [Description("点胶过程中的移动速度")]
        [Range(1.0, 50.0)]
        public double DispenseSpeed
        {
            get => _dispenseSpeed;
            set => SetProperty(ref _dispenseSpeed, value);
        }

        [Category("Motion Control")]
        [DisplayName("Dispensing height (mm)")]
        [Description("点胶时针头离工件表面的高度")]
        [Range(0.1, 5.0)]
        public double DispenseHeight
        {
            get => _dispenseHeight;
            set => SetProperty(ref _dispenseHeight, value);
        }

        // 3D 扫描时的速度
        private double _scanSpeed = 30;
        [Category("Motion Control")]
        [DisplayName("3D scanning speed (mm/s)")]
        [Description("3D扫描过程中的移动速度")]
        [Range(1.0, 60.0)]
        public double ScanSpeed
        {
            get => _scanSpeed;
            set => SetProperty(ref _scanSpeed, value);
        }

        #endregion

        #region 工艺参数

        [Category("Process Parameters")]
        [DisplayName("UV Curing Time (ms)")]
        [Description("胶点固化所需的时间")]
        [Range(0, 100)]
        public int UVFixTime
        {
            get => _uvFixTime;
            set => SetProperty(ref _uvFixTime, value);
        }

        // Pillar角度允许的误差范围
        private double _pillarAngleTolerance = 0.5;
        [Category("Assembly accuracy")]
        [DisplayName("Pillar Angle Tolerance (°)")]
        [Description("Pillar的安装允许偏差")]
        [Range(0.01, 10.0)]
        public double PillarAngleTolerance
        {
            get => _pillarAngleTolerance;
            set => SetProperty(ref _pillarAngleTolerance, value);
        }

        private double _tabMaxOffsetX = 0.5;
        [Category("Assembly accuracy")]
        [DisplayName("Maximum tab offset X(mm)")]
        [Description("Tab在X轴方向上的最大允许偏差")]
        [Range(0.1, 10.0)]
        public double TabMaxOffsetX
        {
            get => _tabMaxOffsetX;
            set => SetProperty(ref _tabMaxOffsetX, value);
        }

        private double _tabMaxOffsetY = 0.5;
        [Category("Assembly accuracy")]
        [DisplayName("Maximum tab offset Y(mm)")]
        [Description("Tab在Y轴方向上的最大允许偏差")]
        [Range(0.1, 10.0)]
        public double TabMaxOffsetY
        {
            get => _tabMaxOffsetY;
            set => SetProperty(ref _tabMaxOffsetY, value);
        }

        private double _tabMaxOffsetZ = 0.5;
        [Category("Assembly accuracy")]
        [DisplayName("Maximum tab offset Z(mm)")]
        [Description("Tab在Z轴方向上的最大允许偏差")]
        [Range(0.1, 10.0)]
        public double TabMaxOffsetZ
        {
            get => _tabMaxOffsetZ;
            set => SetProperty(ref _tabMaxOffsetZ, value);
        }

        // 最小补偿阈值
        private double _minTabCompensationThreshold = 0.02;
        [Category("Assembly accuracy")]
        [DisplayName("Minimum Tab Compensation Threshold (mm)")]
        [Description("当偏差小于此值时，不进行补偿")]
        [Range(0.01, 1.0)]
        public double MinTabCompensationThreshold
        {
            get => _minTabCompensationThreshold;
            set => SetProperty(ref _minTabCompensationThreshold, value);
        }

        private double _maxAllowableTabError = 0.5;
        [Category("Assembly accuracy")]
        [DisplayName("Maximum Allowable Tab Tolerance XY (mm)")]
        [Description("Tab在XY方向上的最大允许偏差")]
        [Range(0.1, 5.0)]
        public double MaxAllowableTabError
        {
            get => _maxAllowableTabError;
            set => SetProperty(ref _maxAllowableTabError, value);
        }

        // IPQCTolerance
        private double _ipqcTolerance = 0.05;
        [Category("Assembly accuracy")]
        [DisplayName("IPQC Tolerance (mm)")]
        [Description("IPQ在Z轴方向上的允许偏差")]
        [Range(0.01, 0.5)]
        public double IPQCTolerance
        {
            get => _ipqcTolerance;
            set => SetProperty(ref _ipqcTolerance, value);
        }

        // StopOnIPQCFailure
        private bool _stopOnIPQCFailure = true;
        [Category("Assembly accuracy")]
        [DisplayName("Stop when IPQC fails")]
        [Description("当IPQC检测到偏差超过允许范围时，是否立即停止操作")]
        public bool StopOnIPQCFailure
        {
            get => _stopOnIPQCFailure;
            set => SetProperty(ref _stopOnIPQCFailure, value);
        }

        #endregion

        #region IPQC

        // IPQC基准值 Group1
        private double _IPQCBaseValue1X_1 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 1 X-1(mm)")]
        [Description("IPQC的第一个基准位置X坐标")]
        public double IPQCBaseValue1X_1
        {
            get => _IPQCBaseValue1X_1;
            set => SetProperty(ref _IPQCBaseValue1X_1, value);
        }
        private double _IPQCBaseValue1Y_1 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 1 Y-1(mm)")]
        [Description("IPQC的第一个基准位置Y坐标")]
        public double IPQCBaseValue1Y_1
        {
            get => _IPQCBaseValue1Y_1;
            set => SetProperty(ref _IPQCBaseValue1Y_1, value);
        }

        private double _IPQCBaseValue1X_2 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 1 X-2(mm)")]
        [Description("IPQC的第一个基准位置X坐标")]
        public double IPQCBaseValue1X_2
        {
            get => _IPQCBaseValue1X_2;
            set => SetProperty(ref _IPQCBaseValue1X_2, value);
        }
        private double _IPQCBaseValue1Y_2 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 1 Y-2(mm)")]
        [Description("IPQC的第一个基准位置Y坐标")]
        public double IPQCBaseValue1Y_2
        {
            get => _IPQCBaseValue1Y_2;
            set => SetProperty(ref _IPQCBaseValue1Y_2, value);
        }

        // IPQC基准值 Group2
        private double _IPQCBaseValue2X_1 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 2 X-1(mm)")]
        [Description("IPQC的第二个基准位置X坐标")]
        public double IPQCBaseValue2X
        {
            get => _IPQCBaseValue2X_1;
            set => SetProperty(ref _IPQCBaseValue2X_1, value);
        }
        private double _IPQCBaseValue2Y_1 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 2 Y-1(mm)")]
        [Description("IPQC的第二个基准位置Y坐标")]
        public double IPQCBaseValue2Y
        {
            get => _IPQCBaseValue2Y_1;
            set => SetProperty(ref _IPQCBaseValue2Y_1, value);
        }

        private double _IPQCBaseValue2X_2 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 2 X-2(mm)")]
        [Description("IPQC的第二个基准位置X坐标")]
        public double IPQCBaseValue2X_2
        {
            get => _IPQCBaseValue2X_2;
            set => SetProperty(ref _IPQCBaseValue2X_2, value);
        }
        private double _IPQCBaseValue2Y_2 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 2 Y-2(mm)")]
        [Description("IPQC的第二个基准位置Y坐标")]
        public double IPQCBaseValue2Y_2
        {
            get => _IPQCBaseValue2Y_2;
            set => SetProperty(ref _IPQCBaseValue2Y_2, value);
        }


        // IPQC基准值 Group3
        private double _IPQCBaseValue3X_1 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 3 X-1(mm)")]
        [Description("IPQC的第三个基准位置X坐标")]
        public double IPQCBaseValue3X_1
        {
            get => _IPQCBaseValue3X_1;
            set => SetProperty(ref _IPQCBaseValue3X_1, value);
        }
        private double _IPQCBaseValue3Y_1 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 3 Y-1(mm)")]
        [Description("IPQC的第三个基准位置Y坐标")]
        public double IPQCBaseValue3Y_1
        {
            get => _IPQCBaseValue3Y_1;
            set => SetProperty(ref _IPQCBaseValue3Y_1, value);
        }
        private double _IPQCBaseValue3X_2 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 3 X-2(mm)")]
        [Description("IPQC的第三个基准位置X坐标")]
        public double IPQCBaseValue3X_2
        {
            get => _IPQCBaseValue3X_2;
            set => SetProperty(ref _IPQCBaseValue3X_2, value);
        }
        private double _IPQCBaseValue3Y_2 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 3 Y-2(mm)")]
        [Description("IPQC的第三个基准位置Y坐标")]
        public double IPQCBaseValue3Y_2
        {
            get => _IPQCBaseValue3Y_2;
            set => SetProperty(ref _IPQCBaseValue3Y_2, value);
        }

        // IPQC基准值 Group4
        private double _IPQCBaseValue4X_1 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 4 X-1(mm)")]
        [Description("IPQC的第四个基准位置X坐标")]
        public double IPQCBaseValue4X_1
        {
            get => _IPQCBaseValue4X_1;
            set => SetProperty(ref _IPQCBaseValue4X_1, value);
        }
        private double _IPQCBaseValue4Y_1 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 4 Y-1(mm)")]
        [Description("IPQC的第四个基准位置Y坐标")]
        public double IPQCBaseValue4Y_1
        {
            get => _IPQCBaseValue4Y_1;
            set => SetProperty(ref _IPQCBaseValue4Y_1, value);
        }
        private double _IPQCBaseValue4X_2 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 4 X-2(mm)")]
        [Description("IPQC的第四个基准位置X坐标")]
        public double IPQCBaseValue4X_2
        {
            get => _IPQCBaseValue4X_2;
            set => SetProperty(ref _IPQCBaseValue4X_2, value);
        }
        private double _IPQCBaseValue4Y_2 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 4 Y-2(mm)")]
        [Description("IPQC的第四个基准位置Y坐标")]
        public double IPQCBaseValue4Y_2
        {
            get => _IPQCBaseValue4Y_2;
            set => SetProperty(ref _IPQCBaseValue4Y_2, value);
        }

        // IPQC基准值 Group5
        private double _IPQCBaseValue5X_1 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 5 X-1(mm)")]
        [Description("IPQC的第五个基准位置X坐标")]
        public double IPQCBaseValue5X_1
        {
            get => _IPQCBaseValue5X_1;
            set => SetProperty(ref _IPQCBaseValue5X_1, value);
        }
        private double _IPQCBaseValue5Y_1 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 5 Y-1(mm)")]
        [Description("IPQC的第五个基准位置Y坐标")]
        public double IPQCBaseValue5Y_1
        {
            get => _IPQCBaseValue5Y_1;
            set => SetProperty(ref _IPQCBaseValue5Y_1, value);
        }
        private double _IPQCBaseValue5X_2 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 5 X-2(mm)")]
        [Description("IPQC的第五个基准位置X坐标")]
        public double IPQCBaseValue5X_2
        {
            get => _IPQCBaseValue5X_2;
            set => SetProperty(ref _IPQCBaseValue5X_2, value);
        }
        private double _IPQCBaseValue5Y_2 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 5 Y-2(mm)")]
        [Description("IPQC的第五个基准位置Y坐标")]
        public double IPQCBaseValue5Y_2
        {
            get => _IPQCBaseValue5Y_2;
            set => SetProperty(ref _IPQCBaseValue5Y_2, value);
        }

        // IPQC基准值 Group6
        private double _IPQCBaseValue6X_1 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 6 X-1 (mm)")]
        [Description("IPQC的第六个基准位置X坐标")]
        public double IPQCBaseValue6X_1
        {
            get => _IPQCBaseValue6X_1;
            set => SetProperty(ref _IPQCBaseValue6X_1, value);
        }
        private double _IPQCBaseValue6Y_1 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 6 Y-1 (mm)")]
        [Description("IPQC的第六个基准位置Y坐标")]
        public double IPQCBaseValue6Y_1
        {
            get => _IPQCBaseValue6Y_1;
            set => SetProperty(ref _IPQCBaseValue6Y_1, value);
        }
        private double _IPQCBaseValue6X_2 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 6 X-2 (mm)")]
        [Description("IPQC的第六个基准位置X坐标")]
        public double IPQCBaseValue6X_2
        {
            get => _IPQCBaseValue6X_2;
            set => SetProperty(ref _IPQCBaseValue6X_2, value);
        }
        private double _IPQCBaseValue6Y_2 = 0.0;
        [Category("IPQC")]
        [DisplayName("IPQC Reference Value 6 Y-2 (mm)")]
        [Description("IPQC的第六个基准位置Y坐标")]
        public double IPQCBaseValue6Y_2
        {
            get => _IPQCBaseValue6Y_2;
            set => SetProperty(ref _IPQCBaseValue6Y_2, value);
        }

        // 公差X
        private double _IPQCToleranceX = 0.0;
        [Category("IPQC")]
        [DisplayName("Tolerance X (mm)")]
        [Description("相对于基准位置的IPQC的X轴方向上的允许偏差")]
        public double IPQCToleranceX
        {
            get => _IPQCToleranceX;
            set => SetProperty(ref _IPQCToleranceX, value);
        }
        // 公差Y
        private double _IPQCToleranceY = 0.0;
        [Category("IPQC")]
        [DisplayName("Tolerance Y (mm)")]
        [Description("相对于基准位置的IPQC的Y轴方向上的允许偏差")]
        public double IPQCToleranceY
        {
            get => _IPQCToleranceY;
            set => SetProperty(ref _IPQCToleranceY, value);
        }

        private bool _enableManualIPQCConfirmation = false;

        [Category("IPQC Manual verification")]
        [DisplayName("Enable manual confirmation for IPQC")]
        [Description("当第一组IPQC检测通过后，弹出人工确认窗口让操作员确认")]
        public bool EnableManualIPQCConfirmation
        {
            get => _enableManualIPQCConfirmation;
            set => SetProperty(ref _enableManualIPQCConfirmation, value);
        }

        private string _manualConfirmationMessage = "第1组IPQC检测已通过，请人工确认结果";
        [Category("IPQC Manual verification")]
        [DisplayName("Confirmation prompt message")]
        [Description("人工确认窗口中显示的提示消息")]
        public string ManualConfirmationMessage
        {
            get => _manualConfirmationMessage;
            set => SetProperty(ref _manualConfirmationMessage, value);
        }

        private int _manualConfirmationTimeout = 30;
        [Category("IPQC Manual verification")]
        [DisplayName("Confirm timeout duration (seconds)")]
        [Description("人工确认窗口等待操作员确认的超时时间")]
        [Range(10, 300)]
        public int ManualConfirmationTimeout
        {
            get => _manualConfirmationTimeout;
            set => SetProperty(ref _manualConfirmationTimeout, value);
        }

        private bool _requireOperatorSignature = true;
        [Category("IPQC Manual verification")]
        [DisplayName("Operator signature required")]
        [Description("人工确认时是否需要操作员输入签名")]
        public bool RequireOperatorSignature
        {
            get => _requireOperatorSignature;
            set => SetProperty(ref _requireOperatorSignature, value);
        }

        private string _defaultOperatorName = "操作员";
        [Category("IPQC Manual verification")]
        [DisplayName("Default Operator Name")]
        [Description("默认的操作员姓名，可在确认时修改")]
        public string DefaultOperatorName
        {
            get => _defaultOperatorName;
            set => SetProperty(ref _defaultOperatorName, value);
        }

        #endregion

        #region 3D标定参数
        [Category("3D Calibration Settings")]
        [DisplayName("R-axis step angle (°)")]
        [Description("R轴每次旋转的角度")]
        [Range(1.0, 30.0)]
        public double RStepAngle
        {
            get => _rStepAngle;
            set => SetProperty(ref _rStepAngle, value);
        }

        [Category("3D Calibration Settings")]
        [DisplayName("Number of R-axis scans")]
        [Description("R轴完整扫描的拍照次数")]
        [Range(12, 72)]
        public int RScanCount
        {
            get => _rScanCount;
            set => SetProperty(ref _rScanCount, value);
        }

        [Category("3D Calibration Settings")]
        [DisplayName("U-axis step angle (°)")]
        [Description("U轴每次旋转的角度")]
        [Range(1.0, 15.0)]
        public double UStepAngle
        {
            get => _uStepAngle;
            set => SetProperty(ref _uStepAngle, value);
        }

        [Category("3D Calibration Settings")]
        [DisplayName("Number of single-side scans on the U-axis")]
        [Description("U轴每边（正向/负向）的扫描次数")]
        [Range(1, 10)]
        public int UScanCountPerSide
        {
            get => _uScanCountPerSide;
            set => SetProperty(ref _uScanCountPerSide, value);
        }

        [Category("3D Calibration Settings")]
        [DisplayName("Calibrated scan speed (°/s)")]
        [Description("标定过程中轴的旋转速度")]
        [Range(1.0, 50.0)]
        public double CalibrationScanSpeed
        {
            get => _calibrationScanSpeed;
            set => SetProperty(ref _calibrationScanSpeed, value);
        }

        [Category("3D Calibration Settings")]
        [DisplayName("Stable waiting time (ms)")]
        [Description("每次移动后的稳定等待时间")]
        [Range(50, 1000)]
        public double CalibrationStableTime
        {
            get => _calibrationStableTime;
            set => SetProperty(ref _calibrationStableTime, value);
        }

        [Category("3D Calibration Settings")]
        [DisplayName("Enable Calibration Verification")]
        [Description("是否在标定完成后进行验证")]
        public bool EnableCalibrationValidation
        {
            get => _enableCalibrationValidation;
            set => SetProperty(ref _enableCalibrationValidation, value);
        }

        [Category("3D Calibration Settings")]
        [DisplayName("Calibration tolerance (°)")]
        [Description("标定位置精度容差")]
        [Range(0.01, 1.0)]
        public double CalibrationTolerance
        {
            get => _calibrationTolerance;
            set => SetProperty(ref _calibrationTolerance, value);
        }
        #endregion

        #region 属性

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

        #endregion

        [ParameterIgnore]
        public List<GlobalVariable> GlobalVariables { get; set; } = new List<GlobalVariable>();
        [ParameterIgnore]

        public Dictionary<string, FlexiblePosition> Positions { get; set; } = new Dictionary<string, FlexiblePosition>();
        public DispenserStationParams()
        {
            // 确保字典至少包含两个默认位置
            if (!Positions.ContainsKey("StandbyPosition"))
                Positions["StandbyPosition"] = new FlexiblePosition();
            if (!Positions.ContainsKey("SafePosition"))
                Positions["SafePosition"] = new FlexiblePosition();
        }
    }
}

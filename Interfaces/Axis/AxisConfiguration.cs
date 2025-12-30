using Prism.Mvvm;
using System.Collections.Generic;
using System.Linq;

namespace AxisConfiguration.Models
{
    public enum LogicLevel
    {
        High,
        Low
    }
    // 控制卡信息类
    public class CardInfo
    {
        public int CardId { get; set; }
        public string Description { get; set; }

        public override string ToString() => Description;
    }
    public class AxisInSystem 
    {
        public string Name { get; set; }
        public string ConfigId { get; set; }
        public int SetCardId { get; set; }
        public int SetAxisId { get; set; }
        public string DisplayCardInfo => $"[卡:{SetCardId}]轴:{SetAxisId}";
    }
    public class AxisInfo : BindableBase
    {
        public int CardId { get; }
        public int AxisId { get; }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string ConfigId => $"{CardId}-{AxisId}";

        private string _description;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private AxisParams _params = new AxisParams();
        public AxisParams Params
        {
            get => _params;
            set => SetProperty(ref _params, value);
        }
        public AxisInfo(int cardId, int axisId, string name = null)
        {
            CardId = cardId;
            AxisId = axisId;
            Name = !string.IsNullOrEmpty(name) ? name : $"轴 {CardId}-{AxisId}";
        }
    }
    public class EmergencyStopConfig : BindableBase
    {
        private bool _enabled = true;
        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        private LogicLevel _logicLevel = LogicLevel.Low;
        public LogicLevel LogicLevel
        {
            get => _logicLevel;
            set => SetProperty(ref _logicLevel, value);
        }

        private MappedIO _mappedIO;
        public MappedIO MappedIO
        {
            get => _mappedIO;
            set => SetProperty(ref _mappedIO, value);
        }
    }

    public class MappedIO : BindableBase
    {
        public int SetId { get; set; }
        public string PortName { get; set; }
        public string Description { get; set; }

        private short _ioType;
        public short IoType
        {
            get => _ioType;
            set => SetProperty(ref _ioType, value);
        }

        private short _mapIoType;
        public short MapIoType
        {
            get => _mapIoType;
            set => SetProperty(ref _mapIoType, value);
        }

        private short _mapIoIndex;
        public short MapIoIndex
        {
            get => _mapIoIndex;
            set => SetProperty(ref _mapIoIndex, value);
        }

        private double _filterTime;
        public double FilterTime
        {
            get => _filterTime;
            set => SetProperty(ref _filterTime, value);
        }

        // 获取IO类型
        public string IoTypeDescription => IoType switch
        {
            3 => "急停信号 (AxisIoInMsg_EMG)",
            4 => "减速停止信号 (AxisIoInMsg_DSTP)",
            _ => $"未知信号类型 ({IoType})"
        };

        // 获取映射类型
        public string MapTypeDescription => MapIoType switch
        {
            6 => "通用输入端口 (AxisIoInPort_IO)",
            _ => $"未知映射类型 ({MapIoType})"
        };

        public override string ToString() =>
            $"{PortName} (MapIoIndex: {MapIoIndex}, {(IoType == 3 ? "急停" : IoType == 4 ? "减速" : "其他")})";
    }

    public class HomingConfig : BindableBase
    {
        private double _lowSpeed = 0.5;
        public double LowSpeed
        {
            get => _lowSpeed;
            set => SetProperty(ref _lowSpeed, value);
        }

        private double _highSpeed = 5.0;
        public double HighSpeed
        {
            get => _highSpeed;
            set => SetProperty(ref _highSpeed, value);
        }

        private double _accelerationTime = 0.1;
        public double AccelerationTime
        {
            get => _accelerationTime;
            set => SetProperty(ref _accelerationTime, value);
        }

        private double _decelerationTime = 0.1;
        public double DecelerationTime
        {
            get => _decelerationTime;
            set => SetProperty(ref _decelerationTime, value);
        }

        private int _mode = 1;
        public int Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }

        private double _offset;
        public double Offset
        {
            get => _offset;
            set => SetProperty(ref _offset, value);
        }
    }

    public class MotionConfig : BindableBase
    {
        private double _startSpeed = 0;
        public double StartSpeed
        {
            get => _startSpeed;
            set => SetProperty(ref _startSpeed, value);
        }

        private double _maxSpeed = 10.0;
        public double MaxSpeed
        {
            get => _maxSpeed;
            set => SetProperty(ref _maxSpeed, value);
        }

        private double _accelerationTime = 0.1;
        public double AccelerationTime
        {
            get => _accelerationTime;
            set => SetProperty(ref _accelerationTime, value);
        }

        private double _decelerationTime = 0.1;
        public double DecelerationTime
        {
            get => _decelerationTime;
            set => SetProperty(ref _decelerationTime, value);
        }

        private double _stopSpeed = 0.1;
        public double StopSpeed
        {
            get => _stopSpeed;
            set => SetProperty(ref _stopSpeed, value);
        }

        private double _sProfileTime = 0.1;
        public double SProfileTime
        {
            get => _sProfileTime;
            set => SetProperty(ref _sProfileTime, value);
        }

        private double _decStopTime = 0.1;
        public double DecStopTime
        {
            get => _decStopTime;
            set => SetProperty(ref _decStopTime, value);
        }
    }
    public class AxisParams : BindableBase
    {
        private double _pulsePerUnit = 1;
        public double PulsePerUnit
        {
            get => _pulsePerUnit;
            set => SetProperty(ref _pulsePerUnit, value);
        }

        private EmergencyStopConfig _emergencyStop = new EmergencyStopConfig();
        public EmergencyStopConfig EmergencyStop
        {
            get => _emergencyStop;
            set => SetProperty(ref _emergencyStop, value);
        }

        private HomingConfig _homing = new HomingConfig();
        public HomingConfig Homing
        {
            get => _homing;
            set => SetProperty(ref _homing, value);
        }

        private MotionConfig _motion = new MotionConfig();
        public MotionConfig Motion
        {
            get => _motion;
            set => SetProperty(ref _motion, value);
        }
    }
    public class InterpolationSystem
    {
        public int ActCardId { get; set; }
        public int CoordId { get; set; }

        /// <summary>
        /// 包含的轴标识列表 (格式: "卡号-轴号")
        /// </summary>
        public List<string> Axes { get; set; } = new List<string>();

        public InterpolationParams Params { get; set; } = new InterpolationParams();
        // 用于显示的轴信息
        public string AxesInfo => Axes.Any()
            ? $"包含轴: {string.Join(", ", Axes)}"
            : "未分配轴";
    }
    public class InterpolationParams
    {
        public double StartVelocity { get; set; }
        public double InterpolationVelocity { get; set; }
        public double EndVelocity { get; set; }
        public double AccelerationTime { get; set; }
        public double DecelerationTime { get; set; }
        public double SProfileTime { get; set; }
        public double DecelerationStopTime { get; set; }

        // 提供默认值
        public InterpolationParams()
        {
            StartVelocity = 5.0;
            InterpolationVelocity = 50.0;
            EndVelocity = 5.0;
            AccelerationTime = 0.1;
            DecelerationTime = 0.1;
            SProfileTime = 0.1;
            DecelerationStopTime = 0.1;
        }
    }

}

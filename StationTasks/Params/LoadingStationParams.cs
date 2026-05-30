using Core.Abstraction;
using Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace StationTasks.Params
{
    public class LoadingStationParams : TaskParametersBase, INotifyPropertyChanged
    {
        // 重写基类属性
        public override string Identifier => $"LoadingStation";

        private string _taskName = "Loading Station";
        [Category("基本信息")]
        [DisplayName("任务名称")]
        [Description("当前任务的名称")]
        public override string TaskName
        {
            get => _taskName;
            set => SetProperty(ref _taskName, value);
        }

        private int _taskId = 1;
        [Category("基本信息")]
        [DisplayName("任务ID")]
        [Description("当前任务的唯一ID")]
        [DisplayFormat(DataFormatString = "F0")] // 指定零位小数
        [ReadOnly(true)] // 添加只读特性
        public override int TaskId
        {
            get => _taskId;
            set => SetProperty(ref _taskId, value);
        }
        private int _pickDelayTime = 500;
        private int _placeDelayTime = 500;
        private int _breakVacuumTime = 200;
        private int _dispensingTime = 100;
        private int _uvCuringTime = 1000;
        private int _pickupCompleteTimeout = 30000;
        private int _photoCompleteTimeout = 6000;
        private double _yAxisSpeed = 50;
        private double _uAxisSpeed = 15;
        private double _rAxisSpeed = 15;
        private double _materialCheckTimeout = 5000;
        private double _vacuumBuildTimeout = 3000;
        private double _assemblyReadyTimeout = 30000;
        private double _conveyorSpeed = 10.0;
        private double _conveyorAcceleration = 0.5;
        private int _maxLoadCount = 100;
        private int _vacuumDetectTimeout = 1000;
        private double _yAxisSafePosition = 50.0;
        private double _uAxisAngle = 0.0;
        private VacuumControlMode _vacuumControlMode = VacuumControlMode.Auto;

        public event PropertyChangedEventHandler PropertyChanged;

        [Category("时间参数"), DisplayName("拾取延迟时间(ms)")]
        public int PickDelayTime
        {
            get => _pickDelayTime;
            set => SetProperty(ref _pickDelayTime, value);
        }

        [Category("时间参数"), DisplayName("放置延迟时间(ms)")]
        public int PlaceDelayTime
        {
            get => _placeDelayTime;
            set => SetProperty(ref _placeDelayTime, value);
        }

        [Category("时间参数"), DisplayName("破真空时间(ms)")]
        public int BreakVacuumTime
        {
            get => _breakVacuumTime;
            set => SetProperty(ref _breakVacuumTime, value);
        }
        [Category("时间参数"), DisplayName("拾取完成超时(ms)")]
        public int PickupCompleteTimeout
        {
            get => _pickupCompleteTimeout;
            set => SetProperty(ref _pickupCompleteTimeout, value);
        }
        [Category("时间参数"), DisplayName("拍照完成超时(ms)")]
        public int PhotoCompleteTimeout
        {
            get => _photoCompleteTimeout;
            set => SetProperty(ref _photoCompleteTimeout, value);
        }
        [Category("工艺参数"), DisplayName("点胶时间(ms)")]
        public int DispensingTime
        {
            get => _dispensingTime;
            set => SetProperty(ref _dispensingTime, value);
        }

        [Category("时间参数"), DisplayName("UV固化时间(ms)")]
        public int UvCuringTime
        {
            get => _uvCuringTime;
            set => SetProperty(ref _uvCuringTime, value);
        }
        [Category("轴速度参数"), DisplayName("Y轴速度(mm/s)")]
        public double YAxisSpeed
        {
            get => _yAxisSpeed;
            set => SetProperty(ref _yAxisSpeed, value);
        }

        [Category("轴速度参数"), DisplayName("U轴速度(deg/s)")]
        public double UAxisSpeed
        {
            get => _uAxisSpeed;
            set => SetProperty(ref _uAxisSpeed, value);
        }

        [Category("轴速度参数"), DisplayName("R轴速度(deg/s)")]
        public double RAxisSpeed
        {
            get => _rAxisSpeed;
            set => SetProperty(ref _rAxisSpeed, value);
        }

        [Category("超时参数"), DisplayName("物料检查超时(ms)")]
        public double MaterialCheckTimeout
        {
            get => _materialCheckTimeout;
            set => SetProperty(ref _materialCheckTimeout, value);
        }

        [Category("超时参数"), DisplayName("真空建立超时(ms)")]
        public double VacuumBuildTimeout
        {
            get => _vacuumBuildTimeout;
            set => SetProperty(ref _vacuumBuildTimeout, value);
        }

        [Category("超时参数"), DisplayName("装配就绪超时(ms)")]
        public double AssemblyReadyTimeout
        {
            get => _assemblyReadyTimeout;
            set => SetProperty(ref _assemblyReadyTimeout, value);
        }

        [Category("传送带设置")]
        [DisplayName("传送带速度 (m/s)")]
        [Description("控制物料传送速度的设定值")]
        [Range(0.1, 20.0)]
        public double ConveyorSpeed
        {
            get => _conveyorSpeed;
            set => SetProperty(ref _conveyorSpeed, value);
        }

        [Category("传送带设置")]
        [DisplayName("加速度 (m/s²)")]
        [Description("传送带加速度设定值")]
        [DisplayFormat(DataFormatString = "F1")] // 指定一位小数 
        [Range(0.1, 5.0)]
        public double ConveyorAcceleration
        {
            get => _conveyorAcceleration;
            set => SetProperty(ref _conveyorAcceleration, value);
        }

        [Category("运行参数")]
        [DisplayName("最大装载数量")]
        [Description("单次运行最大装载数量")]
        [Range(1, 500)]
        public int MaxLoadCount
        {
            get => _maxLoadCount;
            set => SetProperty(ref _maxLoadCount, value);
        }

        [Category("真空系统")]
        [DisplayName("真空检测超时 (ms)")]
        [Description("真空检测等待超时时间")]
        [Range(100, 5000)]
        public int VacuumDetectTimeout
        {
            get => _vacuumDetectTimeout;
            set => SetProperty(ref _vacuumDetectTimeout, value);
        }

        [Category("位置控制")]
        [DisplayName("Y轴安全位置 (mm)")]
        [Description("Y轴安全停留位置")]
        [Range(0, 200)]
        public double YAxisSafePosition
        {
            get => _yAxisSafePosition;
            set => SetProperty(ref _yAxisSafePosition, value);
        }

        [Category("位置控制")]
        [DisplayName("U轴角度 (°)")]
        [Description("U轴夹持角度")]
        [DisplayFormat(DataFormatString = "F2")] // 指定一位小数
        [Range(-45.0, 45.0)]
        public double UAxisAngle
        {
            get => _uAxisAngle;
            set => SetProperty(ref _uAxisAngle, value);
        }

        [Category("真空系统")]
        [DisplayName("真空控制模式")]
        [Description("真空吸嘴的控制模式")]
        public VacuumControlMode VacuumControlMode
        {
            get => _vacuumControlMode;
            set => SetProperty(ref _vacuumControlMode, value);
        }

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
        [ParameterIgnore]
        public List<GlobalVariable> GlobalVariables { get; set; } = new List<GlobalVariable>();
        [ParameterIgnore]
        // 所有位置点
        public Dictionary<string, FlexiblePosition> Positions { get; set; } = new Dictionary<string, FlexiblePosition>();

        // 构造函数中初始化默认位置
        public LoadingStationParams()
        {
            // 确保字典至少包含两个默认位置
            if (!Positions.ContainsKey("StandbyPosition"))
                Positions["StandbyPosition"] = new FlexiblePosition();
            if (!Positions.ContainsKey("SafePosition"))
                Positions["SafePosition"] = new FlexiblePosition();
        }
    }

    public enum VacuumControlMode
    {
        [Description("自动控制")]
        Auto,
        [Description("手动控制")]
        Manual,
        [Description("测试模式")]
        Test
    }

    public static class EnumExtensions
    {
        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute))
                as DescriptionAttribute;
            return attribute?.Description ?? value.ToString();
        }
    }
}

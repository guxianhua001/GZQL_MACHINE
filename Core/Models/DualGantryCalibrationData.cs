using Core.Services;
using Prism.Mvvm;
using System.Collections.Generic;

namespace Core.Models
{
    /// <summary>
    /// 双龙门标定完整数据模型——用于序列化/反序列化标定配置、双龙门点位、
    /// 公共基准点及仿射标定结果
    /// </summary>
    public class DualGantryCalibrationData
    {
        /// <summary>双龙门标定机构配置</summary>
        public DualGantryCalibrationConfig Config { get; set; } = new DualGantryCalibrationConfig();

        /// <summary>龙门1标定点列表</summary>
        public List<DualGantryCalibrationPoint> Gantry1Points { get; set; } = new List<DualGantryCalibrationPoint>();

        /// <summary>龙门2标定点列表</summary>
        public List<DualGantryCalibrationPoint> Gantry2Points { get; set; } = new List<DualGantryCalibrationPoint>();

        /// <summary>公共基准点列表（共用Y轴上的对齐基准）</summary>
        public List<CommonReferencePoint> CommonReferencePoints { get; set; } = new List<CommonReferencePoint>();

        /// <summary>龙门1仿射标定结果（视觉→机械）</summary>
        public AffineCalibrationResult? Gantry1CalibrationResult { get; set; }

        /// <summary>龙门2仿射标定结果（视觉→机械）</summary>
        public AffineCalibrationResult? Gantry2CalibrationResult { get; set; }

        /// <summary>跨龙门变换参数（龙门1→龙门2）</summary>
        public GantryTransform? GantryTransform { get; set; }
    }

    /// <summary>
    /// 公共基准点模型——固定在机架上的对齐基准点，
    /// 分步记录双相机视觉坐标与各自采集时的共用Y轴坐标，用于求解跨龙门变换参数
    /// 继承BindableBase支持WPF双向绑定
    /// </summary>
    public class CommonReferencePoint : BindableBase
    {
        private int _index;
        /// <summary>序号</summary>
        public int Index { get => _index; set => SetProperty(ref _index, value); }

        private double _commonY1;
        /// <summary>Cam1采集时的共用Y轴机械坐标（Y轴移动到Cam1视野内时的Y位置）</summary>
        public double CommonY1 { get => _commonY1; set => SetProperty(ref _commonY1, value); }

        private double _commonY2;
        /// <summary>Cam2采集时的共用Y轴机械坐标（Y轴移动到Cam2视野内时的Y位置）</summary>
        public double CommonY2 { get => _commonY2; set => SetProperty(ref _commonY2, value); }

        private double _gantry1VisionX;
        /// <summary>Cam1视觉X（龙门1相机）</summary>
        public double Gantry1VisionX { get => _gantry1VisionX; set => SetProperty(ref _gantry1VisionX, value); }

        private double _gantry1VisionY;
        /// <summary>Cam1视觉Y（龙门1相机）</summary>
        public double Gantry1VisionY { get => _gantry1VisionY; set => SetProperty(ref _gantry1VisionY, value); }

        private double _gantry2VisionX;
        /// <summary>Cam2视觉X（龙门2相机）</summary>
        public double Gantry2VisionX { get => _gantry2VisionX; set => SetProperty(ref _gantry2VisionX, value); }

        private double _gantry2VisionY;
        /// <summary>Cam2视觉Y（龙门2相机）</summary>
        public double Gantry2VisionY { get => _gantry2VisionY; set => SetProperty(ref _gantry2VisionY, value); }

        private bool _isGantry1Captured;
        /// <summary>Cam1是否已采集（Y轴在Cam1视野内时已拍照）</summary>
        public bool IsGantry1Captured
        {
            get => _isGantry1Captured;
            set
            {
                if (SetProperty(ref _isGantry1Captured, value))
                    RaisePropertyChanged(nameof(IsCaptured));
            }
        }

        private bool _isGantry2Captured;
        /// <summary>Cam2是否已采集（Y轴在Cam2视野内时已拍照）</summary>
        public bool IsGantry2Captured
        {
            get => _isGantry2Captured;
            set
            {
                if (SetProperty(ref _isGantry2Captured, value))
                    RaisePropertyChanged(nameof(IsCaptured));
            }
        }

        /// <summary>是否已采集（双相机均已分步拍照完成）</summary>
        public bool IsCaptured => IsGantry1Captured && IsGantry2Captured;
    }
}

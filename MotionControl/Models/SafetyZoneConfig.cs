using Prism.Mvvm;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace MotionControl.Models
{
    public class SafetyZoneConfig : BindableBase
    {
        private double _safeHeightZ1 = 50.0;

        [Category("安全区域")]
        [DisplayName("Z1安全高度阈值")]
        [Description("Dz₁安全高度阈值，低于此值时触发安全互锁保护（单位：mm）")]
        public double SafeHeightZ1
        {
            get => _safeHeightZ1;
            set => SetProperty(ref _safeHeightZ1, value);
        }

        private double _dangerZoneXMin = 0.0;

        [Category("危险区域-X轴")]
        [DisplayName("X轴危险区下限")]
        [Description("X轴危险区域下边界位置（单位：mm），低于此值视为进入危险区")]
        public double DangerZoneXMin
        {
            get => _dangerZoneXMin;
            set => SetProperty(ref _dangerZoneXMin, value);
        }

        private double _dangerZoneXMax = 200.0;

        [Category("危险区域-X轴")]
        [DisplayName("X轴危险区上限")]
        [Description("X轴危险区域上边界位置（单位：mm），高于此值视为进入危险区")]
        public double DangerZoneXMax
        {
            get => _dangerZoneXMax;
            set => SetProperty(ref _dangerZoneXMax, value);
        }

        private double _dangerZoneYMin = 0.0;

        [Category("危险区域-Y轴")]
        [DisplayName("Y轴危险区下限")]
        [Description("Y轴危险区域下边界位置（单位：mm），低于此值视为进入危险区")]
        public double DangerZoneYMin
        {
            get => _dangerZoneYMin;
            set => SetProperty(ref _dangerZoneYMin, value);
        }

        private double _dangerZoneYMax = 200.0;

        [Category("危险区域-Y轴")]
        [DisplayName("Y轴危险区上限")]
        [Description("Y轴危险区域上边界位置（单位：mm），高于此值视为进入危险区")]
        public double DangerZoneYMax
        {
            get => _dangerZoneYMax;
            set => SetProperty(ref _dangerZoneYMax, value);
        }

        private bool _enabled = true;

        [Category("全局设置")]
        [DisplayName("启用安全互锁")]
        [Description("是否启用运动安全互锁功能，关闭后将跳过所有安全检查")]
        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        /// <summary>
        /// 深拷贝当前配置对象的所有属性值，用于创建独立副本避免引用共享问题
        /// </summary>
        public SafetyZoneConfig Clone()
        {
            return new SafetyZoneConfig
            {
                SafeHeightZ1 = SafeHeightZ1,
                DangerZoneXMin = DangerZoneXMin,
                DangerZoneXMax = DangerZoneXMax,
                DangerZoneYMin = DangerZoneYMin,
                DangerZoneYMax = DangerZoneYMax,
                Enabled = Enabled
            };
        }
    }
}

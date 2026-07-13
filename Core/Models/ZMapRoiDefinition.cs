using Prism.Mvvm;
using System.Collections.Generic;

namespace Core.Models
{
    /// <summary>ZMAP轨迹ROI类型，对齐参考Plugin.DispensePath第一期支持范围。</summary>
    public enum ZMapRoiType
    {
        Line,
        CircularArc,
        Polyline
    }

    /// <summary>ZMAP图像像素点；X表示列Col，Y表示行Row。</summary>
    public class ZMapPixelPoint
    {
        public double Col { get; set; }
        public double Row { get; set; }
    }

    /// <summary>
    /// ZMAP轨迹ROI定义。直线使用2点、圆弧使用起点/中间点/终点3点、折线使用至少2点。
    /// ROI只描述图像几何，不包含机械控制逻辑，便于后续扩展其它图像源或交互控件。
    /// </summary>
    public class ZMapRoiDefinition : BindableBase
    {
        private ZMapRoiType _type = ZMapRoiType.Polyline;

        public ZMapRoiType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public List<ZMapPixelPoint> ControlPoints { get; set; } = new List<ZMapPixelPoint>();
    }
}

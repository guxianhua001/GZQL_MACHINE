using Prism.Mvvm;
using System.Collections.Generic;

namespace Core.Models
{
    /// <summary>ZMAP轨迹ROI类型，对齐参考Plugin.DispensePath。</summary>
    public enum ZMapRoiType
    {
        Line,
        CircularArc,
        Polyline,
        /// <summary>单点示教：点击图像逐点追加，每个点即一个输出采样点</summary>
        SinglePoint
    }

    /// <summary>
    /// 折线示教插入方向（对齐Plugin.DispensePath）：
    /// Auto=按选中端点自动判定（选中首点向前插入，否则向后追加），Head=始终向前，Tail=始终向后。
    /// </summary>
    public enum ZMapTeachDirection
    {
        Auto,
        Head,
        Tail
    }

    /// <summary>ZMAP图像像素点；X表示列Col，Y表示行Row。</summary>
    public class ZMapPixelPoint
    {
        public double Col { get; set; }
        public double Row { get; set; }
    }

    /// <summary>
    /// ZMAP可编辑ROI顶点（折线顶点/单点示教点）。实现属性变更通知，
    /// 使窗口内顶点表格编辑与图像ROI/骨架连线保持双向联动（对齐Plugin.DispensePath）。
    /// Col=列(X)，Row=行(Y)，与ZMapPixelPoint一致。
    /// </summary>
    public class ZMapRoiVertex : BindableBase
    {
        private int _id;
        public int Id { get => _id; set => SetProperty(ref _id, value); }

        private double _col;
        public double Col { get => _col; set => SetProperty(ref _col, value); }

        private double _row;
        public double Row { get => _row; set => SetProperty(ref _row, value); }
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

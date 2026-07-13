using Core.Models;
using System.Collections.Generic;
using System.Windows;

namespace Module.Controls.ZMap
{
    /// <summary>
    /// ZMAP高度提取悬浮工具窗口——独立弹出窗口，通过 Step3 面板新增按钮打开，
    /// 关闭时不影响 CadPointEditor 主流程的任何已有状态，仅在用户点击"应用"后
    /// 才把提取结果写回传入的采样点 CadPoint.Z（详见 ZMapExtractZViewModel）。
    /// </summary>
    public partial class ZMapExtractZWindow : Window
    {
        public ZMapExtractZWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 按选中线段点数对当前ROI等距采样，确保提取结果与CadPoint严格一一对应。
        /// </summary>
        public IReadOnlyList<ZMapPixelPoint> GetRoiSamplePoints(int pointCount) =>
            RoiCanvas.GetSamplePoints(pointCount);

        public bool IsRoiComplete => RoiCanvas.IsRoiComplete;

        /// <summary>清除ROI仅影响本悬浮窗口，不修改选中线段数据。</summary>
        private void ClearRoi_Click(object sender, RoutedEventArgs e)
        {
            RoiCanvas.ClearRoi();
        }
    }
}

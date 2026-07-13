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
    }
}

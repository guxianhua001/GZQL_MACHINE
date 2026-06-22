using System.Windows.Controls;

namespace Module.Views
{
    /// <summary>
    /// 双龙门标定视图——管理双龙门独立仿射标定、公共基准点采集、跨龙门Y基准对齐
    /// 机构特点：龙门1(Dx+Dy独立) + 龙门2(X2+共用Y) + 双上相机(Cam1/Cam2)
    /// </summary>
    public partial class DualGantryCalibrationView : UserControl
    {
        public DualGantryCalibrationView()
        {
            InitializeComponent();
        }
    }
}

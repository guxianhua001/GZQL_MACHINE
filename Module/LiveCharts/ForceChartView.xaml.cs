using Framework.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Framework.Views
{
    /// <summary>
    /// ForceChartView.xaml 的交互逻辑
    /// </summary>
    public partial class ForceChartView : UserControl
    {
        public ForceChartView()
        {
            InitializeComponent();
            this.Loaded += PlotControl_PlotLoaded;
        }
        private void PlotControl_PlotLoaded(object sender, EventArgs e)
        {
            if (DataContext is ForceChartViewModel vm)
            {
                //vm.PlotControl = PlotControl; // 绑定控件到ViewModel
                //vm.InitPlot();
            }
        }

    }
}

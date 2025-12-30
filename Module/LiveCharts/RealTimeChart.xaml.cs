using LiveCharts.Wpf.Charts.Base;
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
    /// RealTimeChart.xaml 的交互逻辑
    /// </summary>
    public partial class RealTimeChart : UserControl
    {
        public RealTimeChart()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 启用WPF硬件加速
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;

            // 设置图表缓存策略
            var cachePolicy = new BitmapCache
            {
                EnableClearType = true,
                SnapsToDevicePixels = true
            };
            //Chart.CacheMode = cachePolicy;
        }
    }
}

using Interfaces;
using Stations;
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
    /// PinMapView.xaml 的交互逻辑
    /// </summary>
    public partial class PinMapView : UserControl
    {
        // 依赖属性：任务源
        public static readonly DependencyProperty TaskSourceProperty =
            DependencyProperty.Register("TaskSource", typeof(ITaskWithPoints), typeof(PinMapView));

        // 依赖属性：标题
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(PinMapView));

        public ITaskWithPoints TaskSource
        {
            get => (ITaskWithPoints)GetValue(TaskSourceProperty);
            set => SetValue(TaskSourceProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public PinMapView()
        {
            InitializeComponent();
        }
    }
}

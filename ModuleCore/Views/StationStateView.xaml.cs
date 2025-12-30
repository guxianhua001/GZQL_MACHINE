using ModuleCore.ViewModels;
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

namespace ModuleCore.Views
{
    /// <summary>
    /// StationStateView.xaml 的交互逻辑
    /// </summary>
    public partial class StationStateView : UserControl
    {
        public static readonly DependencyProperty StationIdProperty =
    DependencyProperty.Register("StationId", typeof(int), typeof(StationStateView),
        new PropertyMetadata(default(int), OnStationIdChanged));

        public int StationId
        {
            get => (int)GetValue(StationIdProperty);
            set => SetValue(StationIdProperty, value);
        }

        private static void OnStationIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = (StationStateView)d;
            if (view.DataContext is StationStateViewModel vm)
            {
                vm.StationId = (int)e.NewValue;  // 同步到 ViewModel
            }
        }
        public StationStateView()
        {
            InitializeComponent();
        }
    }
}

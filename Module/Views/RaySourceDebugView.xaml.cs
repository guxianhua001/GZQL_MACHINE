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
    /// RaySourceDebugView.xaml 的交互逻辑
    /// </summary>
    public partial class RaySourceDebugView : UserControl
    {
        public RaySourceDebugView()
        {
            InitializeComponent();
        }
        private void UserControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is RaySourceDebugViewModel vm)
            {
                vm.CommunicationService.StatusChanged += (s, status) =>
                {
                    // 根据状态更新UI
                };
            }
        }
    }
}

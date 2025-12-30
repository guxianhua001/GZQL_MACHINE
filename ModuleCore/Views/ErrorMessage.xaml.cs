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
    /// ErrorMessage.xaml 的交互逻辑
    /// </summary>
    public partial class ErrorMessage : Window
    {
        public ErrorMessage()
        {
            InitializeComponent();
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //startPoint = e.GetPosition(this); // 获取鼠标按下的位置相对于窗体的位置
            this.Cursor = Cursors.Arrow; // 改变光标为可调整大小的形状，提供视觉反馈
            DragMove();//window 内部的移动方法    
        }
    }
}

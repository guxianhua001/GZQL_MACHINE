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
    /// WindowClosedQuestion.xaml 的交互逻辑
    /// </summary>
    public partial class WindowClosedQuestion : Window
    {
        public bool IsClosing { get; set; } = false;
        public WindowClosedQuestion()
        {
            InitializeComponent();
            //Width = SystemParameters.PrimaryScreenWidth;
        }
        private void ButtonYes_Click(object sender, RoutedEventArgs e)
        {
            IsClosing = true;
            this.Close();
        }

        private void ButtonNo_Click(object sender, RoutedEventArgs e)
        {
            IsClosing = false;
            this.Close();
        }
    }
}

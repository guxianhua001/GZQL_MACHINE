using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
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
    /// WindowAutoClosedSuccess.xaml 的交互逻辑
    /// </summary>
    public partial class WindowAutoClosedSuccess : Window
    {
        private Timer MyTimer { get; set; }

        public WindowAutoClosedSuccess(string content = "程序执行完成", int t = 1000)
        {
            InitializeComponent();

            TB_Info.Text = content;
            TB_Time.Text = DateTime.Now.ToString("G");

            MyTimer = new Timer(t);
            MyTimer.Elapsed += new ElapsedEventHandler(Timer_Elapsed);
            MyTimer.Start();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            MyTimer.Stop();
            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
                Close();
            }));
        }
    }
}

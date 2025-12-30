using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Interfaces
{
    /// <summary>
    /// ErrorDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ErrorDialog : Window
    {
        // Win32 API声明
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        public ErrorDialog(ErrorDialogViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
            // 绑定关闭事件
            viewModel.RequestClose += (result) =>
            {
                DialogResult = result == ButtonResult.Yes;
                Close();
            };
        }
        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            // 隐藏关闭按钮
            var hwnd = new WindowInteropHelper(this).Handle;
            var value = GetWindowLong(hwnd, GWL_STYLE);
            SetWindowLong(hwnd, GWL_STYLE, value & ~WS_SYSMENU);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 禁止通过Alt+F4或其它方式关闭
            if (DialogResult == null)
            {
                e.Cancel = true;
            }
        }
    }
}

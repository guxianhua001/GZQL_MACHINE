using Core.Models;
using Module.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Module.Views
{
    /// <summary>
    /// 数据看板视图 - 显示步骤的输入输出变量
    /// 支持双击加载示意图、表达式编辑、增删行、刷新数值
    /// </summary>
    public partial class DataDashboardView : UserControl
    {
        public DataDashboardView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 双击示意图区域加载图片
        /// </summary>
        private void OnImageAreaDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2 && DataContext is DataDashboardViewModel vm)
            {
                vm.LoadImageCommand.Execute();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 双击全局变量列表项，插入变量到表达式编辑器
        /// </summary>
        private void OnGlobalVariableDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is GlobalVariable
                && DataContext is DataDashboardViewModel vm)
            {
                vm.InsertGlobalVariableCommand.Execute();
                e.Handled = true;
            }
        }
    }
}

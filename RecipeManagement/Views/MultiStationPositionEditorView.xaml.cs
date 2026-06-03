using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Recipe.ViewModels;

namespace Recipe.Views
{
    /// <summary>
    /// Interaction logic for MultiStationPositionEditorView.xaml
    /// </summary>
    public partial class MultiStationPositionEditorView : UserControl
    {
        public MultiStationPositionEditorView()
        {
            InitializeComponent();
        }
        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            // 隐藏 IsReadOnly 列
            if (e.PropertyName == "IsReadOnly")
            {
                e.Cancel = true;
                return;
            }

            // 为 PositionName 列设置固定宽度
            if (e.PropertyName == "PositionName")
            {
                if (e.Column is DataGridTextColumn textColumn)
                {
                    textColumn.Width = 150;
                }
            }
            // 为 Comment 列设置最小宽度
            else if (e.PropertyName == "Comment")
            {
                if (e.Column is DataGridTextColumn textColumn)
                {
                    textColumn.MinWidth = 250;
                }
            }
            else
            {
                // 其余列（轴列）统一设置最小宽度
                if (e.Column is DataGridTextColumn axisColumn)
                {
                    axisColumn.MinWidth = 80;
                    // 也可以使用固定宽度： axisColumn.Width = 80;
                }
            }

            // 禁用用户排序（可选）
            e.Column.CanUserSort = false;
        }

        /// <summary>
        /// 单元格切换时通知 ViewModel 更新选中轴列（供单轴 GOTO 使用）
        /// </summary>
        private void PositionsDataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            if (DataContext is not MultiStationPositionEditorViewModel vm || sender is not DataGrid grid)
                return;

            var cell = grid.CurrentCell;
            if (cell.Column is DataGridTextColumn textColumn && textColumn.Binding is Binding binding)
                vm.SetSelectedAxisColumn(binding.Path.Path);
            else
                vm.SetSelectedAxisColumn(null);
        }
    }
}

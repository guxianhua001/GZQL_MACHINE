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
                // 其余列（轴列）统一设置最小宽度与3位小数显示
                if (e.Column is DataGridTextColumn axisColumn)
                {
                    axisColumn.MinWidth = 80;
                    // 轴位置统一显示3位小数（如 12.345），仅影响显示不影响实际值
                    if (axisColumn.Binding is Binding existingBinding)
                        existingBinding.StringFormat = "F3";
                    else
                        axisColumn.Binding = new Binding(e.PropertyName) { StringFormat = "F3" };
                }
            }

            // 禁用用户排序
            e.Column.CanUserSort = false;
        }

        /// <summary>
        /// 单元格切换时通知 ViewModel 更新选中轴列（供单轴 GOTO 使用）
        /// 通过 DisplayIndex 映射到 DataTable 列，避开 Header/SortMemberPath/Binding 不可靠问题
        /// </summary>
        private void PositionsDataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            if (DataContext is not MultiStationPositionEditorViewModel vm || sender is not DataGrid grid)
                return;

            var cell = grid.CurrentCell;
            if (!cell.IsValid || cell.Column == null)
            {
                vm.SetSelectedAxisColumn(-1);
                return;
            }

            // DataTable Columns: [PositionName, IsReadOnly, X, Y, ..., Comment]
            // DataGrid Columns:    [PositionName, X, Y, ..., Comment]  (IsReadOnly hidden)
            // 因此 DataGrid DisplayIndex >= 1 时，DataTable 索引 = DisplayIndex + 1
            var gridIdx = cell.Column.DisplayIndex;
            var tableIdx = gridIdx == 0 ? 0 : gridIdx + 1; // 跳过隐藏的 IsReadOnly

            vm.SetSelectedAxisColumn(tableIdx);
        }
    }
}
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
    }
}

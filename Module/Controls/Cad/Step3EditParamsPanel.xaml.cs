using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Module.Controls
{
    /// <summary>Step3：编辑参数面板——轨迹段 DataGrid + 批量操作 + ROI 工具 + 参数编辑</summary>
    public partial class Step3EditParamsPanel : UserControl
    {
        public Step3EditParamsPanel()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 点位序号列的 TextBlock.Loaded 事件——通过行索引显示序号
        /// </summary>
        private void OnPointNumberLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock tb)
            {
                var row = FindAncestor<DataGridRow>(tb);
                if (row != null)
                {
                    int idx = row.GetIndex();
                    tb.Text = (idx + 1).ToString();
                }
            }
        }

        /// <summary>向上查找指定类型的祖先元素</summary>
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T result)
                    return result;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}

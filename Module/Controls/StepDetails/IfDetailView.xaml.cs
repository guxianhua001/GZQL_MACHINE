using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Module.Views
{
    /// <summary>
    /// IfDetailView.xaml 的交互逻辑。
    /// 处理变量插入下拉框的选择事件，将选中的变量插入到表达式编辑框的光标位置。
    /// </summary>
    public partial class IfDetailView : UserControl
    {
        public IfDetailView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 变量选择下拉框：选中后将变量名插入到表达式编辑框光标位置
        /// </summary>
        private void OnVariableSelected(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox comboBox || comboBox.SelectedItem is not string selectedVar)
                return;
            if (string.IsNullOrEmpty(selectedVar)) return;

            if (ExpressionTextBox != null)
            {
                int caretIndex = ExpressionTextBox.CaretIndex;
                string current = ExpressionTextBox.Text ?? "";
                ExpressionTextBox.Text = current.Insert(caretIndex, selectedVar);
                ExpressionTextBox.CaretIndex = caretIndex + selectedVar.Length;
                ExpressionTextBox.Focus();
            }

            // 重置下拉框选择，避免重复插入
            comboBox.SelectedIndex = -1;
        }
    }
}

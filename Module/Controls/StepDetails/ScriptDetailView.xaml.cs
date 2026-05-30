using Module.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Module.Views
{
    public partial class ScriptDetailView : UserControl
    {
        public ScriptDetailView()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                if (DataContext is ScriptDetailViewModel vm)
                {
                    vm.InsertTextCallback = InsertTextAtCursor;
                }
            };
        }

        /// <summary>
        /// 双击全局变量列表项，插入 ctx.GetDouble("变量名") 到代码编辑器
        /// </summary>
        private void OnGlobalVariableDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is GlobalVariableItem item)
            {
                InsertTextAtCursor($"ctx.GetDouble(\"{item.Name}\")");
                e.Handled = true;
            }
        }

        /// <summary>
        /// 双击步骤输出参数列表项，插入 ctx.GetOutputDouble("参数名") 到代码编辑器
        /// </summary>
        private void OnStepOutputDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem is StepOutputItem item)
            {
                InsertTextAtCursor($"ctx.GetOutputDouble(\"{item.Name}\")");
                e.Handled = true;
            }
        }

        /// <summary>
        /// 在代码编辑器光标位置插入文本
        /// </summary>
        private void InsertTextAtCursor(string text)
        {
            if (ScriptCodeEditor == null) return;

            var selectionStart = ScriptCodeEditor.SelectionStart;
            var selectionLength = ScriptCodeEditor.SelectionLength;

            var currentText = ScriptCodeEditor.Text;
            var newText = currentText.Remove(selectionStart, selectionLength).Insert(selectionStart, text);

            ScriptCodeEditor.Text = newText;
            ScriptCodeEditor.SelectionStart = selectionStart + text.Length;
            ScriptCodeEditor.SelectionLength = 0;
            ScriptCodeEditor.Focus();
        }
    }
}

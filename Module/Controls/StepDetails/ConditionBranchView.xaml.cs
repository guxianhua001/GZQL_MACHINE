using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Core.Models;

namespace Module.Views
{
    /// <summary>
    /// ConditionBranchView.xaml 的交互逻辑
    /// 条件分支配置对话框，支持输出参数定义、条件表达式编辑和跳转目标配置
    /// </summary>
    public partial class ConditionBranchView : UserControl
    {
        private DispatcherTimer _validateTimer;

        public ConditionBranchView()
        {
            InitializeComponent();
            _validateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _validateTimer.Tick += (s, e) =>
            {
                _validateTimer.Stop();
                if (DataContext is ViewModels.ConditionBranchViewModel vm)
                {
                    var focused = FocusManager.GetFocusedElement(this) as TextBox;
                    if (focused != null && focused.DataContext is BranchCondition cond)
                        vm.ValidateCondition(cond);
                }
            };
        }

        /// <summary>
        /// 输出参数名选择后：自动推断 OutputType，清空不匹配的全局变量绑定
        /// </summary>
        private void OnParamNameSelected(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox comboBox || comboBox.SelectedItem is not StepOutputInfo info)
                return;

            if (comboBox.DataContext is BranchOutputParameter param)
            {
                param.OutputType = info.OutputType;

                if (!string.IsNullOrEmpty(param.TargetGlobalVariable))
                {
                    if (DataContext is ViewModels.ConditionBranchViewModel vm)
                    {
                        var validNames = vm.GetFilteredGlobalVariableNames(param.OutputType);
                        if (!validNames.Contains(param.TargetGlobalVariable))
                            param.TargetGlobalVariable = null;
                    }
                }
            }
        }

        /// <summary> 条件表达式 TextBox 失去焦点时触发延迟校验 </summary>
        private void OnExprTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.DataContext is not BranchCondition cond) return;
            _validateTimer.Stop();
            _validateTimer.Start();
            if (DataContext is ViewModels.ConditionBranchViewModel vm)
            {
                vm.ValidateCondition(cond);
            }
        }

        /// <summary>
        /// 变量选择下拉框：选中后将变量名插入到同行 TextBox 光标位置
        /// </summary>
        private void OnVariableSelected(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox comboBox || comboBox.SelectedItem is not string selectedVar)
                return;
            if (string.IsNullOrEmpty(selectedVar)) return;

            var parentGrid = System.Windows.Media.VisualTreeHelper.GetParent(comboBox);
            while (parentGrid != null && parentGrid is not Grid)
                parentGrid = System.Windows.Media.VisualTreeHelper.GetParent(parentGrid);

            if (parentGrid is Grid grid)
            {
                var textBox = grid.Children.OfType<TextBox>().FirstOrDefault();
                if (textBox != null)
                {
                    int caretIndex = textBox.CaretIndex;
                    string current = textBox.Text ?? "";
                    textBox.Text = current.Insert(caretIndex, selectedVar);
                    textBox.CaretIndex = caretIndex + selectedVar.Length;
                    textBox.Focus();
                }
            }

            comboBox.SelectedIndex = -1;
        }

        /// <summary>
        /// 确定按钮点击：先强制提交DataGrid编辑态，再调用ViewModel保存
        /// 解决用户输入条件表达式后直接点"确定"，绑定值未提交的问题
        /// </summary>
        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            try
            {
                CommitAllDataGridEdits();
            }
            catch { }

            if (DataContext is ViewModels.ConditionBranchViewModel vm)
            {
                vm.OkCommand.Execute(null);
            }
        }

        /// <summary>
        /// 遍历可视化树找到所有DataGrid，强制提交正在编辑的行和单元格
        /// DataGrid.CommitEdit()会结束编辑态并将绑定值写入数据源
        /// </summary>
        private void CommitAllDataGridEdits()
        {
            foreach (var dg in FindVisualChildren<DataGrid>(this))
            {
                dg.CommitEdit();
                dg.CommitEdit();
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
        {
            if (parent == null) yield break;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    yield return result;
                foreach (var desc in FindVisualChildren<T>(child))
                    yield return desc;
            }
        }
    }
}

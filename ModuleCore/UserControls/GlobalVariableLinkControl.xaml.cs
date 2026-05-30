using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Core.Models;
using ObservableCollection = System.Collections.ObjectModel.ObservableCollection<Core.Models.GlobalVariable>;

namespace ModuleCore.UserControls
{
    /// <summary>
    /// 全局变量链接控件：封装数值显示 + 链接图标 + 变量选择ComboBox的标准模式
    /// </summary>
    public partial class GlobalVariableLinkControl : UserControl
    {
        #region DependencyProperty

        public static readonly DependencyProperty DisplayValueProperty =
            DependencyProperty.Register(nameof(DisplayValue), typeof(double), typeof(GlobalVariableLinkControl),
                new FrameworkPropertyMetadata(0.0));

        public static readonly DependencyProperty DisplayForegroundProperty =
            DependencyProperty.Register(nameof(DisplayForeground), typeof(Brush), typeof(GlobalVariableLinkControl),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(229, 57, 53))));

        public static readonly DependencyProperty IsLinkedProperty =
            DependencyProperty.Register(nameof(IsLinked), typeof(bool), typeof(GlobalVariableLinkControl),
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty UnlinkCommandProperty =
            DependencyProperty.Register(nameof(UnlinkCommand), typeof(ICommand), typeof(GlobalVariableLinkControl),
                new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty LinkedVariableNameProperty =
            DependencyProperty.Register(nameof(LinkedVariableName), typeof(string), typeof(GlobalVariableLinkControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnLinkedVariableNameChanged));

        public static readonly DependencyProperty LinkableGlobalVariablesProperty =
            DependencyProperty.Register(nameof(LinkableGlobalVariables), typeof(ObservableCollection), typeof(GlobalVariableLinkControl),
                new FrameworkPropertyMetadata(null, OnLinkableGlobalVariablesChanged));

        public static readonly DependencyProperty ComboBoxWidthProperty =
            DependencyProperty.Register(nameof(ComboBoxWidth), typeof(double), typeof(GlobalVariableLinkControl),
                new FrameworkPropertyMetadata(100.0));

        #endregion

        #region 属性包装

        public double DisplayValue
        {
            get => (double)GetValue(DisplayValueProperty);
            set => SetValue(DisplayValueProperty, value);
        }

        public Brush DisplayForeground
        {
            get => (Brush)GetValue(DisplayForegroundProperty);
            set => SetValue(DisplayForegroundProperty, value);
        }

        public bool IsLinked
        {
            get => (bool)GetValue(IsLinkedProperty);
            set => SetValue(IsLinkedProperty, value);
        }

        public ICommand UnlinkCommand
        {
            get => (ICommand)GetValue(UnlinkCommandProperty);
            set => SetValue(UnlinkCommandProperty, value);
        }

        public string LinkedVariableName
        {
            get => (string)GetValue(LinkedVariableNameProperty);
            set => SetValue(LinkedVariableNameProperty, value);
        }

        public ObservableCollection LinkableGlobalVariables
        {
            get => (ObservableCollection)GetValue(LinkableGlobalVariablesProperty);
            set => SetValue(LinkableGlobalVariablesProperty, value);
        }

        public double ComboBoxWidth
        {
            get => (double)GetValue(ComboBoxWidthProperty);
            set => SetValue(ComboBoxWidthProperty, value);
        }

        #endregion

        private bool _isSynchronizingComboBox;

        public GlobalVariableLinkControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// LinkedVariableName变更回调：仅在变量名被清空时清除 ComboBox。
        /// 非空时依赖 WPF SelectedValue 绑定（source→target 方向）自动匹配 ComboBox 选中项，
        /// 不应手动设置 Text，否则会将意外字符串写入可编辑框。
        /// </summary>
        private static void OnLinkedVariableNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (GlobalVariableLinkControl)d;
            if (string.IsNullOrEmpty(e.NewValue as string) && control.PART_ComboBox != null)
            {
                control.PART_ComboBox.SelectedItem = null;
                control.PART_ComboBox.Text = string.Empty;
            }
        }

        /// <summary>
        /// 全局变量列表替换后，WPF 可能丢失 ComboBox 选中状态，
        /// 此处按 LinkedVariableName 在新列表中重新定位选中项（仅设置 SelectedItem，不碰 Text）。
        /// </summary>
        private static void OnLinkableGlobalVariablesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (GlobalVariableLinkControl)d;
            control.SyncComboBoxSelectionAfterItemsSourceChanged();
        }

        /// <summary>
        /// ItemsSource 刷新后恢复下拉框选中项。
        /// 仅通过 SelectedItem 恢复，让 ComboBox 通过 DisplayMemberPath="Name" 自动显示变量名，
        /// 避免手动写 Text 引入错误字符串。
        /// </summary>
        private void SyncComboBoxSelectionAfterItemsSourceChanged()
        {
            if (PART_ComboBox == null || string.IsNullOrWhiteSpace(LinkedVariableName))
                return;

            var matched = LinkableGlobalVariables?
                .Cast<GlobalVariable>()
                .FirstOrDefault(v => string.Equals(v.Name, LinkedVariableName, StringComparison.OrdinalIgnoreCase));

            if (matched == null)
                return;

            try
            {
                _isSynchronizingComboBox = true;
                PART_ComboBox.SelectedItem = matched;
            }
            finally
            {
                _isSynchronizingComboBox = false;
            }
        }

        /// <summary>
        /// 下拉选择后立即将变量名写回 LinkedVariableName，
        /// 避免等到 LostFocus 才触发绑定导致状态不一致。
        /// </summary>
        private void OnComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSynchronizingComboBox)
                return;

            if (PART_ComboBox?.SelectedValue is string selectedName && !string.IsNullOrWhiteSpace(selectedName))
                LinkedVariableName = selectedName;
        }

        /// <summary>
        /// LostFocus 时仅处理用户手动清空文本的场景：
        /// Text 为空 → 解除链接（清空 LinkedVariableName）。
        /// 其余情况由 SelectedValue 绑定（UpdateSourceTrigger=LostFocus）自动处理。
        /// </summary>
        private void OnComboBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (_isSynchronizingComboBox || PART_ComboBox == null)
                return;

            // 用户手动清空了可编辑框文本，视为解除链接
            if (string.IsNullOrWhiteSpace(PART_ComboBox.Text))
            {
                LinkedVariableName = null;
                PART_ComboBox.SelectedItem = null;
            }
        }
    }
}

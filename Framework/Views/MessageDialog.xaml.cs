using Framework.ViewModels;
using MaterialDesignThemes.Wpf;
using System;
using System.Windows;
using System.Windows.Input;

namespace Framework.Views
{
    /// <summary>
    /// MessageDialog.xaml 的交互逻辑
    /// </summary>
    public partial class MessageDialog : Window
    {
        private System.Windows.Point _lastPosition;

        public MessageDialog()
        {
            InitializeComponent();
            // 创建 ViewModel 实例并绑定
            var vm = new MessageDialogViewModel();
            DataContext = vm;

            // 修改回调处理 - 处理多种返回类型
            vm.CloseCallback = (result) =>
            {
                // 将多种结果转换为标准 DialogResult
                if (result is bool boolResult)
                {
                    DialogResult = boolResult;
                }
                else if (result == null)
                {
                    DialogResult = null;
                }
                else if (result is string)
                {
                    // 特殊结果使用 Tag 属性传递
                    Tag = result;
                    DialogResult = true; // 标记为已操作
                }
            };
            this.DataContextChanged += (s, e) =>
            {
                if (DataContext is MessageDialogViewModel vm)
                {
                    // 初始化对话框位置
                    this.Left = (SystemParameters.WorkArea.Width - this.ActualWidth) / 2;
                    this.Top = (SystemParameters.WorkArea.Height - this.ActualHeight) / 3;
                }
            };

            // 添加位置存储
            this.LocationChanged += (s, e) => _lastPosition = new System.Windows.Point(Left, Top);
        }
        // 标题栏拖动支持
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        public object ShowDialog(
           string message,
           string title = "提示",
           PackIconKind iconKind = PackIconKind.Information,
           string yesButtonText = "确定",
           string noButtonText = "取消",
           string extraButtonText = null,
           bool showYesButton = true,
           bool showNoButton = true,
           bool showExtraButton = false)
        {
            if (DataContext is MessageDialogViewModel vm)
            {
                vm.Title = title;
                vm.Message = message;
                vm.IconKind = iconKind;
                vm.YesButtonText = yesButtonText;
                vm.NoButtonText = noButtonText;
                vm.IsYesButtonVisible = showYesButton;
                vm.IsNoButtonVisible = showNoButton;
                vm.IsExtraButtonVisible = showExtraButton;
                vm.ExtraButtonText = extraButtonText ?? "附加操作";
                // 恢复最后位置
                if (_lastPosition.X > 0 && _lastPosition.Y > 0)
                {
                    Left = _lastPosition.X;
                    Top = _lastPosition.Y;
                }

                // 调用基类 ShowDialog
                bool? dialogResult = base.ShowDialog();

                // 返回实际的处理结果
                if (dialogResult == true && Tag != null)
                {
                    // 返回特殊结果
                    return Tag;
                }

                // 返回标准布尔结果
                return dialogResult;
            }
            return false;
        }
        // 添加位置恢复支持
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (_lastPosition.X > 0 && _lastPosition.Y > 0)
            {
                Left = _lastPosition.X;
                Top = _lastPosition.Y;
            }
        }
    }

}

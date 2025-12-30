using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Framework.Controls
{
    public class MaterialDesignDialogWindow : Prism.Services.Dialogs.DialogWindow
    {
        public MaterialDesignDialogWindow()
        {
            // 现代像素渲染技术替代 SnapToDevicePixels
            UseLayoutRounding = true;
            //TextOptions.TextFormattingMode = TextFormattingMode.Display;

            // Material Design 设置
            ApplyMaterialDesignTheme();

            // 窗口行为设置
            ConfigureWindowBehavior();

            // 确保窗口大小合适
            SetDefaultDimensions();
        }

        private void ApplyMaterialDesignTheme()
        {
            // 尝试获取 Material Design 资源
            var paperBrush = Application.Current.TryFindResource("MaterialDesignPaper") as Brush;
            var bodyBrush = Application.Current.TryFindResource("MaterialDesignBody") as Brush;
            var dividerBrush = Application.Current.TryFindResource("MaterialDesignDivider") as Brush;

            // 设置颜色
            Background = paperBrush ?? Brushes.WhiteSmoke;
            Foreground = bodyBrush ?? Brushes.Black;
            BorderBrush = dividerBrush ?? Brushes.LightGray;
            BorderThickness = new Thickness(1);
            FontSize = 14;
        }

        private void ConfigureWindowBehavior()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResize;
            SizeToContent = SizeToContent.Manual;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ShowInTaskbar = false;
        }

        private void SetDefaultDimensions()
        {
            MinWidth = 400;
            MinHeight = 300;
            Width = 800;
            Height = 650;
        }

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            base.OnContentChanged(oldContent, newContent);

            // 当内容变更时自动调整窗口大小
            if (newContent is FrameworkElement element)
            {
                element.Loaded += (s, e) =>
                {
                    SizeToContent = SizeToContent.WidthAndHeight;
                    SizeToContent = SizeToContent.Manual;
                };
            }
        }
    }
}

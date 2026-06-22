using System.Windows;

namespace Framework.Controls
{
    public class MaterialDesignDialogWindow : Prism.Services.Dialogs.DialogWindow
    {
        public MaterialDesignDialogWindow()
        {
            // 现代像素渲染技术替代 SnapToDevicePixels
            UseLayoutRounding = true;
            //TextOptions.TextFormattingMode = TextFormattingMode.Display;

            // Material Design 设置：使用 DynamicResource 而非直接赋值，
            // 确保主题切换时窗口背景和前景色自动更新
            ApplyMaterialDesignTheme();

            // 窗口行为设置
            ConfigureWindowBehavior();

            // 确保窗口大小合适
            SetDefaultDimensions();
        }

        /// <summary>
        /// 应用 MaterialDesign 主题：使用 SetResourceReference 设置动态资源引用，
        /// 而非直接赋值 Brush。这样主题切换时窗口颜色会自动跟随更新。
        /// </summary>
        private void ApplyMaterialDesignTheme()
        {
            // 使用 SetResourceReference 设置 DynamicResource，主题切换时自动刷新
            SetResourceReference(BackgroundProperty, "MaterialDesignPaper");
            SetResourceReference(ForegroundProperty, "MaterialDesignBody");
            SetResourceReference(BorderBrushProperty, "MaterialDesignDivider");
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

using System;
using System.Windows;
using System.Windows.Input;
using Core.Abstraction;
using ModuleCore.Themes;
using ModuleCore.ViewModels;

namespace ModuleCore.Views
{
    /// <summary>
    /// 基础对话框窗口：统一的弹出窗口外观，支持暗色/明亮主题切换
    /// 使用自定义配色方案（深色非纯黑、亮色非纯白），提升层次感和视觉舒适度
    /// </summary>
    public partial class BaseDialogWindow : Window
    {
        private IThemeService _themeService;
        private BaseDialogWindowViewModel _viewModel;

        public BaseDialogWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 初始化窗口：设置 ViewModel、应用自定义主题、订阅主题变化事件
        /// </summary>
        /// <param name="viewModel">窗口 ViewModel</param>
        /// <param name="themeService">全局主题服务（可选，用于同步全局主题）</param>
        public void Initialize(BaseDialogWindowViewModel viewModel, IThemeService themeService = null)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _themeService = themeService;
            DataContext = viewModel;

            // 获取初始主题模式并应用自定义对话框配色
            bool isDark = themeService?.IsDarkTheme ?? false;
            viewModel.IsDarkTheme = isDark;

            // 1. 更新窗口自身资源
            ApplyDialogTheme(isDark);

            // 2. 关键修复：在显示前就处理 Content（UserControl）的 Resources
            //    UserControl 的 Resources 在 XAML 解析时已初始化（无需等视觉树构建），
            //    此时更新可确保首帧渲染时背景色即为正确的主题色，避免首次显示为 Light 主题颜色
            if (viewModel.Content is FrameworkElement content)
            {
                DialogThemeHelper.ApplyTheme(content.Resources, isDark);
            }

            // 3. 窗口 Loaded 时视觉树完整，递归遍历确保所有子控件的资源均更新为当前主题色
            Loaded += (s, e) => ApplyDialogTheme(isDark);

            // 订阅 VM 侧的主题切换请求 → 同步到全局服务 + 刷新本地颜色
            viewModel.ThemeToggleRequested += (requestedIsDark) =>
            {
                themeService?.SetTheme(requestedIsDark);
                ApplyDialogTheme(requestedIsDark);
            };

            // 订阅全局主题变化（其他地方切换时）→ 同步到 VM + 刷新本地颜色
            if (themeService != null)
            {
                themeService.ThemeChanged += (globalIsDark) =>
                {
                    viewModel.IsDarkTheme = globalIsDark;
                    ApplyDialogTheme(globalIsDark);
                };
            }

            // 订阅关闭请求
            viewModel.RequestClose += (result) =>
            {
                DialogResult = result != null;
                Close();
            };
        }

        /// <summary>
        /// 应用自定义对话框主题配色：
        /// 1. 更新窗口自身资源
        /// 2. 递归更新所有子控件资源（确保深层嵌套控件如 Border > Grid > TextBlock 也被覆盖）
        /// </summary>
        private void ApplyDialogTheme(bool isDark)
        {
            // 1. 更新窗口自身的资源
            DialogThemeHelper.ApplyTheme(Resources, isDark);

            // 2. 递归遍历整个视觉树，更新每个 FrameworkElement 的本地资源字典
            // 这确保 MaterialDesign 控件（Button/TextBox 等）在亮色模式下不使用默认的 White 前景
            DialogThemeHelper.ApplyThemeRecursive(this, isDark);
        }

        /// <summary>标题栏拖动：允许用户拖动窗口</summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                try
                {
                    DragMove();
                }
                catch
                {
                    // 拖动失败时忽略（如窗口最大化状态下）
                }
            }
        }
    }
}

using System;
using System.Windows;
using System.Windows.Media;

namespace ModuleCore.Themes
{
    /// <summary>
    /// 对话框主题助手：根据当前主题模式（暗色/明亮）动态切换颜色
    /// 同时覆盖 MaterialDesign 内置资源键，确保子控件也能获得正确的主题色
    /// </summary>
    public static class DialogThemeHelper
    {
        // 自定义画刷键名与对应深色/亮色颜色键名的映射
        private static readonly (string BrushKey, string DarkColorKey, string LightColorKey)[] CustomMappings = new[]
        {
            ("DialogBackgroundBrush",       "DialogDark.BgColor",          "DialogLight.BgColor"),
            ("DialogCardBrush",             "DialogDark.SurfaceColor",     "DialogLight.SurfaceColor"),
            ("DialogElevatedBrush",         "DialogDark.ElevatedColor",    "DialogLight.ElevatedColor"),
            ("DialogBorderBrush",           "DialogDark.BorderColor",      "DialogLight.BorderColor"),
            ("DialogTextPrimaryBrush",      "DialogDark.PrimaryTextColor", "DialogLight.PrimaryTextColor"),
            ("DialogTextSecondaryBrush",    "DialogDark.SecondaryTextColor","DialogLight.SecondaryTextColor"),
            ("DialogTextMutedBrush",        "DialogDark.MutedTextColor",   "DialogLight.MutedTextColor"),
            // 标题栏渐变色（Color 类型）
            ("DialogTitleBarStartColor",   "DialogDark.TitleBarStart",    "DialogLight.TitleBarStart"),
            ("DialogTitleBarEndColor",     "DialogDark.TitleBarEnd",      "DialogLight.TitleBarEnd"),
            // 标题栏文本画刷（关键！亮色模式下背景是白色，文本必须用深色）
            ("DialogTitleBarTextBrush",    "DialogDark.TitleBarTextColor", "DialogLight.TitleBarTextColor"),
            // 标题栏强调标签画刷
            ("DialogTitleAccentBrush",     "DialogDark.TitleAccentColor", "DialogLight.TitleAccentColor"),
            // DataGrid 专用
            ("DialogDataGridHeaderBrush",   "DialogDark.ElevatedColor",    "DialogLight.ElevatedColor"),
            ("DialogDataGridSelectedBrush", "DialogDark.BorderColor",      "DialogLight.BorderColor"),
            ("DialogDataGridHoverBrush",    "DialogDark.ElevatedColor",    "DialogLight.ElevatedColor"),
            // 强调色（全局变量图标、表头、公式编辑器标题等）
            ("DialogAccentBrush",           "DialogDark.AccentColor",      "DialogLight.AccentColor"),
            // 日志窗口颜色
            ("LogInfoBackground",           "DialogDark.LogInfoBgColor",   "DialogLight.LogInfoBgColor"),
            ("LogWarnBackground",           "DialogDark.LogWarnBgColor",   "DialogLight.LogWarnBgColor"),
            ("LogErrorBackground",          "DialogDark.LogErrorBgColor",  "DialogLight.LogErrorBgColor"),
            ("LogSelectedBackground",       "DialogDark.LogSelectedBgColor","DialogLight.LogSelectedBgColor"),
            ("LatestLogBackground",         "DialogDark.LogLatestBgColor", "DialogLight.LogLatestBgColor"),
            ("LogInfoHoverBackground",      "DialogDark.LogHoverInfoColor","DialogLight.LogHoverInfoColor"),
            ("LogWarnHoverBackground",      "DialogDark.LogHoverWarnColor","DialogLight.LogHoverWarnColor"),
            ("LogErrorHoverBackground",     "DialogDark.LogHoverErrorColor","DialogLight.LogHoverErrorColor"),
            ("LogForeground",               "DialogDark.LogTextColor",     "DialogLight.LogTextColor"),
            ("LogToolBarBackground",        "DialogDark.ElevatedColor",    "DialogLight.ElevatedColor"),
            ("LogToolTipBackground",        "DialogDark.SurfaceColor",     "DialogLight.SurfaceColor"),
        };

        // MaterialDesign 内置资源键 → 自定义颜色的映射（全面覆盖 MD 默认值）
        private static readonly (string MdResourceKey, string DarkColorKey, string LightColorKey)[] MdOverrideMappings = new[]
        {
            // 核心背景/前景
            ("MaterialDesignPaper",              "DialogDark.BgColor",          "DialogLight.BgColor"),
            ("MaterialDesignCardBackground",     "DialogDark.SurfaceColor",     "DialogLight.SurfaceColor"),
            // 主文本（TextBlock、Label、ContentPresenter 等）
            ("MaterialDesignBody",               "DialogDark.PrimaryTextColor", "DialogLight.PrimaryTextColor"),
            ("MaterialDesignBodyLight",         "DialogDark.SecondaryTextColor","DialogLight.SecondaryTextColor"),
            // 全局前景色（影响大多数控件文本）
            ("MaterialDesignForeground",         "DialogDark.PrimaryTextColor", "DialogLight.PrimaryTextColor"),
            // 工具栏背景
            ("MaterialDesignToolBarBackground",  "DialogDark.ElevatedColor",    "DialogLight.ElevatedColor"),
            // 分隔线
            ("MaterialDesignDivider",            "DialogDark.BorderColor",      "DialogLight.BorderColor"),
            // 浅色背景（提示框等）
            ("MaterialDesignBackground",         "DialogDark.ElevatedColor",    "DialogLight.ElevatedColor"),
            ("MaterialDesignLightBackground",    "DialogDark.ElevatedColor",    "DialogLight.ElevatedColor"),
            // 浅色前景（弱化文本）
            ("MaterialDesignLightForeground",     "DialogDark.MutedTextColor",   "DialogLight.MutedTextColor"),
            // ===== 关键：Button/RaisedButton 前景色（MD Light 模式默认 White！） =====
            ("MaterialDesign.Button.Foreground",       "DialogDark.PrimaryTextColor", "DialogLight.PrimaryTextColor"),
            ("MaterialDesign.RaisedButton.Foreground", "DialogDark.PrimaryTextColor", "DialogLight.PrimaryTextColor"),
            ("MaterialDesign.OutlinedButton.Foreground","DialogDark.PrimaryTextColor","DialogLight.PrimaryTextColor"),
            // CheckBox / RadioButton 文本
            ("MaterialDesign.CheckBox.Foreground",    "DialogDark.PrimaryTextColor", "DialogLight.PrimaryTextColor"),
            ("MaterialDesign.RadioButton.Foreground", "DialogDark.PrimaryTextColor", "DialogLight.PrimaryTextColor"),
            // TextBox / ComboBox 文本
            ("MaterialDesign.TextBox.Foreground",     "DialogDark.PrimaryTextColor", "DialogLight.PrimaryTextColor"),
            ("MaterialDesign.ComboBox.Foreground",    "DialogDark.PrimaryTextColor", "DialogLight.PrimaryTextColor"),
            // DataGrid
            ("MaterialDesign.DataGridRowHoverBackground", "DialogDark.ElevatedColor", "DialogLight.ElevatedColor"),
            ("MaterialDesign.DataGrid.Selected.Background", "DialogDark.BorderColor", "DialogLight.BorderColor"),
            ("DataGrid.Cell.Foreground",                   "DialogDark.PrimaryTextColor", "DialogLight.PrimaryTextColor"),
            ("DataGrid.ColumnHeader.Foreground",           "DialogDark.PrimaryTextColor", "DialogLight.PrimaryTextColor"),
            ("MaterialDesign.DataGridRow.Foreground",      "DialogDark.PrimaryTextColor", "DialogLight.PrimaryTextColor"),
            ("DataGrid.Row.HeaderForeground",              "DialogDark.PrimaryTextColor", "DialogLight.PrimaryTextColor"),
        };

        /// <summary>
        /// 应用对话框主题到指定资源字典
        /// </summary>
        public static void ApplyTheme(ResourceDictionary resources, bool isDark)
        {
            if (resources == null) return;

            // 1. 更新自定义画刷
            foreach (var (brushKey, darkKey, lightKey) in CustomMappings)
            {
                object rawColor = isDark ? resources[darkKey] : resources[lightKey];
                if (rawColor is not Color targetColor) continue;

                if (resources[brushKey] is SolidColorBrush brush)
                {
                    // 关键修复：检查画刷是否被冻结（Frozen）
                    // 冻结的画刷是只读的，不能修改 Color 属性，必须创建新实例替换
                    if (brush.IsFrozen)
                        resources[brushKey] = new SolidColorBrush(targetColor);
                    else
                        brush.Color = targetColor;
                }
                else if (resources[brushKey] is Color)
                    resources[brushKey] = targetColor;
            }

            // 2. 全面覆盖 MaterialDesign 内置资源（始终创建新实例避免污染全局）
            foreach (var (mdKey, darkKey, lightKey) in MdOverrideMappings)
            {
                if (resources[isDark ? darkKey : lightKey] is not Color targetColor) continue;
                resources[mdKey] = new SolidColorBrush(targetColor);
            }
        }

        /// <summary>
        /// 递归应用主题到视觉树中的所有 FrameworkElement
        /// 确保嵌套控件（Border > Grid > TextBlock）的资源都能被更新
        /// </summary>
        public static void ApplyThemeRecursive(DependencyObject element, bool isDark)
        {
            if (element == null) return;

            // 对 FrameworkElement 应用主题
            if (element is FrameworkElement fe && fe.Resources != null)
            {
                ApplyTheme(fe.Resources, isDark);
            }

            // 递归处理子元素
            int childrenCount = VisualTreeHelper.GetChildrenCount(element);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                ApplyThemeRecursive(child, isDark);
            }
        }
    }
}

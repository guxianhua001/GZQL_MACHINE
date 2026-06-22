using System;
using Core.Abstraction;
using MaterialDesignThemes.Wpf;
using Prism.Mvvm;

namespace ModuleCore.Services
{
    /// <summary>
    /// 全局主题服务：管理暗色/明亮主题切换，持久化到 AppSettings
    /// 使用自定义调色板（深色非纯黑、亮色非纯白），提升层次感和视觉舒适度
    /// </summary>
    public class ThemeService : BindableBase, IThemeService
    {
        private readonly IAppSettingService _appSettingService;
        private bool _isDarkTheme;

        /// <summary>当前是否为暗色主题</summary>
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            private set => SetProperty(ref _isDarkTheme, value);
        }

        /// <summary>主题变化事件（参数：是否暗色）</summary>
        public event Action<bool> ThemeChanged;

        public ThemeService(IAppSettingService appSettingService)
        {
            _appSettingService = appSettingService ?? throw new ArgumentNullException(nameof(appSettingService));
        }

        /// <summary>切换主题（暗色↔明亮）</summary>
        public void ToggleTheme()
        {
            SetTheme(!_isDarkTheme);
        }

        /// <summary>
        /// 设置指定主题并应用全局
        /// </summary>
        /// <param name="isDark">true=暗色，false=明亮</param>
        public void SetTheme(bool isDark)
        {
            if (_isDarkTheme == isDark) return;

            IsDarkTheme = isDark;
            ApplyCustomTheme(isDark);
            SaveThemeToSettings(isDark);
            ThemeChanged?.Invoke(isDark);
        }

        /// <summary>从持久化配置加载主题并应用</summary>
        public void LoadThemeFromSettings()
        {
            var themeValue = _appSettingService.Settings.Theme ?? "Light";
            var isDark = string.Equals(themeValue, "Dark", StringComparison.OrdinalIgnoreCase);
            IsDarkTheme = isDark;
            ApplyCustomTheme(isDark);
        }

        /// <summary>
        /// 应用自定义主题：
        /// - 通过 PaletteHelper 切换基础主题（Dark / Light）
        /// - 具体颜色由 DialogTheme.xaml 中的 DynamicResource 资源字典提供
        /// - 深色/亮色的具体配色值定义在 DialogTheme.xaml 中
        /// </summary>
        private static void ApplyCustomTheme(bool isDark)
        {
            var paletteHelper = new PaletteHelper();
            var theme = paletteHelper.GetTheme();

            // 设置基础主题模式（Dark 或 Light）
            theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);

            paletteHelper.SetTheme(theme);
        }

        /// <summary>保存主题到 AppSettings</summary>
        private void SaveThemeToSettings(bool isDark)
        {
            try
            {
                _appSettingService.Settings.Theme = isDark ? "Dark" : "Light";
                _appSettingService.Save();
            }
            catch
            {
                // 主题保存失败不应影响应用运行
            }
        }
    }
}

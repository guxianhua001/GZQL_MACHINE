using System;
using System.Windows.Input;
using Core.Abstraction;
using MaterialDesignThemes.Wpf;
using Prism.Commands;
using Prism.Mvvm;

namespace ModuleCore.ViewModels
{
    /// <summary>
    /// 基础对话框窗口 ViewModel：管理标题、内容、主题切换和关闭
    /// </summary>
    public class BaseDialogWindowViewModel : BindableBase
    {
        private string _title;
        private object _content;
        private bool _isDarkTheme;
        private PackIconKind _themeIconKind = PackIconKind.WeatherSunny;

        /// <summary>窗口标题</summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>对话框内容（UserControl）</summary>
        public object Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        private string _iconKind;
        /// <summary>标题栏图标（MaterialDesign PackIcon Kind 名称），null 时显示默认齿轮图标</summary>
        public string IconKind
        {
            get => _iconKind;
            set => SetProperty(ref _iconKind, value);
        }

        /// <summary>是否为暗色主题</summary>
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (SetProperty(ref _isDarkTheme, value))
                    UpdateThemeIcon();
            }
        }

        /// <summary>主题切换图标</summary>
        public PackIconKind ThemeIconKind
        {
            get => _themeIconKind;
            set => SetProperty(ref _themeIconKind, value);
        }

        /// <summary>主题切换命令</summary>
        public DelegateCommand ToggleThemeCommand { get; }

        /// <summary>关闭命令</summary>
        public DelegateCommand CloseCommand { get; }

        /// <summary>请求关闭事件（参数为返回结果）</summary>
        public event Action<object> RequestClose;

        /// <summary>主题切换请求事件（通知窗口切换资源字典）</summary>
        public event Action<bool> ThemeToggleRequested;

        /// <summary>
        /// 构造函数：初始化命令
        /// </summary>
        public BaseDialogWindowViewModel()
        {
            ToggleThemeCommand = new DelegateCommand(OnToggleTheme);
            CloseCommand = new DelegateCommand(() => RequestClose?.Invoke(null));
        }

        /// <summary>切换主题</summary>
        private void OnToggleTheme()
        {
            IsDarkTheme = !IsDarkTheme;
            ThemeToggleRequested?.Invoke(IsDarkTheme);
        }

        /// <summary>更新主题图标</summary>
        private void UpdateThemeIcon()
        {
            ThemeIconKind = IsDarkTheme ? PackIconKind.WeatherNight : PackIconKind.WeatherSunny;
        }
    }
}

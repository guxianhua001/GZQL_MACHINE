using Core.Abstraction;
using MaterialDesignThemes.Wpf;
using Prism.Commands;
using Prism.Mvvm;
using System;

namespace Recipe.ViewModels
{
    /// <summary>
    /// 配方确认弹窗ViewModel，用于配方切换等操作的确认提示
    /// 使用MaterialDesign DialogHost全局弹窗，替代Prism IDialogService
    /// </summary>
    public class RecipeConfirmDialogViewModel : BindableBase
    {
        private readonly ILocalizationService _localization;
        private string _title;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private string _message;
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        private PackIconKind _iconKind;
        public PackIconKind IconKind
        {
            get => _iconKind;
            set => SetProperty(ref _iconKind, value);
        }

        private string _iconColor = "#FF9800";
        public string IconColor
        {
            get => _iconColor;
            set => SetProperty(ref _iconColor, value);
        }

        public DelegateCommand ConfirmCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public RecipeConfirmDialogViewModel(ILocalizationService localization)
        {
            _localization = localization;
            ConfirmCommand = new DelegateCommand(ExecuteConfirm);
            CancelCommand = new DelegateCommand(ExecuteCancel);

            // 默认标题本地化（Initialize 调用时通常会被覆盖）
            _title = _localization.GetResourceOrDefault("RCD_Title_ConfirmOperation", "确认操作");
            _iconKind = ResolveIconKind("Information");
        }

        /// <summary>
        /// 通过图标名称运行时解析PackIconKind，避免编译时与运行时枚举值不一致导致图标显示错误
        /// </summary>
        private static PackIconKind ResolveIconKind(string iconName, string fallbackName = "Information")
        {
            if (!string.IsNullOrEmpty(iconName) && Enum.TryParse<PackIconKind>(iconName, out var result))
                return result;
            if (Enum.TryParse<PackIconKind>(fallbackName, out var fallback))
                return fallback;
            return default;
        }

        /// <summary>
        /// 初始化弹窗内容，iconName使用字符串名称在运行时解析，避免PackIconKind枚举版本兼容问题
        /// </summary>
        public void Initialize(string title, string message, string iconName = null, string iconColor = null)
        {
            Title = title;
            Message = message;
            if (!string.IsNullOrEmpty(iconName))
                IconKind = ResolveIconKind(iconName);
            if (iconColor != null) IconColor = iconColor;
        }

        private void ExecuteConfirm()
        {
            DialogHost.Close("MainDialogHost", true);
        }

        private void ExecuteCancel()
        {
            DialogHost.Close("MainDialogHost", false);
        }
    }
}

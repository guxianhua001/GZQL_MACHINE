using System;
using System.Collections.ObjectModel;
using MaterialDesignThemes.Wpf;
using Framework.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;

namespace ModuleCore.ViewModels
{
    /// <summary>
    /// 多功能自定义弹窗 ViewModel
    /// 支持可配置图标/标题/消息/动态按钮列表
    /// </summary>
    public class CustomDialogViewModel : BindableBase, IDialogAware
    {
        #region 属性

        private string _title = "提示";
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

        private PackIconKind _iconKind = PackIconKind.InfoOutline;
        /// <summary>标题区图标</summary>
        public PackIconKind IconKind
        {
            get => _iconKind;
            set => SetProperty(ref _iconKind, value);
        }

        private string _iconForeground = "#FF9800";
        /// <summary>标题区图标颜色</summary>
        public string IconForeground
        {
            get => _iconForeground;
            set => SetProperty(ref _iconForeground, value);
        }

        /// <summary>动态按钮列表</summary>
        public ObservableCollection<DialogButton> Buttons { get; } = new ObservableCollection<DialogButton>();

        #endregion

        #region IDialogAware

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            // 基础属性
            if (parameters.ContainsKey("title"))
                Title = parameters.GetValue<string>("title");
            if (parameters.ContainsKey("message"))
                Message = parameters.GetValue<string>("message");
            if (parameters.ContainsKey("iconKind"))
                IconKind = parameters.GetValue<PackIconKind>("iconKind");
            if (parameters.ContainsKey("iconForeground"))
                IconForeground = parameters.GetValue<string>("iconForeground");

            // 动态按钮列表
            if (parameters.ContainsKey("buttons"))
            {
                var buttons = parameters.GetValue<ObservableCollection<DialogButton>>("buttons");
                if (buttons != null)
                {
                    foreach (var btn in buttons)
                    {
                        // 注入点击命令（闭包捕获索引）
                        var capturedIndex = btn.ButtonIndex;
                        btn.ClickCommand = new DelegateCommand(() => CloseWithResult(capturedIndex));
                        Buttons.Add(btn);
                    }
                }
            }
        }

        #endregion

        public CustomDialogViewModel() { }

        /// <summary>关闭弹窗并返回按钮索引</summary>
        private void CloseWithResult(int buttonIndex)
        {
            var result = buttonIndex >= 0 ? ButtonResult.OK : ButtonResult.Cancel;
            var parameters = new DialogParameters { { "buttonIndex", buttonIndex } };
            RequestClose?.Invoke(new DialogResult(result, parameters));
        }
    }
}

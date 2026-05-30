using Core.Abstraction;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Data;

namespace ModuleCore.ViewModels
{
    public class LogViewViewModel : BindableBase, IDialogAware
    {
        private readonly ILocalizationService _localization;
        private string _title;

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        public LogViewViewModel(ILocalizationService localizationService)
        {
            _localization = localizationService;
            _title = _localization.GetResourceOrDefault("LogView_Title", "日志");
        }
        //窗体关闭
        public virtual void RaiseRequestClose(IDialogResult dialogResult)
        {
            RequestClose?.Invoke(dialogResult);
        }
        //窗体打开
        public void OnDialogOpened(IDialogParameters parameters)
        {
          
        }
        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
        }

    }
}

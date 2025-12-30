using ModuleCore.Common;
using ModuleCore.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Data;

namespace ModuleCore.ViewModels
{
    public class LogViewerViewModel : BindableBase, IDialogAware
    {
        private string _title = "日志";

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
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

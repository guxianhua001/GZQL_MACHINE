using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Module.ViewModels
{
    public class PathConfigViewModel : BindableBase
    {
        private readonly IDialogService _dialogService;
        public PathConfigViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

    }
}

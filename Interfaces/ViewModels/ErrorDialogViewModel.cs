using Prism.Commands;
using Prism.Mvvm;
using System;
using Prism.Services.Dialogs;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Input;
using System.Runtime.CompilerServices;
using MaterialDesignThemes.Wpf;

namespace Interfaces
{
    // ErrorDialogViewModel.cs
    public class ErrorDialogViewModel : INotifyPropertyChanged
    {
        public event Action<ButtonResult> RequestClose;

        private string _title = "错误";
        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged();
            }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }
        private PackIconKind _iconKind = PackIconKind.AlertCircle;
        public PackIconKind IconKind
        {
            get => _iconKind;
            set
            {
                _iconKind = value;
                OnPropertyChanged();
            }
        }
        public ICommand ContinueCommand { get; }
        public ICommand PauseCommand { get; }

        public ErrorDialogViewModel()
        {
            ContinueCommand = new RelayCommand(() => RequestClose?.Invoke(ButtonResult.Yes));
            PauseCommand = new RelayCommand(() => RequestClose?.Invoke(ButtonResult.No));
        }

        // 实现 INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

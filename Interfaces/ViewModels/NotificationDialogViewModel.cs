using Interfaces;
using MaterialDesignThemes.Wpf;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

public class NotificationDialogViewModel : INotifyPropertyChanged
{
    public event Action<ButtonResult> RequestClose;

    private string _title = "Notification";
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged();
        }
    }

    private string _message;
    public string Message
    {
        get => _message;
        set
        {
            _message = value;
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
    public ICommand CloseCommand { get; }

    public NotificationDialogViewModel()
    {
        CloseCommand = new RelayCommand(() => RequestClose?.Invoke(ButtonResult.Yes));
    }

    // 实现 INotifyPropertyChanged
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

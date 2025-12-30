using Interfaces;
using Prism.Commands;
using Prism.Mvvm;
using Stations;
using System.Collections.ObjectModel;

namespace Framework.ViewModels
{
    public class PointStatusVerificationViewModel : BindableBase
    {
        private readonly ITaskWithPoints _task;

        // 二维码绑定属性
        public string TaskMaterialQRCode => _task.MaterialQRCode;

        public PointStatusVerificationViewModel(ITaskWithPoints task)
        {
            _task = task;
            //RaisePropertyChanged(nameof(TaskMaterialQRCode));
        }

        public PinMapViewModel MapViewModel { get; set; }
        public ObservableCollection<PointViewModel> Points =>
            new ObservableCollection<PointViewModel>(_task.PinPoints);

        public DelegateCommand ConfirmCommand { get; set; }
    }

}

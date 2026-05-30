using Core.Models;
using StationTasks.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;

namespace Module.ViewModels
{
    public class AddEditStepDialogViewModel : BindableBase, IDialogAware
    {
        private ProcessStep _step;
        public ProcessStep Step
        {
            get => _step;
            set => SetProperty(ref _step, value);
        }

        private List<string> _componentFeatures = new List<string>();
        public List<string> ComponentFeatures
        {
            get => _componentFeatures;
            set
            {
                _componentFeatures = value;
                RaisePropertyChanged(); // 通知 UI 更新
            }
        }

        private List<string> _siteFeatures = new List<string>();
        public List<string> SiteFeatures
        {
            get => _siteFeatures;
            set
            {
                _siteFeatures = value;
                RaisePropertyChanged();
            }
        }

        private List<string> _cameraOptions = new List<string>();
        public List<string> CameraOptions
        {
            get => _cameraOptions;
            set
            {
                _cameraOptions = value;
                RaisePropertyChanged();
            }
        }

        public Array StepTypeValues => Enum.GetValues(typeof(StepType));

        public DelegateCommand OkCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public AddEditStepDialogViewModel()
        {
            Step = new ProcessStep { Seq = 0, Step = StepType.GOTO, CompFeature = "—", SiteFeature = "—", Camera = "—" };
            OkCommand = new DelegateCommand(OnOk);
            CancelCommand = new DelegateCommand(OnCancel);
        }

        private void OnOk()
        {
            var parameters = new DialogParameters { { "step", Step } };
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
        }

        private void OnCancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public string Title => "Add/Edit Step";
        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("componentFeatures"))
                ComponentFeatures = parameters.GetValue<List<string>>("componentFeatures");
            if (parameters.ContainsKey("siteFeatures"))
                SiteFeatures = parameters.GetValue<List<string>>("siteFeatures");
            if (parameters.ContainsKey("cameraOptions"))
                CameraOptions = parameters.GetValue<List<string>>("cameraOptions");
        }
    }
}
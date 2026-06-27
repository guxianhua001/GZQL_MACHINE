using Core.Abstraction;
using StationTasks.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;

namespace Module.ViewModels
{
    /// <summary>
    /// 添加/编辑工艺步骤对话框 ViewModel
    /// </summary>
    public class AddEditStepDialogViewModel : BindableBase, IDialogAware
    {
        private readonly ILocalizationService _localization;

        private ProcessStep _step;
        public ProcessStep Step
        {
            get => _step;
            set => SetProperty(ref _step, value);
        }

        private string _title = string.Empty;
        /// <summary>对话框标题（随语言切换刷新）</summary>
        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
        }

        private List<string> _componentFeatures = new List<string>();
        public List<string> ComponentFeatures
        {
            get => _componentFeatures;
            set
            {
                _componentFeatures = value;
                RaisePropertyChanged();
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

        public event Action<IDialogResult> RequestClose;

        public AddEditStepDialogViewModel(ILocalizationService localization)
        {
            _localization = localization;
            RefreshLocalizedTitle();
            _localization.LanguageChanged += OnLanguageChanged;

            Step = new ProcessStep { Seq = 0, Step = StepType.GOTO, CompFeature = "—", SiteFeature = "—", Camera = "—" };
            OkCommand = new DelegateCommand(OnOk);
            CancelCommand = new DelegateCommand(OnCancel);
        }

        /// <summary>刷新对话框标题等多语言文本</summary>
        private void RefreshLocalizedTitle()
        {
            Title = _localization.GetResource("PSE_AddEditStepTitle");
        }

        private void OnLanguageChanged(object sender, LanguageChangedEventArgs e)
        {
            RefreshLocalizedTitle();
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

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }

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

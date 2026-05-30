using Core.Models;
using Module.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.ObjectModel;

namespace Module.ViewModels
{
    public class FeatureEditorDialogViewModel : BindableBase, IDialogAware
    {
        private string _featureType;
        private ComponentFeature _componentFeature;
        private SiteFeature _siteFeature;

        private string _title;
        private string _componentFeatureId;
        private string _componentFeatureName;
        private string _componentFeatureDescription;
        private string _siteFeatureId;
        private string _siteFeatureName;
        private string _siteFeatureTypeString;
        private string _subAssy;

        public string Title
        {
            get => _title;
            private set => SetProperty(ref _title, value);
        }

        public string FeatureType
        {
            get => _featureType;
            set => SetProperty(ref _featureType, value);
        }

        // Component Feature properties
        public string ComponentFeatureId
        {
            get => _componentFeatureId;
            set => SetProperty(ref _componentFeatureId, value);
        }

        public string ComponentFeatureName
        {
            get => _componentFeatureName;
            set => SetProperty(ref _componentFeatureName, value);
        }

        public string ComponentFeatureDescription
        {
            get => _componentFeatureDescription;
            set => SetProperty(ref _componentFeatureDescription, value);
        }

        // Site Feature properties
        public string SiteFeatureId
        {
            get => _siteFeatureId;
            set => SetProperty(ref _siteFeatureId, value);
        }

        public string SiteFeatureName
        {
            get => _siteFeatureName;
            set => SetProperty(ref _siteFeatureName, value);
        }

        public string SiteFeatureTypeString
        {
            get => _siteFeatureTypeString;
            set => SetProperty(ref _siteFeatureTypeString, value);
        }

        public string SubAssy
        {
            get => _subAssy;
            set => SetProperty(ref _subAssy, value);
        }

        // 下拉选项（静态）
        public ObservableCollection<string> SiteFeatureTypeOptions { get; }
            = new ObservableCollection<string> { "Site", "Dispense", "AssyGroup" };

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public event Action<IDialogResult> RequestClose;

        public FeatureEditorDialogViewModel()
        {
            SaveCommand = new DelegateCommand(OnSave);
            CancelCommand = new DelegateCommand(OnCancel);
        }

        public bool CanCloseDialog() => true;
        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            FeatureType = parameters.GetValue<string>("featureType");
            Title = _featureType == "Component" ? "Edit Component Feature" : "Edit Site Feature";

            if (_featureType == "Component")
            {
                _componentFeature = parameters.GetValue<ComponentFeature>("feature") ?? new ComponentFeature();
                ComponentFeatureId = _componentFeature.Id;
                ComponentFeatureName = _componentFeature.Name;
                ComponentFeatureDescription = _componentFeature.Description;
            }
            else // Site
            {
                _siteFeature = parameters.GetValue<SiteFeature>("feature") ?? new SiteFeature();
                SiteFeatureId = _siteFeature.Id;
                SiteFeatureName = _siteFeature.Name;
                SiteFeatureTypeString = _siteFeature.Type.ToString();
                SubAssy = _siteFeature.Description;
            }
        }

        private void OnSave()
        {
            var result = new DialogResult(ButtonResult.OK);
            if (_featureType == "Component")
            {
                _componentFeature.Id = ComponentFeatureId;
                _componentFeature.Name = ComponentFeatureName;
                _componentFeature.Description = ComponentFeatureDescription;
                result.Parameters.Add("feature", _componentFeature);
            }
            else
            {
                _siteFeature.Id = SiteFeatureId;
                _siteFeature.Name = SiteFeatureName;
                _siteFeature.Type = Enum.Parse<SiteFeatureType>(SiteFeatureTypeString);
                _siteFeature.Description = SubAssy;
                result.Parameters.Add("feature", _siteFeature);
            }
            RequestClose?.Invoke(result);
        }

        private void OnCancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }
    }
}
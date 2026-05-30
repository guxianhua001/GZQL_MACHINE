using Framework.Dialogs;
using MaterialDesignThemes.Wpf;
using Module.Models;
using Module.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using Recipe;
using Recipe.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Module.ViewModels
{
    public class WorkOrderConfigViewModel : BindableBase, INavigationAware, IDisposable
    {
        #region 字段
        private readonly IRecipePoolService _recipePoolService;
        private readonly IDialogService _dialogService;
        private readonly IProcessSequenceService _sequenceService;
        private const string WORK_ORDER_DATA_KEY = "WorkOrderData";
        #endregion

        #region 属性
        private ObservableCollection<Models.Component> _components;
        private Models.Component _selectedComponent;
        private ComponentFeature _selectedComponentFeature;
        private SiteFeature _selectedSiteFeature;
        private ObservableCollection<CameraConstant> _cameras;
        private ObservableCollection<PurposeConstant> _purposes;
        private ObservableCollection<AxisConstant> _axes;
        private CameraConstant _selectedCamera;
        private PurposeConstant _selectedPurpose;
        private AxisConstant _selectedAxis;
        public Array SiteFeatureTypes => Enum.GetValues(typeof(SiteFeatureType));
        public ObservableCollection<string> SiteFeatureTypeOptions { get; }
            = new ObservableCollection<string> { "site", "dispenser", "assy" };

        public ObservableCollection<Models.Component> Components
        {
            get => _components;
            set => SetProperty(ref _components, value);
        }

        public Models.Component SelectedComponent
        {
            get => _selectedComponent;
            set
            {
                if (SetProperty(ref _selectedComponent, value))
                {
                    (EditComponentCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    (DeleteComponentCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ComponentFeature SelectedComponentFeature
        {
            get => _selectedComponentFeature;
            set => SetProperty(ref _selectedComponentFeature, value);
        }

        public ObservableCollection<CameraConstant> Cameras
        {
            get => _cameras;
            set => SetProperty(ref _cameras, value);
        }

        public ObservableCollection<PurposeConstant> Purposes
        {
            get => _purposes;
            set => SetProperty(ref _purposes, value);
        }

        public ObservableCollection<AxisConstant> Axes
        {
            get => _axes;
            set => SetProperty(ref _axes, value);
        }

        public CameraConstant SelectedCamera
        {
            get => _selectedCamera;
            set
            {
                if (SetProperty(ref _selectedCamera, value))
                {
                    (DeleteCameraCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public PurposeConstant SelectedPurpose
        {
            get => _selectedPurpose;
            set
            {
                if (SetProperty(ref _selectedPurpose, value))
                {
                    (DeletePurposeCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public AxisConstant SelectedAxis
        {
            get => _selectedAxis;
            set => SetProperty(ref _selectedAxis, value);
        }

        private ObservableCollection<Site> _sites;
        public ObservableCollection<Site> Sites
        {
            get => _sites;
            set => SetProperty(ref _sites, value);
        }

        private Site _selectedSite;
        public Site SelectedSite
        {
            get => _selectedSite;
            set
            {
                if (SetProperty(ref _selectedSite, value))
                {
                    RefreshCurrentGroupSiteFeatures();
                }
            }
        }

        public SiteFeature SelectedSiteFeature
        {
            get => _selectedSiteFeature;
            set => SetProperty(ref _selectedSiteFeature, value);
        }

        private ObservableCollection<SiteFeature> _currentGroupSiteFeatures;
        public ObservableCollection<SiteFeature> CurrentGroupSiteFeatures
        {
            get => _currentGroupSiteFeatures;
            set => SetProperty(ref _currentGroupSiteFeatures, value);
        }

        private PropertyChangedEventHandler _propertyChangedHandler;
        private string _currentPoolName;
        public string CurrentPoolName
        {
            get => _currentPoolName;
            set => SetProperty(ref _currentPoolName, value);
        }
        #endregion

        public WorkOrderConfigViewModel(
            IRecipePoolService recipeExecutionService,
            IProcessSequenceService sequenceService,
            IDialogService dialogService)
        {
            _recipePoolService = recipeExecutionService;
            _dialogService = dialogService;
            _sequenceService = sequenceService;

            LoadDataCommand = new DelegateCommand(OnLoadData);
            SaveDataCommand = new DelegateCommand(OnSaveData);

            AddComponentFeatureCommand = new DelegateCommand(OnAddComponentFeature);
            EditComponentFeatureCommand = new DelegateCommand(OnEditComponentFeature, () => SelectedComponentFeature != null).ObservesProperty(() => SelectedComponentFeature);
            DeleteComponentFeatureCommand = new DelegateCommand(OnDeleteComponentFeature, () => SelectedComponentFeature != null).ObservesProperty(() => SelectedComponentFeature);

            AddSiteFeatureCommand = new DelegateCommand(OnAddSiteFeature);
            EditSiteFeatureCommand = new DelegateCommand(OnEditSiteFeature, () => SelectedSiteFeature != null).ObservesProperty(() => SelectedSiteFeature);
            DeleteSiteFeatureCommand = new DelegateCommand(OnDeleteSiteFeature, () => SelectedSiteFeature != null).ObservesProperty(() => SelectedSiteFeature);

            AddAxisCommand = new DelegateCommand(OnAddAxis);
            EditAxisCommand = new DelegateCommand(OnEditAxis, () => SelectedAxis != null).ObservesProperty(() => SelectedAxis);
            DeleteAxisCommand = new DelegateCommand(OnDeleteAxis, () => SelectedAxis != null).ObservesProperty(() => SelectedAxis);

            AddGroupCommand = new DelegateCommand(OnAddGroup);
            EditGroupCommand = new DelegateCommand(OnEditGroup, () => SelectedSite != null).ObservesProperty(() => SelectedSite);
            DeleteGroupCommand = new DelegateCommand(OnDeleteGroup, () => SelectedSite != null).ObservesProperty(() => SelectedSite);

            AddCameraCommand = new DelegateCommand(OnAddCamera);
            DeleteCameraCommand = new DelegateCommand(OnDeleteCamera, () => SelectedCamera != null).ObservesProperty(() => SelectedCamera);
            AddPurposeCommand = new DelegateCommand(OnAddPurpose);
            DeletePurposeCommand = new DelegateCommand(OnDeletePurpose, () => SelectedPurpose != null).ObservesProperty(() => SelectedPurpose);

            AddComponentCommand = new DelegateCommand(OnAddComponent);
            EditComponentCommand = new DelegateCommand(OnEditComponent, () => SelectedComponent != null).ObservesProperty(() => SelectedComponent);
            DeleteComponentCommand = new DelegateCommand(OnDeleteComponent, () => SelectedComponent != null).ObservesProperty(() => SelectedComponent);

            // 订阅配方池变化
            if (_recipePoolService is INotifyPropertyChanged inpc)
            {
                _propertyChangedHandler = (s, e) =>
                {
                    if (e.PropertyName == nameof(IRecipePoolService.CurrentPoolName))
                    {
                        // 延迟加载，确保新的池已完全切换
                        Application.Current?.Dispatcher.InvokeAsync(OnLoadData);
                    }
                };
                inpc.PropertyChanged += _propertyChangedHandler;
            }

            OnLoadData();
        }

        #region 命令
        public ICommand LoadDataCommand { get; }
        public ICommand SaveDataCommand { get; }
        public ICommand AddComponentFeatureCommand { get; }
        public ICommand EditComponentFeatureCommand { get; }
        public ICommand DeleteComponentFeatureCommand { get; }
        public ICommand AddSiteFeatureCommand { get; }
        public ICommand EditSiteFeatureCommand { get; }
        public ICommand DeleteSiteFeatureCommand { get; }
        public ICommand AddAxisCommand { get; }
        public ICommand EditAxisCommand { get; }
        public ICommand DeleteAxisCommand { get; }
        public ICommand AddGroupCommand { get; }
        public ICommand EditGroupCommand { get; }
        public ICommand DeleteGroupCommand { get; }
        public ICommand AddCameraCommand { get; }
        public ICommand DeleteCameraCommand { get; }
        public ICommand AddPurposeCommand { get; }
        public ICommand DeletePurposeCommand { get; }
        public ICommand AddComponentCommand { get; }
        public ICommand EditComponentCommand { get; }
        public ICommand DeleteComponentCommand { get; }
        #endregion

        #region 数据加载保存
        private async void OnLoadData()
        {
            try
            {
                string currentPool = _recipePoolService.CurrentPoolName ?? "Default";
                CurrentPoolName = currentPool; // 同步显示
                var workOrderData = await _recipePoolService.GetExtensionDataAsync<WorkOrderData>(currentPool, WORK_ORDER_DATA_KEY)
                                    ?? new WorkOrderData();

                Components = workOrderData.Components ?? new ObservableCollection<Models.Component>();
                Sites = workOrderData.Sites ?? new ObservableCollection<Site>();
                Cameras = workOrderData.Cameras ?? new ObservableCollection<CameraConstant>();
                Purposes = workOrderData.Purposes ?? new ObservableCollection<PurposeConstant>();
                Axes = workOrderData.Axes ?? new ObservableCollection<AxisConstant>();

                if (Components.Any())
                    SelectedComponent = Components.First();
                if (Sites.Any())
                    SelectedSite = Sites.First();
            }
            catch (Exception ex)
            {
                await ShowErrorMessage($"加载工单配置失败: {ex.Message}");
            }
        }

        private async void OnSaveData()
        {
            try
            {
                string currentPool = _recipePoolService.CurrentPoolName ?? "Default";
                var workOrderData = new WorkOrderData
                {
                    Components = Components,
                    Sites = Sites,
                    Cameras = Cameras,
                    Purposes = Purposes,
                    Axes = Axes,
                };

                await _recipePoolService.UpdateRecipePoolAsync(currentPool, pool =>
                {
                    pool.ExtensionData[WORK_ORDER_DATA_KEY] = JsonSerializer.SerializeToElement(workOrderData);
                });

                // 刷新序列服务的数据，同步下拉选项
                await _sequenceService.ReloadWorkOrderDataAsync();

                await ShowSuccessMessage($"工单配置已保存到配方池 {currentPool}");
            }
            catch (Exception ex)
            {
                await ShowErrorMessage($"保存失败: {ex.Message}");
            }
        }

        #endregion

        #region 特征编辑方法（保持不变）
        private void OnAddComponentFeature()
        {
            _dialogService.ShowDialog("FeatureEditorDialog", new DialogParameters { { "featureType", "Component" } }, result =>
            {
                if (result.Result == ButtonResult.OK && SelectedComponent != null)
                {
                    var newFeature = result.Parameters.GetValue<ComponentFeature>("feature");
                    SelectedComponent.Features.Add(newFeature);
                }
            });
        }

        private void OnEditComponentFeature()
        {
            if (SelectedComponentFeature == null) return;
            var parameters = new DialogParameters { { "feature", SelectedComponentFeature }, { "featureType", "Component" } };
            _dialogService.ShowDialog("FeatureEditorDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK && SelectedComponentFeature != null)
                {
                    var updated = result.Parameters.GetValue<ComponentFeature>("feature");
                    SelectedComponentFeature.Name = updated.Name;
                    SelectedComponentFeature.Description = updated.Description;
                }
            });
        }

        private void OnDeleteComponentFeature()
        {
            if (SelectedComponentFeature == null) return;
            SelectedComponent.Features.Remove(SelectedComponentFeature);
        }

        private void OnAddSiteFeature()
        {
            if (SelectedSite == null) return;
            var parameters = new DialogParameters { { "featureType", "Site" } };
            _dialogService.ShowDialog("FeatureEditorDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var newFeature = result.Parameters.GetValue<SiteFeature>("feature");
                    SelectedSite.Features.Add(newFeature);
                    RefreshCurrentGroupSiteFeatures();
                }
            });
        }

        private void OnEditSiteFeature()
        {
            if (SelectedSiteFeature == null) return;
            var parameters = new DialogParameters
            {
                { "feature", SelectedSiteFeature },
                { "featureType", "Site" }
            };
            _dialogService.ShowDialog("FeatureEditorDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var updated = result.Parameters.GetValue<SiteFeature>("feature");
                    SelectedSiteFeature.Id = updated.Id;
                    SelectedSiteFeature.Name = updated.Name;
                    SelectedSiteFeature.Type = updated.Type;
                    SelectedSiteFeature.Description = updated.Description;
                    RefreshCurrentGroupSiteFeatures();
                }
            });
        }

        private void OnDeleteSiteFeature()
        {
            if (SelectedSiteFeature == null) return;
            SelectedSite.Features.Remove(SelectedSiteFeature);
            RefreshCurrentGroupSiteFeatures();
        }

        private void RefreshCurrentGroupSiteFeatures()
        {
            CurrentGroupSiteFeatures = SelectedSite?.Features ?? new ObservableCollection<SiteFeature>();
        }
        #endregion

        #region 其他编辑方法（保持不变）
        private void OnAddAxis()
        {
            _dialogService.ShowDialog("AxisEditorDialog", new DialogParameters { { "axis", new AxisConstant() } }, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var newAxis = result.Parameters.GetValue<AxisConstant>("axis");
                    Axes.Add(newAxis);
                }
            });
        }

        private void OnEditAxis()
        {
            if (SelectedAxis == null) return;
            var parameters = new DialogParameters { { "axis", SelectedAxis } };
            _dialogService.ShowDialog("AxisEditorDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK && SelectedAxis != null)
                {
                    var updated = result.Parameters.GetValue<AxisConstant>("axis");
                    SelectedAxis.Group = updated.Group;
                    SelectedAxis.Name = updated.Name;
                    SelectedAxis.Description = updated.Description;
                }
            });
        }

        private void OnDeleteAxis()
        {
            if (SelectedAxis != null)
                Axes.Remove(SelectedAxis);
        }

        private void OnAddCamera()
        {
            Cameras.Add(new CameraConstant { Name = "New Camera", Description = "" });
        }

        private void OnDeleteCamera()
        {
            if (SelectedCamera != null)
                Cameras.Remove(SelectedCamera);
        }

        private void OnAddPurpose()
        {
            Purposes.Add(new PurposeConstant { Name = "New Purpose", Description = "" });
        }

        private void OnDeletePurpose()
        {
            if (SelectedPurpose != null)
                Purposes.Remove(SelectedPurpose);
        }

        private void OnAddGroup()
        {
            _dialogService.ShowDialog("GroupEditorDialog", null, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var newGroup = result.Parameters.GetValue<Site>("group");
                    int nextId = Sites.Count + 1;
                    newGroup.Id = nextId.ToString("D3");
                    newGroup.Type = "";
                    newGroup.Description = "";
                    newGroup.Features = new ObservableCollection<SiteFeature>();
                    Sites.Add(newGroup);
                    SelectedSite = newGroup;
                }
            });
        }

        private void OnEditGroup()
        {
            if (SelectedSite == null) return;
            var parameters = new DialogParameters { { "group", SelectedSite } };
            _dialogService.ShowDialog("GroupEditorDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var updated = result.Parameters.GetValue<Site>("group");
                    SelectedSite.Id = updated.Id;
                    SelectedSite.Name = updated.Name;
                }
            });
        }

        private void OnDeleteGroup()
        {
            if (SelectedSite == null) return;
            if (SelectedSite.Features.Any())
            {
                _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "无法删除包含特征的组。" } }, null);
                return;
            }
            Sites.Remove(SelectedSite);
            SelectedSite = Sites.FirstOrDefault();
        }

        private void OnAddComponent()
        {
            var parameters = new DialogParameters();
            _dialogService.ShowDialog("SimpleInputDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var name = result.Parameters.GetValue<string>("value");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        var newComponent = new Models.Component { Name = name };
                        Components.Add(newComponent);
                        SelectedComponent = newComponent;
                    }
                }
            });
        }

        private void OnEditComponent()
        {
            if (SelectedComponent == null) return;
            var parameters = new DialogParameters { { "value", SelectedComponent.Name } };
            _dialogService.ShowDialog("SimpleInputDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var newName = result.Parameters.GetValue<string>("value");
                    if (!string.IsNullOrWhiteSpace(newName))
                        SelectedComponent.Name = newName;
                }
            });
        }

        private void OnDeleteComponent()
        {
            if (SelectedComponent == null) return;
            _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters { { "message", $"Are you sure you want to delete the part {SelectedComponent.Name} and all its features?" } }, r =>
            {
                if (r.Result == ButtonResult.Yes)
                {
                    Components.Remove(SelectedComponent);
                    SelectedComponent = Components.FirstOrDefault();
                }
            });
        }
        #endregion

        #region 消息提示
        private async Task ShowSuccessMessage(string message)
        {
            await _dialogService.ShowDialogAsync("NotificationDialog", new DialogParameters
            {
                { "title", "成功" },
                { "message", message },
                { "icon", PackIconKind.CheckCircle }
            });
        }

        private async Task ShowErrorMessage(string message)
        {
            await _dialogService.ShowDialogAsync("NotificationDialog", new DialogParameters
            {
                { "title", "错误" },
                { "message", message },
                { "icon", PackIconKind.Error }
            });
        }
        #endregion

        #region INavigationAware
        public void OnNavigatedTo(NavigationContext navigationContext) { }
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            Dispose();
        }
        // 实现 IDisposable 取消订阅
        public void Dispose()
        {
            if (_recipePoolService is INotifyPropertyChanged inpc && _propertyChangedHandler != null)
            {
                inpc.PropertyChanged -= _propertyChangedHandler;
            }
        }
        #endregion
    }
}
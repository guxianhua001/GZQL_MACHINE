using Core.Abstraction;
using Framework.Mvvm;
using Framework.Views;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using Stations;
using System;
using System.Collections.ObjectModel;

namespace Framework.ViewModels
{
    /// <summary>
    /// 主流线的轴控件视图模型，用于显示和操作各个轴。
    /// </summary>
    public class LoaderStationAxesViewModel : RegionViewModelBase, INavigationAware
    {
        private readonly TaskInstanceManager _taskManager;
        private readonly LoadingStation _LoadingStation;
        private readonly ILocalizationService _localizationService;
        public ObservableCollection<AxisViewModel> Axes { get; } = new ObservableCollection<AxisViewModel>();//自动绑定轴控件
        public LoaderStationAxesViewModel(
            IRegionManager regionManager,
            TaskInstanceManager taskManager,
            ILocalizationService localizationService) : base(regionManager)
        {
            _taskManager = taskManager;
            _localizationService = localizationService;
            _LoadingStation = _taskManager.GetTask<LoadingStation>();
            InitializeAxes();
        }
        private void InitializeAxes()
        {
            // XDevice.Instance.axisMap 包含所有轴的映射
            foreach (var axis in _LoadingStation.AxisMap.Values)
            {
                Axes.Add(new AxisViewModel(axis, RegionManager, _localizationService));
            }
        }
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            // 调用父视图模型的 OnNavigatedTo 方法
            base.OnNavigatedTo(navigationContext);

            // 调用每个 AxisViewModel 的 OnNavigatedTo 方法
            foreach (var axisViewModel in Axes)
            {
                if (axisViewModel is INavigationAware navigationAware)
                {
                    navigationAware.OnNavigatedTo(navigationContext);
                }
            }
        }

        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            foreach (var axisViewModel in Axes)
            {
                if (axisViewModel is INavigationAware navigationAware)
                {
                    navigationAware.OnNavigatedFrom(navigationContext);
                }
            }
        }
    }
}

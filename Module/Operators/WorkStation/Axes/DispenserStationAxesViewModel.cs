using Core.Abstraction;
using Framework.Mvvm;
using Prism.Mvvm;
using Prism.Regions;
using SmarterMotion;
using Stations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.ViewModels
{
    /// <summary>
    /// GantryStationAxesViewModel 用于管理 GantryStation 的轴控件。
    /// </summary>
    public class DispenserStationAxesViewModel : RegionViewModelBase, INavigationAware
    {
        private readonly DispenserStation _DispenserStation;
        private readonly TaskInstanceManager _taskManager;
        private readonly ILocalizationService _localizationService;
        public ObservableCollection<AxisViewModel> Axes { get; } = new ObservableCollection<AxisViewModel>();//自动绑定轴控件
        public DispenserStationAxesViewModel(
            IRegionManager regionManager, 
            TaskInstanceManager taskManager,
            ILocalizationService localizationService) : base(regionManager)
        {
            _taskManager = taskManager;
            _localizationService = localizationService;
            _DispenserStation = _taskManager.GetTask<DispenserStation>();
            InitializeAxes();
        }
        private void InitializeAxes()
        {
            // XDevice.Instance.axisMap 包含所有轴的映射
            foreach (var axis in _DispenserStation.AxisMap.Values)
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

using Framework.Mvvm;
using Prism.Ioc;
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
    public class AssemblyStationAxesViewModel : RegionViewModelBase, INavigationAware
    {

        private readonly AssemblyStation _AssemblyStation;
        private readonly TaskInstanceManager _taskManager;
        public ObservableCollection<AxisViewModel> Axes { get; } = new ObservableCollection<AxisViewModel>();//自动绑定轴控件

        public AssemblyStationAxesViewModel(IRegionManager regionManager, TaskInstanceManager taskManager) : base(regionManager)
        {
            _taskManager = taskManager;
            _AssemblyStation = _taskManager.GetTask<AssemblyStation>();
            InitializeAxes();
        }
        private void InitializeAxes()
        {
            // XDevice.Instance.axisMap 包含所有轴的映射
            foreach (var axis in _AssemblyStation.AxisMap.Values)
            {
                Axes.Add(new AxisViewModel(axis, RegionManager));
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

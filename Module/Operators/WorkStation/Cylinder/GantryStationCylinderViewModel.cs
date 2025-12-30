using Framework.Mvvm;
using Prism.Mvvm;
using Prism.Regions;
using Stations;
using System.Collections.ObjectModel;
namespace Framework.ViewModels
{
    public class GantryStationCylinderViewModel : RegionViewModelBase, INavigationAware
    {
        private ObservableCollection<CylinderViewModel> _cylinders;
        public ObservableCollection<CylinderViewModel> Cylinders
        {
            get => _cylinders;
            set => SetProperty(ref _cylinders, value);
        }

        public GantryStationCylinderViewModel(IRegionManager regionManager, TaskInstanceManager taskManager) : base(regionManager)
        {
            Cylinders = new ObservableCollection<CylinderViewModel>();
            InitializeCylinders();
        }
        private void InitializeCylinders()
        {
            Cylinders.Clear();

            // 使用工厂模式创建实例
            var configs = new[]
            {
                new  { CylName = "夹爪气缸", SetDo1Id = 31, SetDi1Id = 52, SetDo2Id = 30, SetDi2Id = 53 }
            };

            foreach (var config in configs)
            {
                Cylinders.Add(new CylinderViewModel
                {
                    CylName = config.CylName,
                    SetDo1Id = config.SetDo1Id,
                    SetDi1Id = config.SetDi1Id,
                    SetDo2Id = config.SetDo2Id,
                    SetDi2Id = config.SetDi2Id
                });
            }
        }
    }
}

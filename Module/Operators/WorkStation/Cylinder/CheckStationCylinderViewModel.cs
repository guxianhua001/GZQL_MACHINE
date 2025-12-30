using Framework.Mvvm;
using Prism.Mvvm;
using Prism.Regions;
using Stations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.ViewModels
{
    public class CheckStationCylinderViewModel : RegionViewModelBase, INavigationAware
    {
        private ObservableCollection<CylinderViewModel> _cylinders;
        public ObservableCollection<CylinderViewModel> Cylinders
        {
            get => _cylinders;
            set => SetProperty(ref _cylinders, value);
        }
        public CheckStationCylinderViewModel(IRegionManager regionManager, TaskInstanceManager taskManager) : base(regionManager)
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
                new  { CylName = "1#旋转气缸", SetDo1Id = 32, SetDi1Id = 61, SetDo2Id = 33, SetDi2Id = 62 },
                new  { CylName = "2#旋转气缸", SetDo1Id = 34, SetDi1Id = 63, SetDo2Id = 35, SetDi2Id = 64 },
                new  { CylName = "3#旋转气缸", SetDo1Id = 36, SetDi1Id = 65, SetDo2Id = 37, SetDi2Id = 66 },
                new  { CylName = "4#旋转气缸", SetDo1Id = 38, SetDi1Id = 67, SetDo2Id = 39, SetDi2Id = 68 }
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


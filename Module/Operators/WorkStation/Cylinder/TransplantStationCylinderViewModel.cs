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
    public class TransplantStationCylinderViewModel : RegionViewModelBase, INavigationAware
    {
        private ObservableCollection<CylinderViewModel> _cylinders;
        public ObservableCollection<CylinderViewModel> Cylinders
        {
            get => _cylinders;
            set => SetProperty(ref _cylinders, value);
        }
        public TransplantStationCylinderViewModel(IRegionManager regionManager, TaskInstanceManager taskManager) : base(regionManager)
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
                new  { CylName = "1#伸缩气缸", SetDo1Id = 40, SetDi1Id = 70, SetDo2Id = 41, SetDi2Id = 71 },
                new  { CylName = "1#固定气缸", SetDo1Id = 42, SetDi1Id = 74, SetDo2Id = 43, SetDi2Id = 75 },
                new  { CylName = "2#伸缩气缸", SetDo1Id = 44, SetDi1Id = 77, SetDo2Id = 45, SetDi2Id = 78 },
                new  { CylName = "2#固定气缸", SetDo1Id = 46, SetDi1Id = 81, SetDo2Id = 47, SetDi2Id = 82 },
                new  { CylName = "3#伸缩气缸", SetDo1Id = 48, SetDi1Id = 84, SetDo2Id = 49, SetDi2Id = 85 },
                new  { CylName = "3#固定气缸", SetDo1Id = 50, SetDi1Id = 88, SetDo2Id = 51, SetDi2Id = 89 },
                new  { CylName = "4#伸缩气缸", SetDo1Id = 52, SetDi1Id = 91, SetDo2Id = 53, SetDi2Id = 92 },
                new  { CylName = "4#固定气缸", SetDo1Id = 54, SetDi1Id = 95, SetDo2Id = 55, SetDi2Id = 96 }
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

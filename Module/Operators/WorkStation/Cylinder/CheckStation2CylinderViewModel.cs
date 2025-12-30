using Framework.Mvvm;
using Framework.ViewModels;
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
    public class CheckStation2CylinderViewModel : RegionViewModelBase, INavigationAware
    {
        private ObservableCollection<CylinderViewModel> _cylinders;
        public ObservableCollection<CylinderViewModel> Cylinders
        {
            get => _cylinders;
            set => SetProperty(ref _cylinders, value);
        }
        public CheckStation2CylinderViewModel(IRegionManager regionManager, TaskInstanceManager taskManager) : base(regionManager)
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
                new  { CylName = "2#旋转气缸", SetDo1Id = 35, SetDi1Id = 63, SetDo2Id = 34, SetDi2Id = 64 },
                new  { CylName = "2#伸缩气缸", SetDo1Id = 44, SetDi1Id = 77, SetDo2Id = 45, SetDi2Id = 78 },
                new  { CylName = "2#固定气缸", SetDo1Id = 46, SetDi1Id = 81, SetDo2Id = 47, SetDi2Id = 82 }
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

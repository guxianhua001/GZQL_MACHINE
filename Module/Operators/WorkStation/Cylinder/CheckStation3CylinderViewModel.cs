using Framework.Mvvm;
using Framework.ViewModels;
using Prism.Mvvm;
using Prism.Regions;
using Stations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Framework.ViewModels
{
    public class CheckStation3CylinderViewModel : RegionViewModelBase, INavigationAware
    {
        private ObservableCollection<CylinderViewModel> _cylinders;
        public ObservableCollection<CylinderViewModel> Cylinders
        {
            get => _cylinders;
            set => SetProperty(ref _cylinders, value);
        }
        public CheckStation3CylinderViewModel(IRegionManager regionManager, TaskInstanceManager taskManager) : base(regionManager)
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
               new  { CylName = "3#旋转气缸", SetDo1Id = 37, SetDi1Id = 65, SetDo2Id = 36, SetDi2Id = 66 },
                new  { CylName = "3#伸缩气缸", SetDo1Id = 48, SetDi1Id = 84, SetDo2Id = 49, SetDi2Id = 85 },
                new  { CylName = "3#固定气缸", SetDo1Id = 50, SetDi1Id = 88, SetDo2Id = 51, SetDi2Id = 89 }
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

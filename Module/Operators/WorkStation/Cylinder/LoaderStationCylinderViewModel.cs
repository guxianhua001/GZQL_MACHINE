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
    public class LoaderStationCylinderViewModel : RegionViewModelBase, INavigationAware
    {
        private ObservableCollection<CylinderViewModel> _cylinders;
        public ObservableCollection<CylinderViewModel> Cylinders
        {
            get => _cylinders;
            set => SetProperty(ref _cylinders, value);
        }
        public LoaderStationCylinderViewModel(IRegionManager regionManager, TaskInstanceManager taskManager) : base(regionManager)
        {
            Cylinders = new ObservableCollection<CylinderViewModel>();
            InitializeCylinders();
        }
        public CylinderViewModel Cylinder1 { get; set; }

        ///summary>
        ///初始化气缸 自定义部分
        ///</summary>
        public void Initialize()
        {
            // 初始化 Cylinder1
            Cylinder1 = new CylinderViewModel()
            {
                CylName = "主流线阻挡气缸",
                SetDo1Id = 24,
                SetDi1Id = 25,
                SetDo2Id = 16,
                SetDi2Id = 17
            };
        }
        private void InitializeCylinders()
        {
            Cylinders.Clear();

            // 使用工厂模式创建实例
            var configs = new[]
            {
                new  { CylName = "主流线入口伸缩气缸", SetDo1Id = 33, SetDi1Id = 33, SetDo2Id = 32, SetDi2Id = 34 },
                new  { CylName = "主流线出口伸缩气缸", SetDo1Id = 35, SetDi1Id = 35, SetDo2Id = 34, SetDi2Id = 36 },
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

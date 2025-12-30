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
    /// <summary>
    /// 拨针检测1号模组的气缸视图模型。
    /// </summary>
    public class CheckStation1CylinderViewModel : RegionViewModelBase, INavigationAware
    {
        private ObservableCollection<CylinderViewModel> _cylinders;
        public ObservableCollection<CylinderViewModel> Cylinders
        {
            get => _cylinders;
            set => SetProperty(ref _cylinders, value);
        }
        public CheckStation1CylinderViewModel(IRegionManager regionManager, TaskInstanceManager taskManager) : base(regionManager)
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
                new  { CylName = "1#旋转气缸", SetDo1Id = 33, SetDi1Id = 61, SetDo2Id = 32, SetDi2Id = 62 },
                new  { CylName = "1#伸缩气缸", SetDo1Id = 40, SetDi1Id = 71, SetDo2Id = 41, SetDi2Id = 70 },
                new  { CylName = "1#固定气缸", SetDo1Id = 42, SetDi1Id = 74, SetDo2Id = 43, SetDi2Id = 75 }
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

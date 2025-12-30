
using Framework.Models;
using Prism.Ioc;
using Prism.Regions;
using Framework.Mvvm;

namespace Framework.ViewModels
{
    public class CommonViewModel : RegionViewModelBase
    {
        public ImagePool Pool { get; set; }

        public CommonViewModel(IContainerExtension container, IRegionManager regionManager) : base(regionManager)
        {
            Pool = container.Resolve<ImagePool>();
        }


    }
}
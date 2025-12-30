
using Framework.Mvvm;
using Prism.Ioc;
using Prism.Mvvm;

namespace ModuleCore.ViewModels
{
    public class NavigationManagerViewModel : BindableBase
    {

        public NavigateModel Navigate { get; set; }
        public NavigationManagerViewModel(IContainerExtension container)
        {
            Navigate = container.Resolve<NavigateModel>();
        }


    }
}
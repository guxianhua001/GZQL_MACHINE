// Framework/FrameworkModule.cs
using Core.Abstraction;
using Core.Services;
using Framework.Controls;
using Framework.Dialogs;
using Framework.Services;
using Framework.ViewModels;
using Framework.Views;
using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;

namespace Framework
{
    public class FrameworkModule : IModule
    {
        private readonly IEventAggregator _eventAggregator;

        public FrameworkModule(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }
        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 注册所有相关事件
            //_eventAggregator.GetEvent<SaveParametersCancelledEvent>();
            //_eventAggregator.GetEvent<SaveParametersProgressEvent>(); 
            //_eventAggregator.GetEvent<SaveParametersCompletedEvent>(); 
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 注册核心服务
            containerRegistry.Register<IParameterEditor, ParameterEditorService>(); // 关键注册
            containerRegistry.Register<IParameterStorage, JsonParameterStorage>();

            // 注册参数对话框服务
            containerRegistry.RegisterSingleton<IParameterDialogService, ParameterDialogService>();
            containerRegistry.Register<IFileDialogService, FileDialogService>();
            // 注册自定义对话框窗口
            containerRegistry.RegisterDialog<RecipeSelectionDialog, RecipeSelectionDialogViewModel>("RecipeSelectionDialog");

            // 注册参数服务
            containerRegistry.RegisterSingleton<IParameterService, ParameterService>();
            // 注册视图模型和视图
            containerRegistry.RegisterForNavigation<ParameterEditorView, ParameterEditorViewModel>("ParameterEditor");
            containerRegistry.RegisterForNavigation<BusyIndicatorView, BusyIndicatorViewModel>();
            // 在App.xaml.cs或模块初始化中
            containerRegistry.RegisterSingleton<ITreeConfigService, JsonTreeConfigService>();
            // 注册对话框服务
            containerRegistry.RegisterDialog<CancelableOperationDialog, CancelableOperationDialogViewModel>("CancelableOperationDialog");
            containerRegistry.RegisterSingleton<ICancelableOperationService, CancelableOperationService>();
            containerRegistry.RegisterDialog<MessageDialog, MessageDialogViewModel>(name: "MessageDialog");
            containerRegistry.RegisterDialog<NotificationDialog, NotificationDialogViewModel>(name: "NotificationDialog");

            containerRegistry.RegisterSingleton<Core.Abstraction.IZScanConfigService, Core.Services.ZScanConfigService>();
            containerRegistry.RegisterSingleton<Core.Abstraction.IZScanCalibrationService, Core.Services.ZScanCalibrationService>();
            containerRegistry.RegisterSingleton<Core.Abstraction.IZScanArcCompensationService, Core.Services.ZScanArcCompensationService>();
        }
    }
}



using ModuleCore.Views;
using Framework.Models;
using Framework.ViewModels;
using Framework.Views;
using Prism.Ioc;
using Prism.Modularity;
using Stations;
using System.ComponentModel;
using Interfaces;
using ModuleCore.ViewModels;
using Prism.Mvvm;
using Framework.Services;
using ModuleCore.Services;
using Interfaces.SharedInterfaces;
using Interfaces.Services;
using Core.Abstraction;
using Framework.Mvvm;
using TreeView = Framework.Views.TreeView;
using Module.Views;
using Module.ViewModels;

namespace Module
{
    public class PrimModel : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var Navigate = containerProvider.Resolve<NavigateModel>();
            Navigate.NavigateList.Add(new NavigateItem() { ViewName = "OverView", IconKind = "Github", DisplayName = "首页", UserLevel = 0, Display = true });
            Navigate.NavigateList.Add(new NavigateItem() { ViewName = "TreeView", IconKind = "OrderBoolAscending", DisplayName = "操作页面", UserLevel = 0, Display = true });
            //Navigate.NavigateList.Add(new NavigateItem() { ViewName = "ShellView", IconKind = "RobotIndustrialOutline", DisplayName = "龙门同步", UserLevel = 0, Display = true });
            //Navigate.NavigateList.Add(new NavigateItem() { ViewName = "RaySourceDebugView", IconKind = "CameraSwitchOutline", DisplayName = "X-Ray", UserLevel = 0, Display = true });
            Navigate.NavigateList.Add(new NavigateItem() { ViewName = "AlarmReportingView", IconKind = "AlarmLightOutline", DisplayName = "报警查询", UserLevel = 0, Display = true });
            Navigate.NavigateList.Add(new NavigateItem() { ViewName = "IODisplayView", IconKind = "TuneVariant", DisplayName = "IO视图", UserLevel = 0, Display = true });
            Navigate.NavigateList.Add(new NavigateItem() { ViewName = "RecipeManagerView", IconKind = "Fingerprint", DisplayName = "配方管理", UserLevel = 0, Display = true });
            Navigate.DefaultView = "OverView";//OverView
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // ============ 非导航视图（直接控件） ============
            containerRegistry.Register<AxisView>();
            containerRegistry.Register<PositionView>();
            containerRegistry.Register<SensorView>();
            containerRegistry.Register<CylinderView>();

            // ============ 主内容区域导航视图 ============
            containerRegistry.RegisterDialog<LogViewer>();
            containerRegistry.RegisterDialog<AddPositionDialog>();
            containerRegistry.RegisterForNavigation<TreeView>();
            containerRegistry.RegisterForNavigation<ShellView>();
            containerRegistry.RegisterForNavigation<IODisplayView>();
            containerRegistry.RegisterForNavigation<StationStateView, StationStateViewModel>();
            containerRegistry.RegisterForNavigation<SpeedRatioView>();
            //containerRegistry.RegisterForNavigation<RecipeManagerView>();
            containerRegistry.RegisterForNavigation<OverView, Framework.ViewModels.OverViewModel>();
            containerRegistry.RegisterForNavigation<OperationView, Framework.ViewModels.OperationViewModel>();
            containerRegistry.RegisterForNavigation<DataEditorView, Framework.ViewModels.DataEditorViewModel>();

            // ============ 树形菜单区域导航视图 ============
            containerRegistry.RegisterForNavigation<LoaderStationAxesView,Framework.ViewModels.LoaderStationAxesViewModel>();
            containerRegistry.RegisterForNavigation<DispenserStationAxesView, Framework.ViewModels.DispenserStationAxesViewModel>();
            containerRegistry.RegisterForNavigation<AssemblyStationAxesView, Framework.ViewModels.AssemblyStationAxesViewModel>();
            //工站气缸视图
            containerRegistry.RegisterForNavigation<LoaderStationCylinderView, Framework.ViewModels.LoaderStationCylinderViewModel>();
            containerRegistry.RegisterForNavigation<GantryStationCylinderView, Framework.ViewModels.GantryStationCylinderViewModel>();
            //工站位置视图
            containerRegistry.RegisterForNavigation<LoaderStationPositionView, Framework.ViewModels.LoaderStationPositionViewModel>();
            containerRegistry.RegisterForNavigation<DispenserStationPositionView, Framework.ViewModels.DispenserStationPositionViewModel>();
            containerRegistry.RegisterForNavigation<AssemblyStationPositionView, Framework.ViewModels.AssemblyStationPositionViewModel>();
            //工站视图
            containerRegistry.RegisterForNavigation<LoaderStationView, LoaderStationViewModel>();
            containerRegistry.RegisterForNavigation<DispenserStationView, DispenserStationViewModel>();
            containerRegistry.RegisterForNavigation<AssemblyStationView, AssemblyStationViewModel>();
            //外部设备
            containerRegistry.RegisterSingleton<IDeviceService, LctDeviceService>();
            containerRegistry.RegisterSingleton<IDataAcquisitionService, DataAcquisitionService>();
            //Chat 
            containerRegistry.Register<ICsvParserService, CsvService>();
            containerRegistry.RegisterForNavigation<HistoricalTrendView>();
            containerRegistry.RegisterForNavigation<ForceChartView, ForceChartViewModel>();

            containerRegistry.RegisterForNavigation<DialRecordsTrendView, DialRecordsTrendViewModel>();
            containerRegistry.RegisterForNavigation<NGMonitorView, NGMonitorViewModel>();
            containerRegistry.RegisterForNavigation<DebugThresholdView, DebugThresholdViewModel>();
            containerRegistry.RegisterForNavigation<AlarmReportingView, AlarmReportingViewModel>();
            //注册接口服务
            containerRegistry.Register<IPinMapService, PinMapService>();
            containerRegistry.Register<IPostDialPointMapService, PostDialPointMapService>();
            containerRegistry.RegisterForNavigation<PointTeachingView, PointTeachingViewModel>();
            containerRegistry.RegisterForNavigation<NeedleCalibrationView, NeedleCalibrationViewModel>();
            containerRegistry.RegisterForNavigation<AssemblyStepControlView, AssemblyStepControlViewModel>();
            containerRegistry.RegisterForNavigation<ExtensionParametersView, ExtensionParametersViewModel>();
            containerRegistry.RegisterForNavigation<SlotControlView, SlotControlViewModel>();
        }

    }
}
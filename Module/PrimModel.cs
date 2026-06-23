
using Core.Abstraction;
using Core.Services;
using Framework.Mvvm;
using Framework.Views;
using MaterialDesignThemes.Wpf;
using Module.Services;
using Module.UserControls.Grippers;
using Module.ViewModels;
using Module.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Mvvm;
using Prism.Regions;
using TreeView = Framework.Views.TreeView;

namespace Module
{
    public class PrimModel : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var localizationService = containerProvider.Resolve<ILocalizationService>();
            var Navigate = containerProvider.Resolve<NavigateModel>();

#if HAS_HALCON
            Core.Models.CadEntityHalconExtensions.DxfParserService =
                containerProvider.Resolve<Core.Services.IDxfParserService>();
#endif
            // 1. 首页/总览
            Navigate.NavigateList.Add(new NavigateItem() { ViewName = "OverView", IconKind = "HomeMinusOutline", DisplayName = localizationService.GetResourceOrDefault("Nav_Home", "首页"), DisplayNameKey = "Nav_Home", UserLevel = 0, Display = true });
            // 2. 操作页面
            Navigate.NavigateList.Add(new NavigateItem() { ViewName = "TreeView", IconKind = "FileTreeOutline", DisplayName = localizationService.GetResourceOrDefault("Nav_Operation", "操作页面"), DisplayNameKey = "Nav_Operation", UserLevel = 0, Display = true });
            // 3. 实时报警（当前活跃报警、未确认计数、批量确认/复位）
            Navigate.NavigateList.Add(new NavigateItem() { ViewName = "AlarmListView", IconKind = "AlarmLightOutline", DisplayName = localizationService.GetResourceOrDefault("Nav_AlarmRealtime", "实时报警"), DisplayNameKey = "Nav_AlarmRealtime", UserLevel = 0, Display = true });
            // 4. 报警历史查询（多条件过滤、分页、Excel导出）
            Navigate.NavigateList.Add(new NavigateItem() { ViewName = "AlarmHistoryView", IconKind = "DatabaseOutline", DisplayName = localizationService.GetResourceOrDefault("Nav_AlarmQuery", "报警查询"), DisplayNameKey = "Nav_AlarmQuery", UserLevel = 0, Display = true });
            // 5. 报警统计（等级分布、频率排名、趋势分析）
            Navigate.NavigateList.Add(new NavigateItem() { ViewName = "AlarmStatsView", IconKind = "ChartBar", DisplayName = localizationService.GetResourceOrDefault("Nav_AlarmStats", "报警统计"), DisplayNameKey = "Nav_AlarmStats", UserLevel = 0, Display = true });
            // 6. 报警阈值配置
            Navigate.NavigateList.Add(new NavigateItem() { ViewName = "AlarmThresholdView", IconKind = "CogOutline", DisplayName = localizationService.GetResourceOrDefault("Nav_AlarmThreshold", "报警阈值"), DisplayNameKey = "Nav_AlarmThreshold", UserLevel = 1, Display = true });
            // 7. IO视图 
            Navigate.NavigateList.Add(new NavigateItem() { ViewName = "IODisplayView", IconKind = "SwapHorizontal", DisplayName = localizationService.GetResourceOrDefault("Nav_IOView", "IO视图"), DisplayNameKey = "Nav_IOView", UserLevel = 0, Display = true });
            // 8. 配方管理
            Navigate.NavigateList.Add(new NavigateItem() { ViewName = "RecipeManagerView", IconKind = "NoteEditOutline", DisplayName = localizationService.GetResourceOrDefault("Nav_RecipeManager", "配方管理"), DisplayNameKey = "Nav_RecipeManager", UserLevel = 0, Display = true });
            // 9. TCPIP设置
            Navigate.NavigateList.Add(new NavigateItem() { ViewName = "TcpConfigView", IconKind = "LanPending", DisplayName = localizationService.GetResourceOrDefault("Nav_TcpConfig", "TCPIP设置"), DisplayNameKey = "Nav_TcpConfig", UserLevel = 0, Display = true });
            // 10. N点标定
            Navigate.NavigateList.Add(new NavigateItem() { ViewName = "NPointCalibrationView", IconKind = "VectorIntersection", DisplayName = localizationService.GetResourceOrDefault("Nav_NPointCalibration", "N点标定"), DisplayNameKey = "Nav_NPointCalibration", UserLevel = 0, Display = true });
            // 11. 设备维护
            //Navigate.NavigateList.Add(new NavigateItem() { ViewName = "MaintenanceView", IconKind = "WrenchOutline", DisplayName = localizationService.GetResourceOrDefault("Nav_Maintenance", "设备维护"), DisplayNameKey = "Nav_Maintenance", UserLevel = 1, Display = true });
            Navigate.DefaultView = "OverView";

            var regionManager = containerProvider.Resolve<IRegionManager>();
            regionManager.RegisterViewWithRegion("SafetyZoneConfigRegion", typeof(SafetyZoneConfigView));
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // ============ 非导航视图（直接控件） ============

            // ============ 主内容区域导航视图 ============
            containerRegistry.RegisterDialog<AddPositionDialog>();
            containerRegistry.RegisterForNavigation<TreeView>();
            containerRegistry.RegisterForNavigation<OverView, OverViewModel>();

            // ============ 树形菜单区域导航视图 ============
            containerRegistry.RegisterForNavigation<DispensingView, DispensingViewModel>();
            containerRegistry.RegisterForNavigation<LoadUnloadView, LoadUnloadViewModel>();
            containerRegistry.RegisterForNavigation<AssemblyStepView, AssemblyStepViewModel>();
            containerRegistry.RegisterForNavigation<ProcessSequenceEditorView, ProcessSequenceEditorViewModel>();
            containerRegistry.Register<GotoDetailViewModel>();
            containerRegistry.RegisterForNavigation<AddEditStepDialogView, AddEditStepDialogViewModel>();
            containerRegistry.RegisterForNavigation<CadPointEditorView, CadPointEditorViewModel>();
            containerRegistry.RegisterForNavigation<CadPointEditor3DView, CadPointEditor3DViewModel>();
            containerRegistry.RegisterForNavigation<DotPointEditorView, DotPointEditorViewModel>();
            containerRegistry.RegisterForNavigation<InspectionView, InspectionViewModel>();
            containerRegistry.RegisterForNavigation<VisionCaptureView, VisionCaptureViewModel>();
            containerRegistry.RegisterForNavigation<ZScanDetailView, ZScanDetailViewModel>();

            containerRegistry.Register<Core.Abstraction.IZScanGlobalVariableLinkService, Module.Services.ZScanGlobalVariableLinkService>();
            containerRegistry.Register<Core.Abstraction.INeedleTeachService, Module.Services.NeedleTeachService>();
            // CAD 对齐坐标变换共享服务（单例）——CadAlignment 发布，Dispense 订阅
            containerRegistry.RegisterSingleton<Core.Abstraction.ICadAlignTransformService, Module.Services.CadAlignTransformService>();
            containerRegistry.RegisterSingleton<Core.Abstraction.IStageCalibrationService, Module.Services.StageCalibrationService>();
            containerRegistry.RegisterSingleton<Core.Abstraction.INeedleService, Module.Services.NeedleService>();
            containerRegistry.RegisterSingleton<Core.Services.NeedleCompensationManager>();

            containerRegistry.RegisterSingleton<IAxisConfigurationService, MotionControl.Services.AxisConfigurationService>();
            containerRegistry.RegisterSingleton<Services.ILoadUnloadController, Services.LoadUnloadControllerImpl>();
            containerRegistry.RegisterForNavigation<WorkOrderConfigView, WorkOrderConfigViewModel>();
            containerRegistry.RegisterDialog<FeatureEditorDialog, FeatureEditorDialogViewModel>();
            containerRegistry.RegisterDialog<AxisEditorDialog, AxisEditorDialogViewModel>();
            containerRegistry.RegisterForNavigation<ProductCalibrationView, ProductCalibrationViewModel>();
            containerRegistry.RegisterDialog<GroupEditorDialog, GroupEditorDialogViewModel>();
            containerRegistry.RegisterForNavigation<CheckDetailView, CheckDetailViewModel>();
            containerRegistry.RegisterForNavigation<CadAlignmentView, CadAlignmentViewModel>();
            containerRegistry.RegisterForNavigation<PickDetailView, PickDetailViewModel>();
            containerRegistry.RegisterForNavigation<ReleaseDetailView, ReleaseDetailViewModel>();
            containerRegistry.RegisterForNavigation<CureDetailView, CureDetailViewModel>();
            // GripperControlView 改用 BaseDialogService 弹出，无需 RegisterDialog
            // GripperControlViewModel 通过 IContainerProvider.Resolve 动态解析（瞬态）
            containerRegistry.RegisterDialog<CoordinateCalibrationDialog, CoordinateCalibrationDialogViewModel>();
            containerRegistry.RegisterDialog<SimpleInputDialog, SimpleInputDialogViewModel>();
            containerRegistry.RegisterForNavigation<IPQCView, IPQCViewModel>();
            containerRegistry.RegisterForNavigation<ScanDetailView, ScanDetailViewModel>();
            containerRegistry.RegisterForNavigation<Camera2DView, Camera2DViewModel>();
            containerRegistry.Register<VisionDetailViewModel>();
            containerRegistry.Register<DataDashboardViewModel>();
            containerRegistry.Register<ConditionBranchViewModel>();
            containerRegistry.Register<IfDetailViewModel>();
            containerRegistry.Register<SeekDetailViewModel>();
            containerRegistry.Register<WaitDetailViewModel>();
            containerRegistry.Register<ScriptDetailViewModel>();
            containerRegistry.Register<RunTaskDetailViewModel>();
            // 旋转后坐标查看弹窗 ViewModel（供 DispenseDetailViewModel 通过 IContainerProvider 解析）
            containerRegistry.Register<DispenseRotatedCoordsViewModel>();
            // 信号交互步骤详情 ViewModel 注册
            containerRegistry.Register<SignalSendDetailViewModel>();
            containerRegistry.Register<SignalWaitDetailViewModel>();

            // === Core 服务（跨项目复用，Singleton）===
            containerRegistry.RegisterSingleton<Core.Abstraction.IFormulaEvaluator, Core.Services.FormulaEvaluator>();
            containerRegistry.RegisterSingleton<Core.Services.IDxfParserService, Core.Services.DxfParserService>();
            containerRegistry.RegisterSingleton<Core.Services.IRoiToolService, Core.Services.RoiToolService>();
#if HAS_HALCON
            containerRegistry.RegisterSingleton<Core.Services.ICoordinateAlignService, Core.Services.CoordinateAlignService>();
#else
            // Halcon SDK 未安装时，CoordinateAlignService 被条件编译排除，注册空实现占位
            containerRegistry.RegisterSingleton<Core.Services.ICoordinateAlignService, Core.Services.StubCoordinateAlignService>();
#endif

            // DXF 统一导入服务（保证 CadPointEditorViewModel 和 CadAlignmentViewModel 使用相同导入逻辑）
            containerRegistry.RegisterSingleton<Core.Services.IDxfImportHelper, Core.Services.DxfImportHelper>();

            // === Module 服务（项目特有）===
            containerRegistry.Register<Module.Services.IDispenseExecuteService, Module.Services.DispenseExecuteService>();
            containerRegistry.Register<Module.Services.IDotDispenseService, Module.Services.DotDispenseService>();
            containerRegistry.Register<Module.Services.INeedleAlignerMotionService, Module.Services.NeedleAlignerMotionService>();

            // 看板弹窗服务：订阅 ShowDashboardEvent 并显示 DialogHost
            containerRegistry.RegisterSingleton<Module.Services.DashboardDialogService>();

            containerRegistry.RegisterForNavigation<AlignDetailView, AlignDetailViewModel>();
            containerRegistry.RegisterSingleton<IProcessSequenceService, ProcessSequenceService>();

            containerRegistry.RegisterForNavigation<MaintenanceView, MaintenanceViewModel>();
            containerRegistry.RegisterForNavigation<NeedleCameraAlignmentView, NeedleCameraAlignmentViewModel>();
            containerRegistry.RegisterForNavigation<NeedleAlignerView, NeedleAlignerViewModel>();
            containerRegistry.RegisterForNavigation<NeedleCalibrationVerifyView, NeedleCalibrationVerifyViewModel>();
            containerRegistry.RegisterForNavigation<SafetyZoneConfigView, SafetyZoneConfigViewModel>();

            // === N点标定 ===
            containerRegistry.RegisterSingleton<Core.Abstraction.INPointCalibrationService, Module.Services.NPointCalibrationService>();
            containerRegistry.RegisterForNavigation<NPointCalibrationView, NPointCalibrationViewModel>();

            // === 双龙门标定 ===
            containerRegistry.RegisterSingleton<Core.Abstraction.IDualGantryCalibrationService, Module.Services.DualGantryCalibrationService>();
            containerRegistry.RegisterForNavigation<DualGantryCalibrationView, DualGantryCalibrationViewModel>();
        }
    }
}
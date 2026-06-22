using Core.Abstraction;
using Core.Extensions;
using Core.Models;
using Core.Services;
using Module.Services;
using MotionControl.Interfaces;
using MotionControl.Services;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using Recipe.Events;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Newtonsoft.Json;

namespace Module.ViewModels
{
    /// <summary>
    /// CAD对位操作步骤信息模型——描述每个步骤的显示内容和状态
    /// </summary>
    public class AlignmentStepInfo : BindableBase
    {
        private int _number;
        public int Number { get => _number; set => SetProperty(ref _number, value); }

        private string _title = string.Empty;
        public string Title { get => _title; set => SetProperty(ref _title, value); }

        private string _hint = string.Empty;
        public string Hint { get => _hint; set => SetProperty(ref _hint, value); }

        private bool _isCompleted;
        public bool IsCompleted { get => _isCompleted; set => SetProperty(ref _isCompleted, value); }

        private bool _isCurrent;
        public bool IsCurrent { get => _isCurrent; set => SetProperty(ref _isCurrent, value); }

        public bool ShowConnector => Number < 5;
        public string ConnectorColor => IsCompleted ? "#4CAF50" : "#BDBDBD";
    }

    /// <summary>
    /// 四点拟合点位模型——用于步骤1回转中心拟合的四个角度采样点
    /// </summary>
    public class FitPoint : BindableBase
    {
        public int Index { get; set; } // 行索引，用于示教命令参数
        public string AngleLabel { get; set; } = "";

        private double _fitX;
        public double FitX { get => _fitX; set => SetProperty(ref _fitX, value); }

        private double _fitY;
        public double FitY { get => _fitY; set => SetProperty(ref _fitY, value); }
    }

    /// <summary>
    /// 仿射标定点模型已移至 Core.Models.AffineCalibrationPoint
    /// </summary>
    // AffineCalibrationPoint 使用 Core.Models 版本

    /// <summary>
    /// 回转中心可视化点位——用于Step1画布显示拟合点与回转中心位置
    /// </summary>
    public class VisualFitPoint : BindableBase
    {
        /// <summary>数据X坐标（机械坐标）</summary>
        public double DataX { get; set; }
        /// <summary>数据Y坐标（机械坐标）</summary>
        public double DataY { get; set; }
        /// <summary>显示标签（角度名称）</summary>
        public string Label { get; set; } = "";

        private double _screenX;
        /// <summary>画布屏幕X坐标</summary>
        public double ScreenX { get => _screenX; set => SetProperty(ref _screenX, value); }

        private double _screenY;
        /// <summary>画布屏幕Y坐标</summary>
        public double ScreenY { get => _screenY; set => SetProperty(ref _screenY, value); }
    }

    /// <summary>
    /// 5步CAD对位标准流程ViewModel：
    /// ① 回转中心（四点圆拟合）→ ② 全局偏移（ΔX/ΔY）→ ③ 旋转角度（CAD向量方向角）
    /// → ④ 坐标变换（先平移后旋转）→ ⑤ 夹爪定位（最终组装位置）
    /// </summary>
    public class CadAlignmentViewModel : BindableBase
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly IContainerProvider _containerProvider;
        private readonly IEventAggregator _eventAggregator;

        public CadAlignmentViewModel(IRecipePoolService recipePoolService, IContainerProvider containerProvider, IEventAggregator eventAggregator)
        {
            _recipePoolService = recipePoolService;
            _containerProvider = containerProvider;
            _eventAggregator = eventAggregator;

            // 订阅全局变量变更事件，其他模块写入 GV 时同步刷新本地下拉列表
            _eventAggregator.GetEvent<GlobalVariablesChangedEvent>()
                .Subscribe(OnGlobalVariablesChanged, ThreadOption.UIThread);

            // 初始化统一导入服务
            try
            {
                _dxfImportHelper = containerProvider.Resolve<IDxfImportHelper>();
            }
            catch
            {
                _dxfImportHelper = null;
            }

            CorrespondencePoints = new ObservableCollection<CorrespondencePoint>
            {
                new() { Name = "P1", CadX = 100.0, CadY = 200.0, CadZ = 50.0, ActualX = 70.32, ActualY = 213.26, ActualZ = 0 },
                new() { Name = "P2", CadX = 150.0, CadY = 250.0, CadZ = 55.0, ActualX = 100.20, ActualY = 277.28, ActualZ = 0 },
                new() { Name = "P3", CadX = 120.0, CadY = 180.0, CadZ = 52.0, ActualX = 95.95, ActualY = 201.28, ActualZ = 0 },
                new() { Name = "P4", CadX = 130.0, CadY = 220.0, CadZ = 53.0, ActualX = 91.67, ActualY = 242.28, ActualZ = 0 },
                new() { Name = "P5", CadX = 140.0, CadY = 210.0, CadZ = 54.0, ActualX = 104.47, ActualY = 236.30, ActualZ = 0 },
                new() { Name = "P6", CadX = 110.0, CadY = 190.0, CadZ = 51.0, ActualX = 83.14, ActualY = 207.26, ActualZ = 0 },
            };

            FitPoints = new ObservableCollection<FitPoint>
            {
                new() { Index = 0, AngleLabel = "0°",   FitX = 70.32, FitY = 213.26 },
                new() { Index = 1, AngleLabel = "90°",  FitX = 100.2, FitY = 277.28 },
                new() { Index = 2, AngleLabel = "180°", FitX = 95.95, FitY = 201.28 },
                new() { Index = 3, AngleLabel = "270°", FitX = 91.67, FitY = 242.28 },
            };

            // 初始化仿射标定点集合（默认3个示例点，用户在实际流程中重新示教）
            AffineCalibrationPoints = new ObservableCollection<AffineCalibrationPoint>
            {
                new() { Index = 0, Name = "P1", CadX = 70.92, CadY = 62.42, MachineX = -42.58, MachineY = 80.68 },
                new() { Index = 1, Name = "P2", CadX = 93.41, CadY = 62.38, MachineX = 0, MachineY = 0 },
                new() { Index = 2, Name = "P3", CadX = 82.0,  CadY = 40.0,  MachineX = 0, MachineY = 0 },
            };

            if (CorrespondencePoints.Count > 0)
            {
                P1Cx = CorrespondencePoints[0].CadX;
                P1Cy = CorrespondencePoints[0].CadY;
                P1Mx = CorrespondencePoints[0].ActualX;
                P1My = CorrespondencePoints[0].ActualY;
            }

            FitRotationCenterCommand = new DelegateCommand(OnFitRotationCenter);
            ComputeGlobalOffsetCommand = new DelegateCommand(OnComputeGlobalOffset);
            ComputeCadRotationAngleCommand = new DelegateCommand(OnComputeCadRotationAngle);
            ExecuteTransformCommand = new DelegateCommand(OnExecuteTransform);
            ExecuteBatchTransformCommand = new DelegateCommand(OnExecuteBatchTransform);
            ComputeGripperPositionCommand = new DelegateCommand(OnComputeGripperPosition);
            ShowPrincipleCommand = new DelegateCommand(OnShowPrinciple);
            ExportDxfCommand = new DelegateCommand(OnExportDxf);
            AddCadPointCommand = new DelegateCommand(AddCadPoint);
            DeleteCadPointCommand = new DelegateCommand<CorrespondencePoint>(DeleteCadPoint);
            TeachFitPointCommand = new DelegateCommand<object>(OnTeachFitPointWrapper);
            MoveFitPointCommand = new DelegateCommand<FitPoint>(async fp => await OnMoveFitPointAsync(fp));
            TeachGripperPositionCommand = new DelegateCommand(OnTeachGripperPosition);
            ApplyCalcOffsetCommand = new DelegateCommand(OnApplyCalcOffset, () => TransResultX != 0 && TransResultY != 0);
            InheritTargetFromStep3Command = new DelegateCommand(
                OnInheritTargetFromStep3,
                () => CanInheritFromStep3);
            PickBaselineFromCadCommand = new DelegateCommand(OnPickBaselineFromCad);
            PickTargetFromCadCommand = new DelegateCommand(OnPickTargetFromCad);
            ImportDxfCommand = new DelegateCommand(OnImportDxf);
            AutoRecommendLinesCommand = new DelegateCommand(OnAutoRecommendLines, () => ImportedCadPoints.Count >= 4);
            ShowBaselineSegmentCommand = new DelegateCommand(OnShowBaselineSegment, () => HasBaselineSelected);
            ShowTargetlineSegmentCommand = new DelegateCommand(OnShowTargetlineSegment, () => HasTargetlineSelected);
            WriteToGlobalVariablesCommand = new DelegateCommand(OnWriteToGlobalVariables, () => Step5Done);
            AddAffineCalibrationPointCommand = new DelegateCommand(OnAddAffineCalibrationPoint);
            DeleteAffineCalibrationPointCommand = new DelegateCommand<AffineCalibrationPoint>(OnDeleteAffineCalibrationPoint);
            PickAffineCadCoordCommand = new DelegateCommand<AffineCalibrationPoint>(OnPickAffineCadCoord);
            TeachAffineMachineCoordCommand = new DelegateCommand<object>(OnTeachAffineMachineCoord);
            MoveAffineCalibrationPointCommand = new DelegateCommand<AffineCalibrationPoint>(async pt => await OnMoveAffineCalibrationPointAsync(pt));
            SaveConfigCommand = new DelegateCommand(async () => await SaveConfigToFileAsync());
            LoadConfigCommand = new DelegateCommand(async () => await LoadConfigFromFileAsync());
            UnlinkGripperXCommand = new DelegateCommand(() => { IsGripperXLinked = false; FinalGripperXLinkedVar = ""; });
            UnlinkGripperYCommand = new DelegateCommand(() => { IsGripperYLinked = false; FinalGripperYLinkedVar = ""; });
            UnlinkGripperZCommand = new DelegateCommand(() => { IsGripperZLinked = false; FinalGripperZLinkedVar = ""; });
            UnlinkAlignmentAngleCommand = new DelegateCommand(() => { IsAlignmentAngleLinked = false; AlignmentAngleLinkedVar = ""; });
            MoveTargetAngleCommand = new DelegateCommand(async () => await OnMoveTargetAngleAsync());
            LinkAlignmentAngleCommand = new DelegateCommand(OnLinkAlignmentAngle);
            MoveTargetPositionCommand = new DelegateCommand(async () => await OnMoveTargetPositionAsync());
            SetCameraRefCommand = new DelegateCommand(OnSetCameraRef);
            TeachGripperRefCommand = new DelegateCommand(OnTeachGripperRef);
            CalcCameraOffsetCommand = new DelegateCommand(OnCalcCameraOffset);
            CalcGripperFinalCommand = new DelegateCommand(OnCalcGripperFinal);

            Steps = InitializeSteps();
            _currentStep = 1;
            UpdateStepStates(_currentStep);
            GoNextCommand = new DelegateCommand(GoNext, CanGoNext);
            GoPrevCommand = new DelegateCommand(GoPrev, CanGoPrev);

            RefreshPointPairNames();
            _ = LoadAvailableGlobalVariablesAsync();
            _ = TryAutoLoadConfigAsync();
        }

        #region 对应点集合

        private ObservableCollection<CorrespondencePoint> _correspondencePoints;
        public ObservableCollection<CorrespondencePoint> CorrespondencePoints
        {
            get => _correspondencePoints;
            set => SetProperty(ref _correspondencePoints, value);
        }

        #endregion

        #region 步骤导航

        private int _currentStep;
        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                if (SetProperty(ref _currentStep, value))
                {
                    UpdateStepStates(value);
                    RaisePropertyChanged(nameof(CurrentStepTitle));
                    (GoNextCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    (GoPrevCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 当前步骤标题（多语言支持）
        /// </summary>
        public string CurrentStepTitle
        {
            get
            {
                var lang = _containerProvider.Resolve<ILocalizationService>();
                return _currentStep switch
                {
                    1 => lang.GetResource("CadAlignment_Step1_Title"),
                    2 => lang.GetResource("CadAlignment_Step2_Title"),
                    3 => lang.GetResource("CadAlignment_Step3_Title"),
                    4 => lang.GetResource("CadAlignment_Step4_Title"),
                    5 => lang.GetResource("CadAlignment_Step5_Title"),
                    _ => $"{lang.GetResource("CadAlignment_StepLabel")} {_currentStep}"
                };
            }
        }

        /// <summary>
        /// 获取多语言文本（便捷方法）
        /// </summary>
        private string L(string key) => _containerProvider.Resolve<ILocalizationService>().GetResource(key);

        public ObservableCollection<AlignmentStepInfo> Steps { get; private set; }

        public ICommand GoNextCommand { get; }
        public ICommand GoPrevCommand { get; }

        #endregion

        #region 步骤1 — 四点拟合（回转中心）

        private ObservableCollection<FitPoint> _fitPoints;
        public ObservableCollection<FitPoint> FitPoints
        {
            get => _fitPoints;
            set => SetProperty(ref _fitPoints, value);
        }

        private double _mox;
        public double Mox { get => _mox; set => SetProperty(ref _mox, value); }

        private double _moy;
        public double Moy { get => _moy; set => SetProperty(ref _moy, value); }

        private double _fitRadius;
        public double FitRadius { get => _fitRadius; set => SetProperty(ref _fitRadius, value); }

        private bool _step1Done;
        public bool Step1Done { get => _step1Done; set => SetProperty(ref _step1Done, value); }

        // ——— 回转中心可视化属性 ———

        /// <summary>拟合点在画布上的屏幕坐标集合（绑定到 ItemsControl）</summary>
        public ObservableCollection<VisualFitPoint> VisualFitPoints { get; } = new ObservableCollection<VisualFitPoint>();

        private double _centerScreenX;
        /// <summary>回转中心在画布上的X坐标</summary>
        public double CenterScreenX { get => _centerScreenX; set => SetProperty(ref _centerScreenX, value); }

        private double _centerScreenY;
        /// <summary>回转中心在画布上的Y坐标</summary>
        public double CenterScreenY { get => _centerScreenY; set => SetProperty(ref _centerScreenY, value); }

        private double _circleScreenRadius;
        /// <summary>拟合圆在画布上的屏幕半径</summary>
        public double CircleScreenRadius { get => _circleScreenRadius; set => SetProperty(ref _circleScreenRadius, value); }

        private double _circleCanvasLeft;
        /// <summary>拟合圆在画布上的左边缘X</summary>
        public double CircleCanvasLeft { get => _circleCanvasLeft; set => SetProperty(ref _circleCanvasLeft, value); }

        private double _circleCanvasTop;
        /// <summary>拟合圆在画布上的上边缘Y</summary>
        public double CircleCanvasTop { get => _circleCanvasTop; set => SetProperty(ref _circleCanvasTop, value); }

        private double _circleDiameter;
        /// <summary>拟合圆在画布上的直径（像素）</summary>
        public double CircleDiameter { get => _circleDiameter; set => SetProperty(ref _circleDiameter, value); }

        /// <summary>画布可视区域是否有数据</summary>
        public bool HasRotationCenterVisual => Step1Done && FitPoints != null && FitPoints.Count >= 3;

        /// <summary>
        /// 根据画布尺寸重新计算拟合点和回转中心的屏幕坐标
        /// 坐标映射: 数据空间 → 画布像素（等比例缩放、Y轴翻转、居中布局）
        /// </summary>
        public void UpdateRotationCenterVisual(double canvasWidth, double canvasHeight)
        {
            if (canvasWidth < 10 || canvasHeight < 10 || FitPoints == null || FitPoints.Count < 1)
                return;

            const double padding = 30; // 画布边距像素
            double w = canvasWidth - 2 * padding;
            double h = canvasHeight - 2 * padding;
            if (w < 10 || h < 10) return;

            // 计算数据范围
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            foreach (var fp in FitPoints)
            {
                if (fp.FitX < minX) minX = fp.FitX;
                if (fp.FitX > maxX) maxX = fp.FitX;
                if (fp.FitY < minY) minY = fp.FitY;
                if (fp.FitY > maxY) maxY = fp.FitY;
            }
            // 包含回转中心
            if (Step1Done)
            {
                if (Mox < minX) minX = Mox;
                if (Mox > maxX) maxX = Mox;
                if (Moy < minY) minY = Moy;
                if (Moy > maxY) maxY = Moy;
                // 包含拟合圆边界
                if (Mox - FitRadius < minX) minX = Mox - FitRadius;
                if (Mox + FitRadius > maxX) maxX = Mox + FitRadius;
                if (Moy - FitRadius < minY) minY = Moy - FitRadius;
                if (Moy + FitRadius > maxY) maxY = Moy + FitRadius;
            }

            double rangeX = maxX - minX;
            double rangeY = maxY - minY;
            if (rangeX < 0.1) rangeX = 1;
            if (rangeY < 0.1) rangeY = 1;

            // 等比例缩放
            double scaleX = w / rangeX;
            double scaleY = h / rangeY;
            double scale = Math.Min(scaleX, scaleY);

            // 居中偏移（Y轴翻转）
            double offsetX = padding + (w - rangeX * scale) / 2;
            double offsetY = padding + (h - rangeY * scale) / 2;

            // 更新拟合点屏幕坐标
            VisualFitPoints.Clear();
            foreach (var fp in FitPoints)
            {
                double sx = offsetX + (fp.FitX - minX) * scale;
                double sy = canvasHeight - (offsetY + (fp.FitY - minY) * scale); // Y翻转
                VisualFitPoints.Add(new VisualFitPoint
                {
                    DataX = fp.FitX,
                    DataY = fp.FitY,
                    Label = fp.AngleLabel,
                    ScreenX = sx,
                    ScreenY = sy
                });
            }

            // 更新回转中心和拟合圆
            if (Step1Done)
            {
                double cx = offsetX + (Mox - minX) * scale;
                double cy = canvasHeight - (offsetY + (Moy - minY) * scale);
                CenterScreenX = cx;
                CenterScreenY = cy;
                double r = FitRadius * scale;
                CircleScreenRadius = r;
                CircleCanvasLeft = cx - r;
                CircleCanvasTop = cy - r;
                CircleDiameter = 2 * r;
            }

            RaisePropertyChanged(nameof(HasRotationCenterVisual));
        }

        #endregion

        #region 步骤2 — 全局偏移（支持1点平移和N点仿射两种模式）

        private double _p1Mx;
        public double P1Mx { get => _p1Mx; set => SetProperty(ref _p1Mx, value); }

        private double _p1My;
        public double P1My { get => _p1My; set => SetProperty(ref _p1My, value); }

        private double _p1Cx;
        public double P1Cx { get => _p1Cx; set => SetProperty(ref _p1Cx, value); }

        private double _p1Cy;
        public double P1Cy { get => _p1Cy; set => SetProperty(ref _p1Cy, value); }

        private double _deltaX;
        public double DeltaX { get => _deltaX; set => SetProperty(ref _deltaX, value); }

        private double _deltaY;
        public double DeltaY { get => _deltaY; set => SetProperty(ref _deltaY, value); }

        private bool _step2Done;
        public bool Step2Done { get => _step2Done; set => SetProperty(ref _step2Done, value); }

        // ── N点仿射标定模式 ──

        /// <summary>是否启用N点仿射标定（false=1点平移默认模式，true=N点仿射模式）</summary>
        private bool _useAffineCalibration;
        public bool UseAffineCalibration
        {
            get => _useAffineCalibration;
            set => SetProperty(ref _useAffineCalibration, value);
        }

        /// <summary>仿射标定点集合（用户示教的CAD-机械对应点，至少3个）</summary>
        private ObservableCollection<AffineCalibrationPoint> _affineCalibrationPoints;
        public ObservableCollection<AffineCalibrationPoint> AffineCalibrationPoints
        {
            get => _affineCalibrationPoints;
            set => SetProperty(ref _affineCalibrationPoints, value);
        }

        // ── 仿射标定结果显示属性 ──

        private double _affineA;
        /// <summary>仿射参数A（Mx对Cx的系数）</summary>
        public double AffineA { get => _affineA; set => SetProperty(ref _affineA, value); }

        private double _affineB;
        /// <summary>仿射参数B（Mx对Cy的系数）</summary>
        public double AffineB { get => _affineB; set => SetProperty(ref _affineB, value); }

        private double _affineC;
        /// <summary>仿射参数C（My对Cx的系数）</summary>
        public double AffineC { get => _affineC; set => SetProperty(ref _affineC, value); }

        private double _affineD;
        /// <summary>仿射参数D（My对Cy的系数）</summary>
        public double AffineD { get => _affineD; set => SetProperty(ref _affineD, value); }

        private double _affineTx;
        /// <summary>X方向平移量Tx</summary>
        public double AffineTx { get => _affineTx; set => SetProperty(ref _affineTx, value); }

        private double _affineTy;
        /// <summary>Y方向平移量Ty</summary>
        public double AffineTy { get => _affineTy; set => SetProperty(ref _affineTy, value); }

        private double _affineRmsError;
        /// <summary>仿射标定RMS均方根误差(mm)</summary>
        public double AffineRmsError { get => _affineRmsError; set => SetProperty(ref _affineRmsError, value); }

        private string _affineQualityText = "";
        /// <summary>仿射标定质量评级文本</summary>
        public string AffineQualityText { get => _affineQualityText; set => SetProperty(ref _affineQualityText, value); }

        private double _affineRotDeg;
        /// <summary>仿射等效旋转角度(度)</summary>
        public double AffineRotDeg { get => _affineRotDeg; set => SetProperty(ref _affineRotDeg, value); }

        /// <summary>仿射标定结果对象（供 ExecuteTransform 使用）</summary>
        private AffineCalibrationResult _affineResult;

        #endregion

        #region 步骤3 — 旋转角度

        private int _basePairIndex;
        public int BasePairIndex { get => _basePairIndex; set => SetProperty(ref _basePairIndex, value); }

        private int _targetPairIndex;
        public int TargetPairIndex { get => _targetPairIndex; set => SetProperty(ref _targetPairIndex, value); }

        private List<string> _pairNames = new();
        public List<string> PairNames { get => _pairNames; set => SetProperty(ref _pairNames, value); }

        /// <summary>基准线段选取的坐标点显示文本（如 "#1 (10.5, 20.3) → #3 (15.2, 30.1)"）</summary>
        private string _baselineDisplayText = "";
        public string BaselineDisplayText { get => _baselineDisplayText; set => SetProperty(ref _baselineDisplayText, value); }

        /// <summary>目标线段选取的坐标点显示文本（如 "#5 (30.1, 40.2) → #7 (35.3, 45.4)"）</summary>
        private string _targetlineDisplayText = "";
        public string TargetlineDisplayText { get => _targetlineDisplayText; set => SetProperty(ref _targetlineDisplayText, value); }

        /// <summary>是否已选取基准线段（控制"显示基准线段"按钮启用状态）</summary>
        public bool HasBaselineSelected => BaseStartIndex >= 0 && BaseEndIndex >= 0;

        /// <summary>是否已选取目标线段（控制"显示目标线段"按钮启用状态）</summary>
        public bool HasTargetlineSelected => TargetStartIndex >= 0 && TargetEndIndex >= 0;

        /// <summary>基准起点变换后坐标显示文本</summary>
        private string _baseStartTransformedText = "";
        public string BaseStartTransformedText { get => _baseStartTransformedText; set => SetProperty(ref _baseStartTransformedText, value); }

        /// <summary>基准终点变换后坐标显示文本</summary>
        private string _baseEndTransformedText = "";
        public string BaseEndTransformedText { get => _baseEndTransformedText; set => SetProperty(ref _baseEndTransformedText, value); }

        /// <summary>目标起点变换后坐标显示文本</summary>
        private string _targetStartTransformedText = "";
        public string TargetStartTransformedText { get => _targetStartTransformedText; set => SetProperty(ref _targetStartTransformedText, value); }

        /// <summary>目标终点变换后坐标显示文本</summary>
        private string _targetEndTransformedText = "";
        public string TargetEndTransformedText { get => _targetEndTransformedText; set => SetProperty(ref _targetEndTransformedText, value); }

        /// <summary>步骤3已选点位是否有变换结果（控制变换坐标区域显示）</summary>
        public bool HasStep3TransformResult => Step2Done;

        private double _alphaBaseDeg;
        public double AlphaBaseDeg { get => _alphaBaseDeg; set => SetProperty(ref _alphaBaseDeg, value); }

        private double _alphaTargetDeg;
        public double AlphaTargetDeg { get => _alphaTargetDeg; set => SetProperty(ref _alphaTargetDeg, value); }

        private double _thetaDeg;
        public double ThetaDeg { get => _thetaDeg; set { SetProperty(ref _thetaDeg, value); (InheritTargetFromStep3Command as DelegateCommand)?.RaiseCanExecuteChanged(); RaisePropertyChanged(nameof(ProductRotationAngle)); } }

        private bool _invertXAngle;
        /// <summary>X方向角度取反开关，启用后旋转时dx取反</summary>
        public bool InvertXAngle { get => _invertXAngle; set => SetProperty(ref _invertXAngle, value); }

        private bool _invertYAngle;
        /// <summary>Y方向角度取反开关，启用后旋转时dy取反</summary>
        public bool InvertYAngle { get => _invertYAngle; set => SetProperty(ref _invertYAngle, value); }

        private bool _invertThetaAngle;
        /// <summary>角度θ取反开关，启用后旋转角度θ变为-θ</summary>
        public bool InvertThetaAngle { get => _invertThetaAngle; set => SetProperty(ref _invertThetaAngle, value); }

        /// <summary>实际使用的旋转角度（考虑θ取反开关）</summary>
        private double EffectiveThetaDeg => _invertThetaAngle ? -_thetaDeg : _thetaDeg;

        /// <summary>获取X方向有效偏移（考虑取反开关）</summary>
        private double Ex(double dx) => _invertXAngle ? -dx : dx;
        /// <summary>获取Y方向有效偏移（考虑取反开关）</summary>
        private double Ey(double dy) => _invertYAngle ? -dy : dy;

        private bool _step3Done;
        public bool Step3Done { get => _step3Done; set { SetProperty(ref _step3Done, value); (InheritTargetFromStep3Command as DelegateCommand)?.RaiseCanExecuteChanged(); } }

        public bool CanInheritFromStep3 => Step3Done && ThetaDeg != 0;

        // === 产品对齐角度 ===
        private double _alignmentAngle;
        /// <summary>产品与CAD图纸的对齐角度（用户输入或从全局变量链接）</summary>
        public double AlignmentAngle
        {
            get => _alignmentAngle;
            set
            {
                if (SetProperty(ref _alignmentAngle, value))
                    RaisePropertyChanged(nameof(ProductRotationAngle));
            }
        }

        /// <summary>产品旋转角度 = 对齐角度 − θ（计算属性）</summary>
        public double ProductRotationAngle => Math.Round(_alignmentAngle + ThetaDeg, 3);

        /// <summary>对齐角度链接的全局变量名——选择变量时自动读取值</summary>
        private string _alignmentAngleLinkedVar = "";
        public string AlignmentAngleLinkedVar
        {
            get => _alignmentAngleLinkedVar;
            set
            {
                if (SetProperty(ref _alignmentAngleLinkedVar, value) && !string.IsNullOrWhiteSpace(value))
                    OnLinkAlignmentAngle();
            }
        }

        private bool _isAlignmentAngleLinked;
        public bool IsAlignmentAngleLinked { get => _isAlignmentAngleLinked; set => SetProperty(ref _isAlignmentAngleLinked, value); }

        private bool _hasCadDrawingLoaded;
        public bool HasCadDrawingLoaded
        {
            get => _hasCadDrawingLoaded;
            set => SetProperty(ref _hasCadDrawingLoaded, value);
        }

        private string _cadPickStatus;
        public string CadPickStatus
        {
            get => _cadPickStatus;
            set => SetProperty(ref _cadPickStatus, value);
        }

        // 导入的 CAD 点位集合
        private ObservableCollection<CadPoint> _importedCadPoints = new();
        public ObservableCollection<CadPoint> ImportedCadPoints { get => _importedCadPoints; set => SetProperty(ref _importedCadPoints, value); }

        // DXF 解析结果缓存（含完整 CadEntity 图元信息，供 HalconCanvas 渲染）
        private DxfParseResult _dxfParseResult;
        public DxfParseResult DxfParseResult { get => _dxfParseResult; set => SetProperty(ref _dxfParseResult, value); }

        // ✅ 新增：DXF 统一导入服务（保证与 CadPointEditorViewModel 使用相同导入逻辑）
        private IDxfImportHelper _dxfImportHelper;

        // 所有图元的扁平列表（供 HalconCanvas ItemsSource 绑定）
        private ObservableCollection<CadEntity> _cadEntities = new();
        public ObservableCollection<CadEntity> CadEntities { get => _cadEntities; set => SetProperty(ref _cadEntities, value); }

        // 选取标记叠加层（X标记 + 线段，与 DXF 图元合并后显示在 HalconCanvas 上）
        private readonly List<CadEntity> _alignmentMarkers = new();

        /// <summary>HalconCanvas 绑定的合并实体列表（DXF图元 + 选取标记），必须为 ObservableCollection</summary>
        private ObservableCollection<CadEntity> _canvasDisplayEntities = new();
        public ObservableCollection<CadEntity> CanvasDisplayEntities { get => _canvasDisplayEntities; set => SetProperty(ref _canvasDisplayEntities, value); }

        // DataGrid 选中项（用于点击选取）
        private CadPoint _selectedCadPoint;
        public CadPoint SelectedCadPoint
        {
            get => _selectedCadPoint;
            set
            {
                if (SetProperty(ref _selectedCadPoint, value) && value != null)
                {
                    var idx = ImportedCadPoints.IndexOf(value);
                    if (idx >= 0)
                    {
                        CadSelectedSegmentPoints = new List<CadPoint> { value };
                        CadSelectedPointIndex = 0;
                    }
                    OnCadPointSelected(value);
                }
            }
        }

        // 基准线段起点/终点索引（指向 ImportedCadPoints）
        private int _baseStartIndex = -1;
        public int BaseStartIndex { get => _baseStartIndex; set => SetProperty(ref _baseStartIndex, value); }
        private int _baseEndIndex = -1;
        public int BaseEndIndex { get => _baseEndIndex; set => SetProperty(ref _baseEndIndex, value); }

        // 目标线段起点/终点索引（指向 ImportedCadPoints）
        private int _targetStartIndex = -1;
        public int TargetStartIndex { get => _targetStartIndex; set => SetProperty(ref _targetStartIndex, value); }
        private int _targetEndIndex = -1;
        public int TargetEndIndex { get => _targetEndIndex; set => SetProperty(ref _targetEndIndex, value); }

        // 导入文件路径显示
        private string _cadFilePath = "";
        public string CadFilePath { get => _cadFilePath; set => SetProperty(ref _cadFilePath, value); }

        // 选取模式状态
        private bool _isPickingBaseline;
        private bool _isPickingTarget;
        /// <summary>仿射标定CAD坐标拾取模式（在画布上点击写入 CadX/CadY）</summary>
        private bool _isPickingAffineCadCoord;
        /// <summary>当前正在拾取CAD坐标的仿射标定点</summary>
        private AffineCalibrationPoint _selectedAffineCalibrationPoint;

        /// <summary>图形窗口上显示X标记的点位集合（绑定到HalconCanvasControl.SelectedSegmentPoints）</summary>
        private List<CadPoint> _cadSelectedSegmentPoints;
        public List<CadPoint> CadSelectedSegmentPoints
        {
            get => _cadSelectedSegmentPoints;
            set => SetProperty(ref _cadSelectedSegmentPoints, value);
        }

        /// <summary>图形窗口上高亮显示的选中点位索引（绑定到HalconCanvasControl.SelectedPointIndex）</summary>
        private int _cadSelectedPointIndex = -1;
        public int CadSelectedPointIndex
        {
            get => _cadSelectedPointIndex;
            set => SetProperty(ref _cadSelectedPointIndex, value);
        }

        #endregion

        #region 步骤4 — 坐标变换

        private int _transformSelectedIndex = 2;
        public int TransformSelectedIndex { get => _transformSelectedIndex; set => SetProperty(ref _transformSelectedIndex, value); }

        private int _targetPointIndex;
        public int TargetPointIndex { get => _targetPointIndex; set => SetProperty(ref _targetPointIndex, value); }

        private List<string> _pointNames = new();
        public List<string> PointNames { get => _pointNames; set => SetProperty(ref _pointNames, value); }

        /// <summary>步骤4目标位原始CAD坐标显示文本</summary>
        private string _step4TargetCadText = "";
        public string Step4TargetCadText { get => _step4TargetCadText; set => SetProperty(ref _step4TargetCadText, value); }

        /// <summary>步骤4目标位平移后坐标显示文本</summary>
        private string _step4TargetOffsetText = "";
        public string Step4TargetOffsetText { get => _step4TargetOffsetText; set => SetProperty(ref _step4TargetOffsetText, value); }

        /// <summary>是否使用步骤3的CAD目标点位作为步骤4变换源（而非CorrespondencePoints）</summary>
        private bool _useStep3TargetForTransform;

        private double _transXm;
        public double TransXm { get => _transXm; set => SetProperty(ref _transXm, value); }

        private double _transYm;
        public double TransYm { get => _transYm; set => SetProperty(ref _transYm, value); }

        private double _transDx;
        public double TransDx { get => _transDx; set => SetProperty(ref _transDx, value); }

        private double _transDy;
        public double TransDy { get => _transDy; set => SetProperty(ref _transDy, value); }

        private double _transResultX;
public double TransResultX { get => _transResultX; set { if (SetProperty(ref _transResultX, value)) { (ApplyCalcOffsetCommand as DelegateCommand)?.RaiseCanExecuteChanged(); } } }

private double _transResultY;
public double TransResultY { get => _transResultY; set { if (SetProperty(ref _transResultY, value)) { (ApplyCalcOffsetCommand as DelegateCommand)?.RaiseCanExecuteChanged(); } } }

        private bool _step4Done;
        public bool Step4Done { get => _step4Done; set => SetProperty(ref _step4Done, value); }

        #endregion

        #region 步骤5 — 夹爪定位

private double _gripperOffX = 15.0;
public double GripperOffX { get => _gripperOffX; set => SetProperty(ref _gripperOffX, value); }

private double _gripperOffY = -10.0;
public double GripperOffY { get => _gripperOffY; set => SetProperty(ref _gripperOffY, value); }

// === 示教坐标 ===
private double _teachX;
public double TeachX { get => _teachX; set => SetProperty(ref _teachX, value); }

private double _teachY;
public double TeachY { get => _teachY; set => SetProperty(ref _teachY, value); }

private double _teachRy;
public double TeachRy { get => _teachRy; set => SetProperty(ref _teachRy, value); }

private double _teachZ;
public double TeachZ { get => _teachZ; set => SetProperty(ref _teachZ, value); }

// === 计算偏移量（只读）===
public double CalcOffX => TransResultX != 0 ? Math.Round(TeachX - TransResultX, 3) : 0;
        public double CalcOffY => TransResultY != 0 ? Math.Round(TeachY - TransResultY, 3) : 0;

// === 偏移模式切换 ===
private bool _useCalculatedOffset;
public bool UseCalculatedOffset 
{ 
    get => _useCalculatedOffset; 
    set 
    { 
        if (SetProperty(ref _useCalculatedOffset, value))
        {
            RaisePropertyChanged(nameof(CalcOffX));
            RaisePropertyChanged(nameof(CalcOffY));
            if (value && TransResultX != 0 && TransResultY != 0)
            {
                GripperOffX = CalcOffX;
                GripperOffY = CalcOffY;
            }
        } 
    }
}

private double _finalGripperX;
public double FinalGripperX { get => _finalGripperX; set => SetProperty(ref _finalGripperX, value); }

private double _finalGripperY;
public double FinalGripperY { get => _finalGripperY; set => SetProperty(ref _finalGripperY, value); }

private string _finalGripperXLinkedVar = "GripperFinalX";
public string FinalGripperXLinkedVar { get => _finalGripperXLinkedVar; set => SetProperty(ref _finalGripperXLinkedVar, value); }

private string _finalGripperYLinkedVar = "GripperFinalY";
public string FinalGripperYLinkedVar { get => _finalGripperYLinkedVar; set => SetProperty(ref _finalGripperYLinkedVar, value); }

private string _finalGripperZLinkedVar = "GripperFinalZ";
public string FinalGripperZLinkedVar { get => _finalGripperZLinkedVar; set => SetProperty(ref _finalGripperZLinkedVar, value); }

private bool _isGripperXLinked;
public bool IsGripperXLinked { get => _isGripperXLinked; set => SetProperty(ref _isGripperXLinked, value); }

private bool _isGripperYLinked;
public bool IsGripperYLinked { get => _isGripperYLinked; set => SetProperty(ref _isGripperYLinked, value); }

private bool _isGripperZLinked;
public bool IsGripperZLinked { get => _isGripperZLinked; set => SetProperty(ref _isGripperZLinked, value); }

public DelegateCommand UnlinkGripperXCommand { get; }
public DelegateCommand UnlinkGripperYCommand { get; }
public DelegateCommand UnlinkGripperZCommand { get; }

/// <summary>可选取的全局变量列表（用于ComboBox下拉选择）</summary>
private ObservableCollection<GlobalVariable> _availableGlobalVariables = new();
public ObservableCollection<GlobalVariable> AvailableGlobalVariables { get => _availableGlobalVariables; set => SetProperty(ref _availableGlobalVariables, value); }

public ObservableCollection<GlobalVariable> LinkableGlobalVariables => AvailableGlobalVariables;

private bool _step5Done;
public bool Step5Done { get => _step5Done; set => SetProperty(ref _step5Done, value); }

// === 5步夹爪定位流程属性 ===
/// <summary>相机基准位X（=最终变换结果X）</summary>
private double _cameraRefX;
public double CameraRefX { get => _cameraRefX; set => SetProperty(ref _cameraRefX, value); }

/// <summary>相机基准位Y（=最终变换结果Y）</summary>
private double _cameraRefY;
public double CameraRefY { get => _cameraRefY; set => SetProperty(ref _cameraRefY, value); }

/// <summary>相机基准位Z（Dz₁轴位置）</summary>
private double _cameraRefZ;
public double CameraRefZ { get => _cameraRefZ; set => SetProperty(ref _cameraRefZ, value); }

/// <summary>夹爪基准位X（示教读取）</summary>
private double _gripperRefX;
public double GripperRefX { get => _gripperRefX; set => SetProperty(ref _gripperRefX, value); }

/// <summary>夹爪基准位Y（示教读取）</summary>
private double _gripperRefY;
public double GripperRefY { get => _gripperRefY; set => SetProperty(ref _gripperRefY, value); }

/// <summary>夹爪基准位Z（Z轴示教读取）</summary>
private double _gripperRefZ;
public double GripperRefZ { get => _gripperRefZ; set => SetProperty(ref _gripperRefZ, value); }

/// <summary>相机偏移量X = 当前相机X - 相机基准X</summary>
private double _cameraOffsetX;
public double CameraOffsetX { get => _cameraOffsetX; set => SetProperty(ref _cameraOffsetX, value); }

/// <summary>相机偏移量Y = 当前相机Y - 相机基准Y</summary>
private double _cameraOffsetY;
public double CameraOffsetY { get => _cameraOffsetY; set => SetProperty(ref _cameraOffsetY, value); }

/// <summary>夹爪最终位置X = 夹爪基准X + 相机偏移X</summary>
private double _gripperFinalX;
public double GripperFinalX { get => _gripperFinalX; set => SetProperty(ref _gripperFinalX, value); }

/// <summary>夹爪最终位置Y = 夹爪基准Y + 相机偏移Y</summary>
private double _gripperFinalY;
public double GripperFinalY { get => _gripperFinalY; set => SetProperty(ref _gripperFinalY, value); }

/// <summary>夹爪最终位置Z = 夹爪基准高度Z</summary>
private double _gripperFinalZ;
public double GripperFinalZ { get => _gripperFinalZ; set => SetProperty(ref _gripperFinalZ, value); }

private string _currentFilePath = "";
public string CurrentFilePath { get => _currentFilePath; set => SetProperty(ref _currentFilePath, value); }

private string _currentFileName = "";
public string CurrentFileName { get => _currentFileName; set => SetProperty(ref _currentFileName, value); }

#endregion

        #region 通用属性

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        #endregion

        #region 命令定义

        public ICommand FitRotationCenterCommand { get; private set; }
        public ICommand ComputeGlobalOffsetCommand { get; private set; }
        public ICommand ComputeCadRotationAngleCommand { get; private set; }
        public ICommand ExecuteTransformCommand { get; private set; }
        public ICommand ExecuteBatchTransformCommand { get; private set; }
        public ICommand ComputeGripperPositionCommand { get; private set; }
        /// <summary>移动目标角度命令（将产品旋转角度下发到旋转轴）</summary>
        public ICommand MoveTargetAngleCommand { get; private set; }
        /// <summary>链接对齐角度到全局变量命令</summary>
        public ICommand LinkAlignmentAngleCommand { get; private set; }
        /// <summary>取消对齐角度全局变量链接</summary>
        public DelegateCommand UnlinkAlignmentAngleCommand { get; }
        /// <summary>移动目标位命令（Z轴安全检查 + Dx/Dy移动到变换结果）</summary>
        public ICommand MoveTargetPositionCommand { get; private set; }
        /// <summary>设置相机基准位命令（读取Dx/Dy当前位置作为相机基准）</summary>
        public ICommand SetCameraRefCommand { get; private set; }
        /// <summary>示教夹爪基准位命令</summary>
        public ICommand TeachGripperRefCommand { get; private set; }
        /// <summary>计算相机偏移命令</summary>
        public ICommand CalcCameraOffsetCommand { get; private set; }
        /// <summary>计算夹爪最终位置命令</summary>
        public ICommand CalcGripperFinalCommand { get; private set; }
        public ICommand ShowPrincipleCommand { get; private set; }
        public ICommand ExportDxfCommand { get; private set; }
        public ICommand AddCadPointCommand { get; private set; }
        public ICommand DeleteCadPointCommand { get; private set; }
        /// <summary>示教拟合点坐标命令</summary>
        public ICommand TeachFitPointCommand { get; private set; }
        /// <summary>移动轴到拟合点坐标（Dx/Dy插补）命令</summary>
        public ICommand MoveFitPointCommand { get; private set; }
        public ICommand TeachGripperPositionCommand { get; private set; }
        public ICommand ApplyCalcOffsetCommand { get; private set; }
        public ICommand PickBaselineFromCadCommand { get; private set; }
        public ICommand PickTargetFromCadCommand { get; private set; }
        public ICommand ImportDxfCommand { get; private set; }
        public ICommand AutoRecommendLinesCommand { get; private set; }
        public ICommand InheritTargetFromStep3Command { get; private set; }
        public ICommand ShowBaselineSegmentCommand { get; private set; }
        public ICommand ShowTargetlineSegmentCommand { get; private set; }
        public ICommand WriteToGlobalVariablesCommand { get; private set; }
        /// <summary>添加仿射标定点命令</summary>
        public ICommand AddAffineCalibrationPointCommand { get; private set; }
        /// <summary>删除仿射标定点命令</summary>
        public ICommand DeleteAffineCalibrationPointCommand { get; private set; }
        /// <summary>从CAD画布选取仿射标定点坐标命令</summary>
        public ICommand PickAffineCadCoordCommand { get; private set; }
        /// <summary>示教仿射标定点机械坐标命令</summary>
        public ICommand TeachAffineMachineCoordCommand { get; private set; }
        /// <summary>移动轴到仿射标定点坐标（Dx/Dy插补）命令</summary>
        public ICommand MoveAffineCalibrationPointCommand { get; private set; }
        public DelegateCommand SaveConfigCommand { get; }
        public DelegateCommand LoadConfigCommand { get; }

        /// <summary>请求画布执行 FitToAll 自适应视口（由 View 订阅）</summary>
        public event Action FitToAllRequested;

        /// <summary>请求画布聚焦到指定线段区域（由 View 订阅），参数为 (x1, y1, x2, y2)</summary>
        public event Action<double, double, double, double> FitToSegmentRequested;

        /// <summary>请求画布开始批量更新——暂停渲染，避免多次属性变更导致闪烁</summary>
        public event Action BatchUpdateStartRequested;

        /// <summary>请求画布结束批量恢复渲染——执行一次完整重绘</summary>
        public event Action BatchUpdateEndRequested;

        /// <summary>请求更新回转中心可视化画布（拟合后触发，View订阅后传入画布尺寸）</summary>
        public event Action RotationCenterVisualUpdateRequested;

#endregion

        #region 核心1：四点拟合求回转中心（最小二乘法 Kåsa 方法）

        /// <summary>
        /// 示教命令包装器，将 CommandParameter(object) 转换为 int 后调用实际示教方法
        /// </summary>
        private void OnTeachFitPointWrapper(object parameter)
        {
            if (parameter is int rowIndex)
                OnTeachFitPoint(rowIndex);
            else if (parameter != null && int.TryParse(parameter.ToString(), out int parsed))
                OnTeachFitPoint(parsed);
        }

        /// <summary>
        /// 示教单个拟合点：从运动控制器读取 Dx/Dy 轴实时位置作为拟合坐标
        /// FitX 对应 Dx 轴，FitY 对应 Dy 轴
        /// </summary>
        private void OnTeachFitPoint(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= FitPoints.Count) return;
            var fp = FitPoints[rowIndex];

            try
            {
                var motionService = _containerProvider.Resolve<IMotionService>();
                var axisConfigs = motionService.GetAxisConfigurations();

                // 从 hwcfg.xml 动态解析 Dx/Dy 轴逻辑 ID
                var dxConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dx");
                var dyConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dy");

                if (dxConfig == null || dyConfig == null)
                {
                    StatusMessage = L("CAD_Fit_AxisNotFound");
                    return;
                }

                // 读取 Dx/Dy 轴实时位置
                fp.FitX = Math.Round(motionService.GetAxisPosition(dxConfig.LogicalId), 3);
                fp.FitY = Math.Round(motionService.GetAxisPosition(dyConfig.LogicalId), 3);

                StatusMessage = string.Format(L("CAD_Fit_Coord_Get"), fp.AngleLabel, fp.FitX.ToString("F3"), fp.FitY.ToString("F3"));
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(L("CAD_Teach_Failed"), ex.Message);
            }
        }

        /// <summary>
        /// 使用最小二乘法（Kåsa方法）对四点进行圆拟合，求解回转中心(Mox,Moy)和拟合半径R
        /// 拟合方程：(x-a)² + (y-b)² = r²，其中(a,b)为圆心，r为半径
        /// </summary>
        private void FitRotationCenter()
        {
            if (FitPoints == null || FitPoints.Count < 3)
            {
                StatusMessage = L("CAD_Fit_Need3Points");
                return;
            }

            int n = FitPoints.Count;
            double sumX = 0, sumY = 0;
            for (int i = 0; i < n; i++)
            {
                sumX += FitPoints[i].FitX;
                sumY += FitPoints[i].FitY;
            }
            double xMean = sumX / n;
            double yMean = sumY / n;

            double A = 0, B = 0, C = 0, D = 0, E = 0;
            for (int i = 0; i < n; i++)
            {
                double xi = FitPoints[i].FitX - xMean;
                double yi = FitPoints[i].FitY - yMean;
                double xi2yi2 = xi * xi + yi * yi;
                A += xi2yi2;
                B += xi;
                C += yi;
                D += xi * xi2yi2;
                E += yi * xi2yi2;
            }

            double U = n * A - B * B - C * C;
            if (Math.Abs(U) < 1e-8)
            {
                StatusMessage = L("CAD_Fit_Degenerate");
                return;
            }

            double a = (n * D - B * A) / U + xMean;
            double b = (n * E - C * A) / U + yMean;
            double r = Math.Sqrt((a - xMean) * (a - xMean) + (b - yMean) * (b - yMean) + A / n);

            Mox = a;
            Moy = b;
            FitRadius = r;
            Step1Done = true;
            StatusMessage = string.Format(L("CAD_Fit_Center_Done"), a.ToString("F3"), b.ToString("F3"), r.ToString("F3"));

            // 通知View更新回转中心可视化画布
            RotationCenterVisualUpdateRequested?.Invoke();
        }

        private void OnFitRotationCenter() => FitRotationCenter();

        #endregion

        #region 核心2：计算全局偏移量 ΔX/ΔY （支持1点平移和N点仿射两种模式）

        /// <summary>
        /// 根据当前模式计算全局偏移：
        /// - 1点平移模式：ΔX = P1_Mx - P1_Cx,  ΔY = P1_My - P1_Cy
        /// - N点仿射模式：调用 ComputeAffineCalibration() 求解6参数仿射变换
        /// </summary>
        private void ComputeGlobalOffset()
        {
            if (_useAffineCalibration)
            {
                ComputeAffineCalibration();
                return;
            }

            // 1点平移模式（默认，向后兼容）
            DeltaX = P1Mx - P1Cx;
            DeltaY = P1My - P1Cy;
            _affineResult = null; // 清除仿射结果
            Step2Done = true;
            UpdateMachineCoordinates();
            UpdateTransformedCoordText();
            StatusMessage = string.Format(L("CAD_Offset_Done"), DeltaX.ToString("F3"), DeltaY.ToString("F3"));
        }

        /// <summary>
        /// N点仿射标定计算：从 AffineCalibrationPoints 提取 CAD 和 机械坐标对，
        /// 调用 AffineCalibrationService.Solve() 求解6个仿射参数，并显示结果
        /// </summary>
        private void ComputeAffineCalibration()
        {
            // 检查最少点数
            if (AffineCalibrationPoints == null || AffineCalibrationPoints.Count < 3)
            {
                StatusMessage = L("CAD_Affine_NeedMin3");
                return;
            }

            // 提取坐标对
            var cadPts = new List<(double Cx, double Cy)>();
            var mechPts = new List<(double Mx, double My)>();
            foreach (var pt in AffineCalibrationPoints)
            {
                cadPts.Add((pt.CadX, pt.CadY));
                mechPts.Add((pt.MachineX, pt.MachineY));
            }

            try
            {
                _affineResult = AffineCalibrationService.Solve(cadPts, mechPts);
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(L("CAD_Affine_SolveFailed"), ex.Message);
                return;
            }

            // 填充结果显示属性
            AffineA  = Math.Round(_affineResult.A,  6);
            AffineB  = Math.Round(_affineResult.B,  6);
            AffineC  = Math.Round(_affineResult.C,  6);
            AffineD  = Math.Round(_affineResult.D,  6);
            AffineTx = Math.Round(_affineResult.Tx, 3);
            AffineTy = Math.Round(_affineResult.Ty, 3);
            AffineRmsError = _affineResult.RmsError;
            AffineRotDeg = Math.Round(_affineResult.EquivalentRotationDeg, 3);

            // 质量评级（多语言）
            if (_affineResult.RmsError < 0.05)
                AffineQualityText = L("CAD_Affine_QualityGood");
            else if (_affineResult.RmsError < 0.10)
                AffineQualityText = L("CAD_Affine_QualityOK");
            else
                AffineQualityText = L("CAD_Affine_QualityBad");

            // 将残差写回每个标定点
            for (int i = 0; i < AffineCalibrationPoints.Count && i < _affineResult.Residuals.Count; i++)
            {
                AffineCalibrationPoints[i].Residual = Math.Round(_affineResult.Residuals[i], 4);
            }

            // 仿射模式下同步 DeltaX/DeltaY（用第一个点计算，用于兼容显示）
            if (AffineCalibrationPoints.Count > 0)
            {
                DeltaX = AffineCalibrationPoints[0].MachineX - AffineCalibrationPoints[0].CadX;
                DeltaY = AffineCalibrationPoints[0].MachineY - AffineCalibrationPoints[0].CadY;
            }

            Step2Done = true;
            UpdateMachineCoordinates();
            UpdateTransformedCoordText();
            StatusMessage = string.Format(L("CAD_Affine_Done"), AffineRmsError.ToString("F4"), AffineQualityText);
        }

        private void OnComputeGlobalOffset() => ComputeGlobalOffset();

        #endregion

        #region 仿射标定点管理（添加/删除/示教）

        /// <summary>添加新的仿射标定点（自动命名和索引）</summary>
        private void OnAddAffineCalibrationPoint()
        {
            int idx = AffineCalibrationPoints.Count;
            AffineCalibrationPoints.Add(new AffineCalibrationPoint
            {
                Index = idx,
                Name = $"P{idx + 1}",
                CadX = 0,
                CadY = 0,
                MachineX = 0,
                MachineY = 0
            });
        }

        /// <summary>从CAD画布选取仿射标定点坐标——进入拾取模式，等待画布点击</summary>
        private void OnPickAffineCadCoord(AffineCalibrationPoint point)
        {
            if (point == null) return;
            if (!HasCadDrawingLoaded)
            {
                StatusMessage = L("CAD_ImportDXF_First");
                return;
            }

            _isPickingAffineCadCoord = true;
            _isPickingBaseline = false;
            _isPickingTarget = false;
            _selectedAffineCalibrationPoint = point;
            StatusMessage = L("Step4_Status_PickCadCoord");
        }

        /// <summary>删除指定的仿射标定点（重新索引）</summary>
        private void OnDeleteAffineCalibrationPoint(AffineCalibrationPoint point)
        {
            if (point == null || AffineCalibrationPoints.Count <= 3)
            {
                StatusMessage = L("CAD_Affine_NeedMin3");
                return;
            }
            AffineCalibrationPoints.Remove(point);
            // 重新索引
            for (int i = 0; i < AffineCalibrationPoints.Count; i++)
            {
                AffineCalibrationPoints[i].Index = i;
                AffineCalibrationPoints[i].Name = $"P{i + 1}";
            }
        }

        /// <summary>
        /// 示教指定仿射标定点的机械坐标（从运动控制器实时读取）
        /// CommandParameter: AffineCalibrationPoint 实例或索引(int)
        /// </summary>
        private void OnTeachAffineMachineCoord(object parameter)
        {
            AffineCalibrationPoint target = null;
            if (parameter is AffineCalibrationPoint pt)
                target = pt;
            else if (parameter is int idx && idx >= 0 && idx < AffineCalibrationPoints.Count)
                target = AffineCalibrationPoints[idx];

            if (target == null)
            {
                StatusMessage = L("CAD_Affine_InvalidParam");
                return;
            }

            try
            {
                var motionService = _containerProvider.Resolve<IMotionService>();
                target.MachineX = Math.Round(motionService.GetAxisPosition(8), 3);
                target.MachineY = Math.Round(motionService.GetAxisPosition(6), 3);
                StatusMessage = string.Format(L("CAD_Affine_TeachDone"), target.Name,
                    target.MachineX.ToString("F3"), target.MachineY.ToString("F3"));
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(L("CAD_Teach_Failed"), ex.Message);
            }
        }

        #endregion

        #region 核心3：CAD向量方向角计算

        /// <summary>
        /// 从对应点集中取两对基准线段，分别计算其方向角，
        /// 所需旋转角度 θ = α_基准 - α_目标，归一化到 (-180, 180]
        /// </summary>
        private void ComputeCadRotationAngle()
        {
            // 优先使用从 CAD 导入点位选取的线段
            if (HasCadDrawingLoaded && BaseEndIndex >= 0 && TargetEndIndex >= 0)
            {
                // 边界检查：确保所有索引都在有效范围内
                if (BaseStartIndex < 0 || BaseStartIndex >= ImportedCadPoints.Count ||
                    BaseEndIndex < 0 || BaseEndIndex >= ImportedCadPoints.Count ||
                    TargetStartIndex < 0 || TargetStartIndex >= ImportedCadPoints.Count ||
                    TargetEndIndex < 0 || TargetEndIndex >= ImportedCadPoints.Count)
                {
                    StatusMessage = L("CAD_Rotation_InvalidIndex");
                    return;
                }

                var p1 = ImportedCadPoints[BaseStartIndex];
                var p2 = ImportedCadPoints[BaseEndIndex];
                var p3 = ImportedCadPoints[TargetStartIndex];
                var p4 = ImportedCadPoints[TargetEndIndex];

                double alphaBaseRad = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
                double alphaTargetRad = Math.Atan2(p4.Y - p3.Y, p4.X - p3.X);

                AlphaBaseDeg = alphaBaseRad * 180.0 / Math.PI;
                AlphaTargetDeg = alphaTargetRad * 180.0 / Math.PI;

                double theta = AlphaBaseDeg - AlphaTargetDeg;

                // 归一化到 (-360, 0]：保证始终顺时针旋转
                // 圆弧上多点对齐场景，要求每个位置都按同一方向（顺时针）转到基准角度
                while (theta > 0.0) theta -= 360.0;
                while (theta <= -360.0) theta += 360.0;

                ThetaDeg = theta;
                Step3Done = true;

                StatusMessage = string.Format(L("CAD_Rotation_Done_CAD"), AlphaBaseDeg.ToString("F3"), AlphaTargetDeg.ToString("F3"), ThetaDeg.ToString("F3"));
                CadPickStatus = string.Format(L("CAD_Rotation_Done_Theta"), ThetaDeg.ToString("F3"));
                return;
            }

            // 回退：从 CorrespondencePoints 中按 PairIndex 取点
            if (CorrespondencePoints == null || CorrespondencePoints.Count < 4)
            {
                StatusMessage = L("CAD_Rotation_Need4Points");
                return;
            }

            var cp1 = CorrespondencePoints[BasePairIndex];
            var cp2 = CorrespondencePoints[BasePairIndex + 1];
            var cp3 = CorrespondencePoints[TargetPairIndex];
            var cp4 = CorrespondencePoints[TargetPairIndex + 1];

            double alphaBaseRadFallback = Math.Atan2(cp2.CadY - cp1.CadY, cp2.CadX - cp1.CadX);
            double alphaTargetRadFallback = Math.Atan2(cp4.CadY - cp3.CadY, cp4.CadX - cp3.CadX);

            AlphaBaseDeg = alphaBaseRadFallback * 180.0 / Math.PI;
            AlphaTargetDeg = alphaTargetRadFallback * 180.0 / Math.PI;

            double thetaFallback = AlphaBaseDeg - AlphaTargetDeg;

            // 归一化到 (-360, 0]：保证始终顺时针旋转
            while (thetaFallback > 0.0) thetaFallback -= 360.0;
            while (thetaFallback <= -360.0) thetaFallback += 360.0;

            ThetaDeg = thetaFallback;
            Step3Done = true;
            StatusMessage = string.Format(L("CAD_Rotation_Done"), AlphaBaseDeg.ToString("F3"), AlphaTargetDeg.ToString("F3"), ThetaDeg.ToString("F3"));
        }

        private void OnComputeCadRotationAngle() => ComputeCadRotationAngle();

        /// <summary>从CAD图形窗口选取基准线段(P1-P2)</summary>
        private void OnPickBaselineFromCad()
        {
            if (!HasCadDrawingLoaded)
            {
                StatusMessage = L("CAD_ImportDXF_First");
                CadPickStatus = L("CAD_Import_CAD_First");
                return;
            }

            _isPickingBaseline = true;
            _isPickingTarget = false;
            _isPickingAffineCadCoord = false;

            BaseStartIndex = -1;
            BaseEndIndex = -1;
            UpdateCadPointRoles();

            CadSelectedSegmentPoints = null;
            CadSelectedPointIndex = -1;

            CadPickStatus = L("CAD_PickBaseline_Start");
            StatusMessage = L("CAD_PickBaseline_Status");
        }

        /// <summary>从CAD图形窗口选取目标线段(P3-P4)</summary>
        private void OnPickTargetFromCad()
        {
            if (!HasCadDrawingLoaded)
            {
                StatusMessage = L("CAD_ImportDXF_First");
                CadPickStatus = L("CAD_Import_CAD_First");
                return;
            }

            _isPickingTarget = true;
            _isPickingBaseline = false;
            _isPickingAffineCadCoord = false;

            TargetStartIndex = -1;
            TargetEndIndex = -1;
            UpdateCadPointRoles();

            CadSelectedSegmentPoints = null;
            CadSelectedPointIndex = -1;

            CadPickStatus = L("CAD_PickTarget_Start");
            StatusMessage = L("CAD_PickTarget_Status");
        }

        /// <summary>导入 DXF 文件并提取点位——使用 IDxfImportHelper 统一导入方法</summary>
        private void OnImportDxf()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "DXF文件|*.dxf",
                Title = "选择CAD图纸(DXF)",
                InitialDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "LibreCAD")
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                // 使用统一的 DXF 导入服务（与 CadPointEditorViewModel 共享相同逻辑）
                if (_dxfImportHelper != null)
                {
                    var importResult = _dxfImportHelper.Import(dialog.FileName, DxfImportOptions.ForAlignment);
                    _dxfParseResult = importResult.ParseResult;

                    // 使用统一导入服务返回的实体集合（已根据选项过滤 ARC）
                    CadEntities.Clear();
                    foreach (var entity in importResult.DisplayEntities)
                        CadEntities.Add(entity);

                    // 使用统一导入服务返回的点位数据
                    ImportedCadPoints.Clear();
                    for (int i = 0; i < importResult.ExtractedPoints.Count; i++)
                    {
                        var pt = importResult.ExtractedPoints[i];
                        ImportedCadPoints.Add(new CadPoint
                        {
                            Id = pt.Id ?? (i + 1).ToString(),
                            X = Math.Round(pt.X, 3),
                            Y = Math.Round(pt.Y, 3),
                            Z = Math.Round(pt.Z, 3),
                            AssySite = ""
                        });
                    }
                }
                else
                {
                    // 回退到旧的直接解析方法（当 IDxfImportHelper 不可用时）
                    OnImportDxfLegacy(dialog.FileName);
                }

                if (ImportedCadPoints.Count == 0 && CadEntities.Count == 0)
                {
                    StatusMessage = L("CAD_DXF_NoPoints");
                    CadPickStatus = L("CAD_NoPoints_Found");
                    return;
                }

                CadFilePath = dialog.FileName; // 存储完整路径以便自动加载恢复
                HasCadDrawingLoaded = true;

                RebuildCanvasDisplayEntities();
                FitToAllRequested?.Invoke();
                // 导入后立即用当前偏移量更新图像坐标（FitToAll回调会再次更新）
                UpdateImageCoordinates();

                BaseStartIndex = BaseEndIndex = -1;
                TargetStartIndex = TargetEndIndex = -1;
                _isPickingBaseline = _isPickingTarget = false;
                _isPickingAffineCadCoord = false;
                _selectedAffineCalibrationPoint = null;
                AlphaBaseDeg = AlphaTargetDeg = ThetaDeg = 0;
                Step3Done = false;
                UpdateCadPointRoles();
                UpdateMachineCoordinates();

                CadSelectedSegmentPoints = null;
                CadSelectedPointIndex = -1;

                CadPickStatus = string.Format(L("CAD_Import_Success"), ImportedCadPoints.Count);
                StatusMessage = $"DXF 导入成功：{ImportedCadPoints.Count} 个点，来源: {CadFilePath}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"DXF 导入失败: {ex.Message}";
                CadPickStatus = L("CAD_Import_Failed");
            }
        }

        /// <summary>
        /// 静默导入DXF文件（不弹窗，不重置线段选取），用于配置加载后自动恢复图形和点位
        /// </summary>
        private void ImportDxfSilent(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || !System.IO.File.Exists(fullPath)) return;

            try
            {
                if (_dxfImportHelper != null)
                {
                    var importResult = _dxfImportHelper.Import(fullPath, DxfImportOptions.ForAlignment);
                    _dxfParseResult = importResult.ParseResult;

                    CadEntities.Clear();
                    foreach (var entity in importResult.DisplayEntities)
                        CadEntities.Add(entity);

                    ImportedCadPoints.Clear();
                    for (int i = 0; i < importResult.ExtractedPoints.Count; i++)
                    {
                        var pt = importResult.ExtractedPoints[i];
                        ImportedCadPoints.Add(new CadPoint
                        {
                            Id = pt.Id ?? (i + 1).ToString(),
                            X = Math.Round(pt.X, 3),
                            Y = Math.Round(pt.Y, 3),
                            Z = Math.Round(pt.Z, 3),
                            AssySite = ""
                        });
                    }
                }
                else
                {
                    OnImportDxfLegacy(fullPath);
                }

                if (ImportedCadPoints.Count == 0 && CadEntities.Count == 0) return;

                CadFilePath = fullPath;
                HasCadDrawingLoaded = true;

                RebuildCanvasDisplayEntities();
                FitToAllRequested?.Invoke();
                // 静默恢复后立即用当前偏移量更新图像坐标（FitToAll回调会再次更新）
                UpdateImageCoordinates();
                UpdateCadPointRoles();
                UpdateMachineCoordinates();

                StatusMessage = $"DXF 自动恢复：{ImportedCadPoints.Count} 个点，来源: {System.IO.Path.GetFileName(fullPath)}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"DXF 自动恢复失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 回退的旧版 DXF 导入方法（当 IDxfImportHelper 不可用时使用）
        /// 保持向后兼容性
        /// </summary>
        private void OnImportDxfLegacy(string filePath)
        {
            try
            {
                var container = ContainerLocator.Container;
                if (container != null && container.IsRegistered<IDxfParserService>())
                {
                    var dxfParser = container.Resolve<IDxfParserService>();
                    _dxfParseResult = dxfParser.Parse(filePath);
                    CadEntities.Clear();
                    foreach (var layerEntities in _dxfParseResult.Layers.Values)
                        foreach (var entity in layerEntities)
                            if (entity is not Core.Models.CadArc)
                                CadEntities.Add(entity);
                }
            }
            catch { /* IDxfParserService 不可用时忽略 */ }

            var points = DxfParser.ExtractPoints(filePath, null);
            ImportedCadPoints.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                ImportedCadPoints.Add(new CadPoint
                {
                    Id = (i + 1).ToString(),
                    X = Math.Round(pt.X, 3),
                    Y = Math.Round(pt.Y, 3),
                    Z = Math.Round(pt.Z, 3),
                    AssySite = ""
                });
            }
        }

        /// <summary>
        /// 尝试从POLYLINE的VERTEX点和CIRCLE标记点生成最佳拟合椭圆
        /// 使用Direct Least Squares (DLS)算法 - Fitzgibbon 1999 - 工业级精度
        /// 优先使用POLYLINE的VERTEX点（98个采样点），数据量更大拟合更精确
        /// </summary>
        private void TryGenerateFittedEllipse()
        {
            try
            {
                // 步骤1：收集POLYLINE的所有VERTEX坐标作为主要拟合数据源
                // POLYLINE包含约98个VERTEX点，构成弧形轨迹的真实采样
                var fitPoints = new List<Core.Models.PointF>();

                // 1a: 收集ImportedCadPoints（来自DxfParser.ExtractPoints的POLYLINE VERTEX）
                if (ImportedCadPoints != null && ImportedCadPoints.Count >= 5)
                {
                    foreach (var pt in ImportedCadPoints)
                    {
                        fitPoints.Add(new Core.Models.PointF((float)pt.X, (float)pt.Y));
                    }
                    System.Diagnostics.Debug.WriteLine($"[EllipseFit] 使用{fitPoints.Count}个POLYLINE VERTEX点");
                }

                // 1b: 如果POLYLINE点不足，回退到CIRCLE标记点
                if (fitPoints.Count < 5)
                {
                    foreach (var entity in CadEntities)
                    {
                        if (entity is Core.Models.CadCircle circle)
                        {
                            fitPoints.Add(new Core.Models.PointF(
                                (float)circle.CenterX,
                                (float)circle.CenterY));
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"[EllipseFit] 回退使用{fitPoints.Count}个CIRCLE中心点");
                }

                if (fitPoints.Count < 5)
                {
                    System.Diagnostics.Debug.WriteLine($"[EllipseFit] 数据点不足({fitPoints.Count})，跳过椭圆拟合");
                    return;
                }

                // 步骤2：使用DLS算法计算精确的椭圆参数
#if HAS_HALCON
                var ellipseParams = Core.Models.CadEntityHalconExtensions.FitEllipseDLS(fitPoints);

                if (!ellipseParams.IsValid || double.IsNaN(ellipseParams.CenterX))
                {
                    System.Diagnostics.Debug.WriteLine("[EllipseFit] DLS拟合失败或结果无效");
                    return;
                }

                // 步骤3：生成拟合椭圆的XLD轮廓
                var fittedEllipseXld = Core.Models.CadEntityHalconExtensions.FitEllipseFromPoints(fitPoints);

                if (fittedEllipseXld == null || !fittedEllipseXld.IsInitialized())
                {
                    System.Diagnostics.Debug.WriteLine("[EllipseFit] 拟合椭圆XLD生成失败");
                    return;
                }

                // 步骤4：将拟合椭圆作为特殊实体添加到CadEntities
                var fittedEllipse = new Core.Models.CadArc
                {
                    CenterX = ellipseParams.CenterX,
                    CenterY = ellipseParams.CenterY,
                    Radius = Math.Max(ellipseParams.MajorAxis, ellipseParams.MinorAxis) / 2,
                    StartAngle = 0,
                    EndAngle = 360,
                    LayerName = "0",
                    Color = "#2196F3",
                    IsVisible = true,
                    Id = "FITTED_ELLIPSE_DLS"
                };

                // 将预计算的拟合椭圆XLD轮廓存储到Tag属性
                fittedEllipse.Tag = fittedEllipseXld;

                // 移除原始的ARC实体（起止角=0°,0°的不精确ARC）
                for (int i = CadEntities.Count - 1; i >= 0; i--)
                {
                    var e = CadEntities[i];
                    if (e.Id != null && e is Core.Models.CadArc arc &&
                        arc.StartAngle == 0 && arc.EndAngle == 0)
                    {
                        CadEntities.RemoveAt(i);
                    }
                }

                // 添加拟合椭圆到实体列表
                CadEntities.Add(fittedEllipse);

                System.Diagnostics.Debug.WriteLine(
                    string.Format("[EllipseFit] DLS成功: Center=({0},{1}), Axes=({2},{3}), Rot={4}°",
                        ellipseParams.CenterX.ToString("F2"),
                        ellipseParams.CenterY.ToString("F2"),
                        ellipseParams.MajorAxis.ToString("F2"),
                        ellipseParams.MinorAxis.ToString("F2"),
                        (ellipseParams.RotationRad * 180 / Math.PI).ToString("F1")));
#else
                System.Diagnostics.Debug.WriteLine("[EllipseFit] Halcon SDK 未安装，跳过椭圆拟合");
                return;
#endif

                StatusMessage += L("CAD_EllipseFit_Done");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EllipseFit] 异常: {ex.Message}");
            }
        }

        /// <summary>DataGrid 行选中时触发，根据当前选取模式分配到基准或目标线段</summary>
        /// <remarks>
        /// ✅ 性能优化：单次触发模式
        /// - CadSelectedSegmentPoints只赋值一次（避免多次触发HalconCanvas重绘）
        /// - UpdateCadPointRoles()内部已优化为批量更新
        /// - 状态消息即时反馈（用户体验优先）
        /// </remarks>
        private void OnCadPointSelected(CadPoint point)
        {
            if (point == null) return;

            var idx = ImportedCadPoints.IndexOf(point);
            if (idx < 0 || idx >= ImportedCadPoints.Count) return;

            // ✅ 优化：请求画布开始批量更新，暂停渲染，避免多次属性变更导致闪烁
            BatchUpdateStartRequested?.Invoke();

            try
            {
                CadSelectedPointIndex = idx;

                // 按需显示X标记：只在基准/目标线段选取模式下显示选中点
                if (_isPickingBaseline || _isPickingTarget)
                {
                    CadSelectedSegmentPoints = new List<CadPoint> { point };
                }
                else
                {
                    CadSelectedSegmentPoints = null;
                }

                if (_isPickingBaseline)
                {
                    if (BaseStartIndex < 0)
                    {
                        BaseStartIndex = idx;
                        UpdateCadPointRoles();
                        CadPickStatus = string.Format(L("CAD_Baseline_Start_Selected"), point.Id, point.X.ToString("F1"), point.Y.ToString("F1"));
                        StatusMessage = string.Format(L("CAD_Baseline_Start_Status"), point.Id, point.X.ToString("F1"), point.Y.ToString("F1"));
                    }
                    else if (BaseEndIndex < 0 && idx != BaseStartIndex)
                    {
                        BaseEndIndex = idx;
                        UpdateCadPointRoles();
                        if (BaseStartIndex >= 0 && BaseStartIndex < ImportedCadPoints.Count)
                        {
                            var p1 = ImportedCadPoints[BaseStartIndex];

                            AlphaBaseDeg = Math.Atan2(point.Y - p1.Y, point.X - p1.X) * 180 / Math.PI;
                            RaisePropertyChanged(nameof(AlphaBaseDeg));

                            _isPickingBaseline = false;
                            CadPickStatus = string.Format(L("CAD_Baseline_Done"), p1.Id, point.Id, AlphaBaseDeg.ToString("F2"));
                            StatusMessage = string.Format(L("CAD_Baseline_Done_Status"), p1.X.ToString("F1"), p1.Y.ToString("F1"), point.X.ToString("F1"), point.Y.ToString("F1"));
                        }
                    }
                    else if (idx == BaseStartIndex)
                    {
                        CadPickStatus = L("CAD_SamePoint_Warning_Base");
                    }
                }
                else if (_isPickingTarget)
                {
                    if (TargetStartIndex < 0)
                    {
                        TargetStartIndex = idx;
                        UpdateCadPointRoles();
                        CadPickStatus = string.Format(L("CAD_Target_Start_Selected"), point.Id, point.X.ToString("F1"), point.Y.ToString("F1"));
                        StatusMessage = string.Format(L("CAD_Target_Start_Status"), point.Id, point.X.ToString("F1"), point.Y.ToString("F1"));
                    }
                    else if (TargetEndIndex < 0 && idx != TargetStartIndex)
                    {
                        TargetEndIndex = idx;
                        UpdateCadPointRoles();
                        if (TargetStartIndex >= 0 && TargetStartIndex < ImportedCadPoints.Count)
                        {
                            var p3 = ImportedCadPoints[TargetStartIndex];

                            AlphaTargetDeg = Math.Atan2(point.Y - p3.Y, point.X - p3.X) * 180 / Math.PI;
                            RaisePropertyChanged(nameof(AlphaTargetDeg));

                            _isPickingTarget = false;
                            CadPickStatus = string.Format(L("CAD_Target_Done"), p3.Id, point.Id, AlphaTargetDeg.ToString("F2"));
                            StatusMessage = string.Format(L("CAD_Target_Done_Status"), p3.X.ToString("F1"), p3.Y.ToString("F1"), point.X.ToString("F1"), point.Y.ToString("F1"));

                            if (BaseEndIndex >= 0 && TargetEndIndex >= 0)
                            {
                                CadPickStatus += "\n" + L("CAD_TwoLines_Ready");
                            }
                        }
                    }
                }
                else if (idx == TargetStartIndex)
                {
                    CadPickStatus = L("CAD_SamePoint_Warning_Target");
                }
            }
            finally
            {
                // 请求画布结束批量更新，恢复渲染，执行一次完整重绘
                BatchUpdateEndRequested?.Invoke();
            }
        }

        /// <summary>根据当前选取索引更新每个CAD点位的AssySite角色标记，并重建图形叠加层和X标记</summary>
        /// <remarks>
        /// 批量更新模式 - 抑制中间状态的UI刷新
        /// 所有数据修改完成后统一触发一次UI更新，避免级联刷新风暴
        /// </remarks>
        private void UpdateCadPointRoles()
        {
            // 批量更新：先完成所有数据修改
            foreach (var pt in ImportedCadPoints)
                pt.AssySite = "";

            if (BaseStartIndex >= 0 && BaseStartIndex < ImportedCadPoints.Count)
                ImportedCadPoints[BaseStartIndex].AssySite = L("CAD_Base_Start");
            if (BaseEndIndex >= 0 && BaseEndIndex < ImportedCadPoints.Count)
                ImportedCadPoints[BaseEndIndex].AssySite = L("CAD_Base_End");
            if (TargetStartIndex >= 0 && TargetStartIndex < ImportedCadPoints.Count)
                ImportedCadPoints[TargetStartIndex].AssySite = L("CAD_Target_Start");
            if (TargetEndIndex >= 0 && TargetEndIndex < ImportedCadPoints.Count)
                ImportedCadPoints[TargetEndIndex].AssySite = L("CAD_Target_End");

            // 批量更新：所有数据修改完成后统一触发UI刷新
            UpdateLineSegmentDisplayText();
            UpdateTransformedCoordText();
            UpdateCanvasPointMarkers(); // 此方法内部会设置CadSelectedSegmentPoints，触发一次绑定更新
            RebuildAlignmentMarkers();

            (ShowBaselineSegmentCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            (ShowTargetlineSegmentCommand as DelegateCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>更新已选点位的变换后坐标显示文本（原始CAD坐标 → 平移后 → 旋转后）</summary>
        private void UpdateTransformedCoordText()
        {
            BaseStartTransformedText = FormatPointTransformText(BaseStartIndex);
            BaseEndTransformedText = FormatPointTransformText(BaseEndIndex);
            TargetStartTransformedText = FormatPointTransformText(TargetStartIndex);
            TargetEndTransformedText = FormatPointTransformText(TargetEndIndex);
            RaisePropertyChanged(nameof(HasStep3TransformResult));
        }

        /// <summary>
        /// 格式化单个点位的变换坐标显示文本（原始CAD坐标 → 机械坐标）
        /// - 仿射模式: 使用6参数仿射变换 Mx = A·Cx + B·Cy + Tx
        /// - 1点平移模式: 使用简单偏移 Mx = Cx + ΔX
        /// </summary>
        private string FormatPointTransformText(int pointIndex)
        {
            if (pointIndex < 0 || pointIndex >= ImportedCadPoints.Count) return "";
            var pt = ImportedCadPoints[pointIndex];
            string original = string.Format("({0}, {1})", pt.X.ToString("F2"), pt.Y.ToString("F2"));

            if (!Step2Done) return string.Format("原始: {0}", original);

            double xm, ym;
            if (_useAffineCalibration && _affineResult != null)
            {
                // 仿射模式: 使用矩阵运算
                var (mx, my) = AffineCalibrationService.Transform(_affineResult, pt.X, pt.Y);
                xm = mx;
                ym = my;
            }
            else
            {
                // 1点平移模式
                xm = pt.X + DeltaX;
                ym = pt.Y + DeltaY;
            }
            string machine = string.Format("({0}, {1})", xm.ToString("F2"), ym.ToString("F2"));

            return string.Format("原始: {0}\n机械: {1}", original, machine);
        }

        /// <summary>更新基准线段/目标线段的坐标点显示文本</summary>
        private void UpdateLineSegmentDisplayText()
        {
            if (BaseStartIndex >= 0 && BaseEndIndex >= 0 &&
                BaseStartIndex < ImportedCadPoints.Count && BaseEndIndex < ImportedCadPoints.Count)
            {
                var p1 = ImportedCadPoints[BaseStartIndex];
                var p2 = ImportedCadPoints[BaseEndIndex];
                BaselineDisplayText = string.Format("#{0} ({1}, {2}) → #{3} ({4}, {5})",
                    p1.Id, p1.X.ToString("F1"), p1.Y.ToString("F1"),
                    p2.Id, p2.X.ToString("F1"), p2.Y.ToString("F1"));
            }
            else if (BaseStartIndex >= 0 && BaseStartIndex < ImportedCadPoints.Count)
            {
                var p1 = ImportedCadPoints[BaseStartIndex];
                BaselineDisplayText = string.Format("#{0} ({1}, {2}) → ?",
                    p1.Id, p1.X.ToString("F1"), p1.Y.ToString("F1"));
            }
            else
            {
                BaselineDisplayText = "";
            }

            if (TargetStartIndex >= 0 && TargetEndIndex >= 0 &&
                TargetStartIndex < ImportedCadPoints.Count && TargetEndIndex < ImportedCadPoints.Count)
            {
                var p3 = ImportedCadPoints[TargetStartIndex];
                var p4 = ImportedCadPoints[TargetEndIndex];
                TargetlineDisplayText = string.Format("#{0} ({1}, {2}) → #{3} ({4}, {5})",
                    p3.Id, p3.X.ToString("F1"), p3.Y.ToString("F1"),
                    p4.Id, p4.X.ToString("F1"), p4.Y.ToString("F1"));
            }
            else if (TargetStartIndex >= 0 && TargetStartIndex < ImportedCadPoints.Count)
            {
                var p3 = ImportedCadPoints[TargetStartIndex];
                TargetlineDisplayText = string.Format("#{0} ({1}, {2}) → ?",
                    p3.Id, p3.X.ToString("F1"), p3.Y.ToString("F1"));
            }
            else
            {
                TargetlineDisplayText = "";
            }

            RaisePropertyChanged(nameof(HasBaselineSelected));
            RaisePropertyChanged(nameof(HasTargetlineSelected));
        }

        /// <summary>更新HalconCanvas原生X标记系统：将已选点位集合和当前高亮索引同步到画布</summary>
        private void UpdateCanvasPointMarkers()
        {
            var markedPoints = new List<CadPoint>();
            int highlightIndex = -1;

            if (BaseStartIndex >= 0 && BaseStartIndex < ImportedCadPoints.Count)
                markedPoints.Add(ImportedCadPoints[BaseStartIndex]);
            if (BaseEndIndex >= 0 && BaseEndIndex < ImportedCadPoints.Count)
                markedPoints.Add(ImportedCadPoints[BaseEndIndex]);
            if (TargetStartIndex >= 0 && TargetStartIndex < ImportedCadPoints.Count)
                markedPoints.Add(ImportedCadPoints[TargetStartIndex]);
            if (TargetEndIndex >= 0 && TargetEndIndex < ImportedCadPoints.Count)
                markedPoints.Add(ImportedCadPoints[TargetEndIndex]);

            if (markedPoints.Count > 0)
                highlightIndex = markedPoints.Count - 1;

            CadSelectedSegmentPoints = markedPoints.Count > 0 ? markedPoints : null;
            CadSelectedPointIndex = highlightIndex;
        }

        /// <summary>重建选取标记叠加层：X标记(选中点位) + 蓝色基准线段 + 红色目标线段</summary>
        private void RebuildAlignmentMarkers()
        {
            _alignmentMarkers.Clear();
            if (ImportedCadPoints == null || ImportedCadPoints.Count == 0) return;

            // 根据图形包围盒动态计算标记大小（占包围盒对角线的2%）
            double markSize = CalcDynamicMarkSize();
            double tolerance = markSize * 3; // 容差为标记大小的3倍

            // 辅助方法：在点(x,y)处创建X标记（两条交叉短线）
            void AddCrossMark(double x, double y, string color)
            {
                _alignmentMarkers.Add(new CadLine(x - markSize, y - markSize, x + markSize, y + markSize) { Color = color, LayerName = "_MARK_" });
                _alignmentMarkers.Add(new CadLine(x - markSize, y + markSize, x + markSize, y - markSize) { Color = color, LayerName = "_MARK_" });
            }

            // 基准起点 X (蓝色)
            if (BaseStartIndex >= 0 && BaseStartIndex < ImportedCadPoints.Count)
            {
                var p = ImportedCadPoints[BaseStartIndex];
                AddCrossMark(p.X, p.Y, "#1565C0");
            }
            // 基准终点 X (绿色)
            if (BaseEndIndex >= 0 && BaseEndIndex < ImportedCadPoints.Count)
            {
                var p = ImportedCadPoints[BaseEndIndex];
                AddCrossMark(p.X, p.Y, "#2E7D32");
            }
            // 目标起点 X (紫色)
            if (TargetStartIndex >= 0 && TargetStartIndex < ImportedCadPoints.Count)
            {
                var p = ImportedCadPoints[TargetStartIndex];
                AddCrossMark(p.X, p.Y, "#7B1FA2");
            }
            // 目标终点 X (红色)
            if (TargetEndIndex >= 0 && TargetEndIndex < ImportedCadPoints.Count)
            {
                var p = ImportedCadPoints[TargetEndIndex];
                AddCrossMark(p.X, p.Y, "#C62828");
            }

            // 基准线段 (蓝色粗线)
            if (BaseStartIndex >= 0 && BaseEndIndex >= 0 &&
                BaseStartIndex < ImportedCadPoints.Count && BaseEndIndex < ImportedCadPoints.Count)
            {
                var p1 = ImportedCadPoints[BaseStartIndex];
                var p2 = ImportedCadPoints[BaseEndIndex];
                _alignmentMarkers.Add(new CadLine(p1.X, p1.Y, p2.X, p2.Y) { Color = "#1565C0", LayerName = "_BASELINE_" });
            }

            // 目标线段 (红色粗线)
            if (TargetStartIndex >= 0 && TargetEndIndex >= 0 &&
                TargetStartIndex < ImportedCadPoints.Count && TargetEndIndex < ImportedCadPoints.Count)
            {
                var p3 = ImportedCadPoints[TargetStartIndex];
                var p4 = ImportedCadPoints[TargetEndIndex];
                _alignmentMarkers.Add(new CadLine(p3.X, p3.Y, p4.X, p4.Y) { Color = "#C62828", LayerName = "_TARGETLINE_" });
            }

            // 重建 ObservableCollection 以触发 HalcanCanvasControl.Entities DP 回调 → RenderEntities()
            RebuildCanvasDisplayEntities();
        }

        /// <summary>重建 CanvasDisplayEntities 集合（DXF图元 + 叠加标记），触发 HalcanCanvasControl.Entities DP 回调</summary>
        private void RebuildCanvasDisplayEntities()
        {
            var newList = new ObservableCollection<CadEntity>();
            if (CadEntities != null)
                foreach (var e in CadEntities)
                    // ❌ 过滤掉DLS拟合椭圆实体（已禁用，避免显示偏移的蓝色圆弧）
                    if (e.Id != "FITTED_ELLIPSE_DLS")
                        newList.Add(e);
            foreach (var m in _alignmentMarkers)
                newList.Add(m);
            CanvasDisplayEntities = newList; // 必须赋值给属性而非字段，触发 SetProperty → PropertyChanged → 绑定刷新
        }

        /// <summary>HalconCanvas 点击回调：仿射CAD取点 / 基准·目标线段点位选取</summary>
        public void OnCanvasPointClicked(double cadX, double cadY)
        {
            // 仿射标定：直接使用点击处的 CAD 坐标（与 Step4AlignPanel 一致）
            if (_isPickingAffineCadCoord && _selectedAffineCalibrationPoint != null)
            {
                _selectedAffineCalibrationPoint.CadX = Math.Round(cadX, 3);
                _selectedAffineCalibrationPoint.CadY = Math.Round(cadY, 3);
                _isPickingAffineCadCoord = false;
                StatusMessage = string.Format(L("Step4_Status_PickedAffineCad"),
                    _selectedAffineCalibrationPoint.Name, cadX, cadY);
                return;
            }

            if (!_isPickingBaseline && !_isPickingTarget) return;
            if (ImportedCadPoints.Count == 0) return;

            int nearestIdx = FindNearestPointIndex(cadX, cadY);
            if (nearestIdx < 0)
            {
                StatusMessage = L("CAD_Click_Miss");
                CadPickStatus = L("CAD_Click_Miss_Status");
                return;
            }

            var point = ImportedCadPoints[nearestIdx];

            CadSelectedSegmentPoints = new List<CadPoint> { point };
            CadSelectedPointIndex = 0;

            OnCadPointSelected(point);
        }

        /// <summary>找到距离指定坐标最近的点位索引（容差根据图形尺寸动态调整）</summary>
        private int FindNearestPointIndex(double x, double y)
        {
            int nearestIdx = -1;
            double minDistSq = double.MaxValue;
            for (int i = 0; i < ImportedCadPoints.Count; i++)
            {
                var pt = ImportedCadPoints[i];
                double dx = pt.X - x;
                double dy = pt.Y - y;
                double distSq = dx * dx + dy * dy;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearestIdx = i;
                }
            }
            double tolerance = CalcDynamicMarkSize() * 5;
            return Math.Sqrt(minDistSq) < tolerance ? nearestIdx : -1;
        }

        /// <summary>根据点位分布范围动态计算标记大小（包围盒对角线的2%）</summary>
        private double CalcDynamicMarkSize()
        {
            if (ImportedCadPoints == null || ImportedCadPoints.Count == 0) return 3.0;

            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            foreach (var pt in ImportedCadPoints)
            {
                if (pt.X < minX) minX = pt.X;
                if (pt.X > maxX) maxX = pt.X;
                if (pt.Y < minY) minY = pt.Y;
                if (pt.Y > maxY) maxY = pt.Y;
            }
            double diagonal = Math.Sqrt((maxX - minX) * (maxX - minX) + (maxY - minY) * (maxY - minY));
            return Math.Max(diagonal * 0.02, 1.0); // 至少1单位
        }

        /// <summary>
        /// 更新所有导入CAD点的机械坐标：
        /// - 仿射模式: 先仿射变换得到机械坐标，再绕回转中心旋转θ
        /// - 1点平移模式: 使用简单偏移 Mx = Cx + ΔX
        /// </summary>
        private void UpdateMachineCoordinates()
        {
            // 未完成步骤2：清空所有坐标
            if (!Step2Done)
            {
                foreach (var pt in ImportedCadPoints)
                {
                    pt.OffsetX = null;
                    pt.OffsetY = null;
                    pt.MachineX = null;
                    pt.MachineY = null;
                }
                return;
            }

            if (_useAffineCalibration && _affineResult != null)
            {
                // 仿射模式: 先仿射变换得到机械坐标，再绕回转中心旋转θ
                double thetaRad = EffectiveThetaDeg * Math.PI / 180.0;
                double cosT = Math.Cos(thetaRad);
                double sinT = Math.Sin(thetaRad);

                foreach (var pt in ImportedCadPoints)
                {
                    var (mx, my) = AffineCalibrationService.Transform(_affineResult, pt.X, pt.Y);
                    double dx = mx - Mox;
                    double dy = my - Moy;
                    double edx = Ex(dx), edy = Ey(dy);
                    double rotX = edx * cosT - edy * sinT + Mox;
                    double rotY = edx * sinT + edy * cosT + Moy;
                    pt.OffsetX = Math.Round(rotX, 3);
                    pt.OffsetY = Math.Round(rotY, 3);
                    pt.MachineX = Math.Round(rotX, 3);
                    pt.MachineY = Math.Round(rotY, 3);
                }
                return;
            }

            // 1点平移模式
            foreach (var pt in ImportedCadPoints)
            {
                pt.OffsetX = Math.Round(pt.X + DeltaX, 3);
                pt.OffsetY = Math.Round(pt.Y + DeltaY, 3);
                pt.MachineX = Math.Round(pt.X + DeltaX, 3);
                pt.MachineY = Math.Round(pt.Y + DeltaY, 3);
            }
        }

        /// <summary>CAD→图像坐标转换偏移量（由View在FitToAll后回调设置）</summary>
        private double _cadToImageOffsetX;
        private double _cadToImageOffsetY;

        /// <summary>View回调：设置CAD→图像坐标转换偏移量，并更新所有点位的图像坐标</summary>
        public void SetCadToImageOffset(double offsetX, double offsetY)
        {
            _cadToImageOffsetX = offsetX;
            _cadToImageOffsetY = offsetY;
            UpdateImageCoordinates();
        }

        /// <summary>更新所有导入点位的Halcon图像像素坐标</summary>
        private void UpdateImageCoordinates()
        {
            foreach (var pt in ImportedCadPoints)
            {
                pt.ImageCol = Math.Round(pt.X - _cadToImageOffsetX, 1);
                pt.ImageRow = Math.Round(-pt.Y + _cadToImageOffsetY, 1);
            }
        }

        /// <summary>显示基准线段：高亮X标记并聚焦视口到基准线段区域</summary>
        private void OnShowBaselineSegment()
        {
            if (BaseStartIndex < 0 || BaseEndIndex < 0 ||
                BaseStartIndex >= ImportedCadPoints.Count || BaseEndIndex >= ImportedCadPoints.Count) return;

            var p1 = ImportedCadPoints[BaseStartIndex];
            var p2 = ImportedCadPoints[BaseEndIndex];

            CadSelectedSegmentPoints = new List<CadPoint> { p1, p2 };
            CadSelectedPointIndex = 1;

            FitToSegmentRequested?.Invoke(p1.X, p1.Y, p2.X, p2.Y);
            StatusMessage = string.Format(L("CAD_ShowBaseline_Status"), p1.Id, p1.X.ToString("F1"), p1.Y.ToString("F1"), p2.Id, p2.X.ToString("F1"), p2.Y.ToString("F1"), AlphaBaseDeg.ToString("F2"));
        }

        /// <summary>显示目标线段：高亮X标记并聚焦视口到目标线段区域</summary>
        private void OnShowTargetlineSegment()
        {
            if (TargetStartIndex < 0 || TargetEndIndex < 0 ||
                TargetStartIndex >= ImportedCadPoints.Count || TargetEndIndex >= ImportedCadPoints.Count) return;

            var p3 = ImportedCadPoints[TargetStartIndex];
            var p4 = ImportedCadPoints[TargetEndIndex];

            CadSelectedSegmentPoints = new List<CadPoint> { p3, p4 };
            CadSelectedPointIndex = 1;

            FitToSegmentRequested?.Invoke(p3.X, p3.Y, p4.X, p4.Y);
            StatusMessage = string.Format(L("CAD_ShowTarget_Status"), p3.Id, p3.X.ToString("F1"), p3.Y.ToString("F1"), p4.Id, p4.X.ToString("F1"), p4.Y.ToString("F1"), AlphaTargetDeg.ToString("F2"));
        }

        /// <summary>基于点位分布智能推荐基准/目标线段（最长线段策略+最大夹角策略）</summary>
        private void OnAutoRecommendLines()
        {
            if (ImportedCadPoints.Count < 4)
            {
                StatusMessage = L("CAD_SmartRecommend_Fail");
                CadPickStatus = L("CAD_SmartRecommend_NotEnough");
                return;
            }

            // 策略1: 找最长线段作为基准（通常代表主要特征边）
            var (i1, i2) = FindLongestSegment();
            // 策略2: 找与基准线段夹角最大的线段作为目标
            var (i3, i4) = FindBestTargetSegment(i1, i2);

            BaseStartIndex = i1;
            BaseEndIndex = i2;
            TargetStartIndex = i3;
            TargetEndIndex = i4;
            UpdateCadPointRoles();

            // 自动计算方向角
            CadPoint bp1 = null, bp2 = null, tp3 = null, tp4 = null;
            if (BaseEndIndex >= 0 && BaseStartIndex >= 0 && BaseEndIndex < ImportedCadPoints.Count && BaseStartIndex < ImportedCadPoints.Count)
            {
                bp1 = ImportedCadPoints[BaseStartIndex];
                bp2 = ImportedCadPoints[BaseEndIndex];
                AlphaBaseDeg = Math.Atan2(bp2.Y - bp1.Y, bp2.X - bp1.X) * 180 / Math.PI;
                RaisePropertyChanged(nameof(AlphaBaseDeg));
            }
            if (TargetEndIndex >= 0 && TargetStartIndex >= 0 && TargetEndIndex < ImportedCadPoints.Count && TargetStartIndex < ImportedCadPoints.Count)
            {
                tp3 = ImportedCadPoints[TargetStartIndex];
                tp4 = ImportedCadPoints[TargetEndIndex];
                AlphaTargetDeg = Math.Atan2(tp4.Y - tp3.Y, tp4.X - tp3.X) * 180 / Math.PI;
                RaisePropertyChanged(nameof(AlphaTargetDeg));
            }

            CadPickStatus = string.Format(L("CAD_AutoRecommend_Done"), bp1?.Id ?? "?", bp2?.Id ?? "?", tp3?.Id ?? "?", tp4?.Id ?? "?");
            StatusMessage = L("CAD_AutoRecommend_Confirm");
        }

        /// <summary>找出距离最远的点对（最长线段）</summary>
        private (int, int) FindLongestSegment()
        {
            int maxI = -1, maxJ = -1;
            double maxDistSq = 0;
            for (int i = 0; i < ImportedCadPoints.Count; i++)
                for (int j = i + 1; j < ImportedCadPoints.Count; j++)
                {
                    var dx = ImportedCadPoints[j].X - ImportedCadPoints[i].X;
                    var dy = ImportedCadPoints[j].Y - ImportedCadPoints[i].Y;
                    double distSq = dx * dx + dy * dy;
                    if (distSq > maxDistSq) { maxDistSq = distSq; maxI = i; maxJ = j; }
                }
            return (maxI, maxJ);
        }

        /// <summary>找出与给定基准线段夹角最接近90°的线段作为目标</summary>
        private (int, int) FindBestTargetSegment(int baseI, int baseJ)
        {
            if (baseI < 0 || baseJ < 0) return FindLongestSegment();

            var bp1 = ImportedCadPoints[baseI];
            var bp2 = ImportedCadPoints[baseJ];
            double baseAngle = Math.Atan2(bp2.Y - bp1.Y, bp2.X - bp1.X);

            int bestI = -1, bestJ = -1;
            double bestScore = double.MinValue;

            for (int i = 0; i < ImportedCadPoints.Count; i++)
                for (int j = i + 1; j < ImportedCadPoints.Count; j++)
                {
                    if (i == baseI || i == baseJ || j == baseI || j == baseJ) continue;

                    var tp1 = ImportedCadPoints[i];
                    var tp2 = ImportedCadPoints[j];
                    double targetAngle = Math.Atan2(tp2.Y - tp1.Y, tp2.X - tp1.X);
                    double angleDiff = Math.Abs(targetAngle - baseAngle);
                    // 归一化到 [0, π]
                    while (angleDiff > Math.PI) angleDiff -= 2 * Math.PI;
                    while (angleDiff < -Math.PI) angleDiff += 2 * Math.PI;
                    angleDiff = Math.Abs(angleDiff);

                    // 偏好夹角接近 60°~120° 的线段（典型垂直/倾斜特征）
                    double score = Math.Abs(angleDiff - Math.PI / 2);
                    if (score < bestScore) { bestScore = score; bestI = i; bestJ = j; }
                }

            // 如果没找到合适的，回退到第二长线段
            if (bestI < 0) return FindLongestSegment();
            return (bestI, bestJ);
        }

        #endregion

        /// <summary>一键继承步骤3中选中的目标点对到步骤4的变换目标</summary>
        private void OnInheritTargetFromStep3()
        {
            if (!CanInheritFromStep3)
            {
                StatusMessage = L("CAD_NeedStep3_First");
                return;
            }

            if (HasCadDrawingLoaded && TargetStartIndex >= 0 && TargetStartIndex < ImportedCadPoints.Count)
            {
                var pt = ImportedCadPoints[TargetStartIndex];

                double xm, ym;
                if (_useAffineCalibration && _affineResult != null)
                {
                    // 仿射模式: 使用矩阵运算
                    var (mx, my) = AffineCalibrationService.Transform(_affineResult, pt.X, pt.Y);
                    xm = mx;
                    ym = my;
                }
                else
                {
                    xm = pt.X + DeltaX;
                    ym = pt.Y + DeltaY;
                }

                Step4TargetCadText = string.Format("#{0} ({1}, {2})", pt.Id, pt.X.ToString("F2"), pt.Y.ToString("F2"));
                Step4TargetOffsetText = string.Format("({0}, {1})", xm.ToString("F2"), ym.ToString("F2"));

                TransXm = xm;
                TransYm = ym;
                _useStep3TargetForTransform = true;

                StatusMessage = string.Format(L("CAD_Inherit_Step3_Success"), pt.Id, pt.X.ToString("F2"), pt.Y.ToString("F2"));
            }
            else if (TargetPairIndex * 2 < CorrespondencePoints.Count)
            {
                int targetPointStartIdx = TargetPairIndex * 2;
                TransformSelectedIndex = targetPointStartIdx;

                var pt = CorrespondencePoints[targetPointStartIdx];

                if (_useAffineCalibration && _affineResult != null)
                {
                    var (mx, my) = AffineCalibrationService.Transform(_affineResult, pt.CadX, pt.CadY);
                    TransXm = mx;
                    TransYm = my;
                }
                else
                {
                    TransXm = pt.CadX + DeltaX;
                    TransYm = pt.CadY + DeltaY;
                }

                Step4TargetCadText = string.Format("{0} ({1}, {2})", pt.Name, pt.CadX.ToString("F2"), pt.CadY.ToString("F2"));
                Step4TargetOffsetText = string.Format("({0}, {1})", TransXm.ToString("F2"), TransYm.ToString("F2"));
                _useStep3TargetForTransform = false;

                StatusMessage = $"已继承步骤3目标点 {pt.Name}（索引={targetPointStartIdx}）";
            }
        }

        #region 核心4：单点坐标变换（先平移后旋转）

        /// <summary>
        /// 对选中索引对应的CAD点执行坐标变换：
        /// - 仿射模式: ①仿射变换得到机械坐标 ②绕回转中心(Mox,Moy)旋转θ得到最终结果
        /// - 1点平移模式: ①平移得到机械坐标 ②绕回转中心(Mox,Moy)旋转θ得到最终结果
        ///    X_new = dx·cosθ - dy·sinθ + Mox
        ///    Y_new = dx·sinθ + dy·cosθ + Moy
        /// 同时将结果写入选中点的 RotatedX/Y 属性
        /// </summary>
        private void ExecuteTransform()
        {
            if (!Step2Done)
            {
                StatusMessage = L("CAD_NeedStep2_First");
                return;
            }

            double cadX, cadY;
            string pointName;

            if (_useStep3TargetForTransform && TargetStartIndex >= 0 && TargetStartIndex < ImportedCadPoints.Count)
            {
                var pt = ImportedCadPoints[TargetStartIndex];
                pointName = $"#{pt.Id}";
                cadX = pt.X;
                cadY = pt.Y;
            }
            else
            {
                if (TransformSelectedIndex < 0 || TransformSelectedIndex >= CorrespondencePoints.Count)
                {
                    StatusMessage = L("CAD_Transform_IndexOutOfRange");
                    return;
                }

                var cp = CorrespondencePoints[TransformSelectedIndex];
                pointName = cp.Name;
                cadX = cp.CadX;
                cadY = cp.CadY;
            }

            // 仿射模式: 先仿射变换得到机械坐标，再绕回转中心旋转θ
            if (_useAffineCalibration && _affineResult != null)
            {
                // ① 仿射变换 → 机械坐标
                var (mx, my) = AffineCalibrationService.Transform(_affineResult, cadX, cadY);
                TransXm = Math.Round(mx, 3);
                TransYm = Math.Round(my, 3);

                // ② 相对回转中心偏移
                double dx = mx - Mox;
                double dy = my - Moy;
                TransDx = Math.Round(dx, 3);
                TransDy = Math.Round(dy, 3);

                // ③ 绕回转中心旋转θ角得到最终结果
                double thetaRad = EffectiveThetaDeg * Math.PI / 180.0;
                double cosT = Math.Cos(thetaRad);
                double sinT = Math.Sin(thetaRad);
                double edx = Ex(dx), edy = Ey(dy);
                TransResultX = edx * cosT - edy * sinT + Mox;
                TransResultY = edx * sinT + edy * cosT + Moy;

                // 【调试日志】输出所有中间计算值
                System.Diagnostics.Debug.WriteLine($"=== 坐标变换调试 ===");
                System.Diagnostics.Debug.WriteLine($"输入CAD坐标: ({cadX}, {cadY})");
                System.Diagnostics.Debug.WriteLine($"仿射参数: A={_affineResult.A:F6}, B={_affineResult.B:F6}, C={_affineResult.C:F6}, D={_affineResult.D:F6}, Tx={_affineResult.Tx:F6}, Ty={_affineResult.Ty:F6}");
                System.Diagnostics.Debug.WriteLine($"仿射变换后机械坐标: mx={mx:F6}, my={my:F6}");
                System.Diagnostics.Debug.WriteLine($"回转中心: Mox={Mox:F6}, Moy={Moy:F6}");
                System.Diagnostics.Debug.WriteLine($"相对偏移: dx={dx:F6}, dy={dy:F6}");
                System.Diagnostics.Debug.WriteLine($"取反开关: X={InvertXAngle}, Y={InvertYAngle}, θ={InvertThetaAngle}");
                System.Diagnostics.Debug.WriteLine($"取反后偏移: edx={edx:F6}, edy={edy:F6}");
                System.Diagnostics.Debug.WriteLine($"旋转角度: ThetaDeg={ThetaDeg:F6}, EffectiveThetaDeg={EffectiveThetaDeg:F6}");
                System.Diagnostics.Debug.WriteLine($"cosθ={cosT:F6}, sinθ={sinT:F6}");
                System.Diagnostics.Debug.WriteLine($"最终结果: X={TransResultX:F6}, Y={TransResultY:F6}");
                System.Diagnostics.Debug.WriteLine($"===================");
            }
            else
            {
                // 1点平移模式: 先平移后旋转
                double xm = cadX + DeltaX;
                double ym = cadY + DeltaY;
                double dx = xm - Mox;
                double dy = ym - Moy;
                double thetaRad = EffectiveThetaDeg * Math.PI / 180.0;
                double cosT = Math.Cos(thetaRad);
                double sinT = Math.Sin(thetaRad);
                double edx = Ex(dx), edy = Ey(dy);

                TransXm = xm;
                TransYm = ym;
                TransDx = dx;
                TransDy = dy;
                TransResultX = edx * cosT - edy * sinT + Mox;
                TransResultY = edx * sinT + edy * cosT + Moy;
            }

            if (_useStep3TargetForTransform && TargetStartIndex >= 0 && TargetStartIndex < ImportedCadPoints.Count)
            {
                ImportedCadPoints[TargetStartIndex].MachineX = Math.Round(TransResultX, 3);
                ImportedCadPoints[TargetStartIndex].MachineY = Math.Round(TransResultY, 3);
            }
            else
            {
                var cp = CorrespondencePoints[TransformSelectedIndex];
                cp.RotatedX = TransResultX;
                cp.RotatedY = TransResultY;
                cp.RotatedZ = cp.CadZ;
            }
            Step4Done = true;
            StatusMessage = string.Format(L("CAD_Single_Transform_Done"), pointName, TransResultX.ToString("F3"), TransResultY.ToString("F3"));
        }

        private void OnExecuteTransform() => ExecuteTransform();

        #endregion

        #region 核心4-扩展：批量坐标变换

        /// <summary>
        /// 批量坐标变换：遍历CorrespondencePoints中索引>=2的所有点(P3~P6)
        /// - 仿射模式: ①仿射变换得到机械坐标 ②绕回转中心旋转θ
        /// - 1点平移模式: ①平移得到机械坐标 ②绕回转中心旋转θ
        /// 将结果写入每个点的RotatedX/Y/Z属性
        /// </summary>
        private void ExecuteBatchTransform()
        {
            if (CorrespondencePoints == null || CorrespondencePoints.Count < 3)
            {
                StatusMessage = L("CAD_BatchTransform_NotEnough");
                return;
            }

            int transformedCount = 0;

            if (_useAffineCalibration && _affineResult != null)
            {
                // 仿射模式: 先仿射变换得到机械坐标，再绕回转中心旋转θ
                double thetaRad = EffectiveThetaDeg * Math.PI / 180.0;
                double cosT = Math.Cos(thetaRad);
                double sinT = Math.Sin(thetaRad);

                for (int i = 2; i < CorrespondencePoints.Count; i++)
                {
                    var cp = CorrespondencePoints[i];
                    var (mx, my) = AffineCalibrationService.Transform(_affineResult, cp.CadX, cp.CadY);
                    double dx = mx - Mox;
                    double dy = my - Moy;
                    double edx = Ex(dx), edy = Ey(dy);
                    cp.RotatedX = edx * cosT - edy * sinT + Mox;
                    cp.RotatedY = edx * sinT + edy * cosT + Moy;
                    cp.RotatedZ = cp.CadZ;
                    transformedCount++;
                }
            }
            else
            {
                // 1点平移模式: 先平移后旋转
                double thetaRad = EffectiveThetaDeg * Math.PI / 180.0;
                double cosT = Math.Cos(thetaRad);
                double sinT = Math.Sin(thetaRad);

                for (int i = 2; i < CorrespondencePoints.Count; i++)
                {
                    var cp = CorrespondencePoints[i];
                    double xm = cp.CadX + DeltaX;
                    double ym = cp.CadY + DeltaY;
                    double dx = xm - Mox;
                    double dy = ym - Moy;
                    double edx = Ex(dx), edy = Ey(dy);

                    cp.RotatedX = edx * cosT - edy * sinT + Mox;
                    cp.RotatedY = edx * sinT + edy * cosT + Moy;
                    cp.RotatedZ = cp.CadZ;
                    transformedCount++;
                }
            }

            Step4Done = true;
            StatusMessage = $"批量坐标变换完成：共变换 {transformedCount} 个点（P3~P{2 + transformedCount - 1}）";
        }

        private void OnExecuteBatchTransform() => ExecuteBatchTransform();

        /// <summary>
        /// 根据 CorrespondencePoints 动态生成点对名称和点位名称
        /// 点对: P1→P2, P3→P4, P5→P6 (连续两个点为一对)
        /// 点位: P1(P3), P2(P4)... (用于坐标变换选择目标点位)
        /// </summary>
        private void RefreshPointPairNames()
        {
            var pairList = new List<string>();
            var pointList = new List<string>();

            for (int i = 0; i < CorrespondencePoints.Count; i += 2)
            {
                int p1 = i + 1;
                int p2 = i + 2;
                if (p2 <= CorrespondencePoints.Count)
                {
                    pairList.Add($"P{p1}→P{p2}");
                }
                pointList.Add($"P{p1}(P{p1 + 2})");
            }

            PairNames = pairList;
            PointNames = pointList;
        }

        #endregion

        #region 核心5：夹爪定位计算

        /// <summary>读取夹爪当前机械坐标作为示教基准</summary>
        private void OnTeachGripperPosition()
        {
            try
            {
                var motionService = _containerProvider.Resolve<IMotionService>();
                TeachX = Math.Round(motionService.GetAxisPosition(0), 3);
                TeachY = Math.Round(motionService.GetAxisPosition(1), 3);
                TeachRy = Math.Round(motionService.GetAxisPosition(2), 4);
                TeachZ = Math.Round(motionService.GetAxisPosition(3), 3);
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(L("CAD_Teach_Failed"), ex.Message);
                return;
            }

            RaisePropertyChanged(nameof(CalcOffX));
            RaisePropertyChanged(nameof(CalcOffY));

            StatusMessage = string.Format(L("CAD_Teach_Success"),
                TeachX.ToString("F3"), TeachY.ToString("F3"),
                TeachRy.ToString("F4"), TeachZ.ToString("F3"));
        }

        /// <summary>应用计算偏移量到固定偏移</summary>
        private void OnApplyCalcOffset()
        {
            if (TransResultX == 0 || TransResultY == 0)
            {
                StatusMessage = L("CAD_NeedStep4_First");
                return;
            }

            GripperOffX = CalcOffX;
            GripperOffY = CalcOffY;
            UseCalculatedOffset = true;

            StatusMessage = string.Format(L("CAD_Apply_Calc_Offset"),
                CalcOffX.ToString("F3"), CalcOffY.ToString("F3"));
        }

        /// <summary>
        /// 在变换结果基础上叠加夹爪偏移量，得到最终夹爪目标位置
        /// FinalGripperX = TransResultX + GripperOffX
        /// FinalGripperY = TransResultY + GripperOffY
        /// </summary>
        private void ComputeGripperPosition()
        {
            if (UseCalculatedOffset)
            {
                StatusMessage = L("CAD_OffsetMode_Calc");
            }
            else
            {
                StatusMessage = L("CAD_OffsetMode_Fixed");
            }

            FinalGripperX = TransResultX + GripperOffX;
            FinalGripperY = TransResultY + GripperOffY;
            Step5Done = true;
            (WriteToGlobalVariablesCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            StatusMessage = string.Format(L("CAD_Gripper_Position_Done"),
                FinalGripperX.ToString("F3"), FinalGripperY.ToString("F3"),
                GripperOffX.ToString("F1"), GripperOffY.ToString("F1"));
        }

        private void OnComputeGripperPosition() => ComputeGripperPosition();

        /// <summary>异步加载全局变量列表供ComboBox选取</summary>
        private async Task LoadAvailableGlobalVariablesAsync()
        {
            try
            {
                var poolId = _recipePoolService.CurrentPoolName ?? "Default";
                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
                AvailableGlobalVariables.Clear();
                foreach (var v in variables)
                    AvailableGlobalVariables.Add(v);
            }
            catch { /* 加载失败不影响主流程 */ }
        }

        /// <summary>将夹爪最终位置写入用户指定的全局变量（点击按钮后执行）</summary>
        private async void OnWriteToGlobalVariables()
        {
            try
            {
                var poolId = _recipePoolService.CurrentPoolName ?? "Default";
                var variables = (await _recipePoolService.LoadGlobalVariablesAsync(poolId)).ToList();

                // 优先使用新5步流程的值（GripperFinalX/Y），否则使用旧流程的值（FinalGripperX/Y）
                double writeX = GripperFinalX != 0 ? GripperFinalX : FinalGripperX;
                double writeY = GripperFinalY != 0 ? GripperFinalY : FinalGripperY;
                double writeZ = GripperFinalZ;

                var vx = string.IsNullOrWhiteSpace(FinalGripperXLinkedVar) ? "GripperFinalX" : FinalGripperXLinkedVar.Trim();
                var vy = string.IsNullOrWhiteSpace(FinalGripperYLinkedVar) ? "GripperFinalY" : FinalGripperYLinkedVar.Trim();
                var vz = string.IsNullOrWhiteSpace(FinalGripperZLinkedVar) ? "GripperFinalZ" : FinalGripperZLinkedVar.Trim();

                UpdateOrAddGlobalVariable(variables, vx, writeX.ToString("F3"), "夹爪最终位置X");
                UpdateOrAddGlobalVariable(variables, vy, writeY.ToString("F3"), "夹爪最终位置Y");
                UpdateOrAddGlobalVariable(variables, vz, writeZ.ToString("F3"), "夹爪最终位置Z");

                // 如果对齐角度已链接全局变量，同步写入当前角度值
                if (IsAlignmentAngleLinked && !string.IsNullOrWhiteSpace(AlignmentAngleLinkedVar))
                {
                    UpdateOrAddGlobalVariable(variables, AlignmentAngleLinkedVar.Trim(),
                        AlignmentAngle.ToString("F3"), "CAD对齐角度");
                }

                for (int i = 0; i < variables.Count; i++)
                    variables[i].Index = i + 1;

                await _recipePoolService.SaveGlobalVariablesAsync(poolId, variables);

                // 发布全局变量变更事件，通知全局变量页面及其他订阅者同步刷新
                _eventAggregator?.GetEvent<GlobalVariablesChangedEvent>()?.Publish(poolId);

                // 同步更新本地全局变量下拉列表
                await LoadAvailableGlobalVariablesAsync();

                StatusMessage = string.Format(L("CAD_Write_Global_Var_Success"),
                    vx, writeX.ToString("F3"), vy, writeY.ToString("F3"));
            }
            catch (Exception ex)
            {
                StatusMessage = $"写入全局变量失败: {ex.Message}";
            }
        }

        /// <summary>外部全局变量变更时重新加载本地下拉列表，保持同步</summary>
        private async void OnGlobalVariablesChanged(string poolId)
        {
            await LoadAvailableGlobalVariablesAsync();
        }

        /// <summary>更新或添加全局变量（存在则更新值，不存在则新增）</summary>
        private void UpdateOrAddGlobalVariable(List<GlobalVariable> variables, string name, string value, string comment)
        {
            var existing = variables.FirstOrDefault(v => v.Name == name);
            if (existing != null)
            {
                existing.Value = value;
            }
            else
            {
                variables.Add(new GlobalVariable
                {
                    Name = name,
                    Type = GlobalVariableType.Double,
                    Value = value,
                    Comment = comment
                });
            }
        }

        #endregion

        #region 运动控制——移动目标角度 / 移动目标位

        /// <summary>
        /// 移动目标角度：将产品旋转角度下发到Rz轴（旋转轴）
        /// </summary>
        private async Task OnMoveTargetAngleAsync()
        {
            try
            {
                var motionService = _containerProvider.Resolve<IMotionService>();
                var axisConfigs = motionService.GetAxisConfigurations();
                var ryConfig = axisConfigs.FirstOrDefault(a => a.Name == "Rz");
                if (ryConfig == null)
                {
                    StatusMessage = L("CAD_Move_Axis_Failed") + ": Rz轴未找到";
                    return;
                }

                double targetAngle = ProductRotationAngle;
                await motionService.MoveAbsAsync(ryConfig.LogicalId, targetAngle, 10.0);
                StatusMessage = $"Ry轴已移动到目标角度: {targetAngle:F3}°";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{L("CAD_Move_Axis_Failed")}: {ex.Message}";
            }
        }

        /// <summary>链接对齐角度到全局变量（从全局变量列表中读取值填充AlignmentAngle）</summary>
        private void OnLinkAlignmentAngle()
        {
            if (string.IsNullOrWhiteSpace(AlignmentAngleLinkedVar))
            {
                StatusMessage = "请先选择要链接的全局变量";
                return;
            }

            var gv = AvailableGlobalVariables.FirstOrDefault(v =>
                string.Equals(v.Name, AlignmentAngleLinkedVar, StringComparison.OrdinalIgnoreCase));
            if (gv != null && double.TryParse(gv.Value, out double val))
            {
                AlignmentAngle = val;
                IsAlignmentAngleLinked = true;
                StatusMessage = $"对齐角度已从全局变量 [{gv.Name}] 读取: {val:F3}°";
            }
            else
            {
                StatusMessage = $"全局变量 [{AlignmentAngleLinkedVar}] 未找到或值无效";
            }
        }

        /// <summary>
        /// 移动目标位：先弹出Z轴安全确认，再将Dx/Dy移动到变换结果坐标
        /// </summary>
        private async Task OnMoveTargetPositionAsync()
        {
            // Z轴安全确认
            var result = System.Windows.MessageBox.Show(
                L("CAD_Z_Safety_Warning"),
                "Z轴安全确认",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes)
                return;

            try
            {
                var motionService = _containerProvider.Resolve<IMotionService>();
                var axisConfigs = motionService.GetAxisConfigurations();
                var dxConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dx");
                var dyConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dy");
                if (dxConfig == null || dyConfig == null)
                {
                    StatusMessage = L("CAD_Move_Axis_Failed") + ": Dx/Dy轴未找到";
                    return;
                }

                // 同时移动Dx/Dy两轴
                var t1 = motionService.MoveAbsAsync(dxConfig.LogicalId, TransResultX, 10.0);
                var t2 = motionService.MoveAbsAsync(dyConfig.LogicalId, TransResultY, 10.0);
                await Task.WhenAll(t1, t2);

                StatusMessage = $"Dx/Dy已移动到目标位: X={TransResultX:F3}, Y={TransResultY:F3}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"{L("CAD_Move_Axis_Failed")}: {ex.Message}";
            }
        }

        /// <summary>Z轴抬升安全高度默认值(mm)</summary>
        private const double ZSafeHeightDefault = 30.0;

        /// <summary>
        /// 移动轴到拟合点坐标：弹出Z轴抬升确认后，Dx/Dy插补运动到 FitX/FitY
        /// </summary>
        private async Task OnMoveFitPointAsync(FitPoint fp)
        {
            if (fp == null) return;
            await MoveToTargetWithZPromptAsync(fp.FitX, fp.FitY,
                string.Format(L("CAD_Move_FitPoint_Done"), fp.AngleLabel, fp.FitX.ToString("F3"), fp.FitY.ToString("F3")));
        }

        /// <summary>
        /// 移动轴到仿射标定点坐标：弹出Z轴抬升确认后，Dx/Dy插补运动到 MachineX/MachineY
        /// </summary>
        private async Task OnMoveAffineCalibrationPointAsync(AffineCalibrationPoint pt)
        {
            if (pt == null) return;
            await MoveToTargetWithZPromptAsync(pt.MachineX, pt.MachineY,
                string.Format(L("CAD_Move_CalibPoint_Done"), pt.Name, pt.MachineX.ToString("F3"), pt.MachineY.ToString("F3")));
        }

        /// <summary>
        /// 通用移动流程：弹出Z轴抬升确认 → 可选抬升Z → Dx/Dy插补移动到目标位置
        /// 对话框：是=抬升Z轴  否=直接移动XY  取消=不执行
        /// </summary>
        private async Task MoveToTargetWithZPromptAsync(double targetX, double targetY, string doneMessage)
        {
            // Z轴抬升确认对话框
            var result = System.Windows.MessageBox.Show(
                L("CAD_Move_RaiseZ_Message"),
                L("CAD_Move_RaiseZ_Title"),
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Question);

            // 取消=不执行任何操作
            if (result == System.Windows.MessageBoxResult.Cancel)
                return;

            try
            {
                var motionService = _containerProvider.Resolve<IMotionService>();
                var axisConfigs = motionService.GetAxisConfigurations();
                var dxConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dx");
                var dyConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dy");
                if (dxConfig == null || dyConfig == null)
                {
                    StatusMessage = L("CAD_Move_AxisNotFound_DxDy");
                    return;
                }

                // 用户选择"是"：先抬升Z轴到安全高度
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    var zConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dz₃");
                    if (zConfig != null)
                    {
                        await motionService.MoveAbsAsync(zConfig.LogicalId, ZSafeHeightDefault, 10.0);
                        StatusMessage = string.Format(L("CAD_Move_RaiseZ_Done"), ZSafeHeightDefault);
                    }
                    else
                    {
                        StatusMessage = L("CAD_Move_ZAxisNotFound");
                    }
                }

                // Dx/Dy 插补运动（使用直线插补保证两轴同步）
                int coordId = ResolveDxDyCoordId(motionService);
                await motionService.MoveLineAbsAsync(
                    coordId,
                    new[] { dxConfig.LogicalId, dyConfig.LogicalId },
                    new[] { targetX, targetY },
                    10.0);

                StatusMessage = doneMessage;
            }
            catch (Exception ex)
            {
                StatusMessage = $"{L("CAD_Move_Axis_Failed")}: {ex.Message}";
            }
        }

        /// <summary>
        /// 解析 Dx/Dy 所在的插补坐标系 CoordId（与 NeedleAlignerMotionService 一致）
        /// </summary>
        private int ResolveDxDyCoordId(IMotionService motionService)
        {
            var axisConfigs = motionService.GetAxisConfigurations();
            var dxConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dx");
            if (dxConfig == null) return 0;

            try
            {
                var axisParamService = _containerProvider.Resolve<IAxisParameterService>();
                foreach (var sys in axisParamService.LoadInterpolationSystems())
                {
                    foreach (var axisEntry in sys.Axes)
                    {
                        var parts = axisEntry.Split('-');
                        if (parts.Length == 2 && int.TryParse(parts[1], out int actAxisId)
                            && actAxisId == dxConfig.AxisId)
                        {
                            return sys.CoordId;
                        }
                    }
                }
            }
            catch
            {
                // IAxisParameterService 未注册时回退 0
            }
            return 0;
        }

        #endregion

        #region 5步夹爪定位流程

        /// <summary>步骤1：设置相机基准位（=最终变换结果TransResultX/Y + Dz₁轴当前位置）</summary>
        private void OnSetCameraRef()
        {
            try
            {
                var motionService = _containerProvider.Resolve<IMotionService>();
                var axisConfigs = motionService.GetAxisConfigurations();
                var dxConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dx");
                var dyConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dy");
                var zConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dz₃");
                if (dxConfig == null || dyConfig == null)
                {
                    StatusMessage = L("CAD_Move_Axis_Failed") + ": Dx/Dy轴未找到";
                    return;
                }
                CameraRefX = Math.Round(motionService.GetAxisPosition(dxConfig.LogicalId), 3);
                CameraRefY = Math.Round(motionService.GetAxisPosition(dyConfig.LogicalId), 3);
                if (zConfig != null)
                    CameraRefZ = Math.Round(motionService.GetAxisPosition(zConfig.LogicalId), 3);
            }
            catch { /*  */ }
            StatusMessage = $"相机基准位已设置: X={CameraRefX:F3}, Y={CameraRefY:F3}, Z={CameraRefZ:F3}";
        }

        /// <summary>步骤2：示教夹爪基准位（读取Dx/Dy/Z当前位置）</summary>
        private void OnTeachGripperRef()
        {
            try
            {
                var motionService = _containerProvider.Resolve<IMotionService>();
                var axisConfigs = motionService.GetAxisConfigurations();
                var dxConfig = axisConfigs.FirstOrDefault(a => a.Name == "X");
                var dyConfig = axisConfigs.FirstOrDefault(a => a.Name == "Y");
                var zConfig = axisConfigs.FirstOrDefault(a => a.Name == "Z");
                if (dxConfig == null || dyConfig == null)
                {
                    StatusMessage = L("CAD_Move_Axis_Failed") + ": X/Y轴未找到";
                    return;
                }

                GripperRefX = Math.Round(motionService.GetAxisPosition(dxConfig.LogicalId), 3);
                GripperRefY = Math.Round(motionService.GetAxisPosition(dyConfig.LogicalId), 3);
                if (zConfig != null)
                    GripperRefZ = Math.Round(motionService.GetAxisPosition(zConfig.LogicalId), 3);
                StatusMessage = $"夹爪基准位已示教: X={GripperRefX:F3}, Y={GripperRefY:F3}, Z={GripperRefZ:F3}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"示教失败: {ex.Message}";
            }
        }

        /// <summary>步骤3：计算相机偏移 = 当前相机位置 - 相机基准位</summary>
        private void OnCalcCameraOffset()
        {
            try
            {
                //var motionService = _containerProvider.Resolve<IMotionService>();
                //var axisConfigs = motionService.GetAxisConfigurations();
                //var dxConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dx");
                //var dyConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dy");
                //if (dxConfig == null || dyConfig == null)
                //{
                //    StatusMessage = L("CAD_Move_Axis_Failed") + ": Dx/Dy轴未找到";
                //    return;
                //}

                //double currentCamX = Math.Round(motionService.GetAxisPosition(dxConfig.LogicalId), 3);
                //double currentCamY = Math.Round(motionService.GetAxisPosition(dyConfig.LogicalId), 3);
                CameraOffsetX = Math.Round(TransResultX - CameraRefX, 3);
                CameraOffsetY = Math.Round(TransResultY - CameraRefY, 3);
                StatusMessage = $"相机偏移已计算: ΔX={CameraOffsetX:F3}, ΔY={CameraOffsetY:F3}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"计算失败: {ex.Message}";
            }
        }

        /// <summary>步骤4：夹爪最终位置 = 夹爪基准位 + 相机偏移，Z = 夹爪基准高度</summary>
        private void OnCalcGripperFinal()
        {
            GripperFinalX = Math.Round(GripperRefX - CameraOffsetX, 3);
            GripperFinalY = Math.Round(GripperRefY + CameraOffsetY, 3);
            GripperFinalZ = Math.Round(GripperRefZ, 3);
            Step5Done = true;
            (WriteToGlobalVariablesCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            StatusMessage = $"夹爪最终位置: X={GripperFinalX:F3}, Y={GripperFinalY:F3}, Z={GripperFinalZ:F3}";
        }

        #endregion

        #region 配置文件管理

        /// <summary>获取配置文件存储目录（不存在则自动创建）</summary>
        private static string GetConfigDirectory()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? System.IO.Directory.GetCurrentDirectory();
            var configDir = System.IO.Path.Combine(baseDir, "Config", "CadAlignment");
            if (!System.IO.Directory.Exists(configDir))
                System.IO.Directory.CreateDirectory(configDir);
            return configDir;
        }

        /// <summary>将当前对位配置保存为JSON文件</summary>
        private async Task SaveConfigToFileAsync()
        {
            try
            {
                var configDir = GetConfigDirectory();
                var fileName = $"CadAlignment_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                var filePath = System.IO.Path.Combine(configDir, fileName);

                var config = BuildCurrentConfig();
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                await System.IO.File.WriteAllTextAsync(filePath, json);

                CurrentFilePath = filePath;
                CurrentFileName = fileName;
                await SaveCurrentFileToRecipePoolAsync();

                StatusMessage = string.Format(L("CadAlignment_ConfigSaved"), CurrentFileName);
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(L("CadAlignment_ConfigSaveFail"), ex.Message);
            }
        }

        /// <summary>从文件选择对话框加载配置</summary>
        private async Task LoadConfigFromFileAsync()
        {
            try
            {
                var configDir = GetConfigDirectory();
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                    InitialDirectory = configDir
                };

                if (dialog.ShowDialog() != true) return;

                await LoadConfigFromPathAsync(dialog.FileName);
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(L("CadAlignment_ConfigLoadFail"), ex.Message);
            }
        }

        /// <summary>从指定路径加载配置文件并应用到当前状态</summary>
        private async Task LoadConfigFromPathAsync(string filePath)
        {
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var config = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (config == null) return;

            ApplyConfig(config);
            CurrentFilePath = filePath;
            CurrentFileName = System.IO.Path.GetFileName(filePath);
            StatusMessage = string.Format(L("CadAlignment_ConfigLoaded"), CurrentFileName);
        }

        /// <summary>构建当前配置的字典快照（用于序列化保存）</summary>
        private Dictionary<string, object> BuildCurrentConfig()
        {
            return new Dictionary<string, object>
            {
                // Step1 回转中心
                ["Mox"] = Mox,
                ["Moy"] = Moy,
                ["FitRadius"] = FitRadius,
                ["Step1Done"] = Step1Done,
                ["FitPoints"] = FitPoints.Select(p => new { p.Index, p.AngleLabel, p.FitX, p.FitY }).ToList(),

                // Step2 全局偏移
                ["P1Mx"] = P1Mx, ["P1My"] = P1My,
                ["P1Cx"] = P1Cx, ["P1Cy"] = P1Cy,
                ["DeltaX"] = DeltaX, ["DeltaY"] = DeltaY,
                ["Step2Done"] = Step2Done,
                ["UseAffineCalibration"] = UseAffineCalibration,
                ["AffineCalibrationPoints"] = AffineCalibrationPoints?.Select(p => new
                {
                    p.Index, p.Name, p.CadX, p.CadY, p.MachineX, p.MachineY
                }).ToList(),
                ["AffineA"] = AffineA, ["AffineB"] = AffineB,
                ["AffineC"] = AffineC, ["AffineD"] = AffineD,
                ["AffineTx"] = AffineTx, ["AffineTy"] = AffineTy,
                ["AffineRmsError"] = AffineRmsError,
                ["AffineRotDeg"] = AffineRotDeg,
                ["AffineQualityText"] = AffineQualityText,

                // Step3 旋转角度
                ["ThetaDeg"] = ThetaDeg,
                ["Step3Done"] = Step3Done,
                ["BaseStartIndex"] = BaseStartIndex,
                ["BaseEndIndex"] = BaseEndIndex,
                ["TargetStartIndex"] = TargetStartIndex,
                ["TargetEndIndex"] = TargetEndIndex,
                // 产品对齐角度（Tab3新增）
                ["AlignmentAngle"] = AlignmentAngle,
                ["IsAlignmentAngleLinked"] = IsAlignmentAngleLinked,
                ["AlignmentAngleLinkedVar"] = AlignmentAngleLinkedVar,

                // Step4 坐标变换
                ["TransResultX"] = TransResultX, ["TransResultY"] = TransResultY,
                ["Step4Done"] = Step4Done,
                ["TransformSelectedIndex"] = TransformSelectedIndex,
                ["TargetPairIndex"] = TargetPairIndex,

                // Step5 夹爪定位
                ["GripperOffX"] = GripperOffX, ["GripperOffY"] = GripperOffY,
                ["TeachX"] = TeachX, ["TeachY"] = TeachY, ["TeachRy"] = TeachRy, ["TeachZ"] = TeachZ,
                ["UseCalculatedOffset"] = UseCalculatedOffset,
                ["FinalGripperX"] = FinalGripperX, ["FinalGripperY"] = FinalGripperY,
                ["FinalGripperXLinkedVar"] = FinalGripperXLinkedVar, ["FinalGripperYLinkedVar"] = FinalGripperYLinkedVar, ["FinalGripperZLinkedVar"] = FinalGripperZLinkedVar,
                ["IsGripperXLinked"] = IsGripperXLinked, ["IsGripperYLinked"] = IsGripperYLinked, ["IsGripperZLinked"] = IsGripperZLinked,
                ["CadFilePath"] = CadFilePath,
                ["Step5Done"] = Step5Done,
                ["InvertXAngle"] = InvertXAngle,
                ["InvertYAngle"] = InvertYAngle,
                ["InvertThetaAngle"] = InvertThetaAngle,
                // Tab5 夹爪定位新增 Z轴基准
                ["CameraRefX"] = CameraRefX, ["CameraRefY"] = CameraRefY, ["CameraRefZ"] = CameraRefZ,
                ["GripperRefX"] = GripperRefX, ["GripperRefY"] = GripperRefY, ["GripperRefZ"] = GripperRefZ,
                ["CameraOffsetX"] = CameraOffsetX, ["CameraOffsetY"] = CameraOffsetY,
                ["GripperFinalX"] = GripperFinalX, ["GripperFinalY"] = GripperFinalY, ["GripperFinalZ"] = GripperFinalZ,
            };
        }

        /// <summary>将字典配置应用到当前ViewModel属性</summary>
        private void ApplyConfig(Dictionary<string, object> config)
        {
            // ── 第1步：先导入DXF图形（必须在加载点位索引之前，因为DXF导入会清空 ImportedCadPoints） ──
            string savedCadFilePath = "";
            if (config.TryGetValue("CadFilePath", out var cfp)) savedCadFilePath = cfp?.ToString() ?? "";

            if (!string.IsNullOrEmpty(savedCadFilePath))
            {
                // 尝试完整路径
                string resolvedPath = savedCadFilePath;
                if (!System.IO.File.Exists(resolvedPath))
                {
                    // 回退：如果是旧配置只保存了文件名，在常见目录中查找
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? System.IO.Directory.GetCurrentDirectory();
                    var searchDirs = new[]
                    {
                        System.IO.Path.Combine(baseDir, "Config", "LibreCAD"),
                        System.IO.Path.Combine(baseDir, "Config"),
                        baseDir
                    };
                    string fileName = System.IO.Path.GetFileName(savedCadFilePath);
                    foreach (var dir in searchDirs)
                    {
                        var candidate = System.IO.Path.Combine(dir, fileName);
                        if (System.IO.File.Exists(candidate))
                        {
                            resolvedPath = candidate;
                            break;
                        }
                    }
                }

                if (System.IO.File.Exists(resolvedPath))
                {
                    ImportDxfSilent(resolvedPath);
                }
            }

            // ── 第2步：加载所有配置参数（点位已就绪，索引可正确引用） ──

            // Step1 回转中心
            if (config.TryGetValue("Mox", out var mox)) Mox = Convert.ToDouble(mox);
            if (config.TryGetValue("Moy", out var moy)) Moy = Convert.ToDouble(moy);
            if (config.TryGetValue("FitRadius", out var fr)) FitRadius = Convert.ToDouble(fr);
            if (config.TryGetValue("Step1Done", out var s1)) Step1Done = Convert.ToBoolean(s1);

            // Step2 全局偏移
            if (config.TryGetValue("P1Mx", out var p1mx)) P1Mx = Convert.ToDouble(p1mx);
            if (config.TryGetValue("P1My", out var p1my)) P1My = Convert.ToDouble(p1my);
            if (config.TryGetValue("P1Cx", out var p1cx)) P1Cx = Convert.ToDouble(p1cx);
            if (config.TryGetValue("P1Cy", out var p1cy)) P1Cy = Convert.ToDouble(p1cy);
            if (config.TryGetValue("DeltaX", out var dx)) DeltaX = Convert.ToDouble(dx);
            if (config.TryGetValue("DeltaY", out var dy)) DeltaY = Convert.ToDouble(dy);
            if (config.TryGetValue("Step2Done", out var s2)) Step2Done = Convert.ToBoolean(s2);
            if (config.TryGetValue("UseAffineCalibration", out var uac)) UseAffineCalibration = Convert.ToBoolean(uac);
            if (config.TryGetValue("AffineA", out var aa)) AffineA = Convert.ToDouble(aa);
            if (config.TryGetValue("AffineB", out var ab)) AffineB = Convert.ToDouble(ab);
            if (config.TryGetValue("AffineC", out var ac)) AffineC = Convert.ToDouble(ac);
            if (config.TryGetValue("AffineD", out var ad)) AffineD = Convert.ToDouble(ad);
            if (config.TryGetValue("AffineTx", out var atx)) AffineTx = Convert.ToDouble(atx);
            if (config.TryGetValue("AffineTy", out var aty)) AffineTy = Convert.ToDouble(aty);
            if (config.TryGetValue("AffineRmsError", out var are)) AffineRmsError = Convert.ToDouble(are);
            if (config.TryGetValue("AffineRotDeg", out var ard)) AffineRotDeg = Convert.ToDouble(ard);
            if (config.TryGetValue("AffineQualityText", out var aqt)) AffineQualityText = aqt?.ToString() ?? "";

            // Step3 旋转角度
            if (config.TryGetValue("ThetaDeg", out var td)) ThetaDeg = Convert.ToDouble(td);
            if (config.TryGetValue("Step3Done", out var s3)) Step3Done = Convert.ToBoolean(s3);
            if (config.TryGetValue("BaseStartIndex", out var bsi)) BaseStartIndex = Convert.ToInt32(bsi);
            if (config.TryGetValue("BaseEndIndex", out var bei)) BaseEndIndex = Convert.ToInt32(bei);
            if (config.TryGetValue("TargetStartIndex", out var tsi)) TargetStartIndex = Convert.ToInt32(tsi);
            if (config.TryGetValue("TargetEndIndex", out var tei)) TargetEndIndex = Convert.ToInt32(tei);
            // 产品对齐角度（Tab3新增）
            if (config.TryGetValue("AlignmentAngle", out var alignAngle)) AlignmentAngle = Convert.ToDouble(alignAngle);
            if (config.TryGetValue("IsAlignmentAngleLinked", out var iaal)) IsAlignmentAngleLinked = Convert.ToBoolean(iaal);
            if (config.TryGetValue("AlignmentAngleLinkedVar", out var aalv)) AlignmentAngleLinkedVar = aalv?.ToString() ?? "";

            // Step4 坐标变换
            if (config.TryGetValue("TransResultX", out var trx)) TransResultX = Convert.ToDouble(trx);
            if (config.TryGetValue("TransResultY", out var try_)) TransResultY = Convert.ToDouble(try_);
            if (config.TryGetValue("Step4Done", out var s4)) Step4Done = Convert.ToBoolean(s4);
            if (config.TryGetValue("TransformSelectedIndex", out var tsi2)) TransformSelectedIndex = Convert.ToInt32(tsi2);
            if (config.TryGetValue("TargetPairIndex", out var tpi)) TargetPairIndex = Convert.ToInt32(tpi);

            // Step5 夹爪定位
            if (config.TryGetValue("GripperOffX", out var gox)) GripperOffX = Convert.ToDouble(gox);
            if (config.TryGetValue("GripperOffY", out var goy)) GripperOffY = Convert.ToDouble(goy);
            if (config.TryGetValue("TeachX", out var tx)) TeachX = Convert.ToDouble(tx);
            if (config.TryGetValue("TeachY", out var ty)) TeachY = Convert.ToDouble(ty);
            if (config.TryGetValue("TeachRy", out var try2)) TeachRy = Convert.ToDouble(try2);
            if (config.TryGetValue("TeachZ", out var tz)) TeachZ = Convert.ToDouble(tz);
            if (config.TryGetValue("UseCalculatedOffset", out var uco)) UseCalculatedOffset = Convert.ToBoolean(uco);
            if (config.TryGetValue("FinalGripperX", out var fgx)) FinalGripperX = Convert.ToDouble(fgx);
            if (config.TryGetValue("FinalGripperY", out var fgy)) FinalGripperY = Convert.ToDouble(fgy);
            if (config.TryGetValue("FinalGripperXLinkedVar", out var fgxv)) FinalGripperXLinkedVar = fgxv?.ToString() ?? "";
            if (config.TryGetValue("FinalGripperYLinkedVar", out var fgyv)) FinalGripperYLinkedVar = fgyv?.ToString() ?? "";
            if (config.TryGetValue("FinalGripperZLinkedVar", out var fgzv)) FinalGripperZLinkedVar = fgzv?.ToString() ?? "";
            if (config.TryGetValue("IsGripperXLinked", out var igxl)) IsGripperXLinked = Convert.ToBoolean(igxl);
            if (config.TryGetValue("IsGripperYLinked", out var igyl)) IsGripperYLinked = Convert.ToBoolean(igyl);
            if (config.TryGetValue("IsGripperZLinked", out var igzl)) IsGripperZLinked = Convert.ToBoolean(igzl);
            if (config.TryGetValue("CadFilePath", out var cfp2)) CadFilePath = cfp2?.ToString() ?? "";
            if (config.TryGetValue("Step5Done", out var s5)) Step5Done = Convert.ToBoolean(s5);
            if (config.TryGetValue("InvertXAngle", out var ixa)) InvertXAngle = Convert.ToBoolean(ixa);
            if (config.TryGetValue("InvertYAngle", out var iya)) InvertYAngle = Convert.ToBoolean(iya);
            if (config.TryGetValue("InvertThetaAngle", out var ita)) InvertThetaAngle = Convert.ToBoolean(ita);
            // Tab5 夹爪定位新增 Z轴基准
            if (config.TryGetValue("CameraRefX", out var crx)) CameraRefX = Convert.ToDouble(crx);
            if (config.TryGetValue("CameraRefY", out var cry)) CameraRefY = Convert.ToDouble(cry);
            if (config.TryGetValue("CameraRefZ", out var crz)) CameraRefZ = Convert.ToDouble(crz);
            if (config.TryGetValue("GripperRefX", out var grx)) GripperRefX = Convert.ToDouble(grx);
            if (config.TryGetValue("GripperRefY", out var gry)) GripperRefY = Convert.ToDouble(gry);
            if (config.TryGetValue("GripperRefZ", out var grz)) GripperRefZ = Convert.ToDouble(grz);
            if (config.TryGetValue("CameraOffsetX", out var cox)) CameraOffsetX = Convert.ToDouble(cox);
            if (config.TryGetValue("CameraOffsetY", out var coy)) CameraOffsetY = Convert.ToDouble(coy);
            if (config.TryGetValue("GripperFinalX", out var gfx)) GripperFinalX = Convert.ToDouble(gfx);
            if (config.TryGetValue("GripperFinalY", out var gfy)) GripperFinalY = Convert.ToDouble(gfy);
            if (config.TryGetValue("GripperFinalZ", out var gfz)) GripperFinalZ = Convert.ToDouble(gfz);

            // 拟合点集合
            if (config.TryGetValue("FitPoints", out var fpsObj))
            {
                var fpsJson = JsonConvert.SerializeObject(fpsObj);
                var fpsList = JsonConvert.DeserializeObject<List<FitPoint>>(fpsJson);
                if (fpsList != null)
                {
                    FitPoints.Clear();
                    foreach (var fp in fpsList)
                        FitPoints.Add(fp);
                }
            }

            // 仿射标定点集合
            if (config.TryGetValue("AffineCalibrationPoints", out var acpObj))
            {
                var acpJson = JsonConvert.SerializeObject(acpObj);
                var acpList = JsonConvert.DeserializeObject<List<AffineCalibrationPoint>>(acpJson);
                if (acpList != null)
                {
                    AffineCalibrationPoints.Clear();
                    foreach (var ap in acpList)
                        AffineCalibrationPoints.Add(ap);
                }
            }

            // ── 第3步：恢复仿射标定结果（依赖AffineCalibrationPoints已加载） ──
            if (UseAffineCalibration && AffineCalibrationPoints != null && AffineCalibrationPoints.Count >= 3)
            {
                ComputeAffineCalibration();
            }

            // ── 第4步：刷新所有UI状态（点位已就绪，索引有效） ──
            UpdateStepStates(CurrentStep);
            UpdateMachineCoordinates();
            UpdateTransformedCoordText();
            UpdateLineSegmentDisplayText();

            // 触发回转中心可视化更新
            RotationCenterVisualUpdateRequested?.Invoke();
        }

        /// <summary>将当前配置文件路径保存到配方池扩展数据</summary>
        private async Task SaveCurrentFileToRecipePoolAsync()
        {
            try
            {
                var poolName = _recipePoolService.CurrentPoolName ?? "Default";
                await _recipePoolService.SetExtensionDataAsync(poolName, "CadAlignment_CurrentFile",
                    new { FilePath = CurrentFilePath });
            }
            catch { }
        }

        /// <summary>启动时自动加载配置（优先从配方池恢复，其次加载最近保存的配置文件）</summary>
        private async Task TryAutoLoadConfigAsync()
        {
            try
            {
                // 优先从配方池获取上次保存的文件路径
                var poolName = _recipePoolService.CurrentPoolName ?? "Default";
                var extData = await _recipePoolService.GetExtensionDataAsync<object>(poolName, "CadAlignment_CurrentFile");
                if (extData != null)
                {
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                        JsonConvert.SerializeObject(extData));
                    if (dict != null && dict.TryGetValue("FilePath", out var path) && System.IO.File.Exists(path))
                    {
                        await LoadConfigFromPathAsync(path);
                        return;
                    }
                }

                // 回退：查找配置目录中最近修改的 CadAlignment_*.json 文件
                var configDir = GetConfigDirectory();
                var defaultPath = System.IO.Path.Combine(configDir, "CadAlignment_Default.json");
                if (System.IO.File.Exists(defaultPath))
                {
                    await LoadConfigFromPathAsync(defaultPath);
                    return;
                }

                // 查找最新的带时间戳的配置文件
                var latestFile = System.IO.Directory.GetFiles(configDir, "CadAlignment_*.json")
                    .OrderByDescending(f => System.IO.File.GetLastWriteTimeUtc(f))
                    .FirstOrDefault();
                if (latestFile != null)
                {
                    await LoadConfigFromPathAsync(latestFile);
                }
            }
            catch { }
        }

        #endregion

        private void OnShowPrinciple()
        {
            var win = new Views.CadAlignmentPrincipleWindow
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            win.ShowDialog();
        }

        private void OnExportDxf()
        {
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var filePath = System.IO.Path.Combine(desktop, $"CAD_Alignment_{DateTime.Now:yyyyMMdd_HHmmss}.dxf");
                GenerateDxfFile(filePath);
                StatusMessage = $"DXF 已导出: {filePath}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"DXF 导出失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 根据当前6组点位数据拟合圆弧并生成DXF文件（纯ASCII格式，零依赖）
        /// 每组2个点：切点(在圆弧上) + 辅助点(在切线上)
        /// </summary>
        private void GenerateDxfFile(string filePath)
        {
            using (var sw = new System.IO.StreamWriter(filePath, false, System.Text.Encoding.UTF8))
            {
                // ═════ DXF 文件头 ════
                sw.WriteLine("  0");
                sw.WriteLine("SECTION");
                sw.WriteLine("  2");
                sw.WriteLine("HEADER");
                sw.WriteLine("  9");
                sw.WriteLine("$ACADVER");
                sw.WriteLine("  1");
                sw.WriteLine("AC1015"); // AutoCAD 2000
                sw.WriteLine("  9");
                sw.WriteLine("$INSBASE");
                sw.WriteLine(" 10");
                sw.WriteLine("0.0");
                sw.WriteLine(" 20");
                sw.WriteLine("0.0");
                sw.WriteLine(" 30");
                sw.WriteLine("0.0");
                sw.WriteLine("  0");
                sw.WriteLine("ENDSEC");

                // ═════ 实体区 ════
                sw.WriteLine("  0");
                sw.WriteLine("SECTION");
                sw.WriteLine("  2");
                sw.WriteLine("ENTITIES");

                // --- 拟合圆弧 ---
                var (arcCx, arcCy, arcR) = FitArcFromTangentPairs();
                WriteArc(sw, arcCx, arcCy, arcR, 0, 360, "7", "4"); // 白色/青色

                // --- 6条切线段 ---
                var lineColors = new[] { "1", "3", "5", "30", "50", "160" }; // 红/绿/青/橙/紫/蓝
                for (int i = 0; i < CorrespondencePoints.Count && i < 6; i += 2)
                {
                    if (i + 1 >= CorrespondencePoints.Count) break;
                    var pTangent = CorrespondencePoints[i];     // 切点(圆弧上)
                    var pAux = CorrespondencePoints[i + 1];     // 辅助点(切线上)
                    WriteLine(sw, pTangent.CadX, pTangent.CadY, pAux.CadX, pAux.CadY, lineColors[i / 2]);
                }

                // --- 12个点标记(CIRCLE) ---
                for (int i = 0; i < CorrespondencePoints.Count && i < 12; i++)
                {
                    var pt = CorrespondencePoints[i];
                    WriteCircle(sw, pt.CadX, pt.CadY, 1.8, lineColors[i / 2]);
                }

                // --- 点位标签(TEXT) ---
                for (int i = 0; i < CorrespondencePoints.Count && i < 12; i++)
                {
                    var pt = CorrespondencePoints[i];
                    double offsetX = (i % 2 == 0) ? -6 : 6;
                    double offsetY = 5;
                    WriteText(sw, pt.CadX + offsetX, pt.CadY + offsetY, pt.Name, 2.5, lineColors[i / 2]);
                }

                // --- Rz回转中心标记 ---
                if (Step1Done)
                {
                    WriteCircle(sw, Mox, Moy, 3.0, "0"); // 黑色中心
                    WriteText(sw, Mox + 5, Moy + 3, "O(Rz)", 2.2, "0");
                }

                // --- 图框标题 ---
                WriteText(sw, arcCx - 40, arcCy + arcR + 15, "CAD Alignment - Tangent Arc Fitting", 3.0, "7");

                sw.WriteLine("  0");
                sw.WriteLine("ENDSEC");

                // ═════ 文件尾 ════
                sw.WriteLine("  0");
                sw.WriteLine("EOF");
            }
        }

        /// <summary>
        /// 从6组切线对中提取6个切点，最小二乘法拟合圆心和半径
        /// </summary>
        private (double cx, double cy, double r) FitArcFromTangentPairs()
        {
            int n = Math.Min(CorrespondencePoints.Count / 2, 6);
            if (n < 3) return (100.0, 280.0, 140.0); // 默认值

            // 提取每组的第一个点作为切点（偶数索引: 0,2,4,6,8,10）
            var points = new List<(double x, double y)>();
            for (int i = 0; i < n * 2; i += 2)
            {
                points.Add((CorrespondencePoints[i].CadX, CorrespondencePoints[i].CadY));
            }

            // Kåsa 最小二乘圆拟合
            double sx = 0, sy = 0;
            foreach (var p in points) { sx += p.x; sy += p.y; }
            double mx = sx / n, my = sy / n;

            double suu = 0, svv = 0, suv = 0, uuu = 0, vvv = 0;
            foreach (var p in points)
            {
                double u = p.x - mx, v = p.y - my;
                suu += u * u;
                svv += v * v;
                suv += u * v;
                uuu += u * (u * u + v * v);
                vvv += v * (u * u + v * v);
            }

            double det = suu * svv - suv * suv;
            if (Math.Abs(det) < 1e-10) return (mx, my, 100);

            double uc = (svv * uuu - suv * vvv) / (2 * det);
            double vc = (suu * vvv - suv * uuu) / (2 * det);

            double cx = mx + uc;
            double cy = my + vc;
            double r = Math.Sqrt(uc * uc + vc * vc + (suu + svv) / n);

            return (cx, cy, r);
        }

        #region DXF 辅助写入方法

        private static void WriteLine(System.IO.StreamWriter sw, double x1, double y1, double x2, double y2, string color)
        {
            sw.WriteLine("  0");
            sw.WriteLine("LINE");
            sw.WriteLine("  8");
            sw.WriteLine("0");
            sw.WriteLine(" 62");
            sw.WriteLine(color);
            sw.WriteLine(" 10");
            sw.WriteLine(x1.ToString("F4"));
            sw.WriteLine(" 20");
            sw.WriteLine(y1.ToString("F4"));
            sw.WriteLine(" 30");
            sw.WriteLine("0.0");
            sw.WriteLine(" 11");
            sw.WriteLine(x2.ToString("F4"));
            sw.WriteLine(" 21");
            sw.WriteLine(y2.ToString("F4"));
            sw.WriteLine(" 31");
            sw.WriteLine("0.0");
        }

        private static void WriteArc(System.IO.StreamWriter sw, double cx, double cy, double r, double startAngle, double endAngle, string layer, string color)
        {
            sw.WriteLine("  0");
            sw.WriteLine("ARC");
            sw.WriteLine("  8");
            sw.WriteLine(layer);
            sw.WriteLine(" 62");
            sw.WriteLine(color);
            sw.WriteLine(" 10");
            sw.WriteLine(cx.ToString("F4"));
            sw.WriteLine(" 20");
            sw.WriteLine(cy.ToString("F4"));
            sw.WriteLine(" 30");
            sw.WriteLine("0.0");
            sw.WriteLine(" 40");
            sw.WriteLine(r.ToString("F4"));
            sw.WriteLine(" 50");
            sw.WriteLine(startAngle.ToString("F4"));
            sw.WriteLine(" 51");
            sw.WriteLine(endAngle.ToString("F4"));
        }

        private static void WriteCircle(System.IO.StreamWriter sw, double cx, double cy, double r, string color)
        {
            sw.WriteLine("  0");
            sw.WriteLine("CIRCLE");
            sw.WriteLine("  8");
            sw.WriteLine("0");
            sw.WriteLine(" 62");
            sw.WriteLine(color);
            sw.WriteLine(" 10");
            sw.WriteLine(cx.ToString("F4"));
            sw.WriteLine(" 20");
            sw.WriteLine(cy.ToString("F4"));
            sw.WriteLine(" 30");
            sw.WriteLine("0.0");
            sw.WriteLine(" 40");
            sw.WriteLine(r.ToString("F4"));
        }

        private static void WriteText(System.IO.StreamWriter sw, double x, double y, string text, double height, string color)
        {
            sw.WriteLine("  0");
            sw.WriteLine("TEXT");
            sw.WriteLine("  8");
            sw.WriteLine("0");
            sw.WriteLine(" 62");
            sw.WriteLine(color);
            sw.WriteLine(" 10");
            sw.WriteLine(x.ToString("F4"));
            sw.WriteLine(" 20");
            sw.WriteLine(y.ToString("F4"));
            sw.WriteLine(" 30");
            sw.WriteLine("0.0");
            sw.WriteLine(" 40");
            sw.WriteLine(height.ToString("F2"));
            sw.WriteLine("  1");
            sw.WriteLine(text);
        }

        #endregion

        #region 点位增删操作

        private void AddCadPoint()
        {
            CorrespondencePoints.Add(new CorrespondencePoint
            {
                Name = $"P{CorrespondencePoints.Count + 1}"
            });
            RefreshPointPairNames();
        }

        private void DeleteCadPoint(CorrespondencePoint point)
        {
            if (point != null)
            {
                CorrespondencePoints.Remove(point);
                RefreshPointPairNames();
            }
        }

        #endregion

        #region 步骤导航方法

        private ObservableCollection<AlignmentStepInfo> InitializeSteps()
        {
            var lang = _containerProvider.Resolve<ILocalizationService>();
            return new ObservableCollection<AlignmentStepInfo>
            {
                new AlignmentStepInfo { Number = 1, Title = lang.GetResource("CadAlignment_Step1_Title").Replace("① ", ""), Hint = lang.GetResource("CadAlignment_Step1_Hint") },
                new AlignmentStepInfo { Number = 2, Title = lang.GetResource("CadAlignment_Step2_Title").Replace("② ", ""), Hint = lang.GetResource("CadAlignment_Step2_Hint") },
                new AlignmentStepInfo { Number = 3, Title = lang.GetResource("CadAlignment_Step3_Title").Replace("③ ", ""), Hint = lang.GetResource("CadAlignment_Step3_Hint") },
                new AlignmentStepInfo { Number = 4, Title = lang.GetResource("CadAlignment_Step4_Title").Replace("④ ", ""), Hint = lang.GetResource("CadAlignment_Step4_Hint") },
                new AlignmentStepInfo { Number = 5, Title = lang.GetResource("CadAlignment_Step5_Title").Replace("⑤ ", ""), Hint = lang.GetResource("CadAlignment_Step5_Hint") }
            };
        }

        private void UpdateStepStates(int currentStep)
        {
            foreach (var step in Steps)
            {
                step.IsCurrent = (step.Number == currentStep);
                step.IsCompleted = (step.Number < currentStep);
            }
        }

        private bool CanGoNext() => _currentStep < 5;
        private void GoNext() { if (_currentStep < 5) CurrentStep++; }

        private bool CanGoPrev() => _currentStep > 1;
        private void GoPrev() { if (_currentStep > 1) CurrentStep--; }

        #endregion

        /// <summary>
        /// 相机到夹爪坐标系变换计算（SVD方法，供CoordinateCalibrationDialog调用）
        /// </summary>
        public static (Matrix3x3 R, Vector3 t) ComputeCameraToGripperTransform(List<Core.Extensions.Point3D> cameraPoints, List<Core.Extensions.Point3D> gripperPoints)
        {
            int n = cameraPoints.Count;
            if (n < 3 || n != gripperPoints.Count)
                throw new ArgumentException("至少需要3组对应点，且两组点数必须相同");

            // 计算质心
            double pcx = 0, pcy = 0, pcz = 0;
            double gcx = 0, gcy = 0, gcz = 0;
            for (int i = 0; i < n; i++)
            {
                pcx += cameraPoints[i].X; pcy += cameraPoints[i].Y; pcz += cameraPoints[i].Z;
                gcx += gripperPoints[i].X; gcy += gripperPoints[i].Y; gcz += gripperPoints[i].Z;
            }
            pcx /= n; pcy /= n; pcz /= n;
            gcx /= n; gcy /= n; gcz /= n;

            // 计算协方差矩阵 H = Σ(pi-pc)(gi-gc)^T
            double h11 = 0, h12 = 0, h13 = 0;
            double h21 = 0, h22 = 0, h23 = 0;
            double h31 = 0, h32 = 0, h33 = 0;

            for (int i = 0; i < n; i++)
            {
                double cix = cameraPoints[i].X - pcx, ciy = cameraPoints[i].Y - pcy, ciz = cameraPoints[i].Z - pcz;
                double gix = gripperPoints[i].X - gcx, giy = gripperPoints[i].Y - gcy, gib = gripperPoints[i].Z - gcz;
                h11 += cix * gix; h12 += cix * giy; h13 += cix * gib;
                h21 += ciy * gix; h22 += ciy * giy; h23 += ciy * gib;
                h31 += ciz * gix; h32 += ciz * giy; h33 += ciz * gib;
            }

            var H = new[,]
            {
                { h11, h12, h13 },
                { h21, h22, h23 },
                { h31, h32, h33 }
            };

            var HHT = new double[3, 3];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    for (int k = 0; k < 3; k++)
                        HHT[i, j] += H[i, k] * H[j, k];

            var (_, U) = SymmetricEigenDecomposition(HHT);

            var HTH = new double[3, 3];
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    for (int k = 0; k < 3; k++)
                        HTH[i, j] += H[k, i] * H[k, j];

            var (_, V) = SymmetricEigenDecomposition(HTH);

            // R = V * U^T → Core.Models.Matrix3x3
            var R = new Matrix3x3(
                V[0, 0] * U[0, 0] + V[0, 1] * U[1, 0] + V[0, 2] * U[2, 0],
                V[0, 0] * U[0, 1] + V[0, 1] * U[1, 1] + V[0, 2] * U[2, 1],
                V[0, 0] * U[0, 2] + V[0, 1] * U[1, 2] + V[0, 2] * U[2, 2],
                V[1, 0] * U[0, 0] + V[1, 1] * U[1, 0] + V[1, 2] * U[2, 0],
                V[1, 0] * U[0, 1] + V[1, 1] * U[1, 1] + V[1, 2] * U[2, 1],
                V[1, 0] * U[0, 2] + V[1, 1] * U[1, 2] + V[1, 2] * U[2, 2],
                V[2, 0] * U[0, 0] + V[2, 1] * U[1, 0] + V[2, 2] * U[2, 0],
                V[2, 0] * U[0, 1] + V[2, 1] * U[1, 1] + V[2, 2] * U[2, 1],
                V[2, 0] * U[0, 2] + V[2, 1] * U[1, 2] + V[2, 2] * U[2, 2]
            );

            // 修正反射: det(R) 应为 +1
            double detR = R.M11 * (R.M22 * R.M33 - R.M23 * R.M32)
                       - R.M12 * (R.M21 * R.M33 - R.M23 * R.M31)
                       + R.M13 * (R.M21 * R.M32 - R.M22 * R.M31);
            if (detR < 0)
            {
                for (int i = 0; i < 3; i++) V[i, 2] *= -1;
                R = new Matrix3x3(
                    V[0, 0] * U[0, 0] + V[0, 1] * U[1, 0] + V[0, 2] * U[2, 0],
                    V[0, 0] * U[0, 1] + V[0, 1] * U[1, 1] + V[0, 2] * U[2, 1],
                    V[0, 0] * U[0, 2] + V[0, 1] * U[1, 2] + V[0, 2] * U[2, 2],
                    V[1, 0] * U[0, 0] + V[1, 1] * U[1, 0] + V[1, 2] * U[2, 0],
                    V[1, 0] * U[0, 1] + V[1, 1] * U[1, 1] + V[1, 2] * U[2, 1],
                    V[1, 0] * U[0, 2] + V[1, 1] * U[1, 2] + V[1, 2] * U[2, 2],
                    V[2, 0] * U[0, 0] + V[2, 1] * U[1, 0] + V[2, 2] * U[2, 0],
                    V[2, 0] * U[0, 1] + V[2, 1] * U[1, 1] + V[2, 2] * U[2, 1],
                    V[2, 0] * U[0, 2] + V[2, 1] * U[1, 2] + V[2, 2] * U[2, 2]
                );
            }

            // t = gc - R * pc
            var Rpc = R * new Vector3(pcx, pcy, pcz);
            var t = new Vector3(gcx, gcy, gcz) - Rpc;

            return (R, t);
        }

        /// <summary>
        /// 3x3 对称矩阵特征值分解（Jacobi 迭代法）
        /// </summary>
        private static (double[] eigenvalues, double[,] eigenvectors) SymmetricEigenDecomposition(double[,] A)
        {
            int n = 3;
            var V = new double[n, n];
            for (int i = 0; i < n; i++) V[i, i] = 1.0;
            var D = new double[n];
            for (int i = 0; i < n; i++) D[i] = A[i, i];
            var B = new double[n];
            var Z = new double[n];
            for (int i = 0; i < n; i++) B[i] = D[i];

            for (int iter = 0; iter < 100; iter++)
            {
                // 找最大非对角元
                int p = 0, q = 1;
                double maxOffDiag = Math.Abs(A[0, 1]);
                for (int i = 0; i < n; i++)
                    for (int j = i + 1; j < n; j++)
                        if (Math.Abs(A[i, j]) > maxOffDiag)
                        { maxOffDiag = Math.Abs(A[i, j]); p = i; q = j; }

                if (maxOffDiag < 1e-12) break;

                double diff = D[q] - D[p];
                double theta = 0.5 * Math.Atan2(2 * A[p, q], diff);
                double c = Math.Cos(theta), s = Math.Sin(theta);
                double tau = (1 - c) / s;

                D[p] -= s * A[p, q]; D[q] += s * A[p, q];
                B[p] = D[p]; B[q] = D[q]; Z[p] += s; Z[q] -= s;

                for (int r = 0; r < n; r++)
                {
                    if (r != p && r != q)
                    {
                        double Apr = A[r, p], Arq = A[r, q];
                        A[p, r] = A[r, p] = c * Apr - s * Arq;
                        A[q, r] = A[r, q] = s * Apr + c * Arq;
                    }
                    double Vrp = V[r, p], Vrq = V[r, q];
                    V[r, p] = c * Vrp - s * Vrq;
                    V[r, q] = s * Vrp + c * Vrq;
                }
                A[p, p] = D[p]; A[q, q] = D[q];
                A[p, q] = A[q, p] = 0;
            }

            return (D, V);
        }
    }
}

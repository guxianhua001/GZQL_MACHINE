using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using Framework.Models;
using MaterialDesignThemes.Wpf;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using SmarterMotion;
using Stations;
using Stations.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using ZXing;
using static System.Windows.Forms.AxHost;

namespace Framework.ViewModels
{
    public class DispensingPathViewModel : BindableBase
    {
        #region 属性
        // 相机中心坐标（从视觉结果解析）
        private double _cameraCenterX;
        private double _cameraCenterY;

        // 轨迹参数
        private double _pathStartX;
        private double _pathStartY;
        private double _pathMidX;
        private double _pathMidY;
        private double _pathEndX;
        private double _pathEndY;
        private int _pathSegmentCount = 20;
        private ObservableCollection<PathTypeItem> _pathTypes;
        private PathTypeItem _selectedPathType;

        // 序号选择
        private ObservableCollection<int> _availableIndexes;
        private int _selectedIndex = 1;

        // 轴坐标
        private double _axisX;
        private double _axisY;
        private double _axisStartX;
        private double _axisStartY;
        private double _axisOffsetX;
        private double _axisOffsetY;
        private double _safeHeight;

        // 补偿参数
        private double _cameraNeedleOffsetX;
        private double _cameraNeedleOffsetY;
        private double _needleCompensationX;
        private double _needleCompensationY;

        // 点胶参数
        private double _pathMoveSpeed = 1;
        private double _pathDispensingTime = 100;

        // 列表数据
        private ObservableCollection<PathPoint> _generatedPathPoints;
        private ObservableCollection<AxisPathPoint> _axisPathPoints;
        private ObservableCollection<NeedlePathPoint> _needlePathPoints;

        // 状态
        private bool _isInterpolationRunning;
        private string _interpolationStatus = "就绪";
        private Brush _interpolationStatusColor = Brushes.LightGray;
        private int _currentPointIndex;
        private double _interpolationProgress;

        // 弧线方向控制
        private double _arcDirection = -1.0; // 1.0=向外，-1.0=向内
        private bool _autoAdjustArcDirection = true;

        // 服务和事件
        private readonly ILoggerService _loggerService;
        private IEventAggregator _eventAggregator;
        private readonly ICameraController _cameraController;
        private readonly IVisionDataService _visionDataService;
        private readonly IParameterStorage _parameterStorage;
        private DmcMotionService _motionService;
        private readonly TaskInstanceManager _taskManager;
        private DispenserStation _dispenserStation;
        #endregion

        #region 属性访问器
        // 相机中心坐标
        public double CameraCenterX
        {
            get => _cameraCenterX;
            set => SetProperty(ref _cameraCenterX, value);
        }

        public double CameraCenterY
        {
            get => _cameraCenterY;
            set => SetProperty(ref _cameraCenterY, value);
        }

        public double PathStartX
        {
            get => _pathStartX;
            set => SetProperty(ref _pathStartX, value);
        }

        public double PathStartY
        {
            get => _pathStartY;
            set => SetProperty(ref _pathStartY, value);
        }

        public double PathMidX
        {
            get => _pathMidX;
            set => SetProperty(ref _pathMidX, value);
        }

        public double PathMidY
        {
            get => _pathMidY;
            set => SetProperty(ref _pathMidY, value);
        }

        public double PathEndX
        {
            get => _pathEndX;
            set => SetProperty(ref _pathEndX, value);
        }

        public double PathEndY
        {
            get => _pathEndY;
            set => SetProperty(ref _pathEndY, value);
        }

        public int PathSegmentCount
        {
            get => _pathSegmentCount;
            set => SetProperty(ref _pathSegmentCount, value);
        }

        public ObservableCollection<PathTypeItem> PathTypes
        {
            get => _pathTypes;
            set => SetProperty(ref _pathTypes, value);
        }

        public PathTypeItem SelectedPathType
        {
            get => _selectedPathType;
            set => SetProperty(ref _selectedPathType, value);
        }
        public ObservableCollection<int> AvailableIndexes
        {
            get => _availableIndexes;
            set => SetProperty(ref _availableIndexes, value);
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set => SetProperty(ref _selectedIndex, value);
        }
        public double AxisX
        {
            get => _axisX;
            set => SetProperty(ref _axisX, value);
        }

        public double AxisY
        {
            get => _axisY;
            set => SetProperty(ref _axisY, value);
        }
        public double AxisStartX
        {
            get => _axisStartX;
            set => SetProperty(ref _axisStartX, value);
        }

        public double AxisStartY
        {
            get => _axisStartY;
            set => SetProperty(ref _axisStartY, value);
        }

        public double AxisOffsetX
        {
            get => _axisOffsetX;
            set => SetProperty(ref _axisOffsetX, value);
        }

        public double AxisOffsetY
        {
            get => _axisOffsetY;
            set => SetProperty(ref _axisOffsetY, value);
        }
        public double SafeHeight
        {
            get => _safeHeight;
            set => SetProperty(ref _safeHeight, value);
        }   

        public double CameraNeedleOffsetX
        {
            get => _cameraNeedleOffsetX;
            set => SetProperty(ref _cameraNeedleOffsetX, value);
        }

        public double CameraNeedleOffsetY
        {
            get => _cameraNeedleOffsetY;
            set => SetProperty(ref _cameraNeedleOffsetY, value);
        }

        public double NeedleCompensationX
        {
            get => _needleCompensationX;
            set => SetProperty(ref _needleCompensationX, value);
        }

        public double NeedleCompensationY
        {
            get => _needleCompensationY;
            set => SetProperty(ref _needleCompensationY, value);
        }

        public double PathMoveSpeed
        {
            get => _pathMoveSpeed;
            set => SetProperty(ref _pathMoveSpeed, value);
        }

        public double PathDispensingTime
        {
            get => _pathDispensingTime;
            set => SetProperty(ref _pathDispensingTime, value);
        }

        public ObservableCollection<PathPoint> GeneratedPathPoints
        {
            get => _generatedPathPoints;
            set => SetProperty(ref _generatedPathPoints, value);
        }

        public ObservableCollection<AxisPathPoint> AxisPathPoints
        {
            get => _axisPathPoints;
            set => SetProperty(ref _axisPathPoints, value);
        }

        public ObservableCollection<NeedlePathPoint> NeedlePathPoints
        {
            get => _needlePathPoints;
            set => SetProperty(ref _needlePathPoints, value);
        }

        public bool IsInterpolationRunning
        {
            get => _isInterpolationRunning;
            set => SetProperty(ref _isInterpolationRunning, value);
        }

        public string InterpolationStatus
        {
            get => _interpolationStatus;
            set => SetProperty(ref _interpolationStatus, value);
        }

        public Brush InterpolationStatusColor
        {
            get => _interpolationStatusColor;
            set => SetProperty(ref _interpolationStatusColor, value);
        }

        public int CurrentPointIndex
        {
            get => _currentPointIndex;
            set => SetProperty(ref _currentPointIndex, value);
        }

        public double InterpolationProgress
        {
            get => _interpolationProgress;
            set => SetProperty(ref _interpolationProgress, value);
        }
        // 弧线方向控制
        public double ArcDirection
        {
            get => _arcDirection;
            set => SetProperty(ref _arcDirection, value);
        }

        public bool AutoAdjustArcDirection
        {
            get => _autoAdjustArcDirection;
            set => SetProperty(ref _autoAdjustArcDirection, value);
        }
        #endregion

        #region 命令

        public ICommand GeneratePathCommand { get; private set; }
        public ICommand GenerateAxisPathCommand { get; private set; }
        public ICommand CalculateNeedlePathCommand { get; private set; }
        public ICommand StartContinuousInterpolationCommand { get; private set; }
        public ICommand PauseContinuousInterpolationCommand { get; private set; }
        public ICommand StopContinuousInterpolationCommand { get; private set; }
        public ICommand TestSingleStepCommand { get; private set; }
        public ICommand ExportPathPointsCommand { get; private set; }
        public ICommand ClearPathPointsCommand { get; private set; }
        public ICommand TakeCurvedPathPhoto1Command { get; private set; }
        public ICommand TakeCurvedPathPhoto2Command { get; private set; }
        public ICommand MoveToPhotoPositionCommand { get; private set; }
        public ICommand MoveToStartPointCommand { get; private set; }
        #endregion

        #region 构造函数

        public DispensingPathViewModel(
            IEventAggregator eventAggregator,
            ICameraController cameraController, 
            IVisionDataService visionDataService, 
            ILoggerService loggerService,
            IParameterStorage parameterStorage,
            TaskInstanceManager taskManager,
            DmcMotionService motionService,
            DispenserStation dispenserStation)
        {
            _eventAggregator = eventAggregator;
            _loggerService = loggerService;
            _cameraController = cameraController;
            _visionDataService = visionDataService;
            _taskManager = taskManager;
            _parameterStorage = parameterStorage;
            _motionService = motionService;
            // 初始化集合
            GeneratedPathPoints = new ObservableCollection<PathPoint>();
            AxisPathPoints = new ObservableCollection<AxisPathPoint>();
            NeedlePathPoints = new ObservableCollection<NeedlePathPoint>();
            // 初始化序号选择
            InitializeIndexes(); 
            // 初始化路径类型
            InitializePathTypes();

            // 初始化补偿参数（可以从配置文件或数据库加载）
            InitializeCompensationParameters();

            // 初始化命令
            InitializeCommands();

            _dispenserStation = _taskManager.GetTask<DispenserStation>();

        }

        private void InitializePathTypes()
        {
            PathTypes = new ObservableCollection<PathTypeItem>
            {
                new PathTypeItem { Name = "贝塞尔曲线", Type = PathType.Bezier },
                new PathTypeItem { Name = "直线", Type = PathType.Line },
                new PathTypeItem { Name = "样条曲线", Type = PathType.Spline },
                new PathTypeItem { Name = "圆形", Type = PathType.Circle }
            };
            SelectedPathType = PathTypes.First();
        }
        private void InitializeIndexes()
        {
            // 初始化1-6的序号
            AvailableIndexes = new ObservableCollection<int> { 1, 2, 3, 4, 5, 6 };
            SelectedIndex = 1;
        }
        private void InitializeCompensationParameters()
        {
            // 从配置加载补偿参数
            CameraNeedleOffsetX = 5.0;  // 相机与针头X方向固定间距
            CameraNeedleOffsetY = 5.0;  // 相机与针头Y方向固定间距
            NeedleCompensationX = 0.1;  // 校针X补偿
            NeedleCompensationY = 0.1;  // 校针Y补偿
        }

        private void InitializeCommands()
        {
            GeneratePathCommand = new DelegateCommand(GeneratePath);
            GenerateAxisPathCommand = new DelegateCommand(GenerateAxisPath);
            CalculateNeedlePathCommand = new DelegateCommand(CalculateNeedlePath);
            StartContinuousInterpolationCommand = new DelegateCommand(StartContinuousInterpolation);
            PauseContinuousInterpolationCommand = new DelegateCommand(PauseContinuousInterpolation);
            StopContinuousInterpolationCommand = new DelegateCommand(StopContinuousInterpolation);
            TestSingleStepCommand = new DelegateCommand(TestSingleStep);
            ExportPathPointsCommand = new DelegateCommand(ExportPathPoints);
            ClearPathPointsCommand = new DelegateCommand(ClearPathPoints);
            TakeCurvedPathPhoto1Command = new DelegateCommand(() => TakeCurvedPathPhoto(1));
            TakeCurvedPathPhoto2Command = new DelegateCommand(() => TakeCurvedPathPhoto(2));
            MoveToPhotoPositionCommand = new DelegateCommand(MoveToPhotoPositionAsync);
            MoveToStartPointCommand = new DelegateCommand(MoveToStartPoint);
        }
        private void LoadCalibrationParameters()
        {
            try
            {
                // 使用支持自定义路径的重载方法
                string _customDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                        "Config",
                                        "Calibration");
                var parameters = _parameterStorage?.Load<NeedleCalibrationParameters>(
                    "NeedleCalibration",
                    _customDirectory  // 自定义目录
                );

                if (parameters != null)
                {
                    AxisStartX = parameters.CameraCenterX;
                    AxisStartY = parameters.CameraCenterY;

                    CameraNeedleOffsetX = parameters.CalibrationDeltaX;
                    CameraNeedleOffsetY = parameters.CalibrationDeltaY;
                    NeedleCompensationX = parameters.CompensationX;
                    NeedleCompensationY = parameters.CompensationY;

                    RaisePropertyChanged(nameof(CameraNeedleOffsetX));
                    RaisePropertyChanged(nameof(CameraNeedleOffsetY));
                    RaisePropertyChanged(nameof(NeedleCompensationX));
                    RaisePropertyChanged(nameof(NeedleCompensationY));

                    InterpolationStatus = $"针头校准参数加载成功";
                    InterpolationStatusColor = Brushes.LightGreen;
                }
                else
                {
                    InterpolationStatus = $"未找到针头校准参数，使用默认值";
                    InterpolationStatusColor = Brushes.DarkRed;
                }
            }
            catch (Exception ex)
            {
                InterpolationStatus = $"加载针头校准参数异常: {ex.Message}";
                InterpolationStatusColor = Brushes.DarkRed;
            }
        }
        #endregion

        #region 命令实现

        private void GeneratePath()
        {
            try
            {
                GeneratedPathPoints.Clear();

                if (PathSegmentCount <= 0)
                {
                    InterpolationStatus = "错误: 段数必须大于0";
                    InterpolationStatusColor = Brushes.Red;
                    return;
                }

                // 根据选择的路径类型生成轨迹点
                switch (SelectedPathType.Type)
                {
                    case PathType.Bezier:
                        GenerateBezierPath();
                        break;
                    case PathType.Line:
                        GenerateLinePath();
                        break;
                    case PathType.Spline:
                        GenerateSplinePath();
                        break;
                    case PathType.Circle:
                        GenerateCirclePath();
                        break;
                }

                InterpolationStatus = $"已生成 {GeneratedPathPoints.Count} 个轨迹点";
                InterpolationStatusColor = Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                InterpolationStatus = $"生成轨迹失败: {ex.Message}";
                InterpolationStatusColor = Brushes.Red;
            }
        }

        private void GenerateAxisPath()
        {
            try
            {
                if (GeneratedPathPoints.Count == 0)
                {
                    InterpolationStatus = "请先生成轨迹点";
                    InterpolationStatusColor = Brushes.Orange;
                    return;
                }

                AxisPathPoints.Clear();

                string _customDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                        "Config",
                                        "Calibration");
                var parameters = _parameterStorage?.Load<NeedleCalibrationParameters>(
                    "NeedleCalibration",
                    _customDirectory  // 自定义目录
                );

                if (parameters != null) 
                {
                    AxisStartX = parameters.CameraCenterX;
                    AxisStartY = parameters.CameraCenterY;
                }

                // 获取相机中心位置（拍照位置）
                string positionName = $"CurvedPath{SelectedIndex}点胶拍照位";
                double photoX = _dispenserStation.GetPosition(_dispenserStation.DispX.ActId, positionName);
                double photoY = _dispenserStation.GetPosition(_dispenserStation.DispY_1.ActId, positionName);
                AxisX = photoX;
                AxisY = photoY;

                // 检查是否有相机中心坐标
                bool hasCameraCenter = Math.Abs(CameraCenterX) > 0.001 || Math.Abs(CameraCenterY) > 0.001;

                // 获取第一个轨迹点作为参考点
                var firstPoint = GeneratedPathPoints[0];

                // 计算基础偏移量：将轨迹的起点映射到起始轴坐标
                double baseOffsetX = AxisStartX - firstPoint.X;
                double baseOffsetY = AxisStartY - firstPoint.Y;

                // 自动计算轴偏移量：起始点 - 相机中心
                if (hasCameraCenter)
                {
                    // 计算轴偏移量 = 起始点 - 相机中心
                    double calculatedOffsetX = PathEndX - CameraCenterX; 
                    double calculatedOffsetY = PathEndY - CameraCenterY;

                    // 更新轴偏移量
                    AxisOffsetX = AxisX - AxisStartX - calculatedOffsetX;
                    AxisOffsetY = AxisY - AxisStartY - calculatedOffsetY;

                    RaisePropertyChanged(nameof(AxisOffsetX));
                    RaisePropertyChanged(nameof(AxisOffsetY));

                    InterpolationStatus = $"已自动计算轴偏移量: X={AxisOffsetX:F3}, Y={AxisOffsetY:F3}";
                }
                else
                {
                    InterpolationStatus = "未检测到相机中心坐标，使用手动设置的轴偏移量";
                    InterpolationStatusColor = Brushes.Orange;
                }

                // 根据起始轴坐标、轨迹点和偏移量生成轴轨迹
                for (int i = 0; i < GeneratedPathPoints.Count; i++)
                {
                    var point = GeneratedPathPoints[i];

                    // 计算轴坐标：
                    // 1. 将轨迹点平移到以起始轴坐标为基准的位置
                    // 2. 加上轴偏移量
                    double axisX = point.X + baseOffsetX + AxisOffsetX;
                    double axisY = point.Y + baseOffsetY + AxisOffsetY;

                    var axisPoint = new AxisPathPoint
                    {
                        Index = i + 1,
                        X = point.X,
                        Y = point.Y,
                        AxisOffsetX = Math.Round(axisX, 3),
                        AxisOffsetY = Math.Round(axisY, 3),
                        SegmentLength = point.SegmentLength,
                        AccumulatedLength = point.AccumulatedLength
                    };

                    AxisPathPoints.Add(axisPoint);
                }

                InterpolationStatus = $"已生成 {AxisPathPoints.Count} 个轴轨迹点";
                if (hasCameraCenter)
                {
                    InterpolationStatus += $"，轴偏移量: X={AxisOffsetX:F3}, Y={AxisOffsetY:F3}";
                }
                InterpolationStatusColor = Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                InterpolationStatus = $"生成轴轨迹失败: {ex.Message}";
                InterpolationStatusColor = Brushes.Red;
            }
        }

        private void CalculateNeedlePath()
        {
            try
            {
                if (AxisPathPoints.Count == 0)
                {
                    InterpolationStatus = "请先生成轴轨迹点";
                    InterpolationStatusColor = Brushes.Orange;
                    return;
                }

                NeedlePathPoints.Clear();

                // 加载针头标定文件
                LoadCalibrationParameters();

                // 计算公式：针头坐标 = 轴坐标 + 相机与针头固定间距 + 校针补偿量 + 相机补偿量
                for (int i = 0; i < AxisPathPoints.Count; i++)
                {
                    var axisPoint = AxisPathPoints[i];

                    var needlePoint = new NeedlePathPoint
                    {
                        Index = i + 1,
                        X = axisPoint.X,
                        Y = axisPoint.Y,
                        AxisOffsetX = axisPoint.AxisOffsetX, // 轴偏移量
                        AxisOffsetY = axisPoint.AxisOffsetY,
                        Speed = PathMoveSpeed,
                        DispensingTime = PathDispensingTime
                    };

                    // 计算针头真实坐标 = 轴坐标 + 相机与针头固定间距 + 校针补偿量 + 相机补偿量
                    double needleX = axisPoint.AxisOffsetX
                        + CameraNeedleOffsetX
                        //+ axisPoint.X
                        + NeedleCompensationX;

                    double needleY = axisPoint.AxisOffsetY
                        + CameraNeedleOffsetY
                        //+ axisPoint.Y
                        + NeedleCompensationY;

                    needlePoint.AxisOffsetX = Math.Round(axisPoint.AxisOffsetX, 3);
                    needlePoint.AxisOffsetY = Math.Round(axisPoint.AxisOffsetY, 3);

                    needlePoint.NeedleX = Math.Round(needleX, 3);
                    needlePoint.NeedleY = Math.Round(needleY, 3);
                    needlePoint.SegmentLength = axisPoint.SegmentLength;
                    needlePoint.AccumulatedLength = axisPoint.AccumulatedLength;

                    NeedlePathPoints.Add(needlePoint);
                }

                InterpolationStatus = $"已计算 {NeedlePathPoints.Count} 个针头轨迹点";
                InterpolationStatusColor = Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                InterpolationStatus = $"计算针头轨迹失败: {ex.Message}";
                InterpolationStatusColor = Brushes.Red;
            }
        }

        #region 连续插补方法

        private async void StartContinuousInterpolation()
        {
            try
            {
                if (NeedlePathPoints.Count == 0)
                {
                    InterpolationStatus = "请先生成针头轨迹点";
                    InterpolationStatusColor = Brushes.Orange;
                    return;
                }

                IsInterpolationRunning = true;
                InterpolationStatus = "连续插补运行中...";
                InterpolationStatusColor = Brushes.Green;

                CurrentPointIndex = 0;
                InterpolationProgress = 0;

                await _dispenserStation.ReturnToSafePositionAsync();

                // 移到起始点
                await _dispenserStation.MoveToContinuousTrajectoryStart(
                    NeedlePathPoints[0].NeedleX, NeedlePathPoints[0].NeedleY);

                double safeHeight = SafeHeight;// 41.312;  //临时测试，后续应从3D数据中读取
                await _dispenserStation.MoveToDispensingHeightAsync(safeHeight);

                // 初始化连续插补
                _motionService.InitializeContinuousInterpolation();

                // 将针头轨迹点添加到连续插补列表
                for (int i = 0; i < NeedlePathPoints.Count; i++)
                {
                    if (!IsInterpolationRunning) break;

                    var point = NeedlePathPoints[i];
                    CurrentPointIndex = i + 1;
                    InterpolationProgress = (double)(i + 1) / NeedlePathPoints.Count * 100;

                    // 添加线段到连续插补列表
                    _motionService.AddLineSegment(point.NeedleX, point.NeedleY, 1, i);

                    InterpolationStatus = $"正在添加第 {i + 1}/{NeedlePathPoints.Count} 个点";

                    // 短暂延时，避免添加过快
                    await Task.Delay(10);
                }

                if (!IsInterpolationRunning)
                {
                    InterpolationStatus = "插补已停止";
                    InterpolationStatusColor = Brushes.LightCoral;
                    return;
                }

                // 执行连续插补
                _motionService.ExecuteContinuousInterpolation();

                // 点胶开始
                await _motionService.ControlDispensing(20);

                InterpolationStatus = "连续插补执行中，请等待完成...";

                // 等待运动完成
                await WaitForMotionCompletionAsync();

                // 点胶结束 
                await _motionService.StopDispensing(20);
                await _dispenserStation.ReturnToSafePositionAsync();
                if (IsInterpolationRunning)
                {
                    InterpolationStatus = "连续插补完成";
                    InterpolationStatusColor = Brushes.LightGreen;
                    IsInterpolationRunning = false;
                }
            }
            catch (Exception ex)
            {
                InterpolationStatus = $"连续插补失败: {ex.Message}";
                InterpolationStatusColor = Brushes.Red;
                IsInterpolationRunning = false;

                // 紧急停止
                try
                {
                    _motionService.EmergencyStop();
                }
                catch { }
            }
        }

        private async Task<bool> WaitForMotionCompletionAsync()
        {
            try
            {
                TimeSpan timeout = TimeSpan.FromSeconds(60 * 3);

                return await _motionService.WaitForMotionCompletionAsync(timeout);
            }
            catch (Exception ex)
            {
                InterpolationStatus = $"等待运动完成时出错: {ex.Message}";
                InterpolationStatusColor = Brushes.Orange;
                return false;
            }
        }

        #endregion

        private void PauseContinuousInterpolation()
        {
            try
            {
                // 暂停运动
                // _motionService.PauseMotion();

                InterpolationStatus = "插补已暂停";
                InterpolationStatusColor = Brushes.Orange;
            }
            catch (Exception ex)
            {
                InterpolationStatus = $"暂停插补失败: {ex.Message}";
                InterpolationStatusColor = Brushes.Red;
            }
        }

        private void StopContinuousInterpolation()
        {
            try
            {
                IsInterpolationRunning = false;

                // 紧急停止运动
                _motionService.EmergencyStop();

                InterpolationStatus = "插补已停止";
                InterpolationStatusColor = Brushes.LightCoral;
            }
            catch (Exception ex)
            {
                InterpolationStatus = $"停止插补失败: {ex.Message}";
                InterpolationStatusColor = Brushes.Red;
            }
        }

        private async void TestSingleStep()
        {
            try
            {
                if (NeedlePathPoints.Count == 0)
                {
                    InterpolationStatus = "请先生成针头轨迹点";
                    InterpolationStatusColor = Brushes.Orange;
                    return;
                }

                // 测试第一个点
                var testPoint = NeedlePathPoints.First();

                InterpolationStatus = "单步测试中，移动到第一个点...";
                InterpolationStatusColor = Brushes.Yellow;

                // 初始化连续插补
                _motionService.InitializeContinuousInterpolation();

                // 添加第一个点
                _motionService.AddLineSegment(testPoint.NeedleX, testPoint.NeedleY, 1, 0);

                // 执行
                _motionService.ExecuteContinuousInterpolation();

                // 等待运动完成
                await Task.Delay(500);

                // 测试点胶（如果有点胶时间）
                if (testPoint.DispensingTime > 0)
                {
                    InterpolationStatus = "单步测试中，执行点胶...";

                    //await _motionService.ControlDispensing(testPoint.DispensingTime);
                    await Task.Delay((int)testPoint.DispensingTime);
                }

                InterpolationStatus = "单步测试完成";
                InterpolationStatusColor = Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                InterpolationStatus = $"单步测试失败: {ex.Message}";
                InterpolationStatusColor = Brushes.Red;

                // 紧急停止
                try
                {
                    _motionService.EmergencyStop();
                }
                catch { }
            }
        }

        private void ExportPathPoints()
        {
            try
            {
                // 这里实现导出CSV功能
                InterpolationStatus = "轨迹点已导出";
                InterpolationStatusColor = Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                InterpolationStatus = $"导出失败: {ex.Message}";
                InterpolationStatusColor = Brushes.Red;
            }
        }

        private void ClearPathPoints()
        {
            GeneratedPathPoints.Clear();
            AxisPathPoints.Clear();
            NeedlePathPoints.Clear();
            InterpolationStatus = "已清空所有轨迹点";
            InterpolationStatusColor = Brushes.LightGray;
        }

        #endregion

        #region 轨迹生成算法

        private void GenerateBezierPath2()
        {
            // 二阶贝塞尔曲线: B(t) = (1-t)²P0 + 2(1-t)tP1 + t²P2  
            for (int i = 0; i <= PathSegmentCount; i++)
            {
                double t = (double)i / PathSegmentCount;

                // 计算贝塞尔曲线上的点
                double x = Math.Pow(1 - t, 2) * PathStartX +
                          2 * (1 - t) * t * PathMidX +
                          Math.Pow(t, 2) * PathEndX;

                double y = Math.Pow(1 - t, 2) * PathStartY +
                          2 * (1 - t) * t * PathMidY +
                          Math.Pow(t, 2) * PathEndY;

                AddPathPoint(i + 1, x, y);
            }
        }
        private void GenerateBezierPath()
        {
            // 二阶贝塞尔曲线: B(t) = (1-t)²P0 + 2(1-t)tP1 + t²P2

            // 计算起点和终点的中点
            double midX = (PathStartX + PathEndX) / 2;
            double midY = (PathStartY + PathEndY) / 2;

            // 计算方向向量
            double dx = PathEndX - PathStartX;
            double dy = PathEndY - PathStartY;

            // 计算垂直向量
            double perpendicularX = -dy;
            double perpendicularY = dx;

            // 归一化垂直向量
            double length = Math.Sqrt(perpendicularX * perpendicularX + perpendicularY * perpendicularY);
            if (length > 0)
            {
                perpendicularX /= length;
                perpendicularY /= length;
            }

            // 计算默认控制点
            double controlX = PathMidX;
            double controlY = PathMidY;

            // 如果启用了自动调整弧线方向
            if (AutoAdjustArcDirection)
            {
                // 计算起点到终点距离
                double distance = Math.Sqrt(dx * dx + dy * dy);

                // 计算弧高（距离的10-30%）
                double arcHeight = distance * 0.2;

                // 基于ArcDirection参数调整控制点位置
                controlX = midX + perpendicularX * arcHeight * ArcDirection;
                controlY = midY + perpendicularY * arcHeight * ArcDirection;
            }
            else
            {
                // 使用用户输入的控制点，但可能调整方向
                // 计算用户输入点相对于中点的偏移
                double offsetX = PathMidX - midX;
                double offsetY = PathMidY - midY;

                // 计算在垂直方向上的投影
                double dotProduct = offsetX * perpendicularX + offsetY * perpendicularY;

                // 如果投影方向与所需方向相反，则取反
                if (Math.Sign(dotProduct) != Math.Sign(ArcDirection))
                {
                    // 保持相同距离，但取反方向
                    double offsetMagnitude = Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
                    controlX = midX - perpendicularX * offsetMagnitude * Math.Sign(ArcDirection);
                    controlY = midY - perpendicularY * offsetMagnitude * Math.Sign(ArcDirection);
                }
            }

            // 生成曲线点
            for (int i = 0; i <= PathSegmentCount; i++)
            {
                double t = (double)i / PathSegmentCount;

                double x = Math.Pow(1 - t, 2) * PathStartX +
                          2 * (1 - t) * t * controlX +
                          Math.Pow(t, 2) * PathEndX;

                double y = Math.Pow(1 - t, 2) * PathStartY +
                          2 * (1 - t) * t * controlY +
                          Math.Pow(t, 2) * PathEndY;

                AddPathPoint(i + 1, x, y);
            }
        }
        private void GenerateLinePath()
        {
            // 直线路径：从起点到终点均匀分布
            for (int i = 0; i <= PathSegmentCount; i++)
            {
                double t = (double)i / PathSegmentCount;
                double x = PathStartX + (PathEndX - PathStartX) * t;
                double y = PathStartY + (PathEndY - PathStartY) * t;

                AddPathPoint(i + 1, x, y);
            }
        }

        private void GenerateSplinePath()
        {
            // 简单样条曲线（三次样条）
            for (int i = 0; i <= PathSegmentCount; i++)
            {
                double t = (double)i / PathSegmentCount;

                // 简化的三次样条
                double x = PathStartX * (1 - 3 * t + 3 * t * t - t * t * t) +
                          PathMidX * (3 * t - 6 * t * t + 3 * t * t * t) +
                          PathEndX * (3 * t * t - 3 * t * t * t) +
                          PathMidX * (t * t * t); // 重复使用中点作为第四个控制点

                double y = PathStartY * (1 - 3 * t + 3 * t * t - t * t * t) +
                          PathMidY * (3 * t - 6 * t * t + 3 * t * t * t) +
                          PathEndY * (3 * t * t - 3 * t * t * t) +
                          PathMidY * (t * t * t);

                AddPathPoint(i + 1, x, y);
            }
        }

        private void GenerateCirclePath()
        {
            // 圆形路径（以中点为中心）
            double centerX = PathMidX;
            double centerY = PathMidY;
            double radiusX = Math.Abs(PathEndX - PathStartX) / 2;
            double radiusY = Math.Abs(PathEndY - PathStartY) / 2;

            for (int i = 0; i <= PathSegmentCount; i++)
            {
                double angle = 2 * Math.PI * i / PathSegmentCount;
                double x = centerX + radiusX * Math.Cos(angle);
                double y = centerY + radiusY * Math.Sin(angle);

                AddPathPoint(i + 1, x, y);
            }
        }

        private void AddPathPoint(int index, double x, double y)
        {
            var point = new PathPoint
            {
                Index = index,
                X = Math.Round(x, 3),
                Y = Math.Round(y, 3)
            };

            // 计算段长
            if (GeneratedPathPoints.Count > 0)
            {
                var prevPoint = GeneratedPathPoints.Last();
                double dx = point.X - prevPoint.X;
                double dy = point.Y - prevPoint.Y;
                point.SegmentLength = Math.Round(Math.Sqrt(dx * dx + dy * dy), 3);
                point.AccumulatedLength = Math.Round(prevPoint.AccumulatedLength + point.SegmentLength, 3);
            }
            else
            {
                point.SegmentLength = 0;
                point.AccumulatedLength = 0;
            }

            GeneratedPathPoints.Add(point);
        }

        #endregion

        #region 拍照
        private async void MoveToPhotoPositionAsync()
        {
            try
            {
                InterpolationStatus = "正在移动到拍照位置...";
                InterpolationStatusColor = Brushes.Yellow;

                // 检查点胶站服务是否可用
                if (_dispenserStation == null)
                {
                    InterpolationStatus = "错误: 点胶站服务不可用";
                    InterpolationStatusColor = Brushes.Red;
                    return;
                }

                bool safePositionResult = await Task.Run(() => _dispenserStation.ReturnToSafePositionAsync());

                if (!safePositionResult)
                {
                    InterpolationStatus = "返回到安全位置失败";
                    InterpolationStatusColor = Brushes.Red;
                    return;
                }

                bool moveSuccess = await Task.Run(() => _dispenserStation.MoveToCurvedPathPhotoPosAsync(SelectedIndex));

                if (moveSuccess)
                {
                    InterpolationStatus = $"已移动到序号{SelectedIndex}的拍照位置";
                    InterpolationStatusColor = Brushes.LightGreen;
                }
                else
                {
                    InterpolationStatus = "移动到拍照位置失败";
                    InterpolationStatusColor = Brushes.Red;
                }
            }
            catch (OperationCanceledException)
            {
                InterpolationStatus = "移动操作已取消";
                InterpolationStatusColor = Brushes.Orange;
            }
            catch (Exception ex)
            {
                InterpolationStatus = $"移动失败: {ex.Message}";
                InterpolationStatusColor = Brushes.Red;
            }
        }
        private async void MoveToStartPoint()
        {
            try
            {
                InterpolationStatus = "正在移动到起始点...";
                InterpolationStatusColor = Brushes.Yellow;

                // 检查点胶站服务是否可用
                if (_dispenserStation == null)
                {
                    InterpolationStatus = "错误: 点胶站服务不可用";
                    InterpolationStatusColor = Brushes.Red;
                    return;
                }
                double moveSpeed = 30;
                // 获取第一个点
                var firstPoint = NeedlePathPoints[0];
                double firstX = firstPoint.NeedleX;
                double firstY = firstPoint.NeedleY;
                await Task.Run(() => _dispenserStation.MoveToTargetPositionAsync(firstX, firstY, moveSpeed));
            }
            catch (OperationCanceledException)
            {
                InterpolationStatus = "移动操作已取消";
                InterpolationStatusColor = Brushes.Orange;
            }
            catch (Exception ex)
            {
                InterpolationStatus = $"移动失败: {ex.Message}";
                InterpolationStatusColor = Brushes.Red;
            }
        }
        private async void TakeCurvedPathPhoto(int triggerNumber = 1)
        {
            try
            {
                InterpolationStatus = $"正在拍照(触发{triggerNumber})...";
                InterpolationStatusColor = Brushes.Yellow;

                // 检查相机服务是否可用
                if (_cameraController == null)
                {
                    InterpolationStatus = "错误: 相机服务不可用";
                    InterpolationStatusColor = Brushes.Red;
                    return;
                }

                string cameraName = "DispensingCamera";
                string photoCommand = $"Glue{SelectedIndex}_{triggerNumber}";

                // 1. 先启动等待视觉处理完成的任务
                InterpolationStatus = "启动视觉处理等待任务...";
                var visionWaitTask = _visionDataService.WaitForVisionDataAsync(cameraName, 30000);

                // 2. 触发拍照
                bool photoSuccess = await Task.Run(() => _cameraController.TakePhotoAsync(cameraName, photoCommand));

                if (!photoSuccess)
                {
                    InterpolationStatus = "拍照失败";
                    InterpolationStatusColor = Brushes.Red;
                    return;
                }

                // 3. 等待之前启动的视觉处理任务完成
                InterpolationStatus = "等待视觉处理...";
                string visionData = await visionWaitTask;

                if (string.IsNullOrEmpty(visionData) || !visionData.Contains("SUCCESS"))
                {
                    InterpolationStatus = "视觉处理失败";
                    InterpolationStatusColor = Brushes.Red;
                    return;
                }

                // 4. 解析视觉数据
                var visionResult = ParseVisionData(visionData);

                if (!visionResult.Success)
                {
                    InterpolationStatus = $"视觉处理失败: {visionResult.Message}";
                    InterpolationStatusColor = Brushes.Red;
                    return;
                }

                // 5. 更新相机中心坐标和三个控制点坐标
                CameraCenterX = Math.Round(visionResult.CenterX, 3);
                CameraCenterY = Math.Round(visionResult.CenterY, 3);

                // 检查是否有足够的点
                if (visionResult.Points.Count < 3)
                {
                    InterpolationStatus = $"视觉数据点数不足，需要至少3个点，实际{visionResult.Points.Count}个";
                    InterpolationStatusColor = Brushes.Red;
                    return;
                }

                var point1 = visionResult.Points[0];
                var point2 = visionResult.Points[1];
                var point3 = visionResult.Points[2];

                PathStartX = Math.Round(point1.X, 3);
                PathStartY = Math.Round(point1.Y, 3);
                PathMidX = Math.Round(point2.X, 3);
                PathMidY = Math.Round(point2.Y, 3);
                PathEndX = Math.Round(point3.X, 3);
                PathEndY = Math.Round(point3.Y, 3);

                // 6. 自动生成轨迹
                GeneratedPathPoints.Clear();
                GenerateBezierPath();

                InterpolationStatus = $"拍照完成，已更新相机中心({CameraCenterX:F3}, {CameraCenterY:F3})和三个控制点坐标 (序号{SelectedIndex})";
                InterpolationStatusColor = Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                InterpolationStatus = $"拍照失败(触发{triggerNumber}): {ex.Message}";
                InterpolationStatusColor = Brushes.Red;
            }
        }
        #endregion

        #region 视觉数据处理
        /// <summary>
        /// 解析视觉数据
        /// </summary>
        private VisionResult ParseVisionData(string rawData)
        {
            var result = new VisionResult
            {
                RawData = rawData,
                Success = false,
                Camera = "",
                CenterX = 0,
                CenterY = 0,
                Points = new List<PointResult>()
            };

            try
            {
                if (string.IsNullOrWhiteSpace(rawData))
                {
                    result.Message = "原始数据为空";
                    return result;
                }

                // 1. 解析相机名称
                int cameraStartIndex = rawData.IndexOf("Camera=", StringComparison.OrdinalIgnoreCase);
                if (cameraStartIndex >= 0)
                {
                    int cameraEndIndex = rawData.IndexOf(";", cameraStartIndex);
                    if (cameraEndIndex > cameraStartIndex)
                    {
                        result.Camera = rawData.Substring(cameraStartIndex + 7, cameraEndIndex - cameraStartIndex - 7).Trim();
                    }
                }

                // 2. 检查是否成功
                int resultIndex = rawData.IndexOf("VISION_RESULT:", StringComparison.OrdinalIgnoreCase);
                if (resultIndex < 0)
                {
                    result.Message = "未找到VISION_RESULT标识";
                    return result;
                }

                string resultPart = rawData.Substring(resultIndex);

                // 分割字符串，注意可能有多个冒号
                string[] resultSegments = resultPart.Split(':');

                if (resultSegments.Length < 2)
                {
                    result.Message = "结果格式不正确";
                    return result;
                }

                // 检查结果状态
                string status = resultSegments[1].Trim().ToUpper();
                result.Success = status == "SUCCESS";

                if (!result.Success)
                {
                    result.Message = $"视觉检测失败: {status}";
                    return result;
                }

                // 3. 解析数据段（合并剩余部分）
                string dataSegment = "";
                if (resultSegments.Length >= 3)
                {
                    // 合并第2个之后的所有段
                    dataSegment = string.Join(":", resultSegments, 2, resultSegments.Length - 2);
                }

                // 调试信息：输出数据段内容
                Debug.WriteLine($"数据段内容: {dataSegment}");

                // 4. 解析数据段
                ParsePointsFromDataSegment(dataSegment, ref result);

                return result;
            }
            catch (Exception ex)
            {
                result.Message = $"解析视觉数据异常: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// 从数据段中解析点数据和相机中心数据
        /// 数据段格式: "centerX=-6.653,centerY=594.332,point1X=3.07,point1Y=594.731,point2X=-5.411,point2Y=594.29,point3X=-14.065,point3Y=596.048"
        /// </summary>
        private void ParsePointsFromDataSegment(string dataSegment, ref VisionResult result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dataSegment))
                {
                    result.Message = "数据段为空";
                    return;
                }

                // 去除可能的前后空格和引号
                dataSegment = dataSegment.Trim().Trim('"', '\'');

                // 调试：输出清理后的数据段
                Debug.WriteLine($"清理后的数据段: {dataSegment}");

                string[] pairs = dataSegment.Split(',');

                // 调试：输出分割后的键值对数量
                Debug.WriteLine($"键值对数量: {pairs.Length}");

                Dictionary<string, double> values = new Dictionary<string, double>();

                foreach (string pair in pairs)
                {
                    string[] keyValue = pair.Split('=');
                    if (keyValue.Length == 2)
                    {
                        string key = keyValue[0].Trim().ToLower();
                        string valueStr = keyValue[1].Trim();

                        Debug.WriteLine($"解析键值对: {key}={valueStr}");

                        // 尝试解析数值
                        if (TryParseDouble(valueStr, out double value))
                        {
                            values[key] = value;
                            Debug.WriteLine($"成功解析: {key} = {value}");
                        }
                        else
                        {
                            Debug.WriteLine($"解析失败: {key}={valueStr}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"格式不正确: {pair}");
                    }
                }

                // 解析相机中心坐标
                bool hasCenterX = false;
                bool hasCenterY = false;

                if (values.ContainsKey("centerx"))
                {
                    result.CenterX = values["centerx"];
                    hasCenterX = true;
                    Debug.WriteLine($"设置CenterX: {result.CenterX}");
                }
                else
                {
                    Debug.WriteLine("未找到centerx键");
                }

                if (values.ContainsKey("centery"))
                {
                    result.CenterY = values["centery"];
                    hasCenterY = true;
                    Debug.WriteLine($"设置CenterY: {result.CenterY}");
                }
                else
                {
                    Debug.WriteLine("未找到centery键");
                }

                // 查找所有点
                int pointIndex = 1;
                while (true)
                {
                    string pointXKey = $"point{pointIndex}x";
                    string pointYKey = $"point{pointIndex}y";

                    if (values.ContainsKey(pointXKey) && values.ContainsKey(pointYKey))
                    {
                        var pointResult = new PointResult
                        {
                            PointIndex = pointIndex,
                            X = values[pointXKey],
                            Y = values[pointYKey]
                        };

                        result.Points.Add(pointResult);
                        Debug.WriteLine($"添加点{pointIndex}: ({pointResult.X}, {pointResult.Y})");
                        pointIndex++;
                    }
                    else
                    {
                        Debug.WriteLine($"未找到点{pointIndex}的坐标，停止查找");
                        break;
                    }
                }

                // 如果没有找到点，尝试检查是否有OffsetX/Y等字段
                if (result.Points.Count == 0)
                {
                    // 检查是否有传统格式的偏移量
                    if (values.ContainsKey("offsetx"))
                        result.OffsetX = values["offsetx"];
                    if (values.ContainsKey("offsety"))
                        result.OffsetY = values["offsety"];
                    if (values.ContainsKey("offsetx2"))
                        result.OffsetX2 = values["offsetx2"];
                    if (values.ContainsKey("offsety2"))
                        result.OffsetY2 = values["offsety2"];
                    if (values.ContainsKey("offsetu"))
                        result.OffsetU = values["offsetu"];
                    if (values.ContainsKey("offseth"))
                        result.OffsetH = values["offseth"];
                }

                result.Message = $"成功解析到相机中心({result.CenterX:F3}, {result.CenterY:F3})和 {result.Points.Count} 个点";
                InterpolationStatus = result.Message;
                Debug.WriteLine(result.Message);
            }
            catch (Exception ex)
            {
                result.Message = $"解析点数据失败: {ex.Message}";
                Debug.WriteLine($"解析异常: {ex}");
            }
        }

        /// <summary>
        /// 尝试解析双精度浮点数，支持各种格式
        /// </summary>
        private bool TryParseDouble(string valueStr, out double value)
        {
            value = 0;

            // 尝试常规解析
            if (double.TryParse(valueStr, out value))
                return true;

            // 尝试使用不变文化解析
            if (double.TryParse(valueStr,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value))
                return true;

            // 尝试处理科学计数法
            try
            {
                if (valueStr.Contains("e", StringComparison.OrdinalIgnoreCase))
                {
                    value = double.Parse(valueStr,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch
            {
                // 继续尝试其他方法
            }

            // 尝试使用点作为小数分隔符（不变文化）
            if (double.TryParse(valueStr.Replace(",", "."),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value))
                return true;

            return false;
        }
        #endregion

    }
}

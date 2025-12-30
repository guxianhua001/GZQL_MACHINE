
using OpenCvSharp.XImgProc;
using OpenCvSharp;
using Prism.Ioc;
using Prism.Regions;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Prism.Commands;
using ModuleCore.Services;
using System;
using Microsoft.Win32;
using OpenCvSharp.WpfExtensions;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.Threading.Tasks;
using Interfaces.Services;
using Interfaces;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Windows.Threading;
using System.Collections.Generic;
using MaterialDesignThemes.Wpf;
using System.Timers;
using System.Linq;
using Basler.Pylon;
using Stations;
using Core.Utilities;
using Framework.Mvvm;

namespace Framework.ViewModels
{
    public class CareRayViewModel : RegionViewModelBase
    {
        private readonly CareRayService _careRayService = new CareRayService();  // 平板探测器1和2共用同一个服务实例，但通过_currentDevice区分操作的是哪一个平板探测器
        private readonly IRaySourceCommunicationService _communicationService1;   // 射线源通信服务接口1
        private readonly IRaySourceCommunicationService _communicationService2;   // 射线源通信服务接口2

        public bool IsConnected1 { get; set; }
        public bool IsConnected2 { get; set; }

        private string _ViewName;
        public string ViewName
        {
            get { return _ViewName; }
            set { SetProperty(ref _ViewName, value); }
        }
        // 添加图像处理帮助类
        private class ImageUpdate
        {
            public WriteableBitmap Image { get; set; }
            public int DeviceId { get; set; }
        }

        // 添加原始图像缓存
        private WriteableBitmap _rawDevice1Image;
        private WriteableBitmap _rawDevice2Image;
        private WriteableBitmap _device1Image;
        public WriteableBitmap Device1Image
        {
            get => _device1Image;
            set => Application.Current.Dispatcher.Invoke(() =>
                SetProperty(ref _device1Image, value));
        }

        private WriteableBitmap _device2Image;
        public WriteableBitmap Device2Image
        {
            get => _device2Image;
            set => Application.Current.Dispatcher.Invoke(() =>
                SetProperty(ref _device2Image, value));
        }
        // 探测器
        private int _currentDevice = 1; // 当前操作的平板ID

        public ObservableCollection<string> DeviceList => new ObservableCollection<string>
        {
            "平板1#",
            "平板2#"
        };

        private string _selectedDevice = "平板1#";
        public string SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (SetProperty(ref _selectedDevice, value))
                {
                    // 设备切换时更新当前平板ID
                    _currentDevice = value == "平板1#" ? 1 : 2;

                    // 同步加载模式列表
                    if (_careRayService.IsConnected(_currentDevice))
                        LoadModes();
                }
            }
        }
        // 实现 LoadModes 方法
        private void LoadModes()
        {
            ModeItems.Clear();

            // 从服务获取当前设备的模式
            var modes = _careRayService.GetSupportedModes(_currentDevice);

            // 转换为UI可显示格式
            foreach (var mode in modes)
            {
                ModeItems.Add($"{mode.desc} (ID: {mode.mode_id})");
            }

            if (ModeItems.Any())
            {
                SelectedMode = ModeItems.First();
            }
        }

        // 连接命令
        public ICommand ConnectCommand => new DelegateCommand(async () =>
        {
            await _careRayService.ConnectAsync(_currentDevice);
            LoadModes(); // 连接成功后加载模式
        });

        // 在服务类中添加获取模式的方法
        public IReadOnlyList<CrModeInfo> GetSupportedModes(int deviceId)
        {
            return _careRayService.GetSupportedModes(deviceId);
        }
        // 放射源1属性
        private RaySourceStatus _xray1Status = new();
        public RaySourceStatus XRay1Status
        {
            get => _xray1Status;
            set => SetProperty(ref _xray1Status, value);
        }

        // 放射源1状态
        private bool _xRay1IsOn;
        public bool XRay1IsOn
        {
            get => _xRay1IsOn;
            set => SetProperty(ref _xRay1IsOn, value);
        }

        // 放射源2状态
        private bool _xRay2IsOn;
        public bool XRay2IsOn
        {
            get => _xRay2IsOn;
            set => SetProperty(ref _xRay2IsOn, value);
        }

        // 放射源1电压
        private int _xRay1Voltage = 120;
        public int XRay1Voltage
        {
            get => _xRay1Voltage;
            set => SetProperty(ref _xRay1Voltage, value);
        }

        // 放射源1电流
        private int _xRay1Current = 1000;
        public int XRay1Current
        {
            get => _xRay1Current;
            set => SetProperty(ref _xRay1Current, value);
        }

        // 放射源2属性
        private RaySourceStatus _xray2Status = new();
        public RaySourceStatus XRay2Status
        {
            get => _xray2Status;
            set => SetProperty(ref _xray2Status, value);
        }
        // 放射源2电压
        private int _xRay2Voltage = 120;
        public int XRay2Voltage
        {
            get => _xRay2Voltage;
            set => SetProperty(ref _xRay2Voltage, value);
        }

        // 放射源2电流
        private int _xRay2Current = 1000;
        public int XRay2Current
        {
            get => _xRay2Current;
            set => SetProperty(ref _xRay2Current, value);
        }
        // 窗位属性修改时触发更新
        private double _windowLevel1 = 1500;
        public double WindowLevel1
        {
            get => _windowLevel1;
            set
            {
                if (SetProperty(ref _windowLevel1, value))
                {
                    ApplyWindowLevelToDevice1();
                }
            }
        }

        private double _windowWidth1 = 400;
        public double WindowWidth1
        {
            get => _windowWidth1;
            set
            {
                if (SetProperty(ref _windowWidth1, value))
                {
                    ApplyWindowLevelToDevice1();
                }
            }
        }

        private double _windowLevel2 = 1500;
        public double WindowLevel2
        {
            get => _windowLevel2;
            set
            {
                if (SetProperty(ref _windowLevel2, value))
                {
                    ApplyWindowLevelToDevice2();
                }
            }
        }

        private double _windowWidth2 = 400;
        public double WindowWidth2
        {
            get => _windowWidth2;
            set
            {
                if (SetProperty(ref _windowWidth2, value))
                {
                    ApplyWindowLevelToDevice2();
                }
            }
        }
        // 模式列表
        public ObservableCollection<string> ModeItems { get; } = new ObservableCollection<string>();

        private string _selectedMode;
        public string SelectedMode
        {
            get => _selectedMode;
            set => SetProperty(ref _selectedMode, value);
        }

        // 使用 FreezableCollection 替代普通集合
        private FreezableCollection<WriteableBitmap> _debugImages = new FreezableCollection<WriteableBitmap>();
        public FreezableCollection<WriteableBitmap> DebugImages
        {
            get => _debugImages;
            private set => SetProperty(ref _debugImages, value);
        }

        private WriteableBitmap _currentImage;
        public WriteableBitmap CurrentImage
        {
            get => _currentImage;
            private set => SetProperty(ref _currentImage, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }
        // 设备连接状态指示
        public bool IsDevice1Connected => _careRayService.IsConnected(1);
        public bool IsDevice2Connected => _careRayService.IsConnected(2);

        // 当连接状态变化时刷新指示器
        private void RaiseConnectionStatusChanged()
        {
            RaisePropertyChanged(nameof(IsDevice1Connected));
            RaisePropertyChanged(nameof(IsDevice2Connected));
        }
        // 帧数设置
        private int _acquisitionFrameCount = 1;
        public int AcquisitionFrameCount
        {
            get => _acquisitionFrameCount;
            set => SetProperty(ref _acquisitionFrameCount, value);
        }
        // UI显示属性
        public string StatusMessage => _statusMessage;
        private string _statusMessage = "未连接";

        private ObservableCollection<ImageSource> _fluoroImages = new();
        public ObservableCollection<ImageSource> FluoroImages
        {
            get => _fluoroImages;
            set => SetProperty(ref _fluoroImages, value);
        }
        // XRay 控制命令
        public DelegateCommand SetXRay1VoltageCommand { get; }
        public DelegateCommand SetXRay1CurrentCommand { get; }
        public DelegateCommand SetXRay2VoltageCommand { get; }
        public DelegateCommand SetXRay2CurrentCommand { get; }
        // 命令列表
        public DelegateCommand DisconnectCommand { get; }
        public DelegateCommand<string> ControlRaySourceCommand { get; }
        public DelegateCommand StartAcquisitionCommand { get; }
        public DelegateCommand StopAcquisitionCommand { get; }
        public DelegateCommand ShowJudgmentImageCommand { get; }
        public DelegateCommand CaptureImage1Command { get; }
        public DelegateCommand CaptureImage2Command { get; }
        public DelegateCommand ResetXRay1Command { get; }
        public DelegateCommand ResetXRay2Command { get; }
        public DelegateCommand IncreaseVoltageCommand { get; }
        public DelegateCommand DecreaseVoltageCommand { get; }
        public DelegateCommand IncreaseCurrentCommand { get; }
        public DelegateCommand DecreaseCurrentCommand { get; }
        public DelegateCommand SingleCaptureCommand { get; }
        public DelegateCommand ContinuousCaptureCommand { get; }
        public DelegateCommand TestMesConnectionCommand { get; }
        // 窗位窗宽命令
        public DelegateCommand DecreaseWindowLevel1Command { get; }
        public DelegateCommand IncreaseWindowLevel1Command { get; }
        public DelegateCommand DecreaseWindowWidth1Command { get; }
        public DelegateCommand IncreaseWindowWidth1Command { get; }
        public DelegateCommand DecreaseWindowLevel2Command { get; }
        public DelegateCommand IncreaseWindowLevel2Command { get; }
        public DelegateCommand DecreaseWindowWidth2Command { get; }
        public DelegateCommand IncreaseWindowWidth2Command { get; }

        // 日志系统
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();
        // 状态轮询定时器
        private readonly Timer _statusPollingTimer;


        //private readonly Task2 _task2 ;


        private readonly ILoggerService _logger;

        public CareRayViewModel(
            ILoggerService logger,
            IContainerExtension container,
            IRegionManager regionManager,
            IContainerProvider containerProvider,
            TaskInstanceManager taskManager) : base(regionManager)
        {
            _logger = logger;
            _careRayService = new CareRayService();

            // 创建两个独立的通信服务实例
            _communicationService1 = containerProvider.Resolve<IRaySourceCommunicationService>();
            _communicationService2 = containerProvider.Resolve<IRaySourceCommunicationService>();

            // 为每个服务注册事件
            _communicationService1.StatusChanged += OnStatusChanged_XRay1;
            _communicationService1.StatusMessage += OnServiceMessage_XRay1;

            _communicationService2.StatusChanged += OnStatusChanged_XRay2;
            _communicationService2.StatusMessage += OnServiceMessage_XRay2;

            // 初始化命令
            CaptureImage1Command = new DelegateCommand(OnStartCapture1);
            CaptureImage2Command = new DelegateCommand(OnStartCapture2);
            // 初始化复位命令
            ResetXRay1Command = new DelegateCommand(ExecuteResetXRay1, CanResetXRay1);
            ResetXRay2Command = new DelegateCommand(ExecuteResetXRay2, CanResetXRay2);
            DecreaseWindowLevel1Command = new DelegateCommand(() => WindowLevel1 -= 50);
            IncreaseWindowLevel1Command = new DelegateCommand(() => WindowLevel1 += 50);
            DecreaseWindowWidth1Command = new DelegateCommand(() => WindowWidth1 -= 50);
            IncreaseWindowWidth1Command = new DelegateCommand(() => WindowWidth1 += 50);
            DecreaseWindowLevel2Command = new DelegateCommand(() => WindowLevel2 -= 50);
            IncreaseWindowLevel2Command = new DelegateCommand(() => WindowLevel2 += 50);
            DecreaseWindowWidth2Command = new DelegateCommand(() => WindowWidth2 -= 50);
            IncreaseWindowWidth2Command = new DelegateCommand(() => WindowWidth2 += 50);
            SetXRay1VoltageCommand = new DelegateCommand(() => SetXRayParameter(1, "HIV", XRay1Voltage));
            SetXRay1CurrentCommand = new DelegateCommand(() => SetXRayParameter(1, "CUR", XRay1Current));
            SetXRay2VoltageCommand = new DelegateCommand(() => SetXRayParameter(2, "HIV", XRay2Voltage));
            SetXRay2CurrentCommand = new DelegateCommand(() => SetXRayParameter(2, "CUR", XRay2Current));
            // 订阅状态更改事件以更新命令可用性
            XRay1Status.PropertyChanged += (s, e) => ResetXRay1Command.RaiseCanExecuteChanged();
            XRay2Status.PropertyChanged += (s, e) => ResetXRay2Command.RaiseCanExecuteChanged();

            // 初始化射线源状态轮询定时器（每2秒轮询一次）
            _statusPollingTimer = new Timer(2000);
            _statusPollingTimer.Elapsed += OnStatusPollingTimerElapsed;
            _statusPollingTimer.AutoReset = true;
            _statusPollingTimer.Start(); // 启动定时器

            // 连接射线源
            AutoConnectCommand = new DelegateCommand(ExecuteAutoConnect);

            // 开始自动连接
            AutoConnectCommand.Execute();

                //_task2 = taskManager.GetTask<Task2>();

            _logger.Info("CareRayViewModel 已初始化");
        }
        // 状态轮询定时器事件处理
        private void OnStatusPollingTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // 轮询射线源1状态
            if (IsConnected1 && _communicationService1 != null)
            {
                _communicationService1.SendCommandAsync("STS");
            }

            // 轮询射线源2状态
            if (IsConnected2 && _communicationService2 != null)
            {
                _communicationService2.SendCommandAsync("STS");
            }
        }
        // 设置放射源参数
        private void SetXRayParameter(int rayId, string commandPrefix, int value)
        {
            try
            {
                // 构建命令：HIV 100 或 CUR 500
                string command = $"{commandPrefix} {value}";

                // 发送给对应放射源
                if (rayId == 1)
                {
                    _communicationService1.SendCommandAsync("1", command);
                    LogEntries.Add(new LogEntry($"放射源1# 已设置: {command}", LogEntryLevel.Success));
                }
                else
                {
                    _communicationService2.SendCommandAsync("2", command);
                    LogEntries.Add(new LogEntry($"放射源2# 已设置: {command}", LogEntryLevel.Success));
                }
            }
            catch (Exception ex)
            {
                LogEntries.Add(new LogEntry($"放射源{rayId}设置失败: {ex.Message}", LogEntryLevel.Error));
            }
        }
        // 图像采集
        private void StartFluoroCapture(int detectorIndex, int modeKey, int frameCount)
        {
            Task.Run(() =>
            {
                CareRayOperator.acquisitionFrameCount = frameCount;
                var fluoroBitmaps = CareRayOperator.StartFluoroAcquisition(detectorIndex, modeKey);

                // 在UI线程中更新图像
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (fluoroBitmaps.Count == 0)
                    {
                        LogEntries.Add(new LogEntry($"{GetDetectorName(detectorIndex)}没有获取到图像", LogEntryLevel.Warning));
                        return;
                    }

                    // 将第一帧图像转换为ImageSource
                    var imageSource = fluoroBitmaps[0];

                    // 根据探测器索引设置对应的图像
                    if (detectorIndex == 1)
                        Device1Image = imageSource;
                    else
                        Device2Image = imageSource;
                });
            });
        }

        // 辅助方法：获取探测器名称
        private string GetDetectorName(int detectorIndex) =>
            detectorIndex == 1 ? "平板#1" : "平板#2";

        public void OnStartCapture1()
        {
            //StartFluoroCapture(1, 1, _acquisitionFrameCount);
            //UpdateDevice1Image(Device1Image);
            _logger.Info("版本: 1.0.0.0\r\n  工作目录: C:\\WorkFiles\\GZQL_MACHINE\\MainApp\r\n  系统版本: Microsoft Windows NT 10.0.22631.0\r\n  内存状态: 1.31 MB\r\n  处理器数: 16\r\n  命令行参数: C:\\WorkFiles\\GZQL_MACHINE\\MainApp\\bin\\Debug\\net9.0-windows7.0\\MainApp.dll");
        }
        public void OnStartCapture2()
        {
            StartFluoroCapture(2, 1, _acquisitionFrameCount);
            UpdateDevice1Image(Device2Image);
        }
        // 添加自动连接命令属性
        public DelegateCommand AutoConnectCommand { get; }

        // 射线源连接
        private async void ExecuteAutoConnect()
        {
            LogEntries.Add(new LogEntry("正在建立设备连接...", LogEntryLevel.Info));

            try
            {
                // 使用 Task.WhenAll 同步连接两个射线源
                List<Task> connectTasks = new List<Task>();

                if (!IsConnected1)
                {
                    connectTasks.Add(ConnectToRaySource("XRay1", "COM1", _communicationService1,
                        isConnected => IsConnected1 = isConnected));
                }

                if (!IsConnected2)
                {
                    connectTasks.Add(ConnectToRaySource("XRay2", "COM7", _communicationService2,
                        isConnected => IsConnected2 = isConnected));
                }

                if (connectTasks.Any())
                {
                    await Task.WhenAll(connectTasks);

                    // 启动状态轮询定时器
                    _statusPollingTimer.Start();

                    LogEntries.Add(new LogEntry("设备连接成功，已启动状态轮询", LogEntryLevel.Info));
                }
                else
                {
                    LogEntries.Add(new LogEntry("所有射线源已经连接", LogEntryLevel.Info));
                }
            }
            catch (Exception ex)
            {
                LogEntries.Add(new LogEntry($"自动连接失败: {ex.Message}", LogEntryLevel.Error));
            }
        }

        // 射线源连接辅助方法
        private async Task ConnectToRaySource(string sourceId, string defaultPort,
            IRaySourceCommunicationService service, Action<bool> setConnected)
        {
            try
            {
                // 设置端口名
                service.PortName = defaultPort;

                // 尝试连接
                await service.ConnectAsync().WaitAsync(TimeSpan.FromSeconds(10));

                setConnected(service.IsConnected);

                if (service.IsConnected)
                {
                    LogEntries.Add(new LogEntry($"{sourceId} 连接成功", LogEntryLevel.Info));

                    // 连接后立即查询设备状态
                    await QueryStatusImmediately(sourceId, service);
                }
                else
                {
                    LogEntries.Add(new LogEntry($"{sourceId} 未能建立连接", LogEntryLevel.Warning));
                }
            }
            catch (TimeoutException)
            {
                LogEntries.Add(new LogEntry($"{sourceId} 连接超时，请检查设备是否开机", LogEntryLevel.Warning));
                setConnected(false);
            }
            catch (Exception ex)
            {
                LogEntries.Add(new LogEntry($"{sourceId} 连接异常: {ex.Message}", LogEntryLevel.Error));
                setConnected(false);
            }
        }

        // 立即查询射线源状态
        private async Task QueryStatusImmediately(string sourceId, IRaySourceCommunicationService service)
        {
            try
            {
                // 查询工作状态
                await service.SendCommandAsync("STS");

                // 查询实际电压
                var voltageResponse = await service.SendCommandAsync("SHV");
                if (int.TryParse(voltageResponse, out int voltage))
                {
                    UpdateVoltage(sourceId, voltage);
                }

                // 查询实际电流
                var currentResponse = await service.SendCommandAsync("SCU");
                if (int.TryParse(currentResponse, out int current))
                {
                    UpdateCurrent(sourceId, current);
                }

                LogEntries.Add(new LogEntry($"{sourceId} 状态更新完成", LogEntryLevel.Info));
            }
            catch (Exception ex)
            {
                LogEntries.Add(new LogEntry($"{sourceId} 状态查询失败: {ex.Message}", LogEntryLevel.Error));
            }
        }

        private void UpdateVoltage(string sourceId, int voltage)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (sourceId == "XRay1")
                {
                    XRay1Voltage = voltage;
                    XRay1Status.ActualVoltage = voltage;
                }
                else if (sourceId == "XRay2")
                {
                    XRay2Voltage = voltage;
                    XRay2Status.ActualVoltage = voltage;
                }
            });
        }

        private void UpdateCurrent(string sourceId, int current)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (sourceId == "XRay1")
                {
                    XRay1Current = current;
                    XRay1Status.ActualCurrent = current;
                }
                else if (sourceId == "XRay2")
                {
                    XRay2Current = current;
                    XRay2Status.ActualCurrent = current;
                }
            });
        }
        // 射线源开关状态变更处理
        public async void OnXRaySwitchChanged(string rayName, bool isOn)
        {
            IRaySourceCommunicationService service = null;
            RaySourceStatus status = null;
            string statusVerb = isOn ? "开启" : "关闭";

            try
            {
                // 确定要操作的射线源服务和状态
                if (rayName == "XRay1" && IsConnected1)
                {
                    service = _communicationService1;
                    status = XRay1Status;
                }
                else if (rayName == "XRay2" && IsConnected2)
                {
                    service = _communicationService2;
                    status = XRay2Status;
                }

                // 检查是否有有效的服务和状态
                if (service == null || status == null)
                {
                    LogEntries.Add(new LogEntry($"射线源{rayName}未连接或状态不可用，无法{statusVerb}", LogEntryLevel.Warning));
                    return;
                }

                // ===== 处理开启射线源 =====
                if (isOn)
                {
                    LogEntries.Add(new LogEntry($"正在尝试开启射线源{rayName}", LogEntryLevel.Info));

                    // 1. 检查热机状态是否需要启动热机
                    if (status.State == RaySourceState.Standby &&
                        status.WarmupStatus != WarmupStatus.Complete)
                    {
                        LogEntries.Add(new LogEntry($"射线源{rayName}需要热机", LogEntryLevel.Info));

                        // 启动热机序列
                        //StartWarmupSequence(rayName);

                        // 这里返回，不继续执行开启命令，等热机完成后再自动开启
                        return;
                    }
                    // 2. 检查其他无效状态
                    else if (status.State == RaySourceState.Overloaded ||
                             status.State == RaySourceState.Error ||
                             status.State == RaySourceState.Testing)
                    {
                        LogEntries.Add(new LogEntry($"射线源{rayName}处于{status.State}状态，无法开启", LogEntryLevel.Warning));
                        return;
                    }
                    // 3. 如果已经处于激活状态，无需再次开启
                    else if (status.State == RaySourceState.Active)
                    {
                        LogEntries.Add(new LogEntry($"射线源{rayName}已经处于激活状态", LogEntryLevel.Info));
                        return;
                    }

                    // 发送开启射线源命令
                    var response = await service.SendCommandAsync("XON");

                    if (response == "OK" || response.Contains("成功"))
                    {
                        LogEntries.Add(new LogEntry($"射线源{rayName}开启成功", LogEntryLevel.Info));

                        // 更新状态为激活（实际上状态会从设备反馈更新）
                        status.State = RaySourceState.Active;
                    }
                    else
                    {
                        LogEntries.Add(new LogEntry($"射线源{rayName}开启失败: {response}", LogEntryLevel.Error));
                    }
                }
                // ===== 处理关闭射线源 =====
                else
                {
                    LogEntries.Add(new LogEntry($"正在尝试关闭射线源{rayName}", LogEntryLevel.Info));

                    // 1. 检查是否已经是待机状态
                    if (status.State == RaySourceState.Standby)
                    {
                        LogEntries.Add(new LogEntry($"射线源{rayName}已经处于关闭状态", LogEntryLevel.Info));
                        return;
                    }

                    // 2. 检查某些状态是否不能直接关闭
                    if (status.State == RaySourceState.Testing)
                    {
                        LogEntries.Add(new LogEntry($"射线源{rayName}正在自检中，请等待完成", LogEntryLevel.Warning));
                        return;
                    }

                    // 发送关闭射线源命令
                    var response = await service.SendCommandAsync("XOF");

                    if (response == "OK" || response.Contains("成功"))
                    {
                        LogEntries.Add(new LogEntry($"射线源{rayName}关闭成功", LogEntryLevel.Info));

                        // 更新状态为待机（实际上状态会从设备反馈更新）
                        if (status.WarmupStatus == WarmupStatus.Complete)
                            status.State = RaySourceState.Ready;
                        else
                            status.State = RaySourceState.Standby;
                    }
                    else
                    {
                        LogEntries.Add(new LogEntry($"射线源{rayName}关闭失败: {response}", LogEntryLevel.Error));
                    }
                }
            }
            catch (Exception ex)
            {
                LogEntries.Add(new LogEntry($"射线源{rayName}{statusVerb}操作异常: {ex.Message}", LogEntryLevel.Error));
            }
        }

        private Mat Src;
        private DelegateCommand _loadImageFile;
        public DelegateCommand LoadImageFile =>
               _loadImageFile ??= new DelegateCommand(ExecuteLoadImageFile);

        private void ExecuteLoadImageFile()
        {
            OpenFileDialog ofd = new()
            {
                DefaultExt = ".*",
                Filter = "图像文件(*.jpg;*.png;*.bmp)|*.jpg;*.png;*.bmp"
            };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    Src = Cv2.ImRead(ofd.FileName);
                    CurrentImage = WriteableBitmapConverter.ToWriteableBitmap(Src);
                }
                catch (Exception ex)
                {
                    NLogService.Error(ex.Message);
                }
            }
        }
        private void ExecuteConnect(string sourceId)
        {
            try
            {
                var service = sourceId == "XRay1" ? _communicationService1 : _communicationService2;

                // 设置端口名 - 根据实际情况配置
                var portName = sourceId == "XRay1" ? "COM3" : "COM4";
                service.PortName = portName;

                service.ConnectAsync().Wait(TimeSpan.FromSeconds(5));

                if (sourceId == "XRay1")
                    IsConnected1 = service.IsConnected;
                else
                    IsConnected2 = service.IsConnected;

                if (service.IsConnected)
                {
                    QueryStatus(sourceId);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"连接 {sourceId} 失败: {ex.Message}");
            }
        }

        private void ExecuteDisconnect(string sourceId)
        {
            var service = sourceId == "XRay1" ? _communicationService1 : _communicationService2;
            service.DisconnectAsync().Wait();

            if (sourceId == "XRay1")
                IsConnected1 = false;
            else
                IsConnected2 = false;
        }

        private void OnStatusChanged_XRay1(object sender, RaySourceStatus newStatus)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 更新电压电流显示
                XRay1Voltage = (int)newStatus.ActualVoltage;
                XRay1Current = (int)newStatus.ActualCurrent;

                // 更新射线源状态
                XRay1IsOn = newStatus.State switch
                {
                    RaySourceState.Active => true,
                    RaySourceState.Ready => false,
                    RaySourceState.WarmingUp => false,
                    _ => false,
                };

                // 处理警告/错误状态
                if (newStatus.State == RaySourceState.Overloaded ||
                    newStatus.State == RaySourceState.Error)
                {
                    LogEntries.Add(new LogEntry($"射线源1发生故障: {newStatus.State}", LogEntryLevel.Error));
                }

                // 处理自检状态
                if (newStatus.State == RaySourceState.Testing)
                {
                    LogEntries.Add(new LogEntry("射线源1正在自检...", LogEntryLevel.Info));
                }
                // 检查过载保护
                CheckOverloadProtection("XRay1", _communicationService1, newStatus);
            });
        }

        private void OnStatusChanged_XRay2(object sender, RaySourceStatus newStatus)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 更新射源2状态
                XRay2Status = newStatus;

                // 更新电压电流显示
                XRay2Voltage = (int)newStatus.ActualVoltage;
                XRay2Current = (int)newStatus.ActualCurrent;

                // 更新射线源开启状态
                XRay2IsOn = newStatus.State == RaySourceState.Active ||
                           newStatus.State == RaySourceState.Ready ||
                           newStatus.State == RaySourceState.WarmingUp;

                // 检查过载保护
                CheckOverloadProtection("XRay2", _communicationService2, newStatus);
            });
        }

        private void OnServiceMessage_XRay1(object sender, string message)
        {
            if (Application.Current != null)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 添加前缀标识源
                    LogEntries.Add(new LogEntry($"[XRay1] {message}", LogEntryLevel.Info));
                });
        }

        private void OnServiceMessage_XRay2(object sender, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 添加前缀标识源
                LogEntries.Add(new LogEntry($"[XRay2] {message}", LogEntryLevel.Info));
            });
        }

        private async void QueryStatus(string sourceId)
        {
            try
            {
                IRaySourceCommunicationService service;
                string response;

                if (sourceId == "XRay1")
                {
                    service = _communicationService1;
                    service.SendCommandAsync("STS");

                    // 查询实际电压
                    response = await service.SendCommandAsync("SHV");
                    if (double.TryParse(response, out double vVal1))
                    {
                        XRay1Status.ActualVoltage = vVal1;
                    }

                    // 查询实际电流
                    response = await service.SendCommandAsync("SCU");
                    if (double.TryParse(response, out double cVal1))
                    {
                        XRay1Status.ActualCurrent = cVal1;
                    }
                }
                else
                {
                    service = _communicationService2;
                    service.SendCommandAsync("STS");

                    // 查询实际电压
                    response = await service.SendCommandAsync("SHV");
                    if (double.TryParse(response, out double vVal2))
                    {
                        XRay2Status.ActualVoltage = vVal2;
                    }

                    // 查询实际电流
                    response = await service.SendCommandAsync("SCU");
                    if (double.TryParse(response, out double cVal2))
                    {
                        XRay2Status.ActualCurrent = cVal2;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"查询 {sourceId} 状态失败: {ex.Message}");
            }
        }

        // 在ViewModel中添加
        private void HandleWarmupProgress(RaySourceStatus status, string rayName)
        {
            if (status.WarmupStatus == WarmupStatus.InProgress)
            {
                // 获取热机进度
                double progress = (status.WarmupTimeElapsed / status.WarmupTimeRequired) * 100.0;

                LogEntries.Add(new LogEntry(
                    $"射线源{rayName}热机中: {progress:F1}% ({status.WarmupTimeElapsed}/{status.WarmupTimeRequired}秒)",
                    LogEntryLevel.Info));
            }
            else if (status.WarmupStatus == WarmupStatus.Complete)
            {
                LogEntries.Add(new LogEntry(
                    $"射线源{rayName}热机完成，准备就绪",
                    LogEntryLevel.Info));
            }
        }
        public async void StartWarmupSequence(string rayName)
        {
            IRaySourceCommunicationService service = rayName == "XRay1"
                ? _communicationService1
                : _communicationService2;

            try
            {
                // 开始热机
                var response = await service.SendCommandAsync("WUP");

                if (response == "OK")
                {
                    // 启动定时器监控热机进度
                    var timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(1)
                    };

                    timer.Tick += (s, e) =>
                    {
                        var status = rayName == "XRay1" ? XRay1Status : XRay2Status;
                        status.WarmupTimeElapsed++;

                        if (status.WarmupTimeElapsed >= status.WarmupTimeRequired)
                        {
                            timer.Stop();
                            // 热机完成
                            if (rayName == "XRay1")
                                XRay1Status.WarmupStatus = WarmupStatus.Complete;
                            else
                                XRay2Status.WarmupStatus = WarmupStatus.Complete;
                        }
                    };

                    timer.Start();

                    // 设置热机状态
                    if (rayName == "XRay1")
                        XRay1Status.WarmupStatus = WarmupStatus.InProgress;
                    else
                        XRay2Status.WarmupStatus = WarmupStatus.InProgress;
                }
            }
            catch (Exception ex)
            {
                LogEntries.Add(new LogEntry($"启动{rayName}热机失败: {ex.Message}", LogEntryLevel.Error));
            }
        }
        // 射线源1复位实现
        private async void ExecuteResetXRay1()
        {
            await ExecuteResetOverloadProtection("XRay1");
        }

        private bool CanResetXRay1() => CanResetOverloadProtection(XRay1Status);

        // 射线源2复位实现
        private async void ExecuteResetXRay2()
        {
            await ExecuteResetOverloadProtection("XRay2");
        }

        private bool CanResetXRay2() => CanResetOverloadProtection(XRay2Status);

        // 通用的复位条件检测
        private bool CanResetOverloadProtection(RaySourceStatus status)
        {
            // 只有当达到过载状态时才允许复位
            return status.State == RaySourceState.Overloaded ||
                   status.State == RaySourceState.Error;
        }
        // 通用的过载保护复位执行
        private async Task ExecuteResetOverloadProtection(string rayName)
        {
            string resetResult = "";
            try
            {
                // 确定要操作的射线源服务
                IRaySourceCommunicationService service = null;
                RaySourceStatus status = null;

                if (rayName == "XRay1")
                {
                    service = _communicationService1;
                    status = XRay1Status;
                }
                else if (rayName == "XRay2")
                {
                    service = _communicationService2;
                    status = XRay2Status;
                }

                // 验证服务和状态
                if (service == null || status == null)
                {
                    LogEntries.Add(new LogEntry($"无法复位射线源{rayName}: 服务或状态无效", LogEntryLevel.Error));
                    return;
                }

                // 发送复位命令
                string command = "RST";
                LogEntries.Add(new LogEntry($"正在尝试复位{rayName}过载保护...", LogEntryLevel.Warning));

                var response = await service.SendCommandAsync(command);

                if (response == "OK")
                {
                    LogEntries.Add(new LogEntry($"{rayName}过载保护已复位成功", LogEntryLevel.Info));

                    // 更新状态为待机
                    status.State = RaySourceState.Standby;

                    // 重启自动状态读取
                    if (!_statusPollingTimer.Enabled)
                    {
                        _statusPollingTimer.Start();
                    }

                    resetResult = "ResetSuccessful";
                }
                else
                {
                    // 根据错误代码提供更多细节
                    if (response == "ERR 13")
                    {
                        LogEntries.Add(new LogEntry($"{rayName}复位失败: 内部硬件故障", LogEntryLevel.Error));
                    }
                    else
                    {
                        LogEntries.Add(new LogEntry($"{rayName}复位失败: {response}", LogEntryLevel.Error));
                    }

                    resetResult = "ResetFailed";
                }
            }
            catch (Exception ex)
            {
                LogEntries.Add(new LogEntry($"{rayName}复位操作异常: {ex.Message}", LogEntryLevel.Error));
                resetResult = "ExceptionOccurred";
            }
            finally
            {
                // 复位后自动重新查询状态
                if (_communicationService1 != null)
                {
                    await _communicationService1.SendCommandAsync("sts");
                }
                if (_communicationService2 != null)
                {
                    await _communicationService2.SendCommandAsync("sts");
                }
            }
        }

        // 过载保护检测逻辑
        public void CheckOverloadProtection(string rayName, IRaySourceCommunicationService service, RaySourceStatus status)
        {
            // 检查电压是否超出安全范围（300kV为最大安全电压）
            if (status.SetVoltage > 300.0)
            {
                LogEntries.Add(new LogEntry($"{rayName}: 设置电压 {status.SetVoltage} kV 超出安全范围!",
                    LogEntryLevel.Warning));
                EnterOverloadProtection(rayName, status);
            }

            // 检查电流是否超出限制（10mA = 10,000 μA）
            if (status.ActualCurrent > status.SetCurrent * 1.2) // 超过设定值的120%
            {
                LogEntries.Add(new LogEntry($"{rayName}: 实际电流 {status.ActualCurrent} μA 超过设定值 20%!",
                    LogEntryLevel.Warning));
                EnterOverloadProtection(rayName, status);
            }
        }

        // 进入过载保护状态
        private void EnterOverloadProtection(string rayName, RaySourceStatus status)
        {
            if (status.State != RaySourceState.Overloaded)
            {
                // 切换到过载/错误状态
                string displayStatus = status.State == RaySourceState.Error ? "错误" : "过载保护";

                LogEntries.Add(new LogEntry($"⚠️ {rayName}进入{displayStatus}状态，需要人工复位!",
                    LogEntryLevel.Error));

                status.State = RaySourceState.Overloaded;

                // 停止当前发射状态
                if (rayName == "XRay1") _xRay1IsOn = false;
                if (rayName == "XRay2") _xRay2IsOn = false;

                // 停止状态轮询定时器以节省资源
                if (_statusPollingTimer != null && _statusPollingTimer.Enabled)
                {
                    _statusPollingTimer.Stop();
                }

                // 发出UI警告
                ShowOverloadWarningPopup(rayName);
            }
        }
        // 创建警告通知
        private void ShowOverloadWarningPopup(string rayName)
        {
            var result = Framework.Services.DialogService.ShowBlockingDialog(
                   title: "射线源紧急停止!",
                   message: $"射线源 {rayName} 触发过载保护 + \r\n",
                   yesButtonText: "复位设备",
                   noButtonText: "关闭警告",
                   extraButtonText: "",
                   showExtraButton: false,
                   icon: PackIconKind.ClockAlert
                 );
            switch (result)
            {
                case 0:
                    if (rayName == "XRay1") ExecuteResetXRay1();
                    else if (rayName == "XRay2") ExecuteResetXRay2();
                    break;

                case 1:
                    if (rayName == "XRay1") ExecuteResetXRay1();
                    else if (rayName == "XRay2") ExecuteResetXRay2();
                    break;
                case -1: // 对话框被关闭/取消
                    Console.WriteLine("操作取消");
                    break;
            }
        }

        private int ExtractModeId(string modeString)
        {
            // 从 "描述文字 (ID: 123)" 中提取数字123
            var idStart = modeString.IndexOf("ID: ") + 4;
            var idEnd = modeString.IndexOf(")", idStart);
            if (idStart >= 4 && idEnd > idStart)
            {
                var idString = modeString.Substring(idStart, idEnd - idStart);
                if (int.TryParse(idString, out int modeId))
                {
                    return modeId;
                }
            }
            return -1;
        }
        // 初始化探测器设备
        private async Task InitializeDetectorDevices()
        {
            try
            {
                // 同时启动两个设备的连接
                var connectTask1 = ConnectDeviceAsync(1);
                var connectTask2 = ConnectDeviceAsync(2);

                // 等待两个设备连接完成（或失败）
                await Task.WhenAll(connectTask1, connectTask2);
            }
            catch (Exception ex)
            {
                LogEntries.Add(new LogEntry($"⚠️探测器初始化失败: {ex.Message}",
             LogEntryLevel.Error));
            }
        }
        private async Task<bool> ConnectDeviceAsync(int deviceId)
        {
            try
            {
                var service = _careRayService;
                await service.ConnectAsync(deviceId);
                if (service.IsConnected(deviceId))
                {
                    LogEntries.Add(new LogEntry($"探测器{deviceId}连接成功",
                               LogEntryLevel.Success));
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                LogEntries.Add(new LogEntry($"⚠️探测器 {deviceId} 连接失败: {ex.Message}",
                              LogEntryLevel.Error));
                return false;
            }
        }

        // 更新图像处理方法
        private void UpdateDevice1Image(WriteableBitmap rawImage)
        {
            _rawDevice1Image = rawImage;
            ApplyWindowLevelToDevice1();
        }

        private void UpdateDevice2Image(WriteableBitmap rawImage)
        {
            _rawDevice2Image = rawImage;
            ApplyWindowLevelToDevice2();
        }
        // 应用窗位窗宽到设备1
        private void ApplyWindowLevelToDevice1()
        {
            if (_rawDevice1Image == null) return;

            // 创建副本以避免并发修改
            var processed = new WriteableBitmap(_rawDevice1Image);
            WindowLevelProcessor.ApplyWindowLevel(processed, (int)WindowLevel1, (int)WindowWidth1);
            Device1Image = processed;
        }

        // 应用窗位窗宽到设备2
        private void ApplyWindowLevelToDevice2()
        {
            if (_rawDevice2Image == null) return;

            var processed = new WriteableBitmap(_rawDevice2Image);
            WindowLevelProcessor.ApplyWindowLevel(processed, (int)WindowLevel2, (int)WindowWidth2);
            Device2Image = processed;
        }
        // ViewModel析构函数 - 清理资源
        ~CareRayViewModel()
        {
            _statusPollingTimer?.Stop();
            _statusPollingTimer?.Dispose();

            if (_communicationService1 != null)
            {
                _communicationService1.StatusChanged -= OnStatusChanged_XRay1;
                _communicationService1.StatusMessage -= OnServiceMessage_XRay1;
            }

            if (_communicationService2 != null)
            {
                _communicationService2.StatusChanged -= OnStatusChanged_XRay2;
                _communicationService2.StatusMessage -= OnServiceMessage_XRay2;
            }
        }
    }
}

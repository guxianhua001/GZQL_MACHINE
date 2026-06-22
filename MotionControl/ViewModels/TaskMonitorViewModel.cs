using Core.Abstraction;
using Core.Events;
using MotionControl.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
namespace MotionControl.ViewModels
{
    public class TaskMonitorViewModel : BindableBase, IDisposable
    {
        private readonly IEventAggregator _ea;
        private readonly IStationRegistry _stationRegistry;
        private readonly ILocalizationService _localization;
        private readonly IAppSettingService _appSettingService;
        private DispatcherTimer _sharedTimer;
        private bool _disposed;

        /// <summary>持久化存储键：TaskMonitor 卡片宽度</summary>
        private const string CardWidthSettingKey = "TaskMonitorCardWidth";

        public ObservableCollection<TaskDisplayModel> Tasks { get; } = new();

        private int _cardWidth = 170;
        /// <summary>
        /// 紧凑卡片宽度（控制布局密度）。
        /// 默认 170=2列(380px栏宽)，115=3列，85=4列极简模式，360=竖向单列。
        /// 通过 WrapPanel.ItemWidth 绑定实现自动换行。
        /// 值变更时持久化到 appsettings.JSON，重启后保留用户布局偏好。
        /// </summary>
        public int CardWidth
        {
            get => _cardWidth;
            set
            {
                if (SetProperty(ref _cardWidth, value))
                {
                    SaveCardWidthSetting(value);
                }
            }
        }

        /// <summary>
        /// 切换布局密度命令。参数为卡片宽度字符串（"170"=2列, "115"=3列, "85"=4列, "360"=竖向单列）。
        /// 使用 string 类型参数以兼容 DelegateCommand&lt;T&gt; 的引用类型约束及 XAML CommandParameter 字符串传递。
        /// </summary>
        public DelegateCommand<string> SetCardWidthCommand { get; }

        public TaskMonitorViewModel(IEventAggregator ea, IStationRegistry stationRegistry, ILocalizationService localization,
            IAppSettingService appSettingService)
        {
            _ea = ea;
            _stationRegistry = stationRegistry;
            _localization = localization;
            _appSettingService = appSettingService;

            SetCardWidthCommand = new DelegateCommand<string>(OnSetCardWidth);

            // 从持久化配置加载用户上次的布局偏好
            LoadCardWidthSetting();

            _ea.GetEvent<TaskStatusChangedEvent>().Subscribe(OnTaskStatusChanged, ThreadOption.PublisherThread, true);
            _ea.GetEvent<StationRegisteredEvent>().Subscribe(OnStationRegistered, ThreadOption.PublisherThread, true);
            _ea.GetEvent<StationUnregisteredEvent>().Subscribe(OnStationUnregistered, ThreadOption.PublisherThread, true);
            // 订阅工站初始化进度事件，更新各工站回零进度显示
            _ea.GetEvent<StationInitProgressEvent>().Subscribe(OnInitProgressChanged, ThreadOption.PublisherThread, true);

            // 初始化共享定时器，替代各模型独立定时器，降低 N 倍定时器开销
            InitializeSharedTimer();
            LoadTasks();
        }

        /// <summary>设置卡片宽度（布局密度切换）</summary>
        /// <param name="widthStr">目标宽度字符串："170"=2列, "115"=3列, "85"=4列, "360"=竖向单列</param>
        private void OnSetCardWidth(string widthStr)
        {
            if (int.TryParse(widthStr, out int width))
            {
                CardWidth = width;
            }
        }

        /// <summary>
        /// 从 appsettings.JSON 的 ExtensionData 读取持久化的卡片宽度。
        /// 读取失败或无记录时使用默认值 170（2列）。
        /// </summary>
        private void LoadCardWidthSetting()
        {
            try
            {
                if (_appSettingService?.Settings?.ExtensionData != null
                    && _appSettingService.Settings.ExtensionData.TryGetValue(CardWidthSettingKey, out var element)
                    && element.ValueKind == JsonValueKind.Number
                    && element.TryGetInt32(out int saved))
                {
                    _cardWidth = saved;
                }
            }
            catch
            {
                // 读取失败时保持默认值，不影响功能
            }
        }

        /// <summary>
        /// 将卡片宽度持久化到 appsettings.JSON 的 ExtensionData。
        /// 采用 System.Text.Json 序列化，与项目现有 ExtensionData 模式一致。
        /// </summary>
        /// <param name="width">卡片宽度值</param>
        private void SaveCardWidthSetting(int width)
        {
            try
            {
                if (_appSettingService?.Settings?.ExtensionData == null) return;
                _appSettingService.Settings.ExtensionData[CardWidthSettingKey] = JsonSerializer.SerializeToElement(width);
                _appSettingService.Save();
            }
            catch
            {
                // 持久化失败不影响运行时布局切换
            }
        }

        /// <summary>
        /// 初始化共享 DispatcherTimer（1秒间隔）。
        /// 所有 TaskDisplayModel 的时钟与耗时刷新统一由该定时器调度，
        /// 避免每工站独立定时器导致的 N 倍开销，提升多工站场景性能。
        /// </summary>
        private void InitializeSharedTimer()
        {
            _sharedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _sharedTimer.Tick += OnSharedTimerTick;
            _sharedTimer.Start();
        }

        /// <summary>共享定时器回调：统一刷新所有工站的时钟与当前步骤耗时</summary>
        private void OnSharedTimerTick(object sender, EventArgs e)
        {
            foreach (var task in Tasks)
            {
                task.OnSharedTimerTick();
            }
        }

        private void LoadTasks()
        {
            foreach (var task in _stationRegistry.GetAllStations().OfType<ITask>())
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Tasks.Add(new TaskDisplayModel(task, _localization));
                });
            }
        }

        private void OnStationRegistered(IStationParameterProvider station)
        {
            if (station is ITask task)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!Tasks.Any(t => t.TaskId == task.TaskId))
                    {
                        Tasks.Add(new TaskDisplayModel(task, _localization));
                    }
                });
            }
        }

        private void OnStationUnregistered(IStationParameterProvider station)
        {
            if (station is ITask task)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var existing = Tasks.FirstOrDefault(t => t.TaskId == task.TaskId);
                    if (existing != null)
                    {
                        existing.Dispose();
                        Tasks.Remove(existing);
                    }
                });
            }
        }

        private void OnTaskStatusChanged(TaskStatusPayload payload)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var task = Tasks.FirstOrDefault(t => t.TaskId == payload.TaskId);
                if (task != null)
                {
                    task.UpdateStatus(payload);
                }
            });
        }

        /// <summary>
        /// 工站初始化进度事件处理：根据 StationId 匹配对应工站，
        /// 更新其初始化进度（进度条、描述信息、完成状态）。
        /// </summary>
        private void OnInitProgressChanged(StationInitProgressPayload payload)
        {
            if (payload == null || string.IsNullOrEmpty(payload.StationId)) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                var task = Tasks.FirstOrDefault(t => t.StationId == payload.StationId);
                if (task != null)
                {
                    task.UpdateInitProgress(payload.Progress, payload.Message, payload.IsCompleted);
                }
            });
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _sharedTimer?.Stop();
            _sharedTimer = null;

            foreach (var task in Tasks)
            {
                task.Dispose();
            }
            Tasks.Clear();
        }
    }
}

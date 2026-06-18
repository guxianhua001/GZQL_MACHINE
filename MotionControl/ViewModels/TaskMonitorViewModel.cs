using Core.Abstraction;
using Core.Events;
using MotionControl.Events;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Events;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
namespace MotionControl.ViewModels
{
    public class TaskMonitorViewModel : BindableBase
    {
        private readonly IEventAggregator _ea;
        private readonly IStationRegistry _stationRegistry;
        private readonly ILocalizationService _localization;
        public ObservableCollection<TaskDisplayModel> Tasks { get; } = new();

        public TaskMonitorViewModel(IEventAggregator ea, IStationRegistry stationRegistry, ILocalizationService localization)
        {
            _ea = ea;
            _stationRegistry = stationRegistry;
            _localization = localization;

            _ea.GetEvent<TaskStatusChangedEvent>().Subscribe(OnTaskStatusChanged, ThreadOption.PublisherThread, true);
            _ea.GetEvent<StationRegisteredEvent>().Subscribe(OnStationRegistered, ThreadOption.PublisherThread, true);
            _ea.GetEvent<StationUnregisteredEvent>().Subscribe(OnStationUnregistered, ThreadOption.PublisherThread, true);
            // 订阅工站初始化进度事件，更新各工站回零进度显示
            _ea.GetEvent<StationInitProgressEvent>().Subscribe(OnInitProgressChanged, ThreadOption.PublisherThread, true);

            LoadTasks();
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

    }
}

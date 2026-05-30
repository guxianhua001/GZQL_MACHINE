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

    }
}

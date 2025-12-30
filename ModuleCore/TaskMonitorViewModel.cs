using Core.Abstraction;
using Interfaces;
using Interfaces.Events;
using MaterialDesignThemes.Wpf;
using ModuleCore.ViewModels;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using SmarterMotion;
using SmarterMotion.Events;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ModuleCore
{
    public class TaskMonitorViewModel : BindableBase
    {
        private readonly ObservableCollection<ITask> _monitoredTasks = new();
        private readonly ISnackbarMessageQueue _snackbarMessageQueue;
        private readonly IEventAggregator _eventAggregator;
        public ObservableCollection<TaskCardViewModel> Tasks { get; } = new();
        public TaskMonitorViewModel(ISnackbarMessageQueue snackbarMessageQueue,
            IEventAggregator eventAggregator)
        {
            _snackbarMessageQueue = snackbarMessageQueue;
            _eventAggregator = eventAggregator;
            // 注册任务状态变更事件
            _eventAggregator.GetEvent<TaskStepChangedEvent>().Subscribe(OnTaskStepChanged);
        }
        // 添加要监控的任务
        public void AddTaskToMonitor(ITask task)
        {
            if (_monitoredTasks.Contains(task)) return;

            _monitoredTasks.Add(task);
            Tasks.Add(new TaskCardViewModel(task));

            // 注册任务事件
            task.OnStep += HandleTaskStep;
        }
        // 移除监控的任务
        public void RemoveTaskFromMonitor(ITask task)
        {
            if (!_monitoredTasks.Contains(task)) return;

            _monitoredTasks.Remove(task);
            var vmToRemove = Tasks.FirstOrDefault(vm => vm.Task == task);
            if (vmToRemove != null) Tasks.Remove(vmToRemove);

            // 注销任务事件
            task.OnStep -= HandleTaskStep;
        }

        // 处理任务步数变化事件
        private void HandleTaskStep(string stepMessage, Color color)
        {
            // Snackbar 提醒
            if (color == Colors.Red || color == Color.FromRgb(0xE5, 0x39, 0x35)) // Material.Red
            {
                _snackbarMessageQueue.Enqueue($"{stepMessage}", "查看详情", () =>
                {
                    // 可以在这里实现跳转到具体任务的逻辑
                });
            }
        }
        private bool ShouldNotify(Color color)
        {
            return color == Colors.Red ||
                   color == Color.FromRgb(0xE5, 0x39, 0x35) || // Material.Red
                   color == Colors.Orange;
        }
        // 监听全局任务变更事件（使用Prism事件聚合器）
        private void OnTaskStepChanged(TaskStepChangedEventArgs eventArgs)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var vm = Tasks.FirstOrDefault(vm => vm.Task == eventArgs.Task);
                if (vm != null)
                {
                    vm.UpdateStatus(eventArgs.StepMessage, eventArgs.Color);

                    // 显示通知
                    _snackbarMessageQueue.Enqueue($"任务 [{eventArgs.Task.Name}] 状态变更: {eventArgs.StepMessage}");
                }
            });
        }
    }

}

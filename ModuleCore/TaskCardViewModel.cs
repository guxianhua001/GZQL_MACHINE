using Core.Abstraction;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using Prism.Mvvm;
using SmarterMotion;
using System;
using System.Windows.Media;

namespace ModuleCore.ViewModels
{
    public class TaskCardViewModel : BindableBase
    {
        public ITask Task { get; }

        public int TaskId => Task.TaskId;
        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private int _step;
        public int Step
        {
            get => _step;
            set => SetProperty(ref _step, value);
        }

        private int _lastStep;
        public int LastStep
        {
            get => _lastStep;
            set => SetProperty(ref _lastStep, value);
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private Brush _statusColor = new SolidColorBrush(Colors.LightGray);
        public Brush StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        public TaskCardViewModel(ITask task)
        {
            Task = task;
            Name = task.Name;
            UpdateStatus($"任务初始化，开始执行", Colors.Silver);
        }

        public void UpdateStatus(string stepMessage, Color color)
        {
            Step = Task.Step;
            LastStep = Task.LastStep;
            StatusMessage = $"[{DateTime.Now:HH:mm:ss}] {stepMessage}";
            StatusColor = new SolidColorBrush(color);
        }
    }
}

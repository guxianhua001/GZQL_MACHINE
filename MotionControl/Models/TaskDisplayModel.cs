using Core.Abstraction;
using Core.Events;
using MotionControl.Events;
using MotionControl.Interfaces;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
namespace MotionControl.Models
{
    public class TaskDisplayModel : BindableBase, IDisposable
    {
        private readonly ITask _task;
        private readonly ILocalizationService _localization;
        private DateTime _stepStartTime;
        private StepRecord _currentStepRecord;
        private const int MaxHistoryCount = 50;
        public int TaskId => _task.TaskId;

        private string _taskName;
        /// <summary>工站名称（支持语言切换）</summary>
        public string TaskName
        {
            get => _taskName;
            set => SetProperty(ref _taskName, value);
        }

        private string GetLocalizedTaskName()
        {
            if (_task is IStationParameterProvider spp && !string.IsNullOrEmpty(spp.StationIdentifier))
            {
                string resourceKey = $"Station_{spp.StationIdentifier}";
                string localizedName = _localization?.GetResourceOrDefault(resourceKey, _task.TaskName);
                if (!string.IsNullOrEmpty(localizedName))
                    return localizedName;
            }
            return _task.TaskName;
        }
        private TaskState _state;
        public TaskState State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }
        private string _currentTime;
        public string CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }
        private string _currentStepElapsed;
        public string CurrentStepElapsed
        {
            get => _currentStepElapsed;
            set => SetProperty(ref _currentStepElapsed, value);
        }

        private string _currentStepName = "";
        /// <summary>当前执行步骤名称（显式属性，替代脆弱的集合当前项绑定）</summary>
        public string CurrentStepName
        {
            get => _currentStepName;
            set => SetProperty(ref _currentStepName, value);
        }

        private bool _isExpanded;
        /// <summary>是否展开步骤历史（手风琴式，默认收起以保持全局视图）</summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>初始化进度百分比（0-100），仅在 Homing 状态下显示</summary>
        private double _initProgress;
        public double InitProgress
        {
            get => _initProgress;
            set => SetProperty(ref _initProgress, value);
        }

        /// <summary>初始化进度描述信息</summary>
        private string _initMessage;
        public string InitMessage
        {
            get => _initMessage;
            set => SetProperty(ref _initMessage, value);
        }

        /// <summary>是否正在初始化（控制进度条显隐）</summary>
        private bool _isInitializing;
        public bool IsInitializing
        {
            get => _isInitializing;
            set => SetProperty(ref _isInitializing, value);
        }

        /// <summary>工站标识（用于匹配初始化进度事件）</summary>
        public string StationId => (_task as IStationParameterProvider)?.StationIdentifier;
        public ObservableCollection<StepRecord> StepHistory { get; } = new ObservableCollection<StepRecord>();
        public TaskDisplayModel(ITask task, ILocalizationService localization)
        {
            _task = task;
            _localization = localization;
            State = task.State;
            _taskName = GetLocalizedTaskName();
            // 定时器由 TaskMonitorViewModel 共享调度，避免 N 个工站 N 个定时器的开销
            OnSharedTimerTick();
            _localization.LanguageChanged += OnLanguageChanged;
        }

        private string GetStatusText(int retryCount, bool isCurrent)
        {
            if (retryCount > 0)
                return _localization.GetResource("Step_Retrying", retryCount);
            return isCurrent
                ? _localization.GetResource("Step_Running")
                : _localization.GetResource("Step_Completed");
        }

        public void UpdateStatus(TaskStatusPayload payload)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (payload.IsStepCompleted)
                {
                    if (_currentStepRecord != null)
                    {
                        _currentStepRecord.IsCurrent = false;
                        _currentStepRecord.StatusText = GetStatusText(_currentStepRecord.RetryCount, false);
                        CalculateDuration(_currentStepRecord);
                        _currentStepRecord = null;
                    }
                    return;
                }

                State = payload.State;
                // 同步当前步骤名称到显式属性，供紧凑卡片绑定
                CurrentStepName = payload.CurrentStepName ?? string.Empty;
                if (_currentStepRecord == null || _currentStepRecord.StepName != payload.CurrentStepName)
                {
                    if (_currentStepRecord != null)
                    {
                        _currentStepRecord.IsCurrent = false;
                        _currentStepRecord.StatusText = GetStatusText(_currentStepRecord.RetryCount, false);
                        CalculateDuration(_currentStepRecord);
                    }
                    var lastStep = StepHistory.LastOrDefault();
                    if (lastStep != null && lastStep.StepName == payload.CurrentStepName && lastStep.RetryCount > 0)
                    {
                        _currentStepRecord = lastStep;
                        _currentStepRecord.RetryCount++;
                        _currentStepRecord.IsCurrent = true;
                        _currentStepRecord.StatusText = GetStatusText(_currentStepRecord.RetryCount, true);
                    }
                    else if (lastStep != null && lastStep.StepName == payload.CurrentStepName && lastStep.IsCurrent)
                    {
                        lastStep.RetryCount++;
                        lastStep.IsCurrent = true;
                        lastStep.StatusText = GetStatusText(lastStep.RetryCount, true);
                        _currentStepRecord = lastStep;
                    }
                    else
                    {
                        _currentStepRecord = new StepRecord
                        {
                            StepName = payload.CurrentStepName,
                            IsCurrent = true,
                            RetryCount = 0,
                            StatusText = GetStatusText(0, true)
                        };
                        StepHistory.Add(_currentStepRecord);

                        if (StepHistory.Count > MaxHistoryCount)
                        {
                            StepHistory.RemoveAt(0);
                        }
                    }
                    _stepStartTime = DateTime.Now;
                }
            });
        }

        /// <summary>
        /// 更新初始化进度（由 TaskMonitorViewModel 调用，响应 StationInitProgressEvent）
        /// </summary>
        /// <param name="progress">进度百分比 0-100</param>
        /// <param name="message">进度描述</param>
        /// <param name="isCompleted">是否完成</param>
        public void UpdateInitProgress(double progress, string message, bool isCompleted)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsInitializing = !isCompleted;
                InitProgress = progress;
                InitMessage = message;
            });
        }

        private void OnLanguageChanged(object sender, Core.Abstraction.LanguageChangedEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                TaskName = GetLocalizedTaskName();
                foreach (var step in StepHistory)
                {
                    step.StatusText = GetStatusText(step.RetryCount, step.IsCurrent);
                }
            });
        }

        /// <summary>
        /// 共享定时器回调：由 TaskMonitorViewModel 的单一 DispatcherTimer 统一调度，
        /// 刷新时钟与当前步骤耗时，避免每工站独立定时器的 N 倍开销。
        /// </summary>
        public void OnSharedTimerTick()
        {
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            if (_currentStepRecord != null && _currentStepRecord.IsCurrent)
            {
                CurrentStepElapsed = (DateTime.Now - _stepStartTime).ToString(@"mm\:ss");
            }
        }
        private void CalculateDuration(StepRecord record)
        {
            record.DurationText = (DateTime.Now - _stepStartTime).ToString(@"mm\:ss");
        }
        public void Dispose()
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
    }
}

using Newtonsoft.Json;
using Prism.Mvvm;
using StationTasks.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Module.Models
{
    /// <summary>
    /// 工艺方法（中间层），包含一组动作步骤（ProcessStep）。
    /// 在 Task → Method → Action 三级树形结构中作为中间层节点。
    /// </summary>
    public class ProcessMethod : BindableBase
    {
        private string _name;
        /// <summary> 方法名称 </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private ObservableCollection<ProcessStep> _steps;
        /// <summary> 该方法包含的动作步骤列表 </summary>
        public ObservableCollection<ProcessStep> Steps
        {
            get => _steps;
            set
            {
                // 替换集合时先解绑旧集合的事件，再绑定新集合，确保子工具总耗时聚合正确
                if (_steps != null)
                {
                    _steps.CollectionChanged -= OnStepsCollectionChanged;
                    UnsubscribeSteps(_steps);
                }
                SetProperty(ref _steps, value);
                if (_steps != null)
                {
                    _steps.CollectionChanged += OnStepsCollectionChanged;
                    SubscribeSteps(_steps);
                }
                RecomputeSubStepsTotalElapsed();
            }
        }

        private bool _isEnabled = true;
        /// <summary> 方法启用状态：禁用的方法在运行时被跳过 </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        private bool _isExpanded = true;
        /// <summary> TreeView 展开状态 </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private bool _isSelected;
        /// <summary> TreeView 选中状态 </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private TaskItem.TaskStatusEnum _status = TaskItem.TaskStatusEnum.Idle;
        /// <summary> 方法级执行状态（Idle/Running/Paused/Stopped），用于方法级独立控制；运行时状态不持久化 </summary>
        [JsonIgnore]
        public TaskItem.TaskStatusEnum Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private string _comment;
        /// <summary> 方法注释（用户备注，可序列化持久化） </summary>
        public string Comment
        {
            get => _comment;
            set => SetProperty(ref _comment, value);
        }

        private long _lastElapsedMs;
        /// <summary> 方法最近一次执行的耗时（毫秒），运行时记录；不持久化 </summary>
        [JsonIgnore]
        public long LastElapsedMs
        {
            get => _lastElapsedMs;
            set => SetProperty(ref _lastElapsedMs, value);
        }

        private long _subStepsTotalElapsedMs;
        /// <summary>
        /// 所有子工具（步骤）最近一次执行耗时总和（毫秒）。
        /// 聚合 Steps 集合中每个步骤的 LastElapsedMs，包括 IF 嵌套分支内的子步骤。
        /// 自动订阅 Steps 集合变化、嵌套 IF 分支集合变化与各步骤 PropertyChanged 实时刷新。
        /// 仅用于 UI 显示，不持久化。
        /// </summary>
        [JsonIgnore]
        public long SubStepsTotalElapsedMs
        {
            get => _subStepsTotalElapsedMs;
            private set => SetProperty(ref _subStepsTotalElapsedMs, value);
        }

        /// <summary>
        /// 无参构造函数：JSON 反序列化所需
        /// </summary>
        public ProcessMethod()
        {
            Name = string.Empty;
            Steps = new ObservableCollection<ProcessStep>();
        }

        /// <summary>
        /// 构造函数：以指定名称创建空方法
        /// </summary>
        /// <param name="name">方法名称</param>
        public ProcessMethod(string name)
        {
            Name = name;
            Steps = new ObservableCollection<ProcessStep>();
        }

        /// <summary>
        /// 构造函数：以指定名称和初始步骤集合创建方法
        /// </summary>
        /// <param name="name">方法名称</param>
        /// <param name="steps">初始步骤集合</param>
        public ProcessMethod(string name, IEnumerable<ProcessStep> steps)
        {
            Name = name;
            Steps = new ObservableCollection<ProcessStep>(steps);
        }

        // ========== 子工具总耗时聚合 ==========

        /// <summary>
        /// 重新计算所有子工具（含 IF 嵌套子步骤）的耗时总和并更新 SubStepsTotalElapsedMs。
        /// 任何步骤的 LastElapsedMs 变化、Steps/IfBranches 集合变化后均会调用此方法。
        /// </summary>
        private void RecomputeSubStepsTotalElapsed()
        {
            long total = 0;
            if (_steps != null)
            {
                foreach (var step in _steps)
                    total += SumStepElapsedRecursive(step);
            }
            SubStepsTotalElapsedMs = total;
        }

        /// <summary>
        /// 递归求单个步骤及其 IF 嵌套分支内所有子步骤的 LastElapsedMs 之和。
        /// </summary>
        private static long SumStepElapsedRecursive(ProcessStep step)
        {
            if (step == null) return 0;
            long sum = step.LastElapsedMs;
            if (step.IfBranches != null)
            {
                foreach (var branch in step.IfBranches)
                {
                    if (branch?.Steps != null)
                    {
                        foreach (var sub in branch.Steps)
                            sum += SumStepElapsedRecursive(sub);
                    }
                }
            }
            return sum;
        }

        /// <summary> 订阅 Steps 集合中每个步骤（含嵌套子步骤）的 PropertyChanged 与嵌套集合变化 </summary>
        private void SubscribeSteps(IEnumerable<ProcessStep> steps)
        {
            if (steps == null) return;
            foreach (var step in steps)
                SubscribeStepRecursive(step);
        }

        /// <summary> 取消订阅 Steps 集合中每个步骤（含嵌套子步骤）的 PropertyChanged 与嵌套集合变化 </summary>
        private void UnsubscribeSteps(IEnumerable<ProcessStep> steps)
        {
            if (steps == null) return;
            foreach (var step in steps)
                UnsubscribeStepRecursive(step);
        }

        /// <summary>
        /// 递归订阅步骤及其 IF 分支内子步骤：
        /// - 步骤本身的 PropertyChanged（用于响应 LastElapsedMs / IfBranches 变化）
        /// - 每个 IF 分支组 Steps 集合的 CollectionChanged（用于响应嵌套子步骤增删）
        /// </summary>
        private void SubscribeStepRecursive(ProcessStep step)
        {
            if (step == null) return;
            step.PropertyChanged -= OnStepPropertyChanged;
            step.PropertyChanged += OnStepPropertyChanged;
            if (step.IfBranches != null)
            {
                foreach (var branch in step.IfBranches)
                {
                    if (branch?.Steps != null)
                    {
                        branch.Steps.CollectionChanged -= OnNestedStepsCollectionChanged;
                        branch.Steps.CollectionChanged += OnNestedStepsCollectionChanged;
                        foreach (var sub in branch.Steps)
                            SubscribeStepRecursive(sub);
                    }
                }
            }
        }

        /// <summary> 递归取消订阅步骤及其 IF 分支内子步骤的事件 </summary>
        private void UnsubscribeStepRecursive(ProcessStep step)
        {
            if (step == null) return;
            step.PropertyChanged -= OnStepPropertyChanged;
            if (step.IfBranches != null)
            {
                foreach (var branch in step.IfBranches)
                {
                    if (branch?.Steps != null)
                    {
                        branch.Steps.CollectionChanged -= OnNestedStepsCollectionChanged;
                        foreach (var sub in branch.Steps)
                            UnsubscribeStepRecursive(sub);
                    }
                }
            }
        }

        /// <summary> 顶层 Steps 集合变化：对新增/移除的步骤订阅/取消订阅，并重算总和 </summary>
        private void OnStepsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            HandleStepsCollectionChanged(e);
            RecomputeSubStepsTotalElapsed();
        }

        /// <summary> IF 分支内 Steps 集合变化：对新增/移除的子步骤订阅/取消订阅，并重算总和 </summary>
        private void OnNestedStepsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            HandleStepsCollectionChanged(e);
            RecomputeSubStepsTotalElapsed();
        }

        /// <summary> 处理集合变化：订阅新项、取消订阅旧项（含嵌套递归） </summary>
        private void HandleStepsCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (ProcessStep s in e.OldItems)
                    UnsubscribeStepRecursive(s);
            }
            if (e.NewItems != null)
            {
                foreach (ProcessStep s in e.NewItems)
                    SubscribeStepRecursive(s);
            }
        }

        /// <summary>
        /// 步骤属性变化回调：
        /// - LastElapsedMs 变化：重算总和
        /// - IfBranches 替换：重新订阅新分支集合，并重算总和
        /// </summary>
        private void OnStepPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is ProcessStep step)
            {
                if (e.PropertyName == nameof(ProcessStep.LastElapsedMs))
                {
                    RecomputeSubStepsTotalElapsed();
                }
                else if (e.PropertyName == nameof(ProcessStep.IfBranches))
                {
                    // IfBranches 被替换：重新订阅新分支（去重由 SubscribeStepRecursive 内部 -= / += 保证）
                    SubscribeStepRecursive(step);
                    RecomputeSubStepsTotalElapsed();
                }
            }
        }
    }
}

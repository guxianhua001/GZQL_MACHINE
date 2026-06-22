using Prism.Mvvm;
using StationTasks.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
            set => SetProperty(ref _steps, value);
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
        /// <summary> 方法级执行状态（Idle/Running/Paused/Stopped），用于方法级独立控制 </summary>
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
        /// <summary> 方法最近一次执行的耗时（毫秒），运行时记录 </summary>
        public long LastElapsedMs
        {
            get => _lastElapsedMs;
            set => SetProperty(ref _lastElapsedMs, value);
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
    }
}

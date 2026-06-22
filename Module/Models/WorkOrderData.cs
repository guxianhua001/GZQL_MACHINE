
using Newtonsoft.Json;
using Prism.Mvvm;
using StationTasks.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Module.Models
{
    /// <summary>
    /// 工单配置数据模型，包含组件、站点、相机、用途和轴信息。
    /// </summary>
    public class WorkOrderData
    {
        public ObservableCollection<Component> Components { get; set; } = new ObservableCollection<Component>();
        public ObservableCollection<Site> Sites { get; set; } = new ObservableCollection<Site>();
        public ObservableCollection<CameraConstant> Cameras { get; set; } = new ObservableCollection<CameraConstant>();
        public ObservableCollection<PurposeConstant> Purposes { get; set; } = new ObservableCollection<PurposeConstant>();
        public ObservableCollection<AxisConstant> Axes { get; set; } = new ObservableCollection<AxisConstant>();
        //public ObservableCollection<MachineConstant> MachineConstants { get; set; } = new ObservableCollection<MachineConstant>();
    }
    public class ComponentFeature : BindableBase
    {
        private string _id;
        private string _name;
        private string _description;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
    }
    public class SiteFeature : BindableBase
    {
        private string _id;
        private string _name;
        private SiteFeatureType _type;
        private string _description;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public SiteFeatureType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
    }
    public class Component : BindableBase
    {
        private string _name;
        private ObservableCollection<ComponentFeature> _features;

        public Component()
        {
            Features = new ObservableCollection<ComponentFeature>();
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public ObservableCollection<ComponentFeature> Features
        {
            get => _features;
            set => SetProperty(ref _features, value);
        }
    }
    public class Site : BindableBase
    {
        private string _id;
        private string _name;
        private string _type;
        private string _description;
        private ObservableCollection<SiteFeature> _features;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public ObservableCollection<SiteFeature> Features
        {
            get => _features;
            set => SetProperty(ref _features, value);
        }

        public Site()
        {
            Features = new ObservableCollection<SiteFeature>();
        }
    }
    public class CameraConstant : BindableBase
    {
        private string _name;
        private string _description;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
    }
    public class PurposeConstant : BindableBase
    {
        private string _name;
        private string _description;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
    }
    public class AxisConstant : BindableBase
    {
        private string _group;
        private string _name;
        private string _description;

        public string Group
        {
            get => _group;
            set => SetProperty(ref _group, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
    }
    public class MachineConstant : BindableBase
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    /// <summary> 任务运行模式：Active=主动执行 / Passive=被动触发（被其他流程调用） </summary>
    public enum TaskRunMode { Active, Passive }

    /// <summary>
    /// 任务项，包含名称、方法列表和步骤列表
    /// </summary>
    public class TaskItem : BindableBase
    {
        private string _name;
        private ObservableCollection<ProcessStep> _steps;
        private ObservableCollection<ProcessMethod> _methods;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary> 步骤列表（向后兼容，由 Methods 聚合而来，不参与序列化） </summary>
        [JsonIgnore]
        public ObservableCollection<ProcessStep> Steps
        {
            get => _steps;
            set => SetProperty(ref _steps, value);
        }

        /// <summary> 方法列表（新的主要容器，Task → Method → Action 三级结构的中间层） </summary>
        public ObservableCollection<ProcessMethod> Methods
        {
            get => _methods;
            set => SetProperty(ref _methods, value);
        }

        public enum TaskStatusEnum { Idle, Running, Paused, Stopped }

        private TaskStatusEnum _status = TaskStatusEnum.Idle;
        public TaskStatusEnum Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private TaskRunMode _runMode = TaskRunMode.Active;
        /// <summary> 任务运行模式 </summary>
        public TaskRunMode RunMode
        {
            get => _runMode;
            set => SetProperty(ref _runMode, value);
        }

        private bool _isDefault;
        public bool IsDefault
        {
            get => _isDefault;
            set => SetProperty(ref _isDefault, value);
        }

        private bool _isEnabled = true;
        /// <summary> 任务启用状态：禁用的任务在运行时被跳过 </summary>
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

        private string _comment;
        /// <summary> 任务注释（用户备注，可序列化持久化） </summary>
        public string Comment
        {
            get => _comment;
            set => SetProperty(ref _comment, value);
        }

        private long _lastElapsedMs;
        /// <summary> 任务最近一次执行的耗时（毫秒），运行时记录 </summary>
        public long LastElapsedMs
        {
            get => _lastElapsedMs;
            set => SetProperty(ref _lastElapsedMs, value);
        }

        /// <summary>
        /// 构造函数：以指定名称创建空任务
        /// </summary>
        /// <param name="name">任务名称</param>
        public TaskItem(string name)
        {
            Name = name;
            Methods = new ObservableCollection<ProcessMethod>();
            _steps = new ObservableCollection<ProcessStep>();
        }

        /// <summary>
        /// 构造函数：以指定名称和初始步骤集合创建任务。
        /// 将传入的步骤包装为默认方法 "默认方法" 并加入 Methods，同时同步 _steps 以保持向后兼容。
        /// </summary>
        /// <param name="name">任务名称</param>
        /// <param name="steps">初始步骤集合</param>
        public TaskItem(string name, IEnumerable<ProcessStep> steps)
        {
            Name = name;
            _steps = new ObservableCollection<ProcessStep>(steps);
            Methods = new ObservableCollection<ProcessMethod>
            {
                new ProcessMethod("默认方法", steps)
            };
        }

        /// <summary>
        /// 从所有 Methods 重新聚合 _steps（向后兼容字段）。
        /// 当 Methods 发生变化时调用以保持 Steps 与 Methods 同步。
        /// </summary>
        public void SyncStepsFromMethods()
        {
            var aggregated = new ObservableCollection<ProcessStep>();
            foreach (var method in Methods)
            {
                foreach (var step in method.Steps)
                {
                    aggregated.Add(step);
                }
            }
            _steps = aggregated;
            RaisePropertyChanged(nameof(Steps));
        }
    }
    /// <summary>
    /// 验证结果项
    /// </summary>
    public class ValidationItem
    {
        public string Message { get; }
        public bool IsValid { get; }
        public ValidationItem(string message, bool isValid)
        {
            Message = message;
            IsValid = isValid;
        }
    }
    public enum SiteFeatureType
    {
        Site,       // 工位
        Dispense,   // 点胶位
        AssyGroup   // 装配组
    }
}

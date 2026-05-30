
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
    /// <summary>
    /// 任务项，包含名称和步骤列表
    /// </summary>
    public class TaskItem : BindableBase
    {
        private string _name;
        private ObservableCollection<ProcessStep> _steps;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public ObservableCollection<ProcessStep> Steps
        {
            get => _steps;
            set => SetProperty(ref _steps, value);
        }
        public enum TaskStatusEnum { Idle, Running, Paused, Stopped }

        private TaskStatusEnum _status = TaskStatusEnum.Idle;
        public TaskStatusEnum Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private bool _isDefault;
        public bool IsDefault
        {
            get => _isDefault;
            set => SetProperty(ref _isDefault, value);
        }

        public TaskItem(string name, IEnumerable<ProcessStep> steps)
        {
            Name = name;
            Steps = new ObservableCollection<ProcessStep>(steps);
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

using Core.Abstraction;
using Module.Models;
using Module.Services;
using Prism.Commands;
using Prism.Mvvm;
using StationTasks.Models;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Module.ViewModels
{
    /// <summary>
    /// 调用任务（RUNTASK）步骤编辑器 ViewModel，用于选择要调用的被动任务
    /// </summary>
    public class RunTaskDetailViewModel : BindableBase, IDialogCloseable
    {
        private readonly IProcessSequenceService _sequenceService;
        private readonly ILocalizationService _localization;
        private ProcessStep _step;

        /// <summary>请求关闭对话框时触发</summary>
        public event Action<object> RequestClose;

        /// <summary>是否可以关闭对话框</summary>
        public bool CanCloseDialog() => true;

        /// <summary> 当前编辑的工艺步骤，设置时自动初始化调用任务配置 </summary>
        public ProcessStep Step
        {
            get => _step;
            set
            {
                if (SetProperty(ref _step, value))
                {
                    InitializeFromStep();
                    RaisePropertyChanged(nameof(StepDescription));
                }
            }
        }

        /// <summary> 步骤描述文本（用于标题栏显示） </summary>
        public string StepDescription => _step == null ? "—" : $"Seq{_step.Seq} → {_localization.GetResourceOrDefault("PSE_RunTaskAction", "调用任务")}";

        /// <summary> 可选的被动任务名称集合（仅 RunMode == Passive 的任务） </summary>
        public ObservableCollection<string> PassiveTaskNames { get; } = new ObservableCollection<string>();

        private string _selectedTargetTaskName;
        /// <summary> 当前选中的目标被动任务名称 </summary>
        public string SelectedTargetTaskName
        {
            get => _selectedTargetTaskName;
            set => SetProperty(ref _selectedTargetTaskName, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }

        /// <summary>
        /// 构造函数：注入工艺序列服务和本地化服务，初始化命令并刷新被动任务列表
        /// </summary>
        /// <param name="sequenceService">工艺序列服务，用于获取任务列表</param>
        /// <param name="localization">本地化服务，用于多语言支持</param>
        public RunTaskDetailViewModel(IProcessSequenceService sequenceService, ILocalizationService localization)
        {
            _sequenceService = sequenceService;
            _localization = localization;
            SaveCommand = new DelegateCommand(OnSave);
            CloseCommand = new DelegateCommand(OnClose);
            RefreshPassiveTaskNames();
        }

        /// <summary>
        /// 从 IProcessSequenceService.Tasks 中筛选 Passive 任务，填充下拉选项
        /// </summary>
        private void RefreshPassiveTaskNames()
        {
            PassiveTaskNames.Clear();
            if (_sequenceService?.Tasks == null) return;

            foreach (var task in _sequenceService.Tasks)
            {
                if (task.RunMode == TaskRunMode.Passive)
                {
                    PassiveTaskNames.Add(task.Name);
                }
            }
        }

        /// <summary>
        /// 从 Step.RunTaskDetail 加载配置，为空则创建默认实例
        /// </summary>
        private void InitializeFromStep()
        {
            if (_step == null) return;

            if (_step.RunTaskDetail == null)
            {
                _step.RunTaskDetail = new RunTaskDetail();
            }

            SelectedTargetTaskName = _step.RunTaskDetail.TargetTaskName;
        }

        /// <summary>
        /// 保存当前选择的目标任务名称到 Step.RunTaskDetail 并关闭弹窗
        /// </summary>
        private void OnSave()
        {
            if (_step == null) return;

            if (_step.RunTaskDetail == null)
                _step.RunTaskDetail = new RunTaskDetail();

            _step.RunTaskDetail.TargetTaskName = SelectedTargetTaskName;

            RequestClose?.Invoke(true);
        }

        /// <summary>
        /// 关闭弹窗不保存
        /// </summary>
        private void OnClose()
        {
            RequestClose?.Invoke(false);
        }
    }
}

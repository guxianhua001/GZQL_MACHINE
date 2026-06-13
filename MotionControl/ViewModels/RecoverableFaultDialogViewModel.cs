using MotionControl.Events;
using MotionControl.Interfaces;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using System.Linq;

namespace MotionControl.ViewModels
{
    public class RecoverableFaultDialogViewModel : BindableBase
    {
        private readonly IEventAggregator _ea;
        private readonly IContainerProvider _container;
        private readonly ISystemStateService _systemState;

        private string _taskName;
        public string TaskName
        {
            get => _taskName;
            set => SetProperty(ref _taskName, value);
        }
        private string _stepName;
        public string StepName
        {
            get => _stepName;
            set => SetProperty(ref _stepName, value);
        }
        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }
        private string _suggestedAction;
        public string SuggestedAction
        {
            get => _suggestedAction;
            set => SetProperty(ref _suggestedAction, value);
        }
        private int _taskId;
        public int TaskId
        {
            get => _taskId;
            set => SetProperty(ref _taskId, value);
        }

        public DelegateCommand ResumeCommand { get; }
        public DelegateCommand PauseCommand { get; }
        public DelegateCommand StopCommand { get; }
        /// <summary> 手动操作模式的“确定”命令：只关闭弹窗，不做暂停/恢复/停止操作 </summary>
        public DelegateCommand ConfirmCommand { get; }

        /// <summary> 是否为手动操作（手动时只显示“确定”按钮，隐藏暂停/恢复/停止） </summary>
        private bool _isManualOperation;
        public bool IsManualOperation
        {
            get => _isManualOperation;
            set => SetProperty(ref _isManualOperation, value);
        }
        /// <summary> 是否为自动运行（!IsManualOperation，控制XAML按钮可见性） </summary>
        public bool IsAutoOperation => !IsManualOperation;

        public RecoverableFaultDialogViewModel(IEventAggregator ea, IContainerProvider container,
            ISystemStateService systemState)
        {
            _ea = ea;
            _container = container;
            _systemState = systemState;
            ResumeCommand = new DelegateCommand(ExecuteResume);
            PauseCommand = new DelegateCommand(ExecutePause);
            StopCommand = new DelegateCommand(ExecuteStop);
            ConfirmCommand = new DelegateCommand(ExecuteConfirm);
        }

        public void Initialize(RecoverableFaultPayload payload)
        {
            TaskId = payload.TaskId;
            TaskName = payload.TaskName;
            StepName = payload.StepName;
            ErrorMessage = payload.ErrorMessage;
            SuggestedAction = payload.SuggestedAction;
            IsManualOperation = payload.IsManualOperation;
            RaisePropertyChanged(nameof(IsAutoOperation));
        }

        private ITask FindTask()
        {
            var tasks = _container.Resolve<IEnumerable<ITask>>();
            return tasks.FirstOrDefault(t => t.TaskId == TaskId);
        }

        private void CloseDialog()
        {
            try
            {
                MaterialDesignThemes.Wpf.DialogHost.Close("MainDialogHost");
            }
            catch (InvalidOperationException)
            {
                // DialogHost 已关闭或未打开，无需处理
            }
        }

        private void ExecuteResume()
        {
            _systemState.RequestResume();
            var task = FindTask();
            if (task != null)
            {
                task.ResumeAsync();
            }
            CloseDialog();
        }

        private void ExecutePause()
        {
            CloseDialog();
        }

        private void ExecuteStop()
        {
            _systemState.RequestStop();
            var task = FindTask();
            if (task != null)
            {
                task.StopAsync();
            }
            CloseDialog();
        }

        /// <summary> 手动操作的“确定”按钮：仅关闭弹窗，不做任何状态切换 </summary>
        private void ExecuteConfirm()
        {
            CloseDialog();
        }
    }
}

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

        public RecoverableFaultDialogViewModel(IEventAggregator ea, IContainerProvider container,
            ISystemStateService systemState)
        {
            _ea = ea;
            _container = container;
            _systemState = systemState;
            ResumeCommand = new DelegateCommand(ExecuteResume);
            PauseCommand = new DelegateCommand(ExecutePause);
            StopCommand = new DelegateCommand(ExecuteStop);
        }

        public void Initialize(RecoverableFaultPayload payload)
        {
            TaskId = payload.TaskId;
            TaskName = payload.TaskName;
            StepName = payload.StepName;
            ErrorMessage = payload.ErrorMessage;
            SuggestedAction = payload.SuggestedAction;
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
    }
}

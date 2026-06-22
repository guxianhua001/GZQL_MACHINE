using Prism.Mvvm;

namespace StationTasks.Models
{
    /// <summary>
    /// 调用任务步骤（RUNTASK）的详细配置，记录要调用的被动任务名称
    /// </summary>
    public class RunTaskDetail : BindableBase
    {
        private string _targetTaskName;
        /// <summary> 目标被动任务名称（运行时按此名称查找 Passive 任务） </summary>
        public string TargetTaskName
        {
            get => _targetTaskName;
            set => SetProperty(ref _targetTaskName, value);
        }
    }
}

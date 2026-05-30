using StationTasks.Models;
using StationTasks.Tasks;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Actions
{
    /// <summary>
    /// 工艺步骤动作接口，每种步骤类型实现一个 Action
    /// </summary>
    public interface IProcessStepAction
    {
        /// <summary> 该 Action 支持的步骤类型 </summary>
        StepType SupportedStepType { get; }

        /// <summary>
        /// 执行步骤动作
        /// </summary>
        /// <param name="step">工艺步骤数据</param>
        /// <param name="task">所属工站任务，用于调用运动/IO等方法</param>
        /// <param name="token">取消令牌</param>
        Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token);
    }
}

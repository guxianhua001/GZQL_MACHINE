using StationTasks.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Actions
{
    /// <summary>
    /// 调用任务执行器接口：按任务名称查找并执行 Passive 任务。
    /// 在 StationTasks 层定义，由 Module 层的 ProcessSequenceService 实现，避免倒置依赖。
    /// </summary>
    public interface IRunTaskExecutor
    {
        /// <summary>
        /// 执行指定名称的被动任务
        /// </summary>
        /// <param name="targetTaskName">目标被动任务名称</param>
        /// <param name="callerTask">调用方工站任务（用于在相同上下文执行）</param>
        /// <param name="callStack">调用栈，用于循环引用检测（每层调用压入任务名）</param>
        /// <param name="token">取消令牌</param>
        /// <returns>任务执行完成</returns>
        Task ExecutePassiveTaskAsync(string targetTaskName, StationTaskBase callerTask, Stack<string> callStack, CancellationToken token);
    }
}

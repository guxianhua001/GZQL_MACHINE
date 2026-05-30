using Core.Events;
using StationTasks.Models;
using System.Collections.ObjectModel;

namespace StationTasks.Events
{
    /// <summary>
    /// ProcessStepSequencePayload 扩展方法，提供强类型的步骤列表访问
    /// 事件定义统一使用 Core.Events.ProcessStepSequenceEvent
    /// </summary>
    public static class ProcessStepSequencePayloadExtensions
    {
        /// <summary>
        /// 获取强类型的步骤列表，将 object 类型的 Steps 转换为 ObservableCollection&lt;ProcessStep&gt;
        /// </summary>
        public static ObservableCollection<ProcessStep> GetTypedSteps(this ProcessStepSequencePayload payload)
        {
            return payload.Steps as ObservableCollection<ProcessStep>
                ?? new ObservableCollection<ProcessStep>();
        }
    }
}

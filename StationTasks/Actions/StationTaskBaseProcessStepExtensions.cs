using AlarmModule.Interfaces;
using Core.Abstraction;
using Core.Utilities;
using Recipe.Interfaces;
using StationTasks.Models;
using StationTasks.Actions;
using StationTasks.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Actions
{
    /// <summary>
    /// StationTaskBase 的工艺步骤序列执行扩展方法
    /// 将依赖 Module.Models 的方法放在此处，避免 MotionControl 项目引用 Module
    /// </summary>
    public static class StationTaskBaseProcessStepExtensions
    {
        /// <summary>
        /// 执行工艺步骤序列：创建 ProcessStepExecutor 并运行
        /// </summary>
        public static async Task ExecuteProcessStepSequenceAsync(
            this StationTaskBase task,
            ObservableCollection<ProcessStep> steps,
            IEnumerable<IProcessStepAction> actions,
            IAlarmService alarmService,
            IFormulaEvaluator formulaEvaluator,
            IRecipePoolService recipePoolService,
            CancellationToken token)
        {
            var executor = new ProcessStepExecutor(task, task.TaskLogger, actions, alarmService, formulaEvaluator, recipePoolService);
            await executor.ExecuteAsync(steps, token);
        }
    }
}

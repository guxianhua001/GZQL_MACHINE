using AlarmModule.Interfaces;
using AlarmModule.Models;
using Core.Abstraction;
using Core.Models;
using StationTasks.Events;
using Prism.Events;
using StationTasks.Models;
using StationTasks.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Actions
{
    /// <summary> DASHBOARD 步骤执行器：计算公式值、发布弹窗事件、等待用户确认 </summary>
    public class DashboardStepAction : IProcessStepAction
    {
        public StepType SupportedStepType => StepType.DASHBOARD;

        private readonly IFormulaEvaluator _formulaEvaluator;
        private readonly IEventAggregator _ea;
        private readonly IAlarmService _alarmService;

        /// <summary> 构造函数：注入公式求值器、事件聚合器和报警服务 </summary>
        public DashboardStepAction(IFormulaEvaluator formulaEvaluator, IEventAggregator ea, IAlarmService alarmService)
        {
            _formulaEvaluator = formulaEvaluator;
            _ea = ea;
            _alarmService = alarmService;
        }

        /// <summary> 执行 DASHBOARD 步骤：计算字段公式 → 发布弹窗事件 → 等待用户确认 → 设置结果 </summary>
        public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            var detail = step.DashboardDetail;
            if (detail == null || detail.Fields.Count == 0)
            {
                task.TaskLogger.Warn(string.Format(task.Localization.GetResourceOrDefault("DB_Log_NoFields", "DASHBOARD 步骤 [{0}] 未配置看板字段，跳过"), step.Seq));
                return;
            }

            // 1. 获取当前所有全局变量值
            var variables = GetGlobalVariables(task);

            // 2. 对每个字段求值
            foreach (var field in detail.Fields)
            {
                try
                {
                    if (!string.IsNullOrEmpty(field.Formula))
                        field.CurrentValue = _formulaEvaluator.Evaluate(field.Formula, variables);

                    if (!string.IsNullOrEmpty(field.ConditionFormula))
                        field.ConditionResult = _formulaEvaluator.EvaluateCondition(field.ConditionFormula, variables);
                }
                catch (Exception ex)
                {
                    task.TaskLogger.Error(string.Format(task.Localization.GetResourceOrDefault("DB_Log_FormulaEvalFailed", "DASHBOARD 字段 [{0}] 公式求值失败: {1}"), field.DisplayName, ex.Message));
                }
            }

            task.TaskLogger.Info(string.Format(task.Localization.GetResourceOrDefault("DB_Log_DataCalculated", "DASHBOARD 步骤 [{0}] 数据已计算完成"), step.Seq));

            // 3. 根据配置决定是否弹出看板等待人工确认
            if (detail.RequireManualConfirm)
            {
                // 发布事件 → 打开弹窗 UI 展示（执行模式）
                _ea.GetEvent<ShowDashboardEvent>().Publish(new ShowDashboardPayload
                {
                    Step = step,
                    Fields = new ObservableCollection<DashboardField>(detail.Fields),
                    ImagePath = detail.ImagePath,
                    Annotations = new ObservableCollection<DashboardAnnotation>(detail.Annotations),
                    IsExecutionMode = true
                });

                // 等待用户确认（支持超时自动确认）
                var result = await WaitForConfirmAsync(detail.AutoConfirmTimeout, token);

                // 设置确认结果：OK=true, NG=false，流程均继续执行
                detail.ConfirmResult = (result != DashboardConfirmResult.NG);
                LogDashboardData(task, step);

                if (result == DashboardConfirmResult.NG)
                {
                    task.TaskLogger.Warn(string.Format(task.Localization.GetResourceOrDefault("DB_Log_UserConfirmNG", "DASHBOARD 步骤 [{0}] 用户确认NG，输出结果=false，流程继续"), step.Seq));
                    if (step.AlarmConfig?.IsEnabled == true)
                    {
                        _ea.GetEvent<MotionControl.Events.StepFaultedEvent>().Publish($"[{step.Seq}] {step.Step}");
                        _ea.GetEvent<MotionControl.Events.StepErrorEvent>().Publish(new MotionControl.Events.StepErrorPayload
                        {
                            StepName = $"[{step.Seq}] {step.Step}",
                            ErrorMessage = $"DASHBOARD 步骤 [{step.Seq}] 用户确认NG",
                            ErrorCode = "DASHBOARD_NG"
                        });
                    }
                    await _alarmService.TriggerAlarmAsync(
                        "DASHBOARD_NG",
                        AlarmLevel.General,
                        $"DASHBOARD 步骤 [{step.Seq}] 用户确认NG",
                        source: $"{task.TaskName}.Step{step.Seq}",
                        type: AlarmType.ProcessError);
                }
                else
                {
                    task.TaskLogger.Info(string.Format(task.Localization.GetResourceOrDefault("DB_Log_UserConfirmOK", "DASHBOARD 步骤 [{0}] 用户确认OK，输出结果=true"), step.Seq));
                }
            }
            else
            {
                // 自动模式：根据条件表达式判定结果
                bool allPassed = true;
                var failedFields = new List<string>();

                foreach (var field in detail.Fields.Where(f => f.FieldType == DashboardFieldType.Condition))
                {
                    if (field.ConditionResult == false)
                    {
                        allPassed = false;
                        failedFields.Add($"{field.DisplayName}={field.DisplayValue}");
                    }
                }

                LogDashboardData(task, step);
                detail.ConfirmResult = allPassed;

                if (allPassed)
                {
                    task.TaskLogger.Info(string.Format(task.Localization.GetResourceOrDefault("DB_Log_AutoJudgeOK", "DASHBOARD 步骤 [{0}] 自动判定OK，输出结果=true"), step.Seq));
                }
                else
                {
                    task.TaskLogger.Warn(string.Format(task.Localization.GetResourceOrDefault("DB_Log_AutoJudgeNG", "DASHBOARD 步骤 [{0}] 自动判定NG: {1}，输出结果=false，流程继续"), step.Seq, string.Join(", ", failedFields)));
                    if (step.AlarmConfig?.IsEnabled == true)
                    {
                        _ea.GetEvent<MotionControl.Events.StepFaultedEvent>().Publish($"[{step.Seq}] {step.Step}");
                        _ea.GetEvent<MotionControl.Events.StepErrorEvent>().Publish(new MotionControl.Events.StepErrorPayload
                        {
                            StepName = $"[{step.Seq}] {step.Step}",
                            ErrorMessage = $"DASHBOARD 步骤 [{step.Seq}] 自动判定NG: {string.Join(", ", failedFields)}",
                            ErrorCode = "DASHBOARD_NG"
                        });
                    }
                    await _alarmService.TriggerAlarmAsync(
                        "DASHBOARD_NG",
                        AlarmLevel.General,
                        $"DASHBOARD 步骤 [{step.Seq}] 自动判定NG: {string.Join(", ", failedFields)}",
                        source: $"{task.TaskName}.Step{step.Seq}",
                        type: AlarmType.ProcessError);
                }
            }
        }

        /// <summary> 记录看板数据到日志 </summary>
        private void LogDashboardData(StationTaskBase task, ProcessStep step)
        {
            var fieldData = step.DashboardDetail?.Fields
                .Select(f => $"{f.DisplayName}={f.DisplayValue}")
                .ToArray();

            if (fieldData?.Length > 0)
            {
                task.TaskLogger.Info(string.Format(task.Localization.GetResourceOrDefault("DB_Log_DashboardData", "DASHBOARD 步骤 [{0}] 看板数据: {1}"), step.Seq, string.Join(", ", fieldData)));
            }
        }

        /// <summary> 从 StationTaskBase 的上下文获取全局变量字典 </summary>
        private Dictionary<string, string> GetGlobalVariables(StationTaskBase task)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var globalVarProvider = task as IGlobalVariableProvider;
                if (globalVarProvider != null)
                {
                    foreach (var gv in globalVarProvider.GetGlobalVariables())
                        result[gv.Name] = gv.Value;
                }
            }
            catch (Exception ex)
            {
                task.TaskLogger.Debug(string.Format(task.Localization.GetResourceOrDefault("DB_Log_GlobalVarProviderFailed", "[DashboardAction] 无法获取全局变量提供者: {0}"), ex.Message));
            }

            return result;
        }

        /// <summary> 等待用户点击确认按钮（支持超时自动确认），使用 ManualResetEvent 同步等待 Prism 事件 </summary>
        private async Task<DashboardConfirmResult> WaitForConfirmAsync(int timeoutMs, CancellationToken token)
        {
            DashboardConfirmResult result = DashboardConfirmResult.Continue;
            using var mre = new ManualResetEventSlim(false);
            var subToken = _ea.GetEvent<DashboardConfirmedEvent>().Subscribe(r =>
            {
                result = r;
                mre.Set();
            });

            try
            {
                if (timeoutMs > 0)
                {
                    bool signaled = await Task.Run(() => mre.Wait(timeoutMs, token), token);
                    if (!signaled)
                    {
                        _ea.GetEvent<DashboardConfirmedEvent>().Publish(DashboardConfirmResult.Continue);
                        await Task.Run(() => mre.Wait(token), token);
                    }
                }
                else
                {
                    await Task.Run(() => mre.Wait(token), token);
                }
            }
            finally
            {
                _ea.GetEvent<DashboardConfirmedEvent>().Unsubscribe(subToken);
            }

            return result;
        }
    }

    /// <summary> 全局变量提供者接口，StationTaskBase 可选实现 </summary>
    internal interface IGlobalVariableProvider
    {
        IEnumerable<GlobalVariable> GetGlobalVariables();
    }
}

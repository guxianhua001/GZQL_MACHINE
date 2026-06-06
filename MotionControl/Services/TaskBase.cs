using AlarmModule.Interfaces;
using AlarmModule.Models;
using Core.Utilities;
using MotionControl.Events;
using MotionControl.Exceptions;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Events;
using System;
using System.Threading;
using System.Threading.Tasks;
namespace MotionControl.Services
{
    public abstract class TaskBase : ITask
    {
        protected readonly IMotionService Motion;
        protected readonly IEventAggregator Ea;
        protected readonly ILoggerService Logger;
        /// <summary> 报警服务：用于在轴报警时触发报警记录 </summary>
        protected readonly IAlarmService AlarmService;
        protected CancellationTokenSource _cts;
        /// <summary> 请求取消任务循环，使 RunAsync 正常退出（不停止轴、不改变状态） </summary>
        protected void RequestCancelTaskLoop() => _cts?.Cancel();
        // 为子类暴露只读的取消令牌
        protected CancellationToken CurrentToken => _cts?.Token ?? CancellationToken.None;
        // 实现异步暂停/恢复
        private volatile bool _isPaused = false;
        private TaskCompletionSource<bool> _pauseTcs = new();
        public string TaskName { get; }
        public int TaskId { get; }
        public TaskState State { get; protected set; } = TaskState.Stopped;
        protected TaskBase(IMotionService motion, IEventAggregator ea, ILoggerService logger,
                           IAlarmService alarmService,
                           int taskId, string taskName)
        {
            Motion = motion;
            Ea = ea;
            Logger = logger;
            AlarmService = alarmService;
            TaskId = taskId;
            TaskName = taskName;
            Ea.GetEvent<AxisAlarmEvent>().Subscribe(OnAxisAlarm, ThreadOption.BackgroundThread);
        }
        public async Task RunAsync(CancellationToken token)
        {
            if (State == TaskState.Running) return;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            State = TaskState.Running;
            PublishTaskStatusChanged("Running", State);
            Logger.Info($"[{TaskName}] started");
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    // 执行子类工艺循环
                    await ExecuteCycleAsync(_cts.Token);
                    // 让出执行权，防止死循环卡死主线程/UI，保证 Cancel 信号能被即时捕获
                    await Task.Yield();
                }
                State = TaskState.Idle;
                PublishTaskStatusChanged("Completed", State);
                Logger.Info($"[{TaskName}] completed");
            }
            catch (OperationCanceledException)
            {
                State = TaskState.Stopped;
                PublishTaskStatusChanged("Stopped", State);
            }
            catch (StepFailureException sfe)
            {
                // 能走到这里的 StepFailureException，内部绝对不是 RecoverableException
                Logger.Error($"致命故障，任务 [{TaskName}] 在 [{sfe.StepName}] 步骤崩溃。内部异常: {sfe.InnerException?.Message}");
                State = TaskState.Error;
                await EmergencyStopAsync();
                // 通知全局状态服务：设备必须急停
                Ea.GetEvent<EmergencyStopAllEvent>().Publish();
            }
            catch (Exception ex)
            {
                Logger.Error($"[{TaskName}] 未知严重错误: {ex.Message}");
                State = TaskState.Error;
                await EmergencyStopAsync();
                Ea.GetEvent<EmergencyStopAllEvent>().Publish();
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }
        /// <summary>
        /// 步骤执行失败时抛出的异常，记录步骤名称和原始异常信息。
        /// </summary>
        public class StepFailureException : Exception
        {
            public string StepName { get; }
            public StepFailureException(string stepName, Exception innerException)
                : base($"步骤 [{stepName}] 执行失败", innerException)
            {
                StepName = stepName;
            }
        }

        protected virtual Task ExecuteCycleAsync(CancellationToken token) => Task.CompletedTask;
        /// <summary>
        /// 纯异步暂停检查，绝不阻塞线程
        /// </summary>
        protected async Task CheckPauseAsync(CancellationToken token)
        {
            while (_isPaused && !token.IsCancellationRequested)
            {
                _pauseTcs = new TaskCompletionSource<bool>();
                // 使用 WhenAny 保证暂停状态下也能响应取消
                var tcsTask = _pauseTcs.Task;
                if (await Task.WhenAny(tcsTask, Task.Delay(Timeout.Infinite, token)) == tcsTask)
                {
                    break; // 被 Resume 唤醒
                }
            }
        }
        public virtual Task PauseAsync()
        {
            if (State == TaskState.Running)
            {
                State = TaskState.Paused;
                _isPaused = true;
                Logger.Info($"[{TaskName}] paused");
                PublishTaskStatusChanged("Paused", State);
            }
            return Task.CompletedTask;
        }
        public virtual Task ResumeAsync()
        {
            if (State == TaskState.Paused)
            {
                State = TaskState.Running;
                _isPaused = false;
                // 异步通知解除暂停
                _pauseTcs.TrySetResult(true);
                Logger.Info($"[{TaskName}] resumed");
                PublishTaskStatusChanged("Running", State);
            }
            return Task.CompletedTask;
        }
        public virtual Task StopAsync()
        {
            //if (State == TaskState.Running || State == TaskState.Paused)
            //{
                foreach (var ax in GetAllAxes())
                    Motion.StopAxis(ax); 

                _cts?.Cancel();
                _isPaused = false;
                _pauseTcs.TrySetCanceled();
                State = TaskState.Stopped;

                // 通知 UI 任务状态已变为 Stopped
                PublishTaskStatusChanged("Stopped", State);
            //}
            return Task.CompletedTask;
        }
        public virtual async Task HomeAsync()
        {
            State = TaskState.Homing;
            PublishTaskStatusChanged("Homing", State);
            try
            {
                foreach (var ax in GetHomeAxes())
                    await Motion.HomeAsync(ax, GetHomeMode(ax));
                State = TaskState.Idle;
                PublishTaskStatusChanged("Idle", State);
            }
            catch
            {
                State = TaskState.Error;
                PublishTaskStatusChanged("Error", State);
                throw;
            }
        }

        public virtual Task EmergencyStopAsync()
        {
            //if (State == TaskState.Error) return Task.CompletedTask;

            // 1. 立即停止所有物理轴 (急停指令)
            foreach (var ax in GetAllAxes())
                Motion.EmergencyStop(ax);

            // 2. 取消任务循环
            _cts?.Cancel();
            _isPaused = false;
            _pauseTcs.TrySetCanceled();

            // 3. 标记状态为错误
            State = TaskState.Error;

            // 4. 通知 UI 任务状态已变为 Error (变红)
            PublishTaskStatusChanged("Emergency Stop", State);

            return Task.CompletedTask;
        }
        /// <summary> 获取当前任务管理的所有轴ID，默认返回空数组，由子类 override 或通过 DiscoverAxes 动态发现 </summary>
        protected virtual int[] GetAllAxes() => Array.Empty<int>();
        /// <summary> 获取需要执行回原点操作的轴ID，默认与 GetAllAxes 相同 </summary>
        protected virtual int[] GetHomeAxes() => GetAllAxes();
        protected virtual int GetHomeMode(int axisId) => 1;
        /// <summary>
        /// 轴报警回调：当轴报警信号变化时暂停任务并触发报警记录
        /// </summary>
        private void OnAxisAlarm(AxisAlarmPayload payload)
        {
            if (payload.IsAlarm && (State == TaskState.Running || State == TaskState.Paused))
            {
                // 触发报警记录到新AlarmModule
                _ = AlarmService.TriggerAlarmAsync(
                    "AXIS_ALARM",
                    AlarmLevel.Serious,
                    $"轴{payload.AxisId}报警",
                    source: $"Axis{payload.AxisId}",
                    type: AlarmType.HardwareFault);

                _ = PauseAsync();
            }
        }
        /// <summary>
        /// 发布可恢复异常事件，通知 UI 弹窗提示操作员
        /// </summary>
        /// <param name="stepName">发生故障的步骤名</param>
        /// <param name="rex">可恢复异常实例</param>
        protected void PublishRecoverableFault(string stepName, RecoverableException rex)
        {
            Ea.GetEvent<RecoverableFaultEvent>().Publish(new RecoverableFaultPayload
            {
                TaskId = TaskId,
                TaskName = TaskName,
                StepName = stepName,
                ErrorMessage = rex.Message,
                SuggestedAction = rex.SuggestedAction
            });
        }
        /// <summary>
        /// 发布任务状态变更事件，通知 UI 刷新显示
        /// </summary>
        protected void PublishTaskStatusChanged(string stepName, TaskState state)
        {
            Ea.GetEvent<TaskStatusChangedEvent>().Publish(new TaskStatusPayload
            {
                TaskId = TaskId,
                TaskName = TaskName,
                State = state,
                CurrentStepName = stepName
            });
        }
    }
}
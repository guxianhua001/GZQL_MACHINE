using Core.Utilities;
using Core.Abstraction;
using MotionControl.Events;
using MotionControl.Interfaces;
using Prism.Events;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MotionControl.Services
{
    public class StationTaskManager : ITaskManager
    {
        private readonly IStationRegistry _stationRegistry;
        private CancellationTokenSource _cts;
        private readonly IEventAggregator _ea;
        private readonly ILoggerService _logger;

        /// <summary>
        /// 通过工站注册表获取所有ITask实例，注册表是活集合，不受模块加载时序影响
        /// </summary>
        private IEnumerable<ITask> Tasks => _stationRegistry.GetAllStations().OfType<ITask>();

        public StationTaskManager(IStationRegistry stationRegistry, IEventAggregator ea, ILoggerService logger)
        {
            _stationRegistry = stationRegistry;
            _ea = ea;
            _logger = logger;
        }

        public async Task StartAllAsync()
        {
            _cts = new CancellationTokenSource();
            var runTasks = Tasks.Select(t => Task.Run(() => t.RunAsync(_cts.Token))).ToArray();
            try { await Task.WhenAll(runTasks); }
            catch (OperationCanceledException) { }
        }

        public Task StopAllAsync()
        {
            _cts?.Cancel();
            foreach (var t in Tasks) t.StopAsync();
            return Task.CompletedTask;
        }

        public Task PauseAllAsync()
        {
            foreach (var t in Tasks) t.PauseAsync();
            return Task.CompletedTask;
        }

        public Task ResumeAllAsync()
        {
            foreach (var t in Tasks) t.ResumeAsync();
            return Task.CompletedTask;
        }

        public Task EmergencyStopAllAsync()
        {
            _cts?.Cancel();
            foreach (var t in Tasks) t.EmergencyStopAsync();
            return Task.CompletedTask;
        }

        public async Task HomeAllAsync()
        {
            // 1. 并行启动所有工站的回零
            var homeTasks = Tasks.Select(t => t.HomeAsync()).ToArray();

            try
            {
                // 2. 等待所有工站回零完成
                await Task.WhenAll(homeTasks);
            }
            catch(TaskCanceledException ex)
            {
                _logger.Error($"部分工站回零失败！+ {ex.Message}");
            }
            // 3. 检查所有 Task 的最终状态，只有全部是 Idle 才算成功
            bool allIdle = Tasks.All(t => t.State == TaskState.Idle);

            if (allIdle)
            {
                _ea.GetEvent<SystemResetResultEvent>().Publish(true);
            }
            else
            {
                _ea.GetEvent<SystemResetResultEvent>().Publish(false);
            }
        }

        public void StepNextAll()
        {
            foreach (var t in Tasks.OfType<IHasStep>())
                t.StepNext();
        }

        public void EnableSingleStepAll()
        {
            foreach (var t in Tasks.OfType<IHasStep>())
                t.EnableSingleStep();
        }

        public void DisableSingleStepAll()
        {
            foreach (var t in Tasks.OfType<IHasStep>())
                t.DisableSingleStep();
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Services
{
    /// <summary>
    /// 任务间信号交互服务接口：用于不同 Task 之间的信号发送/等待交互。
    /// 信号语义为"一次性消费"（auto-reset）：发送后保持置位，被等待者消费后立即复位。
    /// </summary>
    public interface ITaskSignalService
    {
        /// <summary>发送（置位）指定名称的信号。若信号已置位则保持不变（幂等）。</summary>
        /// <param name="signalName">信号名称（全局唯一，建议使用有意义的名称）</param>
        void SendSignal(string signalName);

        /// <summary>
        /// 异步等待指定信号被置位，消费后自动复位信号。
        /// 支持无限等待或超时等待，超时返回 false，取消抛出 OperationCanceledException。
        /// </summary>
        /// <param name="signalName">信号名称</param>
        /// <param name="timeoutMs">超时时间（毫秒），&lt;=0 表示无限等待</param>
        /// <param name="token">取消令牌</param>
        /// <returns>true=收到信号并消费；false=超时未收到</returns>
        Task<bool> WaitForSignalAsync(string signalName, int timeoutMs, CancellationToken token);

        /// <summary>查询指定信号当前是否处于置位状态（不消费）</summary>
        bool IsSignalSet(string signalName);

        /// <summary>手动复位指定信号（强制清除置位状态）</summary>
        void ResetSignal(string signalName);
    }

    /// <summary>
    /// 任务间信号交互服务实现：基于 SemaphoreSlim(0,1) 实现一次性消费语义。
    /// 设计要点：
    /// - 发送信号：Release 信号量（计数从 0→1），已置位时为幂等操作
    /// - 等待信号：WaitAsync 信号量（计数从 1→0），自动复位
    /// - 跨 Task 安全：ConcurrentDictionary 保证多线程并发访问安全
    /// - 工业安全性：支持 CancellationToken 即时响应停止/急停
    /// </summary>
    public class TaskSignalService : ITaskSignalService
    {
        /// <summary>信号量池：每个信号名对应一个 SemaphoreSlim(0,1)</summary>
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

        /// <summary>信号置位标记：用于 IsSignalSet 查询，避免 WaitAsync 消费后无法查询历史状态</summary>
        private readonly ConcurrentDictionary<string, bool> _signalSet = new();

        /// <summary>
        /// 获取或创建指定信号的信号量。
        /// 初始计数=0（未置位），最大计数=1（二值信号）。
        /// </summary>
        private SemaphoreSlim GetOrCreateSemaphore(string signalName)
        {
            return _semaphores.GetOrAdd(signalName, _ => new SemaphoreSlim(0, 1));
        }

        /// <summary>
        /// 发送（置位）信号。
        /// 若信号已置位（信号量计数=1），Release 会抛出 SemaphoreFullException，捕获后忽略（幂等）。
        /// </summary>
        public void SendSignal(string signalName)
        {
            if (string.IsNullOrEmpty(signalName)) return;
            _signalSet[signalName] = true;
            var semaphore = GetOrCreateSemaphore(signalName);
            try
            {
                semaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // 信号已置位，幂等忽略
            }
        }

        /// <summary>
        /// 异步等待信号并消费（自动复位）。
        /// - timeoutMs &lt;= 0：无限等待，直到收到信号或被取消
        /// - timeoutMs &gt; 0：等待指定毫秒，超时返回 false
        /// 取消令牌触发时立即抛出 OperationCanceledException，确保急停/停止快速响应。
        /// </summary>
        public async Task<bool> WaitForSignalAsync(string signalName, int timeoutMs, CancellationToken token)
        {
            if (string.IsNullOrEmpty(signalName)) return false;

            var semaphore = GetOrCreateSemaphore(signalName);

            try
            {
                if (timeoutMs <= 0)
                {
                    // 无限等待：仅受取消令牌控制
                    await semaphore.WaitAsync(token).ConfigureAwait(false);
                }
                else
                {
                    // 超时等待：同时受超时和取消令牌控制
                    bool acquired = await semaphore.WaitAsync(timeoutMs, token).ConfigureAwait(false);
                    if (!acquired)
                    {
                        // 超时未收到信号
                        return false;
                    }
                }

                // 成功获取信号，消费后复位置位标记
                _signalSet[signalName] = false;
                return true;
            }
            catch (OperationCanceledException)
            {
                // 取消（急停/停止）：重新抛出，由上层处理
                throw;
            }
        }

        /// <summary>查询信号是否处于置位状态（不消费）</summary>
        public bool IsSignalSet(string signalName)
        {
            if (string.IsNullOrEmpty(signalName)) return false;
            return _signalSet.TryGetValue(signalName, out var isSet) && isSet;
        }

        /// <summary>手动复位信号（强制清除置位状态）</summary>
        public void ResetSignal(string signalName)
        {
            if (string.IsNullOrEmpty(signalName)) return;
            _signalSet[signalName] = false;
            // 尝试消费信号量中可能残留的计数
            if (_semaphores.TryGetValue(signalName, out var semaphore))
            {
                while (semaphore.CurrentCount > 0)
                {
                    semaphore.Wait(0);
                }
            }
        }
    }
}

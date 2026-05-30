using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Core.Utilities
{
    /// <summary>
    /// 全局日志缓存：线程安全的滑动窗口日志存储
    /// 使用ConcurrentQueue替代List，避免多线程并发访问异常
    /// </summary>
    public static class GlobalLogCache
    {
        private static readonly ConcurrentQueue<LogEventArgs> _logs = new ConcurrentQueue<LogEventArgs>();
        private const int MAX_CACHE_SIZE = 1000;

        public static void AddLog(LogEventArgs logEvent)
        {
            _logs.Enqueue(logEvent);

            // 超出容量时移除最早的条目
            while (_logs.Count > MAX_CACHE_SIZE)
            {
                _logs.TryDequeue(out _);
            }
        }

        public static IEnumerable<LogEventArgs> GetLogs()
        {
            return _logs.ToArray();
        }

        public static void Clear()
        {
            while (_logs.TryDequeue(out _)) { }
        }
    }
}

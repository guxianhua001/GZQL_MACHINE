using System.Collections.Generic;
using System.Linq;
using Core.Utilities;

namespace Core.Utilities
{
    public static class GlobalLogCache
    {
        private static readonly List<LogEventArgs> _logs = new List<LogEventArgs>();
        private const int MAX_CACHE_SIZE = 1000;

        public static void AddLog(LogEventArgs logEvent)
        {
            _logs.Add(logEvent);

            // 限制缓存大小
            if (_logs.Count > MAX_CACHE_SIZE)
            {
                _logs.RemoveAt(0);
            }
        }

        public static IEnumerable<LogEventArgs> GetLogs()
        {
            return _logs.ToList();
        }

        public static void Clear()
        {
            _logs.Clear();
        }
    }
}
using System.Collections.Concurrent;
using System.Threading;

namespace StationTasks.Services
{
    public interface IStationInteractionService
    {
        void SetSignal(string fullName, bool value);
        bool GetSignal(string fullName);
        bool WaitForSignal(string fullName, bool expectedValue, int timeoutMs = -1);
    }
    /// <summary>
    /// 工站交互服务，用于工站间通信。
    /// </summary>
    public class StationInteractionService : IStationInteractionService
    {
        private readonly ConcurrentDictionary<string, bool> _signals = new();
        private readonly ConcurrentDictionary<string, AutoResetEvent> _waiters = new();

        public void SetSignal(string fullName, bool value)
        {
            _signals[fullName] = value;
            if (_waiters.TryGetValue(fullName, out var are))
                are.Set();
        }

        public bool GetSignal(string fullName) =>
            _signals.TryGetValue(fullName, out var v) && v;

        public bool WaitForSignal(string fullName, bool expectedValue, int timeoutMs = -1)
        {
            while (!_signals.TryGetValue(fullName, out var current) || current != expectedValue)
            {
                var are = _waiters.GetOrAdd(fullName, _ => new AutoResetEvent(false));
                if (timeoutMs > 0)
                {
                    if (!are.WaitOne(timeoutMs))
                        return false;
                }
                else
                    are.WaitOne();
            }
            return true;
        }
    }
}
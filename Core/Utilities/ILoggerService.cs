
namespace Core.Utilities
{
    public interface ILoggerService
    {
        void Trace(string message);
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
        void Error(Exception ex, string message);
        void Fatal(string message);
        void Fatal(Exception ex, string message);

        // 添加日志事件，供LogViewer订阅
        event EventHandler<LogEventArgs> LogEvent;
    }

    public class LogEventArgs : EventArgs
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }

        public LogEventArgs(string level, string message, Exception exception = null)
        {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message;
            Exception = exception;
        }
    }
}

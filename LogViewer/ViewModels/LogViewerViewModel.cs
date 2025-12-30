using Core.Utilities;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Windows;

namespace Modules.LogViewer.ViewModels
{
    public class LogViewerViewModel : BindableBase
    {
        private readonly ILoggerService _loggerService;
        private ObservableCollection<LogEntry> _logEntries;
        private const int MAX_LOG_ENTRIES = 1000;
        // 事件用于通知视图有新的日志添加
        public event EventHandler LogEntryAdded;

        public LogViewerViewModel(ILoggerService loggerService)
        {
            _loggerService = loggerService;
            LogEntries = new ObservableCollection<LogEntry>();

            // 订阅日志事件
            _loggerService.LogEvent += OnLogEvent;

            // 加载历史日志
            LoadHistoricalLogs();
        }
        private void LoadHistoricalLogs()
        {
            // 这里可以从文件、数据库或其他存储中加载历史日志
            // 或者从全局缓存中获取
            var historicalLogs = GlobalLogCache.GetLogs();
            foreach (var log in historicalLogs)
            {
                LogEntries.Add(new LogEntry
                {
                    Timestamp = log.Timestamp,
                    Level = log.Level,
                    Message = log.Message,
                    Exception = log.Exception?.ToString()
                });
            }
        }
        public ObservableCollection<LogEntry> LogEntries
        {
            get => _logEntries;
            set => SetProperty(ref _logEntries, value);
        }

        private void OnLogEvent(object sender, LogEventArgs e)
        {
            // 在UI线程上更新日志条目
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogEntries.Add(new LogEntry
                {
                    Timestamp = e.Timestamp,
                    Level = e.Level,
                    Message = e.Message,
                    Exception = e.Exception?.ToString()
                });

                // 限制日志条目数量，防止内存溢出
                if (LogEntries.Count > MAX_LOG_ENTRIES)
                {
                    LogEntries.RemoveAt(0);
                }

                // 触发日志添加事件
                LogEntryAdded?.Invoke(this, EventArgs.Empty);
            });
        }
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string Exception { get; set; }
    }
}
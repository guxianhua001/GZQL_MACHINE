using Core.Utilities;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace LogViewer.ViewModels
{
    public class LogViewerViewModel : BindableBase
    {
        private readonly ILoggerService _loggerService;
        private ObservableCollection<LogEntry> _logEntries;
        private const int MAX_LOG_ENTRIES = 200;

        // 批量更新相关：收集日志条目，定时批量刷新到UI
        private readonly List<LogEntry> _pendingEntries = new List<LogEntry>();
        private readonly object _pendingLock = new object();
        private DispatcherTimer _batchTimer;
        private const int BATCH_INTERVAL_MS = 50;

        // 最新日志条目索引，用于高亮最后一行
        private int _lastLogIndex = -1;

        // 事件用于通知视图有新的日志添加
        public event EventHandler<LogEntryAddedEventArgs> LogEntryAdded;

        public LogViewerViewModel(ILoggerService loggerService)
        {
            _loggerService = loggerService;
            LogEntries = new ObservableCollection<LogEntry>();

            _loggerService.LogEvent += OnLogEvent;

            // 批量刷新定时器：每200ms将积攒的日志一次性刷新到UI
            _batchTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(BATCH_INTERVAL_MS)
            };
            _batchTimer.Tick += OnBatchTimerTick;
            _batchTimer.Start();

            LoadHistoricalLogs();
        }

        private void LoadHistoricalLogs()
        {
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
            if (LogEntries.Count > 0)
                _lastLogIndex = LogEntries.Count - 1;
        }

        public ObservableCollection<LogEntry> LogEntries
        {
            get => _logEntries;
            set => SetProperty(ref _logEntries, value);
        }

        /// <summary>
        /// 最新日志条目的索引，用于视图高亮最后一行
        /// </summary>
        public int LastLogIndex
        {
            get => _lastLogIndex;
            set => SetProperty(ref _lastLogIndex, value);
        }

        /// <summary>
        /// 日志事件处理：将日志条目暂存到待处理列表，不直接更新UI
        /// 避免高频日志时每条都触发Dispatcher.Invoke阻塞UI线程
        /// </summary>
        private void OnLogEvent(object sender, LogEventArgs e)
        {
            var entry = new LogEntry
            {
                Timestamp = e.Timestamp,
                Level = e.Level,
                Message = e.Message,
                Exception = e.Exception?.ToString()
            };

            lock (_pendingLock)
            {
                _pendingEntries.Add(entry);
            }
        }

        /// <summary>
        /// 批量刷新定时器回调：将积攒的日志一次性添加到ObservableCollection
        /// 减少CollectionChanged事件触发次数和DataGrid重渲染次数
        /// </summary>
        private void OnBatchTimerTick(object sender, EventArgs e)
        {
            List<LogEntry> batch;
            lock (_pendingLock)
            {
                if (_pendingEntries.Count == 0) return;
                batch = new List<LogEntry>(_pendingEntries);
                _pendingEntries.Clear();
            }

            // 批量添加到ObservableCollection
            foreach (var entry in batch)
            {
                LogEntries.Add(entry);
            }

            // 限制日志条目数量，防止内存溢出
            while (LogEntries.Count > MAX_LOG_ENTRIES)
            {
                LogEntries.RemoveAt(0);
            }

            // 更新最新日志索引
            LastLogIndex = LogEntries.Count - 1;

            // 通知视图有新日志（仅触发一次，而非每条日志触发一次）
            LogEntryAdded?.Invoke(this, new LogEntryAddedEventArgs(LastLogIndex));
        }
    }

    /// <summary>
    /// 日志添加事件参数，携带最新日志索引
    /// </summary>
    public class LogEntryAddedEventArgs : EventArgs
    {
        public int LastIndex { get; }
        public LogEntryAddedEventArgs(int lastIndex) { LastIndex = lastIndex; }
    }

    public class LogEntry : Prism.Mvvm.BindableBase
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string Exception { get; set; }

        private bool _isLatest;
        /// <summary>
        /// 标记是否为最新日志行，用于橘黄色高亮
        /// </summary>
        public bool IsLatest
        {
            get => _isLatest;
            set => SetProperty(ref _isLatest, value);
        }
    }
}

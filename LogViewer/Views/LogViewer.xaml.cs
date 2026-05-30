using LogViewer.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace LogViewer.Views
{
    /// <summary>
    /// LogViewer.xaml 的交互逻辑
    /// 性能优化：批量刷新、节流滚动、最新日志高亮
    /// </summary>
    public partial class LogViewer : UserControl
    {
        private ICollectionView _filteredLogEntries;
        private DispatcherTimer _scrollTimer;
        private bool _needsScrollToEnd;
        private int _lastHighlightedIndex = -1;

        public event PropertyChangedEventHandler PropertyChanged;

        public LogViewer()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LogViewerViewModel viewModel)
            {
                _filteredLogEntries = CollectionViewSource.GetDefaultView(viewModel.LogEntries);
                _filteredLogEntries.Filter = LogEntriesFilter;

                LogDataGrid.ItemsSource = _filteredLogEntries;

                viewModel.LogEntryAdded += OnLogEntryAdded;

                // 初始加载时标记最新日志并自动滚动
                if (viewModel.LogEntries.Count > 0)
                {
                    MarkLatestLog(viewModel.LogEntries.Count - 1);
                    if (AutoScrollToLast)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ScrollToLast();
                        }), DispatcherPriority.Background);
                    }
                }
            }

            // 节流滚动定时器
            _scrollTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _scrollTimer.Tick += OnScrollTimerTick;
            _scrollTimer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_scrollTimer != null)
            {
                _scrollTimer.Stop();
                _scrollTimer = null;
            }
        }

        private bool _autoScrollToLast = true;
        [Description("Automatically scrolls to the last log item in the viewer. Default is true.")]
        [Category("Behavior")]
        public bool AutoScrollToLast
        {
            get { return _autoScrollToLast; }
            set
            {
                if (_autoScrollToLast != value)
                {
                    _autoScrollToLast = value;
                    OnPropertyChanged(nameof(AutoScrollToLast));
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 标记最新日志行：清除旧高亮，设置新高亮
        /// </summary>
        private void MarkLatestLog(int newIndex)
        {
            if (DataContext is LogViewerViewModel viewModel)
            {
                // 清除旧的高亮标记
                if (_lastHighlightedIndex >= 0 && _lastHighlightedIndex < viewModel.LogEntries.Count)
                {
                    viewModel.LogEntries[_lastHighlightedIndex].IsLatest = false;
                }

                // 设置新的高亮标记
                if (newIndex >= 0 && newIndex < viewModel.LogEntries.Count)
                {
                    viewModel.LogEntries[newIndex].IsLatest = true;
                    _lastHighlightedIndex = newIndex;
                }
            }
        }

        /// <summary>
        /// 日志添加事件处理：标记最新日志、刷新过滤、节流滚动
        /// </summary>
        private void OnLogEntryAdded(object sender, LogEntryAddedEventArgs e)
        {
            // 标记最新日志行（橘黄色高亮）
            MarkLatestLog(e.LastIndex);

            // 刷新过滤视图
            _filteredLogEntries?.Refresh();

            // 标记需要滚动
            if (AutoScrollToLast)
            {
                _needsScrollToEnd = true;
            }
        }

        private void OnScrollTimerTick(object sender, EventArgs e)
        {
            if (_needsScrollToEnd)
            {
                _needsScrollToEnd = false;
                ScrollToLast();
            }
        }

        private bool LogEntriesFilter(object item)
        {
            if (LevelFilterComboBox.SelectedItem == null ||
                !(LevelFilterComboBox.SelectedItem is ComboBoxItem selectedItem))
                return true;

            string selectedTag = (selectedItem.Tag as string) ?? string.Empty;

            if (string.IsNullOrEmpty(selectedTag))
                return true;

            if (item is LogEntry logEntry)
            {
                return string.Equals(logEntry.Level, selectedTag, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private void LevelFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_filteredLogEntries != null)
            {
                _filteredLogEntries.Refresh();
            }
        }

        public void Clear()
        {
            if (DataContext is LogViewerViewModel viewModel)
            {
                viewModel.LogEntries.Clear();
                _lastHighlightedIndex = -1;
            }
        }

        public void ScrollToFirst()
        {
            if (LogDataGrid.Items.Count > 0)
            {
                LogDataGrid.ScrollIntoView(LogDataGrid.Items[0]);
                LogDataGrid.SelectedIndex = 0;
            }
        }

        public void ScrollToLast()
        {
            if (LogDataGrid.Items.Count > 0)
            {
                var lastItem = LogDataGrid.Items[LogDataGrid.Items.Count - 1];
                LogDataGrid.ScrollIntoView(lastItem);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Clear();
        }

        private void ScrollToFirstButton_Click(object sender, RoutedEventArgs e)
        {
            ScrollToFirst();
        }

        private void ScrollToLastButton_Click(object sender, RoutedEventArgs e)
        {
            ScrollToLast();
        }
    }
}

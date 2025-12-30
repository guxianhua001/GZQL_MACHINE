using Modules.LogViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Modules.LogViewer.Views
{
    /// <summary>
    /// LogViewer.xaml 的交互逻辑
    /// </summary>
    public partial class LogViewer : UserControl
    {
        private ICollectionView _filteredLogEntries;
        public event PropertyChangedEventHandler PropertyChanged;
        public LogViewer()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LogViewerViewModel viewModel)
            {
                // 创建过滤视图
                _filteredLogEntries = CollectionViewSource.GetDefaultView(viewModel.LogEntries);
                _filteredLogEntries.Filter = LogEntriesFilter;

                // 绑定到过滤后的视图
                LogDataGrid.ItemsSource = _filteredLogEntries;
                // 订阅日志添加事件
                viewModel.LogEntryAdded += (s, args) =>
                {
                    if (AutoScrollToLast)
                    {
                        ScrollToLast();
                    }
                    // 刷新过滤视图
                    _filteredLogEntries.Refresh();
                };
                // 初始加载时自动滚动到最后一条
                if (AutoScrollToLast && viewModel.LogEntries.Count > 0)
                {
                    // 使用Dispatcher延迟执行，确保UI已经完全加载
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ScrollToLast();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
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
        // 日志条目过滤方法
        private bool LogEntriesFilter(object item)
        {
            if (LevelFilterComboBox.SelectedItem == null ||
                !(LevelFilterComboBox.SelectedItem is ComboBoxItem selectedItem))
                return true;

            string selectedLevel = selectedItem.Content.ToString();

            if (selectedLevel == "All")
                return true;

            if (item is LogEntry logEntry)
            {
                return logEntry.Level == selectedLevel;
            }

            return false;
        }

        // 级别过滤组合框选择变化事件
        private void LevelFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_filteredLogEntries != null)
            {
                _filteredLogEntries.Refresh();
            }
        }

        // 公共方法：清除所有日志
        public void Clear()
        {
            if (DataContext is LogViewerViewModel viewModel)
            {
                viewModel.LogEntries.Clear();
            }
        }

        // 公共方法：滚动到第一条日志
        public void ScrollToFirst()
        {
            if (LogDataGrid.Items.Count > 0)
            {
                LogDataGrid.ScrollIntoView(LogDataGrid.Items[0]);
                LogDataGrid.SelectedIndex = 0;
            }
        }

        // 公共方法：滚动到最后一条日志
        public void ScrollToLast()
        {
            if (LogDataGrid.Items.Count > 0)
            {
                var lastItem = LogDataGrid.Items[LogDataGrid.Items.Count - 1];
                LogDataGrid.ScrollIntoView(lastItem);
                LogDataGrid.SelectedIndex = LogDataGrid.Items.Count - 1;
            }
        }
        // 清除按钮点击事件
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Clear();
        }

        // 滚动到第一条按钮点击事件
        private void ScrollToFirstButton_Click(object sender, RoutedEventArgs e)
        {
            ScrollToFirst();
        }

        // 滚动到最后一条按钮点击事件
        private void ScrollToLastButton_Click(object sender, RoutedEventArgs e)
        {
            ScrollToLast();
        }

    }
}

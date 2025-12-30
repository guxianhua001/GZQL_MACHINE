using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NLog;
using NLog.Common;

namespace NlogViewer
{
    /// <summary>
    /// Interaction logic for NlogViewer.xaml
    /// </summary>
    public partial class NlogViewer : UserControl
    {
        public ListView LogView { get { return logView; } }
        public event EventHandler ItemAdded = delegate { };
        public ObservableCollection<LogEventViewModel> LogEntries { get; private set; }
        public bool IsTargetConfigured { get; private set; }

        private double _TimeWidth = 120;
        [Description("Width of time column in pixels"), Category("Data")]
        [TypeConverter(typeof(LengthConverter))]
        public double TimeWidth
        {
            get { return _TimeWidth; }
            set { _TimeWidth = value; }
        }

        private double _LoggerNameWidth = 50;
        [Description("Width of Logger column in pixels, or auto if not specified"), Category("Data")]
        [TypeConverter(typeof(LengthConverter))]
        public double LoggerNameWidth
        {
            get { return _LoggerNameWidth; }
            set { _LoggerNameWidth = value; }
        }

        private double _LevelWidth = 50;
        [Description("Width of Level column in pixels"), Category("Data")]
        [TypeConverter(typeof(LengthConverter))]
        public double LevelWidth
        {
            get { return _LevelWidth; }
            set { _LevelWidth = value; }
        }

        private double _MessageWidth = 800;
        [Description("Width of Message column in pixels"), Category("Data")]
        [TypeConverter(typeof(LengthConverter))]
        public double MessageWidth
        {
            get { return _MessageWidth; }
            set
            {
                _MessageWidth = value;
                UpdateColumnWidths();
            }
        }

        private double _ExceptionWidth = 75;
        [Description("Width of Exception column in pixels"), Category("Data")]
        [TypeConverter(typeof(LengthConverter))]
        public double ExceptionWidth
        {
            get { return _ExceptionWidth; }
            set { _ExceptionWidth = value; }
        }

        private int _MaxRowCount = 150;
        [Description("The maximum number of row count. The oldest log gets deleted. Set to 0 for unlimited count."), Category("Data")]
        [TypeConverter(typeof(Int32Converter))]
        public int MaxRowCount
        {
            get { return _MaxRowCount; }
            set { _MaxRowCount = value; }
        }

        private bool _autoScrollToLast = true;
        [Description("Automatically scrolls to the last log item in the viewer. Default is true."), Category("Data")]
        [TypeConverter(typeof(BooleanConverter))]
        public bool AutoScrollToLast
        {
            get { return _autoScrollToLast; }
            set { _autoScrollToLast = value; }
        }

        public NlogViewer()
        {
            IsTargetConfigured = false;
            LogEntries = new ObservableCollection<LogEventViewModel>();

            InitializeComponent();

            // 禁用DPI缩放和自动缩放
            this.UseLayoutRounding = true;
            this.SnapsToDevicePixels = true;
            this.SizeChanged += (s, e) =>
            {
                // 禁止自动缩放
                this.Width = double.NaN; // Auto
                this.Height = double.NaN; // Auto
            };

            if (!DesignerProperties.GetIsInDesignMode(this))
            {
                foreach (NlogViewerTarget target in LogManager.Configuration.AllTargets.Where(t => t is NlogViewerTarget).Cast<NlogViewerTarget>())
                {
                    IsTargetConfigured = true;
                    target.LogReceived += LogReceived;
                }
            }

            Loaded += (s, e) =>
            {
                var listView = (ListView)logView;
                var scrollViewer = FindVisualChild<ScrollViewer>(listView);
                if (scrollViewer != null)
                {
                    scrollViewer.SizeChanged += (sender, args) => UpdateColumnWidths();
                }
            };
        }

        protected void LogReceived(AsyncLogEventInfo log)
        {
            LogEventViewModel vm = new LogEventViewModel(log.LogEvent);

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (MaxRowCount > 0 && LogEntries.Count >= MaxRowCount)
                    LogEntries.RemoveAt(0);

                LogEntries.Add(vm);

                // 启动滚动但不阻塞UI
                if (AutoScrollToLast)
                {
                    Task.Delay(100).ContinueWith(t =>
                    {
                        SafeBeginInvoke(ScrollToLast);
                    }, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }));
        }
        public void Clear()
        {
            LogEntries.Clear();
        }

        public void ScrollToFirst()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (LogView.Items.Count <= 0) return;
                try
                {
                    var firstItem = LogView.Items[0];
                    ScrollToItem(firstItem);
                }
                catch (Exception ex)
                {
                    // 可记录错误但不抛出，防止闪退
                    Debug.WriteLine($"ScrollToFirst错误: {ex.Message}");
                }
            }), DispatcherPriority.Background);
        }
        public void ScrollToLast()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (LogView.Items.Count <= 0) return;
                try
                {
                    var lastItem = LogView.Items[LogView.Items.Count - 1];
                    ScrollToItem(lastItem);
                }
                catch (Exception ex)
                {
                    // 可记录错误但不抛出，防止闪退
                    Debug.WriteLine($"ScrollToLast错误: {ex.Message}");
                }
            }), DispatcherPriority.Background);
        }

        private void ScrollToItem(object item)
        {
            if (item == null) return;
            // 确保使用安全的同步上下文
            if (LogView.Dispatcher.CheckAccess())
            {
                if (LogView.ItemContainerGenerator != null && LogView.Items.Contains(item))
                {
                    // 1. 确保列表项容器已生成
                    if (LogView.ItemContainerGenerator.ContainerFromItem(item) == null)
                    {
                        LogView.ScrollIntoView(item);
                        // 强制重排UI布局
                        UpdateLayout();
                    }

                    // 2. 更可靠的滚动方法
                    FrameworkElement container = LogView.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;

                    if (container != null)
                    {
                        container.BringIntoView();
                    }
                    else if (LogView.Items.Contains(item))
                    {
                        // 3. 当容器还未生成时
                        LogView.ScrollIntoView(item);
                    }
                }
            }
            else
            {
                // 确保在UI线程执行
                LogView.Dispatcher.BeginInvoke(new Action(() => ScrollToItem(item)), DispatcherPriority.Background);
            }
        }
        // 辅助方法（用于安全的延迟执行）
        private void SafeBeginInvoke(Action action, DispatcherPriority priority = DispatcherPriority.Background)
        {
            if (Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Dispatcher.BeginInvoke(action, priority);
            }
        }

        // 定义各列百分比依赖属性
        public static readonly DependencyProperty TimePercentageProperty =
            DependencyProperty.Register(
                "TimePercentage", typeof(double), typeof(NlogViewer),
                new PropertyMetadata(8.0, OnPercentageChanged)
            );

        public static readonly DependencyProperty LoggerNamePercentageProperty =
            DependencyProperty.Register(
                "LoggerNamePercentage", typeof(double), typeof(NlogViewer),
                new PropertyMetadata(12.0, OnPercentageChanged)
            );

        public static readonly DependencyProperty LevelPercentageProperty =
            DependencyProperty.Register(
                "LevelPercentage", typeof(double), typeof(NlogViewer),
                new PropertyMetadata(8.0, OnPercentageChanged)
            );

        public static readonly DependencyProperty MessagePercentageProperty =
            DependencyProperty.Register(
                "MessagePercentage", typeof(double), typeof(NlogViewer),
                new PropertyMetadata(62.0, OnPercentageChanged)
            );

        public static readonly DependencyProperty ExceptionPercentageProperty =
            DependencyProperty.Register(
                "ExceptionPercentage", typeof(double), typeof(NlogViewer),
                new PropertyMetadata(10.0, OnPercentageChanged)
            );

        // 属性封装
        public double TimePercentage
        {
            get => (double)GetValue(TimePercentageProperty);
            set => SetValue(TimePercentageProperty, value);
        }

        public double LoggerNamePercentage
        {
            get => (double)GetValue(LoggerNamePercentageProperty);
            set => SetValue(LoggerNamePercentageProperty, value);
        }

        public double LevelPercentage
        {
            get => (double)GetValue(LevelPercentageProperty);
            set => SetValue(LevelPercentageProperty, value);
        }

        public double MessagePercentage
        {
            get => (double)GetValue(MessagePercentageProperty);
            set => SetValue(MessagePercentageProperty, value);
        }

        public double ExceptionPercentage
        {
            get => (double)GetValue(ExceptionPercentageProperty);
            set => SetValue(ExceptionPercentageProperty, value);
        }

        // 百分比变化时触发宽度更新
        private static void OnPercentageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NlogViewer viewer)
            {
                viewer.UpdateColumnWidths();
            }
        }

        // 更新列宽
        private void UpdateColumnWidths()
        {
            if (logView.ActualWidth <= 0) return;

            double totalWidth = logView.ActualWidth - 25; // 减去滚动条宽度
            var gridView = (GridView)logView.View;

            // 先处理固定宽度的列
            if (LoggerNameWidth > 0)
                gridView.Columns[1].Width = LoggerNameWidth;
            else
                gridView.Columns[1].Width = 0; // 隐藏列
                                               // 其他列根据配置决定使用固定宽度还是百分比
            gridView.Columns[0].Width = TimeWidth > 0 ? TimeWidth : totalWidth * TimePercentage / 100;
            gridView.Columns[2].Width = LevelWidth > 0 ? LevelWidth : totalWidth * LevelPercentage / 100;
            gridView.Columns[3].Width = MessageWidth > 0 ? MessageWidth : totalWidth * MessagePercentage / 100;
            gridView.Columns[4].Width = ExceptionWidth > 0 ? ExceptionWidth : totalWidth * ExceptionPercentage / 100;

        }

        // 查找 ScrollViewer
        private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T result)
                    return result;
                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                    return descendant;
            }
            return null;
        }
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            // 阻止父容器缩放
            this.Width = double.NaN; // Auto
            this.Height = double.NaN; // Auto
        }
    }
}

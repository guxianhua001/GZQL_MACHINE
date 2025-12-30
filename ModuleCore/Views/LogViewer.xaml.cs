using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Media;
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

namespace ModuleCore.Views
{
    /// <summary>
    /// LogViewer.xaml 的交互逻辑
    /// </summary>
    public partial class LogViewer : UserControl
    {
        private bool _isSizeChangeHandled = false;
        public LogViewer()
        {
            InitializeComponent();
            //logCtrl.MessageWidth = 0;
            //logCtrl.MessagePercentage = 60;
            //logCtrl.ItemAdded += OnLogMessageItemAdded;
            this.Loaded += LogViewer_Loaded;
        }
        private void LogViewer_Loaded(object sender, RoutedEventArgs e)
        {
            //cbAutoScroll.IsChecked = true;
            //logCtrl.AutoScrollToLast = true;
        }
        private void OnLogMessageItemAdded(object o, EventArgs Args)
        {
            // Do what you want :)
            LogEventInfo logInfo = (NLogEvent)Args;
            if (logInfo.Level >= NLog.LogLevel.Error)
                SystemSounds.Beep.Play();
        }
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            logCtrl.Clear();
        }
        private void TopScroll_Click(object sender, RoutedEventArgs e)
        {
            logCtrl.ScrollToFirst();
        }
        private void BottomScroll_Click(object sender, RoutedEventArgs e)
        {
            logCtrl.ScrollToLast();
        }
        private void AutoScroll_Checked(object sender, RoutedEventArgs e)
        {
            //logCtrl.AutoScrollToLast = true;
        }
        private void AutoScroll_Unchecked(object sender, RoutedEventArgs e)
        {
            //logCtrl.AutoScrollToLast = false;
        }
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isSizeChangeHandled) return;
            _isSizeChangeHandled = true;
            // 固定窗口尺寸
            if (this.Parent is Window parentWindow)
            {
                // 防止窗口自动缩放
                parentWindow.SizeToContent = SizeToContent.Manual;

                // 确保窗口不会小于最小尺寸
                if (parentWindow.Width < parentWindow.MinWidth)
                    parentWindow.Width = parentWindow.MinWidth;

                if (parentWindow.Height < parentWindow.MinHeight)
                    parentWindow.Height = parentWindow.MinHeight;
            }
            _isSizeChangeHandled = false;
        }
        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            // 根据窗口大小自动调整消息列宽度(可选)
            if (sizeInfo.WidthChanged)
            {
                var newMessageWidth = sizeInfo.NewSize.Width * 0.6; // 占窗口60%宽度
                //logCtrl.MessageWidth = Math.Max(newMessageWidth, 300); // 最小300像素
            }
        }

    }
}

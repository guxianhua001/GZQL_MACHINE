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
using Prism.Ioc;
using LogViewer.ViewModels;

namespace ModuleCore.Views
{
    /// <summary>
    /// LogViewer.xaml 的交互逻辑
    /// </summary>
    public partial class LogView : UserControl
    {
        private bool _isSizeChangeHandled = false;
        private readonly IContainerProvider _container;

        public LogView(IContainerProvider container)
        {
            _container = container;
            InitializeComponent();
            this.Loaded += LogViewer_Loaded;
        }
        private void LogViewer_Loaded(object sender, RoutedEventArgs e)
        {
            // 确保内嵌的LogViewer控件启用自动滚动
            logCtrl.AutoScrollToLast = true;

            // 确保LogViewer获取正确的ViewModel（解决嵌套UserControl的AutoWireViewModel失效问题）
            if (logCtrl.DataContext == null || !(logCtrl.DataContext is LogViewerViewModel))
            {
                try
                {
                    var viewModel = _container.Resolve<LogViewerViewModel>();
                    logCtrl.DataContext = viewModel;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LogView] 无法解析LogViewerViewModel: {ex.Message}");
                }
            }
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

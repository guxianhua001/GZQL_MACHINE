using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows;

namespace Interfaces.Utilities
{
    /// <summary>
    /// 窗口管理工具类，提供窗口标识、查找、激活等功能的通用实现
    /// </summary>
    public static class WindowManager
    {
        #region 窗口标识常量
        public const string LOG_VIEWER_IDENTIFIER = "LogViewer";
        public const string LOG_VIEWER_NAME = "LogViewerWindow";
        #endregion
        /// <summary>
        /// 标记窗口身份
        /// </summary>
        /// <param name="window">目标窗口</param>
        /// <param name="identity">窗口标识值</param>
        /// <param name="windowName">窗口名称</param>
        public static void MarkWindowIdentity(Window window, string identity = LOG_VIEWER_IDENTIFIER,
                                             string windowName = LOG_VIEWER_NAME)
        {
            if (window == null) return;

            window.Tag = identity;
            window.Name = windowName;
        }
        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">事件参数</param>
        /// <param name="clearReference">清除弱引用回调</param>
        public static void OnLogWindowClosed(object sender, EventArgs e, Action clearReference = null)
        {
            if (sender is Window window)
            {
                window.Closed -= (s, args) => OnLogWindowClosed(s, args, clearReference);
                clearReference?.Invoke();
            }
        }
        /// <summary>
        /// 尝试获取已存在的相应窗口
        /// </summary>
        public static bool TryGetExistingWindow(string identity, out Window window)
        {
            window = Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.Tag?.ToString() == identity ||
                                     w.Name == identity ||
                                     w.Content?.GetType().Name == identity);

            return window != null;
        }
        /// <summary>
        /// 激活并置顶窗口
        /// </summary>
        public static void ActivateWindow(Window window)
        {
            if (window == null) return;
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;
            window.Activate();
            SetWindowTopMost(window);
            if (!window.IsVisible)
                window.Show();
        }
        #region Win32 API 调用
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        /// <summary>
        /// 设置窗口为最顶层
        /// </summary>
        /// <param name="window">目标窗口</param>
        /// <param name="foreground">是否将窗口置于最前</param>
        public static void SetWindowTopMost(Window window, bool foreground = true)
        {
            if (window == null) return;
            var hwnd = new WindowInteropHelper(window).Handle;
            if (foreground)
            {
                SetForegroundWindow(hwnd);
            }
        }
        #endregion
    }

}

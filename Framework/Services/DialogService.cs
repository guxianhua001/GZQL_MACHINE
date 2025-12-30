using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Framework.ViewModels;
using Framework.Views;
using MaterialDesignThemes.Wpf;

namespace Framework.Services
{
    public static class DialogService
    {
        // 跟踪所有打开的对话框的弱引用集合
        private static readonly object _dialogsLock = new object();
        // 使用线程安全的并发集合跟踪对话框
        private static readonly ConcurrentDictionary<Window, DialogHandle> _openDialogs =
            new ConcurrentDictionary<Window, DialogHandle>();

        private class DialogHandle
        {
            public TaskCompletionSource<object> Tcs { get; }
            public Dispatcher Dispatcher { get; }

            public DialogHandle(Dispatcher dispatcher)
            {
                Dispatcher = dispatcher;
                Tcs = new TaskCompletionSource<object>();
            }
        }

        /// <summary>
        /// 关闭所有已打开的对话框
        /// </summary>
        public static void CloseAllDialogs()
        {
            foreach (var dialogHandle in _openDialogs.Values)
            {
                dialogHandle.Dispatcher.Invoke(() =>
                {
                    dialogHandle.Tcs.TrySetResult(null);
                });
            }
            _openDialogs.Clear();
        }

        /// <summary>
        /// 注册对话框
        /// </summary>
        private static void RegisterDialog(Window dialog, DialogHandle handle)
        {
            void CleanUpHandler(object sender, EventArgs e)
            {
                dialog.Closed -= CleanUpHandler;
                _openDialogs.TryRemove(dialog, out _);
                handle.Tcs.TrySetResult(null);
            }

            dialog.Closed += CleanUpHandler;
            _openDialogs[dialog] = handle;
        }

        /// <summary>
        /// 显示非阻塞式对话框 (线程安全)
        /// </summary>
        /// <returns>用户选择结果 (true=确认, false=否定, null=取消或关闭)</returns>
        public static object? ShowNonBlockingDialog(
            string title,
            string message,
            string yesButtonText = "确认",
            string noButtonText = "取消",
            string extraButtonText = null,
            PackIconKind? icon = PackIconKind.Alert,
            bool showYesButton = true,
            bool showNoButton = true,
            bool showExtraButton = false)
        {
            // 移除之前的线程检查，支持在任何线程调用
            object? result = null;
            var waitHandle = new ManualResetEvent(false);
            if (Application.Current == null)
            {
                throw new InvalidOperationException("Application.Current 不可用");
            }
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 在新的STA线程中运行对话框
                var thread = new Thread(() =>
                {
                    try
                    {
                        var window = new MessageDialog();
                        var vm = new MessageDialogViewModel
                        {
                            Title = title,
                            Message = message,
                            IconKind = icon,
                            YesButtonText = yesButtonText,
                            NoButtonText = noButtonText,
                            ExtraButtonText = extraButtonText ?? "附加操作",
                            IsYesButtonVisible = showYesButton,
                            IsNoButtonVisible = showNoButton,
                            IsExtraButtonVisible = showExtraButton,
                            CloseCallback = (dialogResult) =>
                            {
                                result = dialogResult;
                                window.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
                            }
                        };
                        window.DataContext = vm;
                        window.Show();
                        Dispatcher.Run();
                    }
                    catch (Exception ex)
                    {
                        // 处理异常
                        Console.WriteLine(ex.ToString());
                    }
                    finally
                    {
                        waitHandle.Set();
                    }
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
            });
            waitHandle.WaitOne();
            return result;
        }

        /// <summary>
        /// 显示阻塞式对话框 (线程安全)
        /// </summary>
        /// <returns>用户选择结果 (0=确认, 1=取消, 2=附加操作, -1=用户关闭窗口)</returns>
        public static int ShowBlockingDialog(
            string title,
            string message,
            string yesButtonText = "确认",
            string noButtonText = "取消",
            string extraButtonText = null,
            PackIconKind? icon = PackIconKind.Alert,
            bool showYesButton = true,
            bool showNoButton = true,
            bool showExtraButton = false)
        {
            int result = -1; // 默认值保持 -1
            using (var waitHandle = new ManualResetEvent(false))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 解决Owner问题
                    Window ownerWindow = Application.Current.MainWindow;
                    if (ownerWindow is MessageDialog || ownerWindow == null || !ownerWindow.IsLoaded)
                    {
                        ownerWindow = null;
                    }

                    var window = new MessageDialog
                    {
                        Owner = ownerWindow,
                        WindowStartupLocation = ownerWindow != null
                            ? WindowStartupLocation.CenterOwner
                            : WindowStartupLocation.CenterScreen
                    };

                    var vm = new MessageDialogViewModel
                    {
                        Title = title,
                        Message = message,
                        IconKind = icon,
                        YesButtonText = yesButtonText,
                        NoButtonText = noButtonText,
                        ExtraButtonText = extraButtonText ?? "附加操作",
                        IsYesButtonVisible = showYesButton,
                        IsNoButtonVisible = showNoButton,
                        IsExtraButtonVisible = showExtraButton,
                        CloseCallback = (dialogResult) =>
                        {
                            // 通过回调设置结果
                            result = dialogResult is int intResult ? intResult : -1;
                            window.Close();
                        }
                    };

                    window.DataContext = vm;

                    // 简化关闭事件处理
                    window.Closed += (sender, e) =>
                    {
                        waitHandle.Set();
                    };

                    window.ShowDialog();
                });

                // 简单等待窗口关闭
                waitHandle.WaitOne();
                return result;
            }
        }

        /// <summary>
        /// 显示非阻塞对话框 (带自动关闭)
        /// </summary>
        public static void ShowNonBlockingDialog(
            string title,
            string message,
            PackIconKind? icon = null,
            int autoCloseTimeout = 3000)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var dialog = new MessageDialog
                {
                    DataContext = new MessageDialogViewModel
                    {
                        Title = title,
                        Message = message,
                        IconKind = icon,
                        IsYesButtonVisible = false,
                        IsNoButtonVisible = false,
                        AutoCloseTimeout = autoCloseTimeout
                    },
                    Owner = Application.Current.MainWindow
                };
                dialog.Show();
            }), DispatcherPriority.Background);
        }

        #region 支持buttons参数的方法
        /// <summary>
        /// 显示异步阻塞对话框（支持自定义按钮）
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="message">消息内容</param>
        /// <param name="buttons">按钮文本数组</param>
        /// <param name="defaultButtonIndex">默认选中按钮的索引</param>
        /// <param name="icon">图标类型</param>
        /// <param name="autoCloseTimeout">自动关闭超时时间（毫秒）</param>
        /// <param name="owner">对话框所有者窗口</param>
        public static Task<object> ShowDialogAsync(
            string title,
            string message,
            string[] buttons,
            int defaultButtonIndex = -1,
            PackIconKind? icon = PackIconKind.Alert,
            int autoCloseTimeout = 0,
            Window owner = null)
        {
            if (buttons == null || buttons.Length > 3)
                throw new ArgumentException("按钮数量必须为1-3个", nameof(buttons));

            if (defaultButtonIndex >= buttons.Length)
                throw new ArgumentException("默认按钮索引超出范围", nameof(defaultButtonIndex));
            // 确保在UI线程执行
            if (Application.Current?.Dispatcher == null)
                throw new InvalidOperationException("Application dispatcher not available");
            // 如果不是UI线程，切换到UI线程执行
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                return Application.Current.Dispatcher.Invoke(() =>
                    ShowDialogAsync(title, message, buttons, defaultButtonIndex,
                                    icon, autoCloseTimeout, owner));
            }
            var tcs = new TaskCompletionSource<object>();
            try
            {
                owner ??= Application.Current.MainWindow;

                var window = new MessageDialog
                {
                    Owner = owner,
                    WindowStartupLocation = owner != null ?
                        WindowStartupLocation.CenterOwner :
                        WindowStartupLocation.CenterScreen
                };
                var vm = new MessageDialogViewModel
                {
                    Title = title,
                    Message = message,
                    IconKind = icon,
                    AutoCloseTimeout = autoCloseTimeout,
                    CloseCallback = result =>
                    {
                        tcs.TrySetResult(result);
                        window.Close();
                    }
                };
                // 设置按钮显示和文本
                switch (buttons.Length)
                {
                    case 1:
                        vm.YesButtonText = buttons[0];
                        vm.IsYesButtonVisible = true;
                        vm.IsNoButtonVisible = false;
                        vm.IsExtraButtonVisible = false;
                        break;
                    case 2:
                        vm.YesButtonText = buttons[0];
                        vm.NoButtonText = buttons[1];
                        vm.IsYesButtonVisible = true;
                        vm.IsNoButtonVisible = true;
                        vm.IsExtraButtonVisible = false;
                        break;
                    case 3:
                        vm.YesButtonText = buttons[0];
                        vm.NoButtonText = buttons[1];
                        vm.ExtraButtonText = buttons[2];
                        vm.IsYesButtonVisible = true;
                        vm.IsNoButtonVisible = true;
                        vm.IsExtraButtonVisible = true;
                        break;
                }
                // 设置默认按钮
                switch (defaultButtonIndex)
                {
                    case 0:
                        vm.IsYesButtonDefault = true;
                        break;
                    case 1:
                        vm.IsNoButtonDefault = true;
                        break;
                    case 2:
                        vm.IsExtraButtonDefault = true;
                        break;
                        // -1 表示无默认按钮
                }
                // 设置按钮结果映射
                vm.SetButtonResults(buttons);
                window.DataContext = vm;
                RegisterDialog(window, new DialogHandle(window.Dispatcher));
                window.Show();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            return tcs.Task;
        }
        /// <summary>
        /// 显示异步阻塞对话框
        /// </summary>
        public static Task<object> ShowDialogAsync(
            string title,
            string message,
            string yesButtonText = "确认",
            string noButtonText = "取消",
            string extraButtonText = null,
            PackIconKind? icon = PackIconKind.Alert,
            bool showYesButton = true,
            bool showNoButton = true,
            bool showExtraButton = false,
            int autoCloseTimeout = 0,
            Window owner = null)
        {
            // 确保在UI线程执行
            if (Application.Current?.Dispatcher == null)
                throw new InvalidOperationException("Application dispatcher not available");
            // 如果不是UI线程，切换到UI线程执行
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                return Application.Current.Dispatcher.Invoke(() =>
                    ShowDialogAsync(title, message, yesButtonText, noButtonText, extraButtonText,
                                    icon, showYesButton, showNoButton, showExtraButton, autoCloseTimeout, owner));
            }
            var tcs = new TaskCompletionSource<object>();
            try
            {
                owner ??= Application.Current.MainWindow;

                var window = new MessageDialog
                {
                    Owner = owner,
                    WindowStartupLocation = owner != null ?
                        WindowStartupLocation.CenterOwner :
                        WindowStartupLocation.CenterScreen
                };
                var vm = new MessageDialogViewModel
                {
                    Title = title,
                    Message = message,
                    IconKind = icon,
                    AutoCloseTimeout = autoCloseTimeout,
                    CloseCallback = result =>
                    {
                        tcs.TrySetResult(result);
                        window.Close();
                    },
                    YesButtonText = yesButtonText,
                    NoButtonText = noButtonText,
                    ExtraButtonText = extraButtonText ?? "附加操作",
                    IsYesButtonVisible = showYesButton,
                    IsNoButtonVisible = showNoButton,
                    IsExtraButtonVisible = showExtraButton
                };
                window.DataContext = vm;
                RegisterDialog(window, new DialogHandle(window.Dispatcher));
                window.Show();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            return tcs.Task;
        }
        #endregion

        /// <summary>
        /// 显示简单异步提示框（自动关闭）
        /// </summary>
        public static Task ShowToastAsync(
            string title,
            string message,
            int autoCloseTimeout = 3000,
            PackIconKind? icon = null,
            Window owner = null)
        {
            return ShowDialogAsync(
                title,
                message,
                buttons: new[] { "确定" }, // 单个按钮的提示
                autoCloseTimeout: autoCloseTimeout,
                icon: icon,
                owner: owner
            );
        }
    }
}

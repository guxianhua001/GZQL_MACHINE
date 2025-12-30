using Interfaces;
using Framework.ViewModels;
using Framework.Views;
using Prism.Commands;
using System;
using System.Windows;

namespace Framework.Services
{
    public class PostDialPointMapService : IPostDialPointMapService
    {

        public void ShowPostDialVerification(ITaskWithPoints task, string visionResult)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 创建验证视图模型
                var verificationVM = new PostDialPointVerificationViewModel(task, visionResult);

                // 创建模态窗口
                var dialog = new Window
                {
                    Title = "拨针后点位状态确认",
                    Content = new PostDialPointVerificationView { DataContext = verificationVM },
                    SizeToContent = SizeToContent.WidthAndHeight,
                    MaxHeight = 800,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    WindowStyle = WindowStyle.ToolWindow,
                    ResizeMode = ResizeMode.NoResize,
                    Owner = Application.Current.MainWindow
                };

                // 安全设置 Owner
                try
                {
                    // 只有在主窗口已经显示的情况下才设置 Owner
                    if (Application.Current.MainWindow != null &&
                        Application.Current.MainWindow.IsLoaded)
                    {
                        dialog.Owner = Application.Current.MainWindow;
                        dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // 安全地处理异常，不设置 Owner
                    IMessage.Logger.Warn($"设置窗口Owner失败: {ex.Message}");
                }

                // 绑定确认命令
                verificationVM.ConfirmCommand = new DelegateCommand(() => dialog.Close());

                // 显示对话框
                dialog.Show();
            });
        }
    }
}

using Interfaces;
using Framework.ViewModels;
using Framework.Views;
using NLog;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Framework.Services
{
    public class PinMapService : IPinMapService
    {
        public void ShowStatusVerification(ITaskWithPoints task)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 创建地图视图模型
                var mapVM = new PinMapViewModel(task);

                // 创建验证视图模型
                var verificationVM = new PointStatusVerificationViewModel(task)
                {
                    MapViewModel = mapVM
                };

                // 创建模态窗口
                var dialog = new Window
                {
                    Title = "拨针力点位状态验证",
                    Content = new PointStatusVerificationView { DataContext = verificationVM },
                    SizeToContent = SizeToContent.WidthAndHeight,
                    MaxHeight = 800,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    WindowStyle = WindowStyle.ToolWindow,
                    ResizeMode = ResizeMode.NoResize,
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

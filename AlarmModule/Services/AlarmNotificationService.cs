using AlarmModule.Interfaces;
using AlarmModule.Models;
using Core.Utilities;
using System;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace AlarmModule.Services
{
    /// <summary>
    /// 报警通知服务实现：根据报警等级弹出不同级别的通知
    /// Level 1(紧急): 红色模态弹窗+持续蜂鸣
    /// Level 2(严重): 橙色模态弹窗+单次蜂鸣
    /// Level 3(一般): 黄色Toast通知+5秒自动消失
    /// Level 4(提示): 蓝色Toast通知+3秒自动消失
    /// </summary>
    public class AlarmNotificationService : IAlarmNotificationService
    {
        private readonly ILoggerService _logger;
        private CancellationTokenSource? _beepCts;

        /// <summary>
        /// 构造函数：注入日志服务
        /// </summary>
        public AlarmNotificationService(ILoggerService logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 显示报警通知：根据报警等级选择不同的通知方式
        /// </summary>
        public void ShowNotification(AlarmRecord alarm)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                switch (alarm.AlarmLevel)
                {
                    case AlarmLevel.Emergency:
                        ShowEmergencyDialog(alarm);
                        break;
                    case AlarmLevel.Serious:
                        ShowSeriousDialog(alarm);
                        break;
                    case AlarmLevel.General:
                        ShowToast(alarm, "#FFD600", 5000);
                        break;
                    case AlarmLevel.Prompt:
                        ShowToast(alarm, "#2979FF", 3000);
                        break;
                }
            });
        }

        /// <summary>
        /// 关闭所有通知：停止蜂鸣并清除Toast
        /// </summary>
        public void DismissAll()
        {
            StopBeep();
        }

        /// <summary>
        /// 显示紧急报警模态弹窗：红色背景+持续蜂鸣
        /// </summary>
        private void ShowEmergencyDialog(AlarmRecord alarm)
        {
            StartContinuousBeep();

            var dialogContent = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF1744")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(24),
                MinWidth = 420,
                MinHeight = 200,
                Child = new System.Windows.Controls.StackPanel
                {
                    Children =
                    {
                        new System.Windows.Controls.TextBlock
                        {
                            Text = "⚠ 紧急报警",
                            FontSize = 22,
                            FontWeight = FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.White,
                            Margin = new Thickness(0, 0, 0, 12)
                        },
                        new System.Windows.Controls.TextBlock
                        {
                            Text = $"报警代码: {alarm.AlarmCode}",
                            FontSize = 14,
                            Foreground = System.Windows.Media.Brushes.White,
                            Margin = new Thickness(0, 0, 0, 4)
                        },
                        new System.Windows.Controls.TextBlock
                        {
                            Text = $"报警来源: {alarm.AlarmSource}",
                            FontSize = 14,
                            Foreground = System.Windows.Media.Brushes.White,
                            Margin = new Thickness(0, 0, 0, 4)
                        },
                        new System.Windows.Controls.TextBlock
                        {
                            Text = $"描述: {alarm.Description}",
                            FontSize = 14,
                            Foreground = System.Windows.Media.Brushes.White,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 16)
                        },
                        new System.Windows.Controls.Button
                        {
                            Content = "确认",
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Style = (Style)Application.Current.FindResource("MaterialDesignRaisedButton"),
                            Command = new Prism.Commands.DelegateCommand(() =>
                            {
                                StopBeep();
                                MaterialDesignThemes.Wpf.DialogHost.Close("MainDialogHost");
                            })
                        }
                    }
                }
            };

            MaterialDesignThemes.Wpf.DialogHost.Show(dialogContent, "MainDialogHost");
        }

        /// <summary>
        /// 显示严重报警模态弹窗：橙色背景+单次蜂鸣
        /// </summary>
        private void ShowSeriousDialog(AlarmRecord alarm)
        {
            SystemSounds.Exclamation.Play();

            var dialogContent = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF9100")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(24),
                MinWidth = 420,
                MinHeight = 180,
                Child = new System.Windows.Controls.StackPanel
                {
                    Children =
                    {
                        new System.Windows.Controls.TextBlock
                        {
                            Text = "⚡ 严重报警",
                            FontSize = 22,
                            FontWeight = FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.White,
                            Margin = new Thickness(0, 0, 0, 12)
                        },
                        new System.Windows.Controls.TextBlock
                        {
                            Text = $"报警代码: {alarm.AlarmCode}",
                            FontSize = 14,
                            Foreground = System.Windows.Media.Brushes.White,
                            Margin = new Thickness(0, 0, 0, 4)
                        },
                        new System.Windows.Controls.TextBlock
                        {
                            Text = $"报警来源: {alarm.AlarmSource}",
                            FontSize = 14,
                            Foreground = System.Windows.Media.Brushes.White,
                            Margin = new Thickness(0, 0, 0, 4)
                        },
                        new System.Windows.Controls.TextBlock
                        {
                            Text = $"描述: {alarm.Description}",
                            FontSize = 14,
                            Foreground = System.Windows.Media.Brushes.White,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 0, 0, 16)
                        },
                        new System.Windows.Controls.Button
                        {
                            Content = "确认",
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Style = (Style)Application.Current.FindResource("MaterialDesignRaisedButton"),
                            Command = new Prism.Commands.DelegateCommand(() =>
                            {
                                MaterialDesignThemes.Wpf.DialogHost.Close("MainDialogHost");
                            })
                        }
                    }
                }
            };

            MaterialDesignThemes.Wpf.DialogHost.Show(dialogContent, "MainDialogHost");
        }

        /// <summary>
        /// 显示Toast通知：指定背景色和自动消失时间
        /// 左下角弹窗，显示报警代码、来源、描述完整信息
        /// </summary>
        private void ShowToast(AlarmRecord alarm, string hexColor, int dismissAfterMs)
        {
            var toast = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(8),
                MinWidth = 340,
                MaxWidth = 500,
                MinHeight = 80,
                Child = new System.Windows.Controls.StackPanel
                {
                    Children =
                    {
                        new System.Windows.Controls.TextBlock
                        {
                            Text = alarm.AlarmLevel == AlarmLevel.General ? "⚠ 一般报警" : "ℹ 提示预警",
                            FontSize = 15,
                            FontWeight = FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.Black,
                            Margin = new Thickness(0, 0, 0, 8)
                        },
                        new System.Windows.Controls.TextBlock
                        {
                            Text = $"代码: {alarm.AlarmCode}",
                            FontSize = 13,
                            Foreground = System.Windows.Media.Brushes.Black,
                            Margin = new Thickness(0, 0, 0, 4)
                        },
                        new System.Windows.Controls.TextBlock
                        {
                            Text = $"来源: {alarm.AlarmSource}",
                            FontSize = 13,
                            Foreground = new System.Windows.Media.SolidColorBrush(
                                System.Windows.Media.Colors.DarkGray),
                            Margin = new Thickness(0, 0, 0, 4)
                        },
                        new System.Windows.Controls.TextBlock
                        {
                            Text = alarm.Description,
                            FontSize = 13,
                            Foreground = System.Windows.Media.Brushes.Black,
                            TextWrapping = TextWrapping.Wrap,
                            MaxHeight = 120,
                            Margin = new Thickness(0, 0, 0, 0)
                        }
                    }
                }
            };

            var popup = new System.Windows.Controls.Primitives.Popup
            {
                Child = toast,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                PlacementTarget = Application.Current.MainWindow,
                VerticalOffset = -120,
                HorizontalOffset = 20,
                IsOpen = true,
                StaysOpen = false,
                AllowsTransparency = true
            };

            Task.Delay(dismissAfterMs).ContinueWith(_ =>
            {
                try
                {
                    var dispatcher = Application.Current?.Dispatcher;
                    if (dispatcher == null || dispatcher.HasShutdownStarted)
                        return;
                    dispatcher.Invoke(() => popup.IsOpen = false);
                }
                catch (TaskCanceledException) { }
                catch (OperationCanceledException) { }
            });
        }

        /// <summary>
        /// 启动持续蜂鸣：在后台线程循环播放系统提示音，直到被停止
        /// </summary>
        private void StartContinuousBeep()
        {
            StopBeep();
            _beepCts = new CancellationTokenSource();
            var token = _beepCts.Token;

            Task.Run(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        SystemSounds.Exclamation.Play();
                        Task.Delay(800, token).Wait(token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, token);
        }

        /// <summary>
        /// 停止持续蜂鸣
        /// </summary>
        private void StopBeep()
        {
            _beepCts?.Cancel();
            _beepCts?.Dispose();
            _beepCts = null;
        }
    }
}

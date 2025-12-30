using Framework.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using Interfaces.SharedInterfaces;

namespace Framework
{
    // JogButtonHelper附加属性类
    // 完整的JogButtonHelper.cs
    public static class JogButtonHelper
    {
        #region IsJogControl 属性（之前已定义）
        public static readonly DependencyProperty IsJogControlProperty =
            DependencyProperty.RegisterAttached("IsJogControl", typeof(bool), typeof(JogButtonHelper),
            new PropertyMetadata(false, OnIsJogControlChanged));

        public static bool GetIsJogControl(DependencyObject obj) => (bool)obj.GetValue(IsJogControlProperty);
        public static void SetIsJogControl(DependencyObject obj, bool value) => obj.SetValue(IsJogControlProperty, value);
        #endregion

        #region JogDirection 属性
        public static readonly DependencyProperty JogDirectionProperty =
            DependencyProperty.RegisterAttached("JogDirection", typeof(string), typeof(JogButtonHelper),
            new PropertyMetadata("Positive"));

        public static string GetJogDirection(DependencyObject obj) => (string)obj.GetValue(JogDirectionProperty);
        public static void SetJogDirection(DependencyObject obj, string value) => obj.SetValue(JogDirectionProperty, value);
        #endregion

        #region JogDirectionEnum 属性（支持枚举类型方向）
        public static readonly DependencyProperty JogDirectionEnumProperty =
            DependencyProperty.RegisterAttached("JogDirectionEnum", typeof(JogDirection?), typeof(JogButtonHelper),
            new PropertyMetadata(null));

        public static JogDirection? GetJogDirectionEnum(DependencyObject obj) => (JogDirection?)obj.GetValue(JogDirectionEnumProperty);
        public static void SetJogDirectionEnum(DependencyObject obj, JogDirection? value) => obj.SetValue(JogDirectionEnumProperty, value);
        #endregion

        private static void OnIsJogControlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Button button)
            {
                // 移除旧事件
                button.PreviewMouseLeftButtonDown -= HandleMouseDown;
                button.PreviewMouseLeftButtonUp -= HandleMouseUp;
                button.LostMouseCapture -= HandleLostCapture;
                button.LostFocus -= HandleLostFocus;

                if ((bool)e.NewValue)
                {
                    // 添加新事件
                    button.PreviewMouseLeftButtonDown += HandleMouseDown;
                    button.PreviewMouseLeftButtonUp += HandleMouseUp;
                    button.LostMouseCapture += HandleLostCapture;
                    button.LostFocus += HandleLostFocus;
                }
            }
        }

        private static void HandleMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button && GetIsJogControl(button))
            {
                var direction = GetJogDirection(button);
                var enumDirection = GetJogDirectionEnum(button);

                if (button.DataContext is AxisViewModel axisVM)
                {
                    // 根据字符串方向参数转换
                    var directionValue = direction switch
                    {
                        "Negative" => 0,
                        "Positive" => 1,
                        _ => throw new InvalidOperationException("未知的Jog方向")
                    };
                    axisVM.StartJog(directionValue);
                }
                else if (button.DataContext is ControlPanelViewModel controlPanelVM)
                {
                    // 处理新的控制面板视图模型
                    if (enumDirection.HasValue)
                    {
                        // 通过命令执行移动
                        controlPanelVM.JogCommand?.Execute(enumDirection.Value);
                    }
                }
                else if (button.DataContext is AssemblyStationViewModel assemblyStationVM)
                {
                    // 处理AssemblyStationViewModel
                    if (enumDirection.HasValue)
                    {
                        assemblyStationVM.GripperJogCommand?.Execute(enumDirection.Value);
                    }
                }
                e.Handled = true; // 阻止双击事件
            }
        }

        private static void HandleMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button && GetIsJogControl(button))
            {
                ExecuteStop(button.DataContext);
            }
        }

        private static void HandleLostCapture(object sender, MouseEventArgs e)
        {
            if (sender is Button button && GetIsJogControl(button))
            {
                ExecuteStop(button.DataContext);
            }
        }

        private static void HandleLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && GetIsJogControl(button))
            {
                ExecuteStop(button.DataContext);
            }
        }

        // 统一停止处理方法
        private static void ExecuteStop(object dataContext)
        {
            if (dataContext is AxisViewModel axisVM)
            {
                axisVM.ExecuteStop();
            }
            else if (dataContext is ControlPanelViewModel controlPanelVM)
            {
                controlPanelVM.StopJogCommand?.Execute();
            }
            else if (dataContext is AssemblyStationViewModel assemblyStationVM)
            {
                assemblyStationVM.StopGripperJogCommand?.Execute();
            }
        }

    }
}

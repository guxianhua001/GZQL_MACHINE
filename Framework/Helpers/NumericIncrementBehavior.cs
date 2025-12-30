using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors;

namespace Framework.Helpers
{
    public class NumericIncrementBehavior : Behavior<DecimalUpDown>
    {
        public static readonly DependencyProperty IncrementProperty =
            DependencyProperty.Register("Increment", typeof(double),
            typeof(NumericIncrementBehavior),
            new PropertyMetadata(1.0, OnIncrementChanged));

        public double Increment
        {
            get => (double)GetValue(IncrementProperty);
            set => SetValue(IncrementProperty, value);
        }

        private RepeatButton _increaseButton;
        private RepeatButton _decreaseButton;

        protected override void OnAttached()
        {
            base.OnAttached();

            System.Diagnostics.Debug.WriteLine("🔧 NumericIncrementBehavior 已附加");

            // 监听键盘事件
            AssociatedObject.PreviewKeyDown += OnPreviewKeyDown;

            // 延迟设置按钮事件
            AssociatedObject.Loaded += OnNumericUpDownLoaded;
        }

        protected override void OnDetaching()
        {
            System.Diagnostics.Debug.WriteLine("🔧 NumericIncrementBehavior 已分离");

            if (AssociatedObject != null)
            {
                AssociatedObject.Loaded -= OnNumericUpDownLoaded;
                AssociatedObject.PreviewKeyDown -= OnPreviewKeyDown;
                CleanupButtonEvents();
            }
            base.OnDetaching();
        }

        private void OnNumericUpDownLoaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("🔧 NumericUpDown 已加载");

            // 延迟执行，确保模板应用完成
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                SetupButtonEvents();
                TrySetIncrementProperty();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private static void OnIncrementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NumericIncrementBehavior behavior && behavior.AssociatedObject != null)
            {
                behavior.TrySetIncrementProperty();
            }
        }

        private void TrySetIncrementProperty()
        {
            if (AssociatedObject == null) return;

            try
            {
                // 尝试设置步长属性
                var propertiesToTry = new[] { "Increment", "ValueIncrement", "NumericStep", "Step" };

                foreach (var propName in propertiesToTry)
                {
                    var property = AssociatedObject.GetType().GetProperty(propName);
                    if (property != null && property.CanWrite && property.PropertyType == typeof(double))
                    {
                        try
                        {
                            property.SetValue(AssociatedObject, Increment);
                            System.Diagnostics.Debug.WriteLine($"✅ 成功设置 {propName} = {Increment}");
                            break;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"❌ 设置 {propName} 失败: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ TrySetIncrementProperty 失败: {ex.Message}");
            }
        }

        private void SetupButtonEvents()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("🔍 开始查找 RepeatButton...");

                // 查找 RepeatButton 而不是 Button
                _increaseButton = FindVisualChild<RepeatButton>(AssociatedObject, "PART_IncreaseButton");
                _decreaseButton = FindVisualChild<RepeatButton>(AssociatedObject, "PART_DecreaseButton");

                System.Diagnostics.Debug.WriteLine($"🔍 增加按钮: {_increaseButton != null}");
                System.Diagnostics.Debug.WriteLine($"🔍 减少按钮: {_decreaseButton != null}");

                if (_increaseButton != null)
                {
                    _increaseButton.PreviewMouseLeftButtonDown -= OnIncrementPreview;
                    _increaseButton.PreviewMouseLeftButtonDown += OnIncrementPreview;
                    _increaseButton.Click -= OnIncrementClick;
                    _increaseButton.Click += OnIncrementClick;
                    System.Diagnostics.Debug.WriteLine("✅ 增加按钮事件已注册");
                }

                if (_decreaseButton != null)
                {
                    _decreaseButton.PreviewMouseLeftButtonDown -= OnDecrementPreview;
                    _decreaseButton.PreviewMouseLeftButtonDown += OnDecrementPreview;
                    _decreaseButton.Click -= OnDecrementClick;
                    _decreaseButton.Click += OnDecrementClick;
                    System.Diagnostics.Debug.WriteLine("✅ 减少按钮事件已注册");
                }

                // 如果没有找到按钮，尝试直接设置 RepeatButton 的 Interval
                if (_increaseButton != null || _decreaseButton != null)
                {
                    SetRepeatButtonInterval();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 设置按钮事件时出错: {ex.Message}");
            }
        }

        private void SetRepeatButtonInterval()
        {
            try
            {
                // 设置 RepeatButton 的间隔，使其响应更快
                if (_increaseButton != null)
                {
                    _increaseButton.Interval = 100; // 毫秒
                    _increaseButton.Delay = 300;    // 延迟
                }

                if (_decreaseButton != null)
                {
                    _decreaseButton.Interval = 100;
                    _decreaseButton.Delay = 300;
                }

                System.Diagnostics.Debug.WriteLine("✅ RepeatButton 间隔已设置");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 设置 RepeatButton 间隔失败: {ex.Message}");
            }
        }

        private void CleanupButtonEvents()
        {
            try
            {
                if (_increaseButton != null)
                {
                    _increaseButton.PreviewMouseLeftButtonDown -= OnIncrementPreview;
                    _increaseButton.Click -= OnIncrementClick;
                }

                if (_decreaseButton != null)
                {
                    _decreaseButton.PreviewMouseLeftButtonDown -= OnDecrementPreview;
                    _decreaseButton.Click -= OnDecrementClick;
                }

                System.Diagnostics.Debug.WriteLine("✅ 按钮事件已清理");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 清理按钮事件时出错: {ex.Message}");
            }
        }

        private void OnIncrementPreview(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("🔼 PreviewMouseLeftButtonDown - 增加");
            if (AssociatedObject != null && AssociatedObject.IsEnabled)
            {
                HandleIncrement();
                e.Handled = true; // 阻止默认行为
            }
        }

        private void OnIncrementClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("🔼 Click - 增加");
            if (AssociatedObject != null && AssociatedObject.IsEnabled)
            {
                HandleIncrement();
                e.Handled = true; // 阻止默认行为
            }
        }

        private void OnDecrementPreview(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("🔽 PreviewMouseLeftButtonDown - 减少");
            if (AssociatedObject != null && AssociatedObject.IsEnabled)
            {
                HandleDecrement();
                e.Handled = true; // 阻止默认行为
            }
        }

        private void OnDecrementClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("🔽 Click - 减少");
            if (AssociatedObject != null && AssociatedObject.IsEnabled)
            {
                HandleDecrement();
                e.Handled = true; // 阻止默认行为
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"⌨️ 按键: {e.Key}");
            if (AssociatedObject != null && AssociatedObject.IsEnabled)
            {
                if (e.Key == Key.Up)
                {
                    HandleIncrement();
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    HandleDecrement();
                    e.Handled = true;
                }
            }
        }

        private void HandleIncrement()
        {
            System.Diagnostics.Debug.WriteLine("🎯 执行 HandleIncrement");
            if (AssociatedObject != null)
            {
                double currentValue = (double)AssociatedObject.Value;
                var newValue = currentValue + Increment;
                System.Diagnostics.Debug.WriteLine($"📊 当前值: {currentValue}, 增量: {Increment}, 新值: {newValue}");

                if (newValue <= (double)AssociatedObject.Maximum)
                {
                    AssociatedObject.Value = (decimal)newValue;
                    System.Diagnostics.Debug.WriteLine($"✅ 设置新值: {AssociatedObject.Value}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 新值 {newValue} 超过最大值 {AssociatedObject.Maximum}");
                }
            }
        }

        private void HandleDecrement()
        {
            System.Diagnostics.Debug.WriteLine("🎯 执行 HandleDecrement");
            if (AssociatedObject != null)
            {
                double currentValue = (double)AssociatedObject.Value;
                var newValue = currentValue - Increment;
                System.Diagnostics.Debug.WriteLine($"📊 当前值: {currentValue}, 减量: {Increment}, 新值: {newValue}");

                if (newValue >= (double)AssociatedObject.Minimum)
                {
                    AssociatedObject.Value = (decimal)newValue;
                    System.Diagnostics.Debug.WriteLine($"✅ 设置新值: {AssociatedObject.Value}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"❌ 新值 {newValue} 低于最小值 {AssociatedObject.Minimum}");
                }
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent, string childName = null) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T foundChild && (string.IsNullOrEmpty(childName) || (child as FrameworkElement)?.Name == childName))
                {
                    return foundChild;
                }

                var descendant = FindVisualChild<T>(child, childName);
                if (descendant != null)
                {
                    return descendant;
                }
            }
            return null;
        }
    }
}
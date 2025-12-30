using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Framework.Helpers
{
    public static class NumericFormatHelper
    {
        public static readonly DependencyProperty FormatStringProperty =
            DependencyProperty.RegisterAttached("FormatString",
                typeof(string),
                typeof(NumericFormatHelper),
                new PropertyMetadata("F2", OnFormatStringChanged));

        public static string GetFormatString(DependencyObject obj)
        {
            return (string)obj.GetValue(FormatStringProperty);
        }

        public static void SetFormatString(DependencyObject obj, string value)
        {
            obj.SetValue(FormatStringProperty, value);
        }

        private static void OnFormatStringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is NumericUpDown numericUpDown)) return;

            // 确保在控件加载后应用格式化
            if (numericUpDown.IsLoaded)
            {
                ApplyFormatting(numericUpDown);
            }
            else
            {
                numericUpDown.Loaded += (s, args) => ApplyFormatting(numericUpDown);
            }
        }

        private static void ApplyFormatting(NumericUpDown numericUpDown)
        {
            var formatString = GetFormatString(numericUpDown) ?? "F2";

            // 使用更可靠的方式查找内部 TextBox
            var textBox = FindVisualChild<TextBox>(numericUpDown);
            if (textBox != null)
            {
                // 清除现有绑定
                BindingOperations.ClearBinding(textBox, TextBox.TextProperty);

                // 创建新的格式化绑定
                var binding = new Binding("Value")
                {
                    Source = numericUpDown,
                    StringFormat = formatString,
                    Mode = BindingMode.OneWay
                };

                textBox.SetBinding(TextBox.TextProperty, binding);
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }
            return null;
        }
    }
}
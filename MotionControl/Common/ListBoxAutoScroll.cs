using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
namespace MotionControl.Common
{
    /// <summary>
    /// 让 ListBox 在数据源变化时自动滚动到底部的附加属性
    /// </summary>
    public class ListBoxAutoScroll
    {
        public static bool GetIsEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsEnabledProperty);
        }
        public static void SetIsEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsEnabledProperty, value);
        }
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(ListBoxAutoScroll),
                new UIPropertyMetadata(false, OnIsEnabledChanged));
        private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var listBox = d as ListBox;
            if (listBox == null) return;
            if ((bool)e.NewValue)
            {
                listBox.Loaded += ListBox_Loaded;
            }
            else
            {
                listBox.Loaded -= ListBox_Loaded;
            }
        }
        private static void ListBox_Loaded(object sender, RoutedEventArgs e)
        {
            var listBox = (ListBox)sender;
            // 监听 ItemsSource 的集合变化
            if (listBox.Items.SourceCollection is INotifyCollectionChanged items)
            {
                items.CollectionChanged += (s, args) =>
                {
                    if (args.Action == NotifyCollectionChangedAction.Add)
                    {
                        // 当有新项添加时，滚动到最后一个项
                        if (listBox.Items.Count > 0)
                        {
                            listBox.ScrollIntoView(listBox.Items[listBox.Items.Count - 1]);
                        }
                    }
                };
            }
        }
    }
}
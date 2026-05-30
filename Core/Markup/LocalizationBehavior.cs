using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using Core.Abstraction;
using Core.Attributes;
using Prism.Ioc;
using Prism.Mvvm;

namespace Core.Markup
{
    /// <summary>
    /// 本地化行为附加属性——语言切换时自动刷新 ViewModel 中标记了 [Localized] 特性的属性。
    /// 使用方式：在 XAML 中添加 lang:LocalizationBehavior.AutoRefresh="True"
    /// </summary>
    public class LocalizationBehavior : DependencyObject
    {
        /// <summary>
        /// 存储 FrameworkElement 与其事件处理器的弱关联，元素被 GC 回收时自动清理
        /// </summary>
        private static readonly ConditionalWeakTable<FrameworkElement, EventHandler<LanguageChangedEventArgs>>
            _subscriptions = new ConditionalWeakTable<FrameworkElement, EventHandler<LanguageChangedEventArgs>>();

        /// <summary>
        /// 缓存 ViewModel 类型中标记了 [Localized] 特性的属性信息，避免重复反射
        /// </summary>
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]>
            _localizedPropertyCache = new ConcurrentDictionary<Type, PropertyInfo[]>();

        #region AutoRefresh 附加属性

        /// <summary>
        /// 是否启用自动刷新附加属性
        /// </summary>
        public static readonly DependencyProperty AutoRefreshProperty =
            DependencyProperty.RegisterAttached(
                "AutoRefresh",
                typeof(bool),
                typeof(LocalizationBehavior),
                new PropertyMetadata(false, OnAutoRefreshChanged));

        /// <summary>
        /// 获取 AutoRefresh 属性值
        /// </summary>
        public static bool GetAutoRefresh(DependencyObject obj) => (bool)obj.GetValue(AutoRefreshProperty);

        /// <summary>
        /// 设置 AutoRefresh 属性值
        /// </summary>
        public static void SetAutoRefresh(DependencyObject obj, bool value) => obj.SetValue(AutoRefreshProperty, value);

        #endregion

        /// <summary>
        /// AutoRefresh 属性变更回调：绑定或解绑语言切换事件
        /// </summary>
        private static void OnAutoRefreshChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is FrameworkElement element))
                return;

            if ((bool)e.NewValue)
            {
                // 启用：监听 DataContext 变化和元素卸载事件
                element.DataContextChanged += OnDataContextChanged;
                element.Unloaded += OnElementUnloaded;
                // DataContext 可能已设置，立即尝试订阅
                TrySubscribe(element);
            }
            else
            {
                // 禁用：移除事件监听并取消订阅语言切换
                element.DataContextChanged -= OnDataContextChanged;
                element.Unloaded -= OnElementUnloaded;
                Unsubscribe(element);
            }
        }

        /// <summary>
        /// DataContext 变更回调：取消旧订阅，尝试为新 DataContext 建立订阅
        /// </summary>
        private static void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(sender is FrameworkElement element))
                return;

            // 先取消当前订阅
            Unsubscribe(element);
            // 用新的 DataContext 重新订阅
            TrySubscribe(element);
        }

        /// <summary>
        /// 元素卸载回调：取消订阅，防止内存泄漏
        /// </summary>
        private static void OnElementUnloaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element))
                return;

            element.DataContextChanged -= OnDataContextChanged;
            element.Unloaded -= OnElementUnloaded;
            Unsubscribe(element);
        }

        /// <summary>
        /// 尝试为元素的 DataContext 订阅语言切换事件
        /// </summary>
        private static void TrySubscribe(FrameworkElement element)
        {
            if (element.DataContext == null)
                return;

            // 获取本地化服务
            ILocalizationService localizationService;
            try
            {
                localizationService = ContainerLocator.Container.Resolve<ILocalizationService>();
            }
            catch
            {
                // ILocalizationService 未注册，无法订阅
                return;
            }

            if (localizationService == null)
                return;

            // 创建语言变更事件处理器
            EventHandler<LanguageChangedEventArgs> handler = (s, args) =>
            {
                RefreshLocalizedProperties(element.DataContext);
            };

            // ConditionalWeakTable 不支持重复添加，先移除再添加
            _subscriptions.Remove(element);
            _subscriptions.Add(element, handler);
            localizationService.LanguageChanged += handler;
        }

        /// <summary>
        /// 取消元素的语言切换事件订阅
        /// </summary>
        private static void Unsubscribe(FrameworkElement element)
        {
            if (!_subscriptions.TryGetValue(element, out var handler))
                return;

            // 从服务中移除事件订阅
            try
            {
                var localizationService = ContainerLocator.Container.Resolve<ILocalizationService>();
                if (localizationService != null)
                {
                    localizationService.LanguageChanged -= handler;
                }
            }
            catch
            {
                // 服务不可用时忽略
            }

            // 从弱引用表中移除
            _subscriptions.Remove(element);
        }

        /// <summary>
        /// 刷新 ViewModel 中所有标记了 [Localized] 特性的属性
        /// </summary>
        private static void RefreshLocalizedProperties(object viewModel)
        {
            if (viewModel == null)
                return;

            var viewModelType = viewModel.GetType();

            // 从缓存获取或通过反射查找标记了 [Localized] 特性的属性
            var localizedProps = _localizedPropertyCache.GetOrAdd(viewModelType, type =>
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.IsDefined(typeof(LocalizedAttribute), inherit: true))
                    .ToArray());

            if (localizedProps.Length == 0)
                return;

            // 优先通过 Prism BindableBase 的 RaisePropertyChanged 方法通知属性变更
            if (viewModel is BindableBase bindableBase)
            {
                var raisePropertyChangedMethod = viewModelType.GetMethod(
                    "RaisePropertyChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    new[] { typeof(string) },
                    null);

                if (raisePropertyChangedMethod != null)
                {
                    foreach (var prop in localizedProps)
                    {
                        raisePropertyChangedMethod.Invoke(bindableBase, new object[] { prop.Name });
                    }
                    return;
                }
            }

            // 通用方式：通过 INotifyPropertyChanged.PropertyChanged 事件直接触发通知
            if (viewModel is INotifyPropertyChanged notifyChanged)
            {
                // 尝试获取 PropertyChanged 事件的委托字段
                var propertyChangedField = viewModelType.GetField(
                    nameof(INotifyPropertyChanged.PropertyChanged),
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (propertyChangedField != null)
                {
                    var eventDelegate = propertyChangedField.GetValue(notifyChanged) as MulticastDelegate;
                    if (eventDelegate != null)
                    {
                        foreach (var prop in localizedProps)
                        {
                            var args = new PropertyChangedEventArgs(prop.Name);
                            foreach (var d in eventDelegate.GetInvocationList())
                            {
                                d.DynamicInvoke(notifyChanged, args);
                            }
                        }
                        return;
                    }
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using Core.Abstraction;
using Prism.Ioc;

namespace Core.Markup
{
    /// <summary>
    /// WPF MarkupExtension，用于替代 DynamicResource 实现语言本地化绑定。
    /// 运行时通过 Binding 绑定 Value 属性，语言切换时调用 InvalidateAll() 刷新所有实例。
    /// </summary>
    public class LangExtension : MarkupExtension, INotifyPropertyChanged
    {
        /// <summary>
        /// 所有存活实例的弱引用列表，用于语言切换时批量刷新
        /// </summary>
        private static readonly List<WeakReference> _instances = new();

        private string _key;
        private object[] _args;
        private string _value;

        /// <summary>
        /// 翻译 Key
        /// </summary>
        public string Key
        {
            get => _key;
            set
            {
                if (_key != value)
                {
                    _key = value;
                    UpdateValue();
                }
            }
        }

        /// <summary>
        /// 格式化参数（可选）
        /// </summary>
        public object[] Args
        {
            get => _args;
            set
            {
                _args = value;
                UpdateValue();
            }
        }

        /// <summary>
        /// 当前翻译值，WPF Binding 绑定此属性
        /// </summary>
        public string Value
        {
            get => _value;
            private set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged(nameof(Value));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 默认构造函数 — 支持XAML属性语法: {lang:Lang Key=xxx}
        /// </summary>
        public LangExtension()
        {
            _instances.Add(new WeakReference(this));
        }

        /// <summary>
        /// 构造函数 — 仅指定翻译 Key
        /// </summary>
        public LangExtension(string key)
        {
            _key = key;
            _instances.Add(new WeakReference(this));
        }

        /// <summary>
        /// 构造函数 — 指定翻译 Key 和1个格式化参数
        /// </summary>
        public LangExtension(string key, string arg1) : this(key)
        {
            _args = new object[] { arg1 };
        }

        /// <summary>
        /// 构造函数 — 指定翻译 Key 和2个格式化参数
        /// </summary>
        public LangExtension(string key, string arg1, string arg2) : this(key)
        {
            _args = new object[] { arg1, arg2 };
        }

        /// <summary>
        /// 提供 Binding 值。设计时直接返回翻译文本，运行时返回绑定到 Value 属性的 OneWay Binding。
        /// 注意：XAML 设计器在 DataTemplate 内部可能在 Key 赋值前调用 ProvideValue，
        /// 因此所有代码路径均需对 null Key 做防御处理，避免 XDG0006。
        /// 对于 Run.Text 等非依赖属性，无法使用 Binding，直接返回字符串值。
        /// </summary>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // 设计器在 DataTemplate 内部可能在 Key 赋值前调用 ProvideValue，直接返回空占位
            if (string.IsNullOrEmpty(_key))
                return string.Empty;

            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                string designValue = null;

                if (Application.Current != null)
                {
                    try
                    {
                        var resource = Application.Current.TryFindResource(_key);
                        if (resource != null)
                            designValue = resource.ToString();
                    }
                    catch
                    {
                    }
                }

                if (string.IsNullOrEmpty(designValue))
                {
                    try
                    {
                        var designDict = new ResourceDictionary();
                        designDict.Source = new Uri("/MainApp;component/Languages/Strings.zh-CN.xaml", UriKind.Relative);
                        // 防御 null key：ResourceDictionary.Contains(null) 会抛出 ArgumentNullException("key")
                        if (!string.IsNullOrEmpty(_key) && designDict.Contains(_key))
                            designValue = designDict[_key]?.ToString();
                    }
                    catch
                    {
                    }
                }

                if (!string.IsNullOrEmpty(designValue))
                    return FormatValue(designValue);

                return $"[{_key}]";
            }

            // 运行时：初始化 Value
            UpdateValue();

            // 检查目标属性是否为依赖属性（如 Run.Text 不是依赖属性，无法使用 Binding）
            var targetProperty = GetTargetProperty(serviceProvider);
            if (targetProperty == null)
            {
                // 目标不是依赖属性（如 Run.Text），直接返回字符串值
                // 语言切换时通过 InvalidateAll 刷新，但 Run.Text 无法自动更新
                return Value ?? $"[{_key}]";
            }

            var binding = new Binding(nameof(Value))
            {
                Source = this,
                Mode = BindingMode.OneWay
            };

            try
            {
                return binding.ProvideValue(serviceProvider);
            }
            catch (Exception)
            {
                // serviceProvider 在 DataTemplate 内部可能无法提供有效的 IProvideValueTarget，
                // 此时直接返回当前翻译值，避免设计器 XDG0006 错误
                return Value ?? $"[{_key}]";
            }
        }

        /// <summary>
        /// 从 IServiceProvider 获取目标属性信息，判断是否为依赖属性
        /// </summary>
        private object GetTargetProperty(IServiceProvider serviceProvider)
        {
            try
            {
                var provideValueTarget = serviceProvider?.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
                if (provideValueTarget?.TargetProperty is System.Reflection.PropertyInfo pi)
                {
                    // 检查属性是否由 DependencyProperty 支撑（如 TextBlock.Text 有 DependencyProperty）
                    // Run.Text 没有 DependencyProperty，无法使用 Binding
                    var declaringType = pi.DeclaringType;
                    if (declaringType != null)
                    {
                        var field = declaringType.GetField(pi.Name + "Property",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        return field; // 非 null 表示是依赖属性
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// 语言切换时调用，遍历所有存活实例并刷新 Value
        /// </summary>
        public static void InvalidateAll()
        {
            // 清理已失效的弱引用，避免列表无限增长
            _instances.RemoveAll(wr => !wr.IsAlive);

            foreach (var wr in _instances)
            {
                var instance = wr.Target as LangExtension;
                instance?.UpdateValue();
            }
        }

        /// <summary>
        /// 获取翻译值：优先从 Application.Resources 查找，再通过 ILocalizationService 获取
        /// </summary>
        private string GetValue()
        {
            string rawValue = null;

            // 优先从 XAML 资源字典获取
            if (Application.Current != null)
            {
                var resource = Application.Current.TryFindResource(_key);
                if (resource != null)
                {
                    rawValue = resource.ToString();
                }
            }

            // 资源字典未找到，尝试通过 ILocalizationService 获取
            if (rawValue == null)
            {
                try
                {
                    var localizationService = ContainerLocator.Container
                        .Resolve<ILocalizationService>();
                    if (localizationService != null)
                    {
                        rawValue = localizationService.GetResource(_key);
                    }
                }
                catch
                {
                    // ILocalizationService 未注册或解析失败，忽略
                }
            }

            // Key 不存在时返回 [Key] 格式，不抛异常
            if (string.IsNullOrEmpty(rawValue))
            {
                return $"[{_key}]";
            }

            return FormatValue(rawValue);
        }

        /// <summary>
        /// 使用 Args 对原始翻译值进行格式化
        /// </summary>
        private string FormatValue(string rawValue)
        {
            if (_args != null && _args.Length > 0)
            {
                try
                {
                    return string.Format(rawValue, _args);
                }
                catch
                {
                    // 格式化失败时返回原始值
                    return rawValue;
                }
            }
            return rawValue;
        }

        /// <summary>
        /// 更新 Value 属性
        /// </summary>
        private void UpdateValue()
        {
            if (_key != null)
            {
                Value = GetValue();
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

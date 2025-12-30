using Core.Models;
using Prism.Mvvm;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Core.Abstraction
{
    [AddINotifyPropertyChangedInterface]
    public class ParameterGroup
    {
        public string Category { get; }
        public ObservableCollection<ParameterItem> Parameters { get; }

        [DoNotNotify]
        public bool IsVisible { get; set; } = true;

        public ParameterGroup(string category, IEnumerable<ParameterItem> parameters)
        {
            Category = category;
            Parameters = new ObservableCollection<ParameterItem>(parameters);
        }
    }

    #region Parameter Types
    [AddINotifyPropertyChangedInterface]
    public abstract class ParameterItem : BindableBase
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public bool IsRequired { get; set; }
        public abstract object Value { get; set; }
        public abstract object DefaultValue { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsEditable { get; set; } = true;
        /// <summary>格式字符串(如 F0, F1, F2)</summary>
        public string FormatString { get; set; } = "F2";
        /// <summary>原始属性类型</summary>
        public Type OriginalType { get; set; }
        public abstract void ResetToDefault();

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        // 支持嵌套对象
        public virtual bool IsNestedObject { get; set; }
        public ObservableCollection<ParameterGroup> ChildrenGroups { get; } = new ObservableCollection<ParameterGroup>();
    }
    // 嵌套对象表示类
    public class NestedObjectParameterItem : ParameterItem
    {
        public ObservableCollection<ParameterGroup> ChildrenGroups { get; } = new ObservableCollection<ParameterGroup>();

        public override bool IsNestedObject => true;

        // 实际值字段
        private object _actualValue;

        // 实现 Value 属性
        public override object Value
        {
            get => _actualValue;
            set => SetProperty(ref _actualValue, value);
        }
        public override object DefaultValue
        {
            get => null;
            set { } // 什么都不做
        }
        public override void ResetToDefault()
        {
            // 什么都不做
        }
    }
    public class StringParameterItem : ParameterItem
    {
        private string _value;
        public override object Value
        {
            get => _value;
            set
            {
                _value = value as string;
                OnPropertyChanged(nameof(Value));
            }
        }

        public override object DefaultValue { get; set; }

        public override void ResetToDefault()
        {
            if (DefaultValue is string defaultValue)
            {
                Value = defaultValue;
            }
        }
    }

    public class BooleanParameterItem : ParameterItem
    {
        private bool _value;
        public override object Value
        {
            get => _value;
            set
            {
                _value = (bool)value;
                OnPropertyChanged(nameof(Value));
            }
        }

        public override object DefaultValue { get; set; }

        public override void ResetToDefault()
        {
            if (DefaultValue is bool defaultValue)
            {
                Value = defaultValue;
            }
        }
    }

    public class NumberParameterItem : ParameterItem
    {
        private double _actualValue;
        private int _decimalPlaces = 2; // 默认2位小数
        private double _value;

        /// <summary>实际数值（统一为double类型）</summary>
        public double ActualValue
        {
            get => _actualValue;
            set
            {
                // 限制范围并更新值
                var newValue = Math.Clamp(value, MinValue, MaxValue);
                newValue = Math.Round(newValue, DecimalPlaces);
                if (SetProperty(ref _actualValue, newValue))
                {
                    // 更新显示文本
                    UpdateDisplayString();
                    // 同步Value属性
                    RaisePropertyChanged(nameof(Value));
                    // 通知格式化值更新
                    RaisePropertyChanged(nameof(FormattedValue));
                }
            }
        }
        /// <summary>小数位数</summary>
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set
            {
                if (SetProperty(ref _decimalPlaces, value))
                {
                    // 步长变化时通知更新
                    RaisePropertyChanged(nameof(SmallChange));

                    // 重新舍入当前值
                    ActualValue = Math.Round(_actualValue, DecimalPlaces);
                }
            }
        }
        /// <summary>格式化后的显示值（用于绑定）</summary>
        public string FormattedValue
        {
            get
            {
                if (DecimalPlaces == 0)
                {
                    return ((int)Math.Round(_actualValue)).ToString();
                }
                else
                {
                    return _actualValue.ToString($"F{DecimalPlaces}");
                }
            }
        }
        /// <summary>格式字符串(如 F0, F1, F2)</summary>
        public string FormatString { get; set; } = "F2";
        /// <summary>显示字符串（带格式化的值）</summary>
        public string DisplayString { get; private set; } = "";
        public override object Value
        {
            get => _actualValue;
            set
            {
                if (value is double d)
                {
                    ActualValue = d;
                }
                else
                {
                    // 尝试转换为double
                    double parsedValue = value switch
                    {
                        int i => (double)i,
                        float f => (double)f,
                        _ => Convert.ToDouble(value)
                    };
                    ActualValue = parsedValue;
                }
            }
        }

        private void UpdateDisplayString()
        {
            // 特殊处理整数和小数
            if (DecimalPlaces == 0 || Math.Abs(_actualValue - Math.Round(_actualValue)) < 0.0001)
            {
                // 对于整数值，直接显示整数
                DisplayString = $"当前值: {(int)Math.Round(_actualValue)}";
            }
            else
            {
                // 对于小数值，使用指定格式显示
                DisplayString = $"当前值: {_actualValue.ToString(FormatString)}";
            }
            RaisePropertyChanged(nameof(DisplayString));
        }
        public double SmallChange => DecimalPlaces switch
        {
            0 => 1,
            1 => 0.1,
            2 => 0.01,
            _ => Math.Pow(0.1, DecimalPlaces)
        };
        public override object DefaultValue { get; set; }
        public double MinValue { get; set; } = 0;
        public double MaxValue { get; set; } = 100;

        public override void ResetToDefault()
        {
            if (DefaultValue is double d)
            {
                ActualValue = d;
            }
            else if (DefaultValue != null)
            {
                ActualValue = Convert.ToDouble(DefaultValue);
            }
        }
    }

    // 修改为没有泛型的枚举类
    public class EnumParameterItem : ParameterItem
    {
        private object _value;
        public override object Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged(nameof(Value));
            }
        }

        public override object DefaultValue { get; set; }

        public List<object> EnumValues { get; set; } = new List<object>();

        public override void ResetToDefault()
        {
            Value = DefaultValue;
        }
        public Type EnumType { get; set; }

    }

    public class ColorParameterItem : ParameterItem
    {
        private System.Windows.Media.Color _value;
        public override object Value
        {
            get => _value;
            set
            {
                if (value is System.Windows.Media.Color color)
                {
                    _value = color;
                }
                else if (value is System.Windows.Media.SolidColorBrush brush)
                {
                    _value = brush.Color;
                }
                OnPropertyChanged(nameof(Value));
            }
        }

        public override object DefaultValue { get; set; }

        public override void ResetToDefault()
        {
            if (DefaultValue is System.Windows.Media.Color colorDefault)
            {
                Value = colorDefault;
            }
            else if (DefaultValue is System.Windows.Media.SolidColorBrush brushDefault)
            {
                Value = brushDefault.Color;
            }
        }
    }
    public class PointFParameterItem : ParameterItem
    {
        private PointF _value;
        public override object Value
        {
            get => _value;
            set
            {
                _value = (PointF)value;
                OnPropertyChanged(nameof(Value));
                OnPropertyChanged(nameof(X));
                OnPropertyChanged(nameof(Y));
                OnPropertyChanged(nameof(Z));
            }
        }

        public double X
        {
            get => _value.X;
            set
            {
                _value.X = (float)value;
                OnPropertyChanged(nameof(X));
                OnPropertyChanged(nameof(Value));
            }
        }

        public double Y
        {
            get => _value.Y;
            set
            {
                _value.Y = (float)value;
                OnPropertyChanged(nameof(Y));
                OnPropertyChanged(nameof(Value));
            }
        }

        public double Z
        {
            get => _value.Z;
            set
            {
                _value.Z = (float)value;
                OnPropertyChanged(nameof(Z));
                OnPropertyChanged(nameof(Value));
            }
        }

        public override object DefaultValue { get; set; }

        public override void ResetToDefault()
        {
            if (DefaultValue is PointF defaultValue)
            {
                Value = defaultValue;
            }
        }
    }

    #endregion
}

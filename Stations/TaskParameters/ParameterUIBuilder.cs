using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows;

namespace Stations.TaskParameters
{
    public class ParameterUIBuilder
    {
        public UIElement BuildEditorForProperty(PropertyInfo property, object dataContext)
        {
            // 根据属性类型和特性创建合适的UI控件
            AttributeCollection attributes = TypeDescriptor.GetProperties(dataContext)[property.Name].Attributes;

            if (property.PropertyType == typeof(double))
            {
                var sliderAttr = attributes.OfType<RangeAttribute>().FirstOrDefault();

                var slider = new Slider
                {
                    Minimum = (double)(sliderAttr?.Minimum ?? 0),
                    Maximum = (double)(sliderAttr?.Maximum ?? 100),
                    //TickFrequency = sliderAttr?.Step ?? 1,
                    Value = (double)property.GetValue(dataContext)
                };

                slider.SetBinding(Slider.ValueProperty, new Binding(property.Name)
                {
                    Source = dataContext,
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                });

                var panel = new StackPanel { Orientation = Orientation.Vertical };
                panel.Children.Add(new TextBlock { Text = attributes.OfType<DescriptionAttribute>().FirstOrDefault()?.Description ?? property.Name });
                panel.Children.Add(slider);
                panel.Children.Add(new TextBlock { Text = $"{slider.Value:F1}" });

                return panel;
            }

            // 对其他类型类似处理...

            return new TextBox { Text = property.GetValue(dataContext)?.ToString() };
        }
    }

}

using Core.Abstraction;
using System.Windows;
using System.Windows.Controls;

namespace Framework.ViewModels
{
    public class ParameterTemplateSelector : DataTemplateSelector
    {
        public DataTemplate StringTemplate { get; set; }
        public DataTemplate BooleanTemplate { get; set; }
        public DataTemplate NumberTemplate { get; set; }
        public DataTemplate EnumTemplate { get; set; }
        public DataTemplate ColorTemplate { get; set; }
        public DataTemplate NestedObjectTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is NestedObjectParameterItem)
                return NestedObjectTemplate;

            if (item is StringParameterItem)
                return StringTemplate;

            if (item is BooleanParameterItem)
                return BooleanTemplate;

            if (item is NumberParameterItem)
                return NumberTemplate;

            if (item is EnumParameterItem)
                return EnumTemplate;

            if (item is ColorParameterItem)
                return ColorTemplate;

            return base.SelectTemplate(item, container);
        }
    }

}

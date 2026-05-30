using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Media;
using Core.Models;

namespace Core.Models
{
    /// <summary> 标注元素类型模板选择器：根据子类类型返回对应 DataTemplate </summary>
    public class AnnotationTemplateSelector : DataTemplateSelector
    {
        // 文字标注模板
        private static readonly DataTemplate TextTemplate = CreateTextTemplate();
        
        // 线条/箭头标注模板
        private static readonly DataTemplate LineTemplate = CreateLineTemplate();
        
        // 矩形框标注模板
        private static readonly DataTemplate RectTemplate = CreateRectTemplate();

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            return item switch
            {
                TextAnnotation => TextTemplate,
                LineAnnotation => LineTemplate,
                RectAnnotation => RectTemplate,
                _ => base.SelectTemplate(item, container)
            };
        }

        private static DataTemplate CreateTextTemplate()
        {
            var factory = new FrameworkElementFactory(typeof(TextBlock));
            factory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Text"));
            factory.SetBinding(Canvas.LeftProperty, new System.Windows.Data.Binding("X"));
            factory.SetBinding(Canvas.TopProperty, new System.Windows.Data.Binding("Y"));
            factory.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Color") { ConverterParameter = "#000000" });
            factory.SetBinding(TextBlock.FontSizeProperty, new System.Windows.Data.Binding("FontSize"));
            factory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            return new DataTemplate { VisualTree = factory };
        }

        private static DataTemplate CreateLineTemplate()
        {
            var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
            
            // 线条
            var lineFactory = new FrameworkElementFactory(typeof(Line));
            lineFactory.SetBinding(Line.X1Property, new System.Windows.Data.Binding("X"));
            lineFactory.SetBinding(Line.Y1Property, new System.Windows.Data.Binding("Y"));
            lineFactory.SetBinding(Line.X2Property, new System.Windows.Data.Binding("X2"));
            lineFactory.SetBinding(Line.Y2Property, new System.Windows.Data.Binding("Y2"));
            lineFactory.SetBinding(Line.StrokeProperty, new System.Windows.Data.Binding("Color"));
            lineFactory.SetValue(Line.StrokeThicknessProperty, 1.5);
            stackFactory.AppendChild(lineFactory);

            // 标签文字
            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetBinding(Canvas.LeftProperty, new System.Windows.Data.Binding("X2"));
            textFactory.SetBinding(Canvas.TopProperty, new System.Windows.Data.Binding("Y2"));
            textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Text"));
            textFactory.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Color"));
            textFactory.SetValue(TextBlock.FontSizeProperty, 10.0);
            textFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(3, -5, 0, 0));
            stackFactory.AppendChild(textFactory);

            return new DataTemplate { VisualTree = stackFactory };
        }

        private static DataTemplate CreateRectTemplate()
        {
            var rectFactory = new FrameworkElementFactory(typeof(Rectangle));
            rectFactory.SetBinding(Canvas.LeftProperty, new System.Windows.Data.Binding("X"));
            rectFactory.SetBinding(Canvas.TopProperty, new System.Windows.Data.Binding("Y"));
            rectFactory.SetBinding(FrameworkElement.WidthProperty, new System.Windows.Data.Binding("Width"));
            rectFactory.SetBinding(FrameworkElement.HeightProperty, new System.Windows.Data.Binding("Height"));
            rectFactory.SetBinding(Shape.FillProperty, new System.Windows.Data.Binding("FillColor"));
            rectFactory.SetBinding(Shape.StrokeProperty, new System.Windows.Data.Binding("Color"));
            rectFactory.SetValue(Shape.StrokeThicknessProperty, 1.0);
            return new DataTemplate { VisualTree = rectFactory };
        }
    }
}

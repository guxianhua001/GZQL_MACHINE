using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows;

namespace Modules.LogViewer.Converters
{
    /// <summary>
    /// 将文本中的换行符转换为LineBreak元素的转换器
    /// </summary>
    public class NewLineToLineBreakConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;

            string text = value.ToString();
            if (string.IsNullOrEmpty(text)) return text;

            // 将文本分割成行
            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // 创建一个TextBlock来包含所有行
            TextBlock textBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New, monospace"),
                FontSize = 12
            };

            // 添加每一行文本
            for (int i = 0; i < lines.Length; i++)
            {
                textBlock.Inlines.Add(new Run(lines[i]));
                if (i < lines.Length - 1)
                {
                    textBlock.Inlines.Add(new LineBreak());
                }
            }

            return textBlock;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

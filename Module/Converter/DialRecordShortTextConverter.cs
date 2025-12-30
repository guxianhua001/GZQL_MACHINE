using Interfaces;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Framework.Converters
{
    public class DialRecordShortTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DialRecord record)
            {
                // 更健壮的方向符号映射
                string directionSymbol = GetDirectionSymbol(record.Direction);

                // 使用操作方向作为备用显示
                string displayDirection = !string.IsNullOrWhiteSpace(record.Direction)
                    ? directionSymbol
                    : GetDirectionSymbol(record.OperationDirection);

                // 格式: [方向 针号 拨针力]
                return $"{displayDirection} {record.NeedleId} {record.DialForce:F2}N";
            }
            return string.Empty;
        }

        private static string GetDirectionSymbol(string direction)
        {
            if (string.IsNullOrWhiteSpace(direction))
                return "⬤"; // 默认符号表示未知方向

            return direction.Trim().ToUpper() switch
            {
                "POSITIVE" or "RIGHT" or "+" => "→",  // 正向箭头
                "NEGATIVE" or "LEFT" or "-" => "←",   // 负向箭头
                "UP" or "TOP" or "U" => "↑",
                "DOWN" or "BOTTOM" or "D" => "↓",
                _ => "⬤" // 圆形符号表示未知方向
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

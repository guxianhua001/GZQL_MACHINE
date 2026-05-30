using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace MotionControl.Converters
{
    /// <summary>
    /// 轴方向到图标类型的转换器
    /// 支持 hwcfg.xml 中的描述性方向名（如 Left_Right、Down_Up、Rotate_antiClock）
    /// 以及简单方向名（X、Y、Z、R）
    /// </summary>
    public class AxisDirectionToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string param = parameter as string ?? "Positive";
            bool isPositive = param.Equals("Positive", StringComparison.OrdinalIgnoreCase);

            if (value is not string direction)
                return isPositive ? PackIconKind.ArrowUp : PackIconKind.ArrowDown;

            string dir = direction.ToUpperInvariant().Trim();

            // hwcfg 描述性方向名
            return dir switch
            {
                // 水平方向（X 轴）
                "LEFT_RIGHT" => isPositive ? PackIconKind.ArrowRight : PackIconKind.ArrowLeft,
                "RIGHT_LEFT" => isPositive ? PackIconKind.ArrowLeft : PackIconKind.ArrowRight,

                // 前后方向（Y 轴）
                "FRONT_BACK" => isPositive ? PackIconKind.ArrowDown : PackIconKind.ArrowUp,
                "BACK_FRONT" => isPositive ? PackIconKind.ArrowUp : PackIconKind.ArrowDown,

                // 上下方向（Z 轴）
                "UP_DOWN" => isPositive ? PackIconKind.ArrowDown : PackIconKind.ArrowUp,
                "DOWN_UP" => isPositive ? PackIconKind.ArrowUp : PackIconKind.ArrowDown,

                // 旋转方向
                "ROTATE" => isPositive ? PackIconKind.RotateRight : PackIconKind.RotateLeft,
                "ROTATE_ANTICLOCK" or "ROTATE_ANTICLOCKWISE" => isPositive ? PackIconKind.RotateLeft : PackIconKind.RotateRight,

                // 简单方向名
                "X" => isPositive ? PackIconKind.ArrowRight : PackIconKind.ArrowLeft,
                "-X" or "NX" => isPositive ? PackIconKind.ArrowLeft : PackIconKind.ArrowRight,
                "Y" => isPositive ? PackIconKind.ArrowUp : PackIconKind.ArrowDown,
                "-Y" or "NY" => isPositive ? PackIconKind.ArrowDown : PackIconKind.ArrowUp,
                "Z" => isPositive ? PackIconKind.ArrowUp : PackIconKind.ArrowDown,
                "-Z" or "NZ" => isPositive ? PackIconKind.ArrowDown : PackIconKind.ArrowUp,
                "R" or "RY" or "A" => isPositive ? PackIconKind.RotateRight : PackIconKind.RotateLeft,
                "-R" or "-RY" or "-A" => isPositive ? PackIconKind.RotateLeft : PackIconKind.RotateRight,
                "RX" or "B" => isPositive ? PackIconKind.RotateRight : PackIconKind.RotateLeft,
                "RZ" or "C" => isPositive ? PackIconKind.RotateRight : PackIconKind.RotateLeft,

                _ => GuessIconFromDirection(direction, isPositive)
            };
        }

        /// <summary>
        /// 从方向字符串中猜测合适的图标（处理自定义命名如 Dz、Dy、Dx）
        /// </summary>
        private static PackIconKind GuessIconFromDirection(string direction, bool isPositive)
        {
            string upper = direction.ToUpperInvariant();

            if (upper.Contains("LEFT") && upper.Contains("RIGHT"))
                return isPositive ? PackIconKind.ArrowRight : PackIconKind.ArrowLeft;
            if (upper.Contains("RIGHT") && upper.Contains("LEFT"))
                return isPositive ? PackIconKind.ArrowLeft : PackIconKind.ArrowRight;
            if (upper.Contains("UP") && upper.Contains("DOWN"))
                return isPositive ? PackIconKind.ArrowDown : PackIconKind.ArrowUp;
            if (upper.Contains("DOWN") && upper.Contains("UP"))
                return isPositive ? PackIconKind.ArrowUp : PackIconKind.ArrowDown;
            if (upper.Contains("FRONT") && upper.Contains("BACK"))
                return isPositive ? PackIconKind.ArrowDown : PackIconKind.ArrowUp;
            if (upper.Contains("BACK") && upper.Contains("FRONT"))
                return isPositive ? PackIconKind.ArrowUp : PackIconKind.ArrowDown;
            if (upper.Contains("ROTATE") || upper.Contains("ANTICLOCK"))
                return isPositive ? PackIconKind.RotateRight : PackIconKind.RotateLeft;
            if (upper.Contains("X"))
                return isPositive ? PackIconKind.ArrowRight : PackIconKind.ArrowLeft;
            if (upper.Contains('R'))
                return isPositive ? PackIconKind.RotateRight : PackIconKind.RotateLeft;
            if (upper.Contains("Z") || upper.Contains("DZ"))
                return isPositive ? PackIconKind.ArrowUp : PackIconKind.ArrowDown;

            return isPositive ? PackIconKind.ArrowUp : PackIconKind.ArrowDown;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

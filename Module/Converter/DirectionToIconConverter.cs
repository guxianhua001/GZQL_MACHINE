using Core.Abstraction;
using MaterialDesignThemes.Wpf;
using SmarterMotion;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Framework.Converters
{
    public class DirectionToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not XAxisDirection direction || parameter is not string buttonType)
                return PackIconKind.ArrowLeftRight; // 默认图标

            switch (direction)
            {
                case XAxisDirection.Front_Back:
                    return buttonType == "Positive" ? PackIconKind.ArrowUp : PackIconKind.ArrowDown;

                case XAxisDirection.Back_Front:
                    return buttonType == "Positive" ? PackIconKind.ArrowDown : PackIconKind.ArrowUp;

                case XAxisDirection.Left_Right:
                    return buttonType == "Positive" ? PackIconKind.ArrowRight : PackIconKind.ArrowLeft;

                case XAxisDirection.Right_Left:
                    return buttonType == "Positive" ? PackIconKind.ArrowLeft : PackIconKind.ArrowRight;

                case XAxisDirection.Up_Down:
                    return buttonType == "Positive" ? PackIconKind.ArrowUp : PackIconKind.ArrowDown;

                case XAxisDirection.Down_Up:
                    return buttonType == "Positive" ? PackIconKind.ArrowDown : PackIconKind.ArrowUp;

                case XAxisDirection.Rotate:
                    return buttonType == "Positive" ? PackIconKind.RotateRight : PackIconKind.RotateLeft;

                case XAxisDirection.Rotate_antiClock:
                    return buttonType == "Positive" ? PackIconKind.RotateLeft : PackIconKind.RotateRight;

                default:
                    return PackIconKind.Minus; // 默认图标
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

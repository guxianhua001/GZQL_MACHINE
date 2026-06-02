using System;
using System.Globalization;
using System.Windows.Data;

namespace Module.Converters
{
    /// <summary>
    /// 计算安全区域矩形的宽度（基于危险区边界配置）
    /// 从ViewModel获取配置数据，返回Canvas像素宽度
    /// </summary>
    public class SafeZoneWidthConverter : IValueConverter
    {
        private const double CanvasWidth = 400;
        private const double CoordinateRange = 200;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ViewModels.SafetyZoneConfigViewModel vm)
                return CanvasWidth;

            double range = Math.Abs(vm.DangerZoneXMax - vm.DangerZoneXMin);
            if (range <= 0)
                return CanvasWidth;

            return (range / (CoordinateRange * 2)) * CanvasWidth;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

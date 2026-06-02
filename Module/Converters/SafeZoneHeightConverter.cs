using System;
using System.Globalization;
using System.Windows.Data;

namespace Module.Converters
{
    /// <summary>
    /// 计算安全区域矩形的高度（基于危险区边界配置）
    /// 从ViewModel获取配置数据，返回Canvas像素高度
    /// </summary>
    public class SafeZoneHeightConverter : IValueConverter
    {
        private const double CanvasHeight = 300;
        private const double CoordinateRange = 200;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ViewModels.SafetyZoneConfigViewModel vm)
                return CanvasHeight;

            double range = Math.Abs(vm.DangerZoneYMax - vm.DangerZoneYMin);
            if (range <= 0)
                return CanvasHeight;

            return (range / (CoordinateRange * 2)) * CanvasHeight;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

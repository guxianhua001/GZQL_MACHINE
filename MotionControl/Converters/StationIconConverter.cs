using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace MotionControl.Converters
{
    /// <summary>
    /// 工站名称到图标的转换器
    /// 根据工站名称关键字返回对应的 PackIconKind
    /// 同时支持英文和中文关键字匹配，确保多语言兼容
    /// </summary>
    public class StationIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string stationName)
                return PackIconKind.Factory;

            var upper = stationName.ToUpperInvariant();

            return upper switch
            {
                var s when s.Contains("LOAD") || s.Contains("上料") => PackIconKind.TrayArrowDown,
                var s when s.Contains("UNLOAD") || s.Contains("下料") => PackIconKind.TruckRemove,
                var s when s.Contains("DISPENSE") || s.Contains("点胶") || s.Contains("涂覆") => PackIconKind.Pencil,
                var s when s.Contains("ASSEMBLY") || s.Contains("组装") || s.Contains("装配") => PackIconKind.Puzzle,
                var s when s.Contains("SCAN") || s.Contains("检测") || s.Contains("视觉") => PackIconKind.QrcodeScan,
                var s when s.Contains("WELD") || s.Contains("焊接") => PackIconKind.SolderingIron,
                var s when s.Contains("TEST") || s.Contains("测试") => PackIconKind.TestTube,
                _ => PackIconKind.Cog
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

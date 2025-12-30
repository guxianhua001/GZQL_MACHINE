using System.Windows.Media;

namespace Framework
{
    public static class ColorHelper
    {
        // Material Design 默认颜色资源
        public static SolidColorBrush PrimaryBrush = new SolidColorBrush(Color.FromRgb(103, 58, 183));
        public static SolidColorBrush PrimaryHueLightBrush = new SolidColorBrush(Color.FromRgb(179, 136, 255));
        public static SolidColorBrush PrimaryHueMidBrush = new SolidColorBrush(Color.FromRgb(103, 58, 183));
        public static SolidColorBrush PrimaryHueDarkBrush = new SolidColorBrush(Color.FromRgb(53, 18, 131));

        // 其他辅助方法
        public static Color GetMaterialDesignPrimaryColor()
        {
            return Color.FromRgb(103, 58, 183);
        }
    }
}


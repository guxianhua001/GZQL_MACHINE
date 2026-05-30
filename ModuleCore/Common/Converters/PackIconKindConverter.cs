﻿﻿﻿using MaterialDesignThemes.Wpf;
using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace ModuleCore.Common.Converters
{
    /// <summary>
    /// 图标名转PackIconKind枚举转换器
    /// 通过字符串精确匹配将NavigateItem.IconKind转换为materialDesign:PackIcon的Kind属性
    /// 注意：MaterialDesignThemes 5.x 捆绑的字体文件版本可能滞后于PackIconKind枚举，
    /// 导致部分枚举值存在但字体中无对应字形数据，此时会显示默认图标(Abc)
    /// </summary>
    internal class PackIconKindConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null)
            {
                var iconName = value.ToString();
                var kind = Enum.GetValues<PackIconKind>().Where(k => k.ToString() == iconName);

                if (kind.Any())
                    return kind.FirstOrDefault();

                // 枚举中未找到该图标名，记录日志便于排查
                System.Diagnostics.Debug.WriteLine($"[PackIconKindConverter] 未找到图标: '{iconName}'，将使用默认图标");
            }
            return PackIconKind.Abc;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

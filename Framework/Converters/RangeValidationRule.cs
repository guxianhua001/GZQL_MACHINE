
using System.Globalization;
using System.Windows.Controls;

namespace Framework.ViewModels
{
    public class RangeValidationRule : ValidationRule
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public string ErrorMessage { get; set; }
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            // 优化后的完整验证逻辑
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return new ValidationResult(false, "输入不能为空");
            if (!double.TryParse(value.ToString(), NumberStyles.Any, cultureInfo, out double num))
                return new ValidationResult(false, "必须输入数字");
            if (num < Min || num > Max)
                return new ValidationResult(false, ErrorMessage);
            return ValidationResult.ValidResult;
        }
    }
}

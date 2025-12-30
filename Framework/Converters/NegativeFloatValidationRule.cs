
using System.Globalization;
using System.Windows.Controls;

namespace Framework.ViewModels
{
    public class NegativeFloatValidationRule : ValidationRule
    {
        public float Min { get; set; } = -1000f;  // 负值最小值
        public float Max { get; set; } = -0.0f;   // 负值最大值 (必须为负)
        public string ErrorMessage { get; set; } = "值必须是有效的负数";
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value is string stringValue)
            {
                if (float.TryParse(stringValue, NumberStyles.Float, cultureInfo, out float floatValue))
                {
                    // 验证为负值且在有效范围内
                    if (floatValue <= 0 && floatValue >= Min && floatValue <= Max)
                    {
                        return ValidationResult.ValidResult;
                    }

                    // 错误提示：要求负值，并显示有效范围
                    return new ValidationResult(false,
                        $"{ErrorMessage} (范围: {Min} 到 {Max})");
                }
            }
            return new ValidationResult(false, "请输入有效的数值");
        }
    }
}


using System.Globalization;
using System.Windows.Controls;

namespace Framework.ViewModels
{
    public class PositiveFloatValidationRule : ValidationRule
    {
        public float Min { get; set; } = 0.01f;
        public float Max { get; set; } = 1000f;
        public string ErrorMessage { get; set; } = "值超出范围";

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            if (value is string stringValue)
            {
                if (float.TryParse(stringValue, out float floatValue))
                {
                    if (floatValue >= Min && floatValue <= Max)
                    {
                        return ValidationResult.ValidResult;
                    }
                    return new ValidationResult(false, $"{ErrorMessage} ({Min} - {Max}mm)");
                }
            }
            return new ValidationResult(false, "请输入有效的数值");
        }
    }

}

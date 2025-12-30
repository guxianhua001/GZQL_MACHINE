using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Views
{
    // 枚举描述扩展
    public static class EnumExtensions
    {
        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                            .OfType<DescriptionAttribute>()
                            .FirstOrDefault();
            return attribute?.Description ?? value.ToString();
        }
    }
    // 生成规则选项列表辅助类
    public static class GenerationRule
    {
        public static List<GenerationRuleItem> Values => new List<GenerationRuleItem>
        {
            new GenerationRuleItem(BaseMapViewModel<object>.GenerationRuleType.RelativeOffset),
            new GenerationRuleItem(BaseMapViewModel<object>.GenerationRuleType.AbsoluteCoordinate)
        };

        public class GenerationRuleItem
        {
            public BaseMapViewModel<object>.GenerationRuleType Rule { get; }
            public string Description { get; }

            public GenerationRuleItem(BaseMapViewModel<object>.GenerationRuleType rule)
            {
                Rule = rule;
                Description = rule.GetDescription();
            }
        }
    }

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    public class StringHelper
    {
        /// <summary>
        /// 解析多层嵌套数组字符串(提取第一个子数组)
        /// </summary>
        public static List<double[]> ParseNestedArray(string input)
        {
            var result = new List<double[]>();

            // 去除所有空格和首尾方括号
            string cleanInput = input.Replace(" ", "").Trim('[', ']');

            // 分割子数组
            var subArrays = cleanInput.Split(new[] { "],[" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var sub in subArrays)
            {
                // 分割单个数组元素
                var elements = sub.Split(',')
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s =>
                    {
                        if (double.TryParse(s, out double num))
                            return num;
                        throw new FormatException($"无效的数值格式: {s}");
                    })
                    .ToArray();

                result.Add(elements);
            }
            return result;
        }
        /// <summary>
        /// 计算相邻间距
        /// </summary>
        public static double[] CalculateDistances(double[] numbers)
        {
            var distances = new List<double>();
            for (int i = 1; i < numbers.Length; i++)
            {
                distances.Add(numbers[i] - numbers[i - 1]);
            }
            return distances.ToArray();
        }

    }
}

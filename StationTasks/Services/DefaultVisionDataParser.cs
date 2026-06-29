using Core.Abstraction;
using Core.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace StationTasks.Services
{
    /// <summary>
    /// 默认视觉数据解析器：支持三种常见数据格式
    /// 1. 逗号分隔 key=value: "offsetX=1.5,offsetY=-0.3,offsetU=0.1"
    /// 2. 空格分隔 key value: "offsetX 1.5 offsetY -0.3 offsetU 0.1"
    /// 3. 纯逗号分隔值: "1.5,-0.3,0.1" → 映射为 X, Y, U
    /// </summary>
    public class DefaultVisionDataParser : IVisionDataParser
    {
        private static readonly string[] DefaultKeys = { "X", "Y", "U", "Distance" };
        private readonly ILoggerService _logger;
        /// <summary> 本地化服务，用于日志多语言支持 </summary>
        private readonly ILocalizationService _localization;

        public DefaultVisionDataParser(ILoggerService logger, ILocalizationService localization)
        {
            _logger = logger;
            _localization = localization;
        }

        /// <summary>
        /// 按优先级依次尝试三种格式解析原始数据
        /// 支持带有前缀元数据的数据格式，如 "Camera=SideCamera;VISION_RESULT:SUCCESS:offsetX=1.5,offsetY=..."
        /// </summary>
        public Dictionary<string, double> Parse(string rawData)
        {
            var result = new Dictionary<string, double>();

            if (string.IsNullOrWhiteSpace(rawData))
            {
                _logger.Warn(_localization.GetResourceOrDefault("DefVis_Log_DataEmpty", "视觉数据为空，无法解析"));
                return result;
            }

            var trimmed = rawData.Trim();

            // 预处理：剥离前缀元数据，提取数值数据部分
            var dataForParsing = TryStripDataPrefix(trimmed) ?? trimmed;

            // 优先尝试 key=value 格式
            if (TryParseKeyValuePairs(dataForParsing, out var kvResult))
                return kvResult;

            // 其次尝试空格分隔的 key-value 对
            if (TryParseSpaceSeparated(dataForParsing, out var spaceResult))
                return spaceResult;

            // 最后尝试纯逗号分隔值，使用默认键名
            if (TryParsePlainValues(dataForParsing, out var plainResult))
                return plainResult;

            _logger.Warn(string.Format(_localization.GetResourceOrDefault("DefVis_Log_UnrecognizedFormat", "无法识别的视觉数据格式: {0}"), trimmed));
            return result;
        }

        /// <summary>
        /// 尝试剥离数据前缀元数据，提取包含数值的 key=value 数据部分
        /// 处理格式如 "Camera=SideCamera;VISION_RESULT:SUCCESS:offsetX=1.5,offsetY=-0.3"
        /// 前缀与数据通过 ';' 或 ':' 分隔，数据部分的 value 均为可解析的数值
        /// </summary>
        /// <param name="data">原始数据</param>
        /// <returns>剥离前缀后的数据部分，若无前缀返回null</returns>
        private string TryStripDataPrefix(string data)
        {
            var parts = data.Split(',');
            if (parts.Length < 2) return null;

            // 检查第一个逗号分隔段是否包含非数值前缀（如 "Camera=SideCamera;RESULT:SUCCESS:key=value"）
            var firstKv = parts[0].Split(new[] { '=' }, 2);
            if (firstKv.Length != 2) return null;

            // 如果第一段值部分可以直接解析为double，说明没有前缀
            if (double.TryParse(firstKv[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                return null;

            // 第一段值不可解析，查找数据起始位置
            for (int i = 0; i < parts.Length; i++)
            {
                var kv = parts[i].Split(new[] { '=' }, 2);
                if (kv.Length != 2) continue;

                var valueStr = kv[1].Trim();
                if (double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                {
                    // 找到第一个值为数值的段，检查前一个段是否有混合前缀
                    if (i > 0)
                    {
                        var prevKv = parts[i - 1].Split(new[] { '=' }, 2);
                        if (prevKv.Length == 2)
                        {
                            var prevValue = prevKv[1].Trim();
                            if (!double.TryParse(prevValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                            {
                                // 前一段值非数值，说明存在 ":" 或 ";" 分隔的混合前缀
                                // 从非数值部分的最后一个分隔符后提取数据
                                int lastSepIdx = prevValue.LastIndexOfAny(new[] { ':', ';' });
                                if (lastSepIdx >= 0)
                                {
                                    // realKeyValue包含混合段中嵌入的key=value（如 "offsetX=-1.126"）
                                    var realKeyValue = prevValue.Substring(lastSepIdx + 1);
                                    if (realKeyValue.Contains('='))
                                    {
                                        // 混合段已包含完整的 key=value，直接作为第一条数据
                                        var remaining = new List<string> { realKeyValue };
                                        for (int j = i; j < parts.Length; j++)
                                            remaining.Add(parts[j]);
                                        return string.Join(",", remaining);
                                    }
                                }
                            }
                        }
                    }

                    // 无前缀混合，直接从当前段开始拼接
                    var resultParts = new List<string>();
                    for (int j = i; j < parts.Length; j++)
                        resultParts.Add(parts[j]);
                    return string.Join(",", resultParts);
                }
            }

            return null;
        }

        /// <summary>
        /// 格式1：逗号分隔的 key=value 对，如 "offsetX=1.5,offsetY=-0.3"
        /// </summary>
        private bool TryParseKeyValuePairs(string data, out Dictionary<string, double> result)
        {
            result = new Dictionary<string, double>();

            if (!data.Contains('='))
                return false;

            var parts = data.Split(',');
            foreach (var part in parts)
            {
                var kv = part.Split(new[] { '=' }, 2);
                if (kv.Length != 2)
                    return false;

                var key = kv[0].Trim();
                var valueStr = kv[1].Trim();

                if (!double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    return false;

                result[key] = value;
            }

            return result.Count > 0;
        }

        /// <summary>
        /// 格式2：空格分隔的 key value 对，如 "offsetX 1.5 offsetY -0.3"
        /// </summary>
        private bool TryParseSpaceSeparated(string data, out Dictionary<string, double> result)
        {
            result = new Dictionary<string, double>();

            // 不含逗号且含空格时才尝试此格式
            if (data.Contains(',') || !data.Contains(' '))
                return false;

            var tokens = data.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2 || tokens.Length % 2 != 0)
                return false;

            for (int i = 0; i < tokens.Length - 1; i += 2)
            {
                var key = tokens[i].Trim();
                var valueStr = tokens[i + 1].Trim();

                if (!double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    return false;

                result[key] = value;
            }

            return result.Count > 0;
        }

        /// <summary>
        /// 格式3：纯逗号分隔的数值，如 "1.5,-0.3,0.1"，按顺序映射为 X, Y, U, Distance
        /// </summary>
        private bool TryParsePlainValues(string data, out Dictionary<string, double> result)
        {
            result = new Dictionary<string, double>();

            var parts = data.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                var valueStr = parts[i].Trim();
                if (!double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    return false;

                var key = i < DefaultKeys.Length ? DefaultKeys[i] : $"V{i}";
                result[key] = value;
            }

            return result.Count > 0;
        }
    }
}

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

        public DefaultVisionDataParser(ILoggerService logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 按优先级依次尝试三种格式解析原始数据
        /// </summary>
        public Dictionary<string, double> Parse(string rawData)
        {
            var result = new Dictionary<string, double>();

            if (string.IsNullOrWhiteSpace(rawData))
            {
                _logger.Warn("视觉数据为空，无法解析");
                return result;
            }

            var trimmed = rawData.Trim();

            // 优先尝试 key=value 格式
            if (TryParseKeyValuePairs(trimmed, out var kvResult))
                return kvResult;

            // 其次尝试空格分隔的 key-value 对
            if (TryParseSpaceSeparated(trimmed, out var spaceResult))
                return spaceResult;

            // 最后尝试纯逗号分隔值，使用默认键名
            if (TryParsePlainValues(trimmed, out var plainResult))
                return plainResult;

            _logger.Warn($"无法识别的视觉数据格式: {trimmed}");
            return result;
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
                var kv = part.Split('=');
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

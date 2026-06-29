using Core.Abstraction;
using Core.Utilities;
using MotionControl.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StationTasks.Services
{
    /// <summary>
    /// 3D相机数据解析器：解析 Camera=3DCAMERA;VISION_RESULT:SUCCESS:val1,val2,... 格式
    /// 前 N 个数值分别映射为 Tab1Height, Tab2Height, ... TabNHeight
    /// </summary>
    public class Camera3DDataParser : IVisionDataParser
    {
        private readonly ILoggerService _logger;
        /// <summary> 本地化服务，用于日志多语言支持 </summary>
        private readonly ILocalizationService _localization;

        /// <summary>
        /// Tab数量，决定前N个数值映射为Tab高度键名
        /// </summary>
        public int TabCount { get; set; } = 6;

        public Camera3DDataParser(ILoggerService logger, ILocalizationService localization, int tabCount = 6)
        {
            _logger = logger;
            _localization = localization;
            TabCount = tabCount;
        }

        /// <summary>
        /// 解析3D相机原始数据字符串
        /// 格式：Camera=3DCAMERA;VISION_RESULT:SUCCESS:14.164,10.713,9.399,...
        /// </summary>
        public Dictionary<string, double> Parse(string rawData)
        {
            var result = new Dictionary<string, double>();

            if (string.IsNullOrWhiteSpace(rawData))
            {
                _logger.Warn(_localization.GetResourceOrDefault("Cam3D_Log_DataEmpty", "3D相机数据为空，无法解析"));
                return result;
            }

            try
            {
                var segments = rawData.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                string visionSegment = null;
                foreach (var seg in segments)
                {
                    if (seg.TrimStart().StartsWith("VISION_RESULT:", StringComparison.OrdinalIgnoreCase))
                    {
                        visionSegment = seg.Trim();
                        break;
                    }
                }

                if (visionSegment == null)
                {
                    _logger.Warn(string.Format(_localization.GetResourceOrDefault("Cam3D_Log_VisionResultSegmentNotFound", "3D相机数据中未找到 VISION_RESULT 段: {0}"), rawData));
                    return result;
                }

                var colonParts = visionSegment.Split(new[] { ':' }, 3);
                if (colonParts.Length < 3)
                {
                    _logger.Warn(string.Format(_localization.GetResourceOrDefault("Cam3D_Log_VisionResultSegmentInvalid", "VISION_RESULT 段格式不正确: {0}"), visionSegment));
                    return result;
                }

                string status = colonParts[1].Trim().ToUpperInvariant();
                string valuesPart = colonParts[2].Trim();

                if (status != "SUCCESS")
                {
                    throw new RecoverableException(
                        message: $"3D相机视觉检测失败，状态: {status}",
                        suggestedAction: "请检查3D相机是否正常工作、被测物是否放置正确，复位后重试。"
                    );
                }

                if (string.IsNullOrEmpty(valuesPart))
                {
                    _logger.Warn(_localization.GetResourceOrDefault("Cam3D_Log_NoValues", "VISION_RESULT 成功但无数值数据"));
                    return result;
                }

                var valueStrings = valuesPart.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                int count = Math.Min(TabCount, valueStrings.Length);

                for (int i = 0; i < count; i++)
                {
                    if (double.TryParse(valueStrings[i].Trim(), out double val))
                    {
                        string key = $"Tab{i + 1}Height";
                        result[key] = val;
                    }
                    else
                    {
                        _logger.Warn(string.Format(_localization.GetResourceOrDefault("Cam3D_Log_TabParseFailed", "Tab{0} 数值解析失败: '{1}'"), i + 1, valueStrings[i]));
                    }
                }

                _logger.Info(string.Format(_localization.GetResourceOrDefault("Cam3D_Log_ParseCompleted", "3D相机数据解析完成: {0} 个Tab高度值"), result.Count));
                return result;
            }
            catch (RecoverableException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localization.GetResourceOrDefault("Cam3D_Log_ParseException", "3D相机数据解析异常: {0}"), ex.Message));
                throw new RecoverableException(
                    message: $"3D相机数据解析异常: {ex.Message}",
                    suggestedAction: "请检查3D相机数据格式是否正确。"
                );
            }
        }
    }
}

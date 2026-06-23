using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Core.Services
{
    /// <summary>
    /// 点胶轨迹段数据源服务实现——桥接 CAD 编辑器、工站参数与 JSON 配置文件
    /// </summary>
    public class DispenseSegmentSourceService : IDispenseSegmentSourceService
    {
        private readonly IDispenseSegmentStore _segmentStore;
        private readonly IStationRegistry _stationRegistry;
        private readonly ILoggerService _logger;

        private static readonly JsonSerializerOptions SegmentJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public DispenseSegmentSourceService(
            IDispenseSegmentStore segmentStore,
            IStationRegistry stationRegistry,
            ILoggerService logger)
        {
            _segmentStore = segmentStore;
            _stationRegistry = stationRegistry;
            _logger = logger;
        }

        /// <inheritdoc />
        public IReadOnlyList<DispenseSegment> GetSourceSegments()
        {
            // 1. 优先使用 CAD 编辑器已注册的实时段数据
            var storeSegments = _segmentStore?.CurrentSegments;
            if (storeSegments != null && storeSegments.Count > 0)
                return storeSegments.ToList();

            // 2. 回退到工站参数内嵌段列表
            var stationParams = GetDispenserStationParameters();
            var stationSegments = TryGetSegmentsFromParameters(stationParams);
            if (stationSegments.Count > 0)
                return stationSegments;

            // 3. 从 LastSegmentConfigPath 指定的 JSON 文件加载（CadPointEditor 保存的轨迹配置）
            var configPath = ResolveSegmentConfigPath(stationParams);
            if (string.IsNullOrWhiteSpace(configPath))
                return Array.Empty<DispenseSegment>();

            var fileSegments = DispenseSegmentFileLoader.LoadFromFile(configPath, _logger);
            if (fileSegments.Count > 0)
                _logger.Info($"[DispenseSegmentSource] 从配置文件加载 {fileSegments.Count} 段: {configPath}");

            return fileSegments;
        }

        /// <inheritdoc />
        public CoordinateAlignData TryLoadAlignData()
        {
            var stationParams = GetDispenserStationParameters();
            var configPath = ResolveSegmentConfigPath(stationParams);
            if (string.IsNullOrWhiteSpace(configPath))
                return null;

            return DispenseSegmentFileLoader.LoadAlignDataFromFile(configPath, _logger);
        }

        /// <summary>获取点胶工站当前参数对象</summary>
        private object GetDispenserStationParameters()
        {
            try
            {
                var station = _stationRegistry?.GetStation("DispenserStation");
                if (station is IStationParameterProvider provider)
                    return provider.CurrentParameters;
            }
            catch (Exception ex)
            {
                _logger.Warn($"[DispenseSegmentSource] 读取工站参数失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>解析轨迹段 JSON 配置路径（工站参数优先，共享存储次之——配方切换时以工站参数为准）</summary>
        private string ResolveSegmentConfigPath(object stationParams)
        {
            var stationPath = TryGetStringProperty(stationParams, "LastSegmentConfigPath");
            if (!string.IsNullOrWhiteSpace(stationPath))
                return stationPath;

            return _segmentStore?.LastSegmentConfigPath ?? string.Empty;
        }

        /// <summary>从工站参数对象读取 Segments 列表（支持 Segments 属性或 SegmentsSerialized JSON）</summary>
        private static List<DispenseSegment> TryGetSegmentsFromParameters(object parameters)
        {
            if (parameters == null)
                return new List<DispenseSegment>();

            var segmentsProp = parameters.GetType().GetProperty("Segments", BindingFlags.Public | BindingFlags.Instance);
            if (segmentsProp?.GetValue(parameters) is IEnumerable<DispenseSegment> segments)
            {
                var list = segments.ToList();
                if (list.Count > 0)
                    return list;
            }

            var serialized = TryGetStringProperty(parameters, "SegmentsSerialized");
            if (string.IsNullOrWhiteSpace(serialized))
                return new List<DispenseSegment>();

            try
            {
                return JsonSerializer.Deserialize<List<DispenseSegment>>(serialized, SegmentJsonOptions)
                       ?? new List<DispenseSegment>();
            }
            catch
            {
                return new List<DispenseSegment>();
            }
        }

        private static string TryGetStringProperty(object target, string propertyName)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
                return null;

            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return prop?.GetValue(target) as string;
        }
    }

    /// <summary>
    /// 轨迹段 JSON 文件加载工具——与 CadPointEditorViewModel 保存/加载格式兼容
    /// </summary>
    public static class DispenseSegmentFileLoader
    {
        private static readonly JsonSerializerOptions SegmentJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>
        /// 从 JSON 文件加载轨迹段——兼容 SegmentSaveData 新格式与纯 List 旧格式
        /// </summary>
        public static List<DispenseSegment> LoadFromFile(string path, ILoggerService logger = null)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                logger?.Warn($"[DispenseSegmentFileLoader] 轨迹配置文件不存在: {path}");
                return new List<DispenseSegment>();
            }

            try
            {
                string json = File.ReadAllText(path);

                var saveData = JsonSerializer.Deserialize<SegmentSaveData>(json, SegmentJsonOptions);
                if (saveData?.Segments != null && saveData.Segments.Count > 0)
                    return saveData.Segments;

                var legacyList = JsonSerializer.Deserialize<List<DispenseSegment>>(json, SegmentJsonOptions);
                return legacyList ?? new List<DispenseSegment>();
            }
            catch (Exception ex)
            {
                logger?.Warn($"[DispenseSegmentFileLoader] 轨迹配置文件反序列化失败 [{path}]: {ex.Message}");
                return new List<DispenseSegment>();
            }
        }

        /// <summary>从 JSON 文件加载坐标对齐数据（AlignData 字段）</summary>
        public static CoordinateAlignData LoadAlignDataFromFile(string path, ILoggerService logger = null)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                string json = File.ReadAllText(path);
                var saveData = JsonSerializer.Deserialize<SegmentSaveData>(json, SegmentJsonOptions);
                return saveData?.AlignData;
            }
            catch (Exception ex)
            {
                logger?.Warn($"[DispenseSegmentFileLoader] 对齐数据加载失败 [{path}]: {ex.Message}");
                return null;
            }
        }
    }
}

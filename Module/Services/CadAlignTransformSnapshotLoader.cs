using Core.Models;
using Core.Services;
using Newtonsoft.Json;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Module.Services
{
    /// <summary>
    /// CAD 对齐变换快照加载器——从持久化配置文件恢复变换快照。
    /// 加载策略与 CadAlignmentViewModel.TryAutoLoadConfigAsync 一致：
    /// 配方池扩展记录优先 → 默认文件 → 目录内最新 CadAlignment_*.json。
    /// </summary>
    public static class CadAlignTransformSnapshotLoader
    {
        /// <summary>从当前配方池关联的 CAD 对齐配置文件恢复变换快照</summary>
        public static async Task<CadAlignTransformSnapshot> TryLoadFromPersistedConfigAsync(IRecipePoolService recipePoolService)
        {
            try
            {
                var filePath = await ResolveConfigFilePathAsync(recipePoolService).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(filePath))
                    return new CadAlignTransformSnapshot();

                return LoadFromFile(filePath);
            }
            catch
            {
                return new CadAlignTransformSnapshot();
            }
        }

        /// <summary>同步从配置文件恢复变换快照（供执行线程使用）</summary>
        public static CadAlignTransformSnapshot TryLoadFromPersistedConfig(IRecipePoolService recipePoolService)
        {
            try
            {
                var filePath = ResolveConfigFilePathAsync(recipePoolService).GetAwaiter().GetResult();
                if (string.IsNullOrWhiteSpace(filePath))
                    return new CadAlignTransformSnapshot();

                return LoadFromFile(filePath);
            }
            catch
            {
                return new CadAlignTransformSnapshot();
            }
        }

        /// <summary>解析 CAD 对齐配置文件路径</summary>
        private static async Task<string> ResolveConfigFilePathAsync(IRecipePoolService recipePoolService)
        {
            var poolName = recipePoolService?.CurrentPoolName ?? "Default";

            // 优先从配方池扩展数据读取上次保存的文件路径
            try
            {
                var extData = await recipePoolService
                    .GetExtensionDataAsync<object>(poolName, "CadAlignment_CurrentFile")
                    .ConfigureAwait(false);
                if (extData != null)
                {
                    var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                        JsonConvert.SerializeObject(extData));
                    if (dict != null && dict.TryGetValue("FilePath", out var path) && File.Exists(path))
                        return path;
                }
            }
            catch { }

            // 回退：默认配置文件
            var configDir = GetConfigDirectory();
            var defaultPath = Path.Combine(configDir, "CadAlignment_Default.json");
            if (File.Exists(defaultPath))
                return defaultPath;

            // 回退：目录内最新的带时间戳配置文件
            if (!Directory.Exists(configDir))
                return null;

            return Directory.GetFiles(configDir, "CadAlignment_*.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        /// <summary>从 JSON 配置文件构建变换快照（仅需 Step1/Step2 变换参数）</summary>
        private static CadAlignTransformSnapshot LoadFromFile(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var config = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (config == null)
                return new CadAlignTransformSnapshot();

            return BuildSnapshotFromConfig(config);
        }

        /// <summary>从配置字典构建变换快照——逻辑与 CadAlignmentViewModel.PublishTransformSnapshot 一致</summary>
        internal static CadAlignTransformSnapshot BuildSnapshotFromConfig(Dictionary<string, object> config)
        {
            bool step1Done = TryGetBool(config, "Step1Done");
            bool step2Done = TryGetBool(config, "Step2Done");
            bool useAffine = TryGetBool(config, "UseAffineCalibration");

            var snapshot = new CadAlignTransformSnapshot
            {
                IsValid = step1Done && step2Done,
                Mox = TryGetDouble(config, "Mox"),
                Moy = TryGetDouble(config, "Moy"),
                DeltaX = TryGetDouble(config, "DeltaX"),
                DeltaY = TryGetDouble(config, "DeltaY"),
                UseAffineCalibration = useAffine,
                InvertXAngle = TryGetBool(config, "InvertXAngle"),
                InvertYAngle = TryGetBool(config, "InvertYAngle"),
                InvertThetaAngle = TryGetBool(config, "InvertThetaAngle")
            };

            if (useAffine)
            {
                // 从持久化的仿射参数重建结果对象（配置加载时 ComputeAffineCalibration 的等价逻辑）
                int pointCount = 0;
                if (config.TryGetValue("AffineCalibrationPoints", out var acpObj))
                {
                    var acpJson = JsonConvert.SerializeObject(acpObj);
                    var acpList = JsonConvert.DeserializeObject<List<object>>(acpJson);
                    pointCount = acpList?.Count ?? 0;
                }

                snapshot.AffineResult = new AffineCalibrationResult
                {
                    A = TryGetDouble(config, "AffineA"),
                    B = TryGetDouble(config, "AffineB"),
                    C = TryGetDouble(config, "AffineC"),
                    D = TryGetDouble(config, "AffineD"),
                    Tx = TryGetDouble(config, "AffineTx"),
                    Ty = TryGetDouble(config, "AffineTy"),
                    RmsError = TryGetDouble(config, "AffineRmsError"),
                    PointCount = pointCount
                };
            }

            return snapshot;
        }

        /// <summary>获取 CAD 对齐配置目录</summary>
        private static string GetConfigDirectory()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
            return Path.Combine(baseDir, "Config", "CadAlignment");
        }

        private static bool TryGetBool(Dictionary<string, object> config, string key) =>
            config.TryGetValue(key, out var val) && Convert.ToBoolean(val);

        private static double TryGetDouble(Dictionary<string, object> config, string key) =>
            config.TryGetValue(key, out var val) ? Convert.ToDouble(val) : 0.0;
    }
}

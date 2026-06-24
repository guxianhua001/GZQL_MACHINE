using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using Newtonsoft.Json;
using Recipe.Interfaces;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Module.Services
{
    /// <summary>
    /// 针头相机标定数据提供者实现。
    /// 数据来源与“针头相机校准”维护页面一致：
    /// 1) 优先读取配方池扩展记录 NeedleCamera_CurrentFile_System{N} 指向的参数文件；
    /// 2) 回退到 Config/NeedleSystems/System{N} 目录中最新的标定文件。
    /// 偏移取 标定文件中的 针尖坐标 - 相机中心坐标（与 CalibrationDelta 一致）。
    /// </summary>
    public class NeedleCameraCalibrationProvider : INeedleCameraCalibrationProvider
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly ILoggerService _logger;

        public NeedleCameraCalibrationProvider(IRecipePoolService recipePoolService, ILoggerService logger)
        {
            _recipePoolService = recipePoolService;
            _logger = logger;
        }

        /// <summary>异步获取相机-针头固定偏移（针尖-相机中心）</summary>
        public async Task<(double OffsetX, double OffsetY)> GetCameraNeedleOffsetAsync(int systemNumber)
        {
            try
            {
                var filePath = await ResolveConfigFilePathAsync(systemNumber).ConfigureAwait(false);
                return LoadOffsetFromFile(filePath, systemNumber);
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[NeedleCameraCalibProvider] 获取系统{systemNumber}针头偏移失败: {ex.Message}");
                return (0, 0);
            }
        }

        /// <summary>同步获取相机-针头固定偏移（供运动执行线程使用）</summary>
        public (double OffsetX, double OffsetY) GetCameraNeedleOffset(int systemNumber)
        {
            try
            {
                // 执行线程不便 await，直接阻塞等待（配方池读取为本地文件，开销可控）
                var filePath = ResolveConfigFilePathAsync(systemNumber).GetAwaiter().GetResult();
                return LoadOffsetFromFile(filePath, systemNumber);
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[NeedleCameraCalibProvider] 获取系统{systemNumber}针头偏移失败: {ex.Message}");
                return (0, 0);
            }
        }

        /// <summary>解析当前系统的标定文件路径：配方池记录优先，回退目录内最新文件</summary>
        private async Task<string> ResolveConfigFilePathAsync(int systemNumber)
        {
            var poolName = _recipePoolService?.CurrentPoolName ?? "Default";
            var extKey = $"NeedleCamera_CurrentFile_System{systemNumber}";

            try
            {
                var record = await _recipePoolService
                    .GetExtensionDataAsync<NeedleCameraFileRecordDto>(poolName, extKey)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(record?.FilePath) && File.Exists(record.FilePath))
                    return record.FilePath;
            }
            catch (Exception ex)
            {
                _logger?.Warn($"[NeedleCameraCalibProvider] 读取配方池记录失败: {ex.Message}");
            }

            var configDir = GetConfigDirectory(systemNumber);
            if (!Directory.Exists(configDir))
                return null;

            return Directory
                .EnumerateFiles(configDir, $"NeedleCalibration_System{systemNumber}_*.json")
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();
        }

        /// <summary>从标定文件读取针尖-相机中心偏移</summary>
        private (double OffsetX, double OffsetY) LoadOffsetFromFile(string filePath, int systemNumber)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                _logger?.Info($"[NeedleCameraCalibProvider] 系统{systemNumber}无可用标定文件，偏移取0");
                return (0, 0);
            }

            var json = File.ReadAllText(filePath);
            var p = JsonConvert.DeserializeObject<NeedleCameraCalibrationParams>(json);
            if (p == null)
                return (0, 0);

            // 优先使用持久化的 CalibrationDelta；为空时回退 针尖-相机中心
            double dx = p.CalibrationDeltaX;
            double dy = p.CalibrationDeltaY;
            if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9)
            {
                dx = p.NeedleTipX - p.CameraCenterX;
                dy = p.NeedleTipY - p.CameraCenterY;
            }

            return (dx, dy);
        }

        /// <summary>系统配置目录：Config/NeedleSystems/System{N}</summary>
        private static string GetConfigDirectory(int systemNumber) =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "NeedleSystems", $"System{systemNumber}");

        /// <summary>配方池扩展记录 DTO（仅需 FilePath，与维护页面记录结构一致）</summary>
        private class NeedleCameraFileRecordDto
        {
            public string FilePath { get; set; }
        }
    }
}

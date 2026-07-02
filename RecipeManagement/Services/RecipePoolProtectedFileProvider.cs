using Core.Abstraction;
using Core.Utilities;
using Recipe.Interfaces;
using Recipe.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Recipe.Services
{
    /// <summary>
    /// 基于配方池 ExtensionData 的受保护文件路径提供者。
    /// 扫描所有配方池的 ExtensionData，提取键名含 "CurrentFile" 的条目中的 FilePath，
    /// 返回被配方池引用的配置文件路径集合，供 ConfigFileRetentionService 清理时跳过。
    /// </summary>
    /// <remarks>
    /// 依赖方向：Core 定义 IProtectedFileProvider 接口，Recipe 实现之（依赖倒置）。
    /// ConfigFileRetentionService 注入接口而非实现，不反向依赖 Recipe。
    /// </remarks>
    public class RecipePoolProtectedFileProvider : IProtectedFileProvider
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly ILoggerService _logger;

        /// <summary>
        /// 构造函数：注入配方池服务用于获取所有池数据。
        /// </summary>
        /// <param name="recipePoolService">配方池服务</param>
        /// <param name="logger">日志服务</param>
        public RecipePoolProtectedFileProvider(IRecipePoolService recipePoolService, ILoggerService logger)
        {
            _recipePoolService = recipePoolService ?? throw new ArgumentNullException(nameof(recipePoolService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        /// <remarks>
        /// 本方法为同步签名（接口定义），内部通过 GetAwaiter().GetResult() 阻塞调用
        /// GetAllRecipePoolsAsync。安全前提：调用方在后台线程（Task.Run）中执行清理，
        /// ConfigFileRetentionService.CleanupFolderByCount 即如此。切勿在 UI 线程直接调用。
        /// </remarks>
        public HashSet<string> GetProtectedFilePaths()
        {
            // 使用 OrdinalIgnoreCase 确保路径大小写不敏感匹配
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // 获取所有配方池（阻塞调用，仅在后台清理线程中使用）
                var pools = _recipePoolService.GetAllRecipePoolsAsync().GetAwaiter().GetResult();
                if (pools == null || pools.Count == 0)
                    return result;

                foreach (var pool in pools)
                {
                    ScanPoolExtensionData(pool, result);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(
                    "扫描配方池受保护文件失败: {0}", ex.Message));
            }

            return result;
        }

        /// <summary>
        /// 扫描单个配方池的 ExtensionData，提取所有 *_CurrentFile 条目中的 FilePath。
        /// </summary>
        /// <param name="pool">配方池实例</param>
        /// <param name="result">受保护文件路径集合（追加写入）</param>
        private void ScanPoolExtensionData(RecipePool pool, HashSet<string> result)
        {
            if (pool?.ExtensionData == null || pool.ExtensionData.Count == 0)
                return;

            foreach (var kvp in pool.ExtensionData)
            {
                // 筛选键名含 "CurrentFile" 的条目
                // （如 ProcessSequence_CurrentFile、VisionCapture_CurrentFile、
                //   NeedleCamera_CurrentFile_System1、CadPoint_CurrentFile 等）
                if (string.IsNullOrEmpty(kvp.Key) ||
                    !kvp.Key.Contains("CurrentFile", StringComparison.OrdinalIgnoreCase))
                    continue;

                var filePath = ExtractFilePath(kvp.Value);
                if (!string.IsNullOrEmpty(filePath))
                {
                    // 规范化路径，确保与 FileInfo.FullName 比较时一致
                    var fullPath = Path.GetFullPath(filePath);
                    result.Add(fullPath);
                }
            }
        }

        /// <summary>
        /// 从 ExtensionData 的 JsonElement? 中提取 FilePath 属性值。
        /// ExtensionData 条目结构为 { "FilePath": "C:\\...\\xxx.json" }。
        /// </summary>
        /// <param name="element">JsonElement? 值</param>
        /// <returns>FilePath 字符串；无效时返回 null</returns>
        private string ExtractFilePath(JsonElement? element)
        {
            if (!element.HasValue)
                return null;

            var value = element.Value;
            if (value.ValueKind != JsonValueKind.Object)
                return null;

            if (!value.TryGetProperty("FilePath", out var filePathProp))
                return null;

            var filePath = filePathProp.GetString();
            return string.IsNullOrEmpty(filePath) ? null : filePath;
        }
    }
}

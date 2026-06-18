using Core.Events;
using MotionControl.Interfaces;
using Prism.Events;
using Recipe.Events;
using Recipe.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace StationTasks.Services
{
    /// <summary>
    /// 从配方中获取位置信息。
    /// </summary>
    public class RecipePositionProvider : IPositionProvider
    {
        private readonly IRecipePoolService _recipePool;
        private readonly IEventAggregator _ea;
        private readonly object _cacheLock = new object();
        private string _cachedPoolName;
        private string _cachedRecipeName;
        private Dictionary<string, Dictionary<string, double>> _positionsCache;

        public RecipePositionProvider(IRecipePoolService recipePool, IEventAggregator ea)
        {
            _recipePool = recipePool;
            _ea = ea;

            _ea.GetEvent<RecipeChangedEvent>().Subscribe(recipeName => { _ = ReloadCacheAsync(); });
            _ea.GetEvent<RecipePoolChangedEvent>().Subscribe(poolName => { _ = ReloadCacheAsync(); });
            // 工站参数保存（含位置编辑器 SaveStationParametersAsync）
            _ea.GetEvent<SaveParametersCompletedEvent>().Subscribe(stationId => { _ = ReloadCacheAsync(); });
            // 位置编辑器单独发布的事件（ParameterEditor / MultiStationPositionEditor）
            _ea.GetEvent<StationParameterSavedEvent>().Subscribe(stationId => { _ = ReloadCacheAsync(); });
        }

        public async Task PreloadAsync()
        {
            var poolName = _recipePool.CurrentPoolName;
            var recipeName = await ResolveCurrentRecipeNameAsync(poolName);

            lock (_cacheLock)
            {
                if (poolName == _cachedPoolName && recipeName == _cachedRecipeName && _positionsCache != null)
                    return;
            }

            await LoadAndCacheAsync(poolName, recipeName);
        }

        public async Task<Dictionary<string, double>> GetPositionsAsync(string stationId)
        {
            var poolName = _recipePool.CurrentPoolName;
            var recipeName = await ResolveCurrentRecipeNameAsync(poolName);

            lock (_cacheLock)
            {
                if (poolName == _cachedPoolName && recipeName == _cachedRecipeName
                    && _positionsCache != null
                    && _positionsCache.TryGetValue(stationId, out var cached))
                {
                    // 返回副本，避免外部持有旧缓存引用
                    return new Dictionary<string, double>(cached);
                }
            }

            await LoadAndCacheAsync(poolName, recipeName);

            lock (_cacheLock)
            {
                if (_positionsCache != null && _positionsCache.TryGetValue(stationId, out var result))
                    return new Dictionary<string, double>(result);
                return new Dictionary<string, double>();
            }
        }

        private async Task LoadAndCacheAsync(string poolName, string recipeName)
        {
            var pool = await _recipePool.GetRecipePoolAsync(poolName);
            var recipe = pool?.GetRecipeByName(recipeName);
            if (recipe == null) return;

            var allStationPositions = new Dictionary<string, Dictionary<string, double>>();
            foreach (var paramKvp in recipe.Parameters)
            {
                var positions = ParsePositions(paramKvp.Value);
                if (positions.Count > 0)
                    allStationPositions[paramKvp.Key] = positions;
            }

            lock (_cacheLock)
            {
                _cachedPoolName = poolName;
                _cachedRecipeName = recipeName;
                _positionsCache = allStationPositions;
            }
            System.Diagnostics.Debug.WriteLine($"[RecipePositionProvider] Cache loaded: pool={poolName}, recipe={recipeName}, stations={allStationPositions.Count}, total positions={allStationPositions.Values.Sum(v => v.Count)}");
        }

        public Task InvalidateCacheAsync()
        {
            lock (_cacheLock)
            {
                _positionsCache = null;
            }
            System.Diagnostics.Debug.WriteLine("[RecipePositionProvider] Cache explicitly invalidated");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 立即从配方文件重新加载位置缓存
        /// </summary>
        public async Task RefreshCacheAsync()
        {
            lock (_cacheLock)
            {
                _positionsCache = null;
            }
            var poolName = _recipePool.CurrentPoolName;
            var recipeName = await ResolveCurrentRecipeNameAsync(poolName);
            await LoadAndCacheAsync(poolName, recipeName);
            System.Diagnostics.Debug.WriteLine($"[RecipePositionProvider] Cache refreshed: pool={poolName}, recipe={recipeName}");
        }

        private Task ReloadCacheAsync() => RefreshCacheAsync();

        /// <summary>
        /// 与位置编辑器一致：优先使用配方池文件中的 CurrentRecipeName
        /// </summary>
        private async Task<string> ResolveCurrentRecipeNameAsync(string poolName)
        {
            var pool = await _recipePool.GetRecipePoolAsync(poolName);
            var recipeName = pool?.CurrentRecipeName;
            if (string.IsNullOrEmpty(recipeName))
                recipeName = _recipePool.CurrentRecipeName;
            if (string.IsNullOrEmpty(recipeName))
                recipeName = "Default";
            return recipeName;
        }

        /// <summary>
        /// 解析参数对象中的位置信息，兼容 JsonElement 和普通对象两种输入
        /// </summary>
        private static Dictionary<string, double> ParsePositions(object paramObj)
        {
            var result = new Dictionary<string, double>();

            try
            {
                var json = JsonSerializer.Serialize(paramObj);
                var node = JsonNode.Parse(json);
                if (node is not JsonObject rootObj)
                    return result;

                var positionsNode = rootObj["Positions"];
                if (positionsNode is not JsonObject posObj)
                    return result;

                foreach (var kvp in posObj)
                {
                    var posName = kvp.Key;
                    var positionObj = kvp.Value as JsonObject;
                    if (positionObj == null) continue;

                    JsonObject axisSource;
                    if (positionObj.TryGetPropertyValue("Axes", out var axesNode) && axesNode is JsonObject axesObj)
                        axisSource = axesObj;
                    else
                        axisSource = positionObj;

                    foreach (var axisKvp in axisSource)
                    {
                        if (axisKvp.Key == "Comment" || axisKvp.Key == "Axes") continue;
                        if (axisKvp.Value is JsonValue val && val.TryGetValue(out double d))
                        {
                            result[$"{posName}.{axisKvp.Key}"] = d;
                        }
                    }
                }
            }
            catch (System.Exception)
            {
                // 参数对象不是有效的 JSON 对象，返回空位置字典
            }

            return result;
        }
    }
}

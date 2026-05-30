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

            _ea.GetEvent<RecipeChangedEvent>().Subscribe(OnRecipeChanged);
            _ea.GetEvent<RecipePoolChangedEvent>().Subscribe(OnPoolChanged);
            // 位置参数保存后刷新缓存，确保自定义编辑器流程获取最新位置
            _ea.GetEvent<SaveParametersCompletedEvent>().Subscribe(OnParametersSaved);
        }

        public async Task PreloadAsync()
        {
            var poolName = _recipePool.CurrentPoolName;
            var recipeName = _recipePool.CurrentRecipeName;

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
            var recipeName = _recipePool.CurrentRecipeName;

            lock (_cacheLock)
            {
                if (poolName == _cachedPoolName && recipeName == _cachedRecipeName
                    && _positionsCache != null
                    && _positionsCache.TryGetValue(stationId, out var cached))
                {
                    return cached;
                }
            }

            await LoadAndCacheAsync(poolName, recipeName);

            lock (_cacheLock)
            {
                return _positionsCache != null && _positionsCache.TryGetValue(stationId, out var result)
                    ? result
                    : new Dictionary<string, double>();
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

        private void OnRecipeChanged(string recipeName)
        {
            _ = ReloadCacheAsync();
        }

        private void OnPoolChanged(string poolName)
        {
            _ = ReloadCacheAsync();
        }

        /// <summary>
        /// 位置参数保存完成后刷新缓存，确保后续位置查询获取最新数据
        /// </summary>
        private void OnParametersSaved(string recipeName)
        {
            lock (_cacheLock)
            {
                _positionsCache = null;
            }
            System.Diagnostics.Debug.WriteLine($"[RecipePositionProvider] Cache cleared after parameters saved. Recipe: {recipeName}");
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

        private async Task ReloadCacheAsync()
        {
            var poolName = _recipePool.CurrentPoolName;
            var recipeName = _recipePool.CurrentRecipeName;
            await LoadAndCacheAsync(poolName, recipeName);
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

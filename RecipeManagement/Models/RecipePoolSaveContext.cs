using Recipe.Interfaces;
using Recipe.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Recipe.Models
{
    public class RecipePoolSaveContext
    {
        private readonly IRecipeStorage _recipeStorage;
        private readonly string _poolId;
        private readonly string _recipeName;
        private readonly Dictionary<string, object> _stationParameters = new Dictionary<string, object>();
        private readonly Dictionary<string, bool> _replacePositionsFlags = new Dictionary<string, bool>();

        public string PoolId => _poolId;
        public string RecipeName => _recipeName;
        public IReadOnlyDictionary<string, object> StationParameters => _stationParameters;

        public RecipePoolSaveContext(IRecipeStorage recipeStorage, string poolId, string recipeName)
        {
            _recipeStorage = recipeStorage;
            _poolId = poolId;
            _recipeName = recipeName;
        }

        public void AddStation(string stationIdentifier, object parameters, bool replacePositions = false)
        {
            _stationParameters[stationIdentifier] = parameters;
            if (replacePositions)
                _replacePositionsFlags[stationIdentifier] = true;
        }

        public async Task<bool> CommitAsync()
        {
            try
            {
                var pool = await _recipeStorage.LoadRecipePoolAsync(_poolId).ConfigureAwait(false);
                if (pool == null)
                {
                    pool = new RecipePool
                    {
                        Id = _poolId,
                        Name = _poolId,
                        CreatedTime = DateTime.Now
                    };
                }

                var recipe = pool.GetRecipeByName(_recipeName);
                if (recipe == null)
                {
                    recipe = new RecipeInfo
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = _recipeName,
                        Description = $"Recipe - {_recipeName}",
                        CreatedTime = DateTime.Now,
                        ModifiedTime = DateTime.Now
                    };
                    pool.AddRecipe(recipe);
                }
                else
                {
                    recipe.ModifiedTime = DateTime.Now;
                }

                // 合并写入工站参数，避免陈旧内存对象整对象覆盖位置编辑器等已持久化的 Positions
                foreach (var kv in _stationParameters)
                {
                    var replacePositions = _replacePositionsFlags.TryGetValue(kv.Key, out var flag) && flag;
                    recipe.MergeStationParameter(kv.Key, kv.Value, replacePositions);
                }

                // 更新配方池修改时间，确保 Save Pool 后 ModifiedTime 反映最新操作
                pool.ModifiedTime = DateTime.Now;

                await _recipeStorage.SaveRecipePoolAsync(pool).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

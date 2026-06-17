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

        public string PoolId => _poolId;
        public string RecipeName => _recipeName;
        public IReadOnlyDictionary<string, object> StationParameters => _stationParameters;

        public RecipePoolSaveContext(IRecipeStorage recipeStorage, string poolId, string recipeName)
        {
            _recipeStorage = recipeStorage;
            _poolId = poolId;
            _recipeName = recipeName;
        }

        public void AddStation(string stationIdentifier, object parameters)
        {
            _stationParameters[stationIdentifier] = parameters;
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
                        CreatedTime = DateTime.UtcNow
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
                        CreatedTime = DateTime.UtcNow,
                        ModifiedTime = DateTime.UtcNow
                    };
                    pool.AddRecipe(recipe);
                }
                else
                {
                    recipe.ModifiedTime = DateTime.UtcNow;
                }

                foreach (var kv in _stationParameters)
                {
                    recipe.SetParameter(kv.Key, kv.Value);
                }

                // 更新配方池修改时间，确保 Save Pool 后 ModifiedTime 反映最新操作
                pool.ModifiedTime = DateTime.UtcNow;

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

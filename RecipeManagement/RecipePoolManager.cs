using Core.Utilities;
using Recipe.Interfaces;
using Recipe.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Recipe
{
    public class RecipePoolManager
    {
        private readonly IRecipeStorage _recipeStorage;
        private readonly ILoggerService _logger;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public RecipePoolManager(IRecipeStorage recipeStorage, ILoggerService logger)
        {
            _recipeStorage = recipeStorage;
            _logger = logger;
        }

        public async Task<bool> SaveStationParametersAsync(string poolId, string recipeName, string stationIdentifier, object parameters)
        {
            await _semaphore.WaitAsync();
            try
            {
                // 加载配方池
                var pool = await _recipeStorage.LoadRecipePoolAsync(poolId);
                if (pool == null)
                {
                    pool = new RecipePool
                    {
                        Id = poolId,
                        Name = poolId,
                        CreatedTime = DateTime.UtcNow
                    };
                }

                // 获取或创建配方
                var recipe = pool.GetRecipeByName(recipeName);
                if (recipe == null)
                {
                    recipe = new RecipeInfo
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = recipeName,
                        Description = $"配方 - {recipeName}",
                        CreatedTime = DateTime.UtcNow,
                        ModifiedTime = DateTime.UtcNow
                    };
                    pool.AddRecipe(recipe);
                }
                else
                {
                    recipe.ModifiedTime = DateTime.UtcNow;
                }

                // 设置工站参数
                recipe.SetParameter(stationIdentifier, parameters);

                // 保存配方池
                await _recipeStorage.SaveRecipePoolAsync(pool);

                _logger.Info($"[{stationIdentifier}] 参数已保存到配方系统: 池 '{poolId}' -> 配方 '{recipeName}'");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"[{stationIdentifier}] 保存参数到配方系统失败: {ex.Message}");
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}

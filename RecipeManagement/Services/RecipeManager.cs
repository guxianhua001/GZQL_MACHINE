// RecipeManagement.Application/RecipeManager.cs
using System.IO;
using System.Text.Json;
using Recipe.Interfaces;
using Recipe.Models;

namespace Recipe.Services
{
    public class RecipeManager : IRecipeManager
    {
        private readonly IRecipeStorage _recipeStorage;
        private RecipePool _currentRecipePool;

        public RecipeManager(IRecipeStorage recipeStorage)
        {
            _recipeStorage = recipeStorage;
            _currentRecipePool = new RecipePool { Id = "Default", Name = "Default" };
        }
        public RecipePool GetCurrentRecipePool()
        {
            return _currentRecipePool;
        }
        public IEnumerable<RecipePool> GetAllRecipePools()
        {
            var poolIds = _recipeStorage.GetAvailableRecipePoolsAsync().Result;
            var pools = new List<RecipePool>();

            foreach (var poolId in poolIds)
            {
                var pool = _recipeStorage.LoadRecipePoolAsync(poolId).Result;
                if (pool != null)
                    pools.Add(pool);
            }
            return pools;
        }

        public RecipePool GetRecipePool(string poolId)
        {
            return _recipeStorage.LoadRecipePoolAsync(poolId).Result;
        }

        public bool SaveRecipePool(RecipePool pool)
        {
            try
            {
                _recipeStorage.SaveRecipePoolAsync(pool).Wait();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DeleteRecipePool(string poolId)
        {
            try
            {
                // 需要先删除池中的所有配方，然后删除池
                var pool = GetRecipePool(poolId);
                if (pool != null)
                {
                    // 实现删除逻辑
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public Recipe.Models.RecipeInfo GetRecipe(string poolId, string recipeId)
        {
            return _recipeStorage.LoadRecipeAsync(poolId, recipeId).Result;
        }

        public bool SaveRecipe(string poolId, Recipe.Models.RecipeInfo recipe)
        {
            try
            {
                _recipeStorage.SaveRecipeAsync(poolId, recipe).Wait();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DeleteRecipe(string poolName, string poolId, string recipeId)
        {
            try
            {
                return _recipeStorage.DeleteRecipeAsync(poolName, poolId, recipeId).Result;
            }
            catch
            {
                return false;
            }
        }

        public bool ImportRecipePool(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var pool = JsonSerializer.Deserialize<RecipePool>(json);
                return SaveRecipePool(pool);
            }
            catch
            {
                return false;
            }
        }

        public bool ExportRecipePool(string poolId, string filePath)
        {
            try
            {
                var pool = GetRecipePool(poolId);
                if (pool == null) return false;

                var json = JsonSerializer.Serialize(pool, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public Recipe.Models.RecipeInfo CreateRecipeFromTemplate(string templateName)
        {
            // 基于模板创建新配方
            return new Recipe.Models.RecipeInfo
            {
                Name = $"Recipe from {templateName}",
                Category = templateName
            };
        }
    }
}
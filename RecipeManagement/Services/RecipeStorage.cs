// RecipeManagement.Infrastructure/Storages/RecipeStorage.cs
using System;
using Core.Abstractions.Storages;
using Core.Services;
using Recipe.Interfaces;
using Recipe.Models;

namespace Recipe.Services
{
    public class RecipeStorage : IRecipeStorage
    {
        private readonly IGenericStorage _genericStorage;
        private const string RecipePoolPrefix = "recipe_pool_";
        private const string RecipePrefix = "recipe_";

        public RecipeStorage(IGenericStorage genericStorage)
        {
            _genericStorage = genericStorage;
        }

        public async Task<RecipePool> LoadRecipePoolAsync(string poolName)
        {
            var key = $"{RecipePoolPrefix}{poolName}";// 配方池名称 默认"Default"
            return await _genericStorage.LoadAsync<RecipePool>(key).ConfigureAwait(false);
        }

        public async Task SaveRecipePoolAsync(RecipePool pool)
        {
            var key = $"{RecipePoolPrefix}{pool.Name}";
            await _genericStorage.SaveAsync(key, pool);
        }

        public async Task<bool> RecipePoolExistsAsync(string poolId)
        {
            var key = $"{RecipePoolPrefix}{poolId}";
            return await _genericStorage.ExistsAsync<RecipePool>(key);
        }

        public async Task<IEnumerable<string>> GetAvailableRecipePoolsAsync()
        {
            try
            {
                // 如果 IGenericStorage 支持文件枚举，可以这样实现
                if (_genericStorage is JsonRecipeFileStorage jsonStorage)
                {
                    // 使用 JsonRecipeFileStorage 的文件枚举方法
                    var poolFiles = await jsonStorage.GetAllRecipePoolFilesAsync();
                    return poolFiles.Select(file => jsonStorage.ExtractPoolIdFromFileName(file)).ToList();
                }

                // 备用方案：检查默认池是否存在
                var defaultPoolExists = await RecipePoolExistsAsync("Default");
                if (defaultPoolExists)
                {
                    return new List<string> { "Default" };
                }

                return new List<string>();
            }
            catch (Exception ex)
            {
                // 记录错误并返回空列表
                Console.WriteLine($"获取配方池列表失败: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<Recipe.Models.RecipeInfo> LoadRecipeAsync(string poolId, string recipeId)
        {
            var pool = await LoadRecipePoolAsync(poolId);
            return pool?.Recipes.FirstOrDefault(r => r.Name == recipeId);
        }

        public async Task SaveRecipeAsync(string poolId, Recipe.Models.RecipeInfo recipe)
        {
            var pool = await LoadRecipePoolAsync(poolId) ?? new RecipePool { Id = poolId, Name = "Default" };
            //pool.Id = recipe.Id;
            //pool.Name = recipe.Name;
            var existingRecipe = pool.GetRecipe(recipe.Id);
            if (existingRecipe != null)
                pool.Recipes.Remove(existingRecipe);

            pool.AddRecipe(recipe);
            await SaveRecipePoolAsync(pool);
        }

        public async Task<bool> DeleteRecipeAsync(string poolName, string poolId, string recipeId)
        {
            var pool = await LoadRecipePoolAsync(poolName);
            if (pool == null) return false;

            var recipe = pool.GetRecipe(poolId);
            if (recipe == null) return false;

            pool.Recipes.Remove(recipe);
            await SaveRecipePoolAsync(pool);
            return true;
        }

        public async Task<IEnumerable<Recipe.Models.RecipeInfo>> LoadAllRecipesAsync(string poolId)
        {
            var pool = await LoadRecipePoolAsync(poolId);
            return pool?.Recipes ?? Enumerable.Empty<Recipe.Models.RecipeInfo>();
        }

        public async Task SaveAllRecipesAsync(string poolId, IEnumerable<Recipe.Models.RecipeInfo> recipes)
        {
            var pool = await LoadRecipePoolAsync(poolId) ?? new RecipePool { Id = poolId ,Name = "Default" };
            pool.Recipes = recipes.ToList();
            await SaveRecipePoolAsync(pool);
        }

        public Task DeleteRecipePoolAsync(string poolId)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Models.RecipeInfo>> SearchRecipesAsync(string poolId, string searchTerm)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<string>> GetRecipeCategoriesAsync(string poolId)
        {
            throw new NotImplementedException();
        }
    }
}
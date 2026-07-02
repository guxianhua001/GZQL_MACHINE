using Core.Abstractions.Storages;
using Core.Abstraction;
using Core.Services;
using Core.Utilities;
using Recipe.Interfaces;
using Recipe.Models;
using System;
using System.IO;

namespace Recipe.Services
{
    public class RecipeStorage : IRecipeStorage
    {
        private readonly IGenericStorage _genericStorage;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;
        private const string RecipePoolPrefix = "recipe_pool_";
        private const string RecipePrefix = "recipe_";

        public RecipeStorage(IGenericStorage genericStorage, ILoggerService logger, ILocalizationService localization)
        {
            _genericStorage = genericStorage;
            _logger = logger;
            _localization = localization;
        }

        public async Task<RecipePool> LoadRecipePoolAsync(string poolName)
        {
            var key = $"{RecipePoolPrefix}{poolName}";// 配方池名称 默认"Default"
            return await _genericStorage.LoadAsync<RecipePool>(key).ConfigureAwait(false);
        }

        public async Task SaveRecipePoolAsync(RecipePool pool)
        {
            var key = $"{RecipePoolPrefix}{pool.Name}";
            // 备份现有文件（如果存在）。必须用 pool.Name（存储键后缀）而非
            // pool.CurrentRecipePoolName——后者是 SetCurrentRecipeInfo 的快照字段
            // （默认 "Default"），非默认池克隆自源池后不会更新，会导致备份文件名
            // 误标为 recipe_pool_Default.json_*.bak，备份目录也归到错误的池名下。
            BackupRecipePoolFile(pool.Name);
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

        public async Task SaveRecipeAsync(string poolId, RecipeInfo recipe)
        {
            var pool = await LoadRecipePoolAsync(poolId) ?? new RecipePool { Id = poolId, Name = "Default" };

            var existingRecipe = pool.GetRecipe(recipe.Id);
            if (existingRecipe != null)
                pool.Recipes.Remove(existingRecipe);

            pool.AddRecipe(recipe);
            await SaveRecipePoolAsync(pool);
        }

        public async Task<bool> DeleteRecipeAsync(string poolName, string recipeId)
        {
            var pool = await LoadRecipePoolAsync(poolName);
            if (pool == null) return false;

            var recipe = pool.GetRecipe(recipeId);
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

        public async Task DeleteRecipePoolAsync(string poolId)
        {
            if (string.IsNullOrEmpty(poolId))
                throw new ArgumentNullException(nameof(poolId));

            string key = $"{RecipePoolPrefix}{poolId}";
            await _genericStorage.DeleteAsync<RecipePool>(key);
            _logger?.Info(string.Format(_localization.GetResourceOrDefault("RStor_Log_PoolDeleted", "配方池 '{0}' 已删除。"), poolId));
        }

        // 辅助方法：清理文件名中的非法字符
        private string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        public async Task<IEnumerable<RecipeInfo>> SearchRecipesAsync(string poolId, string searchTerm)
        {
            var allRecipes = await LoadAllRecipesAsync(poolId);
            if (string.IsNullOrWhiteSpace(searchTerm))
                return allRecipes;

            searchTerm = searchTerm.Trim();
            return allRecipes.Where(r =>
                r.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                (r.Description?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }

        public async Task<IEnumerable<string>> GetRecipeCategoriesAsync(string poolId)
        {
            var recipes = await LoadAllRecipesAsync(poolId);
            var categories = recipes
                .Select(r => r.Name?.Split('_', '-', ' ').FirstOrDefault() ?? "未分类")
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();

            if (!categories.Any())
                categories.Add("未分类");

            return categories;
        }
        private void BackupRecipePoolFile(string poolId)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string recipePoolDir = Path.Combine(baseDir, "Recipes", "recipepool");
                string fileName = $"recipe_pool_{poolId}.json";
                string filePath = Path.Combine(recipePoolDir, fileName);
                if (!File.Exists(filePath))
                    return;

                string backupDir = Path.Combine(baseDir, "Recipes", "BackUp", poolId, DateTime.Now.ToString("yyyy-MM-dd"));
                Directory.CreateDirectory(backupDir);
                // 毫秒精度避免同一秒内多次备份互相覆盖
                string timestamp = DateTime.Now.ToString("HHmmssfff");
                string backupFileName = $"{fileName}_{timestamp}.bak";
                string backupPath = Path.Combine(backupDir, backupFileName);
                File.Copy(filePath, backupPath, overwrite: true);
                _logger?.Info(string.Format(_localization.GetResourceOrDefault("RStor_Log_PoolFileBackedUp", "已备份配方池文件: {0}"), backupPath));
            }
            catch (Exception ex)
            {
                // 备份失败不影响正常保存，仅记录警告日志
                _logger?.Warn(string.Format(_localization.GetResourceOrDefault("RStor_Log_BackupPoolFileFailed", "备份配方池文件失败: {0}"), ex.Message));
            }
        }
    }
}

using Recipe.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Recipe.Interfaces
{
    public interface IRecipeStorage
    {
        // 配方池操作
        Task<RecipePool> LoadRecipePoolAsync(string poolId);
        Task SaveRecipePoolAsync(RecipePool pool);
        Task<bool> RecipePoolExistsAsync(string poolId);
        Task<IEnumerable<string>> GetAvailableRecipePoolsAsync();
        Task DeleteRecipePoolAsync(string poolId);

        // 配方操作
        Task<Recipe.Models.RecipeInfo> LoadRecipeAsync(string poolId, string recipeId);
        Task SaveRecipeAsync(string poolId, Recipe.Models.RecipeInfo recipe);
        Task<bool> DeleteRecipeAsync(string poolName, string poolId, string recipeId);
        Task<IEnumerable<Recipe.Models.RecipeInfo>> SearchRecipesAsync(string poolId, string searchTerm);

        // 配方分类方法
        Task<IEnumerable<string>> GetRecipeCategoriesAsync(string poolId);

        // 批量操作
        Task<IEnumerable<Recipe.Models.RecipeInfo>> LoadAllRecipesAsync(string poolId);
        Task SaveAllRecipesAsync(string poolId, IEnumerable<Recipe.Models.RecipeInfo> recipes);

    }
}

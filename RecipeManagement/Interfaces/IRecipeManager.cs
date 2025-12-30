// RecipeManagement/Interfaces/IRecipeManager.cs
using System.Collections.Generic;
using Recipe.Models;

namespace Recipe.Interfaces
{
    public interface IRecipeManager
    {
        // 配方池管理
        IEnumerable<RecipePool> GetAllRecipePools();
        RecipePool GetRecipePool(string poolId);
        bool SaveRecipePool(RecipePool pool);
        bool DeleteRecipePool(string poolId);
        // 获取当前配方池
        RecipePool GetCurrentRecipePool();

        // 配方管理
        Recipe.Models.RecipeInfo GetRecipe(string poolId, string recipeId);
        bool SaveRecipe(string poolId, Recipe.Models.RecipeInfo recipe);
        bool DeleteRecipe(string poolName, string poolId, string recipeId);

        // 导入导出
        bool ImportRecipePool(string filePath);
        bool ExportRecipePool(string poolId, string filePath);

        // 模板功能
        Recipe.Models.RecipeInfo CreateRecipeFromTemplate(string templateName);
    }
}
// Recipe/Models/RecipePool.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Recipe.Models
{
    public class RecipePool
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Default";
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

        // 当前配方信息 - 直接保存在顶层
        public string CurrentRecipeName { get; set; } = "Default";
        public string CurrentRecipePool { get; set; } = "Default";
        //public string StationIdentifier { get; set; } = string.Empty;
        public DateTime SwitchTime { get; set; } = DateTime.UtcNow;
        public bool IsDefault { get; set; } = true;
        public List<RecipeInfo> Recipes { get; set; } = new List<RecipeInfo>();

        public RecipeInfo GetRecipe(string recipeId)
            => Recipes.FirstOrDefault(r => r.Id == recipeId);

        public void AddRecipe(RecipeInfo recipe)
        {
            var existing = GetRecipe(recipe.Id);
            if (existing != null)
                Recipes.Remove(existing);

            Recipes.Add(recipe);
        }

        public RecipeInfo GetRecipeByName(string recipeName)
        {
            return Recipes?.FirstOrDefault(r => r.Name == recipeName);
        }

        /// <summary>
        /// 设置当前配方信息
        /// </summary>
        public void SetCurrentRecipeInfo(string stationIdentifier, string recipeName, string recipePool)
        {
            CurrentRecipeName = recipeName;
            CurrentRecipePool = recipePool;
            //StationIdentifier = stationIdentifier;
            SwitchTime = DateTime.UtcNow;
            IsDefault = recipeName == "Default";
        }

        /// <summary>
        /// 获取当前配方信息
        /// </summary>
        public CurrentRecipeInfo GetCurrentRecipeInfo()
        {
            return new CurrentRecipeInfo
            {
                RecipeName = CurrentRecipeName,
                RecipePool = CurrentRecipePool,
                //StationIdentifier = StationIdentifier,
                SwitchTime = SwitchTime,
                IsDefault = IsDefault,
                IsValid = !string.IsNullOrEmpty(CurrentRecipeName)
            };
        }
    }
}
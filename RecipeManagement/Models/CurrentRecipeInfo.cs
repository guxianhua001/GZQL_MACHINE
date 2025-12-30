// Recipe/Models/CurrentRecipeInfo.cs
using System;

namespace Recipe.Models
{
    /// <summary>
    /// 当前配方信息类
    /// </summary>
    public class CurrentRecipeInfo
    {
        public string RecipeName { get; set; } = "Default";
        public string RecipePool { get; set; } = "Default";
        public string StationIdentifier { get; set; } = string.Empty;
        public DateTime SwitchTime { get; set; } = DateTime.UtcNow;
        public bool IsDefault { get; set; } = true;
        public bool IsValid { get; set; } = false;

        public override string ToString()
        {
            return $"配方: {RecipeName}, 池: {RecipePool}, 工站: {StationIdentifier}, 时间: {SwitchTime:yyyy-MM-dd HH:mm:ss}, 默认: {IsDefault}, 有效: {IsValid}";
        }
    }
}
// Recipe/Models/Recipe.cs
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace Recipe.Models
{
    public class RecipeInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedTime { get; set; } = DateTime.UtcNow;
        public string Category { get; set; } = "Default";
        public string Version { get; set; } = "1.0";
        public List<string> Tags { get; set; } = new List<string>();
        public string Author { get; set; } = string.Empty;
        public int Rating { get; set; }
        public int Difficulty { get; set; } // 1-5
        public TimeSpan EstimatedTime { get; set; }
        public List<Ingredient> Ingredients { get; set; } = new List<Ingredient>();
        public List<RecipeStep> Steps { get; set; } = new List<RecipeStep>();

        // 验证方法
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Id);
        }

        [JsonExtensionData]
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        public T GetParameter<T>(string key)
        {
            if (Parameters.ContainsKey(key))
            {
                var jsonElement = (JsonElement)Parameters[key];
                return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
            }
            return default;
        }

        public void SetParameter<T>(string key, T value)
        {
            Parameters[key] = value;
            ModifiedTime = DateTime.UtcNow;
        }
    }
    // 辅助类
    public class Ingredient
    {
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
    public class RecipeStep
    {
        public int Order { get; set; }
        public string Description { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }
}
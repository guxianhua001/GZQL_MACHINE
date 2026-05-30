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

        public RecipeInfo() { }

        public RecipeInfo(RecipeInfo other)
        {
            Id = Guid.NewGuid().ToString();
            Name = other.Name;
            Description = other.Description;
            CreatedTime = DateTime.UtcNow;
            ModifiedTime = DateTime.UtcNow;
            Category = other.Category;
            Version = other.Version;
            Tags = new List<string>(other.Tags);
            Author = other.Author;

            Parameters = new Dictionary<string, object>();
            foreach (var kvp in other.Parameters)
            {
                Parameters[kvp.Key] = CloneObject(kvp.Value);
            }
        }

        private static object CloneObject(object source)
        {
            if (source == null) return null;
            var json = JsonSerializer.Serialize(source);
            return JsonSerializer.Deserialize<object>(json);
        }

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
}

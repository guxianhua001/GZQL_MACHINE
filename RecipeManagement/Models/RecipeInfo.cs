using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Recipe.Models
{
    public class RecipeInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public DateTime ModifiedTime { get; set; } = DateTime.Now;
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
            CreatedTime = DateTime.Now;
            ModifiedTime = DateTime.Now;
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
            ModifiedTime = DateTime.Now;
        }

        /// <summary>
        /// 合并工站参数：以文件已有数据为基准，叠加 incoming 的字段变更。
        /// 当 incoming 的 Positions 条目数少于文件中已有数据时，视为陈旧内存数据，保留文件 Positions。
        /// replacePositions 为 true 时（位置编辑器保存），完整替换 Positions，支持删除位置。
        /// </summary>
        public void MergeStationParameter(string stationKey, object incoming, bool replacePositions = false)
        {
            if (string.IsNullOrEmpty(stationKey) || incoming == null)
                return;

            if (!Parameters.TryGetValue(stationKey, out var existingRaw) || existingRaw == null)
            {
                Parameters[stationKey] = incoming;
                ModifiedTime = DateTime.Now;
                return;
            }

            try
            {
                var existingNode = ToJsonObject(existingRaw);
                var incomingNode = ToJsonObject(incoming);
                if (existingNode == null)
                {
                    Parameters[stationKey] = incoming;
                    ModifiedTime = DateTime.Now;
                    return;
                }
                if (incomingNode == null)
                    return;

                // 位置参数保护：避免内存中仅有少量默认位置的对象覆盖位置编辑器已保存的大量位置
                // 位置编辑器显式 replacePositions 时跳过保护，允许删除位置后持久化
                if (!replacePositions &&
                    TryGetPositionsCount(existingNode, out int existingPosCount) &&
                    TryGetPositionsCount(incomingNode, out int incomingPosCount) &&
                    incomingPosCount < existingPosCount)
                {
                    incomingNode.Remove("Positions");
                }
                else if (!replacePositions &&
                    existingNode.ContainsKey("Positions") && !incomingNode.ContainsKey("Positions"))
                {
                    incomingNode["Positions"] = JsonNode.Parse(existingNode["Positions"]!.ToJsonString());
                }

                foreach (var kvp in incomingNode)
                {
                    existingNode[kvp.Key] = JsonNode.Parse(kvp.Value?.ToJsonString() ?? "null");
                }

                Parameters[stationKey] = JsonSerializer.SerializeToElement(existingNode);
                ModifiedTime = DateTime.Now;
            }
            catch
            {
                Parameters[stationKey] = incoming;
                ModifiedTime = DateTime.Now;
            }
        }

        private static JsonObject ToJsonObject(object value)
        {
            if (value is JsonElement element)
                return JsonNode.Parse(element.GetRawText())?.AsObject();

            return JsonNode.Parse(JsonSerializer.Serialize(value))?.AsObject();
        }

        private static bool TryGetPositionsCount(JsonObject node, out int count)
        {
            count = 0;
            if (node.TryGetPropertyValue("Positions", out var posNode) && posNode is JsonObject posObj)
            {
                count = posObj.Count;
                return true;
            }
            return false;
        }
    }
}

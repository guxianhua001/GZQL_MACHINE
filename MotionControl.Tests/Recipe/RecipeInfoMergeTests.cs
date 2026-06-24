using Recipe.Models;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace MotionControl.Tests.RecipeMerge
{
    /// <summary>工站参数合并逻辑测试，覆盖位置编辑器删除位置场景</summary>
    public class RecipeInfoMergeTests
    {
        private static JsonObject CreateStationNode(params (string name, double x)[] positions)
        {
            var posObj = new JsonObject();
            foreach (var (name, x) in positions)
            {
                posObj[name] = new JsonObject
                {
                    ["Axes"] = new JsonObject { ["X"] = x },
                    ["Comment"] = ""
                };
            }
            return new JsonObject
            {
                ["SomeParam"] = 42,
                ["Positions"] = posObj
            };
        }

        [Fact]
        public void MergeStationParameter_位置编辑器删除位置时应持久化删除()
        {
            var recipe = new RecipeInfo();
            var existing = CreateStationNode(("StandbyPosition", 0), ("SafePosition", 0), ("CustomPos", 10));
            recipe.SetParameter("DispenserStation", JsonSerializer.SerializeToElement(existing));

            var incoming = CreateStationNode(("StandbyPosition", 0), ("SafePosition", 0));
            recipe.MergeStationParameter("DispenserStation", incoming, replacePositions: true);

            var merged = JsonNode.Parse(((JsonElement)recipe.Parameters["DispenserStation"]).GetRawText())!.AsObject();
            var positions = merged["Positions"]!.AsObject();
            Assert.Equal(2, positions.Count);
            Assert.True(positions.ContainsKey("StandbyPosition"));
            Assert.True(positions.ContainsKey("SafePosition"));
            Assert.False(positions.ContainsKey("CustomPos"));
        }

        [Fact]
        public void MergeStationParameter_工站内存参数较少时应保留文件Positions()
        {
            var recipe = new RecipeInfo();
            var existing = CreateStationNode(("StandbyPosition", 0), ("SafePosition", 0), ("CustomPos", 10));
            recipe.SetParameter("DispenserStation", JsonSerializer.SerializeToElement(existing));

            var incoming = CreateStationNode(("StandbyPosition", 0), ("SafePosition", 0));
            recipe.MergeStationParameter("DispenserStation", incoming, replacePositions: false);

            var merged = JsonNode.Parse(((JsonElement)recipe.Parameters["DispenserStation"]).GetRawText())!.AsObject();
            var positions = merged["Positions"]!.AsObject();
            Assert.Equal(3, positions.Count);
            Assert.True(positions.ContainsKey("CustomPos"));
        }
    }
}

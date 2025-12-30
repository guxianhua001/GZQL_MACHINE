using Core.Abstraction;
using Core.Models;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;

namespace Core.Services
{
    public class JsonTreeConfigService : ITreeConfigService
    {
        private readonly string _configFilePath;

        public JsonTreeConfigService() : this(GetDefaultConfigPath())
        {
        }

        public JsonTreeConfigService(string configFilePath)
        {
            _configFilePath = configFilePath ?? GetDefaultConfigPath();
        }

        private static string GetDefaultConfigPath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
            return Path.Combine(baseDir, "Config", "tree-structure.json");
        }

        public async Task<List<TreeNode>> LoadTreeStructureAsync()
        {
            if (!File.Exists(_configFilePath))
            {
                var defaultStructure = CreateDefaultTreeStructure();
                // 自动创建默认配置文件
                await SaveTreeStructureAsync(defaultStructure);
                return defaultStructure;
            }

            try
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                return JsonConvert.DeserializeObject<List<TreeNode>>(json, new JsonSerializerSettings
                {
                    // 处理类型转换
                    Converters = { new TreeNodeCollectionConverter() }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载树配置失败: {ex.Message}");
                return CreateDefaultTreeStructure();
            }
        }

        public async Task SaveTreeStructureAsync(List<TreeNode> nodes)
        {
            try
            {
                var directory = Path.GetDirectoryName(_configFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonConvert.SerializeObject(nodes, Formatting.Indented, new JsonSerializerSettings
                {
                    Converters = { new TreeNodeCollectionConverter() },
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

                await File.WriteAllTextAsync(_configFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存树配置失败: {ex.Message}");
                throw;
            }
        }

        public TreeNode FindNodeByPath(List<TreeNode> nodes, string path)
        {
            if (nodes == null) return null;

            foreach (var node in nodes)
            {
                if (node.Path == path)
                    return node;

                var childrenList = node.Children as List<TreeNode> ?? node.Children?.ToList();
                var foundInChildren = FindNodeByPath(childrenList, path);
                if (foundInChildren != null)
                    return foundInChildren;
            }
            return null;
        }

        private List<TreeNode> CreateDefaultTreeStructure()
        {
            var root = new TreeNode
            {
                Name = "设备配置",
                Path = "Equipment",
                Icon = "Settings",
                Children = new ObservableCollection<TreeNode>()
            };

            var mainLine = new TreeNode
            {
                Name = "主流线",
                Path = "Equipment/MainLine",
                Icon = "VectorLine",
                Children = new ObservableCollection<TreeNode>()
            };

            mainLine.Children.Add(new TreeNode { Name = "手动调试", Path = "Equipment/MainLine/ManualTest", ViewType = "LoaderStationView", Icon = "Play" });
            mainLine.Children.Add(new TreeNode { Name = "轴调试", Path = "Equipment/MainLine/Axis", ViewType = "LoaderStationAxesView", Icon = "Axis" });
            mainLine.Children.Add(new TreeNode { Name = "气缸调试", Path = "Equipment/MainLine/Cylinder", ViewType = "LoaderStationCylinderView", Icon = "Piston" });
            mainLine.Children.Add(new TreeNode { Name = "位置参数", Path = "Equipment/MainLine/Param", ViewType = "LoaderStationPositionView", Icon = "MapMarker" });

            root.Children.Add(mainLine);

            return new List<TreeNode> { root };
        }
    }

    // JSON 转换器用于处理 TreeNode 集合
    public class TreeNodeCollectionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ObservableCollection<TreeNode>) ||
                   objectType == typeof(IList<TreeNode>);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return serializer.Deserialize<List<TreeNode>>(reader) is List<TreeNode> list
                ? new ObservableCollection<TreeNode>(list)
                : new ObservableCollection<TreeNode>();
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var collection = value as IList<TreeNode> ?? new List<TreeNode>();
            serializer.Serialize(writer, collection.ToList());
        }
    }
}
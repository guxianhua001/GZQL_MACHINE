using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Core.Abstraction;
using MotionControl.Interfaces;
using MotionControl.Models;

namespace MotionControl.Services
{
    public class HardwareConfigParser : IHardwareConfigLoader
    {
        private readonly string _filePath;

        public HardwareConfigParser(IAppSettingService appSettings = null)  // 可选
        {
            _filePath = appSettings?.GetValue("HardwareConfigPath")
                        ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "HWConfig", "hwcfg.xml");
        }

        public MotionSystemConfig Load()
        {
            var doc = XDocument.Load(_filePath);
            var root = doc.Element("MotionSystem")?.Element("MotionState");
            if (root == null) throw new Exception("Invalid config");

            var config = new MotionSystemConfig();

            foreach (var el in root.Elements("MotionCard"))
                config.Cards.Add(new CardConfig
                {
                    Id = (int)el.Attribute("actCardId"),
                    Name = (string)el.Attribute("name"),
                    Type = (string)el.Attribute("XCommandCard"),
                    ConfigPath = (string)el.Attribute("path")
                });

            foreach (var el in root.Descendants("Axes").Elements("Axis"))
                config.Axes.Add(new AxisConfig
                {
                    CardId = (int)el.Attribute("setCardId"),
                    AxisId = (int)el.Attribute("actAxisId"),
                    LogicalId = (int)el.Attribute("setAxisId"),
                    Name = (string)el.Attribute("name"),
                    TaskId = (int)el.Attribute("taskId"),
                    Direction = (string)el.Attribute("axisDirection")
                });

            foreach (var el in root.Descendants("Inputs").Elements("Port"))
                config.Inputs.Add(new IoConfig
                {
                    CardId = (int)el.Attribute("setCardId"),
                    Port = (int)el.Attribute("channel"),
                    LogicalId = (int)el.Attribute("actDiId"),
                    Name = (string)el.Attribute("name"),
                    IsInput = true
                });

            foreach (var el in root.Descendants("Outputs").Elements("Port"))
                config.Outputs.Add(new IoConfig
                {
                    CardId = (int)el.Attribute("setCardId"),
                    Port = (int)el.Attribute("channel"),
                    LogicalId = (int)el.Attribute("actDoId"),
                    Name = (string)el.Attribute("name"),
                    IsInput = false
                });

            foreach (var el in root.Descendants("Task").Elements("task"))
                config.Tasks.Add(new TaskConfig
                {
                    TaskId = (int)el.Attribute("taskId"),
                    Name = (string)el.Attribute("name"),
                    Type = (string)el.Attribute("type"),
                    StationId = (int)el.Attribute("stationId")
                });

            // 解析 SignalGroups
            foreach (var group in root.Descendants("SignalGroups").Elements("Group"))
            {
                string groupName = (string)group.Attribute("name");
                foreach (var sig in group.Elements("Signal"))
                {
                    config.Signals.Add(new SignalConfig
                    {
                        Name = (string)sig.Attribute("name"),
                        LogicalId = ParseInt(sig.Attribute("io")?.Value),
                        Polarity = (string)sig.Attribute("polarity") ?? "LowActive",
                        Type = (string)sig.Attribute("type") ?? "Momentary",
                        Group = groupName
                    });
                }
            }

            // 解析 OutputGroups
            foreach (var group in root.Descendants("OutputGroups").Elements("Group"))
            {
                string groupName = (string)group.Attribute("name");
                foreach (var os in group.Elements("Output"))
                {
                    config.OutputSignals.Add(new OutputSignalConfig
                    {
                        Name = (string)os.Attribute("name"),
                        LogicalId = ParseInt(os.Attribute("io")?.Value),
                        Polarity = (string)os.Attribute("polarity") ?? "LowActive",
                        Group = groupName
                    });
                }
            }

            // 解析 TowerLights
            foreach (var light in root.Descendants("TowerLights").Elements("Light"))
            {
                config.Lights.Add(new LightConfig
                {
                    LightType = (string)light.Attribute("type"),
                    LogicalId = ParseInt(light.Attribute("io")?.Value),
                    StationId = ParseInt(light.Attribute("stationId")?.Value)
                });
            }
            return config;
        }

        // 辅助方法，尝试将字符串转换为 int?
        private static int? ParseInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return int.TryParse(s, out int val) ? val : null;
        }
    }
}
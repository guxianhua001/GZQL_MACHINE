﻿using System;
using System.Collections.Generic;
using System.Globalization;
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

            // 解析 <!-->轴卡配置文件<--> 节点下的 <Config path="..."/> 默认配置文件
            foreach (var el in root.Elements("Config"))
            {
                var defaultPath = (string)el.Attribute("path");
                if (!string.IsNullOrWhiteSpace(defaultPath))
                    config.DefaultCardConfigPaths.Add(ResolveCardConfigPath(defaultPath));
            }

            var cardOrdinal = 0;
            foreach (var el in root.Elements("MotionCard"))
            {
                var cardPath = (string)el.Attribute("path");
                var cardIndex = el.Attribute("index") != null
                    ? (ushort)(int)el.Attribute("index")
                    : (ushort)cardOrdinal;

                config.Cards.Add(new CardConfig
                {
                    Index = cardIndex,
                    Id = (int)el.Attribute("actCardId"),
                    Name = (string)el.Attribute("name"),
                    Type = (string)el.Attribute("XCommandCard"),
                    ConfigPath = ResolveCardConfigPath(cardPath, cardIndex, config.DefaultCardConfigPaths)
                });
                cardOrdinal++;
            }

            foreach (var el in root.Descendants("Axes").Elements("Axis"))
                config.Axes.Add(new AxisConfig
                {
                    CardId = (int)el.Attribute("setCardId"),
                    AxisId = (int)el.Attribute("actAxisId"),
                    LogicalId = (int)el.Attribute("setAxisId"),
                    Name = (string)el.Attribute("name"),
                    TaskId = (int)el.Attribute("taskId"),
                    Direction = (string)el.Attribute("axisDirection"),
                    SkipHomeCheck = (bool?)el.Attribute("skipHome") ?? false,
                    HiddenInEditor = (bool?)el.Attribute("hiddenInEditor") ?? false
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
                        Polarity = NormalizePolarity((string)sig.Attribute("polarity") ?? "LowActive"),
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
                        Polarity = NormalizePolarity((string)os.Attribute("polarity") ?? "LowActive"),
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

            // 解析 AnalogInputs（AD 模拟量输入通道配置）
            var analogInputsEl = root.Element("AnalogInputs");
            if (analogInputsEl != null)
            {
                foreach (var ch in analogInputsEl.Elements("ADChannel"))
                {
                    config.AnalogInputs.Add(new ADChannelConfig
                    {
                        Channel = ParseIntAttr(ch, "channel", 0),
                        Name = (string)ch.Attribute("name") ?? string.Empty,
                        MinADValue = ParseDoubleAttr(ch, "minADValue", -32767),
                        MaxADValue = ParseDoubleAttr(ch, "maxADValue", 32767),
                        MinPhysicalValue = ParseDoubleAttr(ch, "minPhysical", 0),
                        MaxPhysicalValue = ParseDoubleAttr(ch, "maxPhysical", 10),
                        Unit = (string)ch.Attribute("unit") ?? "N",
                        CalibrationFactor = ParseDoubleAttr(ch, "calibration", 1.0),
                        ZeroOffset = ParseDoubleAttr(ch, "zeroOffset", 0.0),
                        IsEnabled = ParseBoolAttr(ch, "isEnabled", true)
                    });
                }
            }
            return config;
        }

        // 辅助方法，尝试将字符串转换为 int?
        private static int? ParseInt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return int.TryParse(s, out int val) ? val : null;
        }

        /// <summary>
        /// 规范化极性字符串，统一别名到标准值：
        /// "ActiveHigh"/"HighActive" → "HighActive"；"ActiveLow"/"LowActive" → "LowActive"。
        /// 防止配置中 ActiveHigh 等别名与代码 == "HighActive" 精确比较不匹配，
        /// 导致信号被静默误判为 LowActive（IsSignalActive 取反逻辑出错）。
        /// </summary>
        private static string NormalizePolarity(string polarity)
        {
            if (string.IsNullOrEmpty(polarity)) return "LowActive";
            var p = polarity.Trim();
            if (p.Equals("ActiveHigh", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("HighActive", StringComparison.OrdinalIgnoreCase))
                return "HighActive";
            if (p.Equals("ActiveLow", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("LowActive", StringComparison.OrdinalIgnoreCase))
                return "LowActive";
            return p; // 未知值原样返回，保留扩展性
        }

        /// <summary> 解析整数属性，缺省返回 defaultValue </summary>
        private static int ParseIntAttr(XElement el, string attr, int defaultValue)
        {
            var a = el.Attribute(attr);
            return a != null && int.TryParse(a.Value, out int v) ? v : defaultValue;
        }

        /// <summary> 解析浮点属性，缺省返回 defaultValue </summary>
        private static double ParseDoubleAttr(XElement el, string attr, double defaultValue)
        {
            var a = el.Attribute(attr);
            return a != null && double.TryParse(a.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : defaultValue;
        }

        /// <summary> 解析布尔属性，缺省返回 defaultValue </summary>
        private static bool ParseBoolAttr(XElement el, string attr, bool defaultValue)
        {
            var a = el.Attribute(attr);
            return a != null && bool.TryParse(a.Value, out bool v) ? v : defaultValue;
        }

        /// <summary>
        /// 解析轴卡配置文件路径；MotionCard.path 为空时按卡序号回退到 DefaultCardConfigPaths
        /// </summary>
        private static string ResolveCardConfigPath(string path, int cardIndex, IList<string> defaultPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
                return ResolveCardConfigPath(path);

            if (defaultPaths != null && cardIndex >= 0 && cardIndex < defaultPaths.Count)
                return defaultPaths[cardIndex];

            return string.Empty;
        }

        /// <summary>
        /// 将 hwcfg 中的相对路径（如 \Devices\config1.ini）解析为绝对路径
        /// </summary>
        private static string ResolveCardConfigPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            if (Path.IsPathRooted(path) && path.Length > 1 && path[1] == ':')
                return Path.GetFullPath(path);

            var relativePath = path.TrimStart('\\', '/');
            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath));
        }
    }
}
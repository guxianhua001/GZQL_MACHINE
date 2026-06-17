﻿using System.Collections.Generic;
using Core.Abstraction;

namespace MotionControl.Models
{
    public class MotionSystemConfig
    {
        /// <summary> 轴卡配置文件节点下的默认配置文件路径列表（按卡序号 0、1… 对应） </summary>
        public List<string> DefaultCardConfigPaths { get; set; } = new List<string>();
        public List<CardConfig> Cards { get; set; } = new List<CardConfig>();
        public List<AxisConfig> Axes { get; set; } = new List<AxisConfig>();
        public List<IoConfig> Inputs { get; set; } = new List<IoConfig>();
        public List<IoConfig> Outputs { get; set; } = new List<IoConfig>();
        public List<TaskConfig> Tasks { get; set; } = new List<TaskConfig>();
        public List<SignalConfig> Signals { get; set; } = new();
        public List<OutputSignalConfig> OutputSignals { get; set; } = new();
        public List<LightConfig> Lights { get; set; } = new();

        /// <summary> 模拟量输入通道配置（来自 hwcfg.xml AnalogInputs 节点） </summary>
        public List<ADChannelConfig> AnalogInputs { get; set; } = new();
    }
    public class CardConfig
    {
        public ushort Index { get; set; }      // 对应初始化后 cardIds 数组的索引
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }    // "Leisai"
        public string ConfigPath { get; set; }
    }
    public class AxisConfig
    {
        public int CardId { get; set; }
        public int AxisId { get; set; }        // 物理轴号
        public int LogicalId { get; set; }     // 逻辑轴号
        public string Name { get; set; }
        public int TaskId { get; set; }
        public string Direction { get; set; }

        /// <summary>是否跳过回零检查（龙门从轴等不需要独立回零的轴）</summary>
        public bool SkipHomeCheck { get; set; }

        /// <summary>是否在位置编辑器中隐藏（龙门从轴等跟随主轴运动的轴）</summary>
        public bool HiddenInEditor { get; set; }
    }
    public class IoConfig
    {
        public int CardId { get; set; }
        public int Port { get; set; }          // 物理通道
        public int LogicalId { get; set; }
        public string Name { get; set; }
        public bool IsInput { get; set; }
    }
    public class SignalConfig
    {
        public string Name { get; set; }
        public int? LogicalId { get; set; }         // DI 逻辑号
        public string Polarity { get; set; }        // "LowActive" 或 "HighActive"
        public string Type { get; set; }            // "Momentary"(点动) 或 "Maintained"(保持)
        public string Group { get; set; }           // 所属组名
    }

    public class OutputSignalConfig
    {
        public string Name { get; set; }
        public int? LogicalId { get; set; }
        public string Polarity { get; set; }
        public string Group { get; set; }
    }
    public class LightConfig
    {
        public string LightType { get; set; }            // "Orange", "Green", "Red", "Buzzer"
        public int? LogicalId { get; set; }
        public int? StationId { get; set; }
    }

    public class TaskConfig
    {
        public int TaskId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }       // LoadingStation, DispensingStation ...
        public int StationId { get; set; }
    }
}
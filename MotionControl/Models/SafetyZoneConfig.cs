using Prism.Mvvm;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace MotionControl.Models
{
    /// <summary>
    /// 安全区域互锁配置（JSON 持久化，支持多设备不同规则集）
    /// </summary>
    public class SafetyZoneConfig : BindableBase
    {
        /// <summary>配置架构版本，用于迁移旧版扁平字段</summary>
        public int SchemaVersion { get; set; } = 2;

        private bool _enabled = true;

        [Category("全局设置")]
        [DisplayName("启用安全互锁")]
        [Description("是否启用运动安全互锁功能，关闭后将跳过所有安全检查")]
        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        /// <summary>轴名无法解析或高度轴缺失时是否拒绝运动（fail-closed）</summary>
        public bool FailClosedOnMissingAxis { get; set; } = true;

        /// <summary>Jog 启动前估算位移（mm），用于 SafeJogBehavior 安全检查</summary>
        public double JogEstimateOffset { get; set; } = 10.0;

        /// <summary>互锁规则列表，不同设备可配置不同规则组合</summary>
        public List<SafetyInterlockRuleConfig> Rules { get; set; } = new();

        /// <summary>各轴危险区边界（状态显示/可视化，可选）</summary>
        public List<AxisDangerZoneConfig> DangerZones { get; set; } = new();

        /// <summary>画布X轴行程范围（可视化显示用，用户可设置）</summary>
        public AxisDangerZoneConfig CanvasRangeX { get; set; }

        /// <summary>画布Y轴行程范围（可视化显示用，用户可设置）</summary>
        public AxisDangerZoneConfig CanvasRangeY { get; set; }

        #region 旧版字段（仅用于 JSON 迁移，新配置请使用 Rules / DangerZones）

        public double? SafeHeightZ1 { get; set; }
        public double? DangerZoneXMin { get; set; }
        public double? DangerZoneXMax { get; set; }
        public double? DangerZoneYMin { get; set; }
        public double? DangerZoneYMax { get; set; }

        #endregion

        /// <summary>
        /// 当前机型默认配置：Dz₁/Dz₂/Dz₃ 任一未达安全高度时禁止 Dx/Dy 移动
        /// </summary>
        public static SafetyZoneConfig CreateDefaultForCurrentMachine()
        {
            return new SafetyZoneConfig
            {
                SchemaVersion = 2,
                Enabled = true,
                FailClosedOnMissingAxis = true,
                JogEstimateOffset = 10.0,
                Rules = new List<SafetyInterlockRuleConfig>
                {
                    new()
                    {
                        Id = "HeightLockPlane_XY",
                        Type = SafetyInterlockRuleType.HeightLockPlane,
                        Enabled = true,
                        MessageKey = "SafetyRule_HeightLockPlane",
                        HeightAxes = new List<HeightAxisSafeConfig>
                        {
                            new() { AxisName = "Dz₁", SafeHeight = 50.0 },
                            new() { AxisName = "Dz₂", SafeHeight = 50.0 },
                            new() { AxisName = "Dz₃", SafeHeight = 50.0 }
                        },
                        LockedAxes = new List<string> { "Dx", "Dy" }
                    }
                },
                DangerZones = new List<AxisDangerZoneConfig>
                {
                    new() { AxisName = "Dx", Min = 0, Max = 200 },
                    new() { AxisName = "Dy", Min = 0, Max = 200 }
                }
            };
        }

        /// <summary>获取主高度锁平面规则（UI 编辑 Z 阈值时使用）</summary>
        public SafetyInterlockRuleConfig GetOrCreateHeightLockPlaneRule()
        {
            var rule = Rules.FirstOrDefault(r => r.Type == SafetyInterlockRuleType.HeightLockPlane);
            if (rule != null)
                return rule;

            rule = new SafetyInterlockRuleConfig
            {
                Id = "HeightLockPlane_XY",
                Type = SafetyInterlockRuleType.HeightLockPlane,
                Enabled = true,
                MessageKey = "SafetyRule_HeightLockPlane",
                LockedAxes = new List<string> { "Dx", "Dy" }
            };
            Rules.Add(rule);
            return rule;
        }

        /// <summary>读取指定高度轴的安全高度，未配置时返回默认值</summary>
        public double GetSafeHeightForAxis(string axisName, double defaultValue = 50.0)
        {
            var rule = Rules.FirstOrDefault(r => r.Type == SafetyInterlockRuleType.HeightLockPlane);
            var entry = rule?.HeightAxes?.FirstOrDefault(h =>
                string.Equals(h.AxisName, axisName, System.StringComparison.Ordinal));
            return entry?.SafeHeight ?? defaultValue;
        }

        /// <summary>设置指定高度轴的安全高度</summary>
        public void SetSafeHeightForAxis(string axisName, double safeHeight)
        {
            var rule = GetOrCreateHeightLockPlaneRule();
            var entry = rule.HeightAxes.FirstOrDefault(h =>
                string.Equals(h.AxisName, axisName, System.StringComparison.Ordinal));
            if (entry == null)
            {
                entry = new HeightAxisSafeConfig { AxisName = axisName };
                rule.HeightAxes.Add(entry);
            }
            entry.SafeHeight = safeHeight;
        }

        /// <summary>设置指定高度轴的方向反转标志</summary>
        public void SetInvertedDirectionForAxis(string axisName, bool inverted)
        {
            var rule = GetOrCreateHeightLockPlaneRule();
            var entry = rule.HeightAxes.FirstOrDefault(h =>
                string.Equals(h.AxisName, axisName, System.StringComparison.Ordinal));
            if (entry == null)
            {
                entry = new HeightAxisSafeConfig { AxisName = axisName };
                rule.HeightAxes.Add(entry);
            }
            entry.InvertedDirection = inverted;
        }

        /// <summary>读取指定高度轴的方向反转标志</summary>
        public bool GetInvertedDirectionForAxis(string axisName, bool defaultValue = false)
        {
            var rule = Rules.FirstOrDefault(r => r.Type == SafetyInterlockRuleType.HeightLockPlane);
            var entry = rule?.HeightAxes?.FirstOrDefault(h =>
                string.Equals(h.AxisName, axisName, System.StringComparison.Ordinal));
            return entry?.InvertedDirection ?? defaultValue;
        }

        public SafetyZoneConfig Clone()
        {
            return new SafetyZoneConfig
            {
                SchemaVersion = SchemaVersion,
                Enabled = Enabled,
                FailClosedOnMissingAxis = FailClosedOnMissingAxis,
                JogEstimateOffset = JogEstimateOffset,
                Rules = Rules.Select(CloneRule).ToList(),
                DangerZones = DangerZones.Select(z => new AxisDangerZoneConfig
                {
                    AxisName = z.AxisName,
                    Min = z.Min,
                    Max = z.Max
                }).ToList(),
                CanvasRangeX = CanvasRangeX != null ? new AxisDangerZoneConfig
                {
                    AxisName = CanvasRangeX.AxisName,
                    Min = CanvasRangeX.Min,
                    Max = CanvasRangeX.Max
                } : null,
                CanvasRangeY = CanvasRangeY != null ? new AxisDangerZoneConfig
                {
                    AxisName = CanvasRangeY.AxisName,
                    Min = CanvasRangeY.Min,
                    Max = CanvasRangeY.Max
                } : null
            };
        }

        private static SafetyInterlockRuleConfig CloneRule(SafetyInterlockRuleConfig r) => new()
        {
            Id = r.Id,
            Type = r.Type,
            Enabled = r.Enabled,
            MessageKey = r.MessageKey,
            HeightAxes = r.HeightAxes?.Select(h => new HeightAxisSafeConfig
            {
                AxisName = h.AxisName,
                SafeHeight = h.SafeHeight,
                InvertedDirection = h.InvertedDirection
            }).ToList() ?? new List<HeightAxisSafeConfig>(),
            LockedAxes = r.LockedAxes?.ToList() ?? new List<string>()
        };
    }
}

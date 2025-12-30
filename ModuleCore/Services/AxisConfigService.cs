// Services/AxisConfigService.cs
using AxisConfiguration.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmarterMotion;
using System.Xml.Linq;
using Interfaces;
using System.Windows;
using System.Windows.Media.Animation;

namespace AxisConfiguration.Services
{
    public class AxisConfigService : IAxisConfigService
    {
        public IEnumerable<AxisInfo> LoadAllAxes()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "HWConfig", "hwcfg.xml");
            if (!File.Exists(configPath))
                throw new FileNotFoundException("配置文件不存在", configPath);

            XDocument doc = XDocument.Load(configPath);
            int cardId = 0;
            var motionCard = doc.Descendants("MotionCard").FirstOrDefault();
            if (motionCard?.Attribute("actCardId") != null)
                cardId = Convert.ToInt32(motionCard.Attribute("actCardId").Value);

            return doc.Descendants("Axis").Select(axis => new AxisInfo(cardId,
                Convert.ToInt32(axis.Attribute("setAxisId")?.Value ?? "0"),
                axis.Attribute("name")?.Value ?? "未命名轴")
            {
                Description = axis.Attribute("axisDirection")?.Value ?? "方向未知",
                Params = LoadAxisParameters(cardId, Convert.ToInt32(axis.Attribute("setAxisId")?.Value ?? "0"))
            });
        }

        public AxisParams LoadAxisParameters(int cardId, int axisId)
        {
            try
            {
                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "AxisSettings");
                string configPath = Path.Combine(configDir, $"Axis_{cardId}_{axisId}.json");

                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var settings = JsonConvert.DeserializeObject<AxisParams>(json);

                    // 确保类型正确
                    if (settings.EmergencyStop?.MappedIO != null)
                    {
                        settings.EmergencyStop.MappedIO.IoType = 3;
                        settings.EmergencyStop.MappedIO.MapIoType = 6;
                    }
                    return settings;
                }
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"加载轴{cardId}-{axisId}配置失败", ex);
            }
            return new AxisParams(); // 默认参数
        }

        public void SaveAxisParameters(AxisInfo axis)
        {
            if (axis == null) return;

            try
            {
                // 强制设置IO类型
                var mappedIO = axis.Params.EmergencyStop?.MappedIO;
                if (mappedIO != null)
                {
                    mappedIO.IoType = 3;
                    mappedIO.MapIoType = 6;
                }
                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "AxisSettings");
                Directory.CreateDirectory(configDir);
                string configPath = Path.Combine(configDir, $"Axis_{axis.CardId}_{axis.AxisId}.json");

                string json = JsonConvert.SerializeObject(axis.Params, Formatting.Indented);
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"保存轴{axis.CardId}-{axis.AxisId}配置失败", ex);
            }
        }

        public async Task DownloadAllParametersAsync(IProgressReporter progressReporter = null)
        {
            var axes = LoadAllAxes().ToList();

            if (!axes.Any())
            {
                progressReporter?.Report(1.0, "没有需要设置的轴");
                IMessage.Logger.Warn("没有需要设置的轴");
                return;
            }

            bool allSuccess = true;
            var errors = new StringBuilder();
            int total = axes.Count;

            for (int i = 0; i < total; i++)
            {
                var axis = axes[i];
                string status = $"设置轴 [{axis.Name}] ({i + 1}/{total})";

                try
                {
                    progressReporter?.Report((double)i / total, $"{status}...");
                    await DownloadSingleAxisAsync(axis);
                    progressReporter?.Report((double)(i + 1) / total, $"{status} 成功");
                    IMessage.Logger.Info($"轴{axis.Name} 参数设置成功");
                }
                catch (Exception ex)
                {
                    allSuccess = false;
                    errors.AppendLine($"设置轴{axis.Name}失败: {ex.Message}");
                    progressReporter?.Report((double)(i + 1) / total, $"{status} 失败");
                    IMessage.Logger.Error($"设置轴{axis.Name}失败", ex);
                }
            }

            if (!allSuccess)
                throw new ApplicationException("部分轴设置失败:\n" + errors);
        }

        public Task DownloadSingleAxisAsync(AxisInfo axis)
        {
            return Task.Run(() =>
            {
                // 设置脉冲当量
                LTDMC.dmc_set_equiv((ushort)axis.CardId, (ushort)axis.AxisId, axis.Params.PulsePerUnit);

                // 设置急停 - 使用安全调用方法
                SetEmergencyStop(axis);

                // 设置回零参数
                SetHomingParameters(axis);

                // 设置S曲线
                LTDMC.dmc_set_s_profile((ushort)axis.CardId, (ushort)axis.AxisId, 0,
                    axis.Params.Motion.SProfileTime);

                // 设置运动参数
                SetMotionParameters(axis);
            });
        }

        private void SetEmergencyStop(AxisInfo axis)
        {
            var emergency = axis.Params.EmergencyStop;
            if (emergency == null)
            {
                IMessage.Logger.Warn($"轴{axis.Name} 紧急停止配置为空，使用默认配置");
                emergency = new EmergencyStopConfig();
            }

            // 启用状态和逻辑电平
            LTDMC.dmc_set_emg_mode(
                (ushort)axis.CardId,
                (ushort)axis.AxisId,
                emergency.Enabled ? (ushort)1 : (ushort)0,
                (ushort)(emergency.LogicLevel == LogicLevel.High ? 1 : 0)
            );

            // 确保映射IO不为空
            var io = emergency.MappedIO ?? new MappedIO();
            io.IoType = 3; // 紧急停止类型
            io.MapIoType = 6; // 通用输入端口

            // 设置IO映射
            if (io.MapIoType > 0)
            {
                LTDMC.dmc_set_axis_io_map(
                    (ushort)axis.CardId,
                    (ushort)axis.AxisId,
                    (ushort)io.IoType,
                    (ushort)io.MapIoType,
                    (ushort)io.MapIoIndex,
                    io.FilterTime
                );
            }
        }

        private void SetHomingParameters(AxisInfo axis)
        {
            var homing = axis.Params.Homing;
            if (homing == null)
            {
                IMessage.Logger.Warn($"轴{axis.Name} 回零配置为空，使用默认配置");
                homing = new HomingConfig();
            }

            LTDMC.nmc_set_home_profile(
                (ushort)axis.CardId,
                (ushort)axis.AxisId,
                (ushort)homing.Mode,
                homing.LowSpeed,
                homing.HighSpeed,
                homing.AccelerationTime,
                homing.DecelerationTime,
                homing.Offset
            );
        }

        private void SetMotionParameters(AxisInfo axis)
        {
            var motion = axis.Params.Motion;
            if (motion == null)
            {
                IMessage.Logger.Warn($"轴{axis.Name} 运动配置为空，使用默认配置");
                motion = new MotionConfig();
            }

            LTDMC.dmc_set_profile_unit(
                (ushort)axis.CardId,
                (ushort)axis.AxisId,
                motion.StartSpeed,
                motion.MaxSpeed,
                motion.AccelerationTime,
                motion.DecelerationTime,
                motion.StopSpeed
            );

            // 设置减速停止时间
            if (motion.DecStopTime >= 0)
            {
                LTDMC.dmc_set_dec_stop_time(
                    (ushort)axis.CardId,
                    (ushort)axis.AxisId,
                    (ushort)(motion.DecStopTime)
                );
            }
        }
 
        public void UploadParameters(AxisInfo axis)
        {
            // 实际读取逻辑需硬件支持
        }

        #region 插补系配置
        public IEnumerable<InterpolationSystem> LoadInterpolationSystems()
        {
            // 默认情况下创建6个插补系
            var defaultSystems = new List<InterpolationSystem>();
            for (int i = 0; i < 6; i++)
            {
                defaultSystems.Add(new InterpolationSystem
                {
                    CoordId = i,
                    ActCardId = 0, // 默认卡号
                    Axes = new List<string>(),
                    Params = new InterpolationParams()
                });
            }
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "HWConfig", "hwcfg.xml");
            if (!File.Exists(configPath))
                return defaultSystems; // 返回默认值
            XDocument doc = XDocument.Load(configPath);
            var systems = new List<InterpolationSystem>();
            var root = doc.Root; // <MotionSystem>
            var motionState = root?.Element("MotionState");
            var axisConfig = motionState?.Element("AxisConfig");
            var interpolationSystemsElement = axisConfig?.Element("InterpolationSystems");

            if (interpolationSystemsElement == null)
                return defaultSystems;
            foreach (var systemElement in interpolationSystemsElement.Elements("System"))
            {
                try
                {
                    var actCardIdAttr = systemElement.Attribute("actCardId");
                    var coordIdAttr = systemElement.Attribute("coordId");
                    var axesAttr = systemElement.Attribute("axes");
                    // 确保属性存在
                    if (actCardIdAttr == null || coordIdAttr == null || axesAttr == null)
                    {
                        IMessage.Logger.Warn("插补系配置缺少必要属性，跳过该配置");
                        continue;
                    }
                    var system = new InterpolationSystem
                    {
                        ActCardId = Convert.ToInt32(actCardIdAttr.Value),
                        CoordId = Convert.ToInt32(coordIdAttr.Value),
                        Axes = ParseAxisString(axesAttr.Value ?? "")
                    };
                    // 尝试加载保存的插补参数
                    LoadInterpolationSystem(system);
                    systems.Add(system);
                }
                catch (Exception ex)
                {
                    IMessage.Logger.Error($"加载插补系配置失败: {ex.Message}");
                }
            }
            // 合并默认系统和加载的系统配置
            foreach (var defaultSystem in defaultSystems)
            {
                // 如果XML中已有该ID的系统，跳过默认配置
                if (!systems.Any(s => s.CoordId == defaultSystem.CoordId))
                {
                    systems.Add(defaultSystem);
                }
            }
            return systems.OrderBy(s => s.CoordId).ToList();
        }
        private List<string> ParseAxisString(string axisString)
        {
            if (string.IsNullOrWhiteSpace(axisString))
                return new List<string>();
            return axisString.Split(',')
                            .Where(part => !string.IsNullOrWhiteSpace(part))
                            .ToList();
        }
        public void LoadInterpolationSystem(InterpolationSystem system)
        {
            if (system == null) return;

            try
            {
                // 构建预期的文件路径
                string configPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Config",
                    "Systems",
                    $"System_{system.ActCardId}_{system.CoordId}.json");

                if (!File.Exists(configPath))
                {
                    IMessage.Logger.Debug($"未找到插补系配置: {configPath}");
                    return;
                }

                // 读取和解析JSON
                string json = File.ReadAllText(configPath);
                var systemData = JsonConvert.DeserializeObject<InterpolationSystemData>(json);

                if (systemData != null)
                {
                    // 验证文件内容是否匹配当前系统
                    if (systemData.ActCardId != system.ActCardId ||
                        systemData.CoordId != system.CoordId)
                    {
                        IMessage.Logger.Warn($"配置文件不匹配:" +
                                            $"文件内容(卡{systemData.ActCardId}, 系{systemData.CoordId})" +
                                            $"与系统(卡{system.ActCardId}, 系{system.CoordId})不一致");
                        return;
                    }

                    // 更新当前系统的配置
                    system.Axes = systemData.Axes ?? new List<string>();
                    system.Params = systemData.Params ?? new InterpolationParams();

                    IMessage.Logger.Info($"已加载插补系配置: 卡{system.ActCardId} 系{system.CoordId}" +
                                        $" ({system.Axes.Count}个轴)");
                }
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"加载插补系配置失败: {ex.Message}");
            }
        }

        // 支持类，用于反序列化
        private class InterpolationSystemData
        {
            public int ActCardId { get; set; }
            public int CoordId { get; set; }
            public List<string> Axes { get; set; }
            public InterpolationParams Params { get; set; }
        }

        public void SaveInterpolationSystem(InterpolationSystem system)
        {
            if (system == null) return;

            try
            {
                // 确保配置目录存在
                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Systems");
                Directory.CreateDirectory(configDir);

                // 创建唯一的文件路径
                string configPath = Path.Combine(configDir, $"System_{system.ActCardId}_{system.CoordId}.json");

                // 准备保存的数据对象
                var systemData = new
                {
                    system.ActCardId,
                    system.CoordId,
                    Axes = system.Axes,                         // 包含的轴列表
                    system.Params                                // 运动参数配置
                };

                // 序列化为JSON并保存
                string json = JsonConvert.SerializeObject(systemData, Formatting.Indented);
                File.WriteAllText(configPath, json);

                IMessage.Logger.Debug($"成功保存插补系配置: {configPath}");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"保存插补系配置失败: {ex.Message}");
                throw;
            }
        }

        public void ApplyInterpolationParameters(InterpolationSystem interpolationSystem)
        {
            if (interpolationSystem != null)
            {
                try
                {
                    // 获取参数
                    var p = interpolationSystem.Params;

                    // 设置插补运动速度曲线
                    LTDMC.dmc_set_vector_profile_unit(
                        (ushort)interpolationSystem.ActCardId,
                        (ushort)interpolationSystem.CoordId,
                        p.StartVelocity,
                        p.InterpolationVelocity,
                        p.AccelerationTime,
                        p.DecelerationTime,
                        p.EndVelocity
                    );

                    // 设置S段时间
                    LTDMC.dmc_set_vector_s_profile(
                        (ushort)interpolationSystem.ActCardId,
                        (ushort)interpolationSystem.CoordId,
                        0,
                        p.SProfileTime
                    );
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"应用参数失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        // 用于JSON序列化的设置类
        private class InterpolationSettings
        {
            public InterpolationParams Params { get; set; }
        }
        #endregion

        #region 获取轴速度
        /// <summary>
        /// 获取单轴的速度参数
        /// </summary>
        public double GetAxisSpeed(int cardId, int axisId)
        {
            var axis = GetAxisInfo(cardId, axisId);
            if (axis?.Params?.Motion != null)
            {
                return (axis.Params.Motion.MaxSpeed);
            }

            IMessage.Logger.Warn($"未找到轴({cardId},{axisId})的速度参数，使用默认值");
            return (10.0); // 默认速度
        }

        public AxisInfo GetAxisInfo(int cardId, int axisId)
        {
            return LoadAllAxes().FirstOrDefault(a =>
                a.CardId == cardId && a.AxisId == axisId) ?? new AxisInfo(cardId, axisId, $"默认轴-{axisId}");
        }
        /// <summary>
        /// 获取插补系的速度参数
        /// </summary>
        public double GetInterpolationSpeeds(int cardId, int coordId)
        {
            try
            {
                var system = LoadInterpolationSystems().FirstOrDefault(s =>
                    s.ActCardId == cardId && s.CoordId == coordId);

                if (system?.Params != null)
                {
                    return system.Params.InterpolationVelocity;
                }
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"获取插补系({cardId},{coordId})速度失败", ex);
            }

            // 默认值
            IMessage.Logger.Warn($"使用插补系({cardId},{coordId})的默认速度参数");
            return (10);
        }
        #endregion
    }
}

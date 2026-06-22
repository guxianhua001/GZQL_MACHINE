using Core.Models;
using Core.Services;
using MotionControl.Interfaces;
using MotionControl.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MotionControl.Services
{
    /// <summary>
    /// 轴配置服务：从 hwcfg.xml 动态获取工站-轴映射关系
    /// 通过 IMotionService 访问已解析的硬件配置数据
    /// </summary>
    public class AxisConfigurationService : IAxisConfigurationService
    {
        private readonly IMotionService _motionService;

        public AxisConfigurationService(IMotionService motionService)
        {
            _motionService = motionService;
        }

        /// <summary>
        /// 根据工站标识获取该工站的所有轴定义
        /// 通过 TaskConfig.Type 匹配 stationIdentifier 找到 TaskId，
        /// 再通过 AxisConfig.TaskId 筛选属于该工站的所有轴
        /// </summary>
        public IReadOnlyList<AxisDefinition> GetAxesForStation(string stationIdentifier)
        {
            var axisConfigs = _motionService.GetAxisConfigurations();
            var taskConfigs = _motionService.GetTaskConfigurations();

            // 通过 TaskConfig.Type 匹配 stationIdentifier 找到 TaskId
            var taskConfig = taskConfigs.FirstOrDefault(tc => tc.Type == stationIdentifier);
            if (taskConfig == null)
                return new List<AxisDefinition>();

            // 通过 AxisConfig.TaskId 筛选属于该工站的轴，排除 HiddenInEditor 的从轴
            return axisConfigs
                .Where(a => a.TaskId == taskConfig.TaskId && !a.HiddenInEditor)
                .OrderBy(a => a.LogicalId)
                .Select(a => new AxisDefinition
                {
                    Name = a.Name,
                    DisplayName = $"{a.Name} ({GuessUnit(a.Name)})",
                    Unit = GuessUnit(a.Name),
                    DefaultValue = 0,
                    IsRequired = true
                })
                .ToList();
        }

        /// <summary>
        /// 获取所有工站的全部轴定义（排除 HiddenInEditor 的从轴）
        /// 用于双龙门标定等需要跨工站选择轴的场景
        /// </summary>
        public IReadOnlyList<AxisDefinition> GetAllAxes()
        {
            var axisConfigs = _motionService.GetAxisConfigurations();
            return axisConfigs
                .Where(a => !a.HiddenInEditor)
                .OrderBy(a => a.LogicalId)
                .Select(a => new AxisDefinition
                {
                    Name = a.Name,
                    DisplayName = $"{a.Name} ({GuessUnit(a.Name)})",
                    Unit = GuessUnit(a.Name),
                    DefaultValue = 0,
                    IsRequired = true
                })
                .ToList();
        }

        /// <summary>
        /// 根据轴名称推断单位（旋转轴用度，线性轴用毫米）
        /// </summary>
        private static string GuessUnit(string axisName)
        {
            if (string.IsNullOrEmpty(axisName)) return "mm";
            // 以 R 开头或包含 Ry/Rx/Rz 的轴为旋转轴
            if (axisName.StartsWith("R", StringComparison.OrdinalIgnoreCase) ||
                axisName.Equals("Ey", StringComparison.OrdinalIgnoreCase) ||
                axisName.Equals("Cy", StringComparison.OrdinalIgnoreCase))
                return "°";
            return "mm";
        }
    }
}

using Core.Abstraction;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MotionControl.ViewModels
{
    /// <summary>
    /// 轴控制面板的主 ViewModel
    /// 负责从 hwcfg.xml 加载配置并按工站分组
    /// </summary>
    public class AxisControlPanelViewModel : BindableBase, IDisposable
    {
        private readonly IMotionService _motionService;
        private readonly ILocalizationService _localizationService;

        // 按工站分组的 Tab 列表
        public ObservableCollection<StationAxisViewModel> Stations { get; } = new();

        // 全局紧急停止命令
        public DelegateCommand GlobalEmergencyStopCommand { get; }

        public AxisControlPanelViewModel(IMotionService motionService, 
                                        ILocalizationService localizationService)
        {
            _motionService = motionService ?? throw new ArgumentNullException(nameof(motionService));
            _localizationService = localizationService;

            GlobalEmergencyStopCommand = new DelegateCommand(ExecuteGlobalEmergencyStop);

            // 加载配置并初始化工站
            InitializeStations();
        }

        /// <summary>
        /// 从 IMotionService 获取 hwcfg 配置并按 TaskId（任务/子系统）分组
        /// </summary>
        private void InitializeStations()
        {
            try
            {
                var taskConfigs = _motionService.GetTaskConfigurations();
                var axisConfigs = _motionService.GetAxisConfigurations();

                if (axisConfigs == null || !axisConfigs.Any())
                    return;

                if (taskConfigs == null || !taskConfigs.Any())
                {
                    CreateDefaultStation(axisConfigs);
                    return;
                }

                // 按 TaskId 分组（每个 Task 对应一个工站 Tab）
                var groupedByTask = axisConfigs
                    .Where(a => taskConfigs.Any(t => t.TaskId == a.TaskId))
                    .GroupBy(a => a.TaskId);

                foreach (var taskGroup in groupedByTask)
                {
                    int taskId = taskGroup.Key;
                    var taskConfig = taskConfigs.FirstOrDefault(t => t.TaskId == taskId);
                    string taskName = GetLocalizedStationName(taskConfig);
                    int stationId = taskConfig?.StationId ?? 0;

                    var stationVM = new StationAxisViewModel(
                        stationId, taskName, taskGroup,
                        _motionService, _localizationService);
                    Stations.Add(stationVM);
                }

                // 处理未分配到任何 Task 的轴
                var unassignedAxes = axisConfigs.Where(a =>
                    !taskConfigs.Any(t => t.TaskId == a.TaskId));

                if (unassignedAxes.Any())
                {
                    CreateDefaultStation(unassignedAxes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize stations: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据 TaskConfig.Type 获取本地化的工站名称
        /// 使用约定：资源键 = Station_{Type}，如 Station_LoadingStation
        /// 新增工站类型只需添加对应资源键，无需改动代码
        /// </summary>
        private string GetLocalizedStationName(TaskConfig taskConfig)
        {
            if (taskConfig == null)
                return "Unknown Station";

            string resourceKey = $"Station_{taskConfig.Type}";
            string localizedName = _localizationService?.GetResourceOrDefault(resourceKey, taskConfig.Name);
            if (!string.IsNullOrEmpty(localizedName))
                return localizedName;

            return taskConfig.Name ?? $"Task_{taskConfig.TaskId}";
        }

        /// <summary>
        /// 创建默认工站（用于未分组或无任务配置的情况）
        /// </summary>
        private void CreateDefaultStation(IEnumerable<AxisConfig> axes)
        {
            if (!axes?.Any() == true) return;

            string defaultName = _localizationService?.GetResourceOrDefault("DefaultStationName", "All Axes") ?? "All Axes";
            var defaultStation = new StationAxisViewModel(
                0, defaultName, axes, 
                _motionService, _localizationService);
            Stations.Add(defaultStation);
        }

        /// <summary>
        /// 全局紧急停止：停止所有工站的所有轴
        /// </summary>
        private void ExecuteGlobalEmergencyStop()
        {
            try
            {
                // 并发停止所有工站的所有轴
                foreach (var station in Stations)
                {
                    station.EmergencyStopCommand.Execute();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Global emergency stop error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            foreach (var station in Stations)
            {
                station.Dispose();
            }
            Stations.Clear();
        }
    }
}

// NeedleCalibrationService.cs
using Core.Abstraction;
using Core.Utilities;
using Prism.Events;
using Prism.Services.Dialogs;
using Stations.TaskParameters;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Stations.Service
{
    /// <summary>
    /// 针头校准参数服务 - 独立管理针头校准参数
    /// </summary>
    public class NeedleCalibrationService
    {
        private readonly IParameterStorage _parameterStorage;
        private readonly ILoggerService _logger;
        private readonly IEventAggregator _eventAggregator;

        private const string CALIBRATION_CONFIG_KEY = "NeedleCalibrationConfig";
        private NeedleCalibrationParams _currentParameters;

        public NeedleCalibrationParams CurrentParameters => _currentParameters;

        public event Action<NeedleCalibrationParams> ParametersLoaded;
        public event Action<NeedleCalibrationParams> ParametersSaved;

        public NeedleCalibrationService(
            IParameterStorage parameterStorage,
            ILoggerService logger,
            IEventAggregator eventAggregator)
        {
            _parameterStorage = parameterStorage;
            _logger = logger;
            _eventAggregator = eventAggregator;
            _currentParameters = new NeedleCalibrationParams();
        }

        /// <summary>
        /// 加载针头校准参数
        /// </summary>
        public async Task<bool> LoadParametersAsync(string configName = "Default")
        {
            try
            {
                _logger.Info($"正在加载针头校准参数: {configName}");

                var key = $"{CALIBRATION_CONFIG_KEY}_{configName}";

                // 使用 Task.Run 包装同步调用，使其可以异步执行
                var parameters = await Task.Run(() =>
                    _parameterStorage.Load<NeedleCalibrationParams>(key));

                if (parameters != null)
                {
                    _currentParameters = parameters;
                    _currentParameters.CalibrationName = configName;

                    _logger.Info($"针头校准参数加载成功: {configName}");
                    ParametersLoaded?.Invoke(_currentParameters);
                    return true;
                }
                else
                {
                    // 创建默认参数
                    _currentParameters = new NeedleCalibrationParams { CalibrationName = configName };
                    _logger.Info($"创建默认针头校准参数: {configName}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"加载针头校准参数失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 保存针头校准参数
        /// </summary>
        public async Task<bool> SaveParametersAsync(string configName = null)
        {
            try
            {
                var name = configName ?? _currentParameters.CalibrationName;
                var key = $"{CALIBRATION_CONFIG_KEY}_{name}";

                _logger.Info($"正在保存针头校准参数: {name}");

                // 使用 Task.Run 包装同步调用，使其可以异步执行
                await Task.Run(() => _parameterStorage.Save(key, _currentParameters));

                _logger.Info($"针头校准参数保存成功: {name}");
                ParametersSaved?.Invoke(_currentParameters);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"保存针头校准参数失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取可用的校准配置列表
        /// </summary>
        public async Task<List<string>> GetAvailableConfigsAsync()
        {
            try
            {
                // 这里可以根据实际存储方式实现获取配置列表
                // 简化实现，返回默认列表
                return await Task.Run(() => new List<string> { "Default", "Config1", "Config2" });
            }
            catch (Exception ex)
            {
                _logger.Error($"获取校准配置列表失败: {ex.Message}");
                return new List<string> { "Default" };
            }
        }

        /// <summary>
        /// 应用参数到当前配置
        /// </summary>
        public void ApplyParameters(NeedleCalibrationParams parameters)
        {
            if (parameters != null)
            {
                // 复制属性值到当前参数
                CopyProperties(parameters, _currentParameters);
                _logger.Info("针头校准参数已应用到当前配置");
            }
        }

        /// <summary>
        /// 复制属性值
        /// </summary>
        private void CopyProperties(NeedleCalibrationParams source, NeedleCalibrationParams target)
        {
            target.CalibrationName = source.CalibrationName;
            target.SearchPoint1 = source.SearchPoint1;
            target.SearchPoint2 = source.SearchPoint2;
            target.SearchPoint3 = source.SearchPoint3;
            target.SearchPoint4 = source.SearchPoint4;
            target.ReferenceXYZ = source.ReferenceXYZ;
            target.CompensationXYZ = source.CompensationXYZ;
            target.CurrentXYZ = source.CurrentXYZ;
            target.SearchRange = source.SearchRange;
            target.ZSearchCount = source.ZSearchCount;
            target.SearchSpeed = source.SearchSpeed;
            target.FineSearchSpeed = source.FineSearchSpeed;
        }

        /// <summary>
        /// 更新参数并通知UI
        /// </summary>
        public void UpdateParameters(NeedleCalibrationParams parameters)
        {
            if (parameters != null)
            {
                CopyProperties(parameters, _currentParameters);

                // 触发参数变更事件
                ParametersLoaded?.Invoke(_currentParameters);

                _logger.Info("针头校准参数已更新");
            }
        }

    }
}
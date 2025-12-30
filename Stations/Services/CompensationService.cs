using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Stations.Services
{
    /// <summary>
    /// 补偿值类型
    /// </summary>
    public enum CompensationType
    {
        Tab,        // Tab补偿（X,Y方向）
        TabZ,       // TabZ补偿（Z方向）
        Slot,       // Slot补偿（X,Z方向）
        Actuator,   // 执行器补偿（X方向）
        PressZ,     // 夹爪下压补偿（Z方向）
    }
    /// <summary>
    /// 补偿值数据
    /// </summary>
    public class CompensationData
    {
        public double CompensationX { get; set; }             // X方向补偿（组装）
        public double CompensationY { get; set; }             // Y方向补偿（组装）
        public double CompensationZ { get; set; }             // Z方向补偿（组装）
        public double CompensationXTranslate { get; set; }    // X轴平移
        public double CompensationZTranslate { get; set; }    // Z轴组装
        public double CompensationZPress { get; set; }        // 夹爪下压补偿（Z方向）
        public string Source { get; set; } = "Default";

        // 默认构造函数
        public CompensationData()
        {
            CompensationX = 0;
            CompensationY = 0;
            CompensationZ = 0;
            CompensationXTranslate = 0;
            CompensationZTranslate = 0;
            CompensationZPress = 0;
            Source = "Default";
        }
        public CompensationData(double compensationX, double compensationY, double compensationZ, double compensationXTranslate, double compensationZTranslate, double compensationZPress, string source = "Default")
        {
            CompensationX = compensationX;
            CompensationY = compensationY;
            CompensationZ = compensationZ;
            CompensationXTranslate = compensationXTranslate;
            CompensationZTranslate = compensationZTranslate;
            CompensationZPress = compensationZPress;
            Source = "Default";
        }
    }
    public interface ICompensationService
    {
        /// <summary>
        /// 获取指定模块和类型的补偿值
        /// </summary>
        CompensationData GetCompensation(int moduleNumber, CompensationType type);

        /// <summary>
        /// 更新补偿值
        /// </summary>
        void UpdateCompensation(int moduleNumber, CompensationType type, CompensationData data);

        /// <summary>
        /// 获取所有模块的Tab补偿
        /// </summary>
        Dictionary<int, CompensationData> GetAllTabCompensations();

        /// <summary>
        /// 获取所有模块的Slot补偿
        /// </summary>
        Dictionary<int, CompensationData> GetAllSlotCompensations();

        /// <summary>
        /// 获取所有模块的Actuator补偿
        /// </summary>
        Dictionary<int, CompensationData> GetAllActuatorCompensations();
    }

    public class CompensationService : ICompensationService
    {
        private readonly ILogger _logger;

        // 内存存储补偿值字典
        private readonly ConcurrentDictionary<string, CompensationData> _compensations;

        public CompensationService(ILogger logger)
        {
            _logger = logger;
            _compensations = new ConcurrentDictionary<string, CompensationData>();

            // 初始化默认补偿值
            InitializeDefaultCompensations();
        }

        private void InitializeDefaultCompensations()
        {
            // 为模块1-6设置默认补偿值
            for (int module = 1; module <= 6; module++)
            {
                // Tab补偿默认值
                _compensations[$"Module{module}_Tab"] = new CompensationData
                {
                    CompensationX = 0.0,
                    CompensationY = 0.0,
                    Source = "Default"
                };

                // Slot补偿默认值
                _compensations[$"Module{module}_Slot"] = new CompensationData
                {
                    CompensationZ = 0.0,
                    Source = "Default"
                };

                // Actuator补偿默认值
                _compensations[$"Module{module}_Actuator"] = new CompensationData
                {
                    CompensationX = 0.0,
                    Source = "Default"
                };
            }
        }

        public CompensationData GetCompensation(int moduleNumber, CompensationType type)
        {
            string key = $"Module{moduleNumber}_{type}";

            if (_compensations.TryGetValue(key, out var data))
            {
                return data;
            }

            _logger.Warn($"未找到补偿值: Module={moduleNumber}, Type={type}");
            return new CompensationData(); // 返回空的补偿值
        }

        public void UpdateCompensation(int moduleNumber, CompensationType type, CompensationData data)
        {
            string key = $"Module{moduleNumber}_{type}";
            _compensations[key] = data;

            _logger.Info($"更新补偿值: Module={moduleNumber}, Type={type}, X={data.CompensationX}, Y={data.CompensationY}, Z={data.CompensationZ}");
        }

        public Dictionary<int, CompensationData> GetAllTabCompensations()
        {
            return GetCompensationsByType(CompensationType.Tab);
        }

        public Dictionary<int, CompensationData> GetAllSlotCompensations()
        {
            return GetCompensationsByType(CompensationType.Slot);
        }

        public Dictionary<int, CompensationData> GetAllActuatorCompensations()
        {
            return GetCompensationsByType(CompensationType.Actuator);
        }

        private Dictionary<int, CompensationData> GetCompensationsByType(CompensationType type)
        {
            var result = new Dictionary<int, CompensationData>();

            for (int module = 1; module <= 6; module++)
            {
                string key = $"Module{module}_{type}";
                if (_compensations.TryGetValue(key, out var data))
                {
                    result[module] = data;
                }
            }

            return result;
        }
    }
}

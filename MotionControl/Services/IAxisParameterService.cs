using MotionControl.Models;
using Core.Abstraction;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MotionControl.Services
{
    public interface IAxisParameterService
    {
        /// <summary>
        /// 从hwcfg.xml加载所有轴信息
        /// </summary>
        IReadOnlyList<AxisInfo> LoadAllAxes();

        /// <summary>
        /// 从控制卡读取单个轴参数，同时写入配置文件
        /// </summary>
        Task ReadFromCardAsync(AxisInfo axis);

        /// <summary>
        /// 将单个轴参数写入控制卡，同时写入配置文件
        /// </summary>
        Task WriteToCardAsync(AxisInfo axis);

        /// <summary>
        /// 将所有轴参数写入控制卡，同时写入配置文件
        /// </summary>
        Task WriteAllToCardAsync(IProgressReporter progressReporter = null);

        /// <summary>
        /// 从控制卡读取所有轴参数，同时写入配置文件
        /// </summary>
        Task ReadAllFromCardAsync(IProgressReporter progressReporter = null);

        /// <summary>
        /// 保存所有轴参数到单一JSON文件
        /// </summary>
        void SaveAllAxisParameters(IEnumerable<AxisInfo> axes);

        /// <summary>
        /// 从单一JSON文件加载所有轴参数
        /// </summary>
        Dictionary<string, AxisParams> LoadAllAxisParameters();

        /// <summary>
        /// 加载插补系统列表
        /// </summary>
        IEnumerable<InterpolationSystem> LoadInterpolationSystems();

        /// <summary>
        /// 从控制卡读取插补系参数，同时写入配置文件
        /// </summary>
        void ReadInterpolationFromCard(InterpolationSystem system);

        /// <summary>
        /// 将插补系参数写入控制卡，同时写入配置文件
        /// </summary>
        void WriteInterpolationToCard(InterpolationSystem system);

        /// <summary>
        /// 保存所有插补系参数到单一JSON文件
        /// </summary>
        void SaveAllInterpolationSystems(IEnumerable<InterpolationSystem> systems);

        /// <summary>
        /// 从单一JSON文件加载所有插补系参数
        /// </summary>
        List<InterpolationSystemConfig> LoadAllInterpolationSystems();

        /// <summary>
        /// 将插补系轴配置同步到hwcfg.xml（更新axes属性）
        /// </summary>
        void SyncInterpolationAxesToHwConfig(IEnumerable<InterpolationSystem> systems);

        /// <summary>
        /// 从hwcfg.xml读取插补系轴配置（axes属性格式："卡号-轴号,卡号-轴号"）
        /// </summary>
        void LoadInterpolationAxesFromHwConfig(IEnumerable<InterpolationSystem> systems);

        /// <summary>
        /// 获取轴最大速度
        /// </summary>
        double GetAxisSpeed(int cardId, int axisId);

        /// <summary>
        /// 获取插补速度
        /// </summary>
        double GetInterpolationSpeeds(int cardId, int coordId);
    }
}

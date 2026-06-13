using MotionControl.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MotionControl.Interfaces
{
    /// <summary>
    /// 运动控制服务接口
    /// 支持 IObservable<T> 用于事件驱动的轴状态监控
    /// </summary>
    public interface IMotionService : IObservable<MotionControl.Events.AxisStateChangedEvent>
    {
        Task InitializeAsync();
        void Shutdown();

        /// <summary> 是否运行在模拟环境（无真实硬件卡，使用 VirtualMotionCard） </summary>
        bool IsSimulationMode { get; }

        /// <summary>EtherCAT 总线错误码（nmc_get_errcode），0=正常</summary>
        int GetEtherCatBusErrorCode();

        void EnableAxis(int axisId);
        void DisableAxis(int axisId);

        Task MoveAbsAsync(int axisId, double position, double velocity, CancellationToken token = default);

        /// <summary>
        /// 多轴同步绝对运动：所有轴同时下发运动指令，统一轮询等待完成。
        /// 避免多轴各自独立 WaitForDone 导致的运动卡 DLL 交叉干扰。
        /// </summary>
        /// <param name="moves">轴运动参数列表 (axisId, position, velocity)</param>
        Task MoveAbsMultiAxisAsync(IReadOnlyList<(int axisId, double position, double velocity)> moves, CancellationToken token = default);
        Task MoveRelAsync(int axisId, double distance, double velocity, CancellationToken token = default);

        /// <summary>相对运动下发（单轴手动：后台读卡+下发，立即返回 Task，不阻塞 UI）</summary>
        Task MoveRelStartAsync(int axisId, double distance, double velocity);
        Task MoveLineAbsAsync(int coordId, int[] axisIds, double[] positions, double velocity, CancellationToken token = default);

        /// <summary>单轴回原点：仅指定轴号，使用卡内/参数表已配置的回零模式与速度（单轴操作页）</summary>
        Task HomeAxisAsync(int axisId, CancellationToken token = default);

        /// <summary>回原点并临时写入回零模式与速度（工站任务、配方步骤等）</summary>
        Task HomeAsync(int axisId, int mode = 1, double minVel = 5, double maxVel = 20, CancellationToken token = default);

        /// <summary>点动启动（speed 单位 mm/s，Jog 前写入轴速度曲线）</summary>
        void JogStart(int axisId, bool positiveDirection, double speed);
        void JogStop(int axisId);

        void StopAxis(int axisId);
        void EmergencyStop(int axisId);

        IAxis GetAxisState(int axisId);
        void ClearAlarm(int axisId);

        /// <summary> 查询轴回零状态：1=已回零, 0=进行中, -1=失败/超时 </summary>
        Task<int> CheckHomeDoneAsync(int axisId);
        
        /// <summary> 清除轴位置（归零） </summary>
        void ClearPosition(int axisId);

        /// <summary> 获取轴当前位置（直接读卡，实时性高，用于位置触发等场景） </summary>
        double GetAxisPosition(int axisId);

        bool ReadDi(int port);
        bool ReadDo(int port);
        void WriteDo(int port, bool value);

        void StartPolling(int intervalMs = 100);
        void StopPolling();

        /// <summary> 获取所有轴配置（来自 hwcfg.xml） </summary>
        IReadOnlyList<AxisConfig> GetAxisConfigurations();

        /// <summary> 获取所有任务配置（来自 hwcfg.xml） </summary>
        IReadOnlyList<TaskConfig> GetTaskConfigurations();

        /// <summary> 获取所有数字输入（DI）配置列表 </summary>
        /// <returns>只读的 DI 配置集合</returns>
        IReadOnlyList<IoConfig> GetInputConfigurations();

        /// <summary> 获取所有数字输出（DO）配置列表 </summary>
        /// <returns>只读的 DO 配置集合</returns>
        IReadOnlyList<IoConfig> GetOutputConfigurations();

        /// <summary> 获取三色灯/蜂鸣器配置列表（来自hwcfg.xml TowerLights节） </summary>
        IReadOnlyList<LightConfig> GetLightConfigurations();

        // 连续插补

        /// <summary> 初始化连续插补：设置速度曲线、前瞻模式、打开插补列表 </summary>
        void InitializeContinuousInterpolation(int coordId, int[] axisIds, double startVel = 5, double maxVel = 50, double acc = 500, double dec = 500, double endVel = 0,double sPara = 0.05);

        /// <summary> 添加直线插补段到连续插补列表 </summary>
        void AddLineSegment(int coordId, double[] targetPos, ushort posiMode = 1, int mark = 0);

        /// <summary> 执行连续插补（启动并关闭列表） </summary>
        void ExecuteContinuousInterpolation(int coordId);

        /// <summary> 暂停连续插补 </summary>
        void PauseContinuousInterpolation(int coordId);

        /// <summary> 等待坐标系运动完成（支持取消令牌以实现急停快速响应） </summary>
        Task<bool> WaitForCoordMotionCompletionAsync(int coordId, TimeSpan timeout, CancellationToken token = default);

        /// <summary> 读取单个模拟量通道并转换为物理量（通过雷赛卡AD输入） </summary>
        Task<double> ReadAnalogChannelAsync(int cardNo, int channel);

        /// <summary> 批量读取模拟量通道并转换为物理量（channelMap: 键为逻辑通道标识，值为编码通道号） </summary>
        Task<Dictionary<int, double>> ReadAnalogChannelsAsync(Dictionary<int, int> channelMap);

        /// <summary> 检查指定模拟量通道是否已配置且可用 </summary>
        bool IsAnalogChannelAvailable(int cardNo, int channel);
    }
}
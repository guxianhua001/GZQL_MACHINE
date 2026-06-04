namespace Core.Abstraction
{
    /// <summary>
    /// 上下料工站操作接口：LoadingTask 实现此接口，
    /// Controller 层通过 IStationRegistry 获取工站后转型调用，
    /// 所有运动/IO 控制逻辑的唯一实现层
    /// </summary>
    public interface ILoadUnloadStationOperations
    {
        /// <summary> 工站标识符 </summary>
        string StationIdentifierValue { get; }

        #region 低层操作（基类 StationTaskBase 提供）

        /// <summary> 执行一段手动流程（RunStep 安全保护） </summary>
        Task ExecuteManualProcess(string processName, Func<Task> action);

        /// <summary> 根据轴名查找轴 ID </summary>
        int FindAxisIdByName(string axisName);

        /// <summary> 移动轴到指定位置 </summary>
        Task ExecuteMoveAsync(int axisId, string positionName, double velocity, double offset = 0);

        /// <summary> 轴回零 </summary>
        Task ExecuteHomeAsync(int axisId, int mode = 1, double minVel = 5, double maxVel = 20);

        /// <summary> 查询轴是否已回零 </summary>
        Task<bool> IsAxisHomedAsync(int axisId);

        /// <summary> 气缸/执行器动作（DO + DI 等待） </summary>
        Task TriggerCylinderAsync(int doId, bool value, int diId = -1, int timeoutMs = 3000, int blindDelayMs = 300);

        /// <summary> 写数字输出 </summary>
        void WriteDO(int logicalId, bool value);

        /// <summary> 读数字输入 </summary>
        bool ReadDI(int logicalId);

        #endregion

        #region 平台真空控制（Stage）

        /// <summary> 开平台真空：写 DO + 等待 DI 反馈 </summary>
        Task StageVacuumOnAsync(CancellationToken token = default);

        /// <summary> 破平台真空：脉冲破真空 DO 后关闭 </summary>
        Task StageVacuumOffAsync(CancellationToken token = default);

        /// <summary> 读取平台真空反馈状态 </summary>
        bool IsStageVacuumOn();

        #endregion

        #region 夹爪真空控制

        /// <summary> 开夹爪真空 </summary>
        Task GripperVacuumOnAsync(CancellationToken token = default);

        /// <summary> 关夹爪真空 </summary>
        Task GripperVacuumOffAsync(CancellationToken token = default);

        /// <summary> 读取夹爪真空反馈状态 </summary>
        bool IsGripperVacuumOn();

        #endregion

        #region 自动流程

        /// <summary> 自动取料流程 </summary>
        Task AutoPickUpFlowAsync(CancellationToken token);

        /// <summary> 自动扫描流程 </summary>
        Task AutoScanFlowAsync(CancellationToken token);

        /// <summary> 自动下料流程 </summary>
        Task AutoUnloadFlowAsync(CancellationToken token);

        #endregion
    }
}

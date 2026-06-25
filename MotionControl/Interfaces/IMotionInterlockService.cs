namespace MotionControl.Interfaces
{
    /// <summary>
    /// 工艺手动运动互锁：位置编辑器、上下料、流程编辑器等上层入口使用。
    /// 仅 WAITRUN 允许；轴操作面板直接调用 MotionService，不受此互锁约束。
    /// </summary>
    public interface IMotionInterlockService
    {
        /// <summary>当前是否允许执行手动运动（点动、相对/绝对移动、回零等）</summary>
        bool CanExecuteManualMotion { get; }

        /// <summary>获取禁止操作时的本地化提示文本</summary>
        string GetBlockedMessage();

        /// <summary>校验手动运动许可，不允许时抛出 <see cref="Exceptions.MotionInterlockException"/></summary>
        void EnsureManualMotionAllowed();
    }
}

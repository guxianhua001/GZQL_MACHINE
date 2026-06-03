namespace MotionControl.Interfaces
{
    /// <summary>
    /// 单轴手动操作面板（右侧 Drawer）开关状态。
    /// 关闭时暂停 UI 刷新并降低硬件轮询频率，避免空转占用 CPU。
    /// </summary>
    public interface IAxisOperationPanelState
    {
        bool IsPanelOpen { get; }

        /// <summary>面板打开/关闭时通知（true=打开）</summary>
        event Action<bool> PanelOpenChanged;

        void SetPanelOpen(bool isOpen);
    }
}

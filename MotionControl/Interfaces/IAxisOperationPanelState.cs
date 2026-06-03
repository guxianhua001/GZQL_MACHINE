using System.Collections.Generic;

namespace MotionControl.Interfaces
{
    /// <summary>
    /// 单轴手动操作面板（右侧 Drawer）开关状态。
    /// 关闭时暂停 UI 刷新并降低硬件轮询频率，避免空转占用 CPU。
    /// </summary>
    public interface IAxisOperationPanelState
    {
        bool IsPanelOpen { get; }

        /// <summary>当前 Tab 可见的逻辑轴号；为空表示不限制（轮询全部轴）</summary>
        IReadOnlyList<int> VisibleLogicalAxisIds { get; }

        /// <summary>面板打开/关闭时通知（true=打开）</summary>
        event Action<bool> PanelOpenChanged;

        void SetPanelOpen(bool isOpen);

        /// <summary>更新当前工站 Tab 可见轴，仅对这些轴做快采样</summary>
        void SetVisibleLogicalAxisIds(IReadOnlyList<int> axisIds);
    }
}

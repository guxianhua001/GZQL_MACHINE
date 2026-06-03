using MotionControl.Interfaces;
using System;

namespace MotionControl.Services
{
    /// <summary>
    /// 单轴操作面板可见性状态（单例）
    /// </summary>
    public sealed class AxisOperationPanelState : IAxisOperationPanelState
    {
        public bool IsPanelOpen { get; private set; }

        /// <summary>面板开关变更（true=打开）</summary>
        public event Action<bool> PanelOpenChanged;

        public void SetPanelOpen(bool isOpen)
        {
            if (IsPanelOpen == isOpen) return;
            IsPanelOpen = isOpen;
            PanelOpenChanged?.Invoke(isOpen);
        }
    }
}

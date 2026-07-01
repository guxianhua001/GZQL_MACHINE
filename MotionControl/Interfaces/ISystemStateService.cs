using System;
using MotionControl.Models;

namespace MotionControl.Interfaces
{
    public interface ISystemStateService
    {
        StationState CurrentState { get; }
        bool CanStart { get; }
        bool CanPause { get; }
        bool CanResume { get; }
        /// <summary>安全门锁锁定状态：true=已锁定（DO 点位由 hwcfg.xml OutputGroups Group="DoorLocks" 配置）</summary>
        bool IsSafetyDoorLocked { get; }
        void RequestStart();
        void RequestStop();
        void RequestPause();
        void RequestResume();
        void RequestReset();
        void RequestEmergencyStop();
        void UpdateSignalStates(); // 由轮询线程调用

        void SimulateButtonPress(string buttonName);
        void SimulateSafetyTrigger(string signalName);

        /// <summary>安全门锁状态变更事件（参数: true=已锁定, false=已解锁）</summary>
        event Action<bool> SafetyDoorLockChanged;
    }
}

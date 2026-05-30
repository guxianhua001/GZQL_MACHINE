using MotionControl.Models;

namespace MotionControl.Interfaces
{
    public interface ISystemStateService
    {
        StationState CurrentState { get; }
        bool CanStart { get; }
        bool CanPause { get; }
        bool CanResume { get; }
        void RequestStart();
        void RequestStop();
        void RequestPause();
        void RequestResume();
        void RequestReset();
        void RequestEmergencyStop();
        void UpdateSignalStates(); // 由轮询线程调用

        void SimulateButtonPress(string buttonName);
        void SimulateSafetyTrigger(string signalName);
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Module.Services
{
    public enum VacuumStatus
    {
        Unknown = 0,
        On = 1,
        Off = 2,
        Checking = 3
    }

    public interface ILoadUnloadController
    {
        Task ChuckVacuumOnAsync();
        Task ChuckVacuumOffAsync();
        Task<bool> ChuckVacuumCheckAsync();
        Task GripperVacuumOnAsync();
        Task GripperVacuumOffAsync();
        Task<bool> GripperVacuumCheckAsync();

        Task MoveToPickPositionAsync();
        Task MoveToScanPositionAsync();
        Task MoveToUnloadPositionAsync();
        Task MoveToAssemblyPositionAsync(int siteIndex);
        Task HomeAllAsync();

        Task ClampAsync();
        Task ReleaseAsync();
        Task MoveGripperToAngleAsync(double angle);

        Task AutoPickUpAsync();
        Task AutoScanAsync();
        Task AutoUnloadAsync();

        Task<Dictionary<string, bool>> GetAxisReadyStatusAsync();
        Task<Dictionary<string, double>> GetRealTimePositionsAsync();
        VacuumStatus GetVacuumStatus();
        VacuumStatus GetGripperVacuumStatus();
        bool CanExecuteMotion();

        void StopMotion();
    }
}

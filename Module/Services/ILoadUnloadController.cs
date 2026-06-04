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
        // 平台真空控制（Stage）：从 hwconfig 读取 IO 地址
        Task ChuckVacuumOnAsync();
        Task ChuckVacuumOffAsync();

        // 夹爪真空控制
        Task GripperVacuumOnAsync();
        Task GripperVacuumOffAsync();

        Task MoveToPickPositionAsync();
        Task MoveToScanPositionAsync();
        Task MoveToUnloadPositionAsync();
        Task MoveToAssemblyPositionAsync(int siteIndex);
        Task HomeAllAsync();

        Task ClampAsync();
        Task ReleaseAsync();

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

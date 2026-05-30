using System;
using System.Threading.Tasks;

namespace Module.Services
{
    public interface ILoadUnloadStationOperations
    {
        string StationIdentifierValue { get; }
        Task ExecuteManualProcess(string processName, Func<Task> action);
        int FindAxisIdByName(string axisName);
        Task ExecuteMoveAsync(int axisId, string positionName, double velocity, double offset = 0);
        Task ExecuteHomeAsync(int axisId, int mode = 1, double minVel = 5, double maxVel = 20);
        Task<bool> IsAxisHomedAsync(int axisId);
        Task TriggerCylinderAsync(int doId, bool value, int diId = -1, int timeoutMs = 3000, int blindDelayMs = 300);
        void WriteDO(int logicalId, bool value);
        bool ReadDI(int logicalId);
    }
}

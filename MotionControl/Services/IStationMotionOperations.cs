using System.Threading.Tasks;

namespace MotionControl.Services
{
    public interface IStationMotionOperations
    {
        int FindAxisIdByName(string axisName);
        Task ExecuteMoveAsync(int axisId, string positionName, double velocity, double offset = 0);
        string StationIdentifierValue { get; }
    }
}

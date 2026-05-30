using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Abstraction
{
    public interface IPositionMotionController
    {
        Task<Dictionary<string, double>> TeachAsync(string stationIdentifier);

        Task GotoAsync(string stationIdentifier, Dictionary<string, double> targetPositions, double velocity);

        void Stop(string stationIdentifier);

        bool CanExecuteMotion(string stationIdentifier);
    }
}

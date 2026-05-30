using System.Threading;
using System.Threading.Tasks;

namespace Core.Abstraction
{
    public interface INeedleTeachService
    {
        Task MoveNeedleToBaseZAsync(int zAxisId, double baseZ, double speed, CancellationToken ct = default);
        Task<double> TeachCurrentPositionAsync(int zAxisId);
    }
}

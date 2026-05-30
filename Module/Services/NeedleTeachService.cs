using Core.Abstraction;
using MotionControl.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace Module.Services
{
    public class NeedleTeachService : INeedleTeachService
    {
        private readonly IMotionService _motionService;

        public NeedleTeachService(IMotionService motionService)
        {
            _motionService = motionService;
        }

        public async Task MoveNeedleToBaseZAsync(int zAxisId, double baseZ, double speed, CancellationToken ct = default)
        {
            await _motionService.MoveAbsAsync(zAxisId, baseZ, speed, ct);
        }

        public Task<double> TeachCurrentPositionAsync(int zAxisId)
        {
            double position = _motionService.GetAxisPosition(zAxisId);
            return Task.FromResult(position);
        }
    }
}

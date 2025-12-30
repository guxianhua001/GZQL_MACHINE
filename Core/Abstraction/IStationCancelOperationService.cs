

namespace Core.Abstraction
{
    public interface IStationCancelOperationService
    {
        void CancelCurrentOperation();
        void StopAllAxes();
    }
}

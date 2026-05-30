
namespace MotionControl.Interfaces
{
    public interface IIoPoint
    {
        string Name { get; }
        int LogicalId { get; }
        bool IsInput { get; }
        bool Value { get; }
    }
}
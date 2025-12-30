
namespace Core.Abstraction
{
    public interface IMotionTask : ITask
    {
        // 设备访问
        IStation Station { get; }
        Dictionary<int, IAxis> AxisMap { get; }
        Dictionary<int, IDigitalOutput> DoMap { get; }
        Dictionary<int, IDigitalInput> DiMap { get; }
        Dictionary<int, IAxis> PositionTableAxisMap { get; }
        // 设备注册方法
        void RegisterAxis(int axisSetId, bool IsShownInPositionTable = true);
        void RegisterDo(int doSetId);
        void RegisterDi(int diSetId);

        // 运动控制特定状态
        bool TaskHomeOK { get; set; }
        bool IsMaterialInitialization { get; set; }
        int Z_PositionAxisIdIndex { get; set; }
        double Z_Safe { get; set; }
    }
}
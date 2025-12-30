
namespace Interfaces
{
    // 数据模型
    public struct TorquePositionData
    {
        public double Torque { get; }
        public double Position { get; }

        public TorquePositionData(double torque, double position)
        {
            Torque = torque;
            Position = position;
        }
    }
}

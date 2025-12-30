using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces.SharedInterfaces
{
    public enum SystemStatus
    {
        Ready,      // 系统就绪
        Moving,     // 运动中
        Error,      // 系统错误
        Connecting, // 连接中
        Calibrating // 校准中
    }
    public enum AxisType
    {
        UpperX,
        UpperY,
        LowerX,
        LowerY
    }
    public interface IGantrySyncService
    {
        int SystemId { get; set; }
        bool IsSynchronizing { get; }

        event ServiceStatusChangedHandler StatusChanged;
        event PositionUpdatedHandler PositionUpdated;

        bool RecordBasePositions();
        void EnableSynchronization(bool enable);
        Task MoveBothToTarget(PointF targetPosition, GantryType gantryType, double speed);
        Task MoveToTarget(PointF upperTarget, PointF lowerTarget, double speed);
        void MoveAxisJog(AxisType axis, int dir, float speed);
        void StopAllMotion();
        void ResetSystem(PointF safePosition);

        // 添加持久化方法
        void SaveBasePositions();
        void LoadBasePositions();

        // 获取基准位置
        PointF BasePositionUpper { get; }
        PointF BasePositionLower { get; }

        // 上/下龙门当前位置获取方法
        PointF GetUpperGantryPosition();
        PointF GetLowerGantryPosition();
        void Jog(GantryType gantry, JogDirection? direction, double speed, bool synchronize);
        void StopJog();
    }
    public delegate void ServiceStatusChangedHandler(string status);
    public delegate void PositionUpdatedHandler(GantryState state);
}

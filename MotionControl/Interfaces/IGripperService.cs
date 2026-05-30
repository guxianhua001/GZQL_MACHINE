using System;
using System.Threading;
using System.Threading.Tasks;
using MotionControl.Models;

namespace MotionControl.Interfaces
{
    public interface IGripperService
    {
        #region 生命周期
        Task InitializeAsync(CancellationToken token = default);
        void StartMonitoring(int intervalMs = 200);
        void StopMonitoring();
        #endregion

        #region 快速操作（用于 Pick 流程）
        Task ClampAsync(double position, CancellationToken token = default);
        Task ReleaseAsync(double position, CancellationToken token = default);
        #endregion

        #region 运动控制
        Task MoveToPositionAsync(double position, double speed, CancellationToken token = default);
        Task JogLeftAsync(double step, double speed, CancellationToken token = default);
        Task JogRightAsync(double step, double speed, CancellationToken token = default);
        void Stop();
        #endregion

        #region 力矩控制
        void SetTorque(double percentage);
        #endregion

        #region 系统操作
        Task HomeAsync(CancellationToken token = default);
        void ResetAlarm();
        #endregion

        #region 状态查询
        GripperState GetState();
        double GetCurrentPosition();
        bool IsMoving { get; }
        bool IsInitialized { get; }
        #endregion

    }
}

using System.Threading;
using System.Threading.Tasks;
using MotionControl.Models;

namespace MotionControl.Interfaces
{
    public enum TaskState
    {
        Idle,
        Running,
        Paused,
        Stopped,
        Completed,
        Error,
        Homing
    }

    public interface ITask
    {
        string TaskName { get; }
        int TaskId { get; }
        TaskState State { get; }

        Task RunAsync(CancellationToken token);
        Task PauseAsync();
        Task ResumeAsync();
        Task StopAsync();
        Task HomeAsync();
        Task EmergencyStopAsync();
    }
}
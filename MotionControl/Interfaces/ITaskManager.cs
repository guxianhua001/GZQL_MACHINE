using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MotionControl.Interfaces
{
    public interface ITaskManager
    {
        Task StartAllAsync();
        Task StopAllAsync();
        Task PauseAllAsync();
        Task ResumeAllAsync();
        Task EmergencyStopAllAsync();
        Task HomeAllAsync();
        void StepNextAll();
        void EnableSingleStepAll();
        void DisableSingleStepAll();
    }
}

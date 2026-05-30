using MotionControl.Interfaces;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StationTasks.ViewModel
{
    public class StationControlViewModel : BindableBase
    {
        private readonly ITaskManager _taskManager;

        public StationControlViewModel(ITaskManager taskManager)
        {
            _taskManager = taskManager;
        }

        public async void Start() => await _taskManager.StartAllAsync();
        public void Stop() => _taskManager.StopAllAsync();
        public void Pause() => _taskManager.PauseAllAsync();
        public void Resume() => _taskManager.ResumeAllAsync();
        public void Estop() => _taskManager.EmergencyStopAllAsync();
        public void Home() => _taskManager.HomeAllAsync();
        public void Step() => _taskManager.StepNextAll();
        public void EnableSingleStep() => _taskManager.EnableSingleStepAll();
        public void DisableSingleStep() => _taskManager.DisableSingleStepAll();
    }
}

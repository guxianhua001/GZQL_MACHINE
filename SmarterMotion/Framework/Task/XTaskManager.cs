using Core.Abstraction;
using System.Collections.Generic;

namespace SmarterMotion
{
    public sealed class XTaskManager : XObject
    {
        private Dictionary<int, IMotionTask> tasks = new Dictionary<int, IMotionTask>();

        private readonly static XTaskManager instance = new XTaskManager();
        XTaskManager()
        {

        }
        public static XTaskManager Instance
        {
            get { return instance; }
        }

        public Dictionary<int, IMotionTask> Tasks
        {
            get { return tasks; }
        }

        public void BindTask(int taskId, IMotionTask task, string name)
        {
            if (tasks.ContainsKey(taskId) == false)
            {
                task.TaskId = taskId;
                task.Name = name;
                tasks.Add(taskId, task);
            }
        }

        public IMotionTask FindTaskById(int taskId)
        {
            if (tasks.ContainsKey(taskId) == false)
            {
                return null;
            }
            return tasks[taskId];
        }
        public int FindTaskdByTask(IMotionTask task)
        {
            foreach (var ts in tasks)
            {
                if (ts.Value == task)
                {
                    return ts.Key;

                }
            }
            return 999;
        }
    }
}

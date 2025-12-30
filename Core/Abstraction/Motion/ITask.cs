using System;
using System.Windows.Media;

namespace Core.Abstraction
{
    public interface ITask
    {
        // 基础标识属性
        int TaskId { get; set; }
        string Name { get; set; }
        int StationId { get; set; }

        // 通用任务状态
        int Step { get; set; }
        int LastStep { get; set; }
        bool IsPaused { get; set; }
        bool IsStopped { get; set; }
        bool TaskHomeOK { get; set; }

        // 参数管理
        TaskParametersBase ParametersBase { get; }

        // 核心任务控制方法
        void SetTaskId(int taskId);
        void Initialize();
        void Start(object runMode);
        void Reset();
        void Cancel();
        void Pause();
        void Continue();

        // 通用事件
        event Action<string, Color> OnStep;

        // 错误处理
        bool HasShownErrorDialog { get; set; }
        // 事件处理
        int HandleEvent(IEvent xEvent);
    }
}
using System;

namespace Interfaces
{
    // UI协调器服务
    public interface IUICoordinator
    {
        void RunOnUIThread(Action action);
    }
}

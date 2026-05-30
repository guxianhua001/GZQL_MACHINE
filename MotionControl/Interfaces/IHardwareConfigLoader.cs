
using MotionControl.Models;

namespace MotionControl.Interfaces
{
    public interface IHardwareConfigLoader
    {
        MotionSystemConfig Load();  // 内部自己决定从哪里读取
    }
}

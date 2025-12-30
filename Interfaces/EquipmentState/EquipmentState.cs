

using System.ComponentModel;

namespace Interfaces
{
    #region 状态枚举
    public enum EquipmentState
    {
        [Description("待机")] Idle,
        [Description("运行")] Running,
        [Description("报警")] Alarm,
        [Description("暂停")] Paused,
        [Description("停止")] DOWN
    }
    #endregion
}

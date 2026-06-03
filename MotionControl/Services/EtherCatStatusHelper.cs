namespace MotionControl.Services
{
    /// <summary>
    /// EtherCAT 轴状态机 / 总线状态多语言资源键（与旧项目 nmc_get_axis_state_machine 0~7 一致）
    /// </summary>
    public static class EtherCatStatusHelper
    {
        /// <summary>轴状态机 → 资源键</summary>
        public static string GetAxisStateMachineResourceKey(int stateMachine)
        {
            return stateMachine switch
            {
                0 => "EtherCat_AxisState_0",
                1 => "EtherCat_AxisState_1",
                2 => "EtherCat_AxisState_2",
                3 => "EtherCat_AxisState_3",
                4 => "EtherCat_AxisState_4",
                5 => "EtherCat_AxisState_5",
                6 => "EtherCat_AxisState_6",
                7 => "EtherCat_AxisState_7",
                _ => "EtherCat_AxisState_Unknown"
            };
        }
    }
}

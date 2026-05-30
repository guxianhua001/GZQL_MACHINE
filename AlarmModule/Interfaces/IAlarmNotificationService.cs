using AlarmModule.Models;

namespace AlarmModule.Interfaces
{
    /// <summary>
    /// 报警通知服务接口：根据报警等级弹出不同级别的通知
    /// Level 1/2: 模态弹窗+蜂鸣
    /// Level 3/4: Toast通知
    /// </summary>
    public interface IAlarmNotificationService
    {
        /// <summary>
        /// 显示报警通知
        /// </summary>
        void ShowNotification(AlarmRecord alarm);

        /// <summary>
        /// 关闭所有通知
        /// </summary>
        void DismissAll();
    }
}

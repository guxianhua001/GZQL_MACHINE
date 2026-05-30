using AlarmModule.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace AlarmModule.Interfaces
{
    /// <summary>
    /// 报警服务接口：提供单行代码触发报警、生命周期管理、查询导出等核心功能
    /// </summary>
    public interface IAlarmService
    {
        /// <summary>
        /// 单行代码触发报警并持久化到数据库
        /// 支持防抖：相同Code+Source在配置时间窗口内不重复触发
        /// </summary>
        Task TriggerAlarmAsync(string alarmCode, AlarmLevel level, string description,
            string source = "", AlarmType type = AlarmType.HardwareFault,
            double? triggerValue = null, double? thresholdValue = null);

        /// <summary>
        /// 确认单条报警
        /// </summary>
        Task ConfirmAsync(long alarmId, string confirmedBy);

        /// <summary>
        /// 复位单条报警
        /// </summary>
        Task ResetAsync(long alarmId, string resetBy);

        /// <summary>
        /// 消除单条报警
        /// </summary>
        Task EliminateAsync(long alarmId);

        /// <summary>
        /// 确认所有未确认报警
        /// </summary>
        Task ConfirmAllAsync(string confirmedBy);

        /// <summary>
        /// 复位所有已确认报警
        /// </summary>
        Task ResetAllAsync(string resetBy);

        /// <summary>
        /// 当前活跃报警集合（Status != Eliminated）
        /// </summary>
        ObservableCollection<AlarmRecord> ActiveAlarms { get; }

        /// <summary>
        /// 未确认报警数量
        /// </summary>
        int UnconfirmedCount { get; }

        /// <summary>
        /// 报警触发事件
        /// </summary>
        event Action<AlarmRecord>? AlarmTriggered;

        /// <summary>
        /// 分页查询报警记录
        /// </summary>
        Task<PagedResult<AlarmRecord>> QueryAsync(AlarmQueryParams parameters);

        /// <summary>
        /// 导出报警数据到Excel
        /// </summary>
        Task ExportToExcelAsync(string filePath, AlarmQueryParams parameters);

        /// <summary>
        /// 刷新活跃报警列表
        /// </summary>
        Task RefreshActiveAlarmsAsync();
    }
}

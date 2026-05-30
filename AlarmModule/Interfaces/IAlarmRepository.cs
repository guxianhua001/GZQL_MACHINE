using AlarmModule.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AlarmModule.Interfaces
{
    /// <summary>
    /// 报警数据仓储接口：提供报警记录和阈值配置的CRUD操作
    /// </summary>
    public interface IAlarmRepository
    {
        Task<AlarmRecord> AddAsync(AlarmRecord alarm);
        Task UpdateAsync(AlarmRecord alarm);
        Task<AlarmRecord?> GetByIdAsync(long id);
        Task<IReadOnlyList<AlarmRecord>> GetActiveAlarmsAsync();
        Task<AlarmRecord?> FindRecentAsync(string alarmCode, string source, TimeSpan within);
        Task<int> CountUnconfirmedAsync();
        Task ConfirmAllAsync(string confirmedBy);
        Task ResetAllAsync(string resetBy);
        Task<PagedResult<AlarmRecord>> QueryAsync(AlarmQueryParams parameters);
        Task<List<AlarmRecord>> GetAlarmsForExportAsync(AlarmQueryParams parameters);

        Task<AlarmThresholdConfig?> GetThresholdConfigAsync(string alarmCode, string? source);
        Task<List<AlarmThresholdConfig>> GetAllThresholdConfigsAsync();
        Task SaveThresholdConfigAsync(AlarmThresholdConfig config);
        Task DeleteThresholdConfigAsync(int id);

        Task<Dictionary<AlarmLevel, int>> GetLevelDistributionAsync(DateTime? start, DateTime? end);
        Task<List<(string Source, int Count)>> GetTopSourcesAsync(int topN, DateTime? start, DateTime? end);
        Task<List<(DateTime Date, int Count)>> GetDailyTrendAsync(int days);
    }
}

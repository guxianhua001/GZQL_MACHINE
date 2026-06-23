using AlarmModule.Interfaces;
using AlarmModule.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AlarmModule.Data
{
    /// <summary>
    /// 报警数据仓储实现：基于EF Core + SQLite，提供报警记录和阈值配置的完整CRUD与统计查询
    /// 每次数据库操作创建新的DbContext实例，避免Singleton场景下DbContext被disposed
    /// </summary>
    public class AlarmRepository : IAlarmRepository
    {
        private readonly DbContextOptions<AlarmDbContext> _dbOptions;

        public AlarmRepository(DbContextOptions<AlarmDbContext> dbOptions)
        {
            _dbOptions = dbOptions;
        }

        /// <summary> 创建新的DbContext实例，确保每次操作使用独立的上下文 </summary>
        private AlarmDbContext CreateContext() => new AlarmDbContext(_dbOptions);

        public async Task<AlarmRecord> AddAsync(AlarmRecord alarm)
        {
            using var context = CreateContext();
            context.AlarmRecords.Add(alarm);
            await context.SaveChangesAsync();
            return alarm;
        }

        public async Task UpdateAsync(AlarmRecord alarm)
        {
            using var context = CreateContext();
            context.AlarmRecords.Update(alarm);
            await context.SaveChangesAsync();
        }

        public async Task<AlarmRecord?> GetByIdAsync(long id)
        {
            using var context = CreateContext();
            return await context.AlarmRecords.FindAsync(id);
        }

        public async Task<IReadOnlyList<AlarmRecord>> GetActiveAlarmsAsync()
        {
            using var context = CreateContext();
            return await context.AlarmRecords
                .Where(a => a.Status != AlarmStatus.Eliminated)
                .OrderByDescending(a => a.AlarmTime)
                .ToListAsync();
        }

        public async Task<AlarmRecord?> FindRecentAsync(string alarmCode, string source, TimeSpan within)
        {
            using var context = CreateContext();
            var cutoff = DateTime.Now - within;
            return await context.AlarmRecords
                .Where(a => a.AlarmCode == alarmCode && a.AlarmSource == source && a.AlarmTime >= cutoff)
                .OrderByDescending(a => a.AlarmTime)
                .FirstOrDefaultAsync();
        }

        public async Task<int> CountUnconfirmedAsync()
        {
            using var context = CreateContext();
            return await context.AlarmRecords
                .CountAsync(a => a.Status == AlarmStatus.Unconfirmed);
        }

        public async Task ConfirmAllAsync(string confirmedBy)
        {
            using var context = CreateContext();
            var now = DateTime.Now;
            var alarms = await context.AlarmRecords
                .Where(a => a.Status == AlarmStatus.Unconfirmed)
                .ToListAsync();

            foreach (var alarm in alarms)
            {
                alarm.Status = AlarmStatus.Confirmed;
                alarm.ConfirmedBy = confirmedBy;
                alarm.ConfirmedTime = now;
            }

            await context.SaveChangesAsync();
        }

        public async Task ResetAllAsync(string resetBy)
        {
            using var context = CreateContext();
            var now = DateTime.Now;
            var alarms = await context.AlarmRecords
                .Where(a => a.Status == AlarmStatus.Confirmed)
                .ToListAsync();

            foreach (var alarm in alarms)
            {
                alarm.Status = AlarmStatus.Reset;
                alarm.ResetBy = resetBy;
                alarm.ResetTime = now;
            }

            await context.SaveChangesAsync();
        }

        public async Task<PagedResult<AlarmRecord>> QueryAsync(AlarmQueryParams parameters)
        {
            using var context = CreateContext();
            var query = BuildFilterQuery(context, parameters);

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.AlarmTime)
                .Skip((parameters.PageNumber - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToListAsync();

            return new PagedResult<AlarmRecord>
            {
                CurrentPage = parameters.PageNumber,
                PageSize = parameters.PageSize,
                TotalCount = totalCount,
                Items = items
            };
        }

        public async Task<List<AlarmRecord>> GetAlarmsForExportAsync(AlarmQueryParams parameters)
        {
            using var context = CreateContext();
            var query = BuildFilterQuery(context, parameters);
            return await query.OrderByDescending(a => a.AlarmTime).ToListAsync();
        }

        public async Task<AlarmThresholdConfig?> GetThresholdConfigAsync(string alarmCode, string? source)
        {
            using var context = CreateContext();
            return await context.AlarmThresholdConfigs
                .FirstOrDefaultAsync(t => t.AlarmCode == alarmCode && t.AlarmSource == source);
        }

        public async Task<List<AlarmThresholdConfig>> GetAllThresholdConfigsAsync()
        {
            using var context = CreateContext();
            return await context.AlarmThresholdConfigs.ToListAsync();
        }

        public async Task SaveThresholdConfigAsync(AlarmThresholdConfig config)
        {
            using var context = CreateContext();

            // 编辑模式：按主键Id更新，允许修改AlarmCode/AlarmSource
            if (config.Id > 0)
            {
                var existingById = await context.AlarmThresholdConfigs.FindAsync(config.Id);
                if (existingById != null)
                {
                    existingById.AlarmCode = config.AlarmCode;
                    existingById.AlarmSource = config.AlarmSource;
                    existingById.ThresholdValue = config.ThresholdValue;
                    existingById.AlarmLevel = config.AlarmLevel;
                    existingById.AlarmType = config.AlarmType;
                    existingById.SuppressionWindowSeconds = config.SuppressionWindowSeconds;
                    existingById.IsEnabled = config.IsEnabled;
                    context.AlarmThresholdConfigs.Update(existingById);
                    await context.SaveChangesAsync();
                    return;
                }
            }

            // 新增模式：按AlarmCode+AlarmSource判断是否存在，存在则更新其余字段
            var existing = await context.AlarmThresholdConfigs
                .FirstOrDefaultAsync(t => t.AlarmCode == config.AlarmCode && t.AlarmSource == config.AlarmSource);

            if (existing != null)
            {
                existing.ThresholdValue = config.ThresholdValue;
                existing.AlarmLevel = config.AlarmLevel;
                existing.AlarmType = config.AlarmType;
                existing.SuppressionWindowSeconds = config.SuppressionWindowSeconds;
                existing.IsEnabled = config.IsEnabled;
                context.AlarmThresholdConfigs.Update(existing);
            }
            else
            {
                context.AlarmThresholdConfigs.Add(config);
            }

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// 批量更新阈值配置的启用状态
        /// </summary>
        /// <param name="ids">需要更新的配置Id集合</param>
        /// <param name="isEnabled">目标启用状态</param>
        public async Task BatchUpdateEnabledAsync(IReadOnlyList<int> ids, bool isEnabled)
        {
            if (ids == null || ids.Count == 0) return;

            using var context = CreateContext();
            var targets = await context.AlarmThresholdConfigs
                .Where(t => ids.Contains(t.Id))
                .ToListAsync();

            foreach (var target in targets)
            {
                target.IsEnabled = isEnabled;
            }

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// 批量删除阈值配置
        /// </summary>
        /// <param name="ids">需要删除的配置Id集合</param>
        public async Task BatchDeleteAsync(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0) return;

            using var context = CreateContext();
            var targets = await context.AlarmThresholdConfigs
                .Where(t => ids.Contains(t.Id))
                .ToListAsync();

            context.AlarmThresholdConfigs.RemoveRange(targets);
            await context.SaveChangesAsync();
        }

        public async Task DeleteThresholdConfigAsync(int id)
        {
            using var context = CreateContext();
            var config = await context.AlarmThresholdConfigs.FindAsync(id);
            if (config != null)
            {
                context.AlarmThresholdConfigs.Remove(config);
                await context.SaveChangesAsync();
            }
        }

        public async Task<Dictionary<AlarmLevel, int>> GetLevelDistributionAsync(DateTime? start, DateTime? end)
        {
            using var context = CreateContext();
            var query = context.AlarmRecords.AsQueryable();

            if (start.HasValue)
                query = query.Where(a => a.AlarmTime >= start.Value);
            if (end.HasValue)
                query = query.Where(a => a.AlarmTime <= end.Value);

            return await query
                .GroupBy(a => a.AlarmLevel)
                .ToDictionaryAsync(g => g.Key, g => g.Count());
        }

        public async Task<List<(string Source, int Count)>> GetTopSourcesAsync(int topN, DateTime? start, DateTime? end)
        {
            using var context = CreateContext();
            var query = context.AlarmRecords.AsQueryable();

            if (start.HasValue)
                query = query.Where(a => a.AlarmTime >= start.Value);
            if (end.HasValue)
                query = query.Where(a => a.AlarmTime <= end.Value);

            return await query
                .GroupBy(a => a.AlarmSource)
                .OrderByDescending(g => g.Count())
                .Take(topN)
                .Select(g => new ValueTuple<string, int>(g.Key, g.Count()))
                .ToListAsync();
        }

        public async Task<List<(DateTime Date, int Count)>> GetDailyTrendAsync(int days)
        {
            using var context = CreateContext();
            var startDate = DateTime.Today.AddDays(-days + 1);

            var rawDataList = await context.AlarmRecords
                .Where(a => a.AlarmTime >= startDate)
                .GroupBy(a => a.AlarmTime.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            var rawData = rawDataList.ToDictionary(g => g.Date, g => g.Count);

            var result = new List<(DateTime Date, int Count)>();
            for (int i = 0; i < days; i++)
            {
                var date = startDate.AddDays(i);
                var count = rawData.ContainsKey(date) ? rawData[date] : 0;
                result.Add((date, count));
            }

            return result;
        }

        private IQueryable<AlarmRecord> BuildFilterQuery(AlarmDbContext context, AlarmQueryParams parameters)
        {
            var query = context.AlarmRecords.AsQueryable();

            if (parameters.StartTime.HasValue)
                query = query.Where(a => a.AlarmTime >= parameters.StartTime.Value);
            if (parameters.EndTime.HasValue)
                query = query.Where(a => a.AlarmTime <= parameters.EndTime.Value);
            if (parameters.Level.HasValue)
                query = query.Where(a => a.AlarmLevel == parameters.Level.Value);
            if (!string.IsNullOrWhiteSpace(parameters.Source))
                query = query.Where(a => a.AlarmSource == parameters.Source);
            if (parameters.Status.HasValue)
                query = query.Where(a => a.Status == parameters.Status.Value);
            if (parameters.Type.HasValue)
                query = query.Where(a => a.AlarmType == parameters.Type.Value);
            if (!string.IsNullOrWhiteSpace(parameters.Keyword))
                query = query.Where(a => a.Description.Contains(parameters.Keyword) || a.AlarmCode.Contains(parameters.Keyword));

            return query;
        }
    }
}

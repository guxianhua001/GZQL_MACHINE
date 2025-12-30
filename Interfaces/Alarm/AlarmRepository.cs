using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Interfaces
{
    // Infrastructure/Repositories/AlarmRepository.cs
    public class AlarmRepository : IAlarmRepository
    {
        private readonly AlarmDbContext _context;

        public AlarmRepository(IDbContextFactory<AlarmDbContext> factory)
        {
            _context = factory.CreateDbContext();
        }


        public async Task AddAsync(PersistentAlarm alarm)
        {
            await _context.Alarms.AddAsync(alarm);
            await _context.SaveChangesAsync();
        }

        public async Task BulkInsertAsync(IEnumerable<PersistentAlarm> alarms)
        {
            // 分块处理（防止内存溢出）
            foreach (var chunk in alarms.Chunk(10000))
            {
                var bulkConfig = new BulkConfig
                {
                    BatchSize = 5000,
                    //InsertKeepIdentity = true,
                    SetOutputIdentity = false,
                    PropertiesToExclude = new List<string> { "Duration" },
                    EnableStreaming = true // 允许流式传输数据
                };

                // 自动事务管理（无需显式BeginTransaction）
                await _context.BulkInsertAsync(chunk.ToList(), bulkConfig);
            }
        }

        public async Task ClearAllAsync()
        {
            await _context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE Alarms");
        }

        public async Task<IEnumerable<PersistentAlarm>> GetHistoryAsync(DateTime? start, DateTime? end)
        {
            var query = _context.Alarms.AsNoTracking().OrderByDescending(a => a.Timestamp);

            if (start.HasValue)
                query = (IOrderedQueryable<PersistentAlarm>)query.Where(a => a.Timestamp >= start.Value);

            if (end.HasValue)
                query = (IOrderedQueryable<PersistentAlarm>)query.Where(a => a.Timestamp <= end.Value);

            return await query.Take(10000).ToListAsync(); // 防止内存溢出
        }
    }

}

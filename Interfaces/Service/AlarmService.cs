using Prism.Events;
using System.Collections.Generic;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Concurrent;
using Interfaces;
using System.IO;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Interfaces.Services
{
    public interface IAlarmService
    {
        ObservableCollection<XAlarmEventArgs> ActiveAlarms { get; }
        ObservableCollection<XAlarmEventArgs> HistoricalAlarms { get; }
        void RaiseNewAlarm(XAlarmEventArgs alarm);
        void ClearAllAlarms();
        Task LoadHistoricalAlarmsAsync();
        Task<PagedResult<XAlarmEventArgs>> GetPagedAlarmsAsync(
        int pageNumber,
        int pageSize,
        DateTime? startDate = null,
        DateTime? endDate = null,
        AlarmLevel? level = null,
        int? stationId = null);

        Task DeleteAlarmsAsync(IEnumerable<int> alarmIds);
        Task DeleteAlarmsByDateRangeAsync(DateTime startDate, DateTime endDate);
        Task ExportAlarmsToCsvAsync(string filePath, DateTime? startDate = null, DateTime? endDate = null);
        Task<long> GetExportCountAsync(DateTime? startDate = null, DateTime? endDate = null, AlarmLevel? level = null);
    }
    public class AlarmService : IAlarmService
    {
        private readonly object _lock = new object();
        public ObservableCollection<XAlarmEventArgs> ActiveAlarms { get; }
            = new ObservableCollection<XAlarmEventArgs>();
        public ObservableCollection<XAlarmEventArgs> HistoricalAlarms { get; }
            = new ObservableCollection<XAlarmEventArgs>();

        private readonly IEventAggregator _eventAggregator;
        private readonly IAlarmRepository _repository;
        private readonly IDbContextFactory<AlarmDbContext> _dbContextFactory;

        // 使用Producer-Consumer模式处理批量写入
        private readonly BlockingCollection<PersistentAlarm> _writeQueue = new();
        private bool _isPersisting;
        private object baseQuery;

        // 数据库操作字段
        private const int MaxBatchSize = 1000;
        private readonly SemaphoreSlim _dbOperationLock = new(1, 1);

        public async Task<PagedResult<XAlarmEventArgs>> GetPagedAlarmsAsync(
        int pageNumber,
        int pageSize,
        DateTime? startDate = null,
        DateTime? endDate = null,
        AlarmLevel? level = null,
        int? stationId = null)
        {
            try
            {
                await _dbOperationLock.WaitAsync();
                using var context = _dbContextFactory.CreateDbContext();
                // 步骤1：构建基础查询条件
                var baseQuery = context.Alarms.AsQueryable();
                if (startDate.HasValue)
                    baseQuery = baseQuery.Where(a => a.Timestamp >= startDate.Value);
                if (endDate.HasValue)
                    baseQuery = baseQuery.Where(a => a.Timestamp <= endDate.Value);
                if (level.HasValue)
                    baseQuery = baseQuery.Where(a => a.Level == level.Value);
                if (stationId.HasValue)
                    baseQuery = baseQuery.Where(a => a.StationId == stationId.Value);
                // 步骤2：获取总数
                var totalCount = await baseQuery.CountAsync();
                // 步骤3：计算分页参数
                int startRow = (pageNumber - 1) * pageSize + 1;
                int endRow = pageNumber * pageSize;
                // 步骤4：构建原生SQL分页查询
                var sqlQuery = @"
                                    SELECT * FROM (
                                        SELECT 
                                            *, 
                                            ROW_NUMBER() OVER (ORDER BY Timestamp DESC, Id ASC) AS RowNum
                                        FROM [Alarms] AS [a]
                                        {0}
                                    ) AS Paged
                                    WHERE RowNum BETWEEN @startRow AND @endRow
                                ";
                var whereClauses = new List<string>();
                var parameters = new List<object>();
                // 添加查询条件
                if (startDate.HasValue)
                {
                    whereClauses.Add("Timestamp >= @startDate");
                    parameters.Add(new SqlParameter("startDate", startDate.Value));
                }
                if (endDate.HasValue)
                {
                    whereClauses.Add("Timestamp <= @endDate");
                    parameters.Add(new SqlParameter("endDate", endDate.Value));
                }
                if (level.HasValue)
                {
                    whereClauses.Add("Level = @level");
                    parameters.Add(new SqlParameter("level", (int)level.Value));
                }
                if (stationId.HasValue)
                {
                    whereClauses.Add("StationId = @stationId");
                    parameters.Add(new SqlParameter("stationId", stationId.Value));
                }
                // 组装WHERE子句
                var whereCondition = whereClauses.Any() ? $"WHERE {string.Join(" AND ", whereClauses)}" : "";
                // 最终SQL
                var finalSql = string.Format(sqlQuery, whereCondition);
                IMessage.Logger.Debug($"分页SQL: {finalSql}");
                // 添加分页参数
                parameters.Add(new SqlParameter("startRow", startRow));
                parameters.Add(new SqlParameter("endRow", endRow));
                // 步骤5：执行原生SQL查询
                var alarms = await context.Alarms
                    .FromSqlRaw(finalSql, parameters.ToArray())
                    .AsNoTracking()
                    .ToListAsync();
                // 步骤6：映射结果
                var items = alarms.Select(MapToXAlarmEventArgs).ToList();
                return new PagedResult<XAlarmEventArgs>
                {
                    TotalCount = totalCount,
                    Items = items
                };
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"分页查询报警历史失败: {ex}");
                throw;
            }
            finally
            {
                _dbOperationLock.Release();
            }
        }

        public async Task DeleteAlarmsAsync(IEnumerable<int> alarmIds)
        {
            if (alarmIds == null || !alarmIds.Any())
                return;

            try
            {
                await _dbOperationLock.WaitAsync();
                using var context = _dbContextFactory.CreateDbContext();

                // 分批次删除 (传统方式)
                int totalDeleted = 0;
                const int batchSize = 200;

                var allIds = alarmIds.ToList();
                var totalBatches = (int)Math.Ceiling((double)allIds.Count / batchSize);

                for (int batch = 0; batch < totalBatches; batch++)
                {
                    var idsSegment = allIds.Skip(batch * batchSize).Take(batchSize).ToList();

                    // 使用传统 DELETE FROM WHERE IN 语句
                    var idList = string.Join(",", idsSegment);
                    int deletedCount = await context.Database.ExecuteSqlRawAsync(
                        $"DELETE FROM Alarms WHERE Id IN ({idList})");

                    totalDeleted += deletedCount;

                    // 稍作暂停减少数据库压力
                    await Task.Delay(100);
                }

                IMessage.Logger.Info($"已删除 {totalDeleted} 条报警记录");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"删除指定报警记录失败: {ex}");
                throw;
            }
            finally
            {
                _dbOperationLock.Release();
            }
        }

        public async Task<long> GetExportCountAsync(DateTime? startDate = null, DateTime? endDate = null, AlarmLevel? level = null)
        {
            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                var query = context.Alarms.AsQueryable();

                if (startDate.HasValue)
                    query = query.Where(a => a.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(a => a.Timestamp <= endDate.Value);

                if (level.HasValue)
                    query = query.Where(a => a.Level == level.Value);

                return await query.LongCountAsync();
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"获取导出数量失败: {ex}");
                // 返回保守估计值
                return 1000;
            }
        }

        public async Task DeleteAlarmsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                await _dbOperationLock.WaitAsync();
                using var context = _dbContextFactory.CreateDbContext();

                // 使用原始 SQL 避免使用 ExecuteDeleteAsync
                var sql = $"DELETE FROM Alarms WHERE Timestamp >= @start AND Timestamp <= @end";

                var startParam = new SqlParameter("@start", startDate);
                var endParam = new SqlParameter("@end", endDate);

                int deletedCount = await context.Database.ExecuteSqlRawAsync(sql, startParam, endParam);

                IMessage.Logger.Info($"删除日期范围 {startDate:yyyy-MM-dd} 至 {endDate:yyyy-MM-dd} 的报警记录: {deletedCount} 条");

                // 更新本地历史报警
                await LoadHistoricalAlarmsAsync();
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"按日期范围删除报警失败: {ex}");
                throw;
            }
            finally
            {
                _dbOperationLock.Release();
            }
        }
        public async Task ExportAlarmsToCsvAsync(string filePath, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                using var context = _dbContextFactory.CreateDbContext();
                var query = context.Alarms.AsQueryable();

                if (startDate.HasValue)
                    query = query.Where(a => a.Timestamp >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(a => a.Timestamp <= endDate.Value);

                // 一次性加载所有数据
                var alarms = await query
                    .OrderBy(a => a.Timestamp)
                    .AsNoTracking()
                    .ToListAsync();

                using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
                await writer.WriteLineAsync("Id,Timestamp,StationId,Code,Level,Category,Description");

                foreach (var alarm in alarms)
                {
                    await writer.WriteLineAsync(FormatAlarmForCsv(alarm));
                }

                IMessage.Logger.Info($"成功导出 {alarms.Count} 条报警记录到 {filePath}");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"导出报警记录到CSV失败: {ex}");
                throw;
            }
        }


        private string FormatAlarmForCsv(PersistentAlarm alarm)
        {
            return $"{alarm.Id}," +
                   $"{alarm.Timestamp:yyyy-MM-ddTHH:mm:ss.fffffff}," +
                   $"{alarm.StationId}," +
                   $"{alarm.Code}," +
                   $"{alarm.Level}," +
                   $"{EscapeCsvField(alarm.Category)}," +
                   $"{EscapeCsvField(alarm.Description)}";
        }

        // 工具方法：CSV字段转义
        private string EscapeCsvField(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            {
                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            return value;
        }

        // 映射方法优化
        private XAlarmEventArgs MapToXAlarmEventArgs(PersistentAlarm alarm)
        {
            if (!int.TryParse(alarm.Code, out var codeVal))
            {
                codeVal = 0;
                IMessage.Logger.Warn($"警报编码格式错误: {alarm.Code}");
            }
            return new XAlarmEventArgs(
                intvalue: (int)alarm.Id, // 使用数据库ID
                code: codeVal,
                category: alarm.Category ?? "未分类",
                description: alarm.Description ?? "无描述")
            {
                StationId = alarm.StationId,
                StartTime = alarm.OriginalRawTime,
                AlarmLevel = (int)alarm.Level
            };
        }

        public AlarmService(IEventAggregator eventAggregator,
            IAlarmRepository repository,
            IDbContextFactory<AlarmDbContext> dbContextFactory)
        {
            _eventAggregator = eventAggregator;
            _repository = repository;
            _dbContextFactory = dbContextFactory;

            // 初始化后台持久化任务
            Task.Run(ProcessWriteQueue);

            TestConnection();
        }

        private async Task ProcessWriteQueue()
        {
            const int batchSize = 1000;
            var buffer = new List<PersistentAlarm>(batchSize);

            while (!_writeQueue.IsCompleted)
            {
                while (buffer.Count < batchSize && _writeQueue.TryTake(out var item))
                {
                    buffer.Add(item);
                }

                if (buffer.Count > 0)
                {
                    try
                    {
                        await _repository.BulkInsertAsync(buffer);
                        IMessage.Logger.Info($"警告批量插入成功，数量: {buffer.Count}");
                        buffer.Clear();
                    }
                    catch (Exception ex)
                    {
                        var sample = buffer.Take(3).Select(a =>
                                   $"Code={a.Code}, Time={a.Timestamp:HH:mm:ss}"
                                   );
                        IMessage.Logger.Error(
                            $"批量插入失败！样本: [{string.Join("; ", sample)}] | " +
                            $"错误: {ex.Message.Substring(0, 50)}..." + ex
                        );
                        // 写入失败时保留数据
                        File.AppendAllText("alarm_fallback.log",
                            $"{DateTime.UtcNow:o}|{ex.Message}|{ex.StackTrace}\n");
                        buffer.ForEach(a => _writeQueue.Add(a));
                        buffer.Clear();
                        await Task.Delay(5000);
                    }
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }
        public void RaiseNewAlarm(XAlarmEventArgs alarm)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_lock)
                {
                    ActiveAlarms.Add(alarm);

                    // 构建持久化实体
                    var persistentAlarm = new PersistentAlarm
                    {
                        Timestamp = DateTime.Now,
                        StationId = alarm.StationId,
                        Code = alarm.Code.ToString(),
                        Level = (AlarmLevel)alarm.AlarmLevel,
                        Category = alarm.Category,
                        Description = alarm.Description,
                        OriginalRawTime = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffffff")
                    };

                    _writeQueue.Add(persistentAlarm);
                }
            });
        }
        public async Task LoadHistoricalAlarmsAsync()
        {
            try
            {
                var lastWeek = DateTime.UtcNow.AddDays(-7);
                IMessage.Logger.Debug($"正在查询最近一周的报警数据，起始时间: {lastWeek:yyyy-MM-dd}");
                // 查询数据库
                var alarms = await _repository.GetHistoryAsync(lastWeek, null);
                var alarmList = alarms?.ToList() ?? new List<PersistentAlarm>();
                IMessage.Logger.Info($"已接收{alarmList.Count}条原始记录");
                if (alarmList.Count == 0)
                {
                    IMessage.Logger.Warn("无历史警报数据");
                    return;
                }
                // 调试日志：检查数据源字段值
                alarmList.ForEach(a => Debug.WriteLine(
                    $"DB记录: Desc='{a.Description}', Time='{a.OriginalRawTime}'")
                );

                // 数据映射
                var mappedAlarms = alarms.Select(MapToXAlarmEventArgs).ToList();
                IMessage.Logger.Info($"成功映射 {mappedAlarms.Count} 条警报");
                // UI线程更新集合
                Application.Current.Dispatcher.Invoke(() =>
                {
                    HistoricalAlarms.Clear();
                    foreach (var alarm in mappedAlarms)
                    {
                        HistoricalAlarms.Add(alarm);
                    }
                    IMessage.Logger.Info($"已加载 {HistoricalAlarms.Count} 条历史警报到内存");
                });
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"加载历史报警异常: {ex.Message}");
                _eventAggregator.GetEvent<SystemErrorEvent>()
                    .Publish($"历史数据加载失败: {ex.Message}");
            }
        }
        public async void ClearAllAlarms()
        {
            try
            {
                // 清空本地缓存
                lock (_lock)
                {
                    ActiveAlarms.Clear();
                    HistoricalAlarms.Clear();
                }
                // 清空数据库（使用ExecuteDeleteAsync批量删除）
              await Task.Run(async () =>
                {
                    try
                    {
                        await _dbOperationLock.WaitAsync();

                        using var context = _dbContextFactory.CreateDbContext();

                        // 删除全部记录
                        int deleteCount = await context.Alarms.ExecuteDeleteAsync();

                        IMessage.Logger.Info($"已清空 {deleteCount} 条报警记录");

                        // 清理残留日志文件
                        if (File.Exists("alarm_fallback.log"))
                        {
                            File.Delete("alarm_fallback.log");
                        }
                    }
                    catch (Exception ex)
                    {
                        IMessage.Logger.Error($"清空报警失败: {ex}");
                        _eventAggregator.GetEvent<SystemErrorEvent>().Publish(
                            $"清除报警失败: {ex.Message}");
                    }
                    finally
                    {
                        _dbOperationLock.Release();
                    }
                });
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"清除本地报警缓存失败: {ex}");
            }

        }
        // AlarmService中添加
        private void HandlePersistenceFailure(IEnumerable<PersistentAlarm> failedAlarms)
        {
            try
            {
                var fallbackPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "SmartMotion/AlarmFallback");

                Directory.CreateDirectory(fallbackPath);
                File.AppendAllLines(
                    Path.Combine(fallbackPath, $"{DateTime.Now:yyyyMMdd}.alarmlog"),
                    failedAlarms.Select(a => JsonSerializer.Serialize(a))
                );
            }
            catch
            {
                // 最后保障
                Debug.WriteLine("全量报警持久化失败！");
            }
        }
        public void TestConnection()
        {
            try
            {
                using (var context = _dbContextFactory.CreateDbContext())
                {
                    bool canConnect = context.Database.CanConnect();
                    Debug.WriteLine($"1. 基础连接状态: {canConnect}");
                    IMessage.Logger.Info($"数据库连接状态: {canConnect}");
                    // 若基础连接成功但数据库不可用会有不同提示
                    if (!canConnect)
                    {
                        Debug.WriteLine("2. 尝试检查服务器版本...");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"3. 连接错误类型: {ex.GetType().Name}");
                Debug.WriteLine($"4. 完整错误信息: {ex.Message}");
                if (ex.InnerException != null)
                    Debug.WriteLine($"5. 内部异常: {ex.InnerException.Message}");
            }
        }


        private void PublishAlarmEvent(XAlarmEventArgs alarm)
        {
            _eventAggregator.GetEvent<AlarmRaisedEvent>().Publish(alarm);
        }

    }
    public class AlarmRaisedEvent : PubSubEvent<XAlarmEventArgs> { }
    public class SystemErrorEvent : PubSubEvent<string> { }

}

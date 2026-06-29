using AlarmModule.Interfaces;
using AlarmModule.Models;
using ClosedXML.Excel;
using Core.Abstraction;
using Core.Utilities;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AlarmModule.Services
{
    /// <summary>
    /// 报警服务实现：提供报警触发、生命周期管理、查询导出等核心功能
    /// 支持防抖抑制、状态流转校验、活跃报警实时集合
    /// </summary>
    public class AlarmService : IAlarmService
    {
        private readonly IAlarmRepository _repository;
        private readonly IAlarmNotificationService _notificationService;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;

        /// <summary>
        /// 当前活跃报警集合（Status != Eliminated）
        /// </summary>
        public ObservableCollection<AlarmRecord> ActiveAlarms { get; } = new ObservableCollection<AlarmRecord>();

        /// <summary>
        /// 未确认报警数量
        /// </summary>
        public int UnconfirmedCount => ActiveAlarms.Count(a => a.Status == AlarmStatus.Unconfirmed);

        /// <summary>
        /// 报警触发事件
        /// </summary>
        public event Action<AlarmRecord>? AlarmTriggered;

        /// <summary>
        /// 构造函数：注入仓储、通知服务、日志服务、本地化服务，并初始化活跃报警列表
        /// </summary>
        public AlarmService(IAlarmRepository repository, IAlarmNotificationService notificationService, ILoggerService logger, ILocalizationService localization)
        {
            _repository = repository;
            _notificationService = notificationService;
            _logger = logger;
            _localization = localization;
            InitializeActiveAlarmsAsync();
        }

        /// <summary>
        /// 单行代码触发报警并持久化到数据库
        /// 支持防抖：相同Code+Source在配置时间窗口内不重复触发
        /// </summary>
        public async Task TriggerAlarmAsync(string alarmCode, AlarmLevel level, string description,
            string source = "", AlarmType type = AlarmType.HardwareFault,
            double? triggerValue = null, double? thresholdValue = null)
        {
            var thresholdConfig = await _repository.GetThresholdConfigAsync(alarmCode, source);
            var suppressionWindow = thresholdConfig?.SuppressionWindowSeconds ?? 60;

            var recentAlarm = await _repository.FindRecentAsync(alarmCode, source, TimeSpan.FromSeconds(suppressionWindow));
            if (recentAlarm != null && recentAlarm.Status != AlarmStatus.Eliminated)
            {
                recentAlarm.AlarmTime = DateTime.Now;
                await _repository.UpdateAsync(recentAlarm);
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("Alarm_Log_DebounceSuppressed", "报警防抖抑制：{0}@{1}，更新时间而非重复创建"), alarmCode, source));
                return;
            }

            var record = new AlarmRecord
            {
                AlarmTime = DateTime.Now,
                AlarmLevel = level,
                AlarmCode = alarmCode,
                AlarmSource = source,
                AlarmType = type,
                Description = description,
                TriggerValue = triggerValue,
                ThresholdValue = thresholdValue,
                Status = AlarmStatus.Unconfirmed
            };

            await _repository.AddAsync(record);

            InvokeOnUIThread(() =>
            {
                ActiveAlarms.Add(record);
            });

            AlarmTriggered?.Invoke(record);
            _notificationService.ShowNotification(record);
            _logger.Warn(string.Format(_localization.GetResourceOrDefault("Alarm_Log_Triggered", "报警触发：[{0}] {1}@{2} - {3}"), level, alarmCode, source, description));
        }

        /// <summary>
        /// 确认单条报警：仅Unconfirmed状态可确认
        /// </summary>
        public async Task ConfirmAsync(long alarmId, string confirmedBy)
        {
            var record = await _repository.GetByIdAsync(alarmId);
            if (record == null)
                throw new InvalidOperationException($"报警记录不存在：Id={alarmId}");
            if (record.Status != AlarmStatus.Unconfirmed)
                throw new InvalidOperationException($"仅未确认报警可确认，当前状态：{record.Status}");

            record.Status = AlarmStatus.Confirmed;
            record.ConfirmedBy = confirmedBy;
            record.ConfirmedTime = DateTime.Now;
            await _repository.UpdateAsync(record);

            UpdateActiveAlarm(record);
            _logger.Info(string.Format(_localization.GetResourceOrDefault("Alarm_Log_Confirmed", "报警确认：Id={0}，操作人={1}"), alarmId, confirmedBy));
        }

        /// <summary>
        /// 复位单条报警：仅Confirmed状态可复位
        /// </summary>
        public async Task ResetAsync(long alarmId, string resetBy)
        {
            var record = await _repository.GetByIdAsync(alarmId);
            if (record == null)
                throw new InvalidOperationException($"报警记录不存在：Id={alarmId}");
            if (record.Status != AlarmStatus.Confirmed)
                throw new InvalidOperationException($"仅已确认报警可复位，当前状态：{record.Status}");

            record.Status = AlarmStatus.Reset;
            record.ResetBy = resetBy;
            record.ResetTime = DateTime.Now;
            await _repository.UpdateAsync(record);

            UpdateActiveAlarm(record);
            _logger.Info(string.Format(_localization.GetResourceOrDefault("Alarm_Log_Reset", "报警复位：Id={0}，操作人={1}"), alarmId, resetBy));
        }

        /// <summary>
        /// 消除单条报警：Unconfirmed/Confirmed/Reset状态均可消除
        /// </summary>
        public async Task EliminateAsync(long alarmId)
        {
            var record = await _repository.GetByIdAsync(alarmId);
            if (record == null)
                throw new InvalidOperationException($"报警记录不存在：Id={alarmId}");
            if (record.Status == AlarmStatus.Eliminated)
                throw new InvalidOperationException($"报警已消除，无需重复操作");

            record.Status = AlarmStatus.Eliminated;
            await _repository.UpdateAsync(record);

            InvokeOnUIThread(() =>
            {
                var existing = ActiveAlarms.FirstOrDefault(a => a.Id == alarmId);
                if (existing != null)
                    ActiveAlarms.Remove(existing);
            });

            _logger.Info(string.Format(_localization.GetResourceOrDefault("Alarm_Log_Eliminated", "报警消除：Id={0}"), alarmId));
        }

        /// <summary>
        /// 确认所有未确认报警
        /// </summary>
        public async Task ConfirmAllAsync(string confirmedBy)
        {
            await _repository.ConfirmAllAsync(confirmedBy);

            InvokeOnUIThread(() =>
            {
                foreach (var alarm in ActiveAlarms.Where(a => a.Status == AlarmStatus.Unconfirmed))
                {
                    alarm.Status = AlarmStatus.Confirmed;
                    alarm.ConfirmedBy = confirmedBy;
                    alarm.ConfirmedTime = DateTime.Now;
                }
            });

            _logger.Info(string.Format(_localization.GetResourceOrDefault("Alarm_Log_ConfirmAll", "批量确认所有未确认报警，操作人={0}"), confirmedBy));
        }

        /// <summary>
        /// 复位所有已确认报警
        /// </summary>
        public async Task ResetAllAsync(string resetBy)
        {
            await _repository.ResetAllAsync(resetBy);

            InvokeOnUIThread(() =>
            {
                foreach (var alarm in ActiveAlarms.Where(a => a.Status == AlarmStatus.Confirmed))
                {
                    alarm.Status = AlarmStatus.Reset;
                    alarm.ResetBy = resetBy;
                    alarm.ResetTime = DateTime.Now;
                }
            });

            _logger.Info(string.Format(_localization.GetResourceOrDefault("Alarm_Log_ResetAll", "批量复位所有已确认报警，操作人={0}"), resetBy));
        }

        /// <summary>
        /// 分页查询报警记录
        /// </summary>
        public async Task<PagedResult<AlarmRecord>> QueryAsync(AlarmQueryParams parameters)
        {
            return await _repository.QueryAsync(parameters);
        }

        /// <summary>
        /// 导出报警数据到Excel：使用ClosedXML生成包含完整报警信息的xlsx文件
        /// </summary>
        public async Task ExportToExcelAsync(string filePath, AlarmQueryParams parameters)
        {
            var alarms = await _repository.GetAlarmsForExportAsync(parameters);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("报警记录");

            var headers = new[]
            {
                "Id", "报警时间", "报警等级", "报警代码", "报警来源",
                "报警类型", "描述", "触发值", "阈值", "状态",
                "确认人", "确认时间", "复位人", "复位时间", "处理备注"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            for (int i = 0; i < alarms.Count; i++)
            {
                var a = alarms[i];
                int row = i + 2;
                worksheet.Cell(row, 1).Value = a.Id;
                worksheet.Cell(row, 2).Value = a.AlarmTime.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cell(row, 3).Value = a.AlarmLevel.ToString();
                worksheet.Cell(row, 4).Value = a.AlarmCode;
                worksheet.Cell(row, 5).Value = a.AlarmSource;
                worksheet.Cell(row, 6).Value = a.AlarmType.ToString();
                worksheet.Cell(row, 7).Value = a.Description;
                worksheet.Cell(row, 8).Value = a.TriggerValue?.ToString() ?? "";
                worksheet.Cell(row, 9).Value = a.ThresholdValue?.ToString() ?? "";
                worksheet.Cell(row, 10).Value = a.Status.ToString();
                worksheet.Cell(row, 11).Value = a.ConfirmedBy ?? "";
                worksheet.Cell(row, 12).Value = a.ConfirmedTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
                worksheet.Cell(row, 13).Value = a.ResetBy ?? "";
                worksheet.Cell(row, 14).Value = a.ResetTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
                worksheet.Cell(row, 15).Value = a.ProcessingNotes ?? "";
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(filePath);

            _logger.Info(string.Format(_localization.GetResourceOrDefault("Alarm_Log_ExportedToExcel", "报警数据已导出到Excel：{0}，共{1}条记录"), filePath, alarms.Count));
        }

        /// <summary>
        /// 刷新活跃报警列表：从数据库重新加载所有未消除的报警
        /// </summary>
        public async Task RefreshActiveAlarmsAsync()
        {
            var activeAlarms = await _repository.GetActiveAlarmsAsync();

            InvokeOnUIThread(() =>
            {
                ActiveAlarms.Clear();
                foreach (var alarm in activeAlarms)
                {
                    ActiveAlarms.Add(alarm);
                }
            });

            _logger.Info(string.Format(_localization.GetResourceOrDefault("Alarm_Log_ActiveListRefreshed", "活跃报警列表已刷新，共{0}条"), activeAlarms.Count));
        }

        /// <summary>
        /// 初始化活跃报警列表：从数据库加载所有未消除的报警
        /// </summary>
        private async void InitializeActiveAlarmsAsync()
        {
            try
            {
                await RefreshActiveAlarmsAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localization.GetResourceOrDefault("Alarm_Log_InitActiveListFailed", "初始化活跃报警列表失败：{0}"), ex.Message));
            }
        }

        /// <summary>
        /// 更新活跃报警集合中的单条记录
        /// </summary>
        private void UpdateActiveAlarm(AlarmRecord updated)
        {
            InvokeOnUIThread(() =>
            {
                var existing = ActiveAlarms.FirstOrDefault(a => a.Id == updated.Id);
                if (existing != null)
                {
                    var index = ActiveAlarms.IndexOf(existing);
                    ActiveAlarms[index] = updated;
                }
            });
        }

        /// <summary>
        /// 安全的 UI 线程调度：检查 Dispatcher 是否可用，避免应用关闭时抛出 TaskCanceledException
        /// </summary>
        private static void InvokeOnUIThread(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted)
                return;

            try
            {
                dispatcher.Invoke(action);
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
        }
    }
}

using AlarmModule.Interfaces;
using AlarmModule.Models;
using Core.Abstraction;
using Core.Utilities;
using Framework.Mvvm;
using Microsoft.Win32;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AlarmModule.ViewModels
{
    /// <summary>
    /// 报警历史查询视图模型：提供多条件查询、分页浏览、Excel导出功能
    /// </summary>
    public class AlarmHistoryViewModel : ViewModelBase
    {
        private readonly IAlarmService _alarmService;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;

        private DateTime? _startTime = DateTime.Today.AddDays(-7);
        private DateTime? _endTime = DateTime.Now;
        private AlarmLevel? _selectedLevel;
        private string _sourceFilter = string.Empty;
        private AlarmStatus? _selectedStatus;
        private AlarmType? _selectedType;
        private string _keyword = string.Empty;
        private PagedResult<AlarmRecord> _queryResult = new PagedResult<AlarmRecord>();
        private int _currentPage = 1;

        /// <summary>
        /// 查询开始时间
        /// </summary>
        public DateTime? StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        /// <summary>
        /// 查询结束时间
        /// </summary>
        public DateTime? EndTime
        {
            get => _endTime;
            set => SetProperty(ref _endTime, value);
        }

        /// <summary>
        /// 选中的报警等级筛选条件
        /// </summary>
        public AlarmLevel? SelectedLevel
        {
            get => _selectedLevel;
            set => SetProperty(ref _selectedLevel, value);
        }

        /// <summary>
        /// 报警来源筛选关键字
        /// </summary>
        public string SourceFilter
        {
            get => _sourceFilter;
            set => SetProperty(ref _sourceFilter, value);
        }

        /// <summary>
        /// 选中的报警状态筛选条件
        /// </summary>
        public AlarmStatus? SelectedStatus
        {
            get => _selectedStatus;
            set => SetProperty(ref _selectedStatus, value);
        }

        /// <summary>
        /// 选中的报警类型筛选条件
        /// </summary>
        public AlarmType? SelectedType
        {
            get => _selectedType;
            set => SetProperty(ref _selectedType, value);
        }

        /// <summary>
        /// 搜索关键字
        /// </summary>
        public string Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        /// <summary>
        /// 查询结果（分页）
        /// </summary>
        public PagedResult<AlarmRecord> QueryResult
        {
            get => _queryResult;
            set => SetProperty(ref _queryResult, value);
        }

        /// <summary>
        /// 当前页码
        /// </summary>
        public int CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        /// <summary>
        /// 总页数
        /// </summary>
        public int TotalPages => QueryResult.TotalPages;

        /// <summary>
        /// 报警等级选项列表（含空选项表示"全部"）
        /// </summary>
        public List<AlarmLevel?> AlarmLevels { get; } =
            new List<AlarmLevel?> { null, AlarmLevel.Emergency, AlarmLevel.Serious, AlarmLevel.General, AlarmLevel.Prompt };

        /// <summary>
        /// 报警状态选项列表（含空选项表示"全部"）
        /// </summary>
        public List<AlarmStatus?> AlarmStatuses { get; } =
            new List<AlarmStatus?> { null, AlarmStatus.Unconfirmed, AlarmStatus.Confirmed, AlarmStatus.Reset, AlarmStatus.Eliminated };

        /// <summary>
        /// 报警类型选项列表（含空选项表示"全部"）
        /// </summary>
        public List<AlarmType?> AlarmTypes { get; } =
            new List<AlarmType?> { null, AlarmType.HardwareFault, AlarmType.ParameterOutOfLimit, AlarmType.CommunicationError, AlarmType.ProcessError };

        /// <summary>
        /// 查询命令
        /// </summary>
        public DelegateCommand QueryCommand { get; }

        /// <summary>
        /// 导出Excel命令
        /// </summary>
        public DelegateCommand ExportCommand { get; }

        /// <summary>
        /// 上一页命令
        /// </summary>
        public DelegateCommand PreviousPageCommand { get; }

        /// <summary>
        /// 下一页命令
        /// </summary>
        public DelegateCommand NextPageCommand { get; }

        /// <summary>
        /// 构造函数：注入报警服务、日志服务、本地化服务，初始化命令
        /// </summary>
        public AlarmHistoryViewModel(IAlarmService alarmService, ILoggerService logger, ILocalizationService localization)
        {
            _alarmService = alarmService;
            _logger = logger;
            _localization = localization;

            QueryCommand = new DelegateCommand(OnQuery);
            ExportCommand = new DelegateCommand(OnExport);
            PreviousPageCommand = new DelegateCommand(OnPreviousPage, () => CurrentPage > 1);
            NextPageCommand = new DelegateCommand(OnNextPage, () => CurrentPage < TotalPages);
        }

        /// <summary>
        /// 执行查询：根据筛选条件分页查询报警记录
        /// </summary>
        private async void OnQuery()
        {
            try
            {
                CurrentPage = 1;
                await ExecuteQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localization.GetResourceOrDefault("AlarmHist_Log_QueryFailed", "查询报警历史失败：{0}"), ex.Message));
            }
        }

        /// <summary>
        /// 导出Excel：弹出文件保存对话框，将查询结果导出为xlsx文件
        /// </summary>
        private async void OnExport()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Excel文件|*.xlsx",
                    FileName = $"报警记录_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    var parameters = BuildQueryParams();
                    await _alarmService.ExportToExcelAsync(dialog.FileName, parameters);
                    _logger.Info(string.Format(_localization.GetResourceOrDefault("AlarmHist_Log_Exported", "报警数据已导出到：{0}"), dialog.FileName));
                }
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localization.GetResourceOrDefault("AlarmHist_Log_ExportFailed", "导出Excel失败：{0}"), ex.Message));
            }
        }

        /// <summary>
        /// 上一页
        /// </summary>
        private async void OnPreviousPage()
        {
            try
            {
                if (CurrentPage > 1)
                {
                    CurrentPage--;
                    await ExecuteQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localization.GetResourceOrDefault("AlarmHist_Log_PaginationFailed", "翻页失败：{0}"), ex.Message));
            }
        }

        /// <summary>
        /// 下一页
        /// </summary>
        private async void OnNextPage()
        {
            try
            {
                if (CurrentPage < TotalPages)
                {
                    CurrentPage++;
                    await ExecuteQueryAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localization.GetResourceOrDefault("AlarmHist_Log_PaginationFailed", "翻页失败：{0}"), ex.Message));
            }
        }

        /// <summary>
        /// 执行分页查询
        /// </summary>
        private async System.Threading.Tasks.Task ExecuteQueryAsync()
        {
            var parameters = BuildQueryParams();
            QueryResult = await _alarmService.QueryAsync(parameters);
            RaisePropertyChanged(nameof(TotalPages));
            PreviousPageCommand.RaiseCanExecuteChanged();
            NextPageCommand.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 构建查询参数
        /// </summary>
        private AlarmQueryParams BuildQueryParams()
        {
            return new AlarmQueryParams
            {
                PageNumber = CurrentPage,
                PageSize = 50,
                StartTime = StartTime,
                EndTime = EndTime,
                Level = SelectedLevel,
                Source = string.IsNullOrWhiteSpace(SourceFilter) ? null : SourceFilter,
                Status = SelectedStatus,
                Type = SelectedType,
                Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword
            };
        }
    }
}

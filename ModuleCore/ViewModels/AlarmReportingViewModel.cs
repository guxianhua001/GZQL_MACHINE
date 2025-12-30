using Interfaces;
using Interfaces.Models;
using Interfaces.Services;
using Interfaces.Views;
using Microsoft.Win32;
using ModuleCore.Common.Authority;
using ModuleCore.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using SmarterMotion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ModuleCore.ViewModels
{
    public class AlarmReportingViewModel : BindableBase
    {
        private LoginModel _loginModel { get; set; }
        private readonly IAlarmService _alarmService;
        private readonly IEventAggregator _eventAggregator;
        private readonly EquipmentStatus _equipmentStatus;
        public ObservableCollection<AlarmItemViewModel> Alarms { get; }
            = new ObservableCollection<AlarmItemViewModel>();
        public AlarmReportingViewModel(IEventAggregator eventAggregator,LoginModel loginModel, IAlarmService alarmService, EquipmentStatus equipmentStatus)
        {
            _eventAggregator = eventAggregator;
            _loginModel = loginModel;
            _alarmService = alarmService;
            _equipmentStatus = equipmentStatus;
            // 初始化命令
            LoadAlarmsCommand = new DelegateCommand(async () => await LoadAlarms());
            QueryCommand = new DelegateCommand(async () =>
            {
                CurrentPage = 1;
                await LoadAlarms();
            });

            PreviousPageCommand = new DelegateCommand(
                executeMethod: () => NavigatePage(-1),
                canExecuteMethod: () => HasPreviousPage
            );

            NextPageCommand = new DelegateCommand(
                executeMethod: () => NavigatePage(1),
                canExecuteMethod: () => HasNextPage
            );

            ClearCommand = new DelegateCommand(
                executeMethod: OnClear,
                canExecuteMethod: () => CanClear
            );

            DeleteSelectedCommand = new DelegateCommand(
                executeMethod: DeleteSelectedAlarms,
                canExecuteMethod: () => CanClear);

            DeleteAllCommand = new DelegateCommand(
                executeMethod: DeleteAllAlarms,
                canExecuteMethod: () => CanClear);

            ExportCsvCommand = new DelegateCommand(
                () => { ExportToCsv(); });

            // 添加选择更改命令的占位符
            SelectionChangedCommand = new DelegateCommand<object>(_ => { });
            SyncSelectedItemsCommand = new DelegateCommand<IList>(SyncSelectedItems);
            // 监听登录模型变化
            _loginModel.PropertyChanged += LoginModel_PropertyChanged;

            // 订阅集合变更事件
            _alarmService.HistoricalAlarms.CollectionChanged += OnHistoricalAlarmsChanged;

            // 初始化 SelectedLevel 为默认值
            SelectedLevel = AlarmLevels[0];

            // 初始加载
            //Task.Run(LoadInitialData);

        }
        private async void NavigatePage(int direction)
        {
            CurrentPage += direction;
            await LoadAlarms();
        }
        private async Task LoadInitialData()
        {
            try
            {
                //await _alarmService.LoadHistoricalAlarmsAsync();
                await LoadAlarms();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"初始化失败: {ex}");
            }
        }

        private void LoginModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LoginModel.LoginUser) ||
                e.PropertyName == nameof(LoginModel.HasPermission))
            {
                IsAdmin = _loginModel.HasPermission(Authority.Administrator);
                ClearCommand.RaiseCanExecuteChanged();
                DeleteSelectedCommand.RaiseCanExecuteChanged();
                DeleteAllCommand.RaiseCanExecuteChanged();
                // 当权限变化时更新选择状态
                if (!IsAdmin && SelectedAlarms.Any())
                {
                    SelectedAlarms.Clear();
                    SelectedItems = null;
                }
            }
        }
        private void OnHistoricalAlarmsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add ||
                e.Action == NotifyCollectionChangedAction.Reset)
            {
                Application.Current.Dispatcher.BeginInvoke(() => LoadAlarmsCommand.Execute());
            }
        }
        private async Task LoadAlarms()
        {
            IsLoading = true;
            try
            {
                DateTime? endDate = EndDate?.Date.AddDays(1).AddMilliseconds(-1); // 包含整日

                var pagedResult = await _alarmService.GetPagedAlarmsAsync(
                    pageNumber: CurrentPage,
                    pageSize: PageSize,
                    startDate: StartDate,
                    endDate: endDate,
                    level: SelectedLevel?.Key);

                // 更新分页信息
                TotalCount = pagedResult.TotalCount;
                RaisePropertyChanged(nameof(HasPreviousPage));
                RaisePropertyChanged(nameof(HasNextPage));
                RaisePropertyChanged(nameof(TotalPages));

                // 更新命令状态
                PreviousPageCommand.RaiseCanExecuteChanged();
                NextPageCommand.RaiseCanExecuteChanged();

                // 更新UI集合
                Alarms.Clear();
                SelectedAlarms.Clear(); // 清除当前选择
                SelectedItems = null; // 清除UI绑定
                int startIndex = (CurrentPage - 1) * PageSize + 1;

                foreach (var alarm in pagedResult.Items)
                {
                    // 将DateTime转换为指定格式的字符串
                    string formattedTime = alarm.StartTime;

                    Alarms.Add(new AlarmItemViewModel
                    {
                        Id = alarm.Id,
                        Index = startIndex++,
                        StartTime = formattedTime, // 保持为string类型
                        StationId = alarm.StationId,
                        Code = alarm.Code,
                        AlarmLevel = GetLevelText(alarm.AlarmLevel),
                        Category = alarm.Category,
                        Description = alarm.Description,
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载报警失败: {ex}");
                _eventAggregator?.GetEvent<SystemErrorEvent>()?
                    .Publish($"加载报警失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }


        private bool _isAdmin;
        public bool IsAdmin
        {
            get => _loginModel?.HasPermission(Authority.Administrator) ?? false;
            private set
            {
                if (SetProperty(ref _isAdmin, value))
                {
                    // 当管理员状态变化时，通知CanClear更新
                    RaisePropertyChanged(nameof(CanClear));
                }
            }
        }
        private bool CanClear =>
             IsAdmin && _equipmentStatus.CurrentState == EquipmentState.Idle;

        private void OnClear()
        {
            if (!CanClear) return;

            // 调用服务方法
            _alarmService.ClearAllAlarms();
        }
        private string GetLevelText(int level) => level switch
        {
            0 => "信息",  //ONLYLOG
            1 => "普通",  //TIP
            2 => "严重",  //PAUSE
            3 => "紧急",  //STOP
            _ => "未知"
        };
        private DateTime? SafeParseDate(string dateString)
        {
            if (string.IsNullOrWhiteSpace(dateString))
            {
                IMessage.Logger.Warn("时间字符串为空！");
                return null;
            }

            // 定义优先级排序的格式列表
            var formats = new[]
            {
                // 严格匹配 ISO 8601（1~7位小数秒）
                @"yyyy-MM-ddTHH:mm:ss.FFFFFFF", 
                // 兼容历史数据中的其他格式
                "yyyy/MM/dd HH:mm:ss:ffff", // 原始硬件格式（4位小数）
                "yyyy-MM-dd HH:mm:ss.fff",  // 常规日志格式（3位小数）
                "yyyyMMddHHmmssffff"        // 紧急无分隔格式
            };

            // 尝试解析（兼容不同小数点长度）
            if (DateTime.TryParseExact(
                dateString,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
                out var result))
            {
                return result;
            }

            // 更详细的错误日志
            IMessage.Logger.Error($"时间字符串无法解析: {dateString} | 支持的格式: {string.Join(", ", formats)}");
            return null;
        }

        // 分页相关属性
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        // 过滤条件
        private DateTime? _startDate = DateTime.Now.AddDays(-7);
        public DateTime? StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }
        private DateTime? _endDate = DateTime.Now;
        public DateTime? EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        // 报警等级选项
        public class AlarmLevelOption
        {
            public AlarmLevel? Key { get; set; }
            public string Value { get; set; }
        }

        public List<AlarmLevelOption> AlarmLevels { get; } = new List<AlarmLevelOption>
        {
            new AlarmLevelOption { Key = null, Value = "全部" },
            new AlarmLevelOption { Key = AlarmLevel.Normal, Value = "信息" },
            new AlarmLevelOption { Key = AlarmLevel.Severe, Value = "警告" },
            new AlarmLevelOption { Key = AlarmLevel.Critical, Value = "严重" }
        };

        private AlarmLevelOption _selectedLevel;
        public AlarmLevelOption SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                if (SetProperty(ref _selectedLevel, value))
                {
                    // 当筛选条件变化时重新加载数据
                    QueryCommand.Execute();
                }
            }
        }
        // 选中的报警项集合
        private ObservableCollection<AlarmItemViewModel> _selectedAlarms =
         new ObservableCollection<AlarmItemViewModel>();
        public ObservableCollection<AlarmItemViewModel> SelectedAlarms
        {
            get => _selectedAlarms;
            set
            {
                if (SetProperty(ref _selectedAlarms, value))
                {
                    // 当选择变化时更新删除命令可用性
                    DeleteSelectedCommand.RaiseCanExecuteChanged();
                }
            }
        }


        private IList _selectedItems;
        public IList SelectedItems
        {
            get => _selectedItems;
            set
            {
                SetProperty(ref _selectedItems, value);
                DeleteSelectedCommand.RaiseCanExecuteChanged();
            }
        }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, Math.Max(1, value));
        }

        private int _pageSize = 50;
        public int PageSize
        {
            get => _pageSize;
            set
            {
                SetProperty(ref _pageSize, value);
                // 切换每页大小时重置到第一页
                CurrentPage = 1;
                QueryCommand.Execute();
            }
        }
        private long _totalCount;
        public long TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

        // 每页大小选项
        public List<int> PageSizeOptions { get; } = new() { 10, 20, 50, 100, 200 };

        // 命令
        public DelegateCommand ClearCommand { get; }
        public DelegateCommand LoadAlarmsCommand { get; }
        public DelegateCommand QueryCommand { get; }
        public DelegateCommand PreviousPageCommand { get; }
        public DelegateCommand NextPageCommand { get; }
        public DelegateCommand ExportCsvCommand { get; }
        public DelegateCommand DeleteSelectedCommand { get; }
        public DelegateCommand DeleteAllCommand { get; }
        public DelegateCommand<object> SelectionChangedCommand { get; }
        public DelegateCommand SelectionSyncCommand { get; }
        public DelegateCommand<IList> SyncSelectedItemsCommand { get; }

        #region CSV导出功能

        private void ExportToCsv()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "CSV文件|*.csv|所有文件|*.*",
                    DefaultExt = "csv",
                    FileName = $"AlarmExport_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() != true) return;

                // 创建取消源以支持取消操作
                var cts = new CancellationTokenSource();

                // 显示带有取消按钮的进度窗口
                var loadingDialog = new LoadingDialog(
                    "正在导出...",
                    "正在准备数据",
                    allowCancel: true // 启用取消按钮
                );

                // 注册取消请求
                loadingDialog.CancelRequested += (s, e) => cts.Cancel();

                loadingDialog.Show();

                // 在后台线程执行导出
                Task.Run(async () =>
                {
                    try
                    {
                        DateTime? endDateFilter = EndDate?.Date.AddDays(1).AddMilliseconds(-1);

                        // 获取要导出的总记录数
                        long exportCount = await Task.Run(() =>
                            _alarmService.GetExportCountAsync(
                                StartDate, endDateFilter, SelectedLevel?.Key
                            ), cts.Token);

                        loadingDialog.Report(0, $"共找到 {exportCount} 条记录，正在导出...");

                        // 执行导出
                        await _alarmService.ExportAlarmsToCsvAsync(
                             filePath: dialog.FileName,
                             startDate: StartDate,
                             endDate: endDateFilter
                             );
                        // 成功完成
                        loadingDialog.SetCompleteState("导出成功！", autoClose: true);

                        MessageBox.Show(
                            $"报警记录已成功导出到:\n{dialog.FileName}",
                            "导出完成",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                    catch (OperationCanceledException)
                    {
                        // 用户取消操作
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            loadingDialog.Close();
                            _eventAggregator?.GetEvent<SystemErrorEvent>()?.Publish("导出已取消");
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            loadingDialog.Close();
                            MessageBox.Show(
                                $"导出失败: {ex.Message}",
                                "错误",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error
                            );
                        });
                    }
                }, cts.Token);
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"CSV导出异常: {ex}");
                _eventAggregator?.GetEvent<SystemErrorEvent>()?.Publish($"CSV导出失败: {ex.Message}");
            }
        }

        #endregion

        #region 删除选中报警功能

        private async void DeleteSelectedAlarms()
        {
            try
            {
                if (SelectedAlarms == null || SelectedAlarms.Count == 0)
                {
                    MessageBox.Show("请选择要删除的报警记录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"确定要删除选中的 {SelectedAlarms.Count} 条报警记录吗？此操作不可恢复！",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result == MessageBoxResult.Yes)
                {
                    var idsToDelete = SelectedAlarms
                        .Select(a => a.Id)
                        .Distinct()
                        .ToList();

                    await _alarmService.DeleteAlarmsAsync(idsToDelete);
                    await LoadAlarms();

                    // 清空选择
                    SelectedAlarms.Clear();
                }
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"删除选中报警失败: {ex}");
                MessageBox.Show($"删除过程中发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 删除所有报警功能

        private async void DeleteAllAlarms()
        {
            if (TotalCount == 0)
            {
                MessageBox.Show("当前没有可删除的报警记录", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var result = MessageBox.Show(
                $"确定要删除所有的 {TotalCount} 条报警记录吗？此操作不可恢复！",
                "确认删除",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning
            );
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // 使用日期范围实现筛选条件下的删除
                    DateTime start = StartDate ?? DateTime.MinValue;
                    DateTime end = (EndDate?.AddDays(1).AddMilliseconds(-1)) ?? DateTime.MaxValue;

                    await _alarmService.DeleteAlarmsByDateRangeAsync(start, end);

                    // 刷新报警数据
                    await LoadAlarms();

                    MessageBox.Show($"成功删除所有报警记录",
                                    "完成",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    IMessage.Logger.Error($"删除所有报警失败: {ex}");
                    MessageBox.Show($"删除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        #region 同步选中项
        private int GetSelectedCount()
        {
            int count = 0;
            // IList.Selection
            if (SelectedItems != null)
            {
                count += SelectedItems.Count;
            }
            // SelectedAlarms 集合
            if (SelectedAlarms != null && SelectedAlarms.Any())
            {
                count += SelectedAlarms.Count;
            }
            return count;
        }
        private List<AlarmItemViewModel> GetSelectedItems()
        {
            var items = new List<AlarmItemViewModel>();

            if (SelectedItems != null)
            {
                foreach (var item in SelectedItems)
                {
                    if (item is AlarmItemViewModel alarmVM)
                    {
                        items.Add(alarmVM);
                    }
                }
            }

            if (SelectedAlarms != null && SelectedAlarms.Any())
            {
                items.AddRange(SelectedAlarms);
            }

            return items.DistinctBy(a => a.Id).ToList();
        }
        // 同步选中项的实现
        private void SyncSelectedItems(IList items)
        {
            SelectedAlarms.Clear();
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item is AlarmItemViewModel alarmVM)
                    {
                        SelectedAlarms.Add(alarmVM);
                    }
                }
            }
            DeleteSelectedCommand.RaiseCanExecuteChanged();
        }

        #endregion
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            // 初始加载报警数据
            LoadAlarmsCommand.Execute();
        }
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
    }

    public class AlarmItemViewModel
    {
        public int Id { get; set; }
        public int Index { get; set; }
        public string StartTime { get; set; } 
        public int StationId { get; set; }
        public int Code { get; set; }
        public string AlarmLevel { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        //public bool IsActive { get; set; }
    }
}

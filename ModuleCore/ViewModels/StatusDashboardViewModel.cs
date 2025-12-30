using Interfaces;
using LiveCharts.Wpf;
using LiveCharts;
using Prism.Commands;
using Prism.Mvvm;
using SkiaSharp;
using SmarterMotion;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Interfaces.Services;
using System.IO;
using Prism.Events;
using Interfaces.Events;
using ModuleCore.Services;
using HSMS;
using System.ComponentModel;

namespace ModuleCore.ViewModels
{
    public class StatusDashboardViewModel : BindableBase
    {
        private readonly EquipmentStatus _equipmentStatus;
        public EquipmentStatus EquipmentStatus => _equipmentStatus;

        private readonly Timer _syncTimer;
        private readonly XStation _station;
        private volatile bool _isDisposed;
        // 良率饼图
        private SeriesCollection _yieldSeries;
        private PieSeries _yieldPie;
        private PieSeries _defectPie;

        // NG饼图
        private SeriesCollection _ngSeries;
        private readonly Dictionary<string, PieSeries> _ngSeriesCache = new();

        public SeriesCollection YieldPieSeries => _yieldSeries;
        public SeriesCollection NgPieSeries => _ngSeries;
        private Brush GetNgBrush(string ngType)
        {
            switch (ngType)
            {
                case "NG1":
                    return Brushes.Orange;
                case "NG2":
                    return Brushes.Red;
                case "NG3":
                    return Brushes.Purple;
                default:
                    return Brushes.Gray;
            }
        }
        // 全局锁对象
        private readonly object _seriesLock = new object();
        // 安全的绑定源
        public IEnumerable<KeyValuePair<string, string>> StatusItemsSnapshot =>
                                                        _equipmentStatus.StatusItemsSnapshot;
        private readonly IEventAggregator _eventAggregator;
        private SubscriptionToken _recipeChangedToken;
        private readonly ISecsGemService _secsService;

        public string CurrentRecipeName;

        // 构造函数
        public StatusDashboardViewModel(
              EquipmentStatus equipmentStatus,
              IEventAggregator eventAggregator,
              ISecsGemService secsService)        // 添加 SECS 服务)   // 添加配方加载服务

        {
            _eventAggregator = eventAggregator;
            _equipmentStatus = equipmentStatus;
            _secsService = secsService;

            _station = XStationManager.Instance.FindStationById(1);
            ResetStatisticsCommand = new DelegateCommand(RequestResetStatistics);
            // 初始化配方显示
            InitializeRecipeName();
            // 初始化图表系列
            InitNgChart();
            InitYieldChart();
            // 使用支持取消的定时器模式
            _syncTimer = new Timer(SyncTimerCallback, null, 0, 500);
        }
        private void OnRecipeLoaderPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IRecipeLoaderService.CurrentRecipeName))
            {
                RaisePropertyChanged(nameof(CurrentRecipeName));
                IMessage.Logger.Info($"通过属性变更收到新配方: {CurrentRecipeName}");
            }
        }
        private void OnRecipeChanged(string recipeName)
        {
            // 只需通知属性变更
            RaisePropertyChanged(nameof(CurrentRecipeName));
            IMessage.Logger.Info($"仪表板收到配方更改通知: {recipeName}");
        }

        #region 定时器处理
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private void SyncTimerCallback(object state)
        {
            // 1. 检查是否已释放
            if (_isDisposed) return;

            // 2. 检查设备和站点状态
            if (_equipmentStatus == null || _station == null) return;

            // 3. 检查应用程序状态
            if (Application.Current?.Dispatcher == null ||
                Application.Current.Dispatcher.HasShutdownStarted ||
                Application.Current.Dispatcher.HasShutdownFinished)
                return;

            try
            {
                // 4. 使用BeginInvoke防止阻塞
                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                {
                    try
                    {
                        // 5. 内部再次检查释放状态
                        if (_isDisposed) return;

                        // 在UI线程执行所有更新
                        _equipmentStatus.UpdateFromXStationManager(_station);
                        UpdateCharts();
                    }
                    catch (TaskCanceledException)
                    {
                        // 忽略调度器关闭时的取消异常
                    }
                    catch (Exception ex)
                    {
                        IMessage.Logger.Error(ex, "状态同步出错");
                    }
                }), DispatcherPriority.Background);
            }
            catch (TaskCanceledException)
            {
                // 处理调度器已关闭的情况
            }
        }

        // 增加取消方法
        public void CancelPendingOperations()
        {
            _cts?.Cancel();
            _syncTimer?.Change(Timeout.Infinite, Timeout.Infinite); // 停止定时器
        }
        #endregion

        #region IDisposable 实现
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                // 释放托管资源
                _isDisposed = true;
                // 1. 取消所有后台操作
                CancelPendingOperations();
                // 2. 安全停止并释放定时器
                _syncTimer?.Dispose();
                // 3. 释放CancellationTokenSource
                _cts?.Dispose();
                _cts = null;
            }
        }
        ~StatusDashboardViewModel()
        {
            Dispose(false);
        }
        #endregion

        private void InitYieldChart()
        {
            _yieldPie = new PieSeries
            {
                Title = "良率",
                Fill = Brushes.Green,
                Stroke = Brushes.DarkGreen,
                StrokeThickness = 2,
                DataLabels = true,
                LabelPosition = PieLabelPosition.InsideSlice,
                Foreground = Brushes.White,
                Values = new ChartValues<double> { 100 }
            };

            _defectPie = new PieSeries
            {
                Title = "不良率",
                Fill = Brushes.Red,
                Stroke = Brushes.DarkRed,
                StrokeThickness = 2,
                DataLabels = true,
                LabelPosition = PieLabelPosition.InsideSlice,
                Values = new ChartValues<double> { 0 }
            };

            _yieldSeries = new SeriesCollection { _yieldPie, _defectPie };

        }

        private void InitNgChart()
        {
            _ngSeries = new SeriesCollection();
        }

        private void UpdateCharts()
        {
            // 更新NG分布
            UpdateNgSeries();
            // 更新良率
            UpdateYieldSeries();
        }
        private double _lastYieldRate = -1;
        private void UpdateYieldSeries()
        {
            lock (_seriesLock) // 添加锁保护
            {
                var yieldRate = _equipmentStatus.YieldRate;
                if (Math.Abs(yieldRate - _lastYieldRate) < 0.01)  // 仅当良率变化超过0.01%才更新
                    return;
                _lastYieldRate = yieldRate;

                var defectRate = 100 - yieldRate;

                // 动态显示/隐藏不良率
                if (defectRate > 0.001 && yieldRate != 0)
                {
                    if (_yieldPie.Values.Count == 0)
                        _yieldPie.Values.Add(yieldRate);
                    else
                        _yieldPie.Values[0] = yieldRate;
                    if (_defectPie.Values.Count == 0)
                        _defectPie.Values.Add(defectRate);
                    else
                        _defectPie.Values[0] = defectRate;
                    if (!_yieldSeries.Contains(_defectPie))
                        _yieldSeries.Add(_defectPie);
                    RaisePropertyChanged(nameof(YieldPieSeries)); // 手动触发通知
                }
                else
                {
                    if (_yieldPie.Values.Count == 0)
                        _yieldPie.Values.Add(100.0);
                    else
                        _yieldPie.Values[0] = 100.0;
                    if (_yieldSeries.Contains(_defectPie))
                        _yieldSeries.Remove(_defectPie);
                    RaisePropertyChanged(nameof(YieldPieSeries)); // 手动触发通知
                }
            }
        }

        private void UpdateNgSeries()
        {
            // 添加安全锁
            lock (_seriesLock)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 确保在UI线程操作图表集合
                    foreach (var ngItem in _equipmentStatus.NgRatioChartData)
                    {
                        if (!_ngSeriesCache.TryGetValue(ngItem.Key, out var series))
                        {
                            series = new PieSeries
                            {
                                Title = ngItem.Key,
                                Fill = GetNgBrush(ngItem.Key),
                                Stroke = Brushes.Gray,
                                StrokeThickness = 1,
                                DataLabels = true,
                                LabelPosition = PieLabelPosition.InsideSlice,
                                Foreground = Brushes.White,
                                Values = new ChartValues<double> { ngItem.Value }  // 直接初始化时赋值
                            };
                            _ngSeriesCache.Add(ngItem.Key, series);
                            _ngSeries.Add(series);
                        }

                        // 更新值
                        if (series.Values.Count == 0)
                            series.Values.Add(ngItem.Value);
                        else
                            series.Values[0] = ngItem.Value;
                    }
                    // 移除不存在的NG类型
                    var toRemove = _ngSeriesCache.Keys
                        .Where(k => !_equipmentStatus.NgRatioChartData.ContainsKey(k))
                        .ToList();

                    foreach (var key in toRemove)
                    {
                        _ngSeries.Remove(_ngSeriesCache[key]);
                        _ngSeriesCache.Remove(key);
                    }
                });
            }
        }
        public DelegateCommand<string> SimulateStateChangeCommand { get; }
        public DelegateCommand ResetStatisticsCommand { get; private set; }

        private async void RequestResetStatistics()
        {
            // 显示确认对话框
            var result = MessageBox.Show(
                "确定要清零所有统计数据吗？此操作不可撤销！",
                "确认清零",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                // 获取备份目录
                string backupDir = DeviceConfigService.GetDefaultDataPath();

                // 添加历史记录子目录
                backupDir = Path.Combine(backupDir, "StatisticsHistory");

                // 使用async/await避免阻塞UI线程
                await Task.Run(() =>
                {
                    // 暂停监控线程
                    _equipmentStatus.StopMonitoring();

                    // 执行重置
                    _equipmentStatus.ResetStatistics(backupDir);

                    // 重启监控线程
                    _equipmentStatus.StartMonitoring();
                });
                // 显示备份消息
                string backupMsg = Directory.Exists(backupDir) ?
                    $"统计数据已备份至: {backupDir}" :
                    "备份失败，但数据已清零";

                MessageBox.Show(
                    $"已重置所有统计数据\n\n{backupMsg}",
                    "操作完成",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

        }

        // 初始化当前配方名称
        private void InitializeRecipeName()
        {
            try
            {
                string recipeName = CurrentRecipeName;
                IMessage.Logger.Info($"初始化仪表板当前配方: {recipeName}");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"获取当前配方失败: {ex.Message}");
            }
        }

    }
}

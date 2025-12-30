using Core.Abstraction;
using Interfaces;
using Interfaces.Services;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SmarterMotion
{
    public class EquipmentStatus : BindableBase
    {
        // 添加线程安全的集合
        private readonly ObservableCollection<KeyValuePair<string, string>> _statusItemsCollection
            = new ObservableCollection<KeyValuePair<string, string>>();
        public IEnumerable<KeyValuePair<string, string>> StatusItemsSnapshot => _statusItemsCollection;
        private DateTime _lastUpdateTime = DateTime.MinValue;

        #region 线程控制和状态管理
        private Thread _statusThread;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly object _lock = new object();
        private bool _isDisposed;
        // 使用ManualResetEvent替代Sleep
        private ManualResetEventSlim _updateEvent = new ManualResetEventSlim(false);
        // 添加安全的停止标志
        public bool ShouldStop => _cancellationTokenSource.IsCancellationRequested;
        #endregion


        #region 状态属性
        // 记录最后有效状态
        private EquipmentState _lastValidState = EquipmentState.Idle;
        private EquipmentState _currentState = EquipmentState.Idle;
        public EquipmentState CurrentState
        {
            get => _currentState;
            set
            {
                // 只在状态真正改变时通知
                if (SetProperty(ref _currentState, value))
                {
                    HandleStateChange(value);
                }
            }

        }
        private void HandleStateChange(EquipmentState newState)
        {
            // 只有在空闲/非空闲状态切换时通知外部
            if (IsIdleState(newState) != IsIdleState(_lastValidState))
            {
                RaisePropertyChanged(nameof(CurrentState));
                _lastValidState = newState;
            }
        }
        private bool IsIdleState(EquipmentState state)
        {
            return state == EquipmentState.Idle || state == EquipmentState.Paused;
        }

        // 计时相关
        private TimeSpan _idleTime;
        public TimeSpan IdleTime { get => _idleTime; private set => SetProperty(ref _idleTime, value); }

        private TimeSpan _runningTime;
        public TimeSpan RunningTime { get => _runningTime; private set => SetProperty(ref _runningTime, value); }

        private TimeSpan _alarmTime;
        public TimeSpan AlarmTime { get => _alarmTime; private set => SetProperty(ref _alarmTime, value); }

        private TimeSpan _pausedTime;
        public TimeSpan PausedTime { get => _pausedTime; private set => SetProperty(ref _pausedTime, value); }

        public string FormattedIdleTime => $"{IdleTime.Hours:D2}:{IdleTime.Minutes:D2}:{IdleTime.Seconds:D2}";
        public string FormattedRunningTime => $"{RunningTime.Hours:D2}:{RunningTime.Minutes:D2}:{RunningTime.Seconds:D2}";
        public string FormattedAlarmTime => $"{AlarmTime.Hours:D2}:{AlarmTime.Minutes:D2}:{AlarmTime.Seconds:D2}";
        public string FormattedPausedTime => $"{PausedTime.Hours:D2}:{AlarmTime.Minutes:D2}:{AlarmTime.Seconds:D2}";
        // 比例属性（百分比形式）
        public double PreNeedleRatio => TotalNG > 0 ? (double)PreNeedleNG / TotalNG * 100 : 0;
        public double NeedleForceRatio => TotalNG > 0 ? (double)NeedleForceNG / TotalNG * 100 : 0;
        public double PostNeedleRatio => TotalNG > 0 ? (double)PostNeedleNG / TotalNG * 100 : 0;
        // 图表数据集合
        public Dictionary<string, double> NgRatioChartData => new()
        {
            ["NG1"] = double.IsNaN(PreNeedleRatio) ? 0 : PreNeedleRatio,
            ["NG2"] = double.IsNaN(NeedleForceRatio) ? 0 : NeedleForceRatio,
            ["NG3"] = double.IsNaN(PostNeedleRatio) ? 0 : PostNeedleRatio
        };

        // 生产数据
        private int _uph;
        public int UPH { get => _uph; private set => SetProperty(ref _uph, value); }

        private int _okCount;
        public int OKCount { get => _okCount; private set => SetProperty(ref _okCount, value); }

        private double _yieldRate;
        public double YieldRate { get => _yieldRate; private set => SetProperty(ref _yieldRate, value); }

        // NG分类
        private int _totalNG;
        public int TotalNG { get => _totalNG; private set => SetProperty(ref _totalNG, value); }

        private int _preNeedleNG;
        public int PreNeedleNG { get => _preNeedleNG; private set => SetProperty(ref _preNeedleNG, value); }

        private int _needleForceNG;
        public int NeedleForceNG { get => _needleForceNG; private set => SetProperty(ref _needleForceNG, value); }

        private int _postNeedleNG;
        public int PostNeedleNG { get => _postNeedleNG; private set => SetProperty(ref _postNeedleNG, value); }
        #endregion

        #region 持久化路径
        private const string SAVE_FILE = "EquipmentStatus.json";
        #endregion

        public EquipmentStatus()
        {
            LoadData();
            StartMonitoring();
        }

        #region 线程方法
        public void StartMonitoring()
        {
            // 确保只启动一次
            if (_statusThread != null && _statusThread.IsAlive)
                return;
            _cancellationTokenSource = new CancellationTokenSource();
            _updateEvent.Reset();
            _statusThread = new Thread(MonitorThreadProc)
            {
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal,
                Name = "EquipmentStatusMonitor"
            };
            //_statusThread.Start();
        }

        private void MonitorThreadProc()
        {
            DateTime lastUpdateTime = DateTime.Now;
            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    EquipmentState lastState = CurrentState;
                    // 使用Wait替代Sleep，支持即时退出
                    if (_updateEvent.Wait(TimeSpan.FromMilliseconds(300)))
                    {
                        _updateEvent.Reset(); // Reset for next wait
                    }
                    if (_cancellationTokenSource.IsCancellationRequested)
                        return;
                    try
                    {
                        UpdateEquipmentState(ref lastUpdateTime, ref lastState);
                    }
                    catch (Exception ex)
                    {
                        // 记录异常信息
                        IMessage.Logger?.Error(ex, "监控线程状态更新异常");
                    }
                }
            }
            catch (ThreadAbortException)
            {
                // 安全忽略
                Thread.ResetAbort();
            }
            catch (Exception ex)
            {
                // 记录线程意外退出
                IMessage.Logger?.Error(ex, "监控线程意外终止");
            }
        }

        private void UpdateEquipmentState(ref DateTime lastUpdateTime, ref EquipmentState lastState)
        {
            // 最小化锁时间
            TimeSpan elapsed;
            lock (_lock)
            {
                elapsed = DateTime.Now - lastUpdateTime;
                lastUpdateTime = DateTime.Now;
            }
            // 计算状态持续时间
            switch (lastState)
            {
                case EquipmentState.Idle:
                    IdleTime += elapsed;
                    break;
                case EquipmentState.Running:
                    RunningTime += elapsed;
                    break;
                case EquipmentState.Alarm:
                    AlarmTime += elapsed;
                    break;
                case EquipmentState.Paused:
                    PausedTime += elapsed;
                    break;
            }
            // 更新UPH
            if (lastState == EquipmentState.Running)
            {
                // 防止除零
                UPH = (int)(OKCount / Math.Max(RunningTime.TotalHours, 0.01));
            }
            // 更新良率
            if (OKCount + TotalNG > 0)
            {
                YieldRate = (double)OKCount / (OKCount + TotalNG) * 100;
            }
            // 更新UI绑定
            TimeSpan timeSinceLastUpdate;
            lock (_lock)
            {
                timeSinceLastUpdate = DateTime.Now - _lastUpdateTime;
            }

            // 限制UI更新频率 (最小200ms)
            if (timeSinceLastUpdate.TotalMilliseconds < 200) return;

            try
            {
                UpdateStatusItemsCollection();
                lock (_lock)
                {
                    _lastUpdateTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                // 安全处理UI线程访问异常
                IMessage.Logger?.Error(ex, "状态更新到UI失败");
            }
        }

        public void StopMonitoring()
        {
            try
            {
                // 安全取消
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    _cancellationTokenSource.Cancel();
                    _updateEvent.Set(); // 唤醒等待中的线程
                }
                // 非阻塞等待线程退出
                if (_statusThread?.IsAlive == true)
                {
                    // 更安全的超时机制
                    if (!_statusThread.Join(TimeSpan.FromSeconds(1)))
                    {
                        IMessage.Logger?.Warn("状态监控线程未在1秒内退出");
                        // 最后手段 - 避免使用Abort() 
                        _statusThread = null;
                    }
                }
            }
            catch (Exception ex)
            {
                IMessage.Logger?.Error(ex, "停止监控线程异常");
            }
            finally
            {
                SaveData();
            }

        }
        #endregion

        #region 数据操作
        public void AddOK() => OKCount++;
        public void AddPreNeedleNG()
        {
            PreNeedleNG++;
            TotalNG++;
            RaisePropertyChanged(nameof(PreNeedleRatio));
            RaisePropertyChanged(nameof(NgRatioChartData));
        }
        public void AddNeedleForceNG()
        {
            NeedleForceNG++;
            TotalNG++;
            RaisePropertyChanged(nameof(NeedleForceRatio));
            RaisePropertyChanged(nameof(NgRatioChartData));
        }
        public void AddPostNeedleNG()
        {
            PostNeedleNG++;
            TotalNG++;
            RaisePropertyChanged(nameof(PostNeedleRatio));
            RaisePropertyChanged(nameof(NgRatioChartData));
        }
        public void ResetStatistics(string backupDirectory = null)
        {
            // 如果有提供备份目录，则先保存快照
            if (!string.IsNullOrWhiteSpace(backupDirectory))
            {
                try
                {
                    SaveStatisticsSnapshot(backupDirectory);
                }
                catch { /* 忽略保存错误，但继续重置 */ }
            }

            // 异步操作避免阻塞线程
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    lock (_lock)
                    {
                        // 重置生产数据
                        OKCount = 0;
                        TotalNG = 0;
                        PreNeedleNG = 0;
                        NeedleForceNG = 0;
                        PostNeedleNG = 0;
                        YieldRate = 0;
                        UPH = 0;
                        // 重置时间统计
                        IdleTime = TimeSpan.Zero;
                        RunningTime = TimeSpan.Zero;
                        AlarmTime = TimeSpan.Zero;
                        PausedTime = TimeSpan.Zero;
                        // 手动触发属性变更通知
                        NotifyAllPropertiesChanged();
                        // 更新状态项集合
                        UpdateStatusItemsCollection();
                        // 立即保存数据
                        SaveData();
                    }
                });
            }
        }
        // 一次性通知所有属性变化的方法
        private void NotifyAllPropertiesChanged()
        {
            RaisePropertyChanged(nameof(FormattedIdleTime));
            RaisePropertyChanged(nameof(FormattedRunningTime));
            RaisePropertyChanged(nameof(FormattedAlarmTime));
            RaisePropertyChanged(nameof(FormattedPausedTime));
            RaisePropertyChanged(nameof(PreNeedleRatio));
            RaisePropertyChanged(nameof(NeedleForceRatio));
            RaisePropertyChanged(nameof(PostNeedleRatio));
            RaisePropertyChanged(nameof(NgRatioChartData));
            RaisePropertyChanged(nameof(UPH));
            RaisePropertyChanged(nameof(OKCount));
            RaisePropertyChanged(nameof(YieldRate));
            RaisePropertyChanged(nameof(TotalNG));
            RaisePropertyChanged(nameof(PreNeedleNG));
            RaisePropertyChanged(nameof(NeedleForceNG));
            RaisePropertyChanged(nameof(PostNeedleNG));
        }

        #endregion

        #region 持久化
        private void SaveData()
        {
            var data = new
            {
                CurrentState,
                IdleTime,
                RunningTime,
                AlarmTime,
                PausedTime,
                UPH,
                OKCount,
                YieldRate,
                TotalNG,
                PreNeedleNG,
                NeedleForceNG,
                PostNeedleNG
            };
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", SAVE_FILE);
            string json = JsonSerializer.Serialize(data);
            File.WriteAllText(filePath, json);
        }

        private void LoadData()
        {
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", SAVE_FILE);
            if (!File.Exists(filePath)) return;

            try
            {
                string json = File.ReadAllText(filePath);
                var data = JsonSerializer.Deserialize<dynamic>(json);
                // 其他属性类似...
                TotalNG = data.GetProperty("TotalNG").GetInt32();
                PreNeedleNG = data.GetProperty("PreNeedleNG").GetInt32();
                NeedleForceNG = data.GetProperty("NeedleForceNG").GetInt32();
                PostNeedleNG = data.GetProperty("PostNeedleNG").GetInt32();
                OKCount = data.GetProperty("OKCount").GetInt32();
                YieldRate = data.GetProperty("YieldRate").GetDouble();
                UPH = data.GetProperty("UPH").GetInt32();
                AlarmTime = TimeSpan.Parse(data.GetProperty("AlarmTime").GetString());
                PausedTime = TimeSpan.Parse(data.GetProperty("PausedTime").GetString());
                //CurrentState = Enum.Parse<EquipmentState>(data.GetProperty("CurrentState").GetString());
                IdleTime = TimeSpan.Parse(data.GetProperty("IdleTime").GetString());
                RunningTime = TimeSpan.Parse(data.GetProperty("RunningTime").GetString());
            }
            catch { /* 忽略加载错误 */ }
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
                _isDisposed = true;

                // 安全终止监控线程
                StopMonitoring();

                // 清理资源
                _cancellationTokenSource?.Dispose();
                _updateEvent?.Dispose();

                // 释放引用
                _cancellationTokenSource = null;
                _updateEvent = null;
                _statusThread = null;
            }
        }
        #endregion

        #region UI 更新
        public void UpdateFromXStationManager(XStation xStation)
        {
            var station = xStation;

            // 状态映射
            if (station.State == XStationState.WAITRUN)
            {
                CurrentState = EquipmentState.Idle;
            }
            else if (station.State == XStationState.WAITRESET ||
               station.State == XStationState.STOP ||
               station.State == XStationState.RESETING)
            {
                CurrentState = EquipmentState.DOWN;
            }
            else if (station.State == XStationState.RUNNING ||
                     station.State == XStationState.PAUSE)
            {
                CurrentState = station.State == XStationState.PAUSE ?
                              EquipmentState.Paused : EquipmentState.Running;
            }
            else if (station.State == XStationState.ALARM ||
                     station.State == XStationState.ESTOP)
            {
                CurrentState = EquipmentState.Alarm;
            }
        }

        private void UpdateStatusItemsCollection()
        {
            var newItems = new List<KeyValuePair<string, string>>
            {
                new("运行状态", CurrentState.ToString()),
                new("运行时间", FormattedRunningTime),
                new("待机时间", FormattedIdleTime),
                new("UPH", $"{UPH} units/h"),
                new("总产量", (OKCount + TotalNG).ToString()),
                new("良率", $"{YieldRate:F1}% (OK: {OKCount})"),
                new("总NG数", $"{TotalNG}"),//(拨前: {PreNeedleNG}, 拨针: {NeedleForceNG}, 拨后: {PostNeedleNG})
                //new("NG分布", $"拨前{PreNeedleRatio:F1}% / 拨针{NeedleForceRatio:F1}% / 拨后{PostNeedleRatio:F1}%")
            };
            // UI线程安全更新
            Application.Current?.Dispatcher.Invoke(() =>
            {
                _statusItemsCollection.Clear();
                foreach (var item in newItems)
                {
                    _statusItemsCollection.Add(item);
                }
            });
        }
        #endregion

        #region 保存统计数据
        public void SaveStatisticsSnapshot(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
            // 创建时间戳文件名
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filePath = Path.Combine(directoryPath, $"StatisticsSnapshot_{timestamp}.json");
            // 创建保存的数据模型
            var snapshot = new
            {
                Timestamp = DateTime.Now,
                TotalCount = OKCount + TotalNG,
                OKCount,
                NGCount = TotalNG,
                PreNeedleNG,
                NeedleForceNG,
                PostNeedleNG,
                IdleTime,
                RunningTime,
                AlarmTime,
                PausedTime,
                UPH,
                CurrentState
            };
            try
            {
                string json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                IMessage.Logger?.Error(ex, "保存统计快照失败");
            }
        }

        #endregion
    }
}

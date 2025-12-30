
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Utilities;
using Prism.Events;

namespace Stations.Services
{
    public class StationCoordinator
    {
        // 事件定义
        public class StationChangedEventArgs : EventArgs
        {
            public int StationNumber { get; set; }
            public string Status { get; set; }
        }

        public class ProgressUpdatedEventArgs : EventArgs
        {
            public int CompletedCount { get; set; }
            public int TotalCount { get; set; }
        }

        public class ExecutionCompletedEventArgs : EventArgs
        {
            public bool IsSuccess { get; set; }
            public string Message { get; set; }
        }

        // 事件
        public event EventHandler<StationChangedEventArgs> StationChanged;
        public event EventHandler<ProgressUpdatedEventArgs> ProgressUpdated;
        public event EventHandler<ExecutionCompletedEventArgs> ExecutionCompleted;

        // 依赖工站
        private readonly LoadingStation _loadingStation;
        private readonly AssemblyStation _assemblyStation;
        private readonly DispenserStation _dispenserStation;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILoggerService _logger;

        // 执行状态
        private bool _isRunning = false;
        private CancellationTokenSource _executionCTS;
        private List<int> _selectedStations;
        private int _currentStationIndex = -1;
        private bool _canSkipCurrent = false;

        public bool IsRunning => _isRunning;
        public bool CanSkipCurrent => _isRunning && _canSkipCurrent;

        // 执行统计
        private int _completedCount = 0;
        private int _totalCount = 0;

        public StationCoordinator(
            LoadingStation loadingStation,
            AssemblyStation assemblyStation,
            DispenserStation dispenserStation,
            IEventAggregator eventAggregator,
            ILoggerService logger)
        {
            _loadingStation = loadingStation;
            _assemblyStation = assemblyStation;
            _dispenserStation = dispenserStation;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public async Task StartExecutionAsync(List<int> selectedStations)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("协调器已经在运行中");
            }

            if (selectedStations == null || selectedStations.Count == 0)
            {
                throw new ArgumentException("必须选择至少一个工位");
            }

            try
            {
                _isRunning = true;
                _selectedStations = selectedStations.OrderBy(s => s).ToList();
                _currentStationIndex = 0;
                _completedCount = 0;
                _totalCount = selectedStations.Count;
                _executionCTS = new CancellationTokenSource();

                _logger.Info($"开始执行工位: {string.Join(", ", selectedStations)}");

                // 触发进度更新事件
                ProgressUpdated?.Invoke(this, new ProgressUpdatedEventArgs
                {
                    CompletedCount = 0,
                    TotalCount = _totalCount
                });

                // 依次执行每个工位
                for (int i = 0; i < _selectedStations.Count; i++)
                {
                    if (_executionCTS.Token.IsCancellationRequested)
                        break;

                    _currentStationIndex = i;
                    int stationNumber = _selectedStations[i];

                    // 更新当前工位状态
                    OnStationChanged(stationNumber, "执行中...");

                    _canSkipCurrent = true;

                    try
                    {
                        // 执行单个工位的完整流程
                        await ExecuteStationProcessAsync(stationNumber, _executionCTS.Token);

                        _completedCount++;

                        // 更新进度
                        ProgressUpdated?.Invoke(this, new ProgressUpdatedEventArgs
                        {
                            CompletedCount = _completedCount,
                            TotalCount = _totalCount
                        });

                        _logger.Info($"工位{stationNumber}执行完成 ({_completedCount}/{_totalCount})");
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Info($"工位{stationNumber}执行被取消");
                        OnStationChanged(stationNumber, "已取消");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"工位{stationNumber}执行失败: {ex.Message}");
                        OnStationChanged(stationNumber, "执行失败");

                        // 询问是否继续执行下一个工位
                        bool continueNext = await AskContinueAfterErrorAsync(stationNumber, ex.Message);

                        if (!continueNext)
                        {
                            break;
                        }
                    }

                    _canSkipCurrent = false;

                    // 短暂延迟，确保状态更新
                    await Task.Delay(100);

                    // 检查是否是最后一个工位
                    if (i == _selectedStations.Count - 1)
                    {
                        // 所有工位执行完成
                        _logger.Info("所有工位执行完成");

                        ExecutionCompleted?.Invoke(this, new ExecutionCompletedEventArgs
                        {
                            IsSuccess = true,
                            Message = $"所有{_totalCount}个工位执行完成"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"工位协调器执行异常: {ex.Message}");

                ExecutionCompleted?.Invoke(this, new ExecutionCompletedEventArgs
                {
                    IsSuccess = false,
                    Message = $"执行异常: {ex.Message}"
                });
            }
            finally
            {
                _isRunning = false;
                _executionCTS?.Dispose();
                _executionCTS = null;

                OnStationChanged(0, "已停止");
            }
        }

        private void OnStationChanged(int stationNumber, string status)
        {
            StationChanged?.Invoke(this, new StationChangedEventArgs
            {
                StationNumber = stationNumber,
                Status = status
            });
        }

        public async Task StopExecutionAsync()
        {
            if (!_isRunning || _executionCTS == null)
            {
                return;
            }

            _logger.Info("正在停止工位执行...");

            // 取消执行
            _executionCTS.Cancel();

            // 停止各工站当前操作
            try
            {
                _loadingStation.StopLoadingProcess();
                _assemblyStation.StopAssemblyProcess();
                _dispenserStation.StopDispensingProcess();
            }
            catch (Exception ex)
            {
                _logger.Error($"停止工站时发生错误: {ex.Message}");
            }

            // 等待一段时间让工站停止
            await Task.Delay(1000);

            _logger.Info("工位执行已停止");
        }

        public async Task SkipCurrentStationAsync()
        {
            if (!_isRunning || !_canSkipCurrent || _currentStationIndex < 0)
            {
                return;
            }

            int currentStation = _selectedStations[_currentStationIndex];
            _logger.Info($"跳过当前工位: {currentStation}");

            // 停止当前工位的执行
            await StopExecutionAsync();

            // 如果有下一个工位，继续执行
            if (_currentStationIndex < _selectedStations.Count - 1)
            {
                // 重新创建CTS
                _executionCTS = new CancellationTokenSource();

                // 跳过当前，从下一个开始
                _currentStationIndex++;

                // 继续执行剩余工位
                var remainingStations = _selectedStations.Skip(_currentStationIndex).ToList();
                _ = Task.Run(async () =>
                {
                    await Task.Delay(500); // 短暂延迟

                    try
                    {
                        await StartExecutionAsync(remainingStations);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"继续执行剩余工位失败: {ex.Message}");
                    }
                });
            }
        }

        private async Task ExecuteStationProcessAsync(int stationNumber, CancellationToken cancellationToken)
        {
            _logger.Info($"开始执行工位{stationNumber}的完整流程");

            try
            {
                // 1. 执行上料工站的单个工位操作
                OnStationChanged(stationNumber, "上料中...");
                await ExecuteLoadingForStationAsync(stationNumber, cancellationToken);

                // 2. 执行组装工站的单个工位操作
                OnStationChanged(stationNumber, "组装中...");
                await ExecuteAssemblyForStationAsync(stationNumber, cancellationToken);

                // 3. 执行点胶工站的单个工位操作
                OnStationChanged(stationNumber, "点胶中...");
                await ExecuteDispensingForStationAsync(stationNumber, cancellationToken);

                OnStationChanged(stationNumber, "完成");
                _logger.Info($"工位{stationNumber}完整流程执行完成");
            }
            catch (Exception ex)
            {
                OnStationChanged(stationNumber, "执行失败");
                throw new Exception($"工位{stationNumber}执行失败: {ex.Message}", ex);
            }
        }

        private async Task ExecuteLoadingForStationAsync(int stationNumber, CancellationToken cancellationToken)
        {
            // 这里调用LoadingStation的单个工位执行方法
            if (_loadingStation != null)
            {
                //await _loadingStation.ExecuteForStationAsync(stationNumber, cancellationToken);
            }
            else
            {
                // 模拟执行
                await Task.Delay(1000, cancellationToken);
            }
        }

        private async Task ExecuteAssemblyForStationAsync(int stationNumber, CancellationToken cancellationToken)
        {
            // 这里调用AssemblyStation的单个工位执行方法
            if (_assemblyStation != null)
            {
                //await _assemblyStation.ExecuteForStationAsync(stationNumber, cancellationToken);
            }
            else
            {
                // 模拟执行
                await Task.Delay(1500, cancellationToken);
            }
        }

        private async Task ExecuteDispensingForStationAsync(int stationNumber, CancellationToken cancellationToken)
        {
            // 这里调用DispenserStation的单个工位执行方法
            if (_dispenserStation != null)
            {
                //await _dispenserStation.ExecuteForStationAsync(stationNumber, cancellationToken);
            }
            else
            {
                // 模拟执行
                await Task.Delay(800, cancellationToken);
            }
        }

        private async Task<bool> AskContinueAfterErrorAsync(int stationNumber, string errorMessage)
        {
            _logger.Warn($"工位{stationNumber}执行失败: {errorMessage}");
            // var result = await ShowDialogAsync("错误", $"工位{stationNumber}执行失败: {errorMessage}\n是否继续执行下一个工位?", "是", "否");
            // return result == "是";
            return false;
        }
    }
}
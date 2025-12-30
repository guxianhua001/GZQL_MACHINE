using Interfaces;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Framework.ViewModels
{
    public class DialRecordsTrendViewModel : BindableBase, INavigationAware,IDisposable
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly IDataAcquisitionService _dataService;
        private readonly RecipePool _recipePool;
        private readonly List<Task> _monitorTasks = new();
        private CancellationTokenSource _cts;
        private bool _disposed;
        // 图表数据集合
        public ChartViewModel TorqueChart1 { get; } = new();
        public ChartViewModel TorqueChart2 { get; } = new();
        // 编码器值
        private int _encoder1Value;
        public int Encoder1Value
        {
            get => _encoder1Value;
            set => SetProperty(ref _encoder1Value, value);
        }
        private int _encoder2Value;
        public int Encoder2Value
        {
            get => _encoder2Value;
            set => SetProperty(ref _encoder2Value, value);
        }
        // 模拟输入值
        private double[] _analog1Inputs = new double[4];
        public double[] Analog1Inputs
        {
            get => _analog1Inputs;
            set => SetProperty(ref _analog1Inputs, value);
        }
        // 模拟输入值
        private double[] _analog2Inputs = new double[4];
        public double[] Analog2Inputs
        {
            get => _analog2Inputs;
            set => SetProperty(ref _analog2Inputs, value);
        }

        public DialRecordsTrendViewModel(IDataAcquisitionService dataService, RecipePool recipePool)
        {
            _dataService = dataService;
            _dataService.DataUpdated += OnDataUpdated;
            _recipePool = recipePool;
        }

        #region 监控任务管理
        public void StartMonitoring()
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _monitorTasks.Add(Task.Run(() => MonitorCondition1(token), token));
            _monitorTasks.Add(Task.Run(() => MonitorCondition2(token), token));
        }
        public void StopMonitoring()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _monitorTasks.Clear();  // Prism会管理Task生命周期
        }
        #endregion

        private void OnDataUpdated(object sender, DataUpdatedEventArgs e)
        {
            // 从事件参数中获取完整数据
            var slave1Data = e.Data.Slave1;
            var slave2Data = e.Data.Slave2;

            if (Application.Current == null) return;
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 更新编码器值
                //Encoder1Value = slave1Data.Encoder;  // 从站1的编码器
                //Encoder2Value = slave2Data.Encoder;  // 从站2的编码器

                // 更新模拟量输入值
                Analog1Inputs = slave1Data.AnalogInputs; // 从站1的4个模拟通道
                Analog2Inputs = slave2Data.AnalogInputs; // 从站2的4个模拟通道

                // 通知属性变更
                //RaisePropertyChanged(nameof(Analog1Inputs));
                //RaisePropertyChanged(nameof(Analog2Inputs));
            });
        }

        private async Task MonitorCondition1(CancellationToken token)
        {
            /*   const double Threshold = 0.3;          // 正负扭矩分界点
           const double MaxDeviation = 0.3;       // 允许偏差阈值
           const int MonitorIntervalMs = 200;      // 检测间隔

           while (!token.IsCancellationRequested)
           {
               try
               {
                   var data = _dataService.CurrentData;

                   var rand = new Random();

                   for (int i = 0; i < 2; i++)
                   {
                       double currentValue = data.Slave1.AnalogInputs[i];
                       bool isPositiveRegion = currentValue >= Threshold;

                       // 获取目标值
                       double torqueTarget = isPositiveRegion ?
                           _recipePool.CurrentRecipe.ForwardTorqueTarget :
                           _recipePool.CurrentRecipe.NegativeTorqueTarget;

                       // 计算偏差方向（区别于绝对值）
                       double signedDifference = currentValue - torqueTarget;

                       // 非对称修正条件判断
                       bool needCorrection =
                           (isPositiveRegion && signedDifference > MaxDeviation) ||  // 正区间：当前值 > 目标+0.2
                           (!isPositiveRegion && signedDifference < -MaxDeviation);  // 负区间：当前值 < 目标-0.2

                       //if (needCorrection)
                       //{
                       //    // 生成定向修正值（始终向目标值方向靠近）
                       //    double correction = isPositiveRegion
                       //        ? torqueTarget + rand.NextDouble() * MaxDeviation  // 正区间：目标值 + [0,0.2]
                       //        : torqueTarget - rand.NextDouble() * MaxDeviation; // 负区间：目标值 - [0,0.2]

                       //    data.Slave1.AnalogInputs[i] = correction;
                       //    //IMessage.Logger.Debug($"通道{i} 修正方向: {(isPositiveRegion ? "向下" : "向上")}, 新值: {correction:F3}");
                       //}
                       if (needCorrection)
                       {
                           double newValue = isPositiveRegion
                               ? currentValue - 0.1 * rand.NextDouble() // 每次最大减少0.1
                               : currentValue + 0.1 * rand.NextDouble();

                           data.Slave1.AnalogInputs[i] = Math.Clamp(newValue,
                               torqueTarget - MaxDeviation,
                               torqueTarget + MaxDeviation);
                       }

                       TorqueChart1.UpdateSeries(data.Slave1.AnalogInputs[i], i);
                   }

                   await Task.Delay(MonitorIntervalMs, token);
               }
               catch (Exception ex)
               {
                   IMessage.Logger.Error($"扭矩监控异常: {ex.Message}");
                   await Task.Delay(1000);
               }
           }*/

            while (true)
            {
                var data = _dataService.CurrentData;
                for (int i = 0; i < 2; i++)
                {
                    TorqueChart1.UpdateSeries(data.Slave1.AnalogInputs[i], i);
                }
                await Task.Delay(200); // 10ms检查间隔
            }
        }

        private async Task MonitorCondition2(CancellationToken token)
        {
            /*const double Threshold = 0.3;          // 正负扭矩分界点
            const double MaxDeviation = 0.3;       // 允许偏差阈值
            const int MonitorIntervalMs = 200;      // 检测间隔

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var data = _dataService.CurrentData;
                    if (_recipePool.CurrentRecipe == null)
                        continue;
                    var rand = new Random();

                    for (int i = 0; i < 2; i++)
                    {
                        double currentValue = data.Slave2.AnalogInputs[i];
                        bool isPositiveRegion = currentValue >= Threshold;

                        // 获取目标值
                        double torqueTarget = isPositiveRegion ?
                            _recipePool.CurrentRecipe.ForwardTorqueTarget :
                            _recipePool.CurrentRecipe.NegativeTorqueTarget;

                        // 计算偏差方向（区别于绝对值）
                        double signedDifference = currentValue - torqueTarget;

                        // 非对称修正条件判断
                        bool needCorrection =
                            (isPositiveRegion && signedDifference > MaxDeviation) ||  // 正区间：当前值 > 目标+0.2
                            (!isPositiveRegion && signedDifference < -MaxDeviation);  // 负区间：当前值 < 目标-0.2

                        //if (needCorrection)
                        //{
                        //    // 生成定向修正值（始终向目标值方向靠近）
                        //    double correction = isPositiveRegion
                        //        ? torqueTarget + rand.NextDouble() * MaxDeviation  // 正区间：目标值 + [0,0.2]
                        //        : torqueTarget - rand.NextDouble() * MaxDeviation; // 负区间：目标值 - [0,0.2]

                        //    data.Slave1.AnalogInputs[i] = correction;
                        //    //IMessage.Logger.Debug($"通道{i} 修正方向: {(isPositiveRegion ? "向下" : "向上")}, 新值: {correction:F3}");
                        //}
                        if (needCorrection)
                        {
                            double newValue = isPositiveRegion
                                ? currentValue - 0.1 * rand.NextDouble() // 每次最大减少0.1
                                : currentValue + 0.1 * rand.NextDouble();

                            data.Slave2.AnalogInputs[i] = Math.Clamp(newValue,
                                torqueTarget - MaxDeviation,
                                torqueTarget + MaxDeviation);
                        }

                        TorqueChart2.UpdateSeries(data.Slave2.AnalogInputs[i], i);
                    }

                    await Task.Delay(MonitorIntervalMs, token);
                }
                catch (Exception ex)
                {
                    IMessage.Logger.Error($"扭矩监控异常: {ex.Message}");
                    await Task.Delay(1000);
                }
            }*/

            while (true)
            {
                var data = _dataService.CurrentData;
                for (int i = 0; i < 2; i++)
                {
                    TorqueChart2.UpdateSeries(data.Slave2.AnalogInputs[i], i);
                }
                await Task.Delay(200); // 10ms检查间隔
            }
        }

        #region Prism生命周期管理
        // 进入视图时启动
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            StartMonitoring();
            //_eventAggregator.GetEvent<ViewActivatedEvent>().Publish("DialRecordsTrend");
        }
        // 离开视图时停止
        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            if (!navigationContext.Parameters.ContainsKey("KeepAlive"))
            {
                StopMonitoring();
            }
        }
        // 是否允许导航离开（可做安全确认）
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        #endregion

        #region Dispose模式实现
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                StopMonitoring();
                //_eventAggregator.GetEvent<ViewDeactivatedEvent>().Publish("DialRecordsTrend");
            }
            _disposed = true;
        }
        #endregion
    }
}

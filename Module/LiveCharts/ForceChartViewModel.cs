// ForceChartViewModel.cs
using Interfaces;
using Prism.Mvvm;
//using ScottPlot;
//using ScottPlot.WPF;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

namespace Framework.ViewModels
{
    public class ForceChartViewModel : BindableBase, IDisposable
    {
        private const int BufferSize = 1000;
        private const int MaxPoints = 16000;
        private readonly IDeviceService _device;
        private readonly int _slaveNo;
        private readonly int _channelIndex;
        private readonly CancellationTokenSource _cts = new();

        // ScottPlot 数据容器
        private double[] _xData = Enumerable.Range(0, BufferSize).Select(x => (double)x).ToArray();
        private double[] _yData = new double[BufferSize];
        private int _dataIndex;

        // 采集队列（生产者-消费者模式）
        private readonly ConcurrentQueue<double[,]> _dataQueue = new();

        // 增加时间基准和缓冲区
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private readonly ConcurrentQueue<double[]> _pointQueue = new ConcurrentQueue<double[]>();
        private const int BATCH_SIZE = 500;

        // WpfPlot 控件必须在UI线程操作
        //public WpfPlot PlotControl { get; set; }

        public ForceChartViewModel(IDeviceService device, int slaveNo, int channelIndex)
        {
            _device = device;
            _slaveNo = slaveNo;
            _channelIndex = channelIndex;

            // 初始化绘图
            InitPlot();

            // 启动采集循环
            //Task.Run(ProducerLoop);
            Task.Run(ConsumerLoop);
        }

        public void InitPlot()
        {
            //var plt = PlotControl?.Plot;
            //if (plt == null) return;

            //plt.Title($"Force Monitor - Slave {_slaveNo} Ch{_channelIndex}");
            //plt.XLabel("Position");
            //plt.YLabel("Force (N)");
            //plt.Style = PlotControl?.Style;
            //plt.Palette = Palette.OneHalfDark;
        }
        public void UpdateSeries(double value, int channelIndex)
        {
            if (channelIndex < 0 || channelIndex >= 2) return;
            // 使用高精度时间戳（秒为单位）
            double timestamp = _sw.Elapsed.TotalSeconds;

            // 存入临时队列（线程安全）
            _pointQueue.Enqueue(new[] { timestamp, value });

            // 队列积攒到批次大小时预处理
            if (_pointQueue.Count >= 2)
            {
                var batch = new double[BATCH_SIZE, 2];
                for (int i = 0; i < BATCH_SIZE; i++)
                {
                    if (_pointQueue.TryDequeue(out var point))
                    {
                        batch[i, 0] = point[0]; // 时间
                        batch[i, 1] = point[1]; // 力值
                    }
                }
                _dataQueue.Enqueue(batch); // 主处理队列
            }
        }


        private Random _rand = new Random(); // 随机数生成器
        private uint _simulatedTimeStamp = 0; // 模拟时间戳

        private async Task ProducerLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    int fifoCount = _device.GetAcqFIFOCnt(_slaveNo, _channelIndex);
                    if (fifoCount > BufferSize)
                    {
                        // 批量读取FIFO数据（最高效方式）
                        short[] ain0 = new short[fifoCount], ain1 = new short[fifoCount];
                        short[] ain2 = new short[fifoCount], ain3 = new short[fifoCount];
                        int[] encoder = new int[fifoCount];
                        uint timeStamp = 0;

                        // 打包数据并发往队列
                        var batch = new double[fifoCount, 2];
                        for (int i = 0; i < 100; i++)
                        {
                            _device.GetFifoData( _slaveNo,
                                                ref ain0[0],
                                                ref ain1[0],
                                                ref ain2[0], ref ain3[0], // 仅取前两通道
                                                ref encoder[0],
                                                ref timeStamp
                            );
                            batch[i, 0] = encoder[0];                      // X:位置
                            batch[i, 1] = (double)ain0[0] / 32767.0 * 10.0;        // Y:力值
                        }
                        _dataQueue.Enqueue(batch);
                        if (fifoCount >= MaxPoints)
                        {
                            _device.ResetFifo(_slaveNo, _channelIndex);
                        }
                    }

                    //await Task.Delay(1000); // 读取间隔10ms  */

                  /*  int fifoCount = 1001;// _device.GetAcqFIFOCnt(_slaveNo, _channelIndex);
                    if (fifoCount > BufferSize)
                    {
                        // 初始化数据容器
                        short[] ain0 = new short[fifoCount], ain1 = new short[fifoCount];
                        short[] ain2 = new short[fifoCount], ain3 = new short[fifoCount];
                        int[] encoder = new int[fifoCount];
                        uint timeStamp = 0;

                        // ========== 生成测试数据（替换实际硬件调用）==========
                        int maxValue = 3 * fifoCount; // 定义随机范围 0 ~ 3N
                        for (int i = 0; i < fifoCount; i++)
                        {
                            ain0[i] = (short)_rand.Next(0, maxValue);     // 模拟通道0
                            ain1[i] = (short)_rand.Next(0, maxValue);     // 模拟通道1
                            ain2[i] = (short)_rand.Next(0, maxValue);     // 模拟通道2
                            ain3[i] = (short)_rand.Next(0, maxValue);     // 模拟通道3
                            encoder[i] = _rand.Next(0, maxValue);         // 模拟编码器
                        }
                        _simulatedTimeStamp++; // 时间戳递增
                        timeStamp = _simulatedTimeStamp;
                        // ========== 测试数据生成结束 ==========

                        // 打包数据并发往队列（保持原逻辑）
                        var batch = new double[fifoCount, 2];
                        for (int i = 0; i < fifoCount; i++)
                        {
                            batch[i, 0] = encoder[i];
                            batch[i, 1] = ain0[i] / 32767.0 * 10.0;
                        }
                        _dataQueue.Enqueue(batch);

                        if (fifoCount >= MaxPoints)
                        {
                            _device.ResetFifo(_slaveNo, _channelIndex);
                        }
                    }  */

                    await Task.Delay(10); // 读取间隔10ms
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"数据采集异常: {ex}");
                }
            }
        }

        private async Task ConsumerLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                if (_dataQueue.TryDequeue(out double[,] batch))
                {
                    UpdatePlotData(batch);
                }
                await Task.Delay(10); // 渲染间隔50fps
            }
        }

        private void UpdatePlotData(double[,] batch)
        {
            //if (PlotControl == null) return;

            // 提取时间序列和力值序列
            int count = batch.GetLength(0);
            double[] x = new double[count];
            double[] y = new double[count];

            for (int i = 0; i < 2; i++)
            {
                _xData[i] = batch[i, 0];
                _yData[i] = batch[i, 1];
            }
            // 使用BeginInvoke确保UI线程操作
            //PlotControl.Dispatcher.BeginInvoke(() =>
            //{
            //    var plt = PlotControl.Plot;
            //    plt.Clear();

            //    // 创建可配置的绘图对象
            //    var scatterPlot = plt.Add.Scatter(
            //        xs: _xData,
            //        ys: _yData
            //    );
            //    scatterPlot.LineWidth = 2;
            //    scatterPlot.Color = Colors.SteelBlue;

            //    // 自动缩放坐标轴
            //    plt.Axes.AutoScale();

            //    // 触发渲染
            //    PlotControl.Refresh();
            //});
            //// 更新数据缓冲区
            //for (int i = 0; i < 1; i++)//batch.GetLength(0)
            //{
            //    _xData[_dataIndex] = batch[i, 0];
            //    _yData[_dataIndex] = batch[i, 1];
            //    _dataIndex = (_dataIndex + 1) % BufferSize;
            //}

            //PlotControl.Dispatcher.BeginInvoke(() =>
            //{
            //    var plt = PlotControl.Plot;
            //    plt.Clear();

            //    // 创建可配置的绘图对象
            //    var scatterPlot = plt.Add.Scatter(
            //        xs: _xData,
            //        ys: _yData
            //    );
            //    scatterPlot.LineWidth = 2;
            //    scatterPlot.Color = Colors.SteelBlue;

            //    // 自动缩放坐标轴
            //    plt.Axes.AutoScale();

            //    // 触发渲染
            //    PlotControl.Refresh();
            //});
        }


        public void Dispose()
        {
            _cts?.Cancel();
            _dataQueue.Clear();
        }
    }
}

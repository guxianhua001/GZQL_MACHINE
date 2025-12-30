using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Interfaces
{
    public interface IDataAcquisitionService
    {
        event EventHandler<DataUpdatedEventArgs> DataUpdated;
        void Start();
        void Stop();
        SlaveData CurrentData { get; }
    }
    public class DataAcquisitionService : IDataAcquisitionService
    {
        private CancellationTokenSource _cts;
        private readonly IDeviceService _deviceService;
        private SlaveData _currentData = new();
        private readonly object _lock = new();
        public event EventHandler<DataUpdatedEventArgs> DataUpdated;
        private readonly ConcurrentQueue<SlaveData> _collectBuffer = new();
        private const int BATCH_SIZE = 10;
        private readonly Stopwatch _batchTimer = Stopwatch.StartNew();
        private readonly object _publishLock = new();
        private readonly List<Task> _monitorTasks = new();
        public SlaveData CurrentData
        {
            get { lock (_lock) return (SlaveData)_currentData.Clone(); }
        }
        private readonly Channel<SlaveData> _dataChannel = Channel.CreateBounded<SlaveData>(
        new BoundedChannelOptions(100)
        {
            SingleWriter = true,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        public DataAcquisitionService(IDeviceService deviceService)
        {
            _deviceService = deviceService;
        }

        public void Start()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _deviceService.InitializeEcat();//初始化力控表总线
            Task.Factory.StartNew(
                () => this.RunDataLoop(this._cts.Token),
                this._cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }

        public void Stop() => _cts?.Cancel();
        private async Task RunDataLoop(CancellationToken token)
        {
            // 生产者任务
            var producer = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var data = ReadSlaveData(token);

                        // 非阻塞写入（通道满时自动丢弃最旧数据）
                        if (!_dataChannel.Writer.TryWrite(data))
                        {
                            //Logger.Warn("通道已满，丢弃最旧数据");
                        }

                        await Task.Delay(0, token); // 保持1ms间隔
                    }
                }
                finally
                {
                    _dataChannel.Writer.Complete();
                }
            }, token);
            // 消费者任务
            var consumer = Task.Run(async () =>
            {
                await foreach (var data in _dataChannel.Reader.ReadAllAsync(token))
                {
                    try
                    {
                        UpdateData(data);
                    }
                    catch (Exception ex)
                    {
                        //Logger.Error($"数据处理失败: {ex.Message}");
                    }
                }
            }, token);
            await Task.WhenAll(producer, consumer);
        }

        private SlaveData ReadSlaveData(CancellationToken token)
        {
            var data = new SlaveData();

            // 读取从站1
            data.Slave1.AnalogInputs = ReadParallelAnalogInputs(1);

            // 读取从站2
            data.Slave2.AnalogInputs = ReadParallelAnalogInputs(2);

            return data;
        }
        // 并行优化的模拟量读取方法
        private double[] ReadParallelAnalogInputs(int slaveNo)
        {
            var inputs = new double[2];

            // 并行读取4个输入通道
            Parallel.For(0, 2, i =>
            {
                double raw = _deviceService.GetAnalogInput(slaveNo, i);
                double current = (raw / 32767.0) * 10.0;  // 计算精确电流值
                Interlocked.Exchange(ref inputs[i], current); // 无锁赋值
            });

            return inputs;
        }

        private void UpdateData(SlaveData newData)
        {
            lock (_lock)
            {
                _currentData = newData;
            }
            DataUpdated?.Invoke(this, new DataUpdatedEventArgs(newData));
        }
    }
    public class SlaveData : ICloneable
    {
        public SlaveDeviceData Slave1 { get; set; } = new();
        public SlaveDeviceData Slave2 { get; set; } = new();

        public object Clone() => new SlaveData
        {
            Slave1 = (SlaveDeviceData)Slave1.Clone(),
            Slave2 = (SlaveDeviceData)Slave2.Clone()
        };
    }

    public class SlaveDeviceData : ICloneable
    {
        public int Encoder { get; set; }
        public double[] AnalogInputs { get; set; } = new double[4];

        public object Clone() => new SlaveDeviceData
        {
            Encoder = this.Encoder,
            AnalogInputs = (double[])this.AnalogInputs.Clone()
        };
    }

    public class DataUpdatedEventArgs : EventArgs
    {
        public SlaveData Data { get; }

        public DataUpdatedEventArgs(SlaveData data)
        {
            Data = data;
        }
    }




}

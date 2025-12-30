using Interfaces;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

public class HighPerformanceDataLogger : IDisposable
{
    private readonly ConcurrentQueue<DataPoint> _dataQueue = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly object _fileLock = new();
    private volatile bool _isRecording;
    private Task _processingTask;
    private StreamWriter _writer;
    private BufferedStream _bufferedStream;
    private readonly string _filePath;
    private long _droppedPoints;
    private readonly int _maxQueueSize = 100000; // 10万点缓冲区
    // 停止事件
    private readonly ManualResetEventSlim _stoppedEvent = new();
    public bool IsStopped => _stoppedEvent.IsSet;
    public bool IsRecording => _isRecording;

    public HighPerformanceDataLogger(string filePath)
    {
        _filePath = filePath;
        LogPathBuilder.CreateDirectoryForFile(filePath);
        _stoppedEvent.Set(); // 初始状态为已停止
    }

    public void StartLogging()
    {
        if (_isRecording) return;

        _isRecording = true;
        _droppedPoints = 0;
        _stoppedEvent.Reset(); // 重置停止事件

        // 初始化文件写入器
        InitializeWriter();

        // 启动数据处理任务
        _processingTask = Task.Run(ProcessDataQueue, _cts.Token);
    }

    private void InitializeWriter()
    {
        lock (_fileLock)
        {
            var fileStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write,
                FileShare.Read, 65536, FileOptions.WriteThrough);

            _bufferedStream = new BufferedStream(fileStream, 65536 * 4);
            _writer = new StreamWriter(_bufferedStream);
            _writer.WriteLine("Timestamp,Torque(N),Position(mm)");
        }
    }

    public void AddDataPoint(DataPoint point)
    {
        if (!_isRecording) return;

        // 非阻塞添加，防止队列过大
        if (_dataQueue.Count < _maxQueueSize)
        {
            _dataQueue.Enqueue(point);
        }
        else
        {
            Interlocked.Increment(ref _droppedPoints);
        }
    }

    private void ProcessDataQueue()
    {
        const int batchSize = 1000; // 每批处理1000个点
        var batch = new DataPoint[batchSize];

        while (_isRecording || !_dataQueue.IsEmpty)
        {
            try
            {
                int count = 0;

                // 批量出队
                while (count < batchSize && _dataQueue.TryDequeue(out var point))
                {
                    batch[count] = point;
                    count++;
                }

                if (count > 0)
                {
                    WriteBatchToFile(batch, count);
                }
                else
                {
                    // 无数据时短暂等待（不阻塞UI）
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error(ex, "数据处理异常");
                Thread.Sleep(10);
            }
        }

        FinalizeWriting();
        _stoppedEvent.Set(); // 标记为已停止
    }

    private void WriteBatchToFile(DataPoint[] batch, int count)
    {
        lock (_fileLock)
        {
            for (int i = 0; i < count; i++)
            {
                var point = batch[i];
                _writer.WriteLine($"{point.Timestamp:HH:mm:ss.fff}," +
                                  $"{point.Torque:F3}," +
                                  $"{point.Position:F3}");
            }

            // 批量刷新而不是每次写入都刷新
            _writer.Flush();
        }
    }

    private void FinalizeWriting()
    {
        lock (_fileLock)
        {
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Dispose();
                _writer = null;
            }

            if (_bufferedStream != null)
            {
                _bufferedStream.Dispose();
                _bufferedStream = null;
            }
        }

        if (_droppedPoints > 0)
        {
            IMessage.Logger.Warn($"数据记录丢失点: {_droppedPoints}");
        }
    }

    public void StopLogging()
    {
        if (!_isRecording) return;

        _isRecording = false;
        _cts.Cancel();

        // 等待最多5秒停止
        _stoppedEvent.Wait(TimeSpan.FromSeconds(5));
    }

    public void Dispose()
    {
        StopLogging();
        _cts.Dispose();
        _stoppedEvent.Dispose();
    }
}

    public struct DataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Torque { get; set; }
        public double Position { get; set; }
    }

    // 高精度计时器实现
    public sealed class HighResolutionTimer : IDisposable
    {
        private readonly int _intervalMs;
        private readonly Thread _timerThread;
        private readonly AutoResetEvent _waitEvent = new(false);
        private volatile bool _isRunning;

        public HighResolutionTimer(int intervalMs)
        {
            _intervalMs = intervalMs;
            _timerThread = new Thread(TimerLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.Highest
            };

            _isRunning = true;
            _timerThread.Start();
        }

        private void TimerLoop()
        {
            // 提高计时器精度
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                WinApi.timeBeginPeriod(1);
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            long nextTick = sw.ElapsedTicks;
            long ticksPerMs = System.Diagnostics.Stopwatch.Frequency / 1000;
            long intervalTicks = _intervalMs * ticksPerMs;

            while (_isRunning)
            {
                nextTick += intervalTicks;
                long currentTicks;

                // 自旋等待直到达到精确时间
                while ((currentTicks = sw.ElapsedTicks) < nextTick)
                {
                    // 根据剩余时间选择等待策略
                    long remainingMs = (nextTick - currentTicks) / ticksPerMs;

                    if (remainingMs > 1)
                    {
                        Thread.Sleep(1);
                    }
                    else if (remainingMs > 0.1)
                    {
                        Thread.SpinWait(100);
                    }
                    else
                    {
                        Thread.SpinWait(10);
                    }
                }

                _waitEvent.Set();
            }
        }

        public void WaitForNextTick()
        {
            _waitEvent.WaitOne();
        }

        public void Dispose()
        {
            _isRunning = false;
            _waitEvent.Set();
            _timerThread.Join();
            _waitEvent.Dispose();

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                WinApi.timeEndPeriod(1);
            }
        }
    }

    // Windows API 封装
    internal static class WinApi
    {
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod", SetLastError = true)]
    internal static extern uint timeBeginPeriod(uint period);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod", SetLastError = true)]
    internal static extern uint timeEndPeriod(uint period);
}
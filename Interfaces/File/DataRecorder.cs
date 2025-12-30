using Interfaces;
using System.Threading;
using System;

public class DataRecorder
{
    private HighPerformanceDataLogger _logger;
    private Thread _acquisitionThread;
    private volatile bool _acquisitionRunning;
    private readonly object _syncLock = new();
    private readonly Func<TorquePositionData> _dataReader;

    public DataRecorder(Func<TorquePositionData> dataReader)
    {
        _dataReader = dataReader ?? throw new ArgumentNullException(nameof(dataReader));
    }

    public void StartDataLogging(string moduleName, string barcode,
                                int index, string carrierBarcode)
    {
        lock (_syncLock)
        {
            StopDataLogging(); // 确保停止之前的记录

            string filePath = LogPathBuilder.BuildTorqueRecordPath(
                moduleName, barcode, index, carrierBarcode);

            LogPathBuilder.CreateDirectoryForFile(filePath);

            _logger = new HighPerformanceDataLogger(filePath);
            _acquisitionRunning = true;

            _acquisitionThread = new Thread(DataAcquisitionLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.BelowNormal
            };

            _acquisitionThread.Start();
        }
    }

    public void StopDataLogging()
    {
        lock (_syncLock)
        {
            _acquisitionRunning = false;

            // 停止采集线程
            if (_acquisitionThread != null && _acquisitionThread.IsAlive)
            {
                _acquisitionThread.Join(1000); // 最多等待1秒
                if (_acquisitionThread.IsAlive)
                {
                    try { _acquisitionThread.Interrupt(); }
                    catch { /* 忽略中断异常 */ }
                }
            }

            // 停止记录器
            _logger?.StopLogging();
            _logger?.Dispose();
            _logger = null;
            _acquisitionThread = null;
        }
    }

    private void DataAcquisitionLoop()
    {
        try
        {
            _logger.StartLogging();

            // 高精度计时器
            using var timer = new HighResolutionTimer(1);

            while (_acquisitionRunning && _logger != null)
            {
                timer.WaitForNextTick();

                // 通过委托调用 ViewModel 的数据读取方法
                var data = _dataReader();

                _logger.AddDataPoint(new DataPoint
                {
                    Timestamp = DateTime.Now,
                    Torque = data.Torque,
                    Position = data.Position
                });
            }
        }
        catch (ThreadInterruptedException)
        {
            // 正常中断
        }
        catch (Exception ex)
        {
            IMessage.Logger.Error(ex, "数据采集异常");
        }
        finally
        {
            // 确保记录器停止
            _logger?.StopLogging();
        }
    }

    // 手动停止方法
    public void ManualStop()
    {
        StopDataLogging();
    }

    // 状态查询方法
    public bool IsLoggingActive => _logger?.IsRecording ?? false;

    // 等待记录完成
    public bool WaitForCompletion(TimeSpan timeout)
    {
        return _logger?.IsStopped ?? true;
    }
}
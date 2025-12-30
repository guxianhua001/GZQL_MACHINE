using Core.Utilities;
using NLog;
using System.Threading.Channels;

public class LoggerService : ILoggerService, IDisposable
{
    private readonly Logger _logger;
    private readonly Channel<LogEventArgs> _logChannel;
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cts;

    public event EventHandler<LogEventArgs> LogEvent;

    public LoggerService()
    {
        _logger = LogManager.GetCurrentClassLogger();

        // 创建有界通道，控制内存使用
        _logChannel = Channel.CreateBounded<LogEventArgs>(new BoundedChannelOptions(1000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest // 丢弃最旧的消息
        });

        _cts = new CancellationTokenSource();

        // 启动单个后台处理任务
        _processingTask = Task.Run(ProcessLogEvents);
    }
    public void Trace(string message)
    {
        _logger.Trace(message);
        OnLogEvent("TRACE", message);
    }

    public void Debug(string message)
    {
        _logger.Debug(message);
        OnLogEvent("DEBUG", message);
    }

    public void Info(string message)
    {
        _logger.Info(message);
        OnLogEvent("INFO", message);
    }

    public void Warn(string message)
    {
        _logger.Warn(message);
        OnLogEvent("WARN", message);
    }

    public void Error(string message)
    {
        _logger.Error(message);
        OnLogEvent("ERROR", message);
    }

    public void Error(Exception ex, string message)
    {
        _logger.Error(ex, message);
        OnLogEvent("ERROR", message, ex);
    }

    public void Fatal(string message)
    {
        _logger.Fatal(message);
        OnLogEvent("FATAL", message);
    }

    public void Fatal(Exception ex, string message)
    {
        _logger.Fatal(ex, message);
        OnLogEvent("FATAL", message, ex);
    }
    private async Task ProcessLogEvents()
    {
        var reader = _logChannel.Reader;

        try
        {
            await foreach (var logEvent in reader.ReadAllAsync(_cts.Token))
            {
                try
                {
                    LogEvent?.Invoke(this, logEvent);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Log event handler error: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
    }

    protected virtual void OnLogEvent(string level, string message, Exception exception = null)
    {
        var logEventArgs = new LogEventArgs(level, message, exception);

        // 同步添加到全局缓存
        GlobalLogCache.AddLog(logEventArgs);

        // 异步写入通道（非常快速，无线程池开销）
        _ = _logChannel.Writer.TryWrite(logEventArgs);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _logChannel.Writer.Complete();
        _processingTask?.Wait(TimeSpan.FromSeconds(5));
        _cts?.Dispose();
    }
}
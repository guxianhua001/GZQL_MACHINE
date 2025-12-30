using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;

public class LogEntry : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private string _message;
    private LogEntryLevel _level;
    private DateTime _timestamp = DateTime.Now;

    public string Message
    {
        get => _message;
        set => SetField(ref _message, value);
    }

    public LogEntryLevel Level
    {
        get => _level;
        set => SetField(ref _level, value);
    }

    public DateTime Timestamp
    {
        get => _timestamp;
        set => SetField(ref _timestamp, value);
    }

    // 添加一个只读属性用于显示完整的日志信息（可选）
    public string FullLog => $"[{Timestamp:HH:mm:ss}] {Message}";

    public LogEntry(string message, LogEntryLevel level)
    {
        Message = message;
        Level = level;
    }

    public LogEntry() : this(string.Empty, LogEntryLevel.Info) { }

    protected void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public enum LogEntryLevel
{
    Info,
    Warning,
    Error,
    Success,
    Exception,
    CriticalAlert
}

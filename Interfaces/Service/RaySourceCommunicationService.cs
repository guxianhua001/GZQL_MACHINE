using Prism.Events;
using Prism.Mvvm;
using System;
using System.ComponentModel;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Interfaces.Services
{
    public interface IRaySourceCommunicationService
    {
        bool IsConnected { get; }
        string PortName { get; set; }
        RaySourceStatus CurrentStatus { get; }
        event EventHandler<string> SendDataReceived;
        event EventHandler<string> ReceiveDataReceived;
        event EventHandler<string> StatusMessage;
        event EventHandler<RaySourceStatus> StatusChanged;

        Task ConnectAsync();
        Task DisconnectAsync();
        Task<string> SendCommandAsync(string command, string data = null);
    }
    public class RaySourceCommunicationService : IRaySourceCommunicationService
    {
        private SerialPort _serialPort;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private bool _isReceiving;
        private readonly StringBuilder _responseBuffer = new StringBuilder();

        public bool IsConnected => _serialPort?.IsOpen ?? false;
        public string PortName { get; set; } = "COM1";

        public event EventHandler<string> SendDataReceived;
        public event EventHandler<string> ReceiveDataReceived;
        public event EventHandler<string> StatusMessage;
        public event EventHandler<RaySourceStatus> StatusChanged;

        private RaySourceStatus _currentStatus = new RaySourceStatus();
        public RaySourceStatus CurrentStatus
        {
            get => _currentStatus;
            private set
            {
                _currentStatus = value;
                StatusChanged?.Invoke(this, value);
            }
        }

        public async Task ConnectAsync()
        {
            if (IsConnected) return;

            try
            {
                _serialPort = new SerialPort(PortName)
                {
                    BaudRate = 38400,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Parity = Parity.None,
                    Handshake = Handshake.None,
                    ReadTimeout = 2000,
                    WriteTimeout = 2000
                };

                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();

                OnStatusMessage("连接成功");

                // 初始状态查询
                await SendCommandAsync("sts");
                await SendCommandAsync("shv");
                await SendCommandAsync("scu");
            }
            catch (Exception ex)
            {
                OnStatusMessage($"连接失败: {ex.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            if (!IsConnected) return;

            try
            {
                _serialPort.DataReceived -= SerialPort_DataReceived;
                _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;

                OnStatusMessage("已断开连接");
                CurrentStatus = new RaySourceStatus();
            }
            catch (Exception ex)
            {
                OnStatusMessage($"断开连接错误: {ex.Message}");
            }
        }

        public async Task<string> SendCommandAsync(string command, string data = null)
        {
            if (!IsConnected)
            {
                OnStatusMessage("未连接, 无法发送命令");
                return null;
            }

            await _semaphore.WaitAsync();
            try
            {
                string fullCommand = data == null ?
                    $"{command}\r" :
                    $"{command} {data}\r";

                OnSendData(fullCommand);
                _serialPort.Write(fullCommand);

                // 等待响应
                _responseBuffer.Clear();
                _isReceiving = true;

                // 等待响应或超时
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                while (_isReceiving && !cts.Token.IsCancellationRequested)
                {
                    await Task.Delay(50);
                }

                if (cts.Token.IsCancellationRequested)
                {
                    OnStatusMessage($"命令 {command} 超时");
                    return "TIMEOUT";
                }

                return _responseBuffer.ToString();
            }
            catch (Exception ex)
            {
                OnStatusMessage($"发送命令错误: {ex.Message}");
                return null;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                while (_serialPort.BytesToRead > 0)
                {
                    char receivedChar = (char)_serialPort.ReadChar();
                    _responseBuffer.Append(receivedChar);

                    // 换行符或回车符表示命令结束
                    if (receivedChar == '\r' || receivedChar == '\n')
                    {
                        string response = _responseBuffer.ToString().Trim();
                        OnReceiveData(response);
                        _isReceiving = false;

                        // 解析响应
                        ParseResponse(response);
                    }
                }
            }
            catch (Exception ex)
            {
                OnStatusMessage($"接收数据错误: {ex.Message}");
            }
        }

        private void ParseResponse(string response)
        {
            if (string.IsNullOrEmpty(response)) return;

            string[] parts = response.Split(' ');
            string command = parts[0].ToLower();

            switch (command)
            {
                case "sts":
                    if (parts.Length > 1 && int.TryParse(parts[1], out int status))
                        CurrentStatus.State = (RaySourceState)status;
                    break;

                case "shv":
                    if (parts.Length > 1 && double.TryParse(parts[1], out double actualVoltage))
                        CurrentStatus.ActualVoltage = actualVoltage;
                    break;

                case "scu":
                    if (parts.Length > 1 && double.TryParse(parts[1], out double actualCurrent))
                        CurrentStatus.ActualCurrent = actualCurrent;
                    break;

                case "spv":
                    if (parts.Length > 1 && double.TryParse(parts[1], out double setVoltage))
                        CurrentStatus.SetVoltage = setVoltage;
                    break;

                case "spc":
                    if (parts.Length > 1 && double.TryParse(parts[1], out double setCurrent))
                        CurrentStatus.SetCurrent = setCurrent;
                    break;

                case "ztb":
                    if (parts.Length > 1 && int.TryParse(parts[1], out int testResult))
                        CurrentStatus.TestResult = testResult == 1;
                    break;

                case "swe":
                    if (parts.Length > 1 && int.TryParse(parts[1], out int warmupStatus))
                        CurrentStatus.WarmupStatus = (WarmupStatus)warmupStatus;
                    break;

                case "err":
                    OnStatusMessage($"返回错误: {response}");
                    break;
            }
        }

        protected void OnSendData(string data) => SendDataReceived?.Invoke(this, data);
        protected void OnReceiveData(string data) => ReceiveDataReceived?.Invoke(this, data);
        protected void OnStatusMessage(string message) => StatusMessage?.Invoke(this, message);
    }

    public class RaySourceStatus : BindableBase
    {
        public RaySourceState State { get; set; } = RaySourceState.Standby;
        public double ActualVoltage { get; set; }
        public double ActualCurrent { get; set; }
        public bool IsTestPassed { get; set; } = false;
        public bool TestResult { get; set; }
        private double _voltageStep = 1.0;
        private double _currentStep = 100.0;
        private double _setVoltage = 20.0;
        private double _setCurrent = 100.0;
        private bool _setVoltageChanged = false;
        private bool _setCurrentChanged = false;
        // 增加步长属性，便于控制增减幅度
        public double VoltageStep
        {
            get => _voltageStep;
            set => SetProperty(ref _voltageStep, value);
        }

        public double CurrentStep
        {
            get => _currentStep;
            set => SetProperty(ref _currentStep, value);
        }

        // 电压设置范围验证 (kV)
        private double _minVoltage = 20.0;
        private double _maxVoltage = 300.0;

        public double SetVoltage
        {
            get => _setVoltage;
            set
            {
                // 确保电压在安全范围内
                var clampedValue = Math.Clamp(value, _minVoltage, _maxVoltage);
                if (clampedValue == _setVoltage) return;

                _setVoltageChanged = true;
                SetProperty(ref _setVoltage, clampedValue);
            }
        }

        // 电流设置范围验证 (μA)
        private double _minCurrent = 100.0;
        private double _maxCurrent = 10000.0;

        public double SetCurrent
        {
            get => _setCurrent;
            set
            {
                // 确保电流在安全范围内
                var clampedValue = Math.Clamp(value, _minCurrent, _maxCurrent);
                if (clampedValue == _setCurrent) return;

                _setCurrentChanged = true;
                SetProperty(ref _setCurrent, clampedValue);
            }
        }
        private WarmupStatus _warmupStatus = WarmupStatus.ReadyNotStarted;
        public WarmupStatus WarmupStatus
        {
            get => _warmupStatus;
            set => SetProperty(ref _warmupStatus, value);
        }

        private double _warmupTimeRequired = 60.0; // 默认60秒热机时间
        public double WarmupTimeRequired
        {
            get => _warmupTimeRequired;
            set => SetProperty(ref _warmupTimeRequired, value);
        }

        private double _warmupTimeElapsed = 0.0;

        public event PropertyChangedEventHandler? PropertyChanged;

        public double WarmupTimeElapsed
        {
            get => _warmupTimeElapsed;
            set => SetProperty(ref _warmupTimeElapsed, value);
        }
        private int _overloadCount;
        public int OverloadCount
        {
            get => _overloadCount;
            set => SetProperty(ref _overloadCount, value);
        }

        private DateTime _lastOverloadTime;
        public DateTime LastOverloadTime
        {
            get => _lastOverloadTime;
            set => SetProperty(ref _lastOverloadTime, value);
        }

        private string _lastOverloadReason = "";
        public string LastOverloadReason
        {
            get => _lastOverloadReason;
            set => SetProperty(ref _lastOverloadReason, value);
        }

        public void LogOverloadEvent(string reason)
        {
            OverloadCount++;
            LastOverloadTime = DateTime.Now;
            LastOverloadReason = reason;
        }

        // 重置过载计数
        public void ResetOverloadCounter()
        {
            OverloadCount = 0;
            LastOverloadReason = "";
        }
    }

    public enum RaySourceState
    {
        Standby = 0,        // 等待热机
        WarmingUp = 1,      // 热机进行中
        Ready = 2,          // 准备发射
        Active = 3,         // 正在发射
        Overloaded = 4,     // 过载保护
        Error = 5,          // 不能发射
        Testing = 6         // 自测进行中
    }

    public enum WarmupStatus
    {
        Complete = 0,
        InProgress = 1,
        ReadyNotStarted = 2
    }
}
// 过载复位结果事件
public class OverloadResetEvent
{
    public string RayName { get; set; }
    public string ResetResult { get; set; }
    public DateTime Timestamp { get; set; }
}

// 过载复位完成事件（用于EventAggregator）
public class OverloadResetCompletedEvent : PubSubEvent<OverloadResetEvent> { }

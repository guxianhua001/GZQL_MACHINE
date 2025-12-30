using Interfaces.Services;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Framework.ViewModels
{
    public class RaySourceDebugViewModel : BindableBase
    {
        private readonly IRaySourceCommunicationService _communicationService;
        public IRaySourceCommunicationService CommunicationService => _communicationService;
        private string _statusMessage = "就绪";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _commandText = "sts";
        public string CommandText
        {
            get => _commandText;
            set => SetProperty(ref _commandText, value);
        }

        private string _parameterText;
        public string ParameterText
        {
            get => _parameterText;
            set => SetProperty(ref _parameterText, value);
        }

        public ObservableCollection<string> AvailablePorts { get; } = new ObservableCollection<string>(
            SerialPort.GetPortNames()
        );

        public ObservableCollection<string> CommunicationLog { get; } = new ObservableCollection<string>();

        public DelegateCommand ConnectCommand { get; }
        public DelegateCommand DisconnectCommand { get; }
        public DelegateCommand SendCommand { get; }
        public DelegateCommand ClearLogCommand { get; }

        // X-Ray 控制命令
        public DelegateCommand TurnOnCommand { get; }
        public DelegateCommand TurnOffCommand { get; }
        public DelegateCommand SetVoltageCommand { get; }
        public DelegateCommand SetCurrentCommand { get; }
        public DelegateCommand RunSelfTestCommand { get; }
        public DelegateCommand ResetProtectionCommand { get; }

        public RaySourceDebugViewModel(IRaySourceCommunicationService communicationService)
        {
            _communicationService = communicationService;

            // 注册通信服务事件
            _communicationService.SendDataReceived += OnSendDataReceived;
            _communicationService.ReceiveDataReceived += OnReceiveDataReceived;
            _communicationService.StatusMessage += OnStatusMessageReceived;
            _communicationService.StatusChanged += OnStatusChanged;
            // 初始化命令
            ConnectCommand = new DelegateCommand(async () => await ConnectAsync());
            DisconnectCommand = new DelegateCommand(async () => await DisconnectAsync());
            SendCommand = new DelegateCommand(async () => await SendAsync());
            ClearLogCommand = new DelegateCommand(() => CommunicationLog.Clear());

            TurnOnCommand = new DelegateCommand(async () => await _communicationService.SendCommandAsync("XON"));
            TurnOffCommand = new DelegateCommand(async () => await _communicationService.SendCommandAsync("XOF"));
            SetVoltageCommand = new DelegateCommand(async () =>
            {
                await _communicationService.SendCommandAsync("HIV", ParameterText);
                await _communicationService.SendCommandAsync("spv");
            });

            SetCurrentCommand = new DelegateCommand(async () =>
            {
                await _communicationService.SendCommandAsync("CUR", ParameterText);
                await _communicationService.SendCommandAsync("spc");
            });

            RunSelfTestCommand = new DelegateCommand(async () =>
            {
                await _communicationService.SendCommandAsync("TSF");
            });

            ResetProtectionCommand = new DelegateCommand(async () =>
            {
                await _communicationService.SendCommandAsync("RST");
            });
        }
        // 状态变化事件处理
        private void OnStatusChanged(object sender, RaySourceStatus status)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 根据状态更新UI
                // 例如：如果状态是过载保护，显示警告
                if (status.State == RaySourceState.Overloaded)
                {
                    StatusMessage = "警告：过载保护激活！";
                }
            });
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs args)
        {
            base.OnPropertyChanged(args);

            // 当通信状态变化时执行操作
            if (args.PropertyName == nameof(CommunicationService) &&
                CommunicationService != null)
            {
                CommunicationService.StatusChanged += OnStatusChanged;
            }
        }
        private async Task ConnectAsync()
        {
            await _communicationService.ConnectAsync();
        }

        private async Task DisconnectAsync()
        {
            await _communicationService.DisconnectAsync();
        }

        private async Task SendAsync()
        {
            await _communicationService.SendCommandAsync(CommandText, ParameterText);
        }

        private void OnSendDataReceived(object sender, string data)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CommunicationLog.Insert(0, $"[发送] {DateTime.Now:HH:mm:ss}: {data}");
                if (CommunicationLog.Count > 100) CommunicationLog.RemoveAt(CommunicationLog.Count - 1);
            });
        }

        private void OnReceiveDataReceived(object sender, string data)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                CommunicationLog.Insert(0, $"[接收] {DateTime.Now:HH:mm:ss}: {data}");
                if (CommunicationLog.Count > 100) CommunicationLog.RemoveAt(CommunicationLog.Count - 1);
            });
        }

        private void OnStatusMessageReceived(object sender, string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusMessage = message;
            });
        }

    }
}

using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using TCPIPModule.Interfaces;

namespace TCPIPModule.ViewModels
{
    /// <summary>
    /// TCPIP配置管理ViewModel
    /// 使用IAppSettingService持久化到appsettings.json，不依赖配方项目
    /// 订阅ITCPEventService事件，实时显示收发消息
    /// </summary>
    public class TcpConfigViewModel : BindableBase
    {
        private readonly IAppSettingService _appSettingService;
        private readonly ITCPClientManagerService _tcpClientManagerService;
        private readonly ITCPEventService _tcpEventService;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;

        /// <summary> 连接模式选项 </summary>
        public ObservableCollection<string> ModeOptions { get; } = new() { "Client", "Server" };

        /// <summary> 编码方式选项 </summary>
        public ObservableCollection<string> EncodingOptions { get; } = new() { "UTF-8", "ASCII", "GB2312", "Unicode" };

        #region 属性

        private ObservableCollection<TcpConfigItem> _configItems = new();
        /// <summary> 配置项列表 </summary>
        public ObservableCollection<TcpConfigItem> ConfigItems
        {
            get => _configItems;
            set => SetProperty(ref _configItems, value);
        }

        private TcpConfigItem? _selectedConfig;
        /// <summary> 当前选中的配置项 </summary>
        public TcpConfigItem? SelectedConfig
        {
            get => _selectedConfig;
            set => SetProperty(ref _selectedConfig, value);
        }

        private string _testResult = string.Empty;
        /// <summary> 测试连接结果 </summary>
        public string TestResult
        {
            get => _testResult;
            set => SetProperty(ref _testResult, value);
        }

        private bool _isTesting;
        /// <summary> 是否正在测试连接 </summary>
        public bool IsTesting
        {
            get => _isTesting;
            set => SetProperty(ref _isTesting, value);
        }

        /// <summary> 消息日志列表，记录所有收发消息 </summary>
        public ObservableCollection<TcpMessageLog> MessageLogs { get; } = new();

        private int _maxLogCount = 200;
        /// <summary> 最大日志保留条数 </summary>
        public int MaxLogCount
        {
            get => _maxLogCount;
            set => SetProperty(ref _maxLogCount, value);
        }

        private bool _autoScroll = true;
        /// <summary> 是否自动滚动到最新消息 </summary>
        public bool AutoScroll
        {
            get => _autoScroll;
            set => SetProperty(ref _autoScroll, value);
        }

        #endregion

        #region 命令

        /// <summary> 添加配置项命令 </summary>
        public DelegateCommand AddConfigCommand { get; }

        /// <summary> 删除配置项命令 </summary>
        public DelegateCommand DeleteConfigCommand { get; }

        /// <summary> 保存配置命令 </summary>
        public DelegateCommand SaveConfigCommand { get; }

        /// <summary> 测试连接命令 </summary>
        public DelegateCommand TestConnectionCommand { get; }

        /// <summary> 清空消息日志命令 </summary>
        public DelegateCommand ClearLogsCommand { get; }

        /// <summary> 发送自定义消息命令 </summary>
        public DelegateCommand SendCustomMessageCommand { get; }

        private string _customMessage = string.Empty;
        /// <summary> 自定义发送消息内容 </summary>
        public string CustomMessage
        {
            get => _customMessage;
            set => SetProperty(ref _customMessage, value);
        }

        #endregion

        public TcpConfigViewModel(
            IAppSettingService appSettingService,
            ITCPClientManagerService tcpClientManagerService,
            ITCPEventService tcpEventService,
            ILoggerService logger,
            ILocalizationService localization)
        {
            _appSettingService = appSettingService;
            _tcpClientManagerService = tcpClientManagerService;
            _tcpEventService = tcpEventService;
            _logger = logger;
            _localization = localization;

            AddConfigCommand = new DelegateCommand(ExecuteAddConfig);
            DeleteConfigCommand = new DelegateCommand(ExecuteDeleteConfig, () => SelectedConfig != null)
                .ObservesProperty(() => SelectedConfig);
            SaveConfigCommand = new DelegateCommand(async () => await ExecuteSaveConfigAsync());
            TestConnectionCommand = new DelegateCommand(async () => await ExecuteTestConnectionAsync(),
                    () => SelectedConfig != null && !IsTesting)
                .ObservesProperty(() => SelectedConfig)
                .ObservesProperty(() => IsTesting);
            ClearLogsCommand = new DelegateCommand(ExecuteClearLogs);
            SendCustomMessageCommand = new DelegateCommand(async () => await ExecuteSendCustomMessageAsync(),
                    () => SelectedConfig != null && !string.IsNullOrWhiteSpace(CustomMessage))
                .ObservesProperty(() => SelectedConfig)
                .ObservesProperty(() => CustomMessage);

            SubscribeTcpEvents();
            LoadConfigFromAppSettings();

            // 回放当前已连接客户端的上线状态，解决首次启动时上线日志丢失问题
            _tcpEventService.ReplayConnectedClients();
        }

        /// <summary>
        /// 订阅TCP事件服务的消息接收事件，实时记录收发消息到日志
        /// </summary>
        private void SubscribeTcpEvents()
        {
            _tcpEventService.CameraMessageReceived += OnMessageReceived;
            _tcpEventService.ClientConnected += (name, ip, port) =>
                AddLog("System", string.Format(_localization.GetResourceOrDefault("Tcp_ClientConnected", "[{0}] Connected ({1}:{2})"), name, ip, port));
            _tcpEventService.ClientDisconnected += (name, ip, port) =>
                AddLog("System", string.Format(_localization.GetResourceOrDefault("Tcp_ClientDisconnected", "[{0}] Disconnected ({1}:{2})"), name, ip, port));
            _tcpEventService.ClientError += (name, ip, port, error) =>
                AddLog("System", string.Format(_localization.GetResourceOrDefault("Tcp_ClientError", "[{0}] ⚠ {1}"), name, error));
        }

        /// <summary>
        /// 收到消息事件处理：添加到日志列表
        /// </summary>
        private void OnMessageReceived(string cameraName, string message)
        {
            AddLog("Receive", cameraName, message);
        }

        /// <summary>
        /// 添加消息日志记录，自动限制最大条数
        /// </summary>
        private void AddLog(string direction, string clientName, string message)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                var log = new TcpMessageLog
                {
                    Direction = direction,
                    ClientName = clientName,
                    Message = message
                };

                MessageLogs.Add(log);

                while (MessageLogs.Count > MaxLogCount)
                    MessageLogs.RemoveAt(0);
            });
        }

        /// <summary>
        /// 添加系统日志（方向为System）
        /// </summary>
        private void AddLog(string clientName, string message)
        {
            AddLog("System", clientName, message);
        }

        #region 命令实现

        /// <summary>
        /// 添加新的TCP配置项
        /// </summary>
        private void ExecuteAddConfig()
        {
            var newItem = new TcpConfigItem
            {
                Name = $"TCP_{ConfigItems.Count + 1}",
                Mode = "Client",
                IP = "127.0.0.1",
                Port = 8080,
                Timeout = 5000,
                Encoding = "UTF-8",
                IsEnabled = true,
                Description = string.Empty
            };
            ConfigItems.Add(newItem);
            SelectedConfig = newItem;
            _logger.Info($"添加TCP配置项: {newItem.Name}");
        }

        /// <summary>
        /// 删除当前选中的配置项
        /// </summary>
        private void ExecuteDeleteConfig()
        {
            if (SelectedConfig == null) return;
            var name = SelectedConfig.Name;
            ConfigItems.Remove(SelectedConfig);
            SelectedConfig = ConfigItems.Count > 0 ? ConfigItems[0] : null;
            _logger.Info($"删除TCP配置项: {name}");
        }

        /// <summary>
        /// 保存配置到appsettings.json（通过IAppSettingService）
        /// 根据Mode区分处理：Client模式创建客户端连接，Server模式启动TCP服务器监听
        /// </summary>
        private async System.Threading.Tasks.Task ExecuteSaveConfigAsync()
        {
            try
            {
                var existingClients = _appSettingService.Clients.ToList();

                // 先清理已删除或模式变更的旧连接/服务器
                foreach (var existing in existingClients)
                {
                    if (!ConfigItems.Any(c => c.Name == existing.ClientName))
                    {
                        // 已删除的配置：停止指定服务器或移除客户端
                        if (existing.Mode == "Server")
                            _tcpEventService.StopServer(existing.ClientName);
                        else
                            await _tcpClientManagerService.RemoveClientAsync(existing.ClientName);
                        _appSettingService.RemoveClient(existing.ClientName);
                    }
                }

                // 逐项保存并启动
                foreach (var item in ConfigItems)
                {
                    var existing = _appSettingService.GetClient(item.Name);
                    if (existing != null)
                    {
                        // 模式变更时先停止旧的
                        if (existing.Mode == "Server")
                            _tcpEventService.StopServer(item.Name);
                        else
                            await _tcpClientManagerService.RemoveClientAsync(item.Name);

                        _appSettingService.RemoveClient(item.Name);
                    }

                    var newConfig = new ClientConfiguration
                    {
                        ClientName = item.Name,
                        Mode = item.Mode,
                        IP = item.IP,
                        Port = item.Port,
                        IsEnabled = item.IsEnabled,
                        Description = item.Description
                    };

                    _appSettingService.AddClient(newConfig);

                    if (item.IsEnabled)
                    {
                        if (item.Mode == "Server")
                        {
                            // 服务端模式：启动TCP服务器监听指定端口
                            var serverConfig = new Core.Models.ServerConfiguration
                            {
                                ServerIP = item.IP,
                                Port = item.Port,
                                EncodingMethod = item.Encoding
                            };
                            _tcpEventService.StartServer(serverConfig, item.Name);
                            _logger.Info($"TCP服务器 [{item.Name}] 已启动监听 {item.IP}:{item.Port}");
                        }
                        else
                        {
                            // 客户端模式：创建TCP客户端连接到远程服务端
                            await _tcpClientManagerService.AddClientAsync(item.Name, newConfig);
                        }
                    }
                }

                _appSettingService.Save();
                TestResult = string.Format(_localization.GetResourceOrDefault("Tcp_SaveSuccess", "Saved successfully, {0} config(s)"), ConfigItems.Count);
                _logger.Info($"TCP配置保存成功，共 {ConfigItems.Count} 项");
            }
            catch (System.Exception ex)
            {
                TestResult = string.Format(_localization.GetResourceOrDefault("Tcp_SaveFailed", "Save failed: {0}"), ex.Message);
                _logger.Error(ex, "保存TCP配置失败");
            }
        }

        /// <summary>
        /// 测试选中配置项的TCP连接
        /// 使用SendCommandAsync发送测试命令，不等待响应
        /// </summary>
        private async System.Threading.Tasks.Task ExecuteTestConnectionAsync()
        {
            if (SelectedConfig == null) return;

            IsTesting = true;
            TestResult = string.Format(_localization.GetResourceOrDefault("Tcp_TestingConnection", "Testing connection {0}:{1} ..."), SelectedConfig.IP, SelectedConfig.Port);

            var sendMsg = "TEST";
            AddLog("Send", SelectedConfig.Name, sendMsg);

            try
            {
                var sent = await _tcpEventService.SendCommandAsync(
                    SelectedConfig.Name,
                    sendMsg,
                    SelectedConfig.Timeout);

                TestResult = sent
                    ? string.Format(_localization.GetResourceOrDefault("Tcp_TestSuccess", "Connection {0}:{1} successful, test command sent"), SelectedConfig.IP, SelectedConfig.Port)
                    : string.Format(_localization.GetResourceOrDefault("Tcp_TestFailed", "Connection {0}:{1} failed, client not connected or timeout"), SelectedConfig.IP, SelectedConfig.Port);
                _logger.Info($"TCP连接测试{(sent ? "成功" : "失败")}: {SelectedConfig.Name} ({SelectedConfig.IP}:{SelectedConfig.Port})");
            }
            catch (System.Exception ex)
            {
                TestResult = string.Format(_localization.GetResourceOrDefault("Tcp_ConnectionFailed", "Connection failed: {0}"), ex.Message);
                _logger.Error(ex, $"TCP连接测试失败: {SelectedConfig.Name} ({SelectedConfig.IP}:{SelectedConfig.Port})");
            }
            finally
            {
                IsTesting = false;
            }
        }

        /// <summary>
        /// 发送自定义消息到选中的客户端
        /// 使用SendCommandAsync单向发送，不等待响应
        /// 发送后数据会通过DataReceived事件自动显示在日志中
        /// </summary>
        private async System.Threading.Tasks.Task ExecuteSendCustomMessageAsync()
        {
            if (SelectedConfig == null || string.IsNullOrWhiteSpace(CustomMessage)) return;

            AddLog("Send", SelectedConfig.Name, CustomMessage);

            try
            {
                var sent = await _tcpEventService.SendCommandAsync(
                    SelectedConfig.Name,
                    CustomMessage,
                    SelectedConfig.Timeout);

                if (!sent)
                    AddLog("System", SelectedConfig.Name, _localization.GetResourceOrDefault("Tcp_SendFailedNotConnected", "Send failed: client not connected or timeout"));
            }
            catch (System.Exception ex)
            {
                AddLog("System", SelectedConfig.Name, string.Format(_localization.GetResourceOrDefault("Tcp_SendFailed", "Send failed: {0}"), ex.Message));
            }
        }

        /// <summary>
        /// 清空消息日志
        /// </summary>
        private void ExecuteClearLogs()
        {
            MessageLogs.Clear();
        }

        #endregion

        /// <summary>
        /// 从IAppSettingService加载TCP配置（来源于appsettings.json）
        /// </summary>
        private void LoadConfigFromAppSettings()
        {
            try
            {
                ConfigItems.Clear();
                foreach (var client in _appSettingService.Clients)
                {
                    ConfigItems.Add(new TcpConfigItem
                    {
                        Name = client.ClientName,
                        Mode = string.IsNullOrEmpty(client.Mode) ? "Client" : client.Mode,
                        IP = client.IP,
                        Port = client.Port,
                        IsEnabled = client.IsEnabled,
                        Description = client.Description,
                        Timeout = 5000,
                        Encoding = "UTF-8"
                    });
                }
                SelectedConfig = ConfigItems.Count > 0 ? ConfigItems[0] : null;
                _logger.Info($"加载TCP配置成功，共 {ConfigItems.Count} 项");
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, "加载TCP配置失败");
            }
        }
    }
}

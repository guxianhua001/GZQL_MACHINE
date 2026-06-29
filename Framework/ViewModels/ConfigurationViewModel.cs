using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using Core.Models;
using Core.Utilities;
using Core.Abstraction;

namespace Framework.ViewModels
{
    public class ConfigurationViewModel : BindableBase
    {
        private readonly IAppSettingService _appConfig;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;

        public ConfigurationViewModel(
            IAppSettingService appConfig,
            IEventAggregator eventAggregator,
            ILoggerService logger,
            ILocalizationService localization)
        {
            _appConfig = appConfig;
            _eventAggregator = eventAggregator;
            _logger = logger;
            _localization = localization;

            // 初始化命令
            SaveCommand = new DelegateCommand(SaveConfiguration);
            AddClientCommand = new DelegateCommand(AddClient);
            RemoveClientCommand = new DelegateCommand<string>(RemoveClient);
            MoveClientUpCommand = new DelegateCommand<ClientConfiguration>(MoveClientUp);
            MoveClientDownCommand = new DelegateCommand<ClientConfiguration>(MoveClientDown);

            // 初始化客户端集合
            Clients = new ObservableCollection<ClientConfiguration>();

            // 加载当前配置
            LoadCurrentConfig();

            _logger.Info(_localization.GetResourceOrDefault("CfgVM_Log_InitComplete", "ConfigurationViewModel 初始化完成"));
        }

        #region 命令
        public DelegateCommand SaveCommand { get; }
        public DelegateCommand AddClientCommand { get; }
        public DelegateCommand<string> RemoveClientCommand { get; }
        public DelegateCommand<ClientConfiguration> MoveClientUpCommand { get; }
        public DelegateCommand<ClientConfiguration> MoveClientDownCommand { get; }
        #endregion

        #region 服务器配置属性
        private string _serverIP;
        public string ServerIP
        {
            get => _serverIP;
            set => SetProperty(ref _serverIP, value);
        }

        private int _serverPort;
        public int ServerPort
        {
            get => _serverPort;
            set => SetProperty(ref _serverPort, value);
        }

        private int _maxClients;
        public int MaxClients
        {
            get => _maxClients;
            set => SetProperty(ref _maxClients, value);
        }

        private string _encodingMethod;
        public string EncodingMethod
        {
            get => _encodingMethod;
            set => SetProperty(ref _encodingMethod, value);
        }
        #endregion

        #region 应用程序配置属性
        private string _appName;
        public string AppName
        {
            get => _appName;
            set => SetProperty(ref _appName, value);
        }

        private string _recipeName;
        public string RecipeName
        {
            get => _recipeName;
            set => SetProperty(ref _recipeName, value);
        }

        private string _lastRecipeName;
        public string LastRecipeName
        {
            get => _lastRecipeName;
            set => SetProperty(ref _lastRecipeName, value);
        }

        private string _lastSelectedRecipePath;
        public string LastSelectedRecipePath
        {
            get => _lastSelectedRecipePath;
            set => SetProperty(ref _lastSelectedRecipePath, value);
        }
        #endregion

        #region 设备配置属性
        private bool _enableSafetyGate;
        public bool EnableSafetyGate
        {
            get => _enableSafetyGate;
            set => SetProperty(ref _enableSafetyGate, value);
        }

        private bool _enableBuzzer;
        public bool EnableBuzzer
        {
            get => _enableBuzzer;
            set => SetProperty(ref _enableBuzzer, value);
        }
        #endregion

        #region 客户端配置
        public ObservableCollection<ClientConfiguration> Clients { get; }

        private ClientConfiguration _selectedClient;
        public ClientConfiguration SelectedClient
        {
            get => _selectedClient;
            set => SetProperty(ref _selectedClient, value);
        }

        // 新客户端模板
        private ClientConfiguration _newClient = new ClientConfiguration();
        public ClientConfiguration NewClient
        {
            get => _newClient;
            set => SetProperty(ref _newClient, value);
        }
        #endregion

        #region 方法
        private void LoadCurrentConfig()
        {
            try
            {
                _logger.Info(_localization.GetResourceOrDefault("CfgVM_Log_StartLoadConfig", "开始加载配置"));

                // 加载服务器配置
                ServerIP = _appConfig.ServerConfig.ServerIP;
                ServerPort = _appConfig.ServerConfig.Port;
                MaxClients = _appConfig.ServerConfig.MaxClients;
                EncodingMethod = _appConfig.ServerConfig.EncodingMethod;

                // 加载应用程序配置
                AppName = _appConfig.RecipeName;
                RecipeName = _appConfig.RecipeName;
                LastRecipeName = _appConfig.LastRecipeName;
                LastSelectedRecipePath = _appConfig.LastSelectedRecipePath;

                // 加载客户端配置
                Clients.Clear();
                foreach (var client in _appConfig.Clients)
                {
                    Clients.Add(new ClientConfiguration
                    {
                        ClientName = client.ClientName,
                        IP = client.IP,
                        Port = client.Port,
                        Description = client.Description,
                        IsEnabled = client.IsEnabled
                    });
                }

                // 设置默认选中的客户端
                if (Clients.Any())
                {
                    SelectedClient = Clients.First();
                }

                _logger.Info(string.Format(_localization.GetResourceOrDefault("CfgVM_Log_ConfigLoaded", "配置加载完成，共 {0} 个客户端"), Clients.Count));
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, _localization.GetResourceOrDefault("CfgVM_Log_LoadConfigFailed", "加载配置失败"));
            }
        }

        private void SaveConfiguration()
        {
            try
            {
                _logger.Info(_localization.GetResourceOrDefault("CfgVM_Log_StartSaveConfig", "开始保存配置"));

                // 验证服务器配置
                if (!IPAddress.TryParse(ServerIP, out _))
                {
                    _logger.Warn(_localization.GetResourceOrDefault("CfgVM_Log_ServerIPInvalid", "服务器IP地址格式无效"));
                    // 可以在这里显示错误消息
                    return;
                }

                if (ServerPort < 1 || ServerPort > 65535)
                {
                    _logger.Warn(_localization.GetResourceOrDefault("CfgVM_Log_ServerPortInvalid", "服务器端口号无效"));
                    return;
                }

                // 更新服务器配置
                _appConfig.ServerConfig.ServerIP = ServerIP;
                _appConfig.ServerConfig.Port = ServerPort;
                _appConfig.ServerConfig.MaxClients = MaxClients;
                _appConfig.ServerConfig.EncodingMethod = EncodingMethod;

                // 更新应用程序配置
                //_appConfig.Name = AppName;
                _appConfig.RecipeName = RecipeName;
                _appConfig.LastRecipeName = LastRecipeName;
                _appConfig.LastSelectedRecipePath = LastSelectedRecipePath;

                // 更新客户端配置 - 先清除所有客户端，然后重新添加
                var currentClientNames = _appConfig.Clients.Select(c => c.ClientName).ToList();
                foreach (var clientName in currentClientNames)
                {
                    _appConfig.RemoveClient(clientName);
                }

                foreach (var client in Clients)
                {
                    _appConfig.AddClient(new ClientConfiguration
                    {
                        ClientName = client.ClientName,
                        IP = client.IP,
                        Port = client.Port,
                        Description = client.Description,
                        IsEnabled = client.IsEnabled
                    });
                }

                // 保存配置
                _appConfig.Save();

                // 发布配置更新事件
                _eventAggregator.GetEvent<ConfigurationUpdatedEvent>().Publish();

                _logger.Info(_localization.GetResourceOrDefault("CfgVM_Log_ConfigSaved", "配置保存成功"));
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, _localization.GetResourceOrDefault("CfgVM_Log_SaveConfigFailed", "保存配置失败"));
            }
        }

        private void AddClient()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(NewClient.ClientName))
                {
                    _logger.Warn(_localization.GetResourceOrDefault("CfgVM_Log_ClientNameEmpty", "客户端名称不能为空"));
                    return;
                }

                if (Clients.Any(c => c.ClientName == NewClient.ClientName))
                {
                    _logger.Warn(string.Format(_localization.GetResourceOrDefault("CfgVM_Log_ClientNameExists", "客户端名称 '{0}' 已存在"), NewClient.ClientName));
                    return;
                }

                if (!IPAddress.TryParse(NewClient.IP, out _))
                {
                    _logger.Warn(_localization.GetResourceOrDefault("CfgVM_Log_ClientIPInvalid", "客户端IP地址格式无效"));
                    return;
                }

                if (NewClient.Port < 1 || NewClient.Port > 65535)
                {
                    _logger.Warn(_localization.GetResourceOrDefault("CfgVM_Log_ClientPortInvalid", "客户端端口号无效"));
                    return;
                }

                var newClient = new ClientConfiguration
                {
                    ClientName = NewClient.ClientName.Trim(),
                    IP = NewClient.IP,
                    Port = NewClient.Port,
                    Description = NewClient.Description ?? string.Empty,
                    IsEnabled = true
                };

                Clients.Add(newClient);
                SelectedClient = newClient;

                // 重置新客户端模板
                NewClient = new ClientConfiguration();

                _logger.Info(string.Format(_localization.GetResourceOrDefault("CfgVM_Log_ClientAdded", "添加客户端: {0}"), newClient.ClientName));
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, _localization.GetResourceOrDefault("CfgVM_Log_AddClientFailed", "添加客户端失败"));
            }
        }

        private void RemoveClient(string clientName)
        {
            try
            {
                var client = Clients.FirstOrDefault(c => c.ClientName == clientName);
                if (client != null)
                {
                    Clients.Remove(client);
                    _logger.Info(string.Format(_localization.GetResourceOrDefault("CfgVM_Log_ClientRemoved", "移除客户端: {0}"), clientName));

                    // 更新选中项
                    if (SelectedClient == client)
                    {
                        SelectedClient = Clients.FirstOrDefault();
                    }
                }
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, string.Format(_localization.GetResourceOrDefault("CfgVM_Log_RemoveClientFailed", "移除客户端失败: {0}"), clientName));
            }
        }

        private void MoveClientUp(ClientConfiguration client)
        {
            try
            {
                var index = Clients.IndexOf(client);
                if (index > 0)
                {
                    Clients.Move(index, index - 1);
                    _logger.Debug(string.Format(_localization.GetResourceOrDefault("CfgVM_Log_ClientMovedUp", "客户端 '{0}' 上移"), client.ClientName));
                }
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, string.Format(_localization.GetResourceOrDefault("CfgVM_Log_MoveClientUpFailed", "上移客户端失败: {0}"), client.ClientName));
            }
        }

        private void MoveClientDown(ClientConfiguration client)
        {
            try
            {
                var index = Clients.IndexOf(client);
                if (index < Clients.Count - 1)
                {
                    Clients.Move(index, index + 1);
                    _logger.Debug(string.Format(_localization.GetResourceOrDefault("CfgVM_Log_ClientMovedDown", "客户端 '{0}' 下移"), client.ClientName));
                }
            }
            catch (System.Exception ex)
            {
                _logger.Error(ex, string.Format(_localization.GetResourceOrDefault("CfgVM_Log_MoveClientDownFailed", "下移客户端失败: {0}"), client.ClientName));
            }
        }

        public void RefreshConfiguration()
        {
            LoadCurrentConfig();
            _logger.Info(_localization.GetResourceOrDefault("CfgVM_Log_ConfigRefreshed", "配置已刷新"));
        }
        #endregion
    }

    /// <summary>
    /// 配置更新事件（无参数版本）
    /// </summary>
    public class ConfigurationUpdatedEvent : PubSubEvent { }
}

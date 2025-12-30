using Prism.Mvvm;
using Prism.Commands;
using System;
using System.ComponentModel;
using Microsoft.Win32;
using System.IO;
using SmarterMotion;
using MaterialDesignThemes.Wpf;
using Interfaces.Services;
using Prism.Events;
using Interfaces;
using HSMS;
using Interfaces.Events;
using System.Threading.Tasks;


namespace ModuleCore.ViewModels
{
    public class DeviceConfigViewModel : BindableBase
    {
        // 配置属性
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
        private bool _enableSnCode;
        public bool EnableSnCode
        {
            get => _enableSnCode;
            set
            {
                if (SetProperty(ref _enableSnCode, value))
                {
                    // 当 SN 码使能状态改变时，立即通知
                    PublishConfigChangeIfSaved();
                }
            }
        }
        private bool _isDirty;

        public bool IsDirty
        {
            get => _isDirty;
            set => SetProperty(ref _isDirty, value);
        }

        private string _dataSavePath;
        public string DataSavePath
        {
            get => _dataSavePath;
            set => SetProperty(ref _dataSavePath, value);
        }
        private int _dataRetentionDays = 30;
        public int DataRetentionDays
        {
            get => _dataRetentionDays;
            set => SetProperty(ref _dataRetentionDays, value);
        }

        private bool _autoCleanOldData = true;
        public bool AutoCleanOldData
        {
            get => _autoCleanOldData;
            set => SetProperty(ref _autoCleanOldData, value);
        }
        // 控制扩展器展开状态
        private bool _pinModuleExpanded;
        public bool PinModuleExpanded
        {
            get => _pinModuleExpanded;
            set => SetProperty(ref _pinModuleExpanded, value);
        }
        // 四个拨针模组启用状态
        private bool _isModule1Enabled = true;
        public bool IsModule1Enabled
        {
            get => _isModule1Enabled;
            set
            {
                if (SetProperty(ref _isModule1Enabled, value))
                {
                    // 发布模块状态变更事件
                    _eventAggregator.GetEvent<ModuleStateChangedEvent>()
                        .Publish(new ModuleStateChangedEventArgs
                        {
                            ModuleId = 1,
                            NewState = value
                        });
                }
            }
        }
        private bool _isModule2Enabled = true;
        public bool IsModule2Enabled
        {
            get => _isModule2Enabled;
            set
            {
                if (SetProperty(ref _isModule2Enabled, value))
                {
                    // 发布模块状态变更事件
                    _eventAggregator.GetEvent<ModuleStateChangedEvent>()
                        .Publish(new ModuleStateChangedEventArgs
                        {
                            ModuleId = 2,
                            NewState = value
                        });
                }
            }
        }
        private bool _isModule3Enabled = true;
        public bool IsModule3Enabled
        {
            get => _isModule3Enabled;
            set
            {
                if (SetProperty(ref _isModule3Enabled, value))
                {
                    // 发布模块状态变更事件
                    _eventAggregator.GetEvent<ModuleStateChangedEvent>()
                        .Publish(new ModuleStateChangedEventArgs
                        {
                            ModuleId = 3,
                            NewState = value
                        });
                }
            }
        }
        private bool _isModule4Enabled = true;
        public bool IsModule4Enabled
        {
            get => _isModule4Enabled;
            set
            {
                if (SetProperty(ref _isModule4Enabled, value))
                {
                    // 发布模块状态变更事件
                    _eventAggregator.GetEvent<ModuleStateChangedEvent>()
                        .Publish(new ModuleStateChangedEventArgs
                        {
                            ModuleId = 4,
                            NewState = value
                        });
                }
            }
        }
        // SEC/GEM相关属性
        private bool _enableSecsGem;
        public bool EnableSecsGem
        {
            get => _enableSecsGem;
            set
            {
                if (SetProperty(ref _enableSecsGem, value))
                {
                    // 实时更新设备状态
                    //XMachine.Instance.SECsGemEnabled = value;
                    //SecGemManager.Instance.SetEnabled(value);
                }
            }
        }
        private bool _secsGemAdvancedExpanded;
        public bool SecsGemAdvancedExpanded
        {
            get => _secsGemAdvancedExpanded;
            set => SetProperty(ref _secsGemAdvancedExpanded, value);
        }
        private string _secsGemIP = "127.0.0.1";
        public string SecsGemIP
        {
            get => _secsGemIP;
            set => SetProperty(ref _secsGemIP, value);
        }
        private string _secsGemPort = "5000";
        public string SecsGemPort
        {
            get => _secsGemPort;
            set => SetProperty(ref _secsGemPort, value);
        }
        private string _secsGemDeviceId = "0";
        public string SecsGemDeviceId
        {
            get => _secsGemDeviceId;
            set => SetProperty(ref _secsGemDeviceId, value);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs args)
        {
            base.OnPropertyChanged(args);
            if (args.PropertyName != nameof(IsDirty))
            {
                IsDirty = true;

                if (_enableSafetyGate == true)
                {
                    XMachine.Instance.DoorEnabled = true;
                }
                else
                {
                    XMachine.Instance.DoorEnabled = false;
                }
                if (_enableBuzzer == true)
                {
                    XMachine.Instance.BuzzerEnabled = true;
                    XStationManager.Instance.FindStationById(1).IsEnableBuzzer = true;
                }
                else
                {
                    XMachine.Instance.BuzzerEnabled = false;
                    XStationManager.Instance.FindStationById(1).IsEnableBuzzer = false;
                }
                if (_enableSecsGem == true)
                {
                    _secsGemService.IsEnableSecs = true;
                    _secsGemService.controlMode = 1;
                }
                else
                {
                    _secsGemService.IsEnableSecs = false;
                    _secsGemService.controlMode = 0;
                }
            }
        }

        // 全选命令
        public DelegateCommand CheckAllModulesCommand => new DelegateCommand(() =>
        {
            IsModule1Enabled = true;
            IsModule2Enabled = true;
            IsModule3Enabled = true;
            IsModule4Enabled = true;
        });

        // 清空命令
        public DelegateCommand UncheckAllModulesCommand => new DelegateCommand(() =>
        {
            IsModule1Enabled = false;
            IsModule2Enabled = false;
            IsModule3Enabled = false;
            IsModule4Enabled = false;
        });
        // 操作命令
        public DelegateCommand SaveCommand { get; }
        public DelegateCommand _browsePathCommand;
        public DelegateCommand BrowsePathCommand =>
            _browsePathCommand ??= new DelegateCommand(ExecuteBrowsePath);
        private DelegateCommand _loadDefaultCommand;
        public DelegateCommand LoadDefaultCommand =>
            _loadDefaultCommand ??= new DelegateCommand(ExecuteLoadDefault);
        private void ExecuteLoadDefault()
        {
            EnableSafetyGate = true;  // 安全门默认开启
            EnableBuzzer = false;     // 蜂鸣器默认关闭
            EnableSnCode = true;      // SN码默认开启
            EnableSecsGem = false;    // SECS/GEM默认关闭
            SecsGemIP = "127.0.0.1";  // SECS/GEM IP默认设置为"127.0.0.1"
            SecsGemPort = "5000";     // SECS/GEM端口默认设置为5000
            SecsGemDeviceId = "0";    // SECS/GEM设备ID默认设置为"0"
            IsModule1Enabled = true;  // 模块1默认开启
            IsModule2Enabled = true;  // 模块2默认开启
            IsModule3Enabled = true;  // 模块3默认开启
            IsModule4Enabled = true;  // 模块4默认开启
            DataSavePath = DeviceConfigService.GetDefaultDataPath();
        }
        private readonly ISecsGemService _secsGemService;
        private readonly IEventAggregator _eventAggregator;
        public DeviceConfigViewModel(IEventAggregator eventAggregator, ISecsGemService secsGemService)
        {
            _eventAggregator = eventAggregator;
            _secsGemService = secsGemService;
            // 初始化加载配置
            LoadDeviceConfig();
            // 初始化保存命令
            SaveCommand = new DelegateCommand(ExecuteSave);
            // 订阅配置改变事件
            DeviceConfigService.ConfigChanged += OnConfigChanged;
        }
        private void OnConfigChanged(object sender, ConfigChangedEventArgs e)
        {
            // 可以在这里处理配置改变的逻辑
            //DataSavePath = e.ConfigFile;
        }
        // 在需要的地方获取路径
        private void LogCurrentDataPath()
        {
            // 直接访问静态属性获取当前路径
            string currentPath = DeviceConfigService.CurrentDataSavePath;
            IMessage.Logger.Info($"当前数据保存路径: {currentPath}");

            // 或者通过配置对象获取
            string altPath = DeviceConfigService.CurrentConfig.DataSavePath;
        }

        private void LoadDeviceConfig()
        {
            var config = DeviceConfigService.LoadDeviceConfig();
            EnableSafetyGate = config.EnableSafetyGate;
            EnableBuzzer = config.EnableBuzzer;
            EnableSnCode = config.EnableSnCode;
            EnableSecsGem = config.EnableSecsGem;
            DataSavePath = config.DataSavePath;
            SecsGemIP = config.SecsGemIP;
            SecsGemPort = config.SecsGemPort;
            SecsGemDeviceId = config.SecsGemDeviceId;
            IsModule1Enabled = config.IsModule1Enabled;
            IsModule2Enabled = config.IsModule2Enabled;
            IsModule3Enabled = config.IsModule3Enabled;
            IsModule4Enabled = config.IsModule4Enabled;
            DataRetentionDays = config.DataRetentionDays;
            AutoCleanOldData = config.AutoCleanOldData;
        }
        public void SaveData()
        {
            SaveData();
        }
        private void ExecuteSave()
        {
            try
            {
                var config = new DeviceConfig
                {
                    EnableSafetyGate = this.EnableSafetyGate,
                    EnableBuzzer = this.EnableBuzzer,
                    EnableSnCode = this.EnableSnCode,
                    EnableSecsGem = this.EnableSecsGem,
                    DataSavePath = this.DataSavePath,
                    SecsGemIP = this.SecsGemIP,
                    SecsGemPort = this.SecsGemPort,
                    SecsGemDeviceId = this.SecsGemDeviceId,
                    IsModule1Enabled = this.IsModule1Enabled,
                    IsModule2Enabled = this.IsModule2Enabled,
                    IsModule3Enabled = this.IsModule3Enabled,
                    IsModule4Enabled = this.IsModule4Enabled,
                    DataRetentionDays = this.DataRetentionDays,
                    AutoCleanOldData = this.AutoCleanOldData
                };
                DeviceConfigService.SaveDeviceConfig(config);
                Task.Run(() => DeviceConfigService.CleanupExpiredData());// 保存后立即清理
                // 应用SECS/GEM设置
                if (EnableSecsGem)
                {
                    // 设置并启用 SECS/GEM
                    if (_secsGemService.Initialize(Convert.ToInt32(SecsGemPort), this.SecsGemDeviceId))
                    {
                        IMessage.Logger.Info($"SECS/GEM 已启用: IP={this.SecsGemIP}, Port={this.SecsGemPort}, DeviceID={this.SecsGemDeviceId}");
                        _secsGemService.SetEnabled(true);
                    }
                    else
                    {
                        IMessage.Logger.Error("SECS/GEM 初始化失败");
                    }
                }
                else
                {
                    // 禁用 SECS/GEM
                    _secsGemService.SetEnabled(false);
                }
                // 发布配置更改事件
                _eventAggregator.GetEvent<DeviceConfigChangedEvent>().Publish(config);
                IsDirty = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"配置保存失败：{ex.Message}");
            }

        }
        private void ExecuteBrowsePath()
        {
            // 使用 WPF 的文件夹选择对话框
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                InitialDirectory = DataSavePath,
                Title = "选择配置文件保存目录"
            };

            if (dialog.ShowDialog() == true)
            {
                DataSavePath = dialog.FolderName;
            }
        }
        private void PublishConfigChangeIfSaved()
        {
            if (!IsDirty) // 配置已保存后立即生效
            {
                var currentConfig = new DeviceConfig
                {
                    EnableSnCode = this.EnableSnCode,
                    // 可以选择只包含修改的字段或全部字段
                };
                _eventAggregator.GetEvent<DeviceConfigChangedEvent>().Publish(currentConfig);
            }
        }

    }
}

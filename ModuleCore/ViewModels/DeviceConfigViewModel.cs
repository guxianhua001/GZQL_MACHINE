using Prism.Mvvm;
using Prism.Commands;
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using Core.Utilities;
using Core.Abstraction;
using Core.Configuration;
using Core.Events;
using Prism.Events;


namespace ModuleCore.ViewModels
{
    public class DeviceConfigViewModel : BindableBase
    {
        #region 配置属性

        private bool _enableSafetyGate;
        /// <summary>安全门使能</summary>
        public bool EnableSafetyGate
        {
            get => _enableSafetyGate;
            set => SetProperty(ref _enableSafetyGate, value);
        }

        private bool _enableBuzzer;
        /// <summary>蜂鸣器使能</summary>
        public bool EnableBuzzer
        {
            get => _enableBuzzer;
            set => SetProperty(ref _enableBuzzer, value);
        }

        private bool _enableSnCode;
        /// <summary>SN码使能</summary>
        public bool EnableSnCode
        {
            get => _enableSnCode;
            set
            {
                if (SetProperty(ref _enableSnCode, value))
                {
                    PublishConfigChangeIfSaved();
                }
            }
        }

        private bool _enableGrating;
        /// <summary>光栅使能</summary>
        public bool EnableGrating
        {
            get => _enableGrating;
            set => SetProperty(ref _enableGrating, value);
        }

        private bool _enableSafetyEventLog;
        /// <summary>安全事件日志使能</summary>
        public bool EnableSafetyEventLog
        {
            get => _enableSafetyEventLog;
            set => SetProperty(ref _enableSafetyEventLog, value);
        }

        private bool _isDirty;
        /// <summary>配置是否有未保存的更改</summary>
        public bool IsDirty
        {
            get => _isDirty;
            set => SetProperty(ref _isDirty, value);
        }

        // TODO: 以下三个属性尚未添加到 AppSettings 类中，后续需迁移至 AppSettings 作为正式属性
        private string _dataSavePath;
        /// <summary>数据保存路径</summary>
        public string DataSavePath
        {
            get => _dataSavePath;
            set => SetProperty(ref _dataSavePath, value);
        }

        private int _dataRetentionDays = 30;
        /// <summary>数据保留天数</summary>
        public int DataRetentionDays
        {
            get => _dataRetentionDays;
            set => SetProperty(ref _dataRetentionDays, value);
        }

        private bool _autoCleanOldData = true;
        /// <summary>自动清理旧数据</summary>
        public bool AutoCleanOldData
        {
            get => _autoCleanOldData;
            set => SetProperty(ref _autoCleanOldData, value);
        }

        #endregion

        #region SEC/GEM 相关属性

        private bool _enableSecsGem;
        /// <summary>SEC/GEM使能</summary>
        public bool EnableSecsGem
        {
            get => _enableSecsGem;
            set
            {
                if (SetProperty(ref _enableSecsGem, value))
                {
                    // 实时更新设备状态
                }
            }
        }

        private bool _secsGemAdvancedExpanded;
        /// <summary>SEC/GEM高级设置展开状态</summary>
        public bool SecsGemAdvancedExpanded
        {
            get => _secsGemAdvancedExpanded;
            set => SetProperty(ref _secsGemAdvancedExpanded, value);
        }

        private string _secsGemIP = "127.0.0.1";
        /// <summary>SEC/GEM IP地址</summary>
        public string SecsGemIP
        {
            get => _secsGemIP;
            set => SetProperty(ref _secsGemIP, value);
        }

        private string _secsGemPort = "5000";
        /// <summary>SEC/GEM端口</summary>
        public string SecsGemPort
        {
            get => _secsGemPort;
            set => SetProperty(ref _secsGemPort, value);
        }

        private string _secsGemDeviceId = "0";
        /// <summary>SEC/GEM设备ID</summary>
        public string SecsGemDeviceId
        {
            get => _secsGemDeviceId;
            set => SetProperty(ref _secsGemDeviceId, value);
        }

        #endregion

        #region IsDirty 追踪

        protected override void OnPropertyChanged(PropertyChangedEventArgs args)
        {
            base.OnPropertyChanged(args);
            if (args.PropertyName != nameof(IsDirty))
            {
                IsDirty = true;
            }
        }

        #endregion

        #region 命令
        /// <summary>保存配置命令</summary>
        public DelegateCommand SaveCommand { get; }

        private DelegateCommand _browsePathCommand;
        /// <summary>浏览路径命令</summary>
        public DelegateCommand BrowsePathCommand =>
            _browsePathCommand ??= new DelegateCommand(ExecuteBrowsePath);

        private DelegateCommand _loadDefaultCommand;
        /// <summary>加载默认配置命令</summary>
        public DelegateCommand LoadDefaultCommand =>
            _loadDefaultCommand ??= new DelegateCommand(ExecuteLoadDefault);

        #endregion

        #region 构造函数与依赖

        private readonly IEventAggregator _eventAggregator;
        private readonly ILoggerService _logger;
        private readonly IAppSettingService _appSettingService;

        /// <summary>
        /// 构造函数：注入事件聚合器、日志服务和应用配置服务
        /// </summary>
        public DeviceConfigViewModel(
            IEventAggregator eventAggregator,
            ILoggerService logger,
            IAppSettingService appSettingService)
        {
            _eventAggregator = eventAggregator;
            _logger = logger;
            _appSettingService = appSettingService;

            LoadDeviceConfig();
            SaveCommand = new DelegateCommand(ExecuteSave);
        }

        #endregion

        #region 配置加载与保存

        /// <summary>
        /// 从 IAppSettingService.Settings 加载所有配置属性
        /// </summary>
        private void LoadDeviceConfig()
        {
            var settings = _appSettingService.Settings;

            // 直接属性：AppSettings 中已定义的属性
            EnableSafetyGate = settings.EnableSafetyGate;
            EnableBuzzer = settings.EnableBuzzer;
            EnableGrating = settings.EnableGrating;
            EnableSafetyEventLog = settings.EnableSafetyEventLog;

            // 扩展属性：通过 ExtensionData 读取 AppSettings 中未显式定义的属性
            EnableSnCode = GetExtensionBool(nameof(EnableSnCode), true);
            EnableSecsGem = GetExtensionBool(nameof(EnableSecsGem), false);
            SecsGemIP = GetExtensionString(nameof(SecsGemIP), "127.0.0.1");
            SecsGemPort = GetExtensionString(nameof(SecsGemPort), "5000");
            SecsGemDeviceId = GetExtensionString(nameof(SecsGemDeviceId), "0");

            // TODO: DataSavePath、DataRetentionDays、AutoCleanOldData 尚未添加到 AppSettings 类，
            // 后续需将这些属性迁移至 AppSettings 作为正式属性
            DataSavePath = GetExtensionString(nameof(DataSavePath), GetDefaultDataPath());
            DataRetentionDays = GetExtensionInt(nameof(DataRetentionDays), 30);
            AutoCleanOldData = GetExtensionBool(nameof(AutoCleanOldData), true);
        }

        /// <summary>
        /// 将所有配置属性写入 IAppSettingService.Settings 并持久化，然后发布配置变更事件
        /// </summary>
        private void ExecuteSave()
        {
            try
            {
                var settings = _appSettingService.Settings;

                // 写入直接属性
                settings.EnableSafetyGate = EnableSafetyGate;
                settings.EnableBuzzer = EnableBuzzer;
                settings.EnableGrating = EnableGrating;
                settings.EnableSafetyEventLog = EnableSafetyEventLog;

                // 写入扩展属性至 ExtensionData
                SetExtensionValue(nameof(EnableSnCode), EnableSnCode);
                SetExtensionValue(nameof(EnableSecsGem), EnableSecsGem);
                SetExtensionValue(nameof(SecsGemIP), SecsGemIP);
                SetExtensionValue(nameof(SecsGemPort), SecsGemPort);
                SetExtensionValue(nameof(SecsGemDeviceId), SecsGemDeviceId);

                // TODO: DataSavePath、DataRetentionDays、AutoCleanOldData 尚未添加到 AppSettings 类，
                // 后续需将这些属性迁移至 AppSettings 作为正式属性
                SetExtensionValue(nameof(DataSavePath), DataSavePath);
                SetExtensionValue(nameof(DataRetentionDays), DataRetentionDays);
                SetExtensionValue(nameof(AutoCleanOldData), AutoCleanOldData);

                _appSettingService.Save();

                // 发布配置变更事件，载荷为当前 AppSettings 实例
                _eventAggregator.GetEvent<DeviceConfigChangedEvent>()
                    .Publish(_appSettingService.Settings);

                IsDirty = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"配置保存失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 加载默认配置值
        /// </summary>
        private void ExecuteLoadDefault()
        {
            EnableSafetyGate = true;
            EnableBuzzer = false;
            EnableSnCode = true;
            EnableGrating = true;
            EnableSafetyEventLog = true;
            EnableSecsGem = false;
            SecsGemIP = "127.0.0.1";
            SecsGemPort = "5000";
            SecsGemDeviceId = "0";
            DataSavePath = GetDefaultDataPath();
        }

        #endregion

        #region 路径浏览

        /// <summary>
        /// 打开文件夹选择对话框，选择数据保存路径
        /// </summary>
        private void ExecuteBrowsePath()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                InitialDirectory = DataSavePath,
                Title = "Select config file save directory"
            };

            if (dialog.ShowDialog() == true)
            {
                DataSavePath = dialog.FolderName;
            }
        }

        #endregion

        #region ExtensionData 辅助方法

        /// <summary>
        /// 从 AppSettings.ExtensionData 读取布尔值
        /// </summary>
        private bool GetExtensionBool(string key, bool defaultValue = false)
        {
            if (_appSettingService.Settings.ExtensionData.TryGetValue(key, out var element)
                && (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
            {
                return element.GetBoolean();
            }
            return defaultValue;
        }

        /// <summary>
        /// 从 AppSettings.ExtensionData 读取字符串值
        /// </summary>
        private string GetExtensionString(string key, string defaultValue = "")
        {
            if (_appSettingService.Settings.ExtensionData.TryGetValue(key, out var element)
                && element.ValueKind == JsonValueKind.String)
            {
                return element.GetString() ?? defaultValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// 从 AppSettings.ExtensionData 读取整数值
        /// </summary>
        private int GetExtensionInt(string key, int defaultValue = 0)
        {
            if (_appSettingService.Settings.ExtensionData.TryGetValue(key, out var element)
                && element.ValueKind == JsonValueKind.Number)
            {
                return element.GetInt32();
            }
            return defaultValue;
        }

        /// <summary>
        /// 将值写入 AppSettings.ExtensionData，支持 JSON 序列化持久化
        /// </summary>
        private void SetExtensionValue<T>(string key, T value)
        {
            var json = JsonSerializer.Serialize(value);
            using var doc = JsonDocument.Parse(json);
            _appSettingService.Settings.ExtensionData[key] = doc.RootElement.Clone();
        }

        #endregion

        #region 其他辅助方法

        /// <summary>
        /// 配置已保存后，若 SN 码使能状态变更则立即发布配置变更事件
        /// </summary>
        private void PublishConfigChangeIfSaved()
        {
            if (!IsDirty)
            {
                _eventAggregator.GetEvent<DeviceConfigChangedEvent>()
                    .Publish(_appSettingService.Settings);
            }
        }

        /// <summary>
        /// 获取默认数据保存路径
        /// </summary>
        private static string GetDefaultDataPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeviceData");
        }

        #endregion
    }
}

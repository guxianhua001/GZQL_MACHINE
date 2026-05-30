using Core.Abstraction;
using Core.Utilities;
using MotionControl.Interfaces;
using StationTasks.Models;
using StationTasks.Services;
using Prism.Commands;
using Prism.Events;
using Prism.Ioc;
using Prism.Mvvm;
using Recipe.Events;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using TCPIPModule.Interfaces;
using System.Windows;

namespace Module.ViewModels
{
    /// <summary>
    /// SCAN 步骤详细配置 ViewModel，以模态弹窗形式展示
    /// 支持运动配置、IO配置、通讯配置、数据解析脚本、变量映射、执行测试等编辑
    /// </summary>
    public class ScanDetailViewModel : BindableBase
    {
        private readonly IContainerProvider _containerProvider;
        private readonly IRecipePoolService _recipePoolService;
        private readonly IPositionProvider _positionProvider;
        private readonly ITCPClientManagerService _tcpClientManagerService;
        private readonly ITCPEventService _tcpEventService;
        private readonly ILoggerService _logger;
        private readonly IVisionDataParser _defaultParser;
        private readonly ScriptVisionDataParser _scriptParser;
        private readonly IStationRegistry _stationRegistry;
        private readonly IEventAggregator _eventAggregator;

        private ProcessStep _step;
        /// <summary> TCP数据接收事件处理器引用，用于取消订阅防止内存泄漏 </summary>
        private Action<string, string>? _cameraDataHandler;
        private string _lastReceivedTime;
        private string _lastReceivedData;

        /// <summary>
        /// 当前编辑的工艺步骤，设置时自动初始化所有配置项
        /// </summary>
        public ProcessStep Step
        {
            get => _step;
            set
            {
                if (SetProperty(ref _step, value))
                    InitializeFromStep();
            }
        }

        /// <summary>
        /// 步骤描述信息，显示 ComponentFeature → SiteFeature
        /// </summary>
        public string StepDescription => _step == null ? "—" : $"{_step.CompFeature ?? "—"} → {_step.SiteFeature ?? "—"}";

        #region 运动配置

        private int _zAxisId;
        /// <summary> Z轴编号 </summary>
        public int ZAxisId { get => _zAxisId; set => SetProperty(ref _zAxisId, value); }

        private int _xAxisId;
        /// <summary> X轴编号 </summary>
        public int XAxisId { get => _xAxisId; set => SetProperty(ref _xAxisId, value); }

        private string _zInitPosition = "Z_Init";
        /// <summary> Z轴初始位置名称 </summary>
        public string ZInitPosition { get => _zInitPosition; set => SetProperty(ref _zInitPosition, value); }

        private string _xStartPosition = "X_Start";
        /// <summary> X轴起始位置名称 </summary>
        public string XStartPosition { get => _xStartPosition; set => SetProperty(ref _xStartPosition, value); }

        private string _zPhotoPosition = "Z_Photo";
        /// <summary> Z轴拍照高度位置名称 </summary>
        public string ZPhotoPosition { get => _zPhotoPosition; set => SetProperty(ref _zPhotoPosition, value); }

        private string _xEndPosition = "X_End";
        /// <summary> X轴结束位置名称 </summary>
        public string XEndPosition { get => _xEndPosition; set => SetProperty(ref _xEndPosition, value); }

        private string _zSafePosition = "Z_Safe";
        /// <summary> Z轴安全高度位置名称 </summary>
        public string ZSafePosition { get => _zSafePosition; set => SetProperty(ref _zSafePosition, value); }

        private string _xStandbyPosition = "X_Standby";
        /// <summary> X轴待机位置名称 </summary>
        public string XStandbyPosition { get => _xStandbyPosition; set => SetProperty(ref _xStandbyPosition, value); }

        private double _moveSpeed = 10.0;
        /// <summary> 运动速度 </summary>
        public double MoveSpeed { get => _moveSpeed; set => SetProperty(ref _moveSpeed, value); }

        /// <summary> Z轴位置名称列表，从 IPositionProvider 加载 </summary>
        public ObservableCollection<string> ZPositions { get; } = new ObservableCollection<string>();

        /// <summary> X轴位置名称列表，从 IPositionProvider 加载 </summary>
        public ObservableCollection<string> XPositions { get; } = new ObservableCollection<string>();

        #endregion

        #region IO配置

        private int _triggerIoPort;
        /// <summary> 触发IO端口号 </summary>
        public int TriggerIoPort { get => _triggerIoPort; set => SetProperty(ref _triggerIoPort, value); }

        private int _ioResetDelayMs = 200;
        /// <summary> IO自动复位延时（毫秒） </summary>
        public int IoResetDelayMs { get => _ioResetDelayMs; set => SetProperty(ref _ioResetDelayMs, value); }

        #endregion

        #region 通讯配置

        /// <summary> 通讯方式选项列表 </summary>
        public ObservableCollection<string> CommunicationTypes { get; } = new ObservableCollection<string> { "TCPIP", "Serial" };

        private string _selectedCommunicationType = "TCPIP";
        /// <summary> 选中的通讯方式 </summary>
        public string SelectedCommunicationType
        {
            get => _selectedCommunicationType;
            set
            {
                if (SetProperty(ref _selectedCommunicationType, value))
                    RaisePropertyChanged(nameof(IsTcpSelected));
            }
        }

        /// <summary> 当前是否选择了 TCPIP 通讯方式 </summary>
        public bool IsTcpSelected => SelectedCommunicationType == "TCPIP";

        /// <summary> TCP 连接名称列表 </summary>
        public ObservableCollection<string> TcpConnections { get; } = new ObservableCollection<string>();

        private string _selectedConnectionName;
        /// <summary> 选中的 TCP 连接名称 </summary>
        public string SelectedConnectionName
        {
            get => _selectedConnectionName;
            set => SetProperty(ref _selectedConnectionName, value);
        }

        private int _responseTimeout = 5000;
        /// <summary> 响应超时时间（毫秒） </summary>
        public int ResponseTimeout
        {
            get => _responseTimeout;
            set => SetProperty(ref _responseTimeout, value);
        }

        #endregion

        #region 数据解析配置

        private string _parseScript;
        /// <summary> C# 数据解析脚本代码 </summary>
        public string ParseScript
        {
            get => _parseScript;
            set => SetProperty(ref _parseScript, value);
        }

        private int _tabCount = 6;
        /// <summary> Tab数量 </summary>
        public int TabCount
        {
            get => _tabCount;
            set => SetProperty(ref _tabCount, value);
        }

        /// <summary> 变量映射集合 </summary>
        public ObservableCollection<VariableMapping> VariableMappings { get; } = new ObservableCollection<VariableMapping>();

        private VariableMapping _selectedMapping;
        /// <summary> 当前选中的变量映射行 </summary>
        public VariableMapping SelectedMapping
        {
            get => _selectedMapping;
            set => SetProperty(ref _selectedMapping, value);
        }

        /// <summary> 全局变量名称列表，用于变量映射下拉选择 </summary>
        public ObservableCollection<string> GlobalVariableNames { get; } = new ObservableCollection<string>();

        #endregion

        #region 数据解析面板

        /// <summary> Tab高度检测结果表格 </summary>
        public ObservableCollection<ScanResultItem> ScanResults { get; } = new ObservableCollection<ScanResultItem>();

        #endregion

        #region 执行测试相关属性

        private string _sampleData;
        /// <summary> 示例/测试数据，用于执行测试 </summary>
        public string SampleData
        {
            get => _sampleData;
            set => SetProperty(ref _sampleData, value);
        }

        private string _executeResult = string.Empty;
        /// <summary> 执行测试结果 </summary>
        public string ExecuteResult
        {
            get => _executeResult;
            set => SetProperty(ref _executeResult, value);
        }

        private bool _isExecuting;
        /// <summary> 是否正在执行测试 </summary>
        public bool IsExecuting
        {
            get => _isExecuting;
            set => SetProperty(ref _isExecuting, value);
        }

        #endregion

        /// <summary> 插入默认3D相机解析脚本模板命令 </summary>
        public ICommand InsertDefaultScriptCommand { get; }
        /// <summary> 填充示例数据命令 </summary>
        public ICommand FillSampleDataCommand { get; }
        /// <summary> 添加变量映射命令 </summary>
        public ICommand AddMappingCommand { get; }
        /// <summary> 删除变量映射命令 </summary>
        public ICommand DeleteMappingCommand { get; }
        /// <summary> 执行测试命令：发送触发命令→解析→映射变量 </summary>
        public ICommand ExecuteTestCommand { get; }
        /// <summary> 使用示例数据执行测试命令 </summary>
        public ICommand ExecuteWithSampleDataCommand { get; }
        /// <summary> 关闭弹窗命令 </summary>
        public ICommand CloseCommand { get; }
        /// <summary> 保存并关闭弹窗命令 </summary>
        public ICommand SaveCommand { get; }

        public ScanDetailViewModel(
            IContainerProvider containerProvider,
            IRecipePoolService recipePoolService,
            IPositionProvider positionProvider,
            ITCPClientManagerService tcpClientManagerService,
            ITCPEventService tcpEventService,
            ILoggerService logger,
            IVisionDataParser defaultParser,
            ScriptVisionDataParser scriptParser,
            IStationRegistry stationRegistry,
            IEventAggregator eventAggregator)
        {
            _containerProvider = containerProvider;
            _recipePoolService = recipePoolService;
            _positionProvider = positionProvider;
            _tcpClientManagerService = tcpClientManagerService;
            _tcpEventService = tcpEventService;
            _logger = logger;
            _defaultParser = defaultParser;
            _scriptParser = scriptParser;
            _stationRegistry = stationRegistry;
            _eventAggregator = eventAggregator;

            InsertDefaultScriptCommand = new DelegateCommand(OnInsertDefaultScript);
            FillSampleDataCommand = new DelegateCommand(OnFillSampleData);
            AddMappingCommand = new DelegateCommand(OnAddMapping);
            DeleteMappingCommand = new DelegateCommand(OnDeleteMapping, () => SelectedMapping != null)
                .ObservesProperty(() => SelectedMapping);
            ExecuteTestCommand = new DelegateCommand(async () => await OnExecuteTestAsync(),
                    () => !IsExecuting && IsTcpSelected && !string.IsNullOrEmpty(SelectedConnectionName))
                .ObservesProperty(() => IsExecuting)
                .ObservesProperty(() => IsTcpSelected)
                .ObservesProperty(() => SelectedConnectionName);
            ExecuteWithSampleDataCommand = new DelegateCommand(async () => await OnExecuteWithSampleDataAsync(),
                    () => !IsExecuting && !string.IsNullOrWhiteSpace(SampleData))
                .ObservesProperty(() => IsExecuting)
                .ObservesProperty(() => SampleData);
            CloseCommand = new DelegateCommand(OnClose);
            SaveCommand = new DelegateCommand(OnSave);

            LoadTcpConnections();
            LoadGlobalVariableNamesAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 从 Step.ScanDetail 加载所有配置项到 ViewModel 属性
        /// </summary>
        private void InitializeFromStep()
        {
            if (_step == null) return;

            if (_step.ScanDetail == null)
                _step.ScanDetail = new ScanDetail();

            var detail = _step.ScanDetail;

            ZAxisId = detail.ZAxisId;
            XAxisId = detail.XAxisId;
            ZInitPosition = detail.ZInitPosition ?? "Z_Init";
            XStartPosition = detail.XStartPosition ?? "X_Start";
            ZPhotoPosition = detail.ZPhotoPosition ?? "Z_Photo";
            XEndPosition = detail.XEndPosition ?? "X_End";
            ZSafePosition = detail.ZSafePosition ?? "Z_Safe";
            XStandbyPosition = detail.XStandbyPosition ?? "X_Standby";
            MoveSpeed = detail.MoveSpeed;

            TriggerIoPort = detail.TriggerIoPort;
            IoResetDelayMs = detail.IoResetDelayMs;

            SelectedCommunicationType = detail.CommunicationType ?? "TCPIP";
            SelectedConnectionName = detail.ConnectionName ?? "";
            ResponseTimeout = detail.ResponseTimeout;

            ParseScript = detail.ParseScript ?? "";
            TabCount = detail.TabCount;

            VariableMappings.Clear();
            if (detail.VariableMappings != null)
            {
                foreach (var mapping in detail.VariableMappings)
                {
                    VariableMappings.Add(new VariableMapping
                    {
                        SourceKey = mapping.SourceKey,
                        GlobalVariableName = mapping.GlobalVariableName,
                        CompensatedGlobalVariableName = mapping.CompensatedGlobalVariableName
                    });
                }
            }

            ScanResults.Clear();

            // 加载上次保存的扫描结果（持久化数据），方便查看最后一次的值
            if (detail.LastScanResults != null && detail.LastScanResults.Count > 0)
            {
                foreach (var item in detail.LastScanResults)
                {
                    ScanResults.Add(new ScanResultItem
                    {
                        Index = item.Index,
                        Name = item.Name,
                        BaseZValue = item.BaseZValue,
                        UpperLimit = item.UpperLimit,
                        LowerLimit = item.LowerLimit,
                        MeasuredValue = item.MeasuredValue,
                        Deviation = item.Deviation,
                        FixedCompensation = item.FixedCompensation,
                        TargetGlobalVariable = item.TargetGlobalVariable,
                        Status = item.Status
                    });
                }
                // 恢复上次的数据和时间戳
                SampleData = detail.LastSampleData ?? "";
                LastReceivedTime = detail.LastReceivedTime ?? "";
                LastReceivedData = detail.LastReceivedData ?? "";

                _logger?.Info($"已加载上次保存的扫描结果: {ScanResults.Count}条记录, 最后解析时间={LastReceivedTime}");
            }

            RaisePropertyChanged(nameof(StepDescription));
            RaisePropertyChanged(nameof(IsTcpSelected));

            var stationId = _step.SubMoves?.FirstOrDefault()?.StationId;
            if (!string.IsNullOrEmpty(stationId))
                LoadPositionsAsync(stationId).ConfigureAwait(false);

            // 订阅TCP被动数据接收事件：3D相机通过IO触发后回传数据，自动解析并刷新数据面板
            SubscribeCameraData();
        }

        /// <summary>
        /// 从 IPositionProvider 加载指定工站的位置名称列表，分别填充 Z 轴和 X 轴位置下拉
        /// </summary>
        private async System.Threading.Tasks.Task LoadPositionsAsync(string stationId)
        {
            ZPositions.Clear();
            XPositions.Clear();
            try
            {
                var positions = await _positionProvider.GetPositionsAsync(stationId);
                var positionNames = positions.Keys
                    .Select(k => k.Contains('.') ? k.Substring(0, k.IndexOf('.')) : k)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                foreach (var name in positionNames)
                {
                    ZPositions.Add(name);
                    XPositions.Add(name);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"加载位置列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 IAppSettingService 加载所有已配置的 TCP 连接名称
        /// 包含 Client 和 Server 两种模式的所有配置项
        /// </summary>
        private void LoadTcpConnections()
        {
            TcpConnections.Clear();
            try
            {
                // 优先从 AppSettingService 获取所有配置项（含 Server 模式）
                var appConfig = _containerProvider.Resolve<Core.Abstraction.IAppSettingService>();
                if (appConfig?.Clients != null)
                {
                    foreach (var client in appConfig.Clients)
                        TcpConnections.Add(client.ClientName);
                }

                // 如果 AppSettingService 无数据，回退到 ClientManagerService（仅 Client 模式）
                if (TcpConnections.Count == 0 && _tcpClientManagerService?.Clients != null)
                {
                    foreach (var name in _tcpClientManagerService.Clients.Keys)
                        TcpConnections.Add(name);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"加载TCP连接列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 IRecipePoolService 加载全局变量名称列表
        /// </summary>
        private async System.Threading.Tasks.Task LoadGlobalVariableNamesAsync()
        {
            GlobalVariableNames.Clear();
            try
            {
                var poolId = _recipePoolService.CurrentPoolId;
                if (string.IsNullOrEmpty(poolId)) return;

                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
                foreach (var v in variables)
                    GlobalVariableNames.Add(v.Name);
            }
            catch (Exception ex)
            {
                _logger?.Error($"加载全局变量列表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 插入默认的3D相机数据解析脚本模板
        /// </summary>
        private void OnInsertDefaultScript()
        {
            ParseScript = GetDefaultParseScript();
        }

        /// <summary>
        /// 填充示例3D相机数据，同时填充变量映射和扫描结果表格
        /// 扫描结果行数与变量映射一一对应
        /// </summary>
        private void OnFillSampleData()
        {
            SampleData = "Camera=3DCAMERA;VISION_RESULT:SUCCESS:14.164,10.713,9.399,11.682,13.871,11.75,0,0,0,0,0,0";

            // 变量映射：6个Tab高度键名，含原始值和补偿后值两个全局变量目标
            VariableMappings.Clear();
            for (int i = 1; i <= 6; i++)
            {
                VariableMappings.Add(new VariableMapping
                {
                    SourceKey = $"Tab{i}Height",
                    GlobalVariableName = $"Scan_Tab{i}_Raw",
                    CompensatedGlobalVariableName = $"Scan_Tab{i}_Compensated"
                });
            }

            // 扫描结果：根据映射动态生成，含基准Z值和固定补偿值
            ScanResults.Clear();
            var sampleValues = new[] { 14.164, 10.713, 9.399, 11.682, 13.871, 11.75 };
            var baseValues = new[] { 11.5, 11.5, 11.5, 11.5, 11.5, 11.5 };
            var compensationValues = new[] { 0.0, 0.1, -0.05, 0.0, 0.15, -0.1 };
            for (int i = 0; i < 6; i++)
            {
                var measured = sampleValues[i];
                var baseVal = baseValues[i];
                var deviation = measured - baseVal;
                bool inRange = measured >= 8.0 && measured <= 15.0;

                ScanResults.Add(new ScanResultItem
                {
                    Index = i + 1,
                    Name = $"Tab{i + 1}Height",
                    BaseZValue = baseVal,
                    UpperLimit = 15.0,
                    LowerLimit = 8.0,
                    MeasuredValue = measured,
                    Deviation = deviation,
                    FixedCompensation = compensationValues[i],
                    TargetGlobalVariable = $"Scan_Tab{i + 1}_Compensated",
                    Status = inRange ? "Pass" : "Fail"
                });
            }

            _logger?.Info("已填充3D相机示例数据、变量映射和扫描结果");
        }

        /// <summary>
        /// 添加一条新的变量映射行
        /// </summary>
        private void OnAddMapping()
        {
            VariableMappings.Add(new VariableMapping
            {
                SourceKey = "",
                GlobalVariableName = "",
                CompensatedGlobalVariableName = ""
            });
        }

        /// <summary>
        /// 删除当前选中的变量映射行
        /// </summary>
        private void OnDeleteMapping()
        {
            if (SelectedMapping != null)
                VariableMappings.Remove(SelectedMapping);
        }

        /// <summary>
        /// 执行测试：通过TCP发送触发命令→接收响应→解析→映射全局变量
        /// </summary>
        private async System.Threading.Tasks.Task OnExecuteTestAsync()
        {
            IsExecuting = true;
            ExecuteResult = "正在发送触发命令...";

            try
            {
                if (string.IsNullOrEmpty(SelectedConnectionName))
                {
                    ExecuteResult = "错误：TCP连接未配置";
                    return;
                }

                string triggerCmd = $"IO:{TriggerIoPort}";
                string response = await _tcpEventService.SendCommandWithResponseAsync(
                    SelectedConnectionName,
                    triggerCmd,
                    ResponseTimeout);

                if (string.IsNullOrEmpty(response))
                {
                    ExecuteResult = "警告：收到空响应，无法解析";
                    return;
                }

                ExecuteResult = $"收到响应: {response}\n";

                var parsedData = ParseDataInternal(response);
                if (parsedData.Count == 0)
                {
                    ExecuteResult += "解析结果为空";
                    return;
                }

                ExecuteResult += $"解析结果: {string.Join(", ", parsedData.Select(kv => $"{kv.Key}={kv.Value:F3}"))}\n";

                UpdateScanResults(parsedData);

                var mappingResult = await ApplyVariableMappingsAsync(parsedData);
                ExecuteResult += mappingResult;
            }
            catch (TimeoutException)
            {
                ExecuteResult = $"超时（{ResponseTimeout}ms）：未收到3D相机响应，请检查连接";
            }
            catch (Exception ex)
            {
                ExecuteResult = $"执行失败: {ex.Message}";
            }
            finally
            {
                IsExecuting = false;
            }
        }

        /// <summary>
        /// 使用示例数据执行测试：跳过TCP发送，直接用SampleData解析→映射全局变量
        /// </summary>
        private async System.Threading.Tasks.Task OnExecuteWithSampleDataAsync()
        {
            IsExecuting = true;
            ExecuteResult = "正在使用示例数据执行测试...";

            try
            {
                var parsedData = ParseDataInternal(SampleData);
                if (parsedData.Count == 0)
                {
                    ExecuteResult = "解析结果为空，请检查示例数据格式和解析脚本";
                    return;
                }

                ExecuteResult = $"示例数据: {SampleData}\n";
                ExecuteResult += $"解析结果: {string.Join(", ", parsedData.Select(kv => $"{kv.Key}={kv.Value:F3}"))}\n";

                UpdateScanResults(parsedData);

                var mappingResult = await ApplyVariableMappingsAsync(parsedData);
                ExecuteResult += mappingResult;
            }
            catch (Exception ex)
            {
                ExecuteResult = $"执行失败: {ex.Message}";
            }
            finally
            {
                IsExecuting = false;
            }
        }

        /// <summary>
        /// 内部解析方法：根据是否有自定义脚本选择对应解析器，默认使用 Camera3DDataParser
        /// </summary>
        private Dictionary<string, double> ParseDataInternal(string rawData)
        {
            if (string.IsNullOrEmpty(rawData))
                return new Dictionary<string, double>();

            if (string.IsNullOrEmpty(ParseScript))
            {
                if (_defaultParser is Camera3DDataParser camera3DParser)
                    camera3DParser.TabCount = TabCount;
                return _defaultParser.Parse(rawData);
            }

            _scriptParser.Script = ParseScript;
            return _scriptParser.Parse(rawData);
        }

        /// <summary>
        /// 根据解析结果和变量映射动态更新扫描结果表格，每行对应一个映射的解析键名
        /// 计算偏差（实测值-基准Z值）和判定状态（是否超限）
        /// </summary>
        private void UpdateScanResults(Dictionary<string, double> parsedData)
        {
            ScanResults.Clear();

            // 无变量映射时生成空表格提示
            if (VariableMappings == null || VariableMappings.Count == 0)
                return;

            for (int i = 0; i < VariableMappings.Count; i++)
            {
                var mapping = VariableMappings[i];
                var key = mapping.SourceKey;
                var item = new ScanResultItem
                {
                    Index = i + 1,
                    Name = string.IsNullOrEmpty(key) ? $"Point{i + 1}" : key,
                    BaseZValue = 11.5,
                    UpperLimit = 15.0,
                    LowerLimit = 8.0,
                    FixedCompensation = 0,
                    TargetGlobalVariable = mapping.CompensatedGlobalVariableName ?? ""
                };

                if (!string.IsNullOrEmpty(key) && parsedData.TryGetValue(key, out double value))
                {
                    item.MeasuredValue = value;
                    item.Deviation = value - item.BaseZValue;
                    item.Status = (value >= item.LowerLimit && value <= item.UpperLimit) ? "Pass" : "Fail";
                }
                else
                {
                    item.MeasuredValue = 0;
                    item.Deviation = 0;
                    item.Status = "---";
                }

                ScanResults.Add(item);
            }
        }

        /// <summary>
        /// 应用变量映射：将解析结果（原始值和补偿后值）写入全局变量并持久化
        /// </summary>
        private async System.Threading.Tasks.Task<string> ApplyVariableMappingsAsync(Dictionary<string, double> parsedData)
        {
            if (VariableMappings == null || VariableMappings.Count == 0)
                return "未配置变量映射，跳过全局变量写入";

            // 使用CurrentPoolId（配方池ID）而非CurrentPoolName（名称），确保正确加载/保存全局变量
            var poolId = _recipePoolService.CurrentPoolId;
            if (string.IsNullOrEmpty(poolId))
                return "当前无配方池，跳过变量映射";

            _logger?.Info($"SCAN 开始同步全局变量: PoolId={poolId}, 解析数据项数={parsedData.Count}");

            var globalVars = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
            var results = new List<string>();
            bool changed = false;

            foreach (var mapping in VariableMappings)
            {
                // 1. 原始实测值 → 全局变量
                if (!string.IsNullOrEmpty(mapping.SourceKey) && !string.IsNullOrEmpty(mapping.GlobalVariableName))
                {
                    if (parsedData.TryGetValue(mapping.SourceKey, out double value))
                    {
                        var targetVar = globalVars.FirstOrDefault(v => v.Name == mapping.GlobalVariableName);
                        if (targetVar != null)
                        {
                            targetVar.Value = value.ToString("F6");
                            results.Add($"✓ {mapping.SourceKey}={value:F3} → '{mapping.GlobalVariableName}'(原始值)");
                            changed = true;
                        }
                        else
                        {
                            results.Add($"⚠ 跳过 '{mapping.GlobalVariableName}': 全局变量不存在");
                        }
                    }
                    else
                    {
                        results.Add($"⚠ 跳过 '{mapping.SourceKey}': 解析结果中不存在此键");
                    }
                }

                // 2. 补偿后值(实测+固定补偿) → 全局变量
                if (!string.IsNullOrEmpty(mapping.SourceKey) && !string.IsNullOrEmpty(mapping.CompensatedGlobalVariableName))
                {
                    if (parsedData.TryGetValue(mapping.SourceKey, out double rawValue))
                    {
                        // 从ScanResults中获取该行的固定补偿值
                        var scanItem = ScanResults.FirstOrDefault(s => s.Name == mapping.SourceKey);
                        double compensation = scanItem?.FixedCompensation ?? 0;
                        double compensatedValue = rawValue + compensation;

                        var compTargetVar = globalVars.FirstOrDefault(v => v.Name == mapping.CompensatedGlobalVariableName);
                        if (compTargetVar != null)
                        {
                            compTargetVar.Value = compensatedValue.ToString("F6");
                            results.Add($"✓ {mapping.SourceKey}({rawValue:F3}+{compensation:F2})={compensatedValue:F3} → '{mapping.CompensatedGlobalVariableName}'(补偿后)");
                            changed = true;
                        }
                        else
                        {
                            results.Add($"⚠ 跳过 '{mapping.CompensatedGlobalVariableName}': 全局变量不存在");
                        }
                    }
                }
            }

            if (changed)
            {
                await _recipePoolService.SaveGlobalVariablesAsync(poolId, globalVars);
                results.Add("全局变量已保存");
                _logger?.Info($"SCAN 全局变量同步完成: PoolId={poolId}, 更新了{results.Count(r => r.StartsWith("✓"))}个变量");

                // 通知全局变量窗口重新加载最新数据
                _eventAggregator.GetEvent<GlobalVariablesChangedEvent>().Publish(poolId);
            }
            else
            {
                _logger?.Warn($"SCAN 全局变量无需更新: 无匹配的变量映射或解析数据");
            }

            return "变量映射:\n" + string.Join("\n", results);
        }

        /// <summary>
        /// 返回默认的3D相机数据解析脚本模板
        /// </summary>
        private string GetDefaultParseScript()
        {
            return @"using System;
            using System.Collections.Generic;

            /// <summary>
            /// 3D相机数据解析脚本
            /// 输入：data（string）— 3D相机原始数据
            /// 格式：Camera=3DCAMERA;VISION_RESULT:SUCCESS:14.164,10.713,9.399,11.682,13.871,11.75,0,0,...
            /// 输出：Dictionary<string, double> — Tab高度键值对
            /// </summary>
            public class VisionParseScript
            {
                public static Dictionary<string, double> Parse(string data)
                {
                    var result = new Dictionary<string, double>();
                    if (string.IsNullOrWhiteSpace(data)) return result;

                    var segments = data.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var seg in segments)
                    {
                        if (!seg.TrimStart().StartsWith(""VISION_RESULT:"", StringComparison.OrdinalIgnoreCase)) continue;
                        var parts = seg.Split(new[] { ':' }, 3);
                        if (parts.Length < 3 || parts[1].Trim().ToUpperInvariant() != ""SUCCESS"") return result;
                        var values = parts[2].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < values.Length; i++)
                        {
                            if (double.TryParse(values[i].Trim(), out double v))
                                result[$""Tab{i + 1}Height""] = v;
                        }
                        break;
                    }
                    return result;
                }
            }";
        }

        /// <summary>
        /// 订阅TCP数据接收事件：3D相机通过IO触发后被动回传数据
        /// 只接收匹配当前配置ConnectionName的数据，解析后自动刷新数据解析面板
        /// </summary>
        private void SubscribeCameraData()
        {
            // 先取消旧订阅（防止重复订阅）
            UnsubscribeCameraData();

            _cameraDataHandler = (cameraName, message) =>
            {
                // 只处理当前配置的TCP连接名称的数据
                if (!string.IsNullOrEmpty(SelectedConnectionName) && cameraName == SelectedConnectionName)
                {
                    Application.Current.Dispatcher.Invoke(async () =>
                    {
                        try
                        {
                            SampleData = message;
                            LastReceivedTime = DateTime.Now.ToString("HH:mm:ss.fff");
                            // 截取前60字符作为数据摘要显示
                            LastReceivedData = message.Length > 60 ? message.Substring(0, 60) + "..." : message;

                            var parsedData = ParseReceivedData(message);
                            UpdateScanResults(parsedData);

                            // 被动接收数据时自动同步全局变量（原始值+补偿后值）
                            await ApplyVariableMappingsAsync(parsedData);

                            _logger?.Info($"SCAN 被动接收到相机数据 [{SelectedConnectionName}]: {message}");
                        }
                        catch (Exception ex)
                        {
                            _logger?.Error($"SCAN 被动数据处理失败: {ex.Message}");
                        }
                    });
                }
            };

            _tcpEventService.CameraMessageReceived += _cameraDataHandler;
            _logger?.Info($"SCAN 已订阅TCP数据接收: 监听连接 '{SelectedConnectionName}'");
        }

        /// <summary>
        /// 取消订阅TCP数据接收事件，防止内存泄漏
        /// </summary>
        private void UnsubscribeCameraData()
        {
            if (_cameraDataHandler != null)
            {
                _tcpEventService.CameraMessageReceived -= _cameraDataHandler;
                _cameraDataHandler = null;
                _logger?.Info("SCAN 已取消订阅TCP数据接收");
            }
        }

        /// <summary>
        /// 解析收到的原始数据：有自定义脚本时使用脚本解析器，否则使用默认3D数据解析器
        /// </summary>
        private Dictionary<string, double> ParseReceivedData(string rawData)
        {
            if (string.IsNullOrEmpty(rawData))
                return new Dictionary<string, double>();

            try
            {
                if (!string.IsNullOrWhiteSpace(ParseScript))
                {
                    _scriptParser.Script = ParseScript;
                    return _scriptParser.Parse(rawData);
                }

                // 默认解析：按变量映射数量动态解析数值
                var parser = new Camera3DDataParser(_logger, VariableMappings.Count);
                return parser.Parse(rawData);
            }
            catch (Exception ex)
            {
                _logger?.Error($"SCAN 数据解析失败: {ex.Message}");
                return new Dictionary<string, double>();
            }
        }

        /// <summary> 最后一次收到数据的时间 (HH:mm:ss.fff)，支持WPF绑定自动刷新 </summary>
        public string LastReceivedTime { get => _lastReceivedTime; set => SetProperty(ref _lastReceivedTime, value); }
        /// <summary> 最后一次收到的原始数据摘要（截取前50字符），用于面板Header显示 </summary>
        public string LastReceivedData { get => _lastReceivedData; set => SetProperty(ref _lastReceivedData, value); }

        /// <summary>
        /// 关闭弹窗，不保存修改，同时取消TCP数据订阅
        /// </summary>
        private void OnClose()
        {
            UnsubscribeCameraData();
            try
            {
                var session = MaterialDesignThemes.Wpf.DialogHost.GetDialogSession("MainDialogHost");
                session?.Close(false);
            }
            catch (InvalidOperationException) { }
        }

        /// <summary>
        /// 保存所有配置项到 Step.ScanDetail 并关闭弹窗
        /// </summary>
        private void OnSave()
        {
            if (_step == null) return;

            if (_step.ScanDetail == null)
                _step.ScanDetail = new ScanDetail();

            var detail = _step.ScanDetail;

            detail.ZAxisId = ZAxisId;
            detail.XAxisId = XAxisId;
            detail.ZInitPosition = ZInitPosition;
            detail.XStartPosition = XStartPosition;
            detail.ZPhotoPosition = ZPhotoPosition;
            detail.XEndPosition = XEndPosition;
            detail.ZSafePosition = ZSafePosition;
            detail.XStandbyPosition = XStandbyPosition;
            detail.MoveSpeed = MoveSpeed;

            detail.TriggerIoPort = TriggerIoPort;
            detail.IoResetDelayMs = IoResetDelayMs;

            detail.CommunicationType = SelectedCommunicationType;
            detail.ConnectionName = SelectedConnectionName;
            detail.ResponseTimeout = ResponseTimeout;

            detail.ParseScript = ParseScript;
            detail.TabCount = TabCount;

            detail.VariableMappings = new ObservableCollection<VariableMapping>(
                VariableMappings.Select(m => new VariableMapping
                {
                    SourceKey = m.SourceKey,
                    GlobalVariableName = m.GlobalVariableName,
                    CompensatedGlobalVariableName = m.CompensatedGlobalVariableName
                }));

            // 持久化保存当前扫描结果和数据，方便下次打开时查看最后一次的值
            detail.LastSampleData = SampleData ?? "";
            detail.LastReceivedTime = LastReceivedTime ?? "";
            detail.LastReceivedData = LastReceivedData ?? "";
            detail.LastScanResults = new ObservableCollection<ScanResultItem>(
                ScanResults.Select(item => new ScanResultItem
                {
                    Index = item.Index,
                    Name = item.Name,
                    BaseZValue = item.BaseZValue,
                    UpperLimit = item.UpperLimit,
                    LowerLimit = item.LowerLimit,
                    MeasuredValue = item.MeasuredValue,
                    Deviation = item.Deviation,
                    FixedCompensation = item.FixedCompensation,
                    TargetGlobalVariable = item.TargetGlobalVariable,
                    Status = item.Status
                }));

            _logger?.Info($"SCAN 步骤配置已保存: {_step.Seq} - {StepDescription}（含{ScanResults.Count}条扫描结果）");

            OnClose();
        }
    }
}

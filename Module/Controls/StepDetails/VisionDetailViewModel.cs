using Core.Abstraction;
using Core.Utilities;
using StationTasks.Models;
using StationTasks.Services;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Events;
using Recipe.Interfaces;
using Recipe.Events;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using TCPIPModule.Interfaces;
using Core.Models;

namespace Module.ViewModels
{
    /// <summary>
    /// VISION 步骤详细配置 ViewModel，以模态弹窗形式展示
    /// 支持通讯配置、触发命令、数据解析脚本、变量映射等编辑
    /// 支持示例数据填充和单次执行测试
    /// </summary>
    public class VisionDetailViewModel : BindableBase, IDialogCloseable
    {
        private readonly IContainerProvider _containerProvider;
        private readonly IRecipePoolService _recipePoolService;
        private readonly ITCPClientManagerService _tcpClientManagerService;
        private readonly ITCPEventService _tcpEventService;
        private readonly ILoggerService _logger;
        private readonly IVisionDataParser _defaultParser;
        private readonly ScriptVisionDataParser _scriptParser;
        private readonly IEventAggregator _eventAggregator;

        private ProcessStep _step;

        /// <summary>请求关闭对话框时触发</summary>
        public event Action<object> RequestClose;

        /// <summary>是否可以关闭对话框</summary>
        public bool CanCloseDialog() => true;

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
        /// 步骤描述信息，显示 SeqN - ComponentFeature → SiteFeature
        /// </summary>
        public string StepDescription => _step == null ? "—" : $"Seq{_step.Seq} - {_step.CompFeature ?? "—"} → {_step.SiteFeature ?? "—"}";

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

        /// <summary> TCP 连接名称列表，从 ITCPClientManagerService.Clients.Keys 加载 </summary>
        public ObservableCollection<string> TcpConnections { get; } = new ObservableCollection<string>();

        private string _selectedConnectionName;
        /// <summary> 选中的 TCP 连接名称 </summary>
        public string SelectedConnectionName
        {
            get => _selectedConnectionName;
            set => SetProperty(ref _selectedConnectionName, value);
        }

        private string _triggerCommand = "TRIGGER";
        /// <summary> 触发拍照命令字符串 </summary>
        public string TriggerCommand
        {
            get => _triggerCommand;
            set => SetProperty(ref _triggerCommand, value);
        }

        private int _responseTimeout = 5000;
        /// <summary> 响应超时时间（毫秒） </summary>
        public int ResponseTimeout
        {
            get => _responseTimeout;
            set => SetProperty(ref _responseTimeout, value);
        }

        private string _parseScript;
        /// <summary> C# 数据解析脚本代码 </summary>
        public string ParseScript
        {
            get => _parseScript;
            set => SetProperty(ref _parseScript, value);
        }

        /// <summary> 变量映射行集合（支持GlobalVariableLinkControl实时显示值） </summary>
        public ObservableCollection<VisionVariableMappingRow> MappingRows { get; } = new ObservableCollection<VisionVariableMappingRow>();

        private VisionVariableMappingRow _selectedMapping;
        /// <summary> 当前选中的变量映射行 </summary>
        public VisionVariableMappingRow SelectedMapping
        {
            get => _selectedMapping;
            set => SetProperty(ref _selectedMapping, value);
        }

        /// <summary> 全局变量名称列表，用于变量映射下拉选择 </summary>
        public ObservableCollection<string> GlobalVariableNames { get; } = new ObservableCollection<string>();

        /// <summary> 可链接的全局变量列表（Double类型，供GlobalVariableLinkControl使用） </summary>
        private ObservableCollection<GlobalVariable> _linkableGlobalVariables = new ObservableCollection<GlobalVariable>();
        public ObservableCollection<GlobalVariable> LinkableGlobalVariables
        {
            get => _linkableGlobalVariables;
            set => SetProperty(ref _linkableGlobalVariables, value);
        }

        #region 执行测试相关属性

        private string _sampleData = "offsetX=0.5,offsetY=-0.3,angle=1.2,score=9.8";
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

        private bool? _isExecuteSuccess;
        /// <summary> 执行结果状态（null=未执行, true=成功, false=失败），用于UI三态颜色切换 </summary>
        public bool? IsExecuteSuccess
        {
            get => _isExecuteSuccess;
            set
            {
                if (SetProperty(ref _isExecuteSuccess, value))
                    RaisePropertyChanged(nameof(ExecuteResult));
            }
        }

        #endregion

        /// <summary> 添加变量映射命令 </summary>
        public ICommand AddMappingCommand { get; }
        /// <summary> 删除变量映射命令 </summary>
        public ICommand DeleteMappingCommand { get; }
        /// <summary> 取消变量链接命令（供GlobalVariableLinkControl使用） </summary>
        public ICommand UnlinkMappingCommand { get; }
        /// <summary> 插入默认解析脚本模板命令 </summary>
        public ICommand InsertDefaultScriptCommand { get; }
        /// <summary> 编译脚本命令：验证脚本语法和约定是否正确 </summary>
        public ICommand CompileScriptCommand { get; }
        /// <summary> 填充示例数据命令 </summary>
        public ICommand FillSampleDataCommand { get; }
        /// <summary> 执行测试命令：发送触发命令→解析→映射变量 </summary>
        public ICommand ExecuteTestCommand { get; }
        /// <summary> 使用示例数据执行测试命令：跳过TCP发送，直接用示例数据解析→映射变量 </summary>
        public ICommand ExecuteWithSampleDataCommand { get; }
        /// <summary> 关闭弹窗命令 </summary>
        public ICommand CloseCommand { get; }
        /// <summary> 保存并关闭弹窗命令 </summary>
        public ICommand SaveCommand { get; }

        /// <summary>
        /// 获取多语言文本（便捷方法）
        /// </summary>
        private string L(string key) => _containerProvider.Resolve<ILocalizationService>().GetResource(key);

        public VisionDetailViewModel(
            IContainerProvider containerProvider,
            IRecipePoolService recipePoolService,
            ITCPClientManagerService tcpClientManagerService,
            ITCPEventService tcpEventService,
            ILoggerService logger,
            IVisionDataParser defaultParser,
            ScriptVisionDataParser scriptParser,
            IEventAggregator eventAggregator)
        {
            _containerProvider = containerProvider;
            _recipePoolService = recipePoolService;
            _tcpClientManagerService = tcpClientManagerService;
            _tcpEventService = tcpEventService;
            _logger = logger;
            _defaultParser = defaultParser;
            _scriptParser = scriptParser;
            _eventAggregator = eventAggregator;

            AddMappingCommand = new DelegateCommand(OnAddMapping);
            DeleteMappingCommand = new DelegateCommand(OnDeleteMapping, () => SelectedMapping != null)
                .ObservesProperty(() => SelectedMapping);
            UnlinkMappingCommand = new DelegateCommand(OnUnlinkMapping, () => SelectedMapping != null)
                .ObservesProperty(() => SelectedMapping);
            InsertDefaultScriptCommand = new DelegateCommand(OnInsertDefaultScript);
            CompileScriptCommand = new DelegateCommand(OnCompileScript);
            FillSampleDataCommand = new DelegateCommand(OnFillSampleData);
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

            // 订阅全局变量变更事件，自动刷新显示值
            _eventAggregator.GetEvent<GlobalVariablesChangedEvent>().Subscribe(_ => RefreshMappingDisplayValues());
        }

        /// <summary>
        /// 从 ITCPClientManagerService 和 ITCPEventService 加载所有 TCP 连接名称
        /// Client模式：从客户端管理器获取已注册的客户端名称
        /// Server模式：从事件服务获取运行中的服务器名称（如TCP_1、TCP_2）
        /// </summary>
        private void LoadTcpConnections()
        {
            TcpConnections.Clear();
            try
            {
                if (_tcpClientManagerService?.Clients != null)
                {
                    foreach (var name in _tcpClientManagerService.Clients.Keys)
                        TcpConnections.Add(name);
                }

                if (_tcpEventService != null)
                {
                    foreach (var name in _tcpEventService.GetServerNames())
                    {
                        if (!TcpConnections.Contains(name))
                            TcpConnections.Add(name);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(string.Format(L("VisionDetail_Log_TcpLoadFailed"), ex.Message));
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

                // 同时填充可链接的全局变量列表（仅Double类型，供GlobalVariableLinkControl使用）
                LinkableGlobalVariables = new ObservableCollection<GlobalVariable>(
                    variables.Where(v => v.Type == GlobalVariableType.Double));
                RefreshMappingDisplayValues();
            }
            catch (Exception ex)
            {
                _logger?.Error(string.Format(L("VisionDetail_Log_VarLoadFailed"), ex.Message));
            }
        }

        /// <summary>
        /// 从 Step.VisionDetail 加载所有配置项到 ViewModel 属性
        /// </summary>
        private void InitializeFromStep()
        {
            if (_step == null) return;

            if (_step.VisionDetail == null)
                _step.VisionDetail = new VisionDetail();

            var detail = _step.VisionDetail;

            SelectedCommunicationType = detail.CommunicationType ?? "TCPIP";
            SelectedConnectionName = detail.ConnectionName ?? "";
            TriggerCommand = detail.TriggerCommand ?? "TRIGGER";
            ResponseTimeout = detail.ResponseTimeout;
            ParseScript = detail.ParseScript ?? "";

            RebuildMappingRows(detail.VariableMappings);

            RaisePropertyChanged(nameof(StepDescription));
            RaisePropertyChanged(nameof(IsTcpSelected));
        }

        /// <summary>
        /// 从VariableMapping列表重建MappingRows，订阅属性变更以自动刷新显示值
        /// </summary>
        private void RebuildMappingRows(ObservableCollection<VariableMapping> sourceMappings)
        {
            foreach (var row in MappingRows)
                row.PropertyChanged -= OnMappingRowPropertyChanged;
            MappingRows.Clear();

            if (sourceMappings != null)
            {
                foreach (var mapping in sourceMappings)
                {
                    var row = new VisionVariableMappingRow
                    {
                        SourceKey = mapping.SourceKey,
                        GlobalVariableName = mapping.GlobalVariableName
                    };
                    row.PropertyChanged += OnMappingRowPropertyChanged;
                    MappingRows.Add(row);
                }
            }

            RefreshMappingDisplayValues();
        }

        /// <summary>
        /// 映射行属性变更回调：当GlobalVariableName变更时自动刷新显示值
        /// </summary>
        private void OnMappingRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VisionVariableMappingRow.GlobalVariableName))
                RefreshMappingDisplayValues();
        }

        /// <summary>
        /// 添加一条新的变量映射行
        /// </summary>
        private void OnAddMapping()
        {
            var row = new VisionVariableMappingRow { SourceKey = "", GlobalVariableName = "" };
            row.PropertyChanged += OnMappingRowPropertyChanged;
            MappingRows.Add(row);
        }

        /// <summary>
        /// 删除当前选中的变量映射行
        /// </summary>
        private void OnDeleteMapping()
        {
            if (SelectedMapping != null)
            {
                SelectedMapping.PropertyChanged -= OnMappingRowPropertyChanged;
                MappingRows.Remove(SelectedMapping);
            }
        }

        /// <summary>
        /// 取消变量链接：清空当前选中映射行的全局变量名
        /// </summary>
        private void OnUnlinkMapping()
        {
            if (SelectedMapping != null)
                SelectedMapping.GlobalVariableName = null;
        }

        /// <summary>
        /// 插入默认的 C# 数据解析脚本模板
        /// </summary>
        private void OnInsertDefaultScript()
        {
            ParseScript = GetDefaultParseScript();
        }

        /// <summary>
        /// 填充示例数据到SampleData字段，同时填充变量映射示例行
        /// 方便用户理解数据格式和映射关系
        /// </summary>
        private void OnFillSampleData()
        {
            SampleData = "offsetX=0.523,offsetY=-0.317,angle=1.245,score=0.98";

            foreach (var row in MappingRows)
                row.PropertyChanged -= OnMappingRowPropertyChanged;
            MappingRows.Clear();

            var keys = new[] { "offsetX", "offsetY", "angle", "score" };
            foreach (var key in keys)
            {
                var row = new VisionVariableMappingRow { SourceKey = key, GlobalVariableName = "" };
                row.PropertyChanged += OnMappingRowPropertyChanged;
                MappingRows.Add(row);
            }

            _logger?.Info(L("VisionDetail_Log_SampleFilled"));
        }

        /// <summary>
        /// 执行测试：通过TCP发送触发命令→接收响应→解析→映射全局变量
        /// </summary>
        private async System.Threading.Tasks.Task OnExecuteTestAsync()
        {
            IsExecuting = true;
            IsExecuteSuccess = null;
            ExecuteResult = L("VisionDetail_ExecutingTrigger");

            try
            {
                if (string.IsNullOrEmpty(TriggerCommand) || string.IsNullOrEmpty(SelectedConnectionName))
                {
                    ExecuteResult = L("VisionDetail_Error_NoConfig");
                    IsExecuteSuccess = false;
                    return;
                }

                string response = await _tcpEventService.SendCommandWithResponseAsync(
                    SelectedConnectionName,
                    TriggerCommand,
                    ResponseTimeout);

                if (string.IsNullOrEmpty(response))
                {
                    ExecuteResult = L("VisionDetail_Warning_EmptyResponse");
                    IsExecuteSuccess = false;
                    return;
                }

                ExecuteResult = string.Format(L("VisionDetail_Received_Response"), response) + "\n";

                var parsedData = ParseDataInternal(response);
                if (parsedData.Count == 0)
                {
                    ExecuteResult += L("VisionDetail_Result_Empty");
                    IsExecuteSuccess = false;
                    return;
                }

                ExecuteResult += string.Format(L("VisionDetail_Parse_Result"),
                    string.Join(", ", parsedData.Select(kv => string.Format("{0}={1:F3}", kv.Key, kv.Value)))) + "\n";

                var mappingResult = await ApplyVariableMappingsAsync(parsedData);
                ExecuteResult += mappingResult;
                IsExecuteSuccess = true;
            }
            catch (TimeoutException)
            {
                ExecuteResult = string.Format(L("VisionDetail_Error_Timeout"), ResponseTimeout);
                IsExecuteSuccess = false;
            }
            catch (Exception ex)
            {
                ExecuteResult = string.Format(L("VisionDetail_Error_ExecuteFailed"), ex.Message);
                IsExecuteSuccess = false;
            }
            finally
            {
                IsExecuting = false;
            }
        }

        /// <summary>
        /// 使用示例数据执行测试：跳过TCP发送，直接用SampleData解析→映射全局变量
        /// 用于验证解析脚本和变量映射配置是否正确
        /// </summary>
        private async System.Threading.Tasks.Task OnExecuteWithSampleDataAsync()
        {
            IsExecuting = true;
            IsExecuteSuccess = null;
            ExecuteResult = L("VisionDetail_ExecutingSample");

            try
            {
                var parsedData = ParseDataInternal(SampleData);
                if (parsedData.Count == 0)
                {
                    ExecuteResult = L("VisionDetail_Error_SampleEmpty");
                    IsExecuteSuccess = false;
                    return;
                }

                ExecuteResult = string.Format(L("VisionDetail_Sample_Data"), SampleData) + "\n";
                ExecuteResult += string.Format(L("VisionDetail_Parse_Result"),
                    string.Join(", ", parsedData.Select(kv => string.Format("{0}={1:F3}", kv.Key, kv.Value)))) + "\n";

                var mappingResult = await ApplyVariableMappingsAsync(parsedData);
                ExecuteResult += mappingResult;
                IsExecuteSuccess = true;
            }
            catch (Exception ex)
            {
                ExecuteResult = string.Format(L("VisionDetail_Error_ExecuteFailed"), ex.Message);
                IsExecuteSuccess = false;
            }
            finally
            {
                IsExecuting = false;
            }
        }

        /// <summary>
        /// 内部解析方法：根据是否有自定义脚本选择对应解析器
        /// </summary>
        private Dictionary<string, double> ParseDataInternal(string rawData)
        {
            if (string.IsNullOrEmpty(rawData))
                return new Dictionary<string, double>();

            if (string.IsNullOrEmpty(ParseScript))
                return _defaultParser.Parse(rawData);

            _scriptParser.Script = ParseScript;
            return _scriptParser.Parse(rawData);
        }

        /// <summary>
        /// 应用变量映射：将解析结果写入全局变量并持久化
        /// </summary>
        private async System.Threading.Tasks.Task<string> ApplyVariableMappingsAsync(Dictionary<string, double> parsedData)
        {
            if (MappingRows == null || MappingRows.Count == 0)
                return L("VisionDetail_Map_NoMapping");

            var poolId = _recipePoolService.CurrentPoolName;
            if (string.IsNullOrEmpty(poolId))
                return L("VisionDetail_Map_NoPool");

            var globalVars = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
            var results = new List<string>();
            bool changed = false;

            foreach (var mapping in MappingRows)
            {
                if (string.IsNullOrEmpty(mapping.SourceKey) || string.IsNullOrEmpty(mapping.GlobalVariableName))
                    continue;

                if (!parsedData.TryGetValue(mapping.SourceKey, out double value))
                {
                    results.Add(string.Format(L("VisionDetail_Map_KeyNotFound"), mapping.SourceKey));
                    continue;
                }

                var targetVar = globalVars.FirstOrDefault(v => v.Name == mapping.GlobalVariableName);
                if (targetVar != null)
                {
                    targetVar.Value = value.ToString("F6");
                    results.Add(string.Format(L("VisionDetail_Map_Success"),
                        mapping.SourceKey, value.ToString("F3"), mapping.GlobalVariableName));
                    changed = true;
                }
                else
                {
                    results.Add(string.Format(L("VisionDetail_Map_VarNotFound"), mapping.GlobalVariableName));
                }
            }

            if (changed)
            {
                await _recipePoolService.SaveGlobalVariablesAsync(poolId, globalVars);
                _eventAggregator.GetEvent<GlobalVariablesChangedEvent>().Publish(poolId);
                RefreshMappingDisplayValues();
                results.Add(L("VisionDetail_Map_Saved"));
            }

            return L("VisionDetail_Map_Header") + "\n" + string.Join("\n", results);
        }

        /// <summary>
        /// 刷新所有映射行的DisplayValue，反映全局变量的当前值
        /// </summary>
        private void RefreshMappingDisplayValues()
        {
            if (LinkableGlobalVariables == null) return;
            foreach (var row in MappingRows)
            {
                if (!string.IsNullOrEmpty(row.GlobalVariableName))
                {
                    var gv = LinkableGlobalVariables.FirstOrDefault(v =>
                        string.Equals(v.Name, row.GlobalVariableName, StringComparison.OrdinalIgnoreCase));
                    row.DisplayValue = gv != null && double.TryParse(gv.Value, out double val) ? val : 0;
                }
                else
                {
                    row.DisplayValue = 0;
                }
            }
        }

        /// <summary>
        /// 编译验证当前脚本：不执行，仅检查语法和约定是否满足
        /// </summary>
        private void OnCompileScript()
        {
            IsExecuteSuccess = null;
            if (string.IsNullOrWhiteSpace(ParseScript))
            {
                ExecuteResult = L("VisionDetail_Compile_Empty");
                IsExecuteSuccess = false;
                return;
            }

            try
            {
                _scriptParser.Script = ParseScript;
                _scriptParser.CompileScript();
                ExecuteResult = L("VisionDetail_Compile_Success");
                IsExecuteSuccess = true;
            }
            catch (Exception ex)
            {
                ExecuteResult = ex.Message;
                IsExecuteSuccess = false;
            }
        }

        /// <summary>
        /// 返回默认的 C# 数据解析脚本模板
        /// 脚本约定：类名 VisionParseScript，含 public static Dictionary Parse(string) 方法
        /// 使用 // 注释而非 /// XML文档注释，避免 Natasha 编译器 CS1569 错误
        /// </summary>
        private string GetDefaultParseScript()
        {
            return @"using System;
using System.Collections.Generic;
using System.Globalization;

// VisionParseScript - 解析逗号分隔的 key=value 数据
// 支持前缀格式: Camera=SideCamera;VISION_RESULT:SUCCESS:offsetX=1.5,...
public class VisionParseScript
{
    public static Dictionary<string, double> Parse(string data)
    {
        var result = new Dictionary<string, double>();
        if (string.IsNullOrWhiteSpace(data)) return result;
        data = StripPrefix(data);
        var sep = new char[] { ',' };
        var pairs = data.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var kv = pair.Split(new[] { '=' }, 2);
            if (kv.Length == 2)
            {
                var key = kv[0].Trim();
                var val = kv[1].Trim();
                if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    result[key] = d;
            }
        }
        return result;
    }

    // 剥离前缀元数据，提取数值数据部分
    private static string StripPrefix(string data)
    {
        var comma = new char[] { ',' };
        var parts = data.Split(comma);
        if (parts.Length < 2) return data;
        var eq = new char[] { '=' };
        var firstKv = parts[0].Split(eq, 2);
        if (firstKv.Length != 2) return data;
        if (double.TryParse(firstKv[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            return data;
        for (int i = 0; i < parts.Length; i++)
        {
            var kv = parts[i].Split(eq, 2);
            if (kv.Length != 2) continue;
            if (double.TryParse(kv[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                if (i > 0)
                {
                    var prev = parts[i - 1].Split(eq, 2);
                    if (prev.Length == 2)
                    {
                        var pv = prev[1].Trim();
                        if (!double.TryParse(pv, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                        {
                            int s = pv.LastIndexOfAny(new[] { ':', ';' });
                            if (s >= 0)
                            {
                                var emb = pv.Substring(s + 1);
                                if (emb.Contains(""=""))
                                {
                                    var list = new List<string>();
                                    list.Add(emb);
                                    for (int j = i; j < parts.Length; j++) list.Add(parts[j]);
                                    return string.Join("","", list);
                                }
                            }
                        }
                    }
                }
                var r = new List<string>();
                for (int j = i; j < parts.Length; j++) r.Add(parts[j]);
                return string.Join("","", r);
            }
        }
        return data;
    }
}";
        }

        /// <summary>
        /// 关闭弹窗，不保存修改
        /// </summary>
        private void OnClose()
        {
            RequestClose?.Invoke(false);
        }

        /// <summary>
        /// 保存所有配置项到 Step.VisionDetail 并关闭弹窗
        /// </summary>
        private void OnSave()
        {
            if (_step == null) return;

            if (_step.VisionDetail == null)
                _step.VisionDetail = new VisionDetail();

            var detail = _step.VisionDetail;

            detail.CommunicationType = SelectedCommunicationType;
            detail.ConnectionName = SelectedConnectionName;
            detail.TriggerCommand = TriggerCommand;
            detail.ResponseTimeout = ResponseTimeout;
            detail.ParseScript = ParseScript;

            detail.VariableMappings = new ObservableCollection<VariableMapping>(
                MappingRows.Select(m => new VariableMapping
                {
                    SourceKey = m.SourceKey,
                    GlobalVariableName = m.GlobalVariableName
                }));

            _logger?.Info(string.Format(L("VisionDetail_Log_ConfigSaved"), _step.Seq, StepDescription));

            OnClose();
        }
    }

    /// <summary>
    /// VISION变量映射行模型，支持GlobalVariableLinkControl实时显示链接变量的当前值
    /// </summary>
    public class VisionVariableMappingRow : BindableBase
    {
        private string _sourceKey;
        /// <summary> 解析结果中的键名（如 offsetX、angle） </summary>
        public string SourceKey
        {
            get => _sourceKey;
            set => SetProperty(ref _sourceKey, value);
        }

        private string _globalVariableName;
        /// <summary> 映射到的全局变量名 </summary>
        public string GlobalVariableName
        {
            get => _globalVariableName;
            set
            {
                if (SetProperty(ref _globalVariableName, value))
                    RaisePropertyChanged(nameof(IsLinked));
            }
        }

        private double _displayValue;
        /// <summary> 链接的全局变量当前值，用于GlobalVariableLinkControl显示 </summary>
        public double DisplayValue
        {
            get => _displayValue;
            set => SetProperty(ref _displayValue, value);
        }

        /// <summary> 是否已链接到全局变量 </summary>
        public bool IsLinked => !string.IsNullOrEmpty(GlobalVariableName);
    }
}

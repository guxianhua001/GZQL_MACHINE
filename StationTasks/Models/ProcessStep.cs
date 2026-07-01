using Core.Models;
using Newtonsoft.Json;
using Prism.Mvvm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace StationTasks.Models
{
    /// <summary>
    /// 工艺步骤类型枚举。
    /// SIGNAL_SEND：发送信号（用于 Task 间信号交互，置位指定名称的信号）
    /// SIGNAL_WAIT：等待信号（阻塞等待指定信号被置位，消费后自动复位，支持超时）
    /// IF：条件分支块（支持 Then/Else 嵌套子步骤，表达式求值决定执行分支）
    /// </summary>
    public enum StepType { GOTO, SCAN, PICK, VISION, RELEASE, DISPENSE, CURE, SEEK, WAIT, SCRIPT, DASHBOARD, BRANCH, RUNTASK, SIGNAL_SEND, SIGNAL_WAIT, IF }

    /// <summary> GOTO 步骤的运动模式 </summary>
    public enum GotoModeEnum { Absolute, Home }

    //public enum StepType { GOTO,  ALIGN, SCAN, PICK, VISION, RELEASE, VERIFY, CHECK, DISPENSE, CURE, SEEK, WAIT, SCRIPT, DASHBOARD, BRANCH }
    public class ProcessStep : INotifyPropertyChanged
    {
        private int _seq;
        /// <summary> 步骤序号（由 RenumberSteps 统一管理，自动通知 UI 刷新） </summary>
        public int Seq
        {
            get => _seq;
            set
            {
                if (_seq != value)
                {
                    _seq = value;
                    OnPropertyChanged();
                }
            }
        }
        public StepType Step { get; set; }

        public string CompFeature { get; set; } = "—";
        public string SiteFeature { get; set; } = "—";
        public string Camera { get; set; } = "—";
        public string Purpose { get; set; }
        public ObservableCollection<SubMove> SubMoves { get; set; } = new ObservableCollection<SubMove>();

        private bool _isEnabled = true;
        /// <summary> 步骤启用状态：禁用的步骤在执行时被跳过 </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isExpanded;
        /// <summary> TreeView 展开状态（叶子节点占位，支持 ItemContainerStyle 统一绑定） </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isSelected;
        /// <summary> TreeView 选中状态（叶子节点占位，支持 ItemContainerStyle 统一绑定） </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        private GotoModeEnum _gotoMode = GotoModeEnum.Absolute;
        /// <summary> GOTO 步骤的运动模式：绝对定位 / 回零 </summary>
        public GotoModeEnum GotoMode
        {
            get => _gotoMode;
            set
            {
                if (_gotoMode != value)
                {
                    _gotoMode = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Gripper { get; set; } = "Gripper 1";
        public int Slot { get; set; } = 1;
        private bool _isCurrent;
        /// <summary> 当前执行步骤标记（运行时状态，不序列化） </summary>
        [JsonIgnore]
        public bool IsCurrent
        {
            get => _isCurrent;
            set
            {
                if (_isCurrent != value)
                {
                    _isCurrent = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isSingleExecuting;
        /// <summary> 单步执行时高亮标记（运行时状态，不序列化） </summary>
        [JsonIgnore]
        public bool IsSingleExecuting
        {
            get => _isSingleExecuting;
            set
            {
                if (_isSingleExecuting != value)
                {
                    _isSingleExecuting = value;
                    OnPropertyChanged();
                }
            }
        }

        public CheckDetail CheckDetail { get; set; }
        private PickDetail _pickDetail;
        public PickDetail PickDetail
        {
            get => _pickDetail;
            set
            {
                if (_pickDetail != value)
                {
                    _pickDetail = value;
                    OnPropertyChanged();
                }
            }
        }
        private ReleaseDetail _releaseDetail;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ReleaseDetail ReleaseDetail
        {
            get => _releaseDetail;
            set
            {
                if (_releaseDetail != value)
                {
                    _releaseDetail = value;
                    OnPropertyChanged();
                }
            }
        }
        private CureDetail _cureDetail;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public CureDetail CureDetail
        {
            get => _cureDetail;
            set
            {
                if (_cureDetail != value)
                {
                    _cureDetail = value;
                    OnPropertyChanged();
                }
            }
        }
        private IpqcDetail _ipqcDetail;
        public IpqcDetail IpqcDetail
        {
            get => _ipqcDetail;
            set
            {
                if (_ipqcDetail != value)
                {
                    _ipqcDetail = value;
                    OnPropertyChanged();
                }
            }
        }
        private ScanDetail _scanDetail;
        public ScanDetail ScanDetail
        {
            get => _scanDetail;
            set
            {
                if (_scanDetail != value)
                {
                    _scanDetail = value;
                    OnPropertyChanged();
                }
            }
        }
        private VisionDetail _visionDetail;
        public VisionDetail VisionDetail
        {
            get => _visionDetail;
            set
            {
                if (_visionDetail != value)
                {
                    _visionDetail = value;
                    OnPropertyChanged();
                }
            }
        }

        private DashboardStepDetail _dashboardDetail;

        /// <summary> DASHBOARD 步骤的看板配置（仅 StepType.DASHBOARD 时使用，其他步骤为 null） </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DashboardStepDetail DashboardDetail
        {
            get => _dashboardDetail;
            set { if (_dashboardDetail != value) { _dashboardDetail = value; OnPropertyChanged(); } }
        }

        private SeekDetail _seekDetail;

        /// <summary> SEEK 步骤的寻针配置（仅 StepType.SEEK 时使用，其他步骤为 null） </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public SeekDetail SeekDetail
        {
            get => _seekDetail;
            set { if (_seekDetail != value) { _seekDetail = value; OnPropertyChanged(); } }
        }

        private WaitDetail _waitDetail;

        /// <summary> WAIT/DELAY 步骤的延时配置（仅 StepType.WAIT 时使用） </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public WaitDetail WaitDetail
        {
            get => _waitDetail;
            set { if (_waitDetail != value) { _waitDetail = value; OnPropertyChanged(); } }
        }

        private ScriptDetail _scriptDetail;

        /// <summary> SCRIPT 步骤的脚本配置（仅 StepType.SCRIPT 时使用） </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ScriptDetail ScriptDetail
        {
            get => _scriptDetail;
            set { if (_scriptDetail != value) { _scriptDetail = value; OnPropertyChanged(); } }
        }

        private RunTaskDetail _runTaskDetail;

        /// <summary> RUNTASK 步骤的配置：要调用的被动任务名称 </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public RunTaskDetail RunTaskDetail
        {
            get => _runTaskDetail;
            set { if (_runTaskDetail != value) { _runTaskDetail = value; OnPropertyChanged(); } }
        }

        private SignalDetail _signalDetail;

        /// <summary>
        /// 信号交互步骤（SIGNAL_SEND / SIGNAL_WAIT）的配置。
        /// SIGNAL_SEND：发送指定名称的信号；SIGNAL_WAIT：等待指定名称的信号并消费。
        /// 其他步骤类型为 null。
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public SignalDetail SignalDetail
        {
            get => _signalDetail;
            set { if (_signalDetail != value) { _signalDetail = value; OnPropertyChanged(); } }
        }

        private DispenseDetail _dispenseDetail;

        /// <summary> DISPENSE 步骤的点胶配置（仅 StepType.DISPENSE 时使用） </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DispenseDetail DispenseDetail
        {
            get => _dispenseDetail;
            set
            {
                if (_dispenseDetail != value)
                {
                    _dispenseDetail = value;
                    OnPropertyChanged();
                }
            }
        }

        private BranchConfig _branchConfig;

        /// <summary>
        /// 步骤的条件分支配置（可选，启用后该步骤执行完会进行条件判断）
        /// 支持基于输出参数或全局变量的表达式判断，决定后续跳转目标
        /// 适用于所有步骤类型，实现通用的流程控制逻辑
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public BranchConfig BranchConfig
        {
            get => _branchConfig;
            set
            {
                if (_branchConfig != value)
                {
                    _branchConfig = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsBranchEnabled));
                }
            }
        }

        /// <summary> 是否启用了条件分支（扁平属性，供DataGrid列直接绑定） </summary>
        [JsonIgnore]
        public bool IsBranchEnabled => _branchConfig?.IsEnabled == true;

        private IfDetail _ifDetail;

        /// <summary>
        /// IF 步骤的条件配置（仅 StepType.IF 时使用）。
        /// 包含条件表达式、描述等信息，表达式支持 @GV: 和 @Output: 变量引用。
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public IfDetail IfDetail
        {
            get => _ifDetail;
            set
            {
                if (_ifDetail != value)
                {
                    _ifDetail = value;
                    OnPropertyChanged();
                }
            }
        }

        private ObservableCollection<IfBranchGroup> _ifBranches;

        /// <summary>
        /// IF 步骤的分支集合（仅 StepType.IF 时使用）。
        /// 包含两个 IfBranchGroup：Then 分支和 Else 分支，每个分支持有自己的子步骤列表。
        /// TreeView 通过此属性递归显示嵌套结构。
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ObservableCollection<IfBranchGroup> IfBranches
        {
            get => _ifBranches;
            set
            {
                if (_ifBranches != value)
                {
                    _ifBranches = value;
                    OnPropertyChanged();
                }
            }
        }

        private StepAlarmConfig _alarmConfig;

        public ProcessStep()
        {
            _alarmConfig = new StepAlarmConfig();
            _alarmConfig.PropertyChanged += OnAlarmConfigChanged;
        }

        /// <summary> 步骤报警配置，定义该步骤异常时的报警行为 </summary>
        public StepAlarmConfig AlarmConfig
        {
            get => _alarmConfig;
            set
            {
                if (_alarmConfig != null)
                    _alarmConfig.PropertyChanged -= OnAlarmConfigChanged;
                _alarmConfig = value ?? new StepAlarmConfig();
                if (_alarmConfig != null)
                    _alarmConfig.PropertyChanged += OnAlarmConfigChanged;
                OnPropertyChanged(nameof(AlarmConfig));
                OnPropertyChanged(nameof(IsAlarmEnabled));
            }
        }

        /// <summary> 报警是否启用（扁平属性，供DataGrid Alarm列直接绑定） </summary>
        public bool IsAlarmEnabled => _alarmConfig?.IsEnabled == true;

        /// <summary>
        /// Alarm 列显示文本：有自定义代码时显示 "CODE 一般"，否则显示 "SEEK_FAULT 一般" 等自动生成代码
        /// </summary>
        [JsonIgnore]
        public string AlarmDisplayText
        {
            get
            {
                if (_alarmConfig == null) return "";
                string code = !string.IsNullOrEmpty(_alarmConfig.AlarmCode)
                    ? _alarmConfig.AlarmCode
                    : $"{Step}_FAULT";
                string levelName = _alarmConfig.AlarmLevel switch
                {
                    1 => "L1-紧急",
                    2 => "L2-严重",
                    3 => "L3-一般",
                    4 => "L4-提示",
                    _ => $"L{_alarmConfig.AlarmLevel}"
                };
                return $"{code} {levelName}";
            }
        }

        /// <summary>
        /// 报警等级显示文本，如 "3-一般"
        /// </summary>
        [JsonIgnore]
        public string AlarmLevelDisplayText
        {
            get
            {
                if (_alarmConfig == null) return "";
                string levelName = _alarmConfig.AlarmLevel switch
                {
                    1 => "紧急",
                    2 => "严重",
                    3 => "一般",
                    4 => "提示",
                    _ => ""
                };
                return $"{_alarmConfig.AlarmLevel}-{levelName}";
            }
        }

        private bool _hasActiveAlarm;
        /// <summary> 该步骤是否存在未确认的活跃报警（仅当实际触发报警后才为true，用于行背景色标识；运行时状态，不序列化） </summary>
        [JsonIgnore]
        public bool HasActiveAlarm
        {
            get => _hasActiveAlarm;
            set
            {
                if (_hasActiveAlarm != value)
                {
                    _hasActiveAlarm = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _errorMessage;
        /// <summary> 步骤执行错误信息（运行时设置，不序列化），用于步骤编辑器错误详情展示 </summary>
        [JsonIgnore]
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        /// <summary> 步骤是否存在错误（ErrorMessage 非空时为 true） </summary>
        [JsonIgnore]
        public bool HasError => !string.IsNullOrEmpty(_errorMessage);

        private long _lastElapsedMs;
        /// <summary> 步骤最近一次执行的耗时（毫秒），运行时记录，不序列化 </summary>
        [JsonIgnore]
        public long LastElapsedMs
        {
            get => _lastElapsedMs;
            set
            {
                if (_lastElapsedMs != value)
                {
                    _lastElapsedMs = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _comment;
        /// <summary> 步骤注释（用户备注，可序列化持久化） </summary>
        public string Comment
        {
            get => _comment;
            set
            {
                if (_comment != value)
                {
                    _comment = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary> JSON反序列化后确保AlarmConfig已正确初始化，通知UI刷新绑定属性 </summary>
        public void EnsureAlarmConfigInitialized()
        {
            if (_alarmConfig == null)
                _alarmConfig = new StepAlarmConfig();
            // 重新订阅事件（反序列化可能丢失事件绑定）
            _alarmConfig.PropertyChanged -= OnAlarmConfigChanged;
            _alarmConfig.PropertyChanged += OnAlarmConfigChanged;
            // 加载时清除运行时状态（报警高亮标记等）
            _hasActiveAlarm = false;
            _errorMessage = null;
            // 强制通知UI刷新绑定属性
            OnPropertyChanged(nameof(IsAlarmEnabled));
            OnPropertyChanged(nameof(HasActiveAlarm));
            OnPropertyChanged(nameof(HasError));

            // 初始化BranchConfig（如果为null则创建默认实例）
            if (_branchConfig == null)
                _branchConfig = new BranchConfig();
            OnPropertyChanged(nameof(IsBranchEnabled));

            // 初始化 IF 步骤结构（IfDetail 和 IfBranches）
            // 反序列化后可能为 null，此处保证结构完整，确保 TreeView 正确显示嵌套节点
            EnsureIfStepStructureInitialized();

            // 递归初始化 IF 分支下的子步骤（支持多层嵌套）
            if (_ifBranches != null)
            {
                foreach (var branch in _ifBranches)
                {
                    if (branch?.Steps != null)
                    {
                        foreach (var subStep in branch.Steps)
                        {
                            subStep?.EnsureAlarmConfigInitialized();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 确保 IF 步骤的 IfDetail 和 IfBranches 结构已初始化。
        /// 反序列化旧数据或新建步骤时可能为 null，此处保证结构完整。
        /// 仅初始化顶层结构，不递归处理子步骤（子步骤由 EnsureAlarmConfigInitialized 递归处理）。
        /// </summary>
        private void EnsureIfStepStructureInitialized()
        {
            // 仅对 IF 类型步骤初始化 IfDetail 和 IfBranches
            if (Step != StepType.IF) return;

            if (_ifDetail == null)
            {
                _ifDetail = new IfDetail
                {
                    ConditionExpression = "",
                    Description = ""
                };
            }

            if (_ifBranches == null || _ifBranches.Count < 2)
            {
                var existingThen = _ifBranches?.FirstOrDefault(b =>
                    string.Equals(b.Header, "Then", StringComparison.OrdinalIgnoreCase));
                var existingElse = _ifBranches?.FirstOrDefault(b =>
                    string.Equals(b.Header, "Else", StringComparison.OrdinalIgnoreCase));

                _ifBranches = new ObservableCollection<IfBranchGroup>
                {
                    existingThen ?? new IfBranchGroup { Header = "Then", Steps = new ObservableCollection<ProcessStep>() },
                    existingElse ?? new IfBranchGroup { Header = "Else", Steps = new ObservableCollection<ProcessStep>() }
                };
            }
        }

        private void OnAlarmConfigChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StepAlarmConfig.IsEnabled))
            {
                OnPropertyChanged(nameof(IsAlarmEnabled));
                if (_alarmConfig?.IsEnabled == true && string.IsNullOrEmpty(_alarmConfig.AlarmCode))
                {
                    _alarmConfig.AlarmCode = $"{Step}_FAULT";
                }
            }
            OnPropertyChanged(nameof(AlarmDisplayText));
            OnPropertyChanged(nameof(AlarmLevelDisplayText));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary> 子步骤动作类型：用于在运动序列中插入夹爪/延时等非运动操作 </summary>
    public enum SubMoveAction
    {
        None,       // 默认：纯运动（向后兼容）
        Clamp,     // 夹爪夹紧
        Release,   // 夹爪释放
        Hold,      // 延时等待
        VacuumOn,  // 开真空
        VacuumOff, // 关真空
        UvOn,      // UV灯打开
        UvOff,     // UV灯关闭
        UvDelay    // UV固化延时
    }

    public class SubMove : BindableBase
    {
        private string _subSeq;
        private string _stationId;
        private string _axis;
        private int _axisId;
        private string _positionName;
        private string _description;
        private double _offset;
        private string _offsetVariableName;
        private double _speed;

        public string SubSeq { get => _subSeq; set => SetProperty(ref _subSeq, value); }
        public string StationId { get => _stationId; set => SetProperty(ref _stationId, value); }
        public string Axis { get => _axis; set => SetProperty(ref _axis, value); }
        public int AxisId { get => _axisId; set => SetProperty(ref _axisId, value); }
        public string PositionName { get => _positionName; set => SetProperty(ref _positionName, value); }
        public string Description { get => _description; set => SetProperty(ref _description, value); }
        public double Offset { get => _offset; set => SetProperty(ref _offset, value); }
        public string OffsetVariableName { get => _offsetVariableName; set => SetProperty(ref _offsetVariableName, value); }
        public double Speed { get => _speed; set => SetProperty(ref _speed, value); }

        private int _homeMode = 0;
        /// <summary> 回零模式（0=卡内配置，其他值=自定义模式，参考运动控制卡 SDK 文档） </summary>
        public int HomeMode { get => _homeMode; set => SetProperty(ref _homeMode, value); }

        private double _homeMinVel = 5;
        /// <summary> 回零低速（搜索原点时的速度） </summary>
        public double HomeMinVel { get => _homeMinVel; set => SetProperty(ref _homeMinVel, value); }

        private double _homeMaxVel = 10;
        /// <summary> 回零高速（寻找原点时的速度） </summary>
        public double HomeMaxVel { get => _homeMaxVel; set => SetProperty(ref _homeMaxVel, value); }

        private SubMoveAction _action = SubMoveAction.None;
        /// <summary> 子步骤动作类型：None=运动, Clamp=夹紧, Release=释放, Hold=延时 </summary>
        public SubMoveAction Action { get => _action; set => SetProperty(ref _action, value); }

        private double _actionParameter;
        /// <summary> 动作参数（如Clamp/Release的位置、Hold的时间），为0时使用PickDetail默认值 </summary>
        public double ActionParameter { get => _actionParameter; set => SetProperty(ref _actionParameter, value); }
    }

    public enum CheckOperator
    {
        LessThan, GreaterThan, Equal, NotEqual, AbsLessThan, AbsGreaterThan
    }

    public enum OnPassAction
    {
        Continue, SkipTo
    }

    public enum OnFailAction
    {
        Retry, Stop, SkipTo
    }

    public enum OnMaxExceededAction
    {
        Alarm, Stop, Continue
    }

    public class CheckDetail
    {
        public List<CheckItem> CheckItems { get; set; } = new List<CheckItem>();
        public OnPassAction OnPassAction { get; set; }
        public int OnPassJumpStepSeq { get; set; }
        public OnFailAction OnFailAction { get; set; }
        public int OnFailJumpStepSeq { get; set; }
        public int MaxRetries { get; set; }
        public OnMaxExceededAction OnMaxExceeded { get; set; }
    }

    public class CheckItem : BindableBase
    {
        private int _index;
        private bool _isChecked;
        private string _dataLink;
        private double _value;
        private bool _status;
        private double _lowerLimit;
        private double _upperLimit;
        private double _lowerTolerance;
        private double _upperTolerance;

        public int Index { get => _index; set => SetProperty(ref _index, value); }
        public bool IsChecked { get => _isChecked; set => SetProperty(ref _isChecked, value); }
        public string DataLink { get => _dataLink; set => SetProperty(ref _dataLink, value); }
        public double Value { get => _value; set => SetProperty(ref _value, value); }
        public bool Status { get => _status; set => SetProperty(ref _status, value); }
        public double LowerLimit { get => _lowerLimit; set => SetProperty(ref _lowerLimit, value); }
        public double UpperLimit { get => _upperLimit; set => SetProperty(ref _upperLimit, value); }
        public double LowerTolerance { get => _lowerTolerance; set => SetProperty(ref _lowerTolerance, value); }
        public double UpperTolerance { get => _upperTolerance; set => SetProperty(ref _upperTolerance, value); }
    }

    public class PickDetail : BindableBase
    {
        private int _vacuumPressure = 80;
        private int _pickHoldingTime = 500;
        private int _vacuumCheckDelay = 200;
        private bool _isVacuumOn;

        private double _clampPosition = 100.0;
        private double _releasePosition = 500.0;
        private bool _skipClampCheck;

        public int VacuumPressure { get => _vacuumPressure; set => SetProperty(ref _vacuumPressure, value); }
        
        /// <summary>
        /// 取料保持时间（毫秒）
        /// 实现方式：在 PickStepAction 中执行完夹紧动作后，Task.Delay(PickHoldingTime)
        /// 用途：确保真空吸附稳定或机械夹持牢固
        /// </summary>
        public int PickHoldingTime { get => _pickHoldingTime; set => SetProperty(ref _pickHoldingTime, value); }
        
        public int VacuumCheckDelay { get => _vacuumCheckDelay; set => SetProperty(ref _vacuumCheckDelay, value); }
        public bool IsVacuumOn { get => _isVacuumOn; set => SetProperty(ref _isVacuumOn, value); }

        /// <summary> 夹紧位置：执行夹紧命令时夹爪移动到的目标位置 </summary>
        public double ClampPosition { get => _clampPosition; set => SetProperty(ref _clampPosition, value); }
        
        /// <summary> 释放位置：执行释放命令时夹爪移动到的目标位置 </summary>
        public double ReleasePosition { get => _releasePosition; set => SetProperty(ref _releasePosition, value); }

        /// <summary> 跳过夹紧到位检测：勾选后夹紧动作不等待 DI 信号确认即继续下一步 </summary>
        public bool SkipClampCheck { get => _skipClampCheck; set => SetProperty(ref _skipClampCheck, value); }

        public ObservableCollection<SubMove> PickMoves { get; set; } = new ObservableCollection<SubMove>();
    }

    public class ReleaseDetail : BindableBase
    {
        private int _vacuumPressure = 80;
        private bool _isVacuumOn;
        private double _releasePosition = 500.0;
        private int _releaseDelayTime = 300;

        public int VacuumPressure { get => _vacuumPressure; set => SetProperty(ref _vacuumPressure, value); }
        public bool IsVacuumOn { get => _isVacuumOn; set => SetProperty(ref _isVacuumOn, value); }
        public double ReleasePosition { get => _releasePosition; set => SetProperty(ref _releasePosition, value); }
        public int ReleaseDelayTime { get => _releaseDelayTime; set => SetProperty(ref _releaseDelayTime, value); }
        public ObservableCollection<SubMove> ReleaseMoves { get; set; } = new ObservableCollection<SubMove>();
    }

    public class CureDetail : BindableBase
    {
        private int _uvHeadIndex = 1;
        private int _cureTimeMs = 5000;
        private int _stage1DurationMs = 1000;
        private double _stage1Intensity = 50.0;
        private int _stage2DurationMs = 1000;
        private double _stage2Intensity = 80.0;
        private int _stage3DurationMs = 1000;
        private double _stage3Intensity = 100.0;
        private int _stage4DurationMs = 2000;
        private double _stage4Intensity = 80.0;
        private int _uvHead1DoPort = 1;
        private int _uvHead2DoPort = 2;

        public int UvHeadIndex { get => _uvHeadIndex; set => SetProperty(ref _uvHeadIndex, value); }
        public int CureTimeMs { get => _cureTimeMs; set => SetProperty(ref _cureTimeMs, value); }
        public int Stage1DurationMs { get => _stage1DurationMs; set => SetProperty(ref _stage1DurationMs, value); }
        public double Stage1Intensity { get => _stage1Intensity; set => SetProperty(ref _stage1Intensity, value); }
        public int Stage2DurationMs { get => _stage2DurationMs; set => SetProperty(ref _stage2DurationMs, value); }
        public double Stage2Intensity { get => _stage2Intensity; set => SetProperty(ref _stage2Intensity, value); }
        public int Stage3DurationMs { get => _stage3DurationMs; set => SetProperty(ref _stage3DurationMs, value); }
        public double Stage3Intensity { get => _stage3Intensity; set => SetProperty(ref _stage3Intensity, value); }
        public int Stage4DurationMs { get => _stage4DurationMs; set => SetProperty(ref _stage4DurationMs, value); }
        public double Stage4Intensity { get => _stage4Intensity; set => SetProperty(ref _stage4Intensity, value); }
        /// <summary>
        /// 固化头1的DO输出端口
        /// </summary>
        public int UvHead1DoPort { get => _uvHead1DoPort; set => SetProperty(ref _uvHead1DoPort, value); }
        /// <summary>
        /// 固化头2的DO输出端口
        /// </summary>
        public int UvHead2DoPort { get => _uvHead2DoPort; set => SetProperty(ref _uvHead2DoPort, value); }

        public ObservableCollection<SubMove> CureMoves { get; set; } = new ObservableCollection<SubMove>();
    }

    public class IpqcDetail : BindableBase
    {
        private string _site;
        private string _checkType;
        private string _recipe;
        private string _camera;
        private double _toleranceXY;
        private double _toleranceZ;
        private int _maxRetries;
        private ObservableCollection<SubMove> _inspectionMoves;

        public string Site { get => _site; set => SetProperty(ref _site, value); }
        public string CheckType { get => _checkType; set => SetProperty(ref _checkType, value); }
        public string Recipe { get => _recipe; set => SetProperty(ref _recipe, value); }
        public string Camera { get => _camera; set => SetProperty(ref _camera, value); }
        public double ToleranceXY { get => _toleranceXY; set => SetProperty(ref _toleranceXY, value); }
        public double ToleranceZ { get => _toleranceZ; set => SetProperty(ref _toleranceZ, value); }
        public int MaxRetries { get => _maxRetries; set => SetProperty(ref _maxRetries, value); }
        public ObservableCollection<SubMove> InspectionMoves { get; set; } = new ObservableCollection<SubMove>();
    }

    public class ScanDetail : BindableBase
    {
        #region 原有扫描参数

        private string _scanMode = "Height Map";
        private double _stepX = 1.0;
        private double _stepY = 1.0;
        private double _scanRangeX = 50.0;
        private double _scanRangeY = 50.0;
        private ObservableCollection<SubMove> _scanMoves;

        public ObservableCollection<SubMove> ScanMoves { get; set; } = new ObservableCollection<SubMove>();

        /// <summary> 扫描模式（如 Height Map） </summary>
        public string ScanMode { get => _scanMode; set => SetProperty(ref _scanMode, value); }
        /// <summary> X方向扫描步距 </summary>
        public double StepX { get => _stepX; set => SetProperty(ref _stepX, value); }
        /// <summary> Y方向扫描步距 </summary>
        public double StepY { get => _stepY; set => SetProperty(ref _stepY, value); }
        /// <summary> X方向扫描范围 </summary>
        public double ScanRangeX { get => _scanRangeX; set => SetProperty(ref _scanRangeX, value); }
        /// <summary> Y方向扫描范围 </summary>
        public double ScanRangeY { get => _scanRangeY; set => SetProperty(ref _scanRangeY, value); }

        /// <summary> 扫描采集的数据点集合 </summary>
        public ObservableCollection<ScanDataPoint> ScanData { get; set; } = new ObservableCollection<ScanDataPoint>();

        #endregion

        #region 运动配置

        private int _zAxisId;
        private int _xAxisId;
        private int _yAxisId;
        private string _zInitPosition = "Z_Init";
        private string _xStartPosition = "X_Start";
        private string _zPhotoPosition = "Z_Photo";
        private string _xEndPosition = "X_End";
        private string _zSafePosition = "Z_Safe";
        private string _xStandbyPosition = "X_Standby";
        private string _yStartPosition = "Y_Start";
        private string _yEndPosition = "Y_End";
        private string _yStandbyPosition = "Y_Standby";
        private double _moveSpeed = 10.0;

        /// <summary> Z轴编号，用于控制Z轴运动 </summary>
        public int ZAxisId { get => _zAxisId; set => SetProperty(ref _zAxisId, value); }
        /// <summary> X轴编号，用于控制X轴运动 </summary>
        public int XAxisId { get => _xAxisId; set => SetProperty(ref _xAxisId, value); }
        /// <summary> Y轴编号，用于控制Y轴运动 </summary>
        public int YAxisId { get => _yAxisId; set => SetProperty(ref _yAxisId, value); }
        /// <summary> Z轴初始位置名称，扫描开始前Z轴先移动到此位置 </summary>
        public string ZInitPosition { get => _zInitPosition; set => SetProperty(ref _zInitPosition, value); }
        /// <summary> X轴起始位置名称，扫描时X轴从此位置开始 </summary>
        public string XStartPosition { get => _xStartPosition; set => SetProperty(ref _xStartPosition, value); }
        /// <summary> Z轴拍照高度位置名称，到达此高度后触发采集 </summary>
        public string ZPhotoPosition { get => _zPhotoPosition; set => SetProperty(ref _zPhotoPosition, value); }
        /// <summary> X轴结束位置名称，扫描时X轴到达此位置结束 </summary>
        public string XEndPosition { get => _xEndPosition; set => SetProperty(ref _xEndPosition, value); }
        /// <summary> Z轴安全高度位置名称，移动时Z轴先抬至此高度以防碰撞 </summary>
        public string ZSafePosition { get => _zSafePosition; set => SetProperty(ref _zSafePosition, value); }
        /// <summary> X轴待机位置名称，扫描完成后X轴回到此位置待命 </summary>
        public string XStandbyPosition { get => _xStandbyPosition; set => SetProperty(ref _xStandbyPosition, value); }
        /// <summary> Y轴起始位置名称，扫描时Y轴从此位置开始 </summary>
        public string YStartPosition { get => _yStartPosition; set => SetProperty(ref _yStartPosition, value); }
        /// <summary> Y轴结束位置名称，扫描时Y轴到达此位置结束 </summary>
        public string YEndPosition { get => _yEndPosition; set => SetProperty(ref _yEndPosition, value); }
        /// <summary> Y轴待机位置名称，扫描完成后Y轴回到此位置待命 </summary>
        public string YStandbyPosition { get => _yStandbyPosition; set => SetProperty(ref _yStandbyPosition, value); }
        /// <summary> 运动速度，扫描过程中轴的移动速度 </summary>
        public double MoveSpeed { get => _moveSpeed; set => SetProperty(ref _moveSpeed, value); }

        #endregion

        #region IO配置

        private int _triggerIoPort;
        private int _ioResetDelayMs = 200;

        /// <summary> 触发IO端口号，用于输出触发信号控制外部设备 </summary>
        public int TriggerIoPort { get => _triggerIoPort; set => SetProperty(ref _triggerIoPort, value); }
        /// <summary> IO自动复位延时（毫秒），触发信号输出后延时自动复位 </summary>
        public int IoResetDelayMs { get => _ioResetDelayMs; set => SetProperty(ref _ioResetDelayMs, value); }

        #endregion

        #region 通讯配置

        private string _communicationType = "TCPIP";
        private string _connectionName = "";
        private int _responseTimeout = 5000;

        /// <summary> 通讯方式（如 TCPIP、Serial 等） </summary>
        public string CommunicationType { get => _communicationType; set => SetProperty(ref _communicationType, value); }
        /// <summary> 连接名称，对应通讯配置中已建立的连接 </summary>
        public string ConnectionName { get => _connectionName; set => SetProperty(ref _connectionName, value); }
        /// <summary> 响应超时时间（毫秒），等待设备回复的最大时长 </summary>
        public int ResponseTimeout { get => _responseTimeout; set => SetProperty(ref _responseTimeout, value); }

        #endregion

        #region 数据解析配置

        private string _parseScript = "";
        private ObservableCollection<VariableMapping> _variableMappings = new ObservableCollection<VariableMapping>();
        private int _tabCount = 6;
        private ObservableCollection<string> _tabHeightKeys = new ObservableCollection<string>
        {
            "Tab1Height", "Tab2Height", "Tab3Height",
            "Tab4Height", "Tab5Height", "Tab6Height"
        };

        /// <summary> C#数据解析脚本代码，用于自定义解析设备返回的原始数据 </summary>
        public string ParseScript { get => _parseScript; set => SetProperty(ref _parseScript, value); }
        /// <summary> 全局变量映射集合，将解析结果中的键名映射到全局变量 </summary>
        public ObservableCollection<VariableMapping> VariableMappings { get => _variableMappings; set => SetProperty(ref _variableMappings, value); }
        /// <summary> Tab数量，扫描采集的Tab（芯片）个数 </summary>
        public int TabCount { get => _tabCount; set => SetProperty(ref _tabCount, value); }
        /// <summary> Tab高度键名集合，每个Tab对应一个高度变量键名（如 Tab1Height ~ Tab6Height） </summary>
        public ObservableCollection<string> TabHeightKeys { get => _tabHeightKeys; set => SetProperty(ref _tabHeightKeys, value); }

        #endregion

        #region 扫描结果持久化

        private ObservableCollection<ScanResultItem> _lastScanResults = new ObservableCollection<ScanResultItem>();
        private string _lastSampleData = "";
        private string _lastReceivedTime = "";
        private string _lastReceivedData = "";

        /// <summary> 上次扫描结果，用于持久化保存，重新打开编辑器时显示最后一次的解析值 </summary>
        public ObservableCollection<ScanResultItem> LastScanResults { get => _lastScanResults; set => SetProperty(ref _lastScanResults, value); }
        /// <summary> 上次样本数据，用于持久化保存 </summary>
        public string LastSampleData { get => _lastSampleData; set => SetProperty(ref _lastSampleData, value); }
        /// <summary> 上次接收时间，用于持久化保存 </summary>
        public string LastReceivedTime { get => _lastReceivedTime; set => SetProperty(ref _lastReceivedTime, value); }
        /// <summary> 上次接收数据摘要，用于持久化保存 </summary>
        public string LastReceivedData { get => _lastReceivedData; set => SetProperty(ref _lastReceivedData, value); }

        #endregion
    }

    public class ScanDataPoint : BindableBase
    {
        private int _num;
        private double _baseHeight;
        private double _upperLimit;
        private double _lowerLimit;
        private double _h1Height;
        private double _h2Height;
        private double _difference;
        private string _status;

        public int Num { get => _num; set => SetProperty(ref _num, value); }
        public double BaseHeight { get => _baseHeight; set => SetProperty(ref _baseHeight, value); }
        public double UpperLimit { get => _upperLimit; set => SetProperty(ref _upperLimit, value); }
        public double LowerLimit { get => _lowerLimit; set => SetProperty(ref _lowerLimit, value); }
        public double H1Height { get => _h1Height; set => SetProperty(ref _h1Height, value); }
        public double H2Height { get => _h2Height; set => SetProperty(ref _h2Height, value); }
        public double Difference { get => _difference; set => SetProperty(ref _difference, value); }
        public string Status { get => _status; set => SetProperty(ref _status, value); }
    }

    public class VisionDetail : BindableBase
    {
        private string _selectedCamera = "Side Camera";
        private string _selectedSlot = "Slot 1";
        private ObservableCollection<Camera2DDataRow> _dataRows;
        private string _communicationType = "TCPIP";
        private string _connectionName = "";
        private string _triggerCommand = "TRIGGER";
        private int _responseTimeout = 5000;
        private string _parseScript = "";
        private ObservableCollection<VariableMapping> _variableMappings = new ObservableCollection<VariableMapping>();

        public string SelectedCamera { get => _selectedCamera; set => SetProperty(ref _selectedCamera, value); }
        public string SelectedSlot { get => _selectedSlot; set => SetProperty(ref _selectedSlot, value); }
        public ObservableCollection<Camera2DDataRow> DataRows { get => _dataRows; set => SetProperty(ref _dataRows, value); }

        /// <summary> 通讯方式（TCPIP/Serial等） </summary>
        public string CommunicationType { get => _communicationType; set => SetProperty(ref _communicationType, value); }
        /// <summary> 选定的TCPIP连接名称 </summary>
        public string ConnectionName { get => _connectionName; set => SetProperty(ref _connectionName, value); }
        /// <summary> 触发拍照命令字符串 </summary>
        public string TriggerCommand { get => _triggerCommand; set => SetProperty(ref _triggerCommand, value); }
        /// <summary> 响应超时时间（毫秒） </summary>
        public int ResponseTimeout { get => _responseTimeout; set => SetProperty(ref _responseTimeout, value); }
        /// <summary> C#数据解析脚本代码 </summary>
        public string ParseScript { get => _parseScript; set => SetProperty(ref _parseScript, value); }
        /// <summary> 全局变量映射集合 </summary>
        public ObservableCollection<VariableMapping> VariableMappings { get => _variableMappings; set => SetProperty(ref _variableMappings, value); }
    }

    public class Camera2DDataRow : BindableBase
    {
        private string _type;
        private double _x;
        private double _y;
        private double _u;
        private double _distance;
        private double _x2;
        private double _y2;
        private double _u2;
        private double _distance2;

        public string Type { get => _type; set => SetProperty(ref _type, value); }
        public double X { get => _x; set => SetProperty(ref _x, value); }
        public double Y { get => _y; set => SetProperty(ref _y, value); }
        public double U { get => _u; set => SetProperty(ref _u, value); }
        public double Distance { get => _distance; set => SetProperty(ref _distance, value); }
        public double X2 { get => _x2; set => SetProperty(ref _x2, value); }
        public double Y2 { get => _y2; set => SetProperty(ref _y2, value); }
        public double U2 { get => _u2; set => SetProperty(ref _u2, value); }
        public double Distance2 { get => _distance2; set => SetProperty(ref _distance2, value); }
    }

    /// <summary>
    /// 全局变量映射：将解析结果的键名映射到全局变量名
    /// 支持两个目标：原始实测值 和 补偿后值（实测值+固定补偿）分别写入不同全局变量
    /// </summary>
    public class VariableMapping : BindableBase
    {
        private string _sourceKey;
        private string _globalVariableName;
        private string _compensatedGlobalVariableName;
        private double _fixedCompensation;
        private double _baseZValue = 11.5;

        /// <summary> 解析结果中的键名（如 Tab1Height、offsetX） </summary>
        public string SourceKey { get => _sourceKey; set => SetProperty(ref _sourceKey, value); }
        /// <summary> 映射到的全局变量名（接收原始实测值） </summary>
        public string GlobalVariableName
        {
            get => _globalVariableName;
            set
            {
                if (SetProperty(ref _globalVariableName, value))
                    RaisePropertyChanged(nameof(IsLinked));
            }
        }
        /// <summary> 补偿后值写入的全局变量名（接收 实测值+固定补偿，可为空表示不写入） </summary>
        public string CompensatedGlobalVariableName
        {
            get => _compensatedGlobalVariableName;
            set
            {
                if (SetProperty(ref _compensatedGlobalVariableName, value))
                    RaisePropertyChanged(nameof(IsCompensationLinked));
            }
        }
        /// <summary> 固定补偿值（mm），补偿后值 = 实测值 + 固定补偿 </summary>
        public double FixedCompensation { get => _fixedCompensation; set => SetProperty(ref _fixedCompensation, value); }
        /// <summary> 基准Z值（mm），偏差 = 实测值 - 基准Z值 </summary>
        public double BaseZValue { get => _baseZValue; set => SetProperty(ref _baseZValue, value); }

        /// <summary> 是否已链接原始值全局变量 </summary>
        public bool IsLinked => !string.IsNullOrEmpty(GlobalVariableName);
        /// <summary> 是否已链接补偿后值全局变量 </summary>
        public bool IsCompensationLinked => !string.IsNullOrEmpty(CompensatedGlobalVariableName);

        private double _displayValue;
        /// <summary> 链接的全局变量当前值，用于 GlobalVariableLinkControl 实时显示 </summary>
        public double DisplayValue
        {
            get => _displayValue;
            set => SetProperty(ref _displayValue, value);
        }

        private double _compensatedDisplayValue;
        /// <summary> 补偿后全局变量当前值，用于 GlobalVariableLinkControl 实时显示 </summary>
        public double CompensatedDisplayValue
        {
            get => _compensatedDisplayValue;
            set => SetProperty(ref _compensatedDisplayValue, value);
        }
    }

    /// <summary>
    /// 扫描结果项，用于解析数据结果表格显示和持久化保存
    /// 行数由变量映射（VariableMappings）动态决定，每行对应一个解析键名
    /// </summary>
    public class ScanResultItem : BindableBase
    {
        private int _index;
        private string _name;
        private double _baseZValue;
        private double _upperLimit = 15.0;
        private double _lowerLimit = 8.0;
        private double _measuredValue;
        private double _deviation;
        private double _fixedCompensation;
        private string _targetGlobalVariable;
        private string _status = "---";

        /// <summary> 序号（从1开始） </summary>
        public int Index { get => _index; set => SetProperty(ref _index, value); }
        /// <summary> 名称（来自变量映射的 SourceKey，如 Tab1Height） </summary>
        public string Name { get => _name; set => SetProperty(ref _name, value); }
        /// <summary> 基准Z值（标称值/中值） </summary>
        public double BaseZValue { get => _baseZValue; set => SetProperty(ref _baseZValue, value); }
        /// <summary> 上限值（mm） </summary>
        public double UpperLimit { get => _upperLimit; set => SetProperty(ref _upperLimit, value); }
        /// <summary> 下限值（mm） </summary>
        public double LowerLimit { get => _lowerLimit; set => SetProperty(ref _lowerLimit, value); }
        /// <summary> 实测值（mm） </summary>
        public double MeasuredValue { get => _measuredValue; set => SetProperty(ref _measuredValue, value); }
        /// <summary> 偏差值 = 实测值 - 基准Z值 </summary>
        public double Deviation { get => _deviation; set => SetProperty(ref _deviation, value); }
        /// <summary> 固定补偿值（mm） </summary>
        public double FixedCompensation { get => _fixedCompensation; set => SetProperty(ref _fixedCompensation, value); }
        /// <summary> 补偿后值 = 实测值 + 固定补偿（mm），只读计算属性 </summary>
        public double CompensatedValue => MeasuredValue + FixedCompensation;
        /// <summary> 补偿后将写入的全局变量名（来自变量映射配置） </summary>
        public string TargetGlobalVariable
        {
            get => _targetGlobalVariable;
            set
            {
                if (SetProperty(ref _targetGlobalVariable, value))
                    RaisePropertyChanged(nameof(IsDeviationLinked));
            }
        }
        /// <summary> 是否已链接到偏差全局变量（供 GlobalVariableLinkControl 使用） </summary>
        [JsonIgnore]
        public bool IsDeviationLinked => !string.IsNullOrEmpty(_targetGlobalVariable);
        /// <summary> 检测状态：Pass(合格) / Fail(超限) / ---(未检测)，根据实测值与上下限判定 </summary>
        public string Status { get => _status; set => SetProperty(ref _status, value); }
    }

    /// <summary>
    /// SEEK 步骤的通道行，定义单个通道的力控寻针参数
    /// </summary>
    public class SeekChannelRow : BindableBase
    {
        private int _sub;
        /// <summary> 子步骤序号 </summary>
        public int Sub { get => _sub; set => SetProperty(ref _sub, value); }

        private int _linkedChannel;
        /// <summary> 关联通道号（AD通道从0开始，0-8） </summary>
        public int LinkedChannel { get => _linkedChannel; set => SetProperty(ref _linkedChannel, value); }

        private double _targetForce = 5.0;
        /// <summary> 目标力值（N） </summary>
        public double TargetForce { get => _targetForce; set => SetProperty(ref _targetForce, value); }

        private double _forceMin = -20.0;
        /// <summary> 力值下限（N） </summary>
        public double ForceMin { get => _forceMin; set => SetProperty(ref _forceMin, value); }

        private double _forceMax = 20.0;
        /// <summary> 力值上限（N） </summary>
        public double ForceMax { get => _forceMax; set => SetProperty(ref _forceMax, value); }

        private string _linkedVariableName;
        /// <summary> 关联变量名，寻针结果写入该全局变量 </summary>
        public string LinkedVariableName
        {
            get => _linkedVariableName;
            set
            {
                if (SetProperty(ref _linkedVariableName, value))
                    RaisePropertyChanged(nameof(IsLinked));
            }
        }

        /// <summary> 是否已链接到全局变量（供 GlobalVariableLinkControl 使用） </summary>
        [JsonIgnore]
        public bool IsLinked => !string.IsNullOrEmpty(_linkedVariableName);

        private string _description;
        /// <summary> 通道描述 </summary>
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        [JsonIgnore]
        private double _currentForce;
        /// <summary> 当前力值（运行时实时更新，不序列化） </summary>
        public double CurrentForce { get => _currentForce; set => SetProperty(ref _currentForce, value); }

        [JsonIgnore]
        private bool _isForceInRange = true;
        /// <summary> 力值是否在合格范围内（运行时判定，不序列化） </summary>
        public bool IsForceInRange { get => _isForceInRange; set => SetProperty(ref _isForceInRange, value); }

        [JsonIgnore]
        private string _channelUnit = "N";
        /// <summary> 通道物理量单位（来自 hwcfg.xml 配置，不序列化） </summary>
        public string ChannelUnit { get => _channelUnit; set => SetProperty(ref _channelUnit, value); }

        [JsonIgnore]
        private string _channelName;
        /// <summary> 通道名称（来自 hwcfg.xml 配置，不序列化） </summary>
        public string ChannelName { get => _channelName; set => SetProperty(ref _channelName, value); }
    }

    /// <summary>
    /// SEEK 步骤的详细配置，包含所有通道的寻针参数
    /// </summary>
    public class SeekDetail : BindableBase
    {
        private ObservableCollection<SeekChannelRow> _channelRows = new ObservableCollection<SeekChannelRow>();
        /// <summary> 通道行集合，每个通道定义独立的寻针力控参数 </summary>
        public ObservableCollection<SeekChannelRow> ChannelRows
        {
            get => _channelRows;
            set => SetProperty(ref _channelRows, value ?? new ObservableCollection<SeekChannelRow>());
        }
    }

    /// <summary>
    /// WAIT/DELAY 步骤的延时配置
    /// </summary>
    public class WaitDetail : BindableBase
    {
        private double _delayMs = 1000;
        /// <summary> 延时时长（毫秒） </summary>
        public double DelayMs
        {
            get => _delayMs;
            set => SetProperty(ref _delayMs, value);
        }

        private string _timeUnit = "ms";
        /// <summary> 时间单位：ms / s / min </summary>
        public string TimeUnit
        {
            get => _timeUnit;
            set => SetProperty(ref _timeUnit, value);
        }

        private string _description;
        /// <summary> 延时说明 </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary> 转换为实际毫秒数（根据 TimeUnit 换算） </summary>
        [JsonIgnore]
        public double ActualDelayMs => TimeUnit switch
        {
            "s" => DelayMs * 1000,
            "min" => DelayMs * 60000,
            _ => DelayMs
        };
    }

    /// <summary>
    /// SCRIPT 步骤的脚本配置，包含 C# 脚本代码及引用信息
    /// </summary>
    public class ScriptDetail : BindableBase
    {
        private string _scriptCode;
        /// <summary> C# 脚本代码（类名必须为 ScriptAction） </summary>
        public string ScriptCode
        {
            get => _scriptCode;
            set => SetProperty(ref _scriptCode, value);
        }

        private ObservableCollection<string> _referencedAssemblies = new ObservableCollection<string>();
        /// <summary> 额外引用的程序集名称 </summary>
        public ObservableCollection<string> ReferencedAssemblies
        {
            get => _referencedAssemblies;
            set => SetProperty(ref _referencedAssemblies, value ?? new ObservableCollection<string>());
        }

        private ObservableCollection<string> _referencedNamespaces = new ObservableCollection<string>();
        /// <summary> 额外引用的命名空间 </summary>
        public ObservableCollection<string> ReferencedNamespaces
        {
            get => _referencedNamespaces;
            set => SetProperty(ref _referencedNamespaces, value ?? new ObservableCollection<string>());
        }

        private string _description;
        /// <summary> 脚本说明 </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
    }

    /// <summary>
    /// 信号交互步骤（SIGNAL_SEND / SIGNAL_WAIT）的配置模型。
    /// 用于 Task 间的信号同步：一个 Task 发送信号，另一个 Task 等待信号后才继续执行。
    /// 信号使用一次性消费语义：被等待方消费后自动复位。
    /// </summary>
    public class SignalDetail : BindableBase
    {
        private string _signalName;
        /// <summary>
        /// 信号名称（全局唯一标识）。
        /// 命名建议：使用有意义的名称，如 "TaskA_Ready"、"Material_Loaded" 等。
        /// 发送方和等待方必须使用相同的信号名称才能完成交互。
        /// </summary>
        public string SignalName
        {
            get => _signalName;
            set => SetProperty(ref _signalName, value);
        }

        private int _timeoutMs = -1;
        /// <summary>
        /// 等待超时时间（毫秒），仅对 SIGNAL_WAIT 有效。
        /// &lt;=0：无限等待，直到收到信号或被取消（急停/停止）
        /// &gt;0：等待指定毫秒数，超时后触发超时处理
        /// 工业安全考虑：长时间等待应配合急停/停止按钮使用
        /// </summary>
        public int TimeoutMs
        {
            get => _timeoutMs;
            set => SetProperty(ref _timeoutMs, value);
        }

        private string _description;
        /// <summary> 信号说明（可选，用于备注信号用途） </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
    }

    /// <summary>
    /// IF 步骤的条件配置模型。
    /// 包含条件表达式（支持 @GV: 和 @Output: 变量引用）和说明信息。
    /// 表达式由 FormulaEvaluator 求值，非 0 为 true（执行 Then 分支），0 为 false（执行 Else 分支）。
    /// </summary>
    public class IfDetail : BindableBase
    {
        private string _conditionExpression = "";
        /// <summary>
        /// 条件表达式（支持 @GV:全局变量、@Output:步骤输出 变量引用）。
        /// 示例："@GV:检测结果 == true"、"@Output:PassFlag == 1 && @GV:Count > 0"。
        /// 表达式为空或求值失败时按 false 处理（执行 Else 分支）。
        /// </summary>
        public string ConditionExpression
        {
            get => _conditionExpression;
            set => SetProperty(ref _conditionExpression, value);
        }

        private string _description;
        /// <summary> IF 步骤说明（可选，用于备注分支用途） </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private bool _isValidationValid = true;
        /// <summary> 表达式语法校验是否通过（运行时状态，不序列化） </summary>
        [JsonIgnore]
        public bool IsValidationValid
        {
            get => _isValidationValid;
            set => SetProperty(ref _isValidationValid, value);
        }

        private string _validationMessage;
        /// <summary> 表达式校验消息（运行时状态，不序列化） </summary>
        [JsonIgnore]
        public string ValidationMessage
        {
            get => _validationMessage;
            set => SetProperty(ref _validationMessage, value);
        }
    }

    /// <summary>
    /// IF 步骤的分支组（Then 或 Else）。
    /// 作为 TreeView 的虚拟中间节点，承载分支标题和子步骤集合，支持递归嵌套。
    /// 序列化时与 IF 步骤一同保存，反序列化后自动恢复树形结构。
    /// </summary>
    public class IfBranchGroup : BindableBase
    {
        private string _header;
        /// <summary> 分支标题："Then" 或 "Else" </summary>
        public string Header
        {
            get => _header;
            set => SetProperty(ref _header, value);
        }

        private ObservableCollection<ProcessStep> _steps = new ObservableCollection<ProcessStep>();
        /// <summary> 该分支下的子步骤集合（支持嵌套 IF 步骤） </summary>
        public ObservableCollection<ProcessStep> Steps
        {
            get => _steps;
            set => SetProperty(ref _steps, value ?? new ObservableCollection<ProcessStep>());
        }

        private bool _isExpanded = true;
        /// <summary> TreeView 展开状态 </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private bool _isSelected;
        /// <summary> TreeView 选中状态 </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}

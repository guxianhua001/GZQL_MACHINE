---
alwaysApply: false
description: 
---
# GZQL_MACHINE Repo Wiki

## 项目概述

工业设备控制系统，WPF桌面应用，用于半导体封装设备的运动控制、视觉检测、配方管理和工艺流程编排。

- **框架**: WPF + .NET 9.0 (net9.0-windows7.0)
- **MVVM**: Prism 8.1 (Unity容器)
- **UI库**: Material Design in XAML 5.3.x
- **视觉**: Halcon (MVTec) + OpenCvSharp4
- **运动控制**: 雷赛运动控制卡 (LTDMC)
- **持久化**: EF Core SQLite (报警), JSON (配方/配置)
- **脚本**: Natasha (Roslyn动态编译)
- **日志**: NLog
- **通信**: TCP/IP (System.Net.Sockets)
- **版本**: Directory.Build.props 自动版本号

## 解决方案结构 (16个项目)

| 项目 | 职责 | 关键依赖 |
|------|------|----------|
| MainApp | WPF入口, Shell, DI注册 | 所有模块 |
| Core | 抽象接口, 模型, 服务 | HalconWrapper, MathNet |
| Framework | UI框架, 对话框, MVVM基础 | Core, MaterialDesign, Prism.Unity |
| Module | UI控件与视图(最大模块) | Core, Framework, 几乎所有模块 |
| ModuleCore | 登录, 权限, 导航 | Core, Framework |
| MotionControl | 运动控制核心 | Core, AlarmModule |
| StationTasks | 工站任务执行, 步骤动作 | Core, MotionControl, Recipe, TCPIPModule |
| RecipeManagement | 配方池, 参数存储 | Core, Framework, EF Core SQLite |
| AlarmModule | 报警管理, 阈值配置 | Core, Prism.Wpf |
| LanguageModule | 多语言切换 | Core, Prism.Wpf |
| TCPIPModule | TCP/IP通信 | Core, Framework |
| HalconWrapper | Halcon视觉库封装 | halcondotnet |

## 架构层次与依赖

```
Core (最底层, 无业务依赖)
  ↑
Framework → ModuleCore → Module (UI聚合层)
  ↑              ↑
MotionControl  StationTasks
  ↑              ↑
RecipeManagement  TCPIPModule
  ↑
AlarmModule
```

核心依赖链: Core → Framework → ModuleCore → Module → StationTasks → MotionControl → Core

## DI 容器注册

### App.xaml.cs 顶层注册
- ILogger (NLog) → Instance
- IConfigurationService → Singleton<ConfigurationService>
- ILoggerService → Singleton<LoggerService> (有界通道容量1000)
- ILocalizationService → Singleton<LocalizationService>
- IAppSettingService → Singleton<ConfigurationService>
- IStationRegistry → Singleton<StationRegistry>

### 各模块关键注册
- **FrameworkModule**: IParameterStorage, IParameterDialogService, IParameterService
- **MotionControlModule**: IMotionCardFactory, IMotionService, IHardwareConfigLoader, ISystemStateService, ISpeedOverrideService, ITaskManager, IGripperService
- **StationTasksModule**: LoadingTask/DispensingTask/AssemblyTask (ITask + IStationParameterProvider + IBatchSwitchable), IProcessStepAction实现, IPositionProvider
- **RecipeModule**: IRecipePoolService, IRecipeStorage, IGenericStorage
- **AlarmModule**: IAlarmRepository, IAlarmService, AlarmDbContext (SQLite)

### DI原则
- 接口优先、单例为主、构造函数注入
- DbContext不可注册为Singleton
- 对话框注册为Transient

## 关键接口

### 运动控制 (MotionControl/Interfaces/)
- **IMotionCard**: 运动控制卡底层抽象(雷赛/虚拟卡), 绝对/相对运动, 回零, JOG, 急停, IO读写, 连续插补
- **IMotionService**: 高层运动API, IObservable<AxisStateChangedEvent>, 异步运动, 轮询, 模拟量
  - InitializeAsync, MoveAbsAsync, MoveRelAsync, HomeAsync, JogStart/Stop, EmergencyStop, 连续插补系列
- **IAxis**: 轴状态(位置, 运动状态, 报警, 使能)
- **ITaskManager**: 任务管理(启动/停止/暂停/急停/回零所有工站)
- **IGripperService**: 夹爪控制
- **ISpeedOverrideService**: 全局速度倍率
- **ISystemStateService**: 系统状态监控

### 运动控制三层架构
```
IMotionService (接口层)
  → MotionService (服务层)
    → IMotionCard / MotionCardBase / VirtualMotionCard (硬件抽象层)
```
- 轮询策略: 10ms快周期 + 每3次完整轮询
- 工厂模式: IMotionCardFactory(GetCard/GetDefaultCard)
- 配置文件: hwcfg.xml
- IsSimulationMode标识虚拟卡模式

### 配方管理 (RecipeManagement/Interfaces/)
- **IRecipePoolService**: 配方池管理(创建/删除/切换/加载全局变量)
  - SwitchAllStationsAsync遍历IStationRegistry中IBatchSwitchable工站
- **IRecipeStorage**: 配方持久化
- **IBatchSwitchable**: 批量切换支持
- **RecipeService<T>**: 单工站参数服务(SwitchRecipeAsync/SaveParametersToRecipe/LoadRecipeParameters), 内部SemaphoreSlim互斥

### 配方数据模型
```
RecipePool (含RecipeInfo[] + GlobalVariables[])
  → RecipeInfo (含Parameters字典按工站键)
    → CurrentRecipeInfo
```
- 存储: IGenericStorage文件持久化 + 备份
- 参数暂存: ParameterStagingArea批量提交

### 核心抽象 (Core/Abstraction/)
- **IStationRegistry**: Register/Unregister/GetAllStations/GetStation, ConcurrentDictionary存储
- **IStationParameterProvider**: StationIdentifier/CurrentPoolName/CurrentRecipeName/CurrentParameters/HasUnsavedChanges
- **ILocalizationService**: SetLanguage(cultureCode)/GetResource/TryGetResource, CurrentLanguage/CurrentCultureCode/SupportedLanguages
- **IParameterService**: LoadParametersAsync/SaveParametersAsync
- **IDeviceManager**: 设备管理
- **IAppSettingService**: Load/Save/SaveAsync/GetValue

### 工站任务 (StationTasks/)
- **RecipeStationBase<TParameters>**: 工站基类, 同时实现 IStationParameterProvider + IRecipeDataAccessor + IBatchSwitchable, 构造时自注册到IStationRegistry
- 三个具体工站: LoadingTask, DispensingTask, AssemblyTask
- **IProcessStepAction**: SupportedStepType + ExecuteAsync(step, task, token)

### 步骤动作 (9种)
| 动作 | 功能 | 关键特性 |
|------|------|----------|
| Goto | 运动+跨工站+偏移 | 绝对/回零模式 |
| Vision | TCP触发+解析+全局变量映射 | C#脚本解析, GlobalVariablesChangedEvent |
| Pick | 取料+夹紧+保压+真空检测 | 夹爪+真空 |
| Scan3D | 七步工作流+TCP实时接收 | TCP事件桥接 |
| Script | Natasha动态编译+缓存 | Roslyn编译 |
| Wait | 延时+取消 | CancellationToken |
| Seek | 模拟量+报警联动 | 阈值检测 |
| Dashboard | 公式+弹窗+超时自动确认 | 公式求值 |
| Branch | 纯逻辑, 执行器处理跳转 | 表达式求值+默认动作 |

新增动作: 实现IProcessStepAction + 注册到执行器

### 工站间通信
- **StationInteractionService**: SetSignal/GetSignal/WaitForSignal, AutoResetEvent
- **SignalToStation/WaitForSignal**: 跨工站信号交互

## Prism 模块加载顺序

1. LogViewerModule
2. LanguageModule
3. FrameworkModule
4. AlarmModule
5. MotionControlModule (初始化运动服务, 夹爪, 状态监控)
6. RecipeModule
7. StationTasksModule (解析ITask单例, 触发工站自注册)
8. CoreModule (ModuleCore - 登录/权限/导航)
9. PrimModel (Module - UI控件)
10. TCPIPModule (异步初始化TCP连接)

## 事件总线 (Prism EventAggregator)

### 核心事件
- AxisAlarmEvent — 轴报警
- AxisStateChangedEvent — 轴状态变更
- EmergencyStopAllEvent — 全局急停
- GlobalResetEvent — 全局复位
- RecoverableFaultEvent — 可恢复故障
- StationStateChangedEvent — 工站状态变更
- MotionCompletedEvent — 运动完成
- LanguageChangedEvent — 语言切换
- StationRegisteredEvent / StationUnregisteredEvent — 工站注册/注销
- ProcessStepSequenceEvent — 工艺步骤序列事件(含StationId过滤)
- SystemInitializedEvent — 系统初始化完成
- RecipeChangedEvent / SaveParametersCompletedEvent — 配方变更
- GlobalVariablesChangedEvent — 全局变量外部更新(参数为poolId)
- SaveGlobalVariablesEvent — 全局变量保存完成(无参数)

### 事件使用约定
- 发布: `EA.GetEvent<T>().Publish(...)`
- 订阅使用弱引用, 需及时Unsubscribe
- 轮询线程发布事件, 快照遍历订阅者
- 步骤序列事件支持ControlAction过滤

## 工站自注册机制

StationRegistry使用ConcurrentDictionary实现线程安全活集合。每个工站任务继承RecipeStationBase, 构造函数中调用`_stationRegistry.Register(this)`完成自注册, 并通过StationRegisteredEvent通知其他模块。StationTasksModule.OnInitialized强制解析所有ITask单例以触发注册。

## 系统状态机

```
WAITRESET → WAITRUN → RUNNING → PAUSE/STOP/ESTOP
```
- **急停**: 轮询EStop信号 → EmergencyStopAllEvent → StationTaskManager广播EmergencyStopAllAsync
- **可恢复故障**: RecoverableException(含suggestedAction) → RecoverableFaultEvent → 恢复/暂停/停止
- **安全点动**: SafeJogBehavior(Mouse.Capture + 双重EnsureStop)
- **配置热更新**: EnableSafetyGate/EnableGrating/EnableBuzzer
- **复位条件**: hwcfg.xml中ResetMustOff/On
- **DI轮询**: 20ms周期

## 运动控制安全设计

- 所有运动API接受CancellationToken, 支持急停快速打断
- RecoverableException携带suggestedAction, 指导操作员排查
- ISpeedOverrideService提供运行时速度调节
- VirtualMotionCard支持无硬件调试
- 运动等待循环使用SpinWait.SpinOnce()避免CPU空转
- 全局异常处理覆盖Dispatcher/Domain/TaskScheduler三类

## TCP通信架构

- **ITCPEventService**: 事件桥接协调器, 管理多TcpServer + TcpClient
- **命令路由**: SendCommandAsync(优先Client直连 → Server广播/定向)
- **SendCommandWithResponseAsync**: Client帧协议 / Server事件桥接+超时
- **连接快照**: _connectedSnapshot解决订阅前已连接竞态, ReplayConnectedClients()回放
- **TcpClientImpl**: Raw/Frame两种模式, 5秒连接超时, 自动重连
- **TcpServerImpl**: 并发字典管理客户端, 广播+定向发送

## 视觉处理架构

- **HalconWrapper分层**: VMHWindowControl(控件) → ViewWindow(视图) → HWndCtrl(控制器+鼠标/缩放) → ROIController(ROI生命周期)
- **GraphicsContext**: Hashtable增量应用图形模式(颜色/线宽/填充), 避免重复Halcon调用
- **ROI族**: ROI基类 → ROICircle/ROIRectangle1/ROIRectangle2, 支持Create/Draw/DisplayActive/GetRegion/GetModelData/moveByHandle
- **RoiToolService**: 直线/折线/圆弧/自由手绘等间距采样+移动平均平滑, 输出CAD点序列

## 参数服务体系

- **ParameterGroup**: Category + ObservableCollection<ParameterItem>
- **子类**: String/Boolean/Number(范围/小数位)/Enum/Color/PointFParameterItem
- **TaskParametersBase**: Identifier/ConfigVersion/LastModified/CreateSnapshot深拷贝/Validate
- **存储**: JsonParameterStorage(Config/Parameters目录, camelCase)
- **编辑**: ParameterEditorService(CreateSnapshot → ShowEditorDialog → CopyParameters反射回写)
- **忽略**: ParameterIgnoreAttribute
- **适配**: ParameterEditableAdapter(IRecipeService → IParameterEditable)

## 多语言系统

- 语言资源: MainApp/Languages/Strings.zh-CN.xaml 和 Strings.en-US.xaml
- XAML标记扩展: `{lang:Lang Key=xxx}` (Core/Markup/LangExtension.cs)
- 行为绑定: LocalizationBehavior支持语言切换时自动更新UI
- ILocalizationService负责运行时语言切换和资源查找
- 切换时: 更新线程Culture + 替换MergedDictionaries + 发布LanguageChangedEvent + 保存配置
- LangExtension: 弱引用批量刷新InvalidateAll, 多级查找(Application.Resources → ILocalizationService → 回退[Key])
- 新增UI文本必须添加到两个语言文件

### 多语言Key安全操作规则（进化记录 v1）

**问题复盘**: 清理重复Key时误删了正在使用的Key，导致运行时资源查找失败。

**强制规则**:
1. **删除Key前必须交叉验证引用**: 使用grep/脚本扫描所有XAML和CS文件，确认Key无任何引用后才可删除
2. **重复Key处理策略**:
   - 同值重复：保留首次出现（通常是被引用的），删除后续重复
   - 异值重复：不可直接删除，需确认哪个值被引用，保留被引用的；若两处均被引用则需重命名其中一个
3. **新增Key时先查重**: 添加新Key前先检查是否已存在同名Key，避免制造新的重复
4. **双语同步验证**: 每次修改后运行完整性检查脚本，确保zh-CN和en-US的Key集合完全一致（0重复、0缺失）
5. **Converter硬编码**: Converter中的枚举转文本需通过静态Initialize方法注入ILocalizationService，不可硬编码中文

### `{lang:Lang}` 使用禁区规则（进化记录 v2）

**问题复盘**: OverView页面空白不渲染，根因是`{lang:Lang}`在Style Setter和MultiBinding.Source中使用，导致XAML解析异常，WPF静默吞掉异常后整个UserControl不渲染。

**`{lang:Lang}` 安全使用规则**:
1. **Style Setter 禁区**: `{lang:Lang}` 不能在 `<Setter Value="{lang:Lang Key}"/>` 中使用（包括 Style.Triggers/DataTrigger 中的 Setter），因为 LangExtension.ProvideValue() 返回 BindingExpression，而 Setter.Value 不支持接收 Binding
2. **MultiBinding.Source 禁区**: `{lang:Lang}` 不能作为 `<Binding Source="{lang:Lang Key}"/>` 使用，Source 属性期望对象而非 BindingExpression
3. **ControlTemplate.Triggers Setter 禁区**: 同 Style Setter，ControlTemplate 内的 Setter 也不能使用 `{lang:Lang}`
4. **安全替代方案**:
   - Style Setter 场景 → 改用 ViewModel 绑定属性，在属性 setter 中通过 ILocalizationService 获取文本
   - MultiBinding 场景 → 改用 ViewModel 组合属性（如 `SpeedDisplayText`），在 ViewModel 中拼接格式化字符串
   - DataTrigger 切换文本 → 使用 ViewModel 属性 + PropertyChanged 自动切换
5. **安全使用场景**（可直接使用）:
   - 直接属性赋值: `Text="{lang:Lang Key}"`, `Content="{lang:Lang Key}"`, `ToolTip="{lang:Lang Key}"`
   - 属性元素语法: `<TextBlock.Text><lang:Lang Key="xxx"/></TextBlock.Text>`

### 页面空白快速诊断流程（进化记录 v2）

**诊断步骤**（按优先级排序）:
1. **检查资源文件重复Key**: 使用 .NET 程序扫描 zh-CN/en-US 资源文件，0 重复才通过
2. **检查 Style/Trigger 中的 MarkupExtension**: 搜索 XAML 中 `<Setter` 和 `{lang:Lang}` 的组合使用
3. **检查 MultiBinding 中的 Source**: 搜索 `<Binding Source="{lang:Lang` 模式
4. **检查 StaticResource 引用**: 确认引用的样式名存在且大小写正确
5. **检查 ViewModel 依赖注入**: 确认所有构造函数参数已注册到 DI 容器
6. **查看运行时日志**: 检查 Warn.log 和 Info.log 中的异常信息

## 编码规范

- 架构: WPF + PRISM + MaterialDesignInXAML, 拒绝倒置依赖
- 命名: PascalCase类型/方法, I前缀接口, _前缀私有字段, ViewModel/View/Converter后缀
- MVVM: 继承ViewModelBase(BindableBase+IDestructible), Destroy中清理订阅/定时器
- 命令: DelegateCommand + ObservesProperty
- 绑定: 优先OneWay, FallbackValue不支持嵌套标记扩展
- 多语言: 任何修改均需实现多语言支持
- 图标: 使用`<materialDesign:PackIcon>`, 不使用emoji
- 注释: 方法和关键点添加注释
- 运动控制: 需符合工业设备控制要求(快速响应性, 安全性)
- 版本记录: 生成到net9.0-windows7.0目录
- 日志: LoggerService异步通道, Trace/Debug/Info/Warn/Error/Fatal
- 异常: 覆盖Dispatcher/Domain/TaskScheduler, 生成崩溃转储

## 方法签名与模板

方法签名：`public async Task<bool> SaveAllStationParametersAsync(string poolId, string recipeName)`
方法描述：保存所有工站的配方参数
参数：
- poolId：配方池ID
- recipeName：新配方名称
返回值：是否成功保存所有工站的配方参数
异常：
- 无

	public async Task HomeAsync(int axisId, int mode = 1, double minVel = 5, double maxVel = 20, CancellationToken token = default)
	{
	    var card = GetCardForAxis(axisId);
	    await Task.Run(() =>
	    {
	        card.SetHomeMode(axisId, mode, minVel, maxVel);
	        card.GoHome(axisId);
	        var spinWait = new SpinWait();
	        while (true)
	        {
	            token.ThrowIfCancellationRequested();
	            int homeStatus = card.CheckHomeDone(axisId);
	            if (homeStatus == 1)
	                break; 
	            if (homeStatus < 0)
	            {
	                throw new RecoverableException(
	                    message: $"轴 {axisId} 回原点失败，错误码: {homeStatus}",
	                    suggestedAction: "请检查原点传感器是否正常、回零方向是否正确、未撞限位，复位后重试。"
	                );
	            }
	            spinWait.SpinOnce();
	        }
	        while (true)
	        {
	            token.ThrowIfCancellationRequested();
	            if (card.CheckDone(axisId) == 1)
	                break;
	            spinWait.SpinOnce();
	        }
	    }, token);
	}

### 链接全局变量标准模式（进化记录 v3）

**问题复盘**: 链接图标在未连接变量时仍显示蓝色；offsetY未同步deltaY；取消链接无效；持久化残留旧变量名导致状态不一致。

**标准化实现规则**（所有链接全局变量功能必须遵循）:

#### 1. ViewModel 属性定义（三件套）

每个可链接参数必须包含三个属性：

```csharp
// ① 值属性（实际数值）
private double _needleOffsetX;
public double NeedleOffsetX
{
    get => _needleOffsetX;
    set => SetProperty(ref _needleOffsetX, value);
}

// ② 链接变量名属性（存储链接的全局变量名）
private string _needleOffsetXLinkedVar;
public string NeedleOffsetXLinkedVar
{
    get => _needleOffsetXLinkedVar;
    set
    {
        var normalized = NormalizeLinkedVarName(value);
        if (SetProperty(ref _needleOffsetXLinkedVar, normalized))
        {
            RaisePropertyChanged(nameof(IsNeedleOffsetXLinked));
            if (!string.IsNullOrEmpty(normalized))
            {
                var gv = AvailableGlobalVariables.FirstOrDefault(v =>
                    string.Equals(v.Name, normalized, StringComparison.OrdinalIgnoreCase));
                if (gv != null && double.TryParse(gv.Value, out var val))
                    NeedleOffsetX = val;
            }
            else
            {
                NeedleOffsetX = 0; // 或恢复为默认值
            }
        }
    }
}

// ③ 链接状态属性（必须校验变量名非空 + 变量真实存在于AvailableGlobalVariables）
public bool IsNeedleOffsetXLinked => !string.IsNullOrWhiteSpace(NeedleOffsetXLinkedVar)
    && AvailableGlobalVariables.Any(v => string.Equals(v.Name, NeedleOffsetXLinkedVar, StringComparison.OrdinalIgnoreCase));
```

#### 2. 辅助方法

```csharp
// 规范化变量名：空白→null，保留名→null，否则Trim
private static string NormalizeLinkedVarName(string value)
{
    if (string.IsNullOrWhiteSpace(value)) return null;
    var trimmed = value.Trim();
    if (ReservedVarNames.Contains(trimmed)) return null;
    return trimmed;
}

// 从链接全局变量读取值
private double ReadLinkedVariableValue(string varName)
{
    if (string.IsNullOrEmpty(varName)) return 0;
    var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == varName);
    if (gv != null && double.TryParse(gv.Value, out var val)) return val;
    return 0;
}
```

#### 3. 取消链接命令

```csharp
UnlinkNeedleOffsetXCommand = new DelegateCommand(() => NeedleOffsetXLinkedVar = null);
```

#### 4. 数据流规则（关键！）

| 场景 | 已链接 | 未链接 |
|------|--------|--------|
| **选择变量时** | 从全局变量读取值 → 赋给值属性 | 值属性设为0或默认值 |
| **切换行/站点时** | `ReadLinkedVariableValue(LinkedVar)` | 使用当前行的计算值 |
| **视觉拍照后** | 先`UpdateGlobalVariableValueAsync`更新全局变量，再`ReadLinkedVariableValue`回读 | 直接赋计算值 |
| **加载参数时** | 先设LinkedVar，再从全局变量读值 | 从持久化值读取 |

**强制规则**: 链接状态下，值属性**只能**从链接的全局变量获取，不可从其他来源覆盖。

#### 5. 加载时序保护

```csharp
private bool _isLoadingTransformParams;

// 加载期间LinkedVar的setter仅更新字段，不触发值同步
if (_isLoadingTransformParams)
{
    if (SetProperty(ref backingField, NormalizeLinkedVarName(value)))
        RaisePropertyChanged(isLinkedPropertyName);
    return;
}
```

加载完成后按顺序：设LinkedVar → 从全局变量读值 → RaisePropertyChanged(IsXxxLinked)

#### 6. 刷新可链接变量列表时通知链接状态

```csharp
private void RefreshLinkableGlobalVariables()
{
    var linkable = AvailableGlobalVariables
        .Where(v => !ReservedVarNames.Contains(v.Name))
        .ToList();
    LinkableGlobalVariables = new ObservableCollection<GlobalVariable>(linkable);
    // 必须通知所有IsXxxLinked属性，因为变量可能被删除导致链接状态变化
    RaisePropertyChanged(nameof(IsNeedleOffsetXLinked));
    RaisePropertyChanged(nameof(IsNeedleOffsetYLinked));
    // ... 其他链接状态
}
```

#### 7. XAML 标准布局

```xml
<!-- 资源定义 -->
<converters:BooleanToBrushConverter x:Key="LinkedToBrushConverter"
                                    TrueBrush="#1565C0" FalseBrush="#BDBDBD" />

<!-- 链接图标按钮：Foreground绑定IsXxxLinked，蓝色=已链接，灰色=未链接 -->
<Button Command="{Binding UnlinkNeedleOffsetXCommand}"
        Style="{StaticResource MaterialDesignIconButton}" Padding="0" Width="16" Height="16"
        Foreground="{Binding IsNeedleOffsetXLinked, Converter={StaticResource LinkedToBrushConverter}}"
        ToolTip="{lang:Lang VisionCapture_UnlinkGlobalVariable}">
    <materialDesign:PackIcon Kind="LinkOff" Width="10" Height="10" />
</Button>

<!-- 变量选择下拉框 -->
<ComboBox ItemsSource="{Binding LinkableGlobalVariables}"
          SelectedValuePath="Name" DisplayMemberPath="Name"
          SelectedValue="{Binding NeedleOffsetXLinkedVar, UpdateSourceTrigger=LostFocus}"
          Width="80" FontSize="8"
          materialDesign:HintAssist.Hint="{lang:Lang VisionCapture_LinkVariable}" />
```

#### 8. 持久化变量命名规范

- 值持久化: `NeedleOffsetX`, `CameraNeedleDistanceX`
- 链接名持久化: `NeedleOffsetX_LinkedVar`, `CameraNeedleDistanceX_LinkedVar`
- 保留名集合: 值名 + `_LinkedVar` 后缀名，这些变量不出现在链接下拉框中

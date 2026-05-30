# Tasks — DotPointEditorView 细化实现

## 阶段一：数据模型层

- [x] **T1: 重构 DotPoint 数据模型（Module/Models/DotPoint.cs）**
  - [x] T1.1 移除旧字段 SubAssy/Offset，重命名 AssyGroup→Group、SiteId→PointId、X→Dx、Y→Dy、Z→Dz2
  - [x] T1.2 新增字段：Dz3(double)、Rx(double)、Ry(double)、Dz2Compensation(double)、Dz3Compensation(double)、IsEnabled(bool)
  - [x] T1.3 新增只读计算属性：EffectiveDz2(=Dz2+Dz2Compensation)、EffectiveDz3(=Dz3+Dz3Compensation)
  - [x] T1.4 所有数值属性添加范围校验（SetProperty 时 Math.Clamp）

- [x] **T2: 创建 DotProcessParams 工艺参数模型（Core/Models/DotProcessParams.cs）**
  - [x] T2.1 创建运动参数属性：MoveSpeed(0.1~50)、SafeHeight(0~200)、ApproachHeight(0~50)、CornerDecel(0~1)
  - [x] T2.2 创建出胶参数属性：DispenseTime(10~5000 ms)、PreDelay(0~5000 ms)、PostDelay(0~5000 ms)、GlueTriggerOffsetMm(0.05~5.0)
  - [x] T2.3 创建阀控参数属性：DispensingPressure(0.1~1.0)、SuckBackTime(10~500)
  - [x] T2.4 创建高度参数属性：TeachHeight(-200~200)、HeightCompensation(-50~50)、EffectiveZHeight(只读)
  - [x] T2.5 添加 JSON 序列化支持（System.Text.Json）

## 阶段二：点胶执行服务层

- [x] **T3: 创建 IDotDispenseService 接口（Module/Services/IDotDispenseService.cs）**
  - [x] T3.1 定义接口方法：
    - `Task DryRunAsync(IEnumerable<DotPoint> points, DotProcessParams params, CancellationToken token)`
    - `Task ExecuteDotDispenseAsync(IEnumerable<DotPoint> points, DotProcessParams params, CancellationToken token)`
    - `Task TeachPointAsync(DotPoint point, CancellationToken token)`
    - `event Action<string, int, int> ProgressChanged`
    - `event Action<string> StatusChanged`
    - `bool IsRunning { get; }`

- [x] **T4: 实现 DotDispenseService（Module/Services/DotDispenseService.cs）**
  - [x] T4.1 注入 IMotionService、ILoggerService
  - [x] T4.2 实现 DryRunAsync：遍历选中点，Z抬升→XY定位→保持在安全高度→Z抬升，不出胶
  - [x] T4.3 实现 ExecuteDotDispenseAsync：遍历选中点，按行业标准流程执行
    - 全部勾选时：先统一抬升Z到安全高度，再逐点执行 XY定位→Z下降(两段式)→开胶前延时→开胶→出胶时间等待→关胶→关胶后延时→Z抬升
    - 部分勾选时：逐点执行 Z抬升→XY定位→Z下降→出胶→Z抬升
  - [x] T4.4 实现 TeachPointAsync：读取 IMotionService.GetAxisPosition 填入 Dx/Dy/Dz2
  - [x] T4.5 异常处理：OperationCanceledException 时安全关胶，其他异常时安全关胶并上报
  - [x] T4.6 发布 ProgressChanged/StatusChanged 事件

## 阶段三：ViewModel 重构

- [x] **T5: 重构 DotPointEditorViewModel（Module/WorkStation/Dispense/DotPointEditorViewModel.cs）**
  - [x] T5.1 注入 IDotDispenseService、IDialogService、IWorkOrderService（或 WorkOrderData 引用）
  - [x] T5.2 管理 DotProcessParams 实例（ProcessParams 属性，支持 UI 双向绑定）
  - [x] T5.3 管理 ObservableCollection<DotPoint> Points 集合
  - [x] T5.4 实现 Group 列表：从 WorkOrderData.Sites 获取 SiteFeatureType.AssyGroup 类型的条目，回退到默认列表
  - [x] T5.5 实现命令：AddPoint / DeleteSelected / SelectAll / DeselectAll / TeachPoint
  - [x] T5.6 实现命令：ApplyProcessParams（将当前工艺参数应用到选中点）
  - [x] T5.7 实现命令：DryRun / ExecuteDotDispense / StopExecution
  - [x] T5.8 实现命令：SaveData / LoadData（JSON 序列化/反序列化）
  - [x] T5.9 实现进度和状态属性：Status / ProgressText / IsExecuting
  - [x] T5.10 实现 Group 筛选：SelectedGroupFilter 属性 + FilteredPoints 计算属性

## 阶段四：UI 重构

- [x] **T6: 重构 DotPointEditorView.xaml（Module/WorkStation/Dispense/DotPointEditorView.xaml）**
  - [x] T6.1 整体布局：Grid 三行（上工艺参数 / 中点位数据 / 下执行控制）
  - [x] T6.2 工艺参数面板：三列卡片布局（运动参数 / 出胶参数 / 高度参数），底部操作按钮
  - [x] T6.3 点位数据区：Group 筛选 ComboBox + 工具栏按钮 + DataGrid
  - [x] T6.4 DataGrid 列按指定顺序：☑(IsSelected) → Group(ComboBox) → ID → Dx → Dy → Dz₂ → Dz₃ → Rx → Ry → Dz₂补偿 → Dz₃补偿 → 示教(Button)
  - [x] T6.5 执行控制区：空跑按钮 + 真实点胶按钮 + 停止按钮 + 进度条 + 状态指示
  - [x] T6.6 使用 MaterialDesign 样式，保持与项目整体风格一致

## 阶段五：DI 注册与集成

- [x] **T7: DI 注册与 DispensingView 集成**
  - [x] T7.1 在 PrimModel.cs 中注册 IDotDispenseService → DotDispenseService
  - [x] T7.2 在 DispensingView.xaml 中取消注释 DotPointEditorView 引用
  - [x] T7.3 编译验证：全项目编译通过，零 error（仅预先存在的 CS1704 程序集重复引用警告）

# Task Dependencies

- [T2] depends on [T1] — DotProcessParams 可能引用 DotPoint（但实际独立，可并行）
- [T3] depends on [T1] — 接口使用 DotPoint 和 DotProcessParams
- [T4] depends on [T1, T2, T3] — 实现使用 DotPoint、DotProcessParams、接口定义
- [T5] depends on [T1, T2, T3] — ViewModel 使用所有模型和服务接口
- [T6] depends on [T5] — UI 绑定 ViewModel 的属性和命令
- [T7] depends on [T4, T6] — 集成需要服务和 UI 都就绪

**可并行执行的独立任务组**：
- Group A (无依赖): T1, T2 可并行
- Group B (依赖 A): T3, T4 可顺序执行
- Group C (依赖 A+B): T5
- Group D (依赖 C): T6
- Group E (依赖 B+D): T7

# Tasks

- [x] Task 1: Machine Coordinates 条件显示
  - [x] SubTask 1.1: VisionCaptureViewModel 添加 HasMachinePoints 计算属性（MachinePoints.Count > 0）
  - [x] SubTask 1.2: VisionCaptureView.xaml 中 Machine Coordinates 区域绑定 Visibility 到 HasMachinePoints（BoolToVisibilityConverter）

- [x] Task 2: 删除 Coordinate Transform 卡片
  - [x] SubTask 2.1: VisionCaptureView.xaml 删除 Coordinate Transform 卡片（约490-543行）
  - [x] SubTask 2.2: 确认 SaveTransformParamsCommand 保留（通过 Transform Details 区域的保存按钮触发）

- [x] Task 3: Transform Details 预览坐标同步视觉数据
  - [x] SubTask 3.1: VisionCaptureViewModel.PreviewMachinePointsAsync 确保使用最新视觉返回数据计算各步骤数值
  - [x] SubTask 3.2: 参数变更时通过链接全局变量自动更新值

- [x] Task 4: NeedleOffset/NeedleComp 支持链接全局变量
  - [x] SubTask 4.1: VisionCaptureViewModel 添加 NeedleOffsetXLinkedVar、NeedleOffsetYLinkedVar、NeedleCompXLinkedVar、NeedleCompYLinkedVar 属性
  - [x] SubTask 4.2: 添加 IsNeedleOffsetXLinked 等布尔属性控制链接状态
  - [x] SubTask 4.3: 添加 AvailableGlobalVariables 集合（从 IRecipePoolService 加载）
  - [x] SubTask 4.4: 链接时从全局变量读取值，取消链接时保留当前值
  - [x] SubTask 4.5: SaveTransformParamsAsync 保存链接关系（参数名→全局变量名）
  - [x] SubTask 4.6: LoadTransformParamsAsync 加载时恢复链接关系
  - [x] SubTask 4.7: VisionCaptureView.xaml Transform Details 步骤③④添加可编辑TextBox和全局变量下拉

- [x] Task 5: Dot 模式操作按钮重构
  - [x] SubTask 5.1: VisionCaptureViewModel 添加 IsPaused 状态属性和 CanXxx 计算属性
  - [x] SubTask 5.2: 添加 StopCommand（取消 CancellationTokenSource）
  - [x] SubTask 5.3: VisionCaptureView.xaml Dot 模式区域添加【执行点胶】【停止】【预览坐标】按钮
  - [x] SubTask 5.4: 按钮可用性绑定到 CanXxx 状态

- [x] Task 6: Arc 模式操作按钮重构（含暂停/继续）
  - [x] SubTask 6.1: VisionCaptureViewModel 添加 PauseCommand、ResumeCommand
  - [x] SubTask 6.2: BezierArcDispenseService.ExecuteArcDispenseAsync 支持暂停点（每段插补间检查暂停信号 ManualResetEventSlim）
  - [x] SubTask 6.3: VisionCaptureView.xaml Arc 模式区域添加【执行点胶】【暂停】【继续】【停止】【预览坐标】按钮
  - [x] SubTask 6.4: 按钮可用性绑定到 IsExecuting/IsPaused 状态

- [x] Task 7: 编译验证
  - [x] SubTask 7.1: dotnet build 全解决方案无 CS 错误

# Task Dependencies
- [Task 2] depends on [Task 4] (删除 Coordinate Transform 前需确保参数编辑功能已迁移到 Transform Details)
- [Task 3] depends on [Task 4] (预览同步需使用链接后的参数值)
- [Task 5] depends on [Task 1] (按钮重构可独立进行)
- [Task 6] depends on [Task 5] (Arc 模式在 Dot 模式基础上增加暂停/继续)

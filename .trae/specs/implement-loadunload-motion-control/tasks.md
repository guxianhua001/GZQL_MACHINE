# Tasks

- [x] Task 1: 定义 ILoadUnloadController 接口（Core.Abstractions）
  - [x] 1.1 创建 ILoadUnloadController.cs，包含真空控制、轴定位、夹爪操作、自动流程、状态查询、停止等方法签名
  - [x] 1.2 定义 VacuumStatus 枚举（On/Off/Checking/Unknown）
  - [x] 1.3 定义 LoadUnloadMotionStatus 类（AxisReady + Positions + Vacuum + Gripper 聚合状态）

- [x] Task 2: 实现 LoadUnloadControllerImpl（MotionControl.Services）
  - [x] 2.1 创建 LoadUnloadControllerImpl.cs，注入 IStationRegistry + IMotionService + IGripperService + ISystemStateService + IAxisConfigurationService + ILoggerService
  - [x] 2.2 实现 ResolveLoadingTask() 私有方法 — 从 IStationRegistry 查找 LoadingTask 实例
  - [x] 2.3 实现真空控制方法 — 通过 LoadingTask.ExecuteManualProcess + WriteDO/ReadDI
  - [x] 2.4 实现轴定位方法 — 通过 LoadingTask.ExecuteMoveAsync/MoveToAsync
  - [x] 2.5 实现夹爪操作方法 — 通过 IGripperService
  - [x] 2.6 实现自动流程方法 — 组合上述原子操作为完整序列
  - [x] 2.7 实现状态查询方法 — 通过 IMotionService 读取轴状态/位置
  - [x] 2.8 实现 CanExecuteMotion/StopMotion
  - [x] 2.9 在 MotionControlModule.cs 注册 Singleton

- [x] Task 3: 编写 LoadUnloadControllerImpl 单元测试（TDD RED→GREEN）
  - [x] 3.1 创建测试文件 LoadUnloadControllerTests.cs
  - [x] 3.2 T1: ChuckVacuumOnAsync_正常调用写入DO并等待DI
  - [x] 3.3 T2: ChuckVacuumOffAsync_正常调用写入DO
  - [x] 3.4 T3: MoveToPickPositionAsync_调用ExecuteMoveAsync
  - [x] 3.5 T4: HomeAllAsync_依次回零三轴
  - [x] 3.6 T5: ClampAsync_调用GripperService
  - [x] 3.7 T6: AutoPickUpAsync_按序执行完整流程
  - [x] 3.8 T7: CanExecuteMotion_系统运行时返回false
  - [x] 3.9 T8: GetRealTimePositionsAsync_返回轴位置字典

- [x] Task 4: 改造 LoadUnloadViewModel
  - [x] 4.1 注入 ILoadUnloadController + ILocalizationService
  - [x] 4.2 添加 IsMoving 属性
  - [x] 4.3 重写所有 Action 方法，调用 ILoadUnloadController 对应方法
  - [x] 4.4 更新 ExecuteAsyncOperation 辅助方法，增加 IsMoving 状态管理
  - [x] 4.5 更新 UpdateRealTimeStatus 定时器回调，调用 controller 状态查询方法
  - [x] 4.6 替换硬编码字符串为 ILocalizationService.GetResource()
  - [x] 4.7 更新 CanExecute 逻辑，结合 IsMoving + CanExecuteMotion

- [x] Task 5: UI 微调
  - [x] 5.1 操作按钮绑定 IsMoving 禁用逻辑
  - [x] 5.2 确认 Ry 轴状态绑定正确

- [x] Task 6: 多语言资源补充
  - [x] 6.1 Strings.zh-CN.xaml 添加 LoadUnload 运动操作相关资源键
  - [x] 6.2 Strings.en-US.xaml 添加对应英文翻译

- [x] Task 7: 编译验证 + 回归测试
  - [x] 7.1 全量编译确认 0 错误
  - [x] 7.2 运行 MotionControl.Tests 全部通过 (24/24)
  - [x] 7.3 确认 Module 项目无直接引用 MotionControl（仅通过 Core.Abstractions）

# Task Dependencies
- [Task 2] depends on [Task 1]
- [Task 3] depends on [Task 1]
- [Task 4] depends on [Task 1, Task 2]
- [Task 5] depends on [Task 4]
- [Task 6] depends on [Task 4]
- [Task 7] depends on [Task 2, Task 3, Task 4, Task 5, Task 6]

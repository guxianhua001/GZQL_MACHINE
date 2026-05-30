# LoadUnloadView 运动控制功能完善 Spec

## Why
当前 LoadUnloadViewModel 的所有动作方法（真空控制、轴定位、夹爪操作、自动流程）均为空壳实现，无法驱动真实硬件。需参考旧项目 LoadingStationFuncition.cs 的完整逻辑，基于现有运动框架（StationTaskBase + IMotionService + IGripperService）实现全部功能，确保动作执行不卡顿、异常可恢复。

## What Changes
- 新增 `Core.Abstractions.ILoadUnloadController` 接口 — 上下料工站运动控制抽象
- 新增 `MotionControl.Services.LoadUnloadControllerImpl` 实现 — 委托 LoadingTask 执行安全操作
- 重写 `Module.ViewModels.LoadUnloadViewModel` — 接入 ILoadUnloadController，替换所有空壳方法
- 微调 `Module.Views.LoadUnloadView.xaml` — 绑定 IsMoving 等新增属性
- 多语言资源补充

## Impact
- Affected specs: LoadUnloadView 运动控制能力
- Affected code: Core.Abstractions, MotionControl, Module (LoadUnloadViewModel/View), StationTasks (LoadingTask)

---

## ADDED Requirements

### Requirement: ILoadUnloadController 抽象接口
系统 SHALL 在 Core.Abstractions 层提供 ILoadUnloadController 接口，供 Module 层 ViewModel 依赖，不直接引用 MotionControl。

#### Scenario: 真空控制
- **WHEN** 调用 ChuckVacuumOnAsync()
- **THEN** 载台真空阀打开、破真空阀关闭，并等待 DI 反馈确认真空建立
- **WHEN** 调用 ChuckVacuumOffAsync()
- **THEN** 载台真空阀关闭、破真空阀打开，延时后破真空阀关闭

#### Scenario: 夹爪真空控制
- **WHEN** 调用 GripperVacuumOnAsync()
- **THEN** 夹爪真空阀打开
- **WHEN** 调用 GripperVacuumOffAsync()
- **THEN** 夹爪真空阀关闭

#### Scenario: 轴定位操作
- **WHEN** 调用 MoveToPickPositionAsync()
- **THEN** Y 轴移动到配方"取料位"
- **WHEN** 调用 MoveToScanPositionAsync()
- **THEN** Y 轴移动到配方"3D扫描位"
- **WHEN** 调用 MoveToUnloadPositionAsync()
- **THEN** Y 轴移动到配方"出料位"
- **WHEN** 调用 MoveToAssemblyPositionAsync(int siteIndex)
- **THEN** U+R 轴联动移动到配方"装配位N"
- **WHEN** 调用 HomeAllAsync()
- **THEN** Y/Rx/Rz 轴依次回原点后移动到待机位

#### Scenario: 夹爪操作
- **WHEN** 调用 ClampAsync()
- **THEN** 通过 IGripperService.ClampAsync() 夹紧
- **WHEN** 调用 ReleaseAsync()
- **THEN** 通过 IGripperService.ReleaseAsync() 释放
- **WHEN** 调用 MoveGripperToAngleAsync()
- **THEN** 夹爪移动到预设夹持角度

#### Scenario: 自动流程
- **WHEN** 调用 AutoPickUpAsync()
- **THEN** 按序执行：真空开 → 移动到取料位 → 真空检测 → Y轴升高 → 旋转到装配角度
- **WHEN** 调用 AutoScanAsync()
- **THEN** 按序执行：移动到3D扫描位 → 等待扫描完成 → Y轴升高
- **WHEN** 调用 AutoUnloadAsync()
- **THEN** 按序执行：移动到出料位 → 真空关 → Y轴升高 → 回待机位

#### Scenario: 状态查询
- **WHEN** 调用 GetAxisReadyStatusAsync()
- **THEN** 返回各轴回零状态字典 { "Y": bool, "Rx": bool, "Rz": bool, "Ry": bool }
- **WHEN** 调用 GetRealTimePositionsAsync()
- **THEN** 返回各轴当前位置字典 { "Y": double, "Rx": double, "Rz": double, "Ry": double }
- **WHEN** 调用 GetVacuumStatus()
- **THEN** 返回载台真空状态 (On/Off/Checking)
- **WHEN** 调用 GetGripperVacuumStatus()
- **THEN** 返回夹爪真空状态 (On/Off)
- **WHEN** 调用 GetGripperState()
- **THEN** 返回夹爪当前 GripperState
- **WHEN** 调用 CanExecuteMotion()
- **THEN** 系统非运行/急停状态时返回 true

#### Scenario: 停止操作
- **WHEN** 调用 StopMotion()
- **THEN** 停止 LoadingTask 所有轴运动

### Requirement: LoadUnloadControllerImpl 实现
系统 SHALL 在 MotionControl 层提供 ILoadUnloadController 的实现类，通过 LoadingTask.ExecuteManualProcess 安全执行所有操作。

#### Scenario: 安全保护
- **WHEN** 执行任何运动操作
- **THEN** 通过 LoadingTask.ExecuteManualProcess 包装，享受 RunStep 的暂停/急停/单步/可恢复异常保护
- **WHEN** LoadingTask 处于 Running 状态
- **THEN** ExecuteManualProcess 拒绝执行，返回 false

#### Scenario: IO 端口映射
- **WHEN** 需要控制真空/破真空
- **THEN** 通过 IMotionService.WriteDo() 写入对应 DO 端口
- **WHEN** 需要检测真空传感器
- **THEN** 通过 IMotionService.ReadDi() 读取对应 DI 端口

### Requirement: LoadUnloadViewModel 改造
系统 SHALL 重写 LoadUnloadViewModel，注入 ILoadUnloadController 替代空壳方法。

#### Scenario: 命令执行不卡顿
- **WHEN** 用户点击任何操作按钮
- **THEN** 命令在后台线程异步执行，UI 线程不阻塞
- **WHEN** 操作正在执行时
- **THEN** IsMoving=true，同类型按钮自动禁用（CanExecute=false）

#### Scenario: 异常处理
- **WHEN** 运动操作抛出异常
- **THEN** 捕获异常，通过 DialogService 显示错误信息，StepStatusList 记录失败步骤
- **WHEN** 异常为 RecoverableException
- **THEN** 系统自动暂停，等待操作员恢复

#### Scenario: 实时状态刷新
- **WHEN** DispatcherTimer 每 500ms 触发
- **THEN** 更新轴就绪状态、实时位置、真空状态、夹爪状态、流程状态

#### Scenario: 步骤状态追踪
- **WHEN** 任何操作步骤开始/完成
- **THEN** StepStatusList 添加/更新对应条目，显示时间戳和描述

### Requirement: UI 微调
系统 SHALL 在 LoadUnloadView.xaml 中绑定新增属性。

#### Scenario: IsMoving 绑定
- **WHEN** IsMoving=true
- **THEN** 操作按钮显示禁用态

#### Scenario: Ry 轴状态
- **WHEN** Ry 轴状态变化
- **THEN** 左侧面板 Ry 轴指示灯实时更新（当前已有 UI，需确保数据绑定正确）

### Requirement: 多语言支持
系统 SHALL 为新增的运动操作提示文本提供中英文资源。

#### Scenario: 资源键覆盖
- **THEN** 所有用户可见的操作提示文本均使用 ILocalizationService.GetResource() 获取

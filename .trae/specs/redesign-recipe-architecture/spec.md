# 配方系统架构重设计 Spec

## Why
当前配方系统存在三个核心问题：(1) 多工站同时保存参数时并发竞争机制过于复杂，SemaphoreSlim + 双写策略 + 不完整的 SaveParametersCoordinator 导致维护困难；(2) 工站任务使用配方需要大量样板代码（创建 IRecipeService、订阅事件、实现 IStationParameterProvider 等），不熟悉项目的人难以使用；(3) RecipeInfo 模型包含与工业配方无关的字段（Ingredients、RecipeStep），RecipeManager 使用 .Result/.Wait() 可能死锁，存在死代码（RecipeController、RecipeBackgroundService）。

## What Changes
- **新增 `RecipeStationBase<TParams>` 基类**：封装所有配方相关样板代码，工站任务继承后即可零代码使用配方数据
- **新增 `IRecipeDataAccessor<TParams>` 接口**：极简配方数据访问接口，提供 `Params` 属性和 `SaveAsync()` 方法
- **重设计保存架构**：引入"暂存区 + 原子提交"模式替代当前的双写 + 信号量竞争机制
- **精简 RecipeInfo 模型**：移除 Ingredients/RecipeStep 等无关字段，保留工业配方所需的核心字段
- **修复 RecipeManager**：将同步方法改为真正的异步方法
- **移除死代码**：删除 RecipeController、RecipeBackgroundService、SaveParametersCoordinator
- **重构 RecipePoolService**：移除 static AsyncLocal 标志，改用实例级批量操作上下文
- **新增 `RecipePoolSaveContext`**：封装批量保存的原子操作，避免多工站同时保存竞争

## Impact
- Affected specs: 配方池管理、工站参数保存、配方切换
- Affected code:
  - `RecipeManagement/` 整个项目的模型、接口、服务层
  - `Stations/LoadingStation.cs`、`Stations/DispenserStation.cs`、`Stations/AssemblyStation.cs`
  - `Stations/RecipeServiceFactory.cs`
  - `Core/Abstraction/IStationParameterProvider.cs`
  - `Core/Abstraction/Parameters/TaskParametersBase.cs`

## ADDED Requirements

### Requirement: RecipeStationBase 零代码配方基类
系统 SHALL 提供 `RecipeStationBase<TParams>` 抽象基类，继承自 `XTaskBase<TParams>` 并实现 `IStationParameterProvider`，封装所有配方交互逻辑。

#### Scenario: 工站零代码使用配方
- **WHEN** 开发者创建新的工站任务类，继承 `RecipeStationBase<TParams>`
- **THEN** 该工站自动获得配方加载、参数编辑、配方切换能力，无需编写任何配方相关代码
- **AND** 工站通过 `this.Params` 属性直接访问强类型参数

#### Scenario: 工站访问配方参数
- **WHEN** 工站任务运行中需要读取当前配方参数
- **THEN** 通过 `this.Params` 直接获取强类型参数对象，类型为 `TParams`
- **AND** 参数对象始终与配方系统保持同步

### Requirement: IRecipeDataAccessor 极简配方访问接口
系统 SHALL 提供 `IRecipeDataAccessor<TParams>` 泛型接口，作为工站访问配方数据的唯一入口。

#### Scenario: 通过接口获取参数
- **WHEN** 工站通过 `IRecipeDataAccessor<TParams>` 访问配方数据
- **THEN** `Params` 属性返回当前强类型参数
- **AND** `CurrentRecipeName` 返回当前配方名称
- **AND** `CurrentPoolName` 返回当前配方池名称

#### Scenario: 通过接口保存参数
- **WHEN** 工站调用 `SaveAsync()` 保存当前参数
- **THEN** 参数被暂存到内存暂存区，等待池级保存时统一提交

### Requirement: 暂存区 + 原子提交保存架构
系统 SHALL 采用"暂存区 + 原子提交"模式管理参数保存，替代当前的双写 + 信号量竞争机制。

#### Scenario: 单工站参数修改
- **WHEN** 用户修改某个工站的参数并点击保存
- **THEN** 修改后的参数被写入内存暂存区（StagingArea），标记为"脏"
- **AND** 本地文件同步更新（保证数据安全）
- **AND** 配方池 JSON 文件不立即更新

#### Scenario: 点击"保存池"统一提交
- **WHEN** 用户点击"保存池"按钮
- **THEN** 系统从暂存区收集所有标记为"脏"的工站参数
- **AND** 一次性原子写入配方池 JSON 文件
- **AND** 写入完成后清除所有"脏"标记
- **AND** 如果写入失败，暂存区数据保留，可重试

#### Scenario: 多工站同时修改参数
- **WHEN** 多个工站同时修改各自的参数
- **THEN** 各工站独立写入各自的暂存区条目，互不干扰
- **AND** 无需信号量或锁机制协调各工站的写入操作
- **AND** "保存池"时按工站标识符顺序依次写入配方池

### Requirement: RecipePoolSaveContext 批量保存上下文
系统 SHALL 提供 `RecipePoolSaveContext` 类，封装批量保存操作的完整生命周期。

#### Scenario: 创建保存上下文
- **WHEN** 系统需要执行批量保存
- **THEN** 创建 `RecipePoolSaveContext` 实例，指定目标配方池和配方名称
- **AND** 上下文内部维护待保存工站参数的有序字典

#### Scenario: 添加工站参数到保存上下文
- **WHEN** 调用 `AddStation(stationIdentifier, parameters)` 方法
- **THEN** 参数被添加到上下文的有序字典中
- **AND** 如果同一工站重复添加，后添加的参数覆盖先前的

#### Scenario: 提交保存
- **WHEN** 调用 `CommitAsync()` 方法
- **THEN** 系统加载配方池，批量设置所有工站参数，保存配方池
- **AND** 整个操作在单次文件写入中完成
- **AND** 操作完成后发布 `SaveParametersCompletedEvent`

### Requirement: 精简 RecipeInfo 模型
系统 SHALL 精简 `RecipeInfo` 模型，移除与工业配方无关的字段。

#### Scenario: RecipeInfo 核心字段
- **WHEN** 创建或序列化 `RecipeInfo` 对象
- **THEN** 仅包含以下字段：Id、Name、Description、CreatedTime、ModifiedTime、Category、Version、Tags、Author
- **AND** 通过 `[JsonExtensionData]` 的 Parameters 字典存储各工站参数
- **AND** 不再包含 Ingredients、RecipeStep、Rating、Difficulty、EstimatedTime 字段

### Requirement: RecipeManager 异步化
系统 SHALL 将 `RecipeManager` 的所有同步方法改为真正的异步方法。

#### Scenario: 异步获取配方池
- **WHEN** 调用 `GetAllRecipePoolsAsync()`
- **THEN** 方法返回 `Task<IEnumerable<RecipePool>>`，不使用 `.Result` 或 `.Wait()`

#### Scenario: 异步保存配方池
- **WHEN** 调用 `SaveRecipePoolAsync()`
- **THEN** 方法返回 `Task<bool>`，不使用 `.Wait()`

## MODIFIED Requirements

### Requirement: IStationParameterProvider 接口简化
原接口包含 `LoadRecipeAsync`、`SwitchToRecipeAsync`、`EditParameters` 等方法。修改后仅保留数据暴露职责，操作方法移至 `RecipeStationBase`。

修改后接口：
```csharp
public interface IStationParameterProvider
{
    string StationIdentifier { get; }
    string CurrentPoolName { get; }
    string CurrentRecipeName { get; }
    object CurrentParameters { get; }
    bool HasUnsavedChanges { get; }
}
```

### Requirement: IRecipePoolService 保存方法重构
原 `SaveStationParametersAsync` 和 `SaveAllStationParametersAsync` 方法直接操作配方池文件。修改后改为操作暂存区。

修改后的核心方法：
- `StageStationParameters(stationIdentifier, parameters)` - 暂存工站参数
- `CommitStagedParametersAsync(poolId, recipeName)` - 提交暂存区参数到配方池
- `SaveAllStationParametersAsync(poolId, recipeName)` - 保留兼容，内部改为：收集所有工站参数 → 暂存 → 提交

### Requirement: RecipePoolService 批量切换重构
原批量切换使用 static `AsyncLocal<bool>` 标志。修改为使用实例级 `BatchSwitchContext` 对象。

修改后：
- `SwitchAllStationsAsync` 方法接受 `BatchSwitchContext` 参数
- `BatchSwitchContext` 包含目标配方名称、池ID、是否显示确认对话框
- 不再使用 static AsyncLocal 字段

## REMOVED Requirements

### Requirement: RecipeController (ASP.NET Core 控制器)
**Reason**: 当前应用为 WPF 桌面应用，不使用 ASP.NET Core Web API。该控制器从未被实际使用。
**Migration**: 如果未来需要远程配方管理 API，应基于独立的 Web 服务项目重新实现。

### Requirement: RecipeBackgroundService (ASP.NET Core 后台服务)
**Reason**: 同上，ASP.NET Core 后台服务不适用于 WPF 应用。定时备份功能应在 WPF 应用内通过 DispatcherTimer 或 Task.Delay 循环实现。
**Migration**: 如需定时备份，在 RecipePoolService 中添加 `StartPeriodicBackup` 方法。

### Requirement: SaveParametersCoordinator
**Reason**: 该类设计不完整（仅收到第一个完成事件就认为全部完成），且与新的暂存区架构不兼容。批量保存的协调逻辑已由 `RecipePoolSaveContext` 替代。
**Migration**: 无需迁移，功能已被 `RecipePoolSaveContext` 完全替代。

### Requirement: RecipeInfo 中的 Ingredients/RecipeStep/Rating/Difficulty/EstimatedTime 字段
**Reason**: 这些字段属于烹饪配方领域，与工业自动化设备的配方管理无关，增加了序列化开销和认知负担。
**Migration**: 如有自定义元数据需求，可通过 RecipeInfo 的 Tags 字段或 ExtensionData 实现。

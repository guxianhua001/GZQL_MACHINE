# 轴配置动态化重构计划（修订版）

## 问题分析

### 当前状态

**hwcfg.xml 中的轴-任务映射（真实数据源）：**

| setAxisId | name | taskId | 对应任务 |
|-----------|------|--------|---------|
| 0 | Z | 3 | AssemblyTask |
| 1 | Ry | 3 | AssemblyTask |
| 2 | Dz₁ | 2 | DispensingTask |
| 3 | Dz₂ | 2 | DispensingTask |
| 4 | Dz3 | 2 | DispensingTask |
| 5 | X | 3 | AssemblyTask |
| 6 | Dy | 2 | DispensingTask |
| 7 | 轴8_点胶工位Y轴Y1-2 | 2 | DispensingTask |
| 8 | Dx | 2 | DispensingTask |
| 9 | Y | 1 | LoadingTask |
| 10 | Rx | 1 | LoadingTask |
| 11 | Rz | 1 | LoadingTask |
| 12 | Ey | 3 | AssemblyTask |
| 13 | Cy | 3 | AssemblyTask |

**AxisConfigurationService 中的硬编码映射（错误数据）：**

| 工站 | 当前配置 | 应该是 |
|------|---------|--------|
| LoadingStation | Rx, Rz, Y | Y, Rx, Rz |
| AssemblyStation | X, **Y**, Z, Ry | X, Z, Ry, Ey, Cy |
| DispenserStation | Dx, Dy, Dz1, Dz2, Dz3 | Dx, Dy, Dz₁, Dz₂, Dz3 |

**关键发现：hwcfg.xml 中的 `taskId` 字段在运行时从未被使用！**
- `AxisConfig.TaskId` 被 HardwareConfigParser 解析并存储，但 MotionService.BuildMappings 完全忽略它
- `TaskConfig.TaskId` 同样被解析但从未被消费
- 轴-任务绑定完全通过各任务类中的硬编码 `const int` 常量实现
- `AxisConfigurationService` 使用独立的硬编码字典，与 hwcfg.xml 完全脱节

### 目标映射

| 工站 | 轴名 | 逻辑ID | 描述 |
|------|------|--------|------|
| LoadingTask | Y | 9 | Stage translation |
| LoadingTask | Rx | 10 | Stage tilt |
| LoadingTask | Rz | 11 | Stage rotation |
| AssemblyTask | X | 5 | Gripper lateral |
| AssemblyTask | Z | 0 | Gripper height/plunge |
| AssemblyTask | Ry | 1 | Gripper pitch |
| AssemblyTask | Ey | 12 | Slot adjustment |
| AssemblyTask | Cy | 13 | Side Camera |
| DispensingTask | Dx | 8 | Dispenser gantry X |
| DispensingTask | Dy | 6 | Dispenser gantry Y |
| DispensingTask | Dz₁ | 2 | Dispenser head 1 Z |
| DispensingTask | Dz₂ | 3 | Dispenser head 2 Z |
| DispensingTask | Dz3 | 4 | Dispenser head 3 Z |

---

## 实施步骤

### Step 1: 将 AxisConfigurationService 从 Core 迁移到 MotionControl 并重构

**用户要求：** 选择方案 b) 将 AxisConfigurationService 迁移至 MotionControl 模块，通过 HardwareConfigParser/MotionService 实现轴配置功能。

**理由：**
- Core 模块不应依赖硬件配置细节
- MotionControl 模块已有 `MotionSystemConfig`、`AxisConfig`、`HardwareConfigParser` 等硬件配置基础设施
- 迁移后可直接访问 `MotionService` 缓存的配置数据，无需额外接口

**具体操作：**

1. **从 Core/Services/AxisConfigurationService.cs 删除** `AxisConfigurationService` 类和 `IAxisConfigurationService` 接口

2. **在 MotionControl/Services/ 新建 AxisConfigurationService.cs**：
   - `IAxisConfigurationService` 接口保持相同的签名（`GetAxesForStation(string stationIdentifier)`）
   - 实现类注入 `IMotionService`，通过新增的 `GetAxisConfigurations()` 和 `GetTaskConfigurations()` 方法获取 hwcfg.xml 数据
   - `GetAxesForStation()` 逻辑：
     a. 通过 `TaskConfig.Type` 匹配 `stationIdentifier` 找到 TaskId
     b. 通过 `AxisConfig.TaskId` 筛选属于该工站的所有轴
     c. 将 `AxisConfig` 转换为 `AxisDefinition` 返回

3. **更新 DI 注册**：将 `IAxisConfigurationService` 的注册从 `Module/PrimModel.cs` 迁移到 `MotionControl` 模块的 DI 注册中

4. **更新所有引用**：确保 `RecipeManagement`、`Module` 等项目的 `using` 语句更新

### Step 2: 在 IMotionService 接口中新增配置查询方法

**文件:** `MotionControl/Interfaces/IMotionService.cs`

**新增方法：**
```csharp
/// <summary> 获取所有轴配置（来自 hwcfg.xml） </summary>
IReadOnlyList<AxisConfig> GetAxisConfigurations();

/// <summary> 获取所有任务配置（来自 hwcfg.xml） </summary>
IReadOnlyList<TaskConfig> GetTaskConfigurations();
```

**文件:** `MotionControl/Services/MotionService.cs`

**实现：** 缓存 `MotionSystemConfig` 实例（新增 `_config` 字段），在 `InitializeAsync` 中保存，通过新方法暴露 `config.Axes` 和 `config.Tasks`。

### Step 3: 评估并移除 hwcfg.xml 中的 taskId 字段

**用户要求：** 评估 taskId 字段的实际使用场景，如无实际用途则移除。

**评估结果：**

| 使用位置 | 是否依赖 taskId | 说明 |
|---------|----------------|------|
| MotionService.BuildMappings | ❌ 不依赖 | 仅使用 LogicalId, CardId, Name |
| AxisConfigurationService（重构后） | ✅ 依赖 | 需要通过 taskId 关联轴与工站 |
| TaskBase.TaskId | ❌ 不依赖 | 这是 C# 代码中的硬编码 ID，与 hwcfg.xml 的 taskId 无关 |
| TaskParametersBase.TaskId | ❌ 不依赖 | 这是配方参数中的 ID，与 hwcfg.xml 的 taskId 无关 |

**结论：重构后 taskId 将成为关键数据！** 重构后的 `AxisConfigurationService` 和 `DiscoverAxes()` 都需要通过 `AxisConfig.TaskId` 和 `TaskConfig.Type` 建立轴-工站映射关系。因此 **taskId 字段必须保留**，它是 hwcfg.xml 中轴-工站绑定的唯一数据源。

**但需要优化：** 当前 `TaskConfig.TaskId` 与 `TaskBase.TaskId`（C# 硬编码值）是两套独立的编号系统，容易混淆。重构后应确保两者一致，或完全依赖 hwcfg.xml 的 taskId。

### Step 4: 修复 hwcfg.xml 中不规范的轴名称

**文件:** `MainApp/bin/Debug/net9.0-windows7.0/Config/HWConfig/hwcfg.xml`

**变更：**
- 第34行：`name="轴8_点胶工位Y轴Y1-2"` → `name="Dy2"`（点胶工位第二Y轴，如为备用轴则可移除整行）
- 确认第7轴（setAxisId=7）是否为实际使用的轴，如果是则规范命名

### Step 5: 重构任务类 — 消除硬编码轴 ID

**核心思路：** 在 `StationTaskBase` 中新增 `ResolveAxisId(string axisName)` 方法，从 `IMotionService` 的轴配置中根据轴名称动态查找逻辑轴 ID。各任务类不再使用 `const int` 常量，而是通过名称查找。

**文件:** `MotionControl/Services/StationTaskBase.cs`

**新增方法：**
```csharp
/// <summary>
/// 根据轴名称解析逻辑轴ID，从硬件配置中查找属于当前工站的轴
/// </summary>
protected int ResolveAxisId(string axisName)
{
    foreach (var axisId in GetAllAxes())
    {
        var state = Motion.GetAxisState(axisId);
        if (state != null && state.Name == axisName)
            return axisId;
    }
    Logger.Warn($"[{TaskName}] 未找到轴 '{axisName}' 的配置");
    return -1;
}
```

**文件:** `StationTasks/Tasks/LoadingTask.cs`

移除 `private const int AxisY = 9; AxisRx = 10; AxisRz = 11;`，改用属性：
```csharp
private int AxisY => ResolveAxisId("Y");
private int AxisRx => ResolveAxisId("Rx");
private int AxisRz => ResolveAxisId("Rz");
```

**文件:** `StationTasks/Tasks/AssemblyTask.cs`

移除 `private const int AxisX = 5; AxisZ = 0; AxisRy = 1;`，改用属性：
```csharp
private int AxisX => ResolveAxisId("X");
private int AxisZ => ResolveAxisId("Z");
private int AxisRy => ResolveAxisId("Ry");
private int AxisEy => ResolveAxisId("Ey");
private int AxisCy => ResolveAxisId("Cy");
```

**文件:** `StationTasks/Tasks/DispensingTask.cs`

移除 `private const int AxisX = 8; AxisY = 6; AxisZ1 = 2; AxisZ2 = 3; AxisZ3 = 4;`，改用属性：
```csharp
private int AxisDx => ResolveAxisId("Dx");
private int AxisDy => ResolveAxisId("Dy");
private int AxisDz1 => ResolveAxisId("Dz₁");
private int AxisDz2 => ResolveAxisId("Dz₂");
private int AxisDz3 => ResolveAxisId("Dz3");
```

### Step 6: 重构 GetAllAxes() — 从配置动态获取

**当前问题：** `GetAllAxes()` 返回硬编码的轴 ID 数组，与 `ResolveAxisId` 依赖的 `GetAllAxes()` 形成循环依赖。

**解决方案：** 在 `StationTaskBase` 中新增 `DiscoverAxes()` 方法，基于 hwcfg.xml 中的 taskId 和 TaskConfig.Type 动态发现属于当前工站的所有轴。

**文件:** `MotionControl/Services/StationTaskBase.cs`

**新增方法：**
```csharp
/// <summary>
/// 从硬件配置中发现属于当前工站的所有轴ID
/// 基于 hwcfg.xml 中 AxisConfig.TaskId 和 TaskConfig.Type 的映射关系
/// </summary>
private int[] DiscoverAxes()
{
    var axisConfigs = Motion.GetAxisConfigurations();
    var taskConfigs = Motion.GetTaskConfigurations();
    
    // 通过 TaskConfig.Type 匹配当前工站的 StationIdentifierValue，找到 TaskId
    int? myTaskId = null;
    foreach (var tc in taskConfigs)
    {
        if (tc.Type == StationIdentifierValue)
        {
            myTaskId = tc.TaskId;
            break;
        }
    }
    
    if (myTaskId == null)
    {
        Logger.Warn($"[{TaskName}] 未在硬件配置中找到工站类型 '{StationIdentifierValue}'");
        return Array.Empty<int>();
    }
    
    return axisConfigs
        .Where(a => a.TaskId == myTaskId.Value)
        .OrderBy(a => a.LogicalId)
        .Select(a => a.LogicalId)
        .ToArray();
}
```

**文件:** `MotionControl/Services/TaskBase.cs`

**变更：** 将 `GetAllAxes()` 和 `GetHomeAxes()` 从 `abstract` 改为 `virtual`，默认实现由子类通过 `DiscoverAxes()` 提供：
```csharp
protected virtual int[] GetAllAxes() => Array.Empty<int>();
protected virtual int[] GetHomeAxes() => GetAllAxes();
```

移除各子类中的 `GetAllAxes()` 和 `GetHomeAxes()` override，改为在 `StationTaskBase` 中统一实现：
```csharp
protected override int[] GetAllAxes() => DiscoverAxes();
protected override int[] GetHomeAxes() => GetAllAxes();
```

### Step 7: 验证 GotoStepAction 位置检索

**当前流程：**
```
GotoStepAction.ExecuteAsync
  → ResolveAxisId(subMove, task)  // 解析轴ID
  → task.ExecuteMoveAsync(axisId, positionName, speed, offset)
    → GetAxisNameById(axisId)  // 获取轴名称（如 "X"）
    → GetPositionAsync(positionName, axisName)  // 从配方获取位置值
      → _positionProvider.GetPositionsAsync(stationId)  // 返回 "StandbyPosition.X" = 100.0
```

**验证点：**
1. `GetAxisNameById` 依赖 `Motion.GetAxisState(axisId).Name`，而 `_axisStates` 在 `BuildMappings` 中从 `AxisConfig.Name` 初始化 — 正确
2. `GetPositionAsync` 使用 `axisName` 作为配方 key — 只要轴名称与配方 JSON 中的 key 一致即可
3. `FindAxisIdByName` 遍历 `GetAllAxes()` — 重构后 `GetAllAxes()` 返回动态发现的轴，此方法仍然正确
4. **关键确认：** 配方 JSON 中的轴名称 key 必须与 hwcfg.xml 中的 `name` 属性一致

### Step 8: 修复 AssemblyTask 多余 Y 轴问题

**根因：** `AxisConfigurationService` 硬编码了 AssemblyStation 包含 Y 轴，但 hwcfg.xml 中 Y 轴的 taskId=1（属于 LoadingStation）。

**修复：** Step 1 重构 `AxisConfigurationService` 后，AssemblyStation 将自动从 hwcfg.xml 获取正确的轴列表（X, Z, Ry, Ey, Cy），不再包含 Y 轴。此问题在 Step 1 完成后自动解决。

### Step 9: 编译验证

编译所有项目确保 0 错误，验证：
- MultiStationPositionEditorView 显示正确的轴列
- 任务初始化时轴发现正确
- GotoStepAction 位置检索正确
- 硬编码流程和自定义序列流程均正常工作
- 暂停/恢复/停止生命周期正常
- 急停功能正常

---

## 文件变更清单

| 文件 | 变更类型 | 说明 |
|------|---------|------|
| `Core/Services/AxisConfigurationService.cs` | 删除 | 移除接口和实现类 |
| `MotionControl/Services/AxisConfigurationService.cs` | 新建 | 从 IMotionService 动态读取轴配置 |
| `MotionControl/Interfaces/IMotionService.cs` | 修改 | 新增 GetAxisConfigurations/GetTaskConfigurations |
| `MotionControl/Services/MotionService.cs` | 修改 | 缓存 config，实现新接口方法 |
| `MotionControl/Services/TaskBase.cs` | 修改 | GetAllAxes/GetHomeAxes 改为 virtual |
| `MotionControl/Services/StationTaskBase.cs` | 修改 | 新增 ResolveAxisId/DiscoverAxes 方法，override GetAllAxes |
| `StationTasks/Tasks/LoadingTask.cs` | 修改 | 移除 const int，改用 ResolveAxisId 属性 |
| `StationTasks/Tasks/LoadingPickTask.cs` | 修改 | 适配新的轴属性 |
| `StationTasks/Tasks/AssemblyTask.cs` | 修改 | 移除 const int，改用 ResolveAxisId 属性，新增 Ey/Cy |
| `StationTasks/Tasks/DispensingTask.cs` | 修改 | 移除 const int，改用 ResolveAxisId 属性 |
| `Module/PrimModel.cs` | 修改 | 更新 IAxisConfigurationService 的 DI 注册 |
| `hwcfg.xml` | 修改 | 修复不规范轴名称 |

## 依赖关系

```
Step 2 (IMotionService 扩展) 
  → Step 1 (AxisConfigurationService 迁移到 MotionControl)
  → Step 6 (DiscoverAxes 动态发现)
  → Step 5 (任务类消除硬编码)
  → Step 3 (评估 taskId — 结论：保留)
  → Step 4 (hwcfg.xml 修复)
  → Step 7 (验证位置检索)
  → Step 8 (AssemblyTask Y轴问题自动解决)
  → Step 9 (编译验证)
```

Step 2 和 Step 4 可以并行执行。

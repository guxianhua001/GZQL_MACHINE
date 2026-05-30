# MultiStationPositionEditorView 运动控制接入 — TDD 执行计划

## 0. 当前状态审计

### 0.1 已完成项 ✅

| # | 项 | 文件 | 状态 |
|---|-----|------|------|
| 1 | IPositionMotionController 抽象接口 | [IPositionMotionController.cs](Core/Abstraction/IPositionMotionController.cs) | ✅ 定义完毕 |
| 2 | PositionMotionControllerImpl 实现 | [PositionMotionControllerImpl.cs](MotionControl/Services/PositionMotionControllerImpl.cs) | ✅ T1-T10 通过 |
| 3 | IStationMotionOperations 适配器接口 | [IStationMotionOperations.cs](MotionControl/Services/IStationMotionOperations.cs) | ✅ |
| 4 | StationTaskBase 实现适配器 | StationTaskBase.cs | ✅ implements IStationMotionOperations |
| 5 | DI 容器注册 | MotionControlModule.cs | ✅ Singleton 注册 |
| 6 | ViewModel 构造函数注入 | MultiStationPositionEditorViewModel.cs L113-128 | ✅ _motionController 字段已添加 |
| 7 | ViewModel 方法体重写 | MultiStationPositionEditorViewModel.cs L373-438 | ✅ Teach/Replay/Stop 已实现 |
| 8 | 测试项目创建 + T1-T10 | PositionMotionControllerTests.cs | ✅ 10/10 通过 |
| 9 | ViewModel 测试 T11-T15 编写 | MultiStationPositionEditorViewModelTests.cs | ✅ 已编写 |

### 0.2 发现的问题 ⚠️

| # | 问题 | 严重性 | 影响 |
|---|------|--------|------|
| P1 | **Recipe.csproj TFM 不一致**: `net9.0-windows` vs 测试项目 `net9.0-windows7.0` | 🔴 编译失败 | 测试项目无法引用 Recipe 项目 |
| P2 | **T11-T15 测试 station 标识符不匹配**: 测试 Verify 期望 `"TestStation"`，但 `_currentStationIdentifier` 为 null（registry mock 返回空列表） | 🔴 测试全部 FAIL | TeachAsync/GotoAsync/Stop 收到 null 而非 "TestStation" |
| P3 | csproj 引用路径已修正 | ✅ 上轮已修 | `RecipeManagement.csproj` → `Recipe.csproj` |

### 0.3 架构确认（无倒置依赖）

```
RecipeManagement (上层UI)
    ↓ 依赖 (仅接口)
Core.Abstractions.IPositionMotionController   ← 抽象层
    ↑ 实现
MotionControl.PositionMotionControllerImpl     ← 具体层
```

---

## 1. Step A：修复编译阻塞问题

### 1.1 统一 TFM (P1)

**文件**: [Recipe.csproj](RecipeManagement/Recipe.csproj) L4

**修改**: `<TargetFramework>net9.0-windows</TargetFramework>` → `<TargetFramework>net9.0-windows7.0</TargetFramework>`

**理由**: Core/MotionControl/Tests 全部使用 `net9.0-windows7.0`，保持一致。

### 1.2 验证编译通过

```bash
dotnet build MotionControl.Tests/MotionControl.Tests.csproj --no-restore
```

---

## 2. Step B：修复 T11-T15 测试对齐 (P2)

### 2.1 根因分析

ViewModel 构造函数末尾 (L156):
```csharp
if (Stations.Any()) SelectedStation = Stations.First();
```

测试当前设置:
```csharp
_stationRegistryMock.Setup(r => r.GetAllStations())
    .Returns(new List<IStationParameterProvider>()); // 空列表!
```

结果: Stations 为空 → 无自动选中 → `_currentStationIdentifier` = null

而测试断言:
```csharp
_motionControllerMock.Verify(m => m.TeachAsync("TestStation"), Times.Once());
// 实际调用: _motionController.TeachAsync(null)  ← 不匹配!
```

### 2.2 修复方案

在 `MultiStationPositionEditorViewModelTests` 中：

**(a)** 添加 `Mock<IStationParameterProvider>` 工站 mock 字段
**(b)** 修改构造函数 setup：让 registry 返回一个 Identifier="TestStation" 的工站
**(c)** 确保 ViewModel 创建后 `_currentStationIdentifier == "TestStation"`

具体修改：

```csharp
// 新增字段
private readonly Mock<IStationParameterProvider> _stationMock;

// 构造函数中添加
_stationMock = new Mock<IStationParameterProvider>();
_stationMock.Setup(s => s.StationIdentifier).Returns("TestStation");
_stationMock.Setup(s => s.CurrentPoolName).Returns("TestPool");
_stationMock.Setup(s => s.CurrentRecipeName).Returns("Default");

// 修改 registry setup — 返回包含 TestStation 的列表
_stationRegistryMock.Setup(r => r.GetAllStations())
    .Returns(new List<IStationParameterProvider> { _stationMock.Object });
```

这样 ViewModel 构造时会：
1. `LoadStationsFromRegistry()` → 从 registry 获取到 TestStation → 加入 Stations 集合
2. `if (Stations.Any()) SelectedStation = Stations.First()` → 选中 TestStation
3. `_currentStationIdentifier = "TestStation"` ✅

### 2.3 逐测试验证清单

| 测试 | 验证点 | 修复后预期 |
|------|--------|-----------|
| T11 | `TeachAsync("TestStation")` 被调用一次 | ✅ _currentStationIdentifier="TestStation" |
| T12 | 无选中行时不调用 | 需单独处理：手动设 SelectedRow=null |
| T13 | Teach 后 SelectedRow["X"/"Y"] 更新 | ✅ mock 返回值写入 DataRow |
| T14 | `GotoAsync("TestStation", {X:50,Y:60}, 15.0)` | ✅ 位置+速度正确 |
| T15 | `Stop("TestStation")` 被调用一次 | ✅ |

> **T12 特殊处理**: T12 测试"无选中行时不调用"，但修复后 ViewModel 会自动有选中站。需要在 T12 中显式 `vm.SelectedRow = null` 来触发该分支。

---

## 3. Step C：运行全量测试验证 GREEN

### 3.1 执行命令

```bash
dotnet test MotionControl.Tests/MotionControl.Tests.csproj --verbosity normal
```

### 3.2 预期结果

```
Total tests: 15
     Passed: 15
 Failed: 0
 Total time: X.XXXX Seconds
```

| 测试组 | 测试数 | 覆盖范围 |
|--------|--------|----------|
| T1-T3 TeachAsync | 3 | 正常读取/工站不存在/空轴配置 |
| T4-T6 GotoAsync | 3 | 正确参数/默认速度/工站不存在 |
| T7 Stop | 1 | 所有轴停止 |
| T8-T10 CanExecuteMotion | 3 | 运行中/空闲/工站不存在 |
| T11-T12 TeachCommand | 2 | 有选中行调用/无选中行不调用 |
| T13 Teach更新数据 | 1 | DataTable 当行更新 |
| T14 ReplayCommand | 1 | GotoAsync 参数正确性 |
| T15 StopCommand | 1 | Stop 调用验证 |

---

## 4. Step D：REFACTOR — 清理优化

### 4.1 提取轴数据转换辅助方法

**当前代码 Replay() L405-412 内联构建 Dictionary:**
```csharp
var axes = _axisConfig.GetAxesForStation(_currentStationIdentifier);
var targetPositions = new Dictionary<string, double>();
foreach (var axis in axes)
{
    var cellValue = SelectedRow[axis.Name];
    if (cellValue != DBNull.Value && cellValue != null)
        targetPositions[axis.Name] = Convert.ToDouble(cellValue);
}
```

**提取为独立方法（复用 + 可测试）:**
```csharp
/// <summary>
/// 从 DataTable 当前行提取轴位置字典
/// </summary>
private Dictionary<string, double> ExtractAxisPositionsFromRow(DataRowView row)
{
    var result = new Dictionary<string, double>();
    if (row == null || string.IsNullOrEmpty(_currentStationIdentifier)) return result;

    var axes = _axisConfig.GetAxesForStation(_currentStationIdentifier);
    foreach (var axis in axes)
    {
        var cellValue = row[axis.Name];
        if (cellValue != DBNull.Value && cellValue != null)
            result[axis.Name] = Convert.ToDouble(cellValue);
    }
    return result;
}
```

Replay() 简化为:
```csharp
var targetPositions = ExtractAxisPositionsFromRow(SelectedRow);
await _motionController.GotoAsync(_currentStationIdentifier, targetPositions, SelectedSpeed);
```

### 4.2 添加 IsMoving 属性（运动状态反馈）

```csharp
private bool _isMoving;
public bool IsMoving { get => _isMoving; private set => SetProperty(ref _isMoving, value); }
```

- Replay 开始时: `IsMoving = true;`
- Replay 完成后 (finally): `IsMoving = false;`
- 用途: UI 可绑定此属性实现运动中按钮禁用

### 4.3 OnDialogClosed 安全停止

```csharp
public void OnDialogClosed()
{
    _recipeChangedToken?.Dispose();
    _poolChangedToken?.Dispose();
    _stationRegisteredToken?.Dispose();
    if (_isMoving)
    {
        _motionController.Stop(_currentStationIdentifier);
        _isMoving = false;
    }
}
```

**目的**: 用户关闭对话框时若轴仍在运动，自动停止（工业安全要求）。

### 4.4 全量编译验证

```bash
dotnet build GZQL_MACHINE.sln --no-incremental
```

确认:
- [ ] 0 编译错误
- [ ] RecipeManagement 无直接引用 MotionControl（仅通过 Core.Abstractions）
- [ ] 无废弃警告

---

## 5. Step E：多语言补充

### 5.1 新增资源键

| 键名 | zh-CN | en-US | 使用位置 |
|------|-------|-------|---------|
| `MultiStationPos_TeachSuccess` | 教位成功 | Teach successful | Teach 成功后提示 |
| `MultiStationPos_TeachFailed` | 教位失败: {0} | Teach failed: {0} | Teach 异常提示 |
| `MultiStationPos_GotoSuccess` | 走位完成 | Move completed | Goto 完成后提示 |
| `MultiStationPos_GotoFailed` | 走位失败: {0} | Move failed: {0} | Goto 异常提示 |
| `MultiStationPos_StopExecuted` | 已停止 | Stopped | Stop 操作反馈 |
| `MultiStationPos_MotionNotReady` | 系统忙，无法执行运动操作 | System busy, motion not available | CanExecuteMotion=false 提示 |
| `MultiStationPos_NoSelection` | 请先选择一行位置 | Please select a position row first | SelectedRow=null 提示 |

### 5.2 修改文件

| 文件 | 操作 |
|------|------|
| [Strings.zh-CN.xaml](MainApp/Languages/Strings.zh-CN.xaml) | 追加上述键的中文值 |
| [Strings.en-US.xaml](MainApp/Languages/Strings.en-US.xaml) | 追加上述键的英文值 |
| [MultiStationPositionEditorViewModel.cs](RecipeManagement/ViewModels/MultiStationPositionEditorViewModel.cs) | 注入 ILocalizationService，硬编码字符串替换为资源查找 |

---

## 6. 变更文件清单

| 操作 | 文件路径 | 步骤 |
|------|----------|------|
| **修改** | `RecipeManagement/Recipe.csproj` | Step 1.1: TFM 统一 |
| **修改** | `MotionControl.Tests/.../MultiStationPositionEditorViewModelTests.cs` | Step 2.2: 修复 station 对齐 |
| **修改** | `RecipeManagement/ViewModels/MultiStationPositionEditorViewModel.cs` | Step 4.1-4.3: REFACTOR + Step 5.2: 多语言 |
| **修改** | `MainApp/Languages/Strings.zh-CN.xaml` | Step 5.2: 中文资源 |
| **修改** | `MainApp/Languages/Strings.en-US.xaml` | Step 5.2: 英文资源 |

---

## 7. 执行顺序与 TDD 纪律

```
Step A (修复编译) → Step B (修复测试对齐) → Step C (GREEN 验证) → Step D (REFACTOR) → Step E (多语言)
     ↑                                                                              |
     └── 必须先通过才能进入下一步 ──────────────────────────────────────────────────┘
```

**TDD 关键纪律**:
- Step B 修复测试后，必须先确认测试因**实现不匹配而失败**（RED），再确认修复后通过（GREEN）
- Step D REFACTOR 期间，每步重构后立即跑测试确保不回归
- 不跳过任何测试验证步骤

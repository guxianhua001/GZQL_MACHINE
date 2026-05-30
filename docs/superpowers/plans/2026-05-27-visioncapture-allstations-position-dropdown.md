# VisionCapture 位置下拉数据源全工站支持 — 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 让 VisionCapture 页面表格中的 Dx/Dy/Dz1/Y 位置下拉列表能选择所有已注册工站的位置，格式为 `StationId.PositionName`（如 `Dispense.NewPosition1`），解决当前只能看到第一个加载工站位置的问题。

**Architecture:** 采用双层结构方案：`_allPositions` 字典的 key 加 `{StationId}.` 前缀避免同名冲突；下拉显示层提取 `StationId.PositionName` 格式；行属性 `DxPositionName` 等存储带前缀的全名；全局位置名（Safe/Standby/Dispense）通过向后兼容逻辑处理旧配置。

**Tech Stack:** WPF + PRISM + C# / Newtonsoft.Json / RecipePositionProvider

---

## 文件变更清单

| 文件 | 操作 | 职责 |
|------|------|------|
| `Module/Controls/Dispense/VisionCaptureViewModel.cs` | Modify | 核心改动：合并逻辑、查找点、兼容处理 |
| `Module/Controls/Dispense/PhotoPositionRow.cs` | Modify | LoadPositionsAsync 方法适配（如有使用） |
| `VisionCaptureView.xaml` | No Change | ComboBox 绑定无需修改 |

---

### Task 1: 修改 MergeAllPositionsAsync — key 加工站前缀

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\Dispense\VisionCaptureViewModel.cs:824-842`

- [ ] **Step 1: 修改 MergeAllPositionsAsync 方法**

将合并时 key 从 `{PosName}.{Axis}` 改为 `{StationId}.{PosName}.{Axis}`：

```csharp
private async Task<Dictionary<string, double>> MergeAllPositionsAsync()
{
    var merged = new Dictionary<string, double>();
    var stations = _stationRegistry.GetAllStations();
    foreach (var station in stations)
    {
        try
        {
            var positions = await _positionProvider.GetPositionsAsync(station.StationIdentifier);
            if (positions == null) continue;
            foreach (var kvp in positions)
            {
                var prefixedKey = $"{station.StationIdentifier}.{kvp.Key}";
                if (!merged.ContainsKey(prefixedKey))
                    merged[prefixedKey] = kvp.Value;
            }
        }
        catch
        {
        }
    }
    return merged;
}
```

关键变更：`kvp.Key` → `$"{station.StationIdentifier}.{kvp.Key}"`，确保不同工站同名位置不冲突。

- [ ] **Step 2: 编译验证**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj --no-restore`
Expected: 0 errors

---

### Task 2: 修改 OnSelectedGroupChanged — 下拉显示带工站前缀

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\Dispense\VisionCaptureViewModel.cs:781-800`

- [ ] **Step 1: 修改下拉列表提取逻辑**

将 key 提取从 `{PosName}` 改为 `{StationId}.{PosName}`：

```csharp
_allPositions = await MergeAllPositionsAsync();

PhotoPositionRows.Clear();
SiteFeatureNames.Clear();
foreach (var feature in site.Features)
{
    var row = new PhotoPositionRow(feature.Name);
    var positionNames = new HashSet<string>();
    foreach (var key in _allPositions.Keys)
    {
        // key 格式: "Dispense.NewPosition1.Dx"
        var parts = key.Split('.');
        if (parts.Length >= 3)
            positionNames.Add($"{parts[0]}.{parts[1]}"); // "Dispense.NewPosition1"
    }
    row.AvailablePositions = new ObservableCollection<string>(positionNames.OrderBy(p => p));
    PhotoPositionRows.Add(row);
    SiteFeatureNames.Add(feature.Name);
}
```

关键变更：`parts.Length >= 2` → `parts.Length >= 3`，提取前两段作为显示名。

- [ ] **Step 2: 编译验证**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj --no-restore`
Expected: 0 errors

---

### Task 3: 更新 TeachPositionAsync 写入逻辑

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\Dispense\VisionCaptureViewModel.cs:950-960`

- [ ] **Step 1: 确认 TeachPositionAsync 的 key 构建已自动适配**

当前代码：
```csharp
_allPositions[$"{row.DxPositionName}.Dx"] = dxPos;
_allPositions[$"{row.DyPositionName}.Dy"] = dyPos;
_allPositions[$"{row.Dz1PositionName}.Dz₁"] = dz1Pos;
_allPositions[$"{row.YPositionName}.Y"] = yPos;
```

由于 `row.DxPositionName` 现在存储的是 `"Dispense.NewPosition1"` 格式，拼接后自然得到 `"Dispense.NewPosition1.Dx"`，与 `MergeAllPositionsAsync` 的 key 格式一致。**此步骤为验证性确认，无需代码修改。**

- [ ] **Step 2: 同样确认 TeachSafePositionAsync (L1438-L1440)**

```csharp
_allPositions[$"{SafePositionName}.Dx"] = dxPos;
_allPositions[$"{SafePositionName}.Dy"] = dyPos;
_allPositions[$"{SafePositionName}.Dz₁"] = dz1Pos;
```

`SafePositionName` 默认值仍为 `"SafePosition"`（无前缀）。此处保持不变，后续 Task 5 处理全局位置的兼容性。

---

### Task 4: 更新所有 _allPositions 读取点 — 行级位置查找

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\Dispense\VisionCaptureViewModel.cs`

以下读取点因 `row.DxPositionName` 已含前缀，key 拼接后自动正确，**逐条确认无额外修改需求**：

| 行号 | 当前代码 | 说明 |
|------|---------|------|
| L893-901 | `ExecuteCaptureAsync` 传入 `row.DxPositionName` | VisionCaptureService 内部用 `{posName}.{axis}` 构建 key → 自动适配 |
| L993-998 | `MoveToTeachAsync` TryGetValue | `row.DxPositionName` 含前缀 → 自动适配 |
| L1046-1052 | `ExecuteDispenseAsync` TryGetValue | `SelectedRow.DxPositionName` 含前缀 → 自动适配 |
| L1111-1112 | `PreviewMachinePointsAsync` TryGetValue | `SelectedRow.DxPositionName` 含前缀 → 自动适配 |

- [ ] **Step 1: 验证以上4处读取点的 key 构建方式均为 ` $"{name}.{axis}" ` 格式**

确认：所有行级位置（Dx/Dy/Dz1/Y）均通过 `row.XxxPositionName` 或 `SelectedRow.XxxPositionName` 构建 key。这些值来自下拉选择（Task 2 已改为带前缀），因此 key 自然匹配新格式。

---

### Task 5: 处理全局位置名的向后兼容（Safe/Standby/Dispense）

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\Dispense\VisionCaptureViewModel.cs`
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\Dispense\VisionCaptureViewModel.cs:813-820` (RefreshSafePositionDisplay)
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\Dispense\VisionCaptureViewModel.cs:1683-1730` (ApplyConfig)

**问题分析：** 全局位置名 `SafePositionName`、`StandbyPositionName`、`DispensePositionName` 在旧 JSON 中存为无前缀值（如 `"SafePosition"`），新 key 格式需要前缀（如 `"ASSY.SafePosition"`）。需提供两种策略：
1. **精确匹配优先**：先尝试 `{name}.{axis}` 前缀查找（兼容旧值）
2. **模糊兜底**：若未找到，遍历所有 key 尝试尾缀匹配

- [ ] **Step 1: 添加辅助方法 ResolvePositionKey 用于全局位置名解析**

在类中添加私有方法：

```csharp
private string ResolvePositionKey(string positionName, string axisName)
{
    if (string.IsNullOrEmpty(positionName)) return null;

    var exactKey = $"{positionName}.{axisName}";
    if (_allPositions.ContainsKey(exactKey))
        return exactKey;

    var suffix = $".{positionName}.{axisName}";
    foreach (var key in _allPositions.Keys)
    {
        if (key.EndsWith(suffix))
            return key;
    }
    return exactKey;
}
```

逻辑：
1. 先尝试直接 key（兼容已有前缀的新配置）
2. 再尝试尾部模糊匹配（兼容无前缀的旧配置）
3. 都没找到返回原始 key（TryGetValue 返回 default 不报错）

- [ ] **Step 2: 修改 RefreshSafePositionDisplay 使用 ResolvePositionKey**

```csharp
private void RefreshSafePositionDisplay()
{
    if (_allPositions == null) return;
    var dxKey = ResolvePositionKey(SafePositionName, "Dx");
    var dyKey = ResolvePositionKey(SafePositionName, "Dy");
    var dz1Key = ResolvePositionKey(SafePositionName, "Dz₁");
    _allPositions.TryGetValue(dxKey, out var dx);
    _allPositions.TryGetValue(dyKey, out var dy);
    _allPositions.TryGetValue(dz1Key, out var dz1);
    SafePositionDx = dx;
    SafePositionDy = dy;
    SafePositionDz1 = dz1;
}
```

- [ ] **Step 3: 替换 ExecuteDispenseAsync 中 SafePositionName/DispensePositionName 的 TryGetValue (L1046-L1052)**

```csharp
// 之前:
// _allPositions.TryGetValue($"{SafePositionName}.Dz₁", out var safeVal)
// 之后:
var safeKey = ResolvePositionKey(SafePositionName, "Dz₁");
_allPositions.TryGetValue(safeKey, out var safeVal);

var dispenseKey = ResolvePositionKey(DispensePositionName, "Dz₁");
_allPositions.TryGetValue(dispenseKey, out var dispenseVal);

var pDxKey = ResolvePositionKey(SelectedRow.DxPositionName, "Dx");
_allPositions.TryGetValue(pDxKey, out var pDxVal);

var pDyKey = ResolvePositionKey(SelectedRow.DyPositionName, "Dy");
_allPositions.TryGetValue(pDyKey, out var pDyVal);
```

注意：`SelectedRow.DxPositionName` 是行级位置（已含前缀），用 `ResolvePositionKey` 也安全（走精确匹配路径）。

- [ ] **Step 4: 替换 MoveToTeachAsync 中 SafePositionName/row 位置的 TryGetValue (L993-L998)**

```csharp
var safeZKey = ResolvePositionKey(SafePositionName, "Dz₁");
_allPositions.TryGetValue(safeZKey, out safeZ);

var photoZKey = ResolvePositionKey(row.Dz1PositionName, "Dz₁");
_allPositions.TryGetValue(photoZKey, out photoZ);

var targetXKey = ResolvePositionKey(row.DxPositionName, "Dx");
_allPositions.TryGetValue(targetXKey, out targetX);

var targetYKey = ResolvePositionKey(row.DyPositionName, "Dy");
_allPositions.TryGetValue(targetYKey, out targetY);
```

- [ ] **Step 5: 替换 PreviewMachinePointsAsync 中 SelectedRow 位置的 TryGetValue (L1111-L1112)**

```csharp
var photoDxKey = ResolvePositionKey(SelectedRow.DxPositionName, "Dx");
_allPositions.TryGetValue(photoDxKey, out photoDx);

var photoDyKey = ResolvePositionKey(SelectedRow.DyPositionName, "Dy");
_allPositions.TryGetValue(photoDyKey, out photoDy);
```

- [ ] **Step 6: 编译验证**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj --no-restore`
Expected: 0 errors

---

### Task 6: ApplyConfig 向后兼容 — 处理旧 JSON 中无前缀的位置名

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\Dispense\VisionCaptureViewModel.cs:1683-1730`

- [ ] **Step 1: 在 ApplyConfig 开头添加位置名迁移逻辑**

在 `ApplyConfig` 方法中，设置 `SelectedGroup` 之后、遍历 Rows 之前，添加位置名自动补前缀逻辑：

```csharp
private async Task ApplyConfig(VisionCaptureConfig config)
{
    SafePositionName = config.SafePositionName ?? "SafePosition";
    StandbyPositionName = config.StandbyPositionName ?? "StandbyPosition";
    DispensePositionName = config.DispensePositionName ?? "DispensePosition";
    CameraCenterX = config.CameraCenterX;
    CameraCenterY = config.CameraCenterY;
    CurrentRunMode = config.CurrentRunMode;

    if (!string.IsNullOrEmpty(config.SelectedGroup) && Groups.Contains(config.SelectedGroup))
        SelectedGroup = config.SelectedGroup;

    await _reloadRowsTask;

    foreach (var rowConfig in config.Rows)
    {
        var row = PhotoPositionRows.FirstOrDefault(r => r.SiteFeatureName == rowConfig.SiteFeatureName);
        if (row != null)
        {
            row.DxPositionName = MigratePositionName(rowConfig.DxPositionName);
            row.DyPositionName = MigratePositionName(rowConfig.DyPositionName);
            row.Dz1PositionName = MigratePositionName(rowConfig.Dz1PositionName);
            row.YPositionName = MigratePositionName(rowConfig.YPositionName);
            // ... 其余属性赋值不变
        }
    }

    RefreshSafePositionDisplay();
}
```

- [ ] **Step 2: 添加 MigratePositionName 辅助方法**

```csharp
/// <summary>
/// 将旧格式的位置名（无工站前缀）迁移为新格式（带工站前缀）。
/// 若已包含前缀（即含有两个以上的 '.' 分隔段）则原样返回。
/// 否则在 _allPositions 中查找匹配项并返回带前缀的完整名称。
/// </summary>
private string MigratePositionName(string oldName)
{
    if (string.IsNullOrEmpty(oldName)) return oldName;

    var parts = oldName.Split('.');
    if (parts.Length >= 2)
    {
        // 新格式: "StationId.PosName" 或更多段，检查是否已在 _allPositions 中存在
        var testKey = $"{oldName}.Dx";
        if (_allPositions.ContainsKey(testKey))
            return oldName;
    }

    // 旧格式: 无前缀的纯位置名，尝试在所有工站中查找
    foreach (var key in _allPositions.Keys)
    {
        var keyParts = key.Split('.');
        if (keyParts.Length >= 3 && keyParts[1] == oldName)
            return $"{keyParts[0]}.{oldName}";
    }

    // 找不到匹配，返回原始值（用户可手动重新选择）
    return oldName;
}
```

逻辑说明：
- 输入 `"Dispense.NewPosition1"` → 含2段且 `.Dx` key 存在 → 直接返回
- 输入 `"NewPosition1"` → 单段 → 遍历找 `"*.NewPosition1.*"` → 返回 `"ASSY.NewPosition1"`
- 输入 `"UnknownPosition"` → 找不到 → 返回原值（下拉中不匹配但不会崩溃）

- [ ] **Step 3: 编译验证**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj --no-restore`
Expected: 0 errors

---

### Task 7: BuildCurrentConfig 确保保存时写入完整格式

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\Dispense\VisionCaptureViewModel.cs:1636-1679`

- [ ] **Step 1: 确认 BuildCurrentConfig 无需修改**

`BuildCurrentConfig` 直接从 ViewModel 属性取值：
```csharp
DxPositionName = r.DxPositionName,
DyPositionName = r.DyPositionName,
// ...
```

这些值已经是带前缀的新格式（来自下拉选择），序列化到 JSON 时自然保存为完整格式。**无需代码修改，仅作确认。**

---

### Task 8: 清理 PhotoPositionRow.LoadPositionsAsync（如不再使用）

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\Dispense\PhotoPositionRow.cs:346-374`

- [ ] **Step 1: 确认 LoadPositionsAsync 是否被调用**

搜索 `LoadPositionsAsync` 调用点：
- 若仅被 `PhotoPositionRow` 自身定义但未被外部调用 → 标记为 dead code，可选删除或保留
- 若被调用 → 更新其 key 提取逻辑以适配新格式（同 Task 2 的提取逻辑）

- [ ] **Step 2: 最终编译验证**

Run: `dotnet build c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj --no-restore`
Expected: 0 errors, 0 warnings related to changed code

---

## 实施顺序依赖关系

```
Task 1 (MergeAllPositionsAsync 加前缀)
  ↓
Task 2 (OnSelectedGroupChanged 下拉提取)
  ↓
Task 3 (Teach 写入确认 — 无代码改)
  ↓
Task 4 (行级读取点确认 — 无代码改)
  ↓
Task 5 (全局位置 ResolvePositionKey) ← 核心改动
  ↓
Task 6 (ApplyConfig 兼容迁移)
  ↓
Task 7 (BuildCurrentConfig 确认 — 无代码改)
  ↓
Task 8 (清理死代码)
  ↓
最终编译验证
```

## 自检清单

- [ ] Spec coverage: 所有 `_allPositions[key]` 读写点均已覆盖（7处写 + ~12处读）
- [ ] Placeholder scan: 无 TBD/TODO/待补充
- [ ] Type consistency: `ResolvePositionKey` 和 `MigratePositionName` 的签名和返回类型在各处调用一致
- [ ] 向后兼容: 旧 JSON（无前缀）可通过 `MigratePositionName` + `ResolvePositionKey` 双重机制正常加载
- [ ] XAML 无需改动: ComboBox 绑定 `AvailablePositions`（Task 2 填充）+ `SelectedItem`（Task 6 赋值）

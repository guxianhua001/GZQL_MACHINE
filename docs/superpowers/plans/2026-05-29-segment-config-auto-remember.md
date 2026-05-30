# 轨迹段配置文件自动记忆 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** CadPointEditorView 加载/保存轨迹段时，记住最后使用的配置文件路径，存入当前配方池，下次初始化时自动加载。

**Architecture:** 在 `DispenserStationParams` 中新增 `LastSegmentConfigPath` 属性，保存时写入路径，加载时读取路径并自动恢复。配方系统已有的 `SegmentsSerialized` 负责段数据持久化，`LastSegmentConfigPath` 仅记录外部 JSON 文件路径作为快速恢复入口。CadPointEditorViewModel 通过 `IDispenseSegmentStore` 获取路径信息，初始化时自动加载。

**Tech Stack:** WPF + Prism + MaterialDesign, System.Text.Json, RecipeManagement (IRecipePoolService / IRecipeStorage)

---

## 关键数据流

```
保存轨迹段 → ExecuteSaveSegments() → 记录文件路径到 _lastSegmentConfigPath
                                        ↓
                              通知 DispenserStationParams.LastSegmentConfigPath
                                        ↓
                              配方系统保存时序列化此属性
                                        ↓
初始化 → RecipeStationBase.InitializeRecipeAsync() → 恢复 DispenserStationParams
                                        ↓
                              CadPointEditorViewModel 检查路径 → 自动加载
```

---

### Task 1: DispenserStationParams 新增 LastSegmentConfigPath 属性

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\StationTasks\Params\DispenserStationParams.cs`

- [ ] **Step 1: 在 DispenserStationParams 中添加 LastSegmentConfigPath 属性**

在 `DispenserStationParams` 类中（`SegmentsSerialized` 属性之后）添加：

```csharp
private string _lastSegmentConfigPath = string.Empty;

[Category("Dispensing path")]
[DisplayName("Last Segment Config Path")]
[Description("最后一次加载/保存轨迹段的配置文件路径")]
[Browsable(false)]
public string LastSegmentConfigPath
{
    get => _lastSegmentConfigPath;
    set => SetProperty(ref _lastSegmentConfigPath, value);
}
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build "c:\WorkFiles\GZQL_MACHINE\StationTasks\StationTasks.csproj" --no-restore -v q`
Expected: BUILD SUCCEEDED, 0 errors

---

### Task 2: IDispenseSegmentStore 扩展 — 暴露 LastSegmentConfigPath

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Core\Abstraction\IDispenseSegmentStore.cs`
- Modify: `c:\WorkFiles\GZQL_MACHINE\Core\Services\DispenseSegmentStore.cs`

- [ ] **Step 1: 在 IDispenseSegmentStore 接口添加属性**

```csharp
/// <summary>最后一次加载/保存轨迹段的配置文件路径（来自配方参数）</summary>
string LastSegmentConfigPath { get; set; }
```

- [ ] **Step 2: 在 DispenseSegmentStore 实现属性**

```csharp
private string _lastSegmentConfigPath = string.Empty;

/// <summary>最后一次加载/保存轨迹段的配置文件路径（来自配方参数）</summary>
public string LastSegmentConfigPath
{
    get => _lastSegmentConfigPath;
    set => SetProperty(ref _lastSegmentConfigPath, value);
}
```

- [ ] **Step 3: 构建验证**

Run: `dotnet build "c:\WorkFiles\GZQL_MACHINE\Core\Core.csproj" --no-restore -v q`
Expected: BUILD SUCCEEDED, 0 errors

---

### Task 3: CadPointEditorViewModel — 保存时记录路径，初始化时自动加载

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Module\Controls\Cad\CadPointEditorViewModel.cs`

- [ ] **Step 1: 在 ExecuteSaveSegments 中记录保存路径**

在 `ExecuteSaveSegments()` 方法中，`System.IO.File.WriteAllText` 成功后，添加路径记录：

```csharp
// 保存成功后记录路径到配方参数
_lastSegmentConfigPath = dialog.FileName;
_dispenseSegmentStore.LastSegmentConfigPath = dialog.FileName;
```

需要在类中添加字段：
```csharp
private string _lastSegmentConfigPath = string.Empty;
```

- [ ] **Step 2: 在 ExecuteLoadSegments 中记录加载路径**

在 `ExecuteLoadSegments()` 方法中，加载成功后（`GoToStep(2)` 之前），添加路径记录：

```csharp
// 加载成功后记录路径到配方参数
_lastSegmentConfigPath = dialog.FileName;
_dispenseSegmentStore.LastSegmentConfigPath = dialog.FileName;
```

- [ ] **Step 3: 添加自动加载方法 TryAutoLoadLastConfig**

在 `CadPointEditorViewModel` 中添加自动加载方法：

```csharp
/// <summary>
/// 尝试自动加载上次使用的轨迹段配置文件
/// 在控件初始化时调用，从配方参数中读取路径
/// </summary>
public void TryAutoLoadLastConfig()
{
    var path = _dispenseSegmentStore?.LastSegmentConfigPath;
    if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return;

    try
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        string json = System.IO.File.ReadAllText(path);

        var saveData = System.Text.Json.JsonSerializer.Deserialize<Core.Models.SegmentSaveData>(json, options);
        List<Core.Models.DispenseSegment> loaded = null;
        Core.Models.CoordinateAlignData alignData = null;

        if (saveData?.Segments != null && saveData.Segments.Count > 0)
        {
            loaded = saveData.Segments;
            alignData = saveData.AlignData;
        }
        else
        {
            loaded = System.Text.Json.JsonSerializer.Deserialize<List<Core.Models.DispenseSegment>>(json, options);
        }

        if (loaded == null || loaded.Count == 0) return;

        CanvasEntities.Clear();
        Segments.Clear();
        _layerCheckList.Clear();
        SelectedSegment = null;

        foreach (var seg in loaded)
            Segments.Add(seg);

        RebuildCanvasEntitiesFromSegments();
        RebuildLayerList();

        if (alignData != null)
        {
            MapFiducialX = alignData.MapFiducialX;
            MapFiducialY = alignData.MapFiducialY;
            MapFiducialZ = alignData.MapFiducialZ;
            MachineFidX = alignData.MachineFidX;
            MachineFidY = alignData.MachineFidY;
            MachineFidZ = alignData.MachineFidZ;
            MachineFidRx = alignData.MachineFidRx;
            MachineFidRz = alignData.MachineFidRz;
            DirectionLength = alignData.DirectionLength;

            switch (alignData.AlignMode)
            {
                case "Affine": IsModeAffine = true; break;
                case "AllPoints": IsModeAllPoints = true; break;
                default: IsModeFirstPoint = true; break;
            }
        }

        FitCanvasToExtents();
        _lastSegmentConfigPath = path;
        GlobalStatus = $"已自动加载上次配置: {Segments.Count} 段 ← {System.IO.Path.GetFileName(path)}";
        GoToStep(2);
    }
    catch (Exception ex)
    {
        GlobalStatus = $"自动加载配置失败: {ex.Message}";
    }
}
```

- [ ] **Step 4: 在构造函数末尾调用自动加载**

在构造函数最后（`_dispenseSegmentStore?.RegisterSegments(_segments);` 之后）添加：

```csharp
// 尝试从配方参数自动加载上次使用的配置
TryAutoLoadLastConfig();
```

- [ ] **Step 5: 构建验证**

Run: `dotnet build "c:\WorkFiles\GZQL_MACHINE\Module\Module.csproj" --no-restore -v q`
Expected: BUILD SUCCEEDED, 0 errors

---

### Task 4: DispenseSegmentStore — 从 DispenserStationParams 同步路径

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Core\Services\DispenseSegmentStore.cs`

- [ ] **Step 1: 在 DispenseSegmentStore 中添加与配方参数的同步逻辑**

在 `DispenseSegmentStore` 中注入 `IStationRegistry`，并在 `LastSegmentConfigPath` setter 中同步到 `DispenserStationParams`：

```csharp
private readonly IStationRegistry _stationRegistry;

public DispenseSegmentStore(IStationRegistry stationRegistry = null)
{
    _stationRegistry = stationRegistry;
}

// 在 LastSegmentConfigPath setter 中同步到配方参数
public string LastSegmentConfigPath
{
    get => _lastSegmentConfigPath;
    set
    {
        if (SetProperty(ref _lastSegmentConfigPath, value))
        {
            SyncPathToStationParams(value);
        }
    }
}

/// <summary>同步路径到 DispenserStationParams（配方系统会自动持久化）</summary>
private void SyncPathToStationParams(string path)
{
    try
    {
        var station = _stationRegistry?.GetStation("DispenserStation");
        if (station is IStationParameterProvider provider &&
            provider.CurrentParameters is DispenserStationParams dsp)
        {
            dsp.LastSegmentConfigPath = path;
        }
    }
    catch { /* 静默处理，不影响主流程 */ }
}
```

- [ ] **Step 2: 更新 DI 注册**

检查 `DispenseSegmentStore` 的 DI 注册，确保 `IStationRegistry` 被注入。

- [ ] **Step 3: 构建验证**

Run: `dotnet build "c:\WorkFiles\GZQL_MACHINE\Core\Core.csproj" --no-restore -v q`
Expected: BUILD SUCCEEDED, 0 errors

---

### Task 5: 初始化时从 DispenserStationParams 恢复路径

**Files:**
- Modify: `c:\WorkFiles\GZQL_MACHINE\Core\Services\DispenseSegmentStore.cs`

- [ ] **Step 1: 添加从配方参数恢复路径的方法**

```csharp
/// <summary>从 DispenserStationParams 恢复上次配置路径（在配方加载后调用）</summary>
public void RestorePathFromStationParams()
{
    try
    {
        var station = _stationRegistry?.GetStation("DispenserStation");
        if (station is IStationParameterProvider provider &&
            provider.CurrentParameters is DispenserStationParams dsp)
        {
            if (!string.IsNullOrWhiteSpace(dsp.LastSegmentConfigPath))
            {
                _lastSegmentConfigPath = dsp.LastSegmentConfigPath;
                OnPropertyChanged(nameof(LastSegmentConfigPath));
            }
        }
    }
    catch { /* 静默处理 */ }
}
```

- [ ] **Step 2: 在 RegisterSegments 中调用恢复**

在 `RegisterSegments` 方法中添加路径恢复调用：

```csharp
public void RegisterSegments(ObservableCollection<DispenseSegment> segments)
{
    CurrentSegments = segments;
    RestorePathFromStationParams();
}
```

- [ ] **Step 3: 构建验证**

Run: `dotnet build "c:\WorkFiles\GZQL_MACHINE\Core\Core.csproj" --no-restore -v q`
Expected: BUILD SUCCEEDED, 0 errors

---

### Task 6: 全项目构建验证

- [ ] **Step 1: 全项目构建**

Run: `dotnet build "c:\WorkFiles\GZQL_MACHINE\GZQL_MACHINE.sln" -v q`
Expected: BUILD SUCCEEDED, 0 errors

---

## Self-Review

1. **Spec coverage:**
   - ✅ 保存轨迹段时记录路径 → Task 3 Step 1
   - ✅ 加载轨迹段时记录路径 → Task 3 Step 2
   - ✅ 路径存入配方池 → Task 1 (DispenserStationParams) + Task 4 (同步)
   - ✅ 下次初始化自动加载 → Task 3 Step 3-4 + Task 5

2. **Placeholder scan:** 无 TBD/TODO/placeholder

3. **Type consistency:**
   - `LastSegmentConfigPath` 在 `DispenserStationParams`、`IDispenseSegmentStore`、`DispenseSegmentStore`、`CadPointEditorViewModel` 中均为 `string` ✅
   - `_dispenseSegmentStore?.LastSegmentConfigPath` 调用一致 ✅

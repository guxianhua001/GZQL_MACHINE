# CadAlignment 坐标对齐页面 Bug 修复计划

## 问题分析

### 问题 1: 回转中心拟合点数据不正确

**文件**: [CadAlignmentViewModel.cs](file:///c:/WorkFiles/GZQL_MACHINE/Module/Controls/Assembly/CadAlignmentViewModel.cs#L75-L81)

**现状**: FitPoints 默认值是硬编码的测试数据：
```csharp
FitPoints = new ObservableCollection<FitPoint>
{
    new() { AngleLabel = "0°",   FitX = 100.000, FitY = 200.000 },
    new() { AngleLabel = "90°",  FitX = 150.500, FitY = 198.200 },
    new() { AngleLabel = "180°", FitX = 102.300, FitY = 150.800 },
    new() { AngleLabel = "270°", FitX = 48.700,  FitY = 153.500 },
};
```

**期望值**（来自截图）：
| 角度 | X(FitX) | Y(FitY) |
|------|---------|---------|
| 0°   | 70.32   | 213.26  |
| 90°  | 100.2   | 277.28  |
| 180° | 95.95   | 201.28  |
| 270° | 91.67   | 242.28  |

**修复**: 将 FitPoints 初始值更新为截图中的数据。

---

### 问题 2: 计算全局偏移 — 命令名不匹配 + P1Mx/P1My 未初始化

#### Bug 2a: 命令名绑定错误

**XAML** ([CadAlignmentView.xaml:375](file:///c:/WorkFiles/GZQL_MACHINE/Module/Controls/Assembly/CadAlignmentView.xaml#L375)):
```xml
<Button Command="{Binding CalculateGlobalOffsetCommand}" .../>
```

**ViewModel** ([CadAlignmentViewModel.cs:90](file:///c:/WorkFiles/GZQL_MACHINE/Module/Controls/Assembly/CadAlignmentViewModel.cs#L90)):
```csharp
ComputeGlobalOffsetCommand = new DelegateCommand(OnComputeGlobalOffset);
```

→ XAML 绑定的是 `CalculateGlobalOffsetCommand`，但 ViewModel 定义的是 `ComputeGlobalOffsetCommand`。**命令名不一致导致按钮点击无响应。**

#### Bug 2b: P1Mx/P1My 未从 CorrespondencePoints 初始化

**现状** ([CadAlignmentViewModel.cs:83-L87](file:///c:/WorkFiles/GZQL_MACHINE/Module/Controls/Assembly/CadAlignmentViewModel.cs#L83-L87)):
```csharp
if (CorrespondencePoints.Count > 0)
{
    P1Cx = CorrespondencePoints[0].CadX;    // ✓ CAD坐标有初始化
    P1Cy = CorrespondencePoints[0].CadY;    // ✓
    // ✗ 缺少: P1Mx = CorrespondencePoints[0].ActualX;
    // ✗ 缺少: P1My = CorrespondencePoints[0].ActualY;
}
```

P1Mx 和 P1My 保持默认值 0，即使用户在界面上看到 CorrespondencePoints 中 P1 的 ActualX=70.32, ActualY=213.26，输入框也不会自动填充这些值。

**修复**:
1. 修正 XAML 命令绑定为 `ComputeGlobalOffsetCommand`
2. 补充 P1Mx/P1My 从 CorrespondencePoints[0] 的初始化

---

### 问题 3: 点对选择下拉框为空

**XAML 绑定** ([CadAlignmentView.xaml:476-L483](file:///c:/WorkFiles/GZQL_MACHINE/Module/Controls/Assembly/CadAlignmentView.xaml#L476-L483)):
```xml
<ComboBox ItemsSource="{Binding PairNames}" SelectedIndex="{Binding BasePairIndex}" .../>
<ComboBox ItemsSource="{Binding PairNames}" SelectedIndex="{Binding TargetPairIndex}" .../>
```

**问题**: ViewModel 中 **不存在 `PairNames` 属性**！ComboBox 没有数据源，所以下拉为空。

同样的问题还存在于:
- [CadAlignmentView.xaml:596](file:///c:/WorkFiles/GZQL_MACHINE/Module/Controls/Assembly/CadAlignmentView.xaml#L596): `{Binding PointNames}` — ViewModel 中也不存在 `PointNames` 属性

**设计意图分析**:
- "基准线段"和"目标线段"应从 CorrespondencePoints 中选取连续的点对（如 P1-P2、P3-P4 等）
- "目标点位"应从 CorrespondencePoints 中选取单个点（P1~P6）
- 这些下拉选项应该动态生成自 CorrespondencePoints 集合

**修复方案**: 在 CadAlignmentViewModel 中添加:
1. `PairNames` 属性 — 返回点对名称列表（如 "P1→P2", "P3→P4", "P5→P6"），基于 CorrespondencePoints 动态生成
2. `PointNames` 属性 — 返回点位名称列表（如 "P1(P3)", "P2(P4)", "P3(P5)", "P4(P6)"），用于变换选择（排除已用作基准的点对）

---

### 额外发现的其他命令名不匹配问题

| XAML 中的绑定 | ViewModel 中的定义 | 所在行 |
|-------------|-----------------|--------|
| `{Binding CalculateGlobalOffsetCommand}` | `ComputeGlobalOffsetCommand` | XAML:375, VM:90 |
| `{Binding CalculateRotationAngleCommand}` | `ComputeCadRotationAngleCommand` | XAML:486, VM:91 |
| `{Binding TransformSinglePointCommand}` | `ExecuteTransformCommand` | XAML:618, VM:92 |

这三个命令全部存在命名不一致的问题，都需要修正。

---

## 实施步骤

### Step 1: 修正 FitPoints 默认数据
**文件**: `Module/Controls/Assembly/CadAlignmentViewModel.cs`
- 将第 75-81 行的 FitPoints 初始值替换为截图中的实际测量数据

### Step 2: 修正命令名绑定（3处 XAML）
**文件**: `Module/Controls/Assembly/CadAlignmentView.xaml`
- 第 375 行: `CalculateGlobalOffsetCommand` → `ComputeGlobalOffsetCommand`
- 第 486 行: `CalculateRotationAngleCommand` → `ComputeCadRotationAngleCommand`
- 第 618 行: `TransformSinglePointCommand` → `ExecuteTransformCommand`

### Step 3: 补充 P1Mx/P1My 初始化
**文件**: `Module/Controls/Assembly/CadAlignmentViewModel.cs`
- 在第 83-87 行的 if 块中补充 P1Mx 和 P1My 的初始化

### Step 4: 添加 PairNames 和 PointNames 属性
**文件**: `Module/Controls/Assembly/CadAlignmentViewModel.cs`
- 新增 `PairNames` 属性（`ObservableCollection<string>` 或 `List<string>`），基于 CorrespondencePoints 生成点对名称
- 新增 `PointNames` 属性（`ObservableCollection<string>` 或 `List<string>`），基于 CorrespondencePoints 生成可选点位名称
- 在 CorrespondencePoints 变化时刷新这两个集合

### Step 5: 编译验证
- `dotnet build Module/Module.csproj` 确认无错误

## 不修改的内容
- FitPoints 的 DataGrid 编辑功能保持不变（用户仍可手动编辑）
- CorrespondencePoint 模型类不变
- 回转中心拟合算法不变
- 全局偏移计算公式不变

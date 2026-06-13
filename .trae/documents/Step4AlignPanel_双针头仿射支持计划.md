# Step4AlignPanel 仿射模式双针头支持计划

## 概述

当前仿射模式是纯 2D 变换，不支持针头选择。由于两个针头在 XY 和 Z 方向都有偏移，需要为每个针头独立进行仿射标定，各自维护标定点列表和变换参数。

## 现状分析

| 项目 | 当前状态 | 需要改动 |
|------|---------|---------|
| AffineCalibrationPoint | 无针头字段，无 Z 轴字段 | 添加 MachineDz1/MachineDz2 字段 |
| 仿射面板 UI | 无针头选择器 | 添加 Dz1/Dz2 选择器（与逐点映射一致） |
| 仿射标定数据 | 单一列表 | 改为按针头索引分两组 |
| 仿射变换结果 | 单一 AffineResult | 改为按针头索引分两组 |
| 仿射示教 | 只读 Dx/Dy | 增加读取 Dz，根据针头选择写入 Dz1/Dz2 |
| 应用变换 | 只设 MachineX/Y | 增加根据针头选择设 MachineZ |
| 保存/加载 | 单一列表 | 保存两组标定点和两组变换结果 |

## 具体改动

### 1. AffineCalibrationPoint 模型 — 添加 Z 轴字段

**文件**: `Core\Models\AffineCalibrationPoint.cs`

- 添加 `MachineDz1` (double) — 针头1 Z 轴机械坐标
- 添加 `MachineDz2` (double) — 针头2 Z 轴机械坐标
- 添加 `CurrentNeedleIndex` (int) — 当前针头索引
- 添加 `CurrentMachineDz` (计算属性) — 根据 CurrentNeedleIndex 返回 Dz1 或 Dz2
- 添加 `ResidualZ` (double) — Z 轴残差（可选，用于质量评估）

### 2. CadPointEditorViewModel — 仿射数据按针头分组

**文件**: `Module\Controls\Cad\CadPointEditorViewModel.cs`

**属性改动**:
- `AffineCalibrationPoints` → 改为根据 `CurrentNeedleIndex` 切换显示对应针头的列表
- 新增 `AffineCalibrationPointsNeedle1` (ObservableCollection<AffineCalibrationPoint>) — 针头1标定点
- 新增 `AffineCalibrationPointsNeedle2` (ObservableCollection<AffineCalibrationPoint>) — 针头2标定点
- `AffineResult` → 改为根据 `CurrentNeedleIndex` 返回对应针头的结果
- 新增 `AffineResultNeedle1` (AffineResult) — 针头1变换结果
- 新增 `AffineResultNeedle2` (AffineResult) — 针头2变换结果
- `HasAffineResult` → 根据当前针头判断
- `AffineQualityText` / `AffineResultDisplay` → 根据当前针头结果

**CurrentNeedleIndex 改动**:
- setter 中增加：切换针头时同步切换 `AffineCalibrationPoints` 引用和 `AffineResult` 显示
- 仿射面板和逐点映射面板共用同一个 `CurrentNeedleIndex`

**命令改动**:
- `AddAffinePointCommand` → 添加到当前针头的列表
- `DeleteAffinePointCommand` → 从当前针头列表删除
- `PickAffineCadCoordCommand` → 不变（CAD 坐标与针头无关）
- `TeachAffineMachineCoordCommand` → 增加读取 Dz 轴，根据 CurrentNeedleIndex 写入 Dz1/Dz2
- `ComputeAffineTransformCommand` → 计算当前针头的变换参数，保存到对应针头结果
- `ApplyTransformToSegmentsCommand` → 根据当前针头选择应用对应变换参数和 Z 值

### 3. Step4AlignPanel.xaml — 仿射面板添加针头选择器

**文件**: `Module\Controls\Cad\Step4AlignPanel.xaml`

在仿射标定面板（N点仿射模式卡片内）的 DataGrid 上方，添加与逐点映射面板一致的针头选择器：
```xml
<materialDesign:Card Margin="0,8,0,0" Padding="8,6">
    <StackPanel Orientation="Horizontal">
        <materialDesign:PackIcon Kind="Needle" .../>
        <TextBlock Text="{lang:Lang Step4_Label_NeedleSelect}" .../>
        <RadioButton Content="Dz1" IsChecked="{Binding IsNeedle1Selected}" GroupName="Step4AffineNeedleSelector" .../>
        <RadioButton Content="Dz2" IsChecked="{Binding IsNeedle2Selected}" GroupName="Step4AffineNeedleSelector" .../>
    </StackPanel>
</materialDesign:Card>
```

仿射 DataGrid 增加列：
- `MachineDz1` 列 — 针头1 Z 坐标（只读或可编辑）
- `MachineDz2` 列 — 针头2 Z 坐标（只读或可编辑）

### 4. 仿射变换应用 — 支持 Z 轴

**文件**: `Module\Controls\Cad\CadPointEditorViewModel.cs` — `ExecuteApplyTransformToSegments`

当前逻辑：
```csharp
pt.MachineX = mx;
pt.MachineY = my;
// MachineZ 未设置
```

改为：
```csharp
pt.MachineX = mx;
pt.MachineY = my;
// 根据当前针头选择，从仿射标定点中取平均 Z 值或使用标定点 Z 值
pt.MachineZ = CurrentNeedleIndex == 0 ? avgDz1 : avgDz2;
```

Z 值策略：使用当前针头所有标定点的 Z 坐标平均值作为该针头的 Z 基准高度。

### 5. CoordinateAlignData — 保存/加载双针头仿射数据

**文件**: `Core\Models\CoordinateAlignData.cs`

```csharp
public class CoordinateAlignData
{
    public string AlignMode { get; set; } = "Affine";
    public List<AffineCalibrationPoint> AffineCalibrationPointsNeedle1 { get; set; }
    public List<AffineCalibrationPoint> AffineCalibrationPointsNeedle2 { get; set; }
    public List<PointMappingPoint> PointMappingPoints { get; set; }
    public AffineResultData AffineResultDataNeedle1 { get; set; }
    public AffineResultData AffineResultDataNeedle2 { get; set; }
    public int CurrentNeedleIndex { get; set; }
}
```

保存/加载逻辑相应更新。

### 6. 多语言资源

**文件**: `MainApp\Languages\Strings.zh-CN.xaml` / `Strings.en-US.xaml`

新增键：
- `Step4_Label_NeedleSelect` — "针头选择" / "Needle Select"
- `Step4_Affine_Col_Dz1` — "Dz1" / "Dz1"
- `Step4_Affine_Col_Dz2` — "Dz2" / "Dz2"
- `Step4_Affine_Needle1Result` — "针头1变换结果" / "Needle 1 Transform Result"
- `Step4_Affine_Needle2Result` — "针头2变换结果" / "Needle 2 Transform Result"

## 实现顺序

1. 修改 `AffineCalibrationPoint` 模型（添加 Dz1/Dz2 字段）
2. 修改 `CadPointEditorViewModel`（仿射数据按针头分组、示教读 Z、计算/应用变换支持 Z）
3. 修改 `Step4AlignPanel.xaml`（仿射面板添加针头选择器和 Dz 列）
4. 修改 `CoordinateAlignData`（保存/加载双针头数据）
5. 更新多语言资源
6. 编译验证

## 验证步骤

1. 编译通过 0 错误
2. 仿射面板显示针头选择器
3. 切换针头时标定点列表正确切换
4. 示教时正确读取并写入对应针头的 Z 值
5. 两个针头可独立计算仿射变换
6. 应用变换时根据针头选择正确设置 MachineZ
7. 保存/加载配置后双针头数据正确恢复

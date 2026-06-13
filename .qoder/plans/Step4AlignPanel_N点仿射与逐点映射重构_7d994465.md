# Step4AlignPanel N点仿射与逐点映射重构

## 改造范围
- **Step4AlignPanel** (CadPointEditorControl 6步流程) — 移除单基准点偏移，保留 N点仿射 + 逐点映射两种模式
- **Step5SimulatePanel** / **Step6ExecutePanel** — 增加双针头切换
- **CadPointEditorViewModel** — 新增仿射标定点集合、逐点映射集合、双针头属性
- **Core层** — 新增 PointMappingPoint 模型类

---

## Task 1: 移除单基准点偏移模式

### 1.1 Step4AlignPanel.xaml
- 移除 FirstPointOffset RadioButton (行37-52)
- 移除图纸基准点A Card (行92-122) — N点仿射不需要单独的基准点输入
- 移除机械基准点A Card (行124-160) — 机械坐标改为每行独立示教
- 移除方向点距离 Card (行162-172) — N点仿射不需要此参数
- 保留自动计算按钮但改为"计算仿射变换"

### 1.2 CadPointEditorViewModel.cs
- 移除 `IsModeFirstPoint` 属性和 setter 逻辑
- 移除 `IsModeFirstPoint` → `AlignMode.FirstPoint` 分支
- 移除 `MapFiducialX/Y/Z`, `MachineFidX/Y/Z/Rx/Rz` 等单基准点属性
- 移除 `TeachMapFiducialCommand`, `TeachMachineFiducialCommand`
- 修改 `AlignMode` enum: 移除 `FirstPoint`，保留 `Affine` 和 `AllPoints`（改为 `PointMapping`）
- 修改默认 `_alignMode = AlignMode.Affine`

### 1.3 多语言资源
- 移除 `Step4_Mode_FirstPointOffset`, `Step4_Mode_FirstPointDesc`, `Step4_Mode_FirstPointDetail` 等键

---

## Task 2: N点仿射模式（参考CadAlignmentView实现）

### 2.1 新增模型 — AffineCalibrationPoint (移到 Core\Models)
将 `CadAlignmentViewModel` 内部的 `AffineCalibrationPoint` 类提取到 `Core\Models\AffineCalibrationPoint.cs`:
```csharp
public class AffineCalibrationPoint : BindableBase
{
    public int Index { get; set; }
    public string Name { get; set; }
    public double CadX { get; set; }
    public double CadY { get; set; }
    public double MachineX { get; set; }
    public double MachineY { get; set; }
    public double Residual { get; set; }
}
```
CadAlignmentViewModel 中改为引用 Core\Models 版本。

### 2.2 CadPointEditorViewModel.cs — N点仿射属性与方法
新增属性:
- `ObservableCollection<AffineCalibrationPoint> AffineCalibrationPoints`
- `double AffineA/B/C/D/Tx/Ty, AffineRmsError`
- `string AffineQualityText`
- `AffineCalibrationResult _affineResult`

新增命令:
- `AddAffinePointCommand` — 添加空行
- `DeleteAffinePointCommand` — 删除指定行（最少3点）
- `TeachAffineMachineCoordCommand` — 示教机械坐标（双针头: Dz1→Dx(8)/Dy(6)/Dz₂, Dz2→Dx/Dy/Dz₃）
- `PickAffineCadCoordFromCanvasCommand` — 从画布选取CAD坐标
- `ComputeAffineTransformCommand` — 计算N点仿射变换

新增方法:
- `OnComputeAffineTransform()` — 调用 `AffineCalibrationService.Solve()` + 更新结果显示

### 2.3 Step4AlignPanel.xaml — N点仿射UI
仿射模式下显示:
```xml
<!-- N点仿射标定 DataGrid -->
<DataGrid ItemsSource="{Binding AffineCalibrationPoints}" ...>
    <DataGridTextColumn Header="Pt" Binding="{Binding Name}" />
    <DataGridTextColumn Header="CAD X" Binding="{Binding CadX}" />
    <DataGridTextColumn Header="CAD Y" Binding="{Binding CadY}" />
    <DataGridTextColumn Header="Mach X" Binding="{Binding MachineX}" IsReadOnly="True"/>
    <DataGridTextColumn Header="Mach Y" Binding="{Binding MachineY}" IsReadOnly="True"/>
    <DataGridTextColumn Header="Residual" Binding="{Binding Residual}" IsReadOnly="True"/>
    <!-- 每行操作按钮列: 从画布选取 / 示教机械 / 删除 -->
</DataGrid>
<StackPanel> <!-- 添加点按钮 + 计算按钮 --> </StackPanel>
<!-- 仿射结果面板 (A/B/C/D/Tx/Ty + RMS) -->
```

### 2.4 画布选取CAD坐标集成
- CadPointEditorControl 已有 HalconCanvasControl 画布（Step1 导入DXF后可见）
- ViewModel 需要新增 `_isPickingAffineCadCoord` 状态
- 画布点击时根据状态将坐标写入选中行的 `CadX/CadY`

---

## Task 3: 逐点映射模式（含双针头）

### 3.1 新增模型 — PointMappingPoint (Core\Models)
```csharp
public class PointMappingPoint : BindableBase
{
    public int Index { get; set; }
    public string Name { get; set; }
    public double CadX { get; set; }    // 画布选取
    public double CadY { get; set; }    // 画布选取
    public double MachineDx { get; set; } // 示教Dx轴
    public double MachineDy { get; set; } // 示教Dy轴
    public double MachineDz1 { get; set; } // Dz₁(Dz₂) — 针头1
    public double MachineDz2 { get; set; } // Dz₂(Dz₃) — 针头2
    // 当前针头对应的Z
    public double CurrentMachineDz => CurrentNeedleIndex == 0 ? MachineDz1 : MachineDz2;
    public int CurrentNeedleIndex { get; set; } // 0=Dz1, 1=Dz2
}
```

### 3.2 CadPointEditorViewModel.cs — 逐点映射属性与方法
新增属性:
- `ObservableCollection<PointMappingPoint> PointMappingPoints`
- `int CurrentNeedleIndex` (0=Dz1/针头1, 1=Dz2/针头2) — 全局针头切换

新增命令:
- `AddMappingPointCommand` — 添加空行
- `DeleteMappingPointCommand` — 删除行
- `PickMappingCadCoordCommand` — 画布选取CAD坐标
- `TeachMappingMachineCoordCommand` — 示教机械坐标(根据当前针头)

### 3.3 Step4AlignPanel.xaml — 逐点映射UI
逐点映射模式下显示:
```xml
<!-- 针头选择器 -->
<StackPanel Orientation="Horizontal" Visibility="{Binding IsModePointMapping, ...}">
    <RadioButton Content="Dz1" IsChecked="{Binding IsNeedle1Selected}" />
    <RadioButton Content="Dz2" IsChecked="{Binding IsNeedle2Selected}" />
</StackPanel>
<!-- 逐点映射 DataGrid -->
<DataGrid ItemsSource="{Binding PointMappingPoints}" ...>
    <DataGridTextColumn Header="Pt" Binding="{Binding Name}" />
    <DataGridTextColumn Header="CAD X" Binding="{Binding CadX}" />
    <DataGridTextColumn Header="CAD Y" Binding="{Binding CadY}" />
    <DataGridTextColumn Header="Mach Dx" Binding="{Binding MachineDx}" IsReadOnly="True"/>
    <DataGridTextColumn Header="Mach Dy" Binding="{Binding MachineDy}" IsReadOnly="True"/>
    <DataGridTextColumn Header="Mach Dz1" Binding="{Binding MachineDz1}" IsReadOnly="True"/>
    <DataGridTextColumn Header="Mach Dz2" Binding="{Binding MachineDz2}" IsReadOnly="True"/>
    <!-- 每行操作按钮列: 从画布选取 / 示教机械 / 删除 -->
</DataGrid>
```

---

## Task 4: Step5/Step6 双针头切换

### 4.1 CadPointEditorViewModel.cs — 双针头属性
- `CurrentNeedleIndex` 属性（0=Dz1, 1=Dz2）
- `IsNeedle1Selected`, `IsNeedle2Selected` RadioButton绑定属性
- 切换针头时更新 Z 轴 ID: Dz1→AxisDz₂(logicalId=4), Dz2→AxisDz₃(logicalId=5)

### 4.2 Step5SimulatePanel.xaml
在执行模式选择Card上方添加针头选择器:
```xml
<StackPanel Orientation="Horizontal" Margin="0,0,0,8">
    <TextBlock Text="针头选择" VerticalAlignment="Center" Margin="0,0,8,0"/>
    <RadioButton Content="Dz1" IsChecked="{Binding IsNeedle1Selected}" GroupName="NeedleSelector" />
    <RadioButton Content="Dz2" IsChecked="{Binding IsNeedle2Selected}" GroupName="NeedleSelector" />
</StackPanel>
```

### 4.3 Step6ExecutePanel.xaml
同上，在目标线段选择Card下方添加针头选择器。
点胶执行时根据 CurrentNeedleIndex 选择 Z 轴。

---

## Task 5: 多语言资源更新

### Strings.zh-CN.xaml
- `Step4_Mode_Affine`: "两点仿射" → "N点仿射"
- `Step4_Mode_AffineDesc`: "（自动方向点）" → "（>=3点标定）"
- `Step4_Mode_AffineDetail`: 更新描述
- `Step4_Mode_PointMapping`: 保持 "逐点映射"
- 新增: Step4_Btn_AddAffinePoint, Step4_Btn_PickFromCanvas, Step4_Btn_TeachMachine, ...
- 新增: Step5_NeedleSelector, Step6_NeedleSelector 相关键

### Strings.en-US.xaml
- 同步英文翻译

---

## Task 6: 保存/加载配置支持

### CadPointEditorViewModel 保存逻辑
- 保存配置时序列化 AffineCalibrationPoints 和 PointMappingPoints
- 加载配置时反序列化恢复
- AffineCalibrationResult 也需保存（6个参数）

---

## Task 7: 构建验证

- `dotnet build` 0 Error
- 确认所有多语言键完整
- 验证 N点仿射计算正确（使用 AffineCalibrationService）
- 验证画布选取和轴示教流程
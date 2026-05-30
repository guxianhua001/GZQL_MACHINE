# 实现计划：段数设置 + 坐标对齐升级 + 变换后坐标观察

## 需求概述

1. **Step3 编辑参数**：生成的线段要能设置段数（即一个 CadEntity 可拆分为 N 个 DispenseSegment）
2. **Step4 坐标对齐**：根据升级后的 UI 实现仿射对齐模式（自动生成方向点 B + Halcon 仿射矩阵计算）
3. **变换后坐标可观察**：自动计算坐标变换矩阵后的点坐标可观察，但不能太占用空间

***

## 需求1：Step3 线段段数设置

### 分析

当前每个 CadEntity 生成 1 个 DispenseSegment，离散化后所有点都在同一个段内。用户需要将一个长线段（如圆弧）拆分为 N 个小段，**所有小段共享同一组点胶参数**。拆分的目的是运动控制的多段插补——每段控制一段位移，而非为了不同参数。

关键设计决策：
- 拆分后的 N 个小段在 DataGrid 中**显示为 1 行**（合并显示，不增加行数）
- 工艺参数只设一组，修改时对所有子段生效
- DispenseSegment 内部维护子段列表，对外表现为一个整体

### 实现步骤

#### 1.1 DispenseSegment 添加子段支持

**文件**: `Core/Models/DispenseSegment.cs`

- 添加 `int SubSegmentCount` 属性（子段数量，默认 1，表示未拆分）
- 添加 `List<List<CadPoint>> SubSegmentPoints` 属性（子段点列表，每个子段独立一组点）
- 修改 `Points` 属性逻辑：当 SubSegmentCount > 1 时，Points 返回所有子段的合并点集（保持向后兼容）
- 添加 `List<CadPoint> GetSubSegmentPoints(int index)` 方法（获取指定子段的点集）
- 添加 `[JsonIgnore]` 标记到 SubSegmentPoints（序列化时通过 SubSegmentCount + Points 重建）
- 修改序列化/反序列化逻辑：保存时将合并点集 + SubSegmentCount 一起保存，加载时按 SubSegmentCount 重新拆分

#### 1.2 CadPointEditorViewModel 添加段数设置属性与命令

**文件**: `Module/ViewModels/CadPointEditorViewModel.cs`

- 添加 `int SegmentSplitCount` 属性（当前选中段的子段数，默认 1）
- 在 `SelectedSegment` setter 中同步 `SegmentSplitCount = SelectedSegment?.SubSegmentCount ?? 1`
- 添加 `DelegateCommand ApplySegmentSplitCommand`（应用段数到当前选中段）

#### 1.3 Step3EditParamsPanel.xaml 添加段数设置 UI

**文件**: `Module/Controls/Step3EditParamsPanel.xaml`

在选中段参数编辑区顶部添加一行：
- TextBlock "段数:" + TextBox 绑定 `SegmentSplitCount`（宽度 50，输入验证 1~100）
- Button "应用" 绑定 `ApplySegmentSplitCommand`
- 提示文字："设置运动插补段数，所有子段共享同一组点胶参数"

#### 1.4 实现拆分逻辑

**文件**: `Module/ViewModels/CadPointEditorViewModel.cs`

`ExecuteApplySegmentSplit()` 逻辑：
1. 获取当前选中段 `SelectedSegment`
2. 读取 `SegmentSplitCount`（N），若 N <= 0 则不操作
3. 将 `SelectedSegment.Points` 按数量均分为 N 份（最后一份包含余数点）
4. 设置 `SelectedSegment.SubSegmentCount = N`
5. 设置 `SelectedSegment.SubSegmentPoints = 拆分后的 N 份点列表`
6. Points 属性保持为所有子段的合并点集（向后兼容图形显示等）
7. 更新 `SelectedSegmentPoints` 触发 DataGrid 刷新

#### 1.5 DataGrid 显示段数信息

**文件**: `Module/Controls/Step3EditParamsPanel.xaml`

- 在现有 DataGrid 中添加 "段数" 列：`DataGridTextColumn Header="段数" Binding="{Binding SubSegmentCount}" Width="45"`
- 当 SubSegmentCount > 1 时显示数字，否则显示 1

***

## 需求2：Step4 坐标对齐升级（仿射模式）

### 分析

当前 Step4 只有两种模式：Mode1（首点偏移/纯平移）和 Mode2（逐点映射）。参考文件要求新增 Mode3（仿射对齐），核心特性：

* 用户只需示教 1 个基准点 A（图纸端 + 机械端各 1 个）

* 方向点 B 自动生成（沿 X 轴偏移固定距离）

* 使用 Halcon `VectorToHomMat2D` 计算仿射矩阵（平移+旋转+缩放）

* 特别适合圆弧轨迹

### 实现步骤

#### 2.1 AlignMode 枚举添加 Affine 模式

**文件**: `Core/Services/ICoordinateAlignService.cs`

* 添加 `Affine` 枚举值到 `AlignMode`

#### 2.2 ICoordinateAlignService 接口扩展

**文件**: `Core/Services/ICoordinateAlignService.cs`

* 添加 `double DirectionLength` 属性（自动生成方向点 B 的偏移距离，默认 100mm）

* 添加 `HHomMat2D AffineMatrix` 属性（Halcon 仿射矩阵，仿射模式下的输出）

* 添加 `void AutoCalculateAffine()` 方法（仿射模式专用计算）

#### 2.3 CoordinateAlignService 实现仿射计算

**文件**: `Core/Services/CoordinateAlignService.cs`

* 添加 `_directionLength` 字段（默认 100.0）

* 添加 `_affineMatrix` 字段（HHomMat2D 类型）

* 实现 `AutoCalculateAffine()`：

  1. 图纸端：自动生成虚拟方向点 B = (MapFiducialX + DirectionLength, MapFiducialY)
  2. 机械端：自动生成虚拟方向点 B = (MachineFiducialX + DirectionLength, MachineFiducialY)
  3. 调用 `HOperatorSet.VectorToHomMat2D` 计算仿射矩阵
  4. 遍历所有注册点，使用 `AffineTransPoint2d` 转换坐标

* 修改 `AutoCalculate()` 方法，根据当前模式分发到不同计算逻辑

#### 2.4 CadPointEditorViewModel 添加仿射模式属性

**文件**: `Module/ViewModels/CadPointEditorViewModel.cs`

* 添加 `bool IsModeAffine` 属性（仿射模式单选按钮绑定）

* 添加 `double DirectionLength` 属性（方向点距离，默认 100.0）

* 添加 `string TransformStatus` 属性（变换状态提示文本）

* 添加 `ICommand ShowSvgCommand`（弹出示意图窗口）

* 修改 `IsAutoCalculateEnabled`：仿射模式下也可用

* 修改 `ExecuteAutoCalculate()`：根据模式分发到不同计算逻辑

#### 2.5 Step4AlignPanel.xaml 升级 UI

**文件**: `Module/Controls/Step4AlignPanel.xaml`

按照参考文件的 UI 设计升级：

* 标题区添加 "查看示意图" 按钮（绑定 `ShowSvgCommand`）

* 对齐模式选择区添加 Mode3（仿射对齐）RadioButton

* 图纸基准点 A 卡片：保持现有（X/Y/Z + "从画布选取"按钮）

* 机械基准点 A 卡片：保持现有（X/Y/Z/Rx/Rz + "示教当前位置"按钮）

* 添加方向点距离输入框（绑定 `DirectionLength`，仿射模式可见）

* 自动计算按钮：三种模式都可用

* 添加状态提示 TextBlock（绑定 `TransformStatus`）

#### 2.6 添加示意图弹出窗口

**文件**: 新建 `Module/Windows/SvgPopupWindow.xaml` + `.xaml.cs`

* 简单的弹出窗口，显示坐标对齐原理示意图

* 使用 Viewbox + Canvas 绘制示意图（CAD端 + 机械端 + 映射关系）

* 窗口样式：ToolWindow，居中显示，不可调整大小

***

## 需求3：变换后坐标可观察（不占太多空间）

### 分析

坐标变换计算后，用户需要观察 CadPoint 的机械坐标（MachineX/Y/Z），但当前 Step3 的点位 DataGrid 只显示 X/Y/Z（CAD坐标），不显示机械坐标。需要添加机械坐标列，但不能让 DataGrid 过宽。

### 实现步骤

#### 3.1 Step3 点位 DataGrid 添加机械坐标列

**文件**: `Module/Controls/Step3EditParamsPanel.xaml`

在现有序号/X/Y/Z 列后添加机械坐标列：

* `DataGridTextColumn Header="MX" Binding="{Binding MachineX, StringFormat=F2}" Width="50"`

* `DataGridTextColumn Header="MY" Binding="{Binding MachineY, StringFormat=F2}" Width="50"`

* `DataGridTextColumn Header="MZ" Binding="{Binding MachineZ, StringFormat=F2}" Width="50"`

设计要点：

* 使用短列名 MX/MY/MZ 而非 MachineX/MachineY/MachineZ，节省空间

* 列宽固定 50px，使用 F2 格式（2位小数）而非 F3，减少宽度

* MachineX/Y/Z 为 null 时显示空白（未对齐状态），不显示 0

#### 3.2 Step4 添加变换结果摘要

**文件**: `Module/Controls/Step4AlignPanel.xaml`

在自动计算按钮下方添加可折叠的变换结果区域：

* 使用 Expander 控件（默认折叠，不占空间）

* 标题显示简要信息如 "✅ 已转换 120 个点"

* 展开后显示：

  * 变换矩阵参数（Tx/Ty/Tz/Rotation/Scale）一行文本

  * 前 5 个点的 CAD→机械坐标对照表（DataGrid，MaxHeight=120）

* 这样用户需要查看时展开，不需要时折叠不占空间

#### 3.3 CadPointEditorViewModel 添加变换结果属性

**文件**: `Module/ViewModels/CadPointEditorViewModel.cs`

* 添加 `ObservableCollection<CadPoint> TransformedPointsPreview` 属性（前 5 个变换后的点，用于预览显示）

* 添加 `string TransformMatrixDisplay` 属性（矩阵参数文本，如 "Tx=12.3 Ty=-5.1 θ=2.5° S=1.00"）

* 在 `ExecuteAutoCalculate()` 完成后更新这些属性

***

## 文件修改清单

| 文件                                             | 修改类型 | 说明                                           |
| ---------------------------------------------- | ---- | -------------------------------------------- |
| `Core/Models/DispenseSegment.cs`               | 修改   | 添加 SubSegmentCount/SubSegmentPoints 属性 + GetSubSegmentPoints 方法 |
| `Core/Services/ICoordinateAlignService.cs`     | 修改   | 添加 Affine 枚举值 + 接口方法                         |
| `Core/Services/CoordinateAlignService.cs`      | 修改   | 实现仿射矩阵计算逻辑                                   |
| `Module/ViewModels/CadPointEditorViewModel.cs` | 修改   | 添加段数设置、仿射模式、变换结果属性                           |
| `Module/Controls/Step3EditParamsPanel.xaml`    | 修改   | 添加段数设置 UI + 机械坐标列                            |
| `Module/Controls/Step4AlignPanel.xaml`         | 修改   | 升级 UI（仿射模式 + 示意图按钮 + 变换结果）                   |
| `Module/Windows/SvgPopupWindow.xaml`           | 新建   | 坐标对齐原理示意图弹出窗口                                |
| `Module/Windows/SvgPopupWindow.xaml.cs`        | 新建   | 弹出窗口 code-behind                             |

***

## 实施顺序

1. **需求1（段数设置）**→ 先做，因为它是 Step3 的独立功能，不依赖其他修改
2. **需求2（坐标对齐升级）**→ 核心功能，涉及枚举/接口/服务/ViewModel/UI 多层修改
3. **需求3（变换后坐标观察）**→ 最后做，依赖需求2的变换计算结果


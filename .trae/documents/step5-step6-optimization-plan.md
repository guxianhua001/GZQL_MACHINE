# Step5/Step6 功能优化计划

## 问题清单与实施方案

---

### 1. 停止按钮无条件使能（安全优先）

**现状**: `StopSimCommand` 的 `CanExecute` 条件为 `() => _isSimulating`，非仿真时停止按钮灰色不可点。
**问题**: 安全角度，停止按钮应始终可点击（如急停），不应有条件限制。
**方案**:
- `StopSimCommand` 移除 CanExecute 条件，改为 `new DelegateCommand(ExecuteStopSim)`（始终使能）
- Step5SimulatePanel.xaml 中停止按钮移除 `IsEnabled="{Binding IsSimulating}"`
- Step6ExecutePanel.xaml 中也添加"停止"按钮（当前没有），同样无条件使能
- `ExecuteStopSim()` 内部加空判断：`_simCts?.Cancel()`，如果 `_simCts` 为 null 则直接 return

**修改文件**:
- `CadPointEditorViewModel.cs`: StopSimCommand 移除 CanExecute 谓词
- `Step5SimulatePanel.xaml`: 停止按钮移除 IsEnabled 绑定
- `Step6ExecutePanel.xaml`: 添加停止按钮

---

### 2. 保存轨迹段移到第4步

**现状**: 保存/加载轨迹段按钮在 Step1ImportPanel.xaml 中（第45-53行）。
**问题**: 操作流程是"导入→编辑参数→坐标对齐→保存"，保存放在第4步（坐标对齐完成后）更符合操作习惯。
**方案**:
- Step4AlignPanel.xaml 底部添加"保存轨迹段"和"加载轨迹段"按钮
- Step1ImportPanel.xaml 保留"加载轨迹段"按钮（首次加载仍需在Step1），移除"保存轨迹段"按钮
- 保存时同时保存坐标对齐数据（已有此逻辑，无需改动）

**修改文件**:
- `Step4AlignPanel.xaml`: 底部添加保存/加载轨迹段按钮
- `Step1ImportPanel.xaml`: 移除保存按钮，保留加载按钮

---

### 3. 选中段显示示教高度和补偿参数

**现状**: Step3EditParamsPanel 的选中段参数编辑区只有4个参数（速度、胶量、开胶延时、收胶延时），缺少 TeachHeight、HeightCompensation、EffectiveZHeight。
**方案**:
- 在参数编辑 Grid 中添加3行：
  - 示教高度 (mm): `TextBox` 绑定 `SelectedSegment.TeachHeight` + "示教"按钮绑定 `TeachHeightCommand`
  - 高度补偿 (mm): `TextBox` 绑定 `SelectedSegment.HeightCompensation`
  - 有效高度 (mm): `TextBlock` 只读显示 `SelectedSegment.EffectiveZHeight`
- DataGrid 中也添加"示教高度"和"补偿"列

**修改文件**:
- `Step3EditParamsPanel.xaml`: 参数编辑区增加3行 + DataGrid 增加2列

---

### 4. 点胶参数显示单位

**现状**: DataGrid 列头和参数编辑区 Label 均未显示单位。
**方案**:
- DataGrid 列头添加单位：
  - "速度" → "速度(mm/s)"
  - "胶量" → "胶量(相对值)"
  - "长度" → "长度(mm)"
  - "点数" 保持不变
- 参数编辑区 Label 添加单位：
  - "运动速度:" → "运动速度(mm/s):"
  - "出胶量:" → "出胶量(相对值):"
  - "开胶延时:" → "开胶延时(ms):"
  - "收胶延时:" → "收胶延时(ms):"
  - "示教高度:" → "示教高度(mm):"
  - "高度补偿:" → "高度补偿(mm):"
  - "有效高度:" → "有效高度(mm):"

**修改文件**:
- `Step3EditParamsPanel.xaml`: 修改列头和 Label 文本

---

### 5. Step5 取消真实点胶选项，移到 Step6；目标工位绑定线段ID

**现状**:
- Step5 有3种模式：空跑仿真、真实空跑、真实点胶
- Step6 的目标工位下拉框绑定硬编码的 SiteNames（ASSY_001~006）
**方案**:
- Step5 只保留2种模式：空跑仿真（UI模拟）、真实空跑（运动不出胶）
  - 移除 `IsRealDispenseMode` RadioButton
  - `ExecuteRun()` 中移除真实点胶分支
- Step6 添加"真实点胶"模式选项
  - Step6 的执行选项中添加模式选择：真实空跑 / 真实点胶
  - `ExecutePath()` 根据模式调用不同服务方法
- Step6 的目标工位下拉框改为绑定 Segments 的 SegmentId
  - `SiteNames` 改为动态生成：`Segments.Select(s => s.SegmentId).ToList()`
  - 选中某个 SegmentId 时，设置 `SelectedSegment` 使对应线段在画布上高亮

**修改文件**:
- `Step5SimulatePanel.xaml`: 移除真实点胶 RadioButton
- `Step6ExecutePanel.xaml`: 添加执行模式选择，修改工位下拉框
- `CadPointEditorViewModel.cs`:
  - 移除 `IsRealDispenseMode` 属性
  - `SiteNames` 改为动态绑定 Segments 的 SegmentId
  - `SiteName` setter 中设置 `SelectedSegment` 实现高亮
  - Step6 添加执行模式属性

---

### 6. 单点执行模式仅限只有1个点的线段

**现状**: `ExecuteSinglePointCommand` 的 CanExecute 条件为 `() => CanExecute`（有启用段且非仿真中即可），没有检查点数。
**方案**:
- CanExecute 条件增加：`SelectedSegment != null && SelectedSegment.SamplePointCount == 1`
- 或者：当启用单点模式时，只执行 `Segments.Where(s => s.IsEnabled && s.SamplePointCount == 1)` 的段
- 在 Step6 的"单点执行模式" CheckBox 旁添加说明文字："仅限只有1个点的线段"

**修改文件**:
- `CadPointEditorViewModel.cs`: ExecuteSinglePointCommand 的 CanExecute 增加点数检查
- `Step6ExecutePanel.xaml`: 单点执行模式旁添加说明

---

### 7. Z高度校正使用说明

**现状**: Step6 有"启用 Z 高度校正" CheckBox，但没有说明其用途和工作原理。
**方案**:
- 在 Z 高度校正 CheckBox 下方添加说明文字：
  - "启用后，执行时将使用每段的示教高度+补偿值作为Z轴目标位置，而非固定安全高度"
  - "需要先在第3步中示教各段高度，否则将使用默认Z高度"
- Z高度校正的实际逻辑：当 `ZCorrectionEnabled=true` 时，执行服务使用 `EffectiveZHeight`（= TeachHeight + HeightCompensation）作为 Z 轴目标；否则使用 `ZHeight` 或固定安全高度

**修改文件**:
- `Step6ExecutePanel.xaml`: Z校正 CheckBox 下方添加说明文字

---

### 8. 删除单点执行按钮（保留单点模式选项）

**现状**: Step6 中单点执行有两个入口：
- CheckBox: `Content="单点执行模式（仅当前位置出胶一次）"` + `Command="{Binding ExecuteSinglePointCommand}"`
- Button: `Content="🔘 单点执行"` + `Command="{Binding ExecuteSinglePointCommand}"`
**问题**: CheckBox 用 Command 而非 IsChecked 绑定，逻辑混乱；按钮和 CheckBox 功能重复。
**方案**:
- 删除底部的"🔘 单点执行"按钮
- 将 CheckBox 改为正确的 IsChecked 绑定：`IsChecked="{Binding IsSinglePointMode}"`
- 新增 `IsSinglePointMode` 属性，作为执行模式标志
- "执行走胶"按钮点击时，根据 `IsSinglePointMode` 决定是执行完整路径还是单点
- 单点模式下，CanExecute 需要检查存在只有1个点的启用段

**修改文件**:
- `Step6ExecutePanel.xaml`: 删除单点执行按钮，修改 CheckBox 为 IsChecked 绑定
- `CadPointEditorViewModel.cs`: 新增 `IsSinglePointMode` 属性，修改 `ExecutePath()` 逻辑

---

## 实施顺序

1. **停止按钮无条件使能** — 安全优先，最先改
2. **参数显示单位** — 简单文本修改
3. **选中段显示示教高度和补偿** — UI 扩展
4. **保存轨迹段移到第4步** — UI 调整
5. **Step5/Step6 模式重构** — 核心逻辑变更
6. **单点执行模式优化** — 依赖第5步
7. **Z高度校正说明** — 文本添加
8. **删除单点执行按钮** — 依赖第6步

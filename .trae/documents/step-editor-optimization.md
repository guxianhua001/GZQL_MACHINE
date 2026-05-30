# 步骤剪辑器优化计划

## 需求概述

1. **最近文件列表**：序列文件下拉选项显示最近使用过的文件列表，可快速切换
2. **任务重命名**：当前任务名称默认 `Task 1` 递增序号，不具备可识别性，需支持重命名但不影响功能

---

## 需求 1：最近文件列表

### 现状分析

- 当前仅记录**单条**最近路径 `LastProcessSequencePath`，存储在 `AppSettings.ExtensionData` 中
- 加载文件通过 `OpenFileDialog` 手动选择，无历史记录
- UI 中 Step 1 区域只显示当前文件名，无下拉切换能力

### 设计方案

#### 数据层：MRU 列表存储

在 `AppSettings.ExtensionData` 中新增键 `RecentProcessSequencePaths`，存储最近文件路径列表（最多 10 条）：

```json
"RecentProcessSequencePaths": [
  "C:\\...\\Config\\ProcessSequences\\Default_20260521_160442.json",
  "C:\\...\\Config\\ProcessSequences\\Default_20260520_143000.json"
]
```

**关键规则**：
- 最大条目数 10，超出时移除最旧的
- 加载/保存文件时，将路径插入列表头部（若已存在则移到头部）
- 文件不存在时从列表中移除

#### 服务层：ProcessSequenceService 扩展

在 `ProcessSequenceService` 中新增：

| 方法/属性 | 说明 |
|-----------|------|
| `ObservableCollection<string> RecentFiles { get; }` | 最近文件列表（用于 UI 绑定） |
| `void RecordRecentFile(string filePath)` | 记录文件到 MRU 列表并持久化 |
| `List<string> LoadRecentFilesFromSettings()` | 从 ExtensionData 读取 MRU 列表 |
| `void SaveRecentFilesToSettings()` | 将 MRU 列表持久化到 ExtensionData |

修改现有方法：
- `RecordLastSequencePath()` → 改为调用 `RecordRecentFile()`，保留向后兼容
- `LoadSequenceFromPathAsync()` → 加载后调用 `RecordRecentFile()`
- `SaveSequenceToPathAsync()` → 保存后调用 `RecordRecentFile()`
- `AutoLoadLastSequenceAsync()` → 初始化时同时加载 MRU 列表到 `RecentFiles`

#### ViewModel 层：ProcessSequenceEditorViewModel 扩展

新增属性：
- `ObservableCollection<string> RecentFiles` — 代理到 `_sequenceService.RecentFiles`
- `string SelectedRecentFile` — 选中的最近文件，setter 触发加载

新增命令：
- `SwitchToRecentFileCommand` — 切换到选中的最近文件

#### UI 层：ProcessSequenceEditorView.xaml 修改

在 Step 1 区域（序列文件卡片）中，将当前文件名显示改为下拉 ComboBox：

```xml
<!-- 替换原有的 Run 文件名显示 -->
<ComboBox ItemsSource="{Binding RecentFiles}"
          SelectedItem="{Binding SelectedRecentFile}"
          Width="250"
          FontSize="12"
          materialDesign:HintAssist.Hint="当前序列文件" />
```

保留原有的"加载"和"保存"按钮不变。

---

## 需求 2：任务重命名

### 现状分析

- `TaskItem.Name` 属性已有 `SetProperty` 支持，可修改
- `AddTask()` 中硬编码 `$"Task {Tasks.Count + 1}"` 作为默认名
- UI 中任务 ComboBox 使用 `DisplayMemberPath="Name"` 显示名称
- 任务名在 JSON 序列化/反序列化中正确保存和恢复
- **关键约束**：任务名仅用于显示标识，不参与运动控制逻辑（运动控制层通过 `StationTaskBase` 的 `TaskId` 和 `StationIdentifierValue` 识别工站）

### 设计方案

#### UI 层：任务 ComboBox 改为可编辑

将任务选择 ComboBox 从只读改为可编辑模式，支持直接在 ComboBox 中重命名：

```xml
<ComboBox Width="130"
          ItemsSource="{Binding Tasks}"
          DisplayMemberPath="Name"
          SelectedItem="{Binding CurrentTask}"
          IsEditable="True"
          IsReadOnly="False"
          FontSize="12" />
```

**优势**：
- 最小改动，利用 MaterialDesign ComboBox 的内置编辑能力
- 用户直接在 ComboBox 中修改名称，体验自然
- `TaskItem.Name` 已有 `SetProperty`，修改后自动通知 UI 更新

#### ViewModel 层：添加 RenameTaskCommand（备选方案）

如果 ComboBox 内联编辑体验不够好，可添加重命名命令：

- `RenameTaskCommand` — 弹出输入对话框，输入新名称
- 对话框使用现有 `ConfirmationDialog` 或新建简单输入对话框

**推荐**：先用 ComboBox 可编辑模式，如果体验不满意再升级为对话框。

#### 安全约束

- 不允许重命名为空字符串
- 不允许与已有任务名重复
- 默认任务（`IsDefault=true`）也允许重命名（名称仅标识用途）
- 重命名后自动保存序列，防止丢失

---

## 修改文件清单

| 文件 | 改动 |
|------|------|
| `Module/Services/IProcessSequenceService.cs` | 新增 `RecentFiles` 属性、`RecordRecentFile` 方法 |
| `Module/Services/ProcessSequenceService.cs` | 实现 MRU 列表逻辑，修改 `RecordLastSequencePath`/`LoadSequenceFromPathAsync`/`SaveSequenceToPathAsync`/`AutoLoadLastSequenceAsync` |
| `Module/Controls/StepEditor/ProcessSequenceEditorViewModel.cs` | 新增 `RecentFiles`/`SelectedRecentFile` 属性，`SwitchToRecentFileCommand` |
| `Module/Controls/StepEditor/ProcessSequenceEditorView.xaml` | Step 1 区域新增最近文件 ComboBox；Step 2 任务 ComboBox 改为可编辑 |

---

## 实施步骤

1. **ProcessSequenceService** — 新增 MRU 列表数据结构和持久化方法
2. **ProcessSequenceService** — 修改现有保存/加载方法，集成 MRU 记录
3. **IProcessSequenceService** — 接口新增 MRU 相关成员
4. **ProcessSequenceEditorViewModel** — 新增 RecentFiles/SelectedRecentFile 绑定
5. **ProcessSequenceEditorView.xaml** — Step 1 新增最近文件下拉；Step 2 任务 ComboBox 改为可编辑
6. **验证构建**

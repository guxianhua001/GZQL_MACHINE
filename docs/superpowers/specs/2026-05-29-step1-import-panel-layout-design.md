# Step1ImportPanel 布局调整设计

## 目标
将 Step1ImportPanel 中"轨迹段加载"从单个按钮升级为与 DXF 导入相同的卡片式布局，加载后显示当前文件名称。

## 当前布局问题
- DXF 导入有完整的文件路径+浏览+导入卡片
- 轨迹段加载只有一个按钮，需要弹 OpenFileDialog
- 两者交互方式不一致，用户体验不统一
- 加载后无法看到当前使用的文件名

## 目标布局

```
┌─ DXF 文件导入 ─────────────────────┐
│ [文件路径输入框(只读)] [浏览]       │
│ [导入 DXF]                         │
└─────────────────────────────────────┘

┌─ 轨迹段加载 ───────────────────────┐
│ [文件路径输入框(只读)] [浏览]       │
│ [加载轨迹段]                        │
└─────────────────────────────────────┘

[状态消息区域]
```

## 变更清单

### 1. XAML 布局变更 (Step1ImportPanel.xaml)

- 轨迹段加载区域从 `Separator + TextBlock + Button` 改为 `materialDesign:Card`
- Card 内部结构与 DXF 导入卡片一致：`Grid(TextBox + Button浏览) + Button加载`
- 移除"加载测试轨迹"按钮
- 轨迹段文件路径 TextBox 绑定 `SegmentFilePath`
- 浏览按钮绑定 `SelectSegmentFileCommand`
- 加载按钮绑定 `LoadSegmentsCommand`

### 2. ViewModel 变更 (CadPointEditorViewModel.cs)

**新增属性：**
- `SegmentFilePath` — 轨迹段配置文件路径（双向绑定到输入框）
- `HasSegmentFilePath` — 路径是否非空（控制加载按钮启用状态）

**新增命令：**
- `SelectSegmentFileCommand` — 弹 OpenFileDialog 选择 JSON 文件，选择后更新 SegmentFilePath

**修改命令：**
- `LoadSegmentsCommand` — 从 `SegmentFilePath` 读取路径加载（不再弹 OpenFileDialog）
- `ExecuteLoadSegments` — 改为从 `_segmentFilePath` 读取路径，无路径时提示用户

**修改方法：**
- `TryAutoLoadLastConfig` — 不再自动加载文件，仅将路径填充到 `SegmentFilePath`
- `RecordSegmentConfigPath` — 保存路径时同步更新 `SegmentFilePath`

### 3. 数据流

```
初始化 → RestorePathFromStationParams()
       → SegmentFilePath = 上次路径（仅显示，不自动加载）

用户点击浏览 → SelectSegmentFileCommand
            → OpenFileDialog 选择 JSON
            → SegmentFilePath = 选择路径

用户点击加载 → LoadSegmentsCommand
            → 从 SegmentFilePath 读取文件
            → 加载轨迹段 + 坐标对齐数据
            → RecordSegmentConfigPath(path) → 同步到配方参数
```

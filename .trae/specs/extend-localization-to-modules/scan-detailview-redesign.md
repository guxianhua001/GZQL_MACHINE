# ScanDetailView 布局重构设计

## Why

当前 ScanDetailView 采用垂直 StackPanel 堆叠 7 个 GroupBox，内容密集且不便于操作。需要参考 VisionDetailView 和 ScriptDetailView 的左右分栏布局模式，将配置/编辑区与数据展示区分离，提升操作效率和视觉一致性。

## What Changes

- 将 ScanDetailView 从**垂直堆叠布局**重构为**左右分栏布局**
- 左侧（~65%）：运动配置 + IO配置 + 通讯配置 + 数据解析（脚本+变量映射）
- 右侧（~280px 固定宽度 ScrollViewer）：数据解析面板(11列结果表) + 执行测试 + 提示信息
- 标题栏统一为 VisionDetailView 风格的渐变深色标题栏
- 底部增加操作栏：[测试] | [取消] [保存]

## Impact

- Affected code: `Module/Controls/StepDetails/ScanDetailView.xaml` 仅此文件
- ViewModel 接口零改动（所有 Binding 保持不变）
- 多语言 key 不变（所有 lang:Lang Key 保持不变）

## 设计方案

### 布局结构

```
Grid (2行: 标题栏 + 主内容区)
├── Row 0: Border (标题栏, 与VisionDetailView一致)
│   └── DockPanel: [关闭按钮] [图标] "SCAN" StepDescription
└── Row 1: Grid (2列: 左右分栏)
    ├── Column 0 (*): ScrollViewer → StackPanel
    │   ├── GroupBox 运动配置 (保持原有4行Grid)
    │   ├── GroupBox IO配置 (保持原有)
    │   ├── GroupBox 通讯配置 (保持原有)
    │   └── GroupBox 数据解析 (点数量+模板按钮+脚本编辑器+变量映射DataGrid)
    │
    └── Column 1 (280固定): ScrollViewer → StackPanel
        ├── GroupBox 数据解析面板 (Header含最后解析时间+数据, 11列DataGrid)
        ├── Expander 执行测试 (示例数据输入+执行按钮组+进度条+结果Border)
        └── Border 提示信息 (蓝色提示边框)

### 底部操作栏 (Grid.Row 2 或嵌入主Grid底部)
StackPanel: [测试按钮(左)] ... [取消][保存](右)
```

### 约束条件

1. **Width="960" MaxHeight="800"** — 外部尺寸不变
2. **所有 Binding 不变** — ViewModel 零改动
3. **所有 lang:Lang key 不变** — 多语言不受影响
4. **GroupBox 内部控件布局不变** — Grid行列、DataGrid列定义、NumericUpDown等保持原样
5. **右侧面板使用 Expander** — 执行测试区域可折叠，节省空间
6. **提示信息区** — 与 VisionDetailView 一致的蓝色边框样式

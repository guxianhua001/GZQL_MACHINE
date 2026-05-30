# VisionDetailView 完全对齐 ScriptDetailView 布局风格

## 目标
将 VisionDetailView 的布局结构完全重写，使其与 ScriptDetailView 保持一致的设计模式：**三行Grid（标题栏 / 左右分栏内容区 / 底部操作栏）**，而非当前的 ScrollViewer+纵向卡片堆叠。

## 当前差异分析

| 维度 | ScriptDetailView (参考) | VisionDetailView (当前) |
|------|------------------------|-----------------------|
| **主体布局** | 三列 Grid：左编辑区(*) \| 分隔线(1px) \| 右侧面板(280px固定) | ScrollViewer > StackPanel 纵向堆叠4个Border卡片 |
| **左侧区域** | 脚本说明TextBox + 代码编辑器 + 编译结果区（三行子Grid） | 无左右分栏 |
| **右侧区域** | ScrollViewer > 3个Expander(全局变量/步骤输出参数/执行结果) + 提示区 | 无 |
| **Converter Key** | `BooleanToVisibilityConverter` | `BoolToVis` |
| **底部按钮图标** | 18×18 | 16×16 |
| **底部左侧按钮** | [编译 Outlined] [执行 Raised] + ProgressBar | 仅 [测试 Outlined] |

## 改造步骤

### Step 1: 统一资源 Key
- 文件: `VisionDetailView.xaml`
- 将 `x:Key="BoolToVis"` 改为 `x:Key="BooleanToVisibilityConverter"`（与ScriptDetailView一致）
- 全文所有 `{StaticResource BoolToVis}` 替换为 `{StaticResource BooleanToVisibilityConverter}`

### Step 2: 重构主体为左右分栏 Grid
- 删除外层 `ScrollViewer` + 内层 `StackPanel`
- 替换为与 ScriptDetailView 一致的三列 Grid：
  ```
  Column0 (*)   : 左侧配置区
  Column1 (Auto): 分隔线 Border Width=1
  Column2 (280) : 右侧操作面板
  ```

### Step 3: 左侧区域重构（对应 ScriptDetailView 的代码编辑区）
将当前 4 个纵向卡片重组到左侧 Grid 中：

```
┌─────────────────────────────────────┐
│ [通讯配置] — 保留现有表单           │  Row0: Auto
├─────────────────────────────────────┤
│                                     │
│ [数据解析脚本]                      │  Row1: * (占据剩余空间)
│ ├ 提示文字 + 默认模板按钮            │     MinHeight=130 MaxHeight=∞
│ └ Consolas TextBox 编辑器           │
│                                     │
├─────────────────────────────────────┤
│ [结果输出] — 三态颜色               │  Row2: Auto (MinHeight=28 MaxHeight=120)
│   Consolas TextBlock                │     DataTrigger灰/绿/红
└─────────────────────────────────────┘
```

具体改动：
- 通讯配置：保持现有 Grid 表单不变，作为 Row0
- 数据解析脚本：移除外层 Border 包裹，直接用 ToolBarBackground 标题 + TextBox（占满剩余空间）
- 结果输出区：新增（从"执行测试"区中提取出 ExecuteResult 显示部分），使用 ScriptDetailView 同款三态颜色 Style

### Step 4: 右侧区域重构（对应 ScriptDetailView 的变量引用面板）
使用 ScrollViewer + StackPanel + Expander 模式：

```
┌──────────────────────┐
│ ▼ 变量映射            │  Expander IsExpanded=True
│   [DataGrid]         │  SourceKey / GlobalVariableName
│   [+添加] [-删除]    │
├──────────────────────┤
│ ▼ 执行测试            │  Expander IsExpanded=True
│   测试数据: [______]  │
│   [填充示例]          │
│   [发送触发] [示例执行]│
│   ━━━━━ 进度条 ━━━━━ │
│   [三态结果输出]      │
├──────────────────────┤
│ 💡 提示               │  Border #1A237E08 背景
│   双击变量名可插入...  │
└──────────────────────┘
```

具体改动：
- 变量映射：从左侧卡片移入右侧 Expander，保持 DataGrid 不变
- 执行测试：从左侧卡片移入右侧 Expander，包含测试数据输入、两个执行按钮、ProgressBar、三态结果输出
- 新增提示区：参考 ScriptDetailView 的 API 提示区块样式

### Step 5: 底部操作栏对齐
- Converter Key 对齐后自动生效
- 图标尺寸统一为 18×18（与 ScriptDetailView 一致）
- 左侧按钮调整为：[发送触发 Raised]（主操作）
- 右侧保持：[取消 Outlined] [保存配置 Raised FontWeight=Medium]

### Step 6: 验证构建
- 运行 `dotnet build Module.csproj --no-restore`
- 确保 0 错误

## 不变的部分（已符合规范）
以下元素已在上一轮修改中对齐，无需再改：
- ✅ 顶部渐变标题栏（#2D3748→#3A475A）
- ✅ DockPanel + 关闭按钮布局
- ✅ Badge 标签样式
- ✅ Border + CornerRadius=6 + ClipToBounds 根容器
- ✅ PackIcon 替代 Emoji
- ✅ Consolas 字体用于代码/结果区
- ✅ 三态颜色 DataTrigger（IsExecuteSuccess）
- ✅ 按钮 Height=34, CornerRadius=4
- ✅ Padding="16,10" 底部操作栏

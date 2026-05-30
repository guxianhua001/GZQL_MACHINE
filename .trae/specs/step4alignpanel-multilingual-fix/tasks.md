# Step4AlignPanel 多语言修复实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复 Step4AlignPanel 的多语言实现，解决缺失资源键、键名不匹配、硬编码字符串和 emoji 违规问题

**Architecture:** 最小化修改方案，仅修改 3 个文件：XAML 视图文件（修复绑定）、中文资源文件（新增+清理）、英文资源文件（新增+清理）

**Tech Stack:** WPF, PRISM, MaterialDesignInXaml, 自定义 Lang MarkupExtension

---

### Task 1: 修复 XAML 键名不匹配

**Files:**
- Modify: `Module/Controls/Cad/Step4AlignPanel.xaml:133`

- [ ] **Step 1: 修复示教按钮的资源键名**

将第 133 行的 `Step4_Btn_TeachCurrentPos` 修改为 `Step4_Btn_TeachCurrentPosition` 以匹配资源文件中已定义的键名。

```xml
<!-- 修改前 -->
<Button Grid.Column="1" Content="{lang:Lang Step4_Btn_TeachCurrentPos}" Command="{Binding TeachMachineFiducialCommand}"

<!-- 修改后 -->
<Button Grid.Column="1" Content="{lang:Lang Step4_Btn_TeachCurrentPosition}" Command="{Binding TeachMachineFiducialCommand}"
```

- [ ] **Step 2: 验证修改**

确认资源文件中存在 `Step4_Btn_TeachCurrentPosition` 键：
- Strings.zh-CN.xaml 第 886 行: `<sys:String x:Key="Step4_Btn_TeachCurrentPosition">示教当前位置</sys:String>`
- Strings.en-US.xaml 第 849 行: `<sys:String x:Key="Step4_Btn_TeachCurrentPosition">Teach Current Position</sys:String>`

---

### Task 2: DataGrid 列头多语言化

**Files:**
- Modify: `Module/Controls/Cad/Step4AlignPanel.xaml:194-198`

- [ ] **Step 1: 替换 DataGrid 列头的硬编码字符串**

```xml
<!-- 修改前 -->
<DataGridTextColumn Header="CAD X" Binding="{Binding X, StringFormat=F2}" Width="55"/>
<DataGridTextColumn Header="CAD Y" Binding="{Binding Y, StringFormat=F2}" Width="55"/>
<DataGridTextColumn Header="MX" Binding="{Binding MachineX, StringFormat=F2}" Width="55"/>
<DataGridTextColumn Header="MY" Binding="{Binding MachineY, StringFormat=F2}" Width="55"/>
<DataGridTextColumn Header="MZ" Binding="{Binding MachineZ, StringFormat=F2}" Width="55"/>

<!-- 修改后 -->
<DataGridTextColumn Header="{lang:Lang Step4_Header_CADX}" Binding="{Binding X, StringFormat=F2}" Width="55"/>
<DataGridTextColumn Header="{lang:Lang Step4_Header_CADY}" Binding="{Binding Y, StringFormat=F2}" Width="55"/>
<DataGridTextColumn Header="{lang:Lang Step4_Header_MX}" Binding="{Binding MachineX, StringFormat=F2}" Width="55"/>
<DataGridTextColumn Header="{lang:Lang Step4_Header_MY}" Binding="{Binding MachineY, StringFormat=F2}" Width="55"/>
<DataGridTextColumn Header="{lang:Lang Step4_Header_MZ}" Binding="{Binding MachineZ, StringFormat=F2}" Width="55"/>
```

---

### Task 3: 新增中文资源键

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`

- [ ] **Step 1: 在现有 Step4_ 键区域附近添加缺失的资源键**

在 `Step4_ToolTip_SaveSegments` (第 894 行) 之后添加以下新键：

```xml
<sys:String x:Key="Step4_Section_MapFiducial">图纸基准点 A</sys:String>
<sys:String x:Key="Step4_Section_MachineFiducial">机械基准点 A</sys:String>
<sys:String x:Key="Step4_Section_PreviewTitle">变换结果预览</sys:String>
<sys:String x:Key="Step4_Header_CADX">CAD X</sys:String>
<sys:String x:Key="Step4_Header_CADY">CAD Y</sys:String>
<sys:String x:Key="Step4_Header_MX">机械 X</sys:String>
<sys:String x:Key="Step4_Header_MY">机械 Y</sys:String>
<sys:String x:Key="Step4_Header_MZ">机械 Z</sys:String>
```

---

### Task 4: 清理中文资源中的 Emoji

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`

- [ ] **Step 1: 移除以下键值中的 emoji 字符**

| 行号 | 键名 | 原值 | 新值 |
|------|------|------|------|
| 868 | `Step4_Title` | 🎯 第四步：坐标对齐 | 第四步：坐标对齐 |
| 870 | `Step4_Btn_ViewDiagram` | 📘 查看示意图 | 查看示意图 |
| 883 | `Step4_Label_CadFiducialA` | 📍 图纸基准点 A（轨迹起点/角点） | 图纸基准点 A（轨迹起点/角点） |
| 885 | `Step4_Label_MachineFiducialA` | 🔧 机械基准点 A（唯一需要示教） | 机械基准点 A（唯一需要示教） |
| 887 | `Step4_Label_DirectionDistance` | 📏 方向点距离: | 方向点距离: |
| 889 | `Step4_Btn_CalculateTransform` | 🔄 自动计算坐标变换矩阵 | 自动计算坐标变换矩阵 |
| 893 | `Step4_Btn_SaveSegments` | 💾 保存轨迹段 | 保存轨迹段 |
| 878 | `Step4_Mode_AffineDetail` | ✅ 圆弧轨迹专用｜仅示教 A 点，自动生成方向向量 | 圆弧轨迹专用｜仅示教 A 点，自动生成方向向量 |

---

### Task 5: 新增英文资源键

**Files:**
- Modify: `MainApp/Languages/Strings.en-US.xaml`

- [ ] **Step 1: 在现有 Step4_ 键区域附近添加缺失的英文资源键**

在 `Step4_ToolTip_SaveSegments` (第 857 行) 之后添加以下新键：

```xml
<sys:String x:Key="Step4_Section_MapFiducial">CAD Fiducial A</sys:String>
<sys:String x:Key="Step4_Section_MachineFiducial">Machine Fiducial A</sys:String>
<sys:String x:Key="Step4_Section_PreviewTitle">Transform Preview</sys:String>
<sys:String x:Key="Step4_Header_CADX">CAD X</sys:String>
<sys:String x:Key="Step4_Header_CADY">CAD Y</sys:String>
<sys:String x:Key="Step4_Header_MX">Machine X</sys:String>
<sys:String x:Key="Step4_Header_MY">Machine Y</sys:String>
<sys:String x:Key="Step4_Header_MZ">Machine Z</sys:String>
```

---

### Task 6: 清理英文资源中的 Emoji

**Files:**
- Modify: `MainApp/Languages/Strings.en-US.xaml`

- [ ] **Step 1: 移除以下键值中的 emoji 字符**

| 行号 | 键名 | 原值 | 新值 |
|------|------|------|------|
| 831 | `Step4_Title` | 🎯 Step 4: Coordinate Alignment | Step 4: Coordinate Alignment |
| 833 | `Step4_Btn_ViewDiagram` | 📘 View Diagram | View Diagram |
| 846 | `Step4_Label_CadFiducialA` | 📍 CAD Fiducial A (trajectory start/corner) | CAD Fiducial A (trajectory start/corner) |
| 848 | `Step4_Label_MachineFiducialA` | 🔧 Machine Fiducial A (only one needs teaching) | Machine Fiducial A (only one needs teaching) |
| 850 | `Step4_Label_DirectionDistance` | 📏 Direction Distance: | Direction Distance: |
| 852 | `Step4_Btn_CalculateTransform` | 🔄 Auto-Calculate Transform Matrix | Auto-Calculate Transform Matrix |
| 856 | `Step4_Btn_SaveSegments` | 💾 Save Segments | Save Segments |
| 841 | `Step4_Mode_AffineDetail` | ✅ Arc trajectory only | Teach point A only, auto-generate direction vector | Arc trajectory only \| Teach point A only, auto-generate direction vector |

---

### Task 7: 编译验证

- [ ] **Step 1: 运行编译命令验证无错误**

Run: `dotnet build GZQL_MACHINE.sln`
Expected: Build succeeded

- [ ] **Step 2: 检查是否还有遗留的硬编码或 emoji 问题**

在 Step4AlignPanel.xaml 中搜索硬编码中文字符和 emoji 字符，确认全部已修复。

---

## 自检清单

**Spec 覆盖度：**
- ✅ 缺失资源键补充 → Task 3, Task 5
- ✅ 键名不匹配修复 → Task 1
- ✅ DataGrid 硬编码 → Task 2
- ✅ Emoji 清理 → Task 4, Task 6

**占位符扫描：**
- ✅ 无 TBD/TODO
- ✅ 所有代码步骤包含实际内容
- ✅ 文件路径完整

**类型一致性：**
- ✅ 所有资源键名称在 XAML 和资源文件中一致

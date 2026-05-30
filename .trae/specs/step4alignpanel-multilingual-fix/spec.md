# Step4AlignPanel 多语言修复设计

## 概述

修复 `Step4AlignPanel.xaml` 中的多语言实现问题，包括缺失的资源键、键名不匹配、硬编码字符串和违规 emoji 使用。

## 问题清单

### 1. 缺失的资源键（XAML 使用但资源文件中不存在）

| XAML 键 | 用途 |
|---------|------|
| `Step4_Section_MapFiducial` | 图纸基准点区域标题 (第100行) |
| `Step4_Section_MachineFiducial` | 机械基准点区域标题 (第132行) |
| `Step4_Btn_TeachCurrentPos` | 示教按钮 (第133行，同时存在名称不匹配) |
| `Step4_Section_PreviewTitle` | 变换结果预览标题 (第188行) |

### 2. 资源键名称不匹配

- **XAML 使用**: `Step4_Btn_TeachCurrentPos`
- **资源文件定义**: `Step4_Btn_TeachCurrentPosition`

### 3. 硬编码的英文字符串

DataGrid 列标题（第194-198行）使用硬编码英文：
- `CAD X`, `CAD Y`, `MX`, `MY`, `MZ`

### 4. Emoji 违规使用

多处资源值包含 emoji 字符，违反项目规则 *"如果按钮需要使用icon，请使用<materialDesign:PackIcon> 不要使用emoji"*。

## 设计方案：方案 A（最小化修改）

### 修改文件

1. **Module/Controls/Cad/Step4AlignPanel.xaml** - 修复键名 + DataGrid 列头
2. **MainApp/Languages/Strings.zh-CN.xaml** - 新增中文资源键 + 移除 emoji
3. **MainApp/Languages/Strings.en-US.xaml** - 新增英文资源键 + 移除 emoji

### 具体修改

#### Step4AlignPanel.xaml

1. 第133行：`Step4_Btn_TeachCurrentPos` → `Step4_Btn_TeachCurrentPosition`
2. 第194-198行：DataGrid 列头改用 `{lang:Lang}` 绑定

#### Strings.zh-CN.xaml - 新增键

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

#### Strings.zh-CN.xaml - 移除 emoji 的现有键

| 键名 | 原值 | 新值 |
|------|------|------|
| `Step4_Title` | 🎯 第四步：坐标对齐 | 第四步：坐标对齐 |
| `Step4_Btn_ViewDiagram` | 📘 查看示意图 | 查看示意图 |
| `Step4_Label_CadFiducialA` | 📍 图纸基准点 A... | 图纸基准点 A... |
| `Step4_Label_MachineFiducialA` | 🔧 机械基准点 A... | 机械基准点 A... |
| `Step4_Label_DirectionDistance` | 📏 方向点距离: | 方向点距离: |
| `Step4_Btn_CalculateTransform` | 🔄 自动计算坐标变换矩阵 | 自动计算坐标变换矩阵 |
| `Step4_Btn_SaveSegments` | 💾 保存轨迹段 | 保存轨迹段 |
| `Step4_Mode_AffineDetail` | ✅ 圆弧轨迹专用... | 圆弧轨迹专用... |

#### Strings.en-US.xaml - 新增键（与中文对应）

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

#### Strings.en-US.xaml - 移除 emoji（与中文对应）

同上，移除所有 emoji 字符。

## 验证标准

1. 编译无错误
2. 中英文切换正常显示
3. 无任何硬编码可见字符串
4. 无 emoji 字符出现在用户界面文本中
5. DataGrid 列头支持多语言切换

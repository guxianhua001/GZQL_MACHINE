# 点涂A 出胶参数组 增加起点开胶延时 设计文档

**日期**: 2026-05-24

## 需求摘要

在点涂A（DotProcessParams）的出胶参数组中，新增"起点开胶延时"参数，位于"出胶时间"下方。

## 参数规格

| 属性 | 值 |
|------|-----|
| 属性名 | `PreDispenseDelay` |
| 显示名 | 起点开胶延时 / Start Delay |
| 类型 | double (ms) |
| 默认值 | 50.0 |
| 范围 | 0 ~ 5000 ms |
| 说明 | 到达起点后延迟开胶时间 |

## 修改文件清单

### 1. Core/Models/DotProcessParams.cs
- 在 `#region 出胶参数` 内，`DispenseTime` 属性之后、`PostDelay` 之前插入新属性
- 遵循现有模式：private字段 + SetProperty + Math.Clamp范围约束 + XML文档注释

### 2. MainApp/Languages/Strings.zh-CN.xaml
- 新增: `Dispensing_Dot_PreDispenseDelay` → "起点开胶延时"
- 新增: `Dispensing_Dot_Tip_PreDispenseDelay` → 提示文本
- 位置: 紧跟在 `Dispensing_Dot_DispenseTime` 相关键之后

### 3. MainApp/Languages/Strings.en-US.xaml
- 新增: `Dispensing_Dot_PreDispenseDelay` → "Start Delay"
- 新增: `Dispensing_Dot_Tip_PreDispenseDelay` → tip text
- 位置: 同上

## 不需要修改的文件

- ParameterEditorViewModel.cs — 反射自动发现新属性
- View/XAML — ItemsSource 自动渲染
- JSON 配置兼容 — 缺少字段时默认值兜底

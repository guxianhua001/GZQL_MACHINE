# Dispense Station 标签页和 Z Height Correction 文字修改

## 概述

修改 Dispense Station 的标签页名称和 Z Height Correction 的显示文字，将 2D 改为 3D 并添加 3D 标注。

## 修改内容

### 修改 1：Tab B 标签名称 (2D → 3D)

| 资源键 | 当前值 | 新值 |
|--------|--------|------|
| `Dispensing_Tab_Line2D` (中文) | 2D线条 (B) | 3D线条 (B) |
| `Dispensing_Tab_Line2D` (英文) | 2D Line (B) | 3D Line (B) |

### 修改 2：Z Height Correction 标注 (3D)

| 资源键 | 当前值 | 新值 |
|--------|--------|------|
| `Step6_CheckBox_ZCorrection` (中文) | 启用 Z 高度校正 | 启用 Z 高度校正 (3D) |
| `Step6_CheckBox_ZCorrection` (英文) | Enable Z Height Correction | Enable Z Height Correction (3D) |
| `Step6_Desc_ZCorrection` (中文) | 启用后，执行时将使用每段的示教高度... | [3D模式] 启用后，执行时将使用每段的示教高度... |
| `Step6_Desc_ZCorrection` (英文) | When enabled, each segment's teach height... | [3D] When enabled, each segment's teach height... |

## 修改文件

1. `MainApp/Languages/Strings.zh-CN.xaml` - 4 处
2. `MainApp/Languages/Strings.en-US.xaml` - 4 处

# CadAlignmentView 多语言支持修复设计

**日期**: 2026-05-20
**状态**: 已批准
**视图**: CadAlignmentView.xaml
**问题**: 38 处硬编码中文字符串未使用 lang:Lang 实现多语言

---

## 1. 问题背景

CadAlignmentView 是 CAD 对齐功能的 5 步骤向导界面，当前多语言覆盖率仅 62%（约 60+ 处已使用 lang:Lang），剩余 38 处硬编码中文字符串需要修复。

### 1.1 当前状态

| 实现方式 | 数量 | 占比 |
|---------|------|------|
| ✅ 已使用 lang:Lang | ~60+ 处 | 62% |
| ❌ 硬编码中文 | 38 处 | 38% |

### 1.2 未实现分类

1. **DataGrid 列标题**（8处）：角度、X(FitX)、机械_X、图像C、基准、目标等
2. **技术说明文字**（15处）：公式说明、操作提示、变换步骤、易错规则
3. **按钮和标签**（7处）：向量方向角计算、导入DXF、智能推荐等
4. **坐标变量名**（4处）：Mox, Moy, FitRadius（决定保持英文）
5. **Emoji 违规**（1处）：🪄 智能推荐

---

## 2. 技术方案

### 2.1 方案选择：继续使用 lang:Lang

**理由**:
- 与现有 60+ 处绑定保持一致
- 支持格式化参数（适合公式说明）
- 无需重构现有代码
- 统一技术栈

### 2.2 资源键命名规范

```
前缀: CAD_ + 功能区域_ + 具体项
示例:
- CAD_Angle              → 角度
- CAD_Machine_X          → 机械_X
- CAD_Formula_Fit_Desc   → 拟合公式说明
- CAD_Error_Rule_1       → 易错规则1
```

### 2.3 分类处理策略

#### A. DataGrid 列标题（8处）
```xml
<!-- 修改前 -->
<DataGridTextColumn Header="角度" .../>

<!-- 修改后 -->
<DataGridTextColumn Header="{lang:Lang CAD_Angle}" .../>
```

#### B. 按钮/标签文本（7处）
```xml
<!-- 修改前 -->
<TextBlock Text="向量方向角计算" .../>

<!-- 修改后 -->
<TextBlock Text="{lang:Lang CAD_Vector_Angle_Calc}" .../>
```

#### C. 长说明文字（10处）
```xml
<!-- 修改前 -->
<Run Text="采用最小二乘圆拟合算法..."/>

<!-- 修改后 -->
<Run Text="{lang:Lang CAD_Fit_Algorithm_Desc}"/>
```

#### D. 变量名标签（4处）- 保持英文不变
```xml
<!-- 保持不变 -->
<TextBlock Text="Mox" .../>
<TextBlock Text="FitRadius" .../>
```
**理由**: Mox, Moy, FitRadius 是国际通用的数学/技术符号。

#### E. Emoji 替换（1处）
```xml
<!-- ❌ 修改前：违规使用 emoji -->
<TextBlock Text="🪄 智能推荐"/>

<!-- ✅ 修改后：使用 materialDesign:PackIcon -->
<StackPanel>
    <materialDesign:PackIcon Kind="LightbulbOutline" Width="14" Height="14"
                             VerticalAlignment="Center"/>
    <TextBlock Text="{lang:Lang CAD_Smart_Recommend}"
               VerticalAlignment="Center" FontSize="11" Margin="5,0,0,0"/>
</StackPanel>
```

---

## 3. 资源键清单（38处）

### 3.1 Tab 1 - 回转中心（4处）

| 键名 | 中文值 | 英文值 |
|-----|--------|--------|
| CAD_Angle | 角度 | Angle |
| CAD_Fit_X | X(FitX) | X(FitX) |
| CAD_Fit_Y | Y(FitY) | Y(FitY) |
| CAD_Fit_Algorithm_Desc | 采用最小二乘圆拟合算法，通过4个以上不同旋转角度下的点位坐标，求解 Rz 轴的回转中心(Mox,Moy)和拟合半径。 | Uses least-squares circle fitting algorithm to solve Rz axis rotation center (Mox,Moy) and fit radius from 4+ points at different angles. |
| CAD_Fit_Principle | 原理：对每个观测点到圆心的距离平方和最小化，即 min Σ[(xi-Mox)²+(yi-Moy)²-R²]。 | Principle: Minimize sum of squared distances from each point to center: min Σ[(xi-Mox)²+(yi-Moy)²-R²]. |
| CAD_Fit_Note | 注意：输入点位必须为机械坐标系下的实际测量值，且各点应均匀分布在旋转圆周上以保证拟合精度。 | Note: Input points must be actual measurements in machine coordinates, evenly distributed around the rotation circle for accuracy. |

### 3.2 Tab 2 - 全局偏移（4处）

| 键名 | 中文值 | 英文值 |
|-----|--------|--------|
| CAD_Tip_Zero_Rz | • 确保当前 Rz 轴已归零（姿态角 = 0°），否则偏移计算将引入旋转误差 | • Ensure Rz axis is zeroed (posture angle = 0°), otherwise offset calculation will introduce rotation error |
| CAD_Tip_Machine_Coord | • 机械坐标来自运动控制器实时反馈，CAD坐标来自图纸设计值 | • Machine coordinates from motion controller real-time feedback, CAD coordinates from design drawings |
| CAD_Tip_Global_Offset | • 全局偏移量 Δ = 机械坐标 - CAD坐标，后续变换将叠加此偏移 | • Global offset Δ = Machine coord - CAD coord, subsequent transforms will add this offset |
| CAD_Tip_No_Move | • 完成此步后请勿移动工件位置，直接进入下一步骤 | • Do not move workpiece after this step, proceed directly to next step |

### 3.3 Tab 3 - 旋转角度（12处）

| 键名 | 中文值 | 英文值 |
|-----|--------|--------|
| CAD_Vector_Angle_Title | 向量方向角计算 | Vector Direction Angle Calculation |
| CAD_Import_DXF_Btn | ① 导入DXF文件 | ① Import DXF File |
| CAD_Smart_Recommend | 智能推荐 | Smart Recommend |
| CAD_Pick_Points_Hint | (在图形上点击选取点位) | (Click on graphic to select points) |
| CAD_Click_Row_Hint | (或点击行直接选取) | (Or click row to select) |
| CAD_Machine_X | 机械_X | Machine_X |
| CAD_Machine_Y | 机械_Y | Machine_Y |
| CAD_Image_Col | 图像C | ImageCol |
| CAD_Image_Row | 图像R | ImageRow |
| CAD_Base_Start | 基准起点 | Base Start |
| CAD_Base_End | 基准终点 | Base End |
| CAD_Target_Start | 目标起点 | Target Start |
| CAD_Target_End | 目标终点 | Target End |

### 3.4 Tab 4 - 坐标变换（9处）

| 键名 | 中文值 | 英文值 |
|-----|--------|--------|
| CAD_Transform_Process | 变换过程 | Transform Process |
| CAD_Step1_Unrotated | ①未旋转机械: | ①Unrotated Machine: |
| CAD_Step2_Relative_Offset | ②相对中心偏移: | ②Relative Center Offset: |
| CAD_Step3_Rotation_Angle | ③旋转角度: | ③Rotation Angle: |
| CAD_Step4_Final_Result | ④最终结果: | ④Final Result: |
| CAD_Formula_Translate | Xm = Cx + ΔX          (先全局平移) | Xm = Cx + ΔX          (Global translate first) |
| CAD_Formula_Relative | dx = Xm - Mox         (相对回转中心) | dx = Xm - Mox         (Relative to center) |
| CAD_Formula_Rotate | X_new = dx·cosθ - dy·sinθ + Mox   (绕中心旋转) | X_new = dx·cosθ - dy·sinθ + Mox   (Rotate around center) |

### 3.5 Tab 5 - 夹爪定位（5处）

| 键名 | 中文值 | 英文值 |
|-----|--------|--------|
| CAD_Error_Rule_1 | 1. 回转中心必须使用机械坐标系值 | 1. Rotation center must use machine coordinate values |
| CAD_Error_Rule_2 | 2. 变换顺序不可颠倒（先平移后旋转） | 2. Transform order must not be reversed (translate first, then rotate) |
| CAD_Error_Rule_3 | 3. 全产品统一使用同一个 Rz 回转中心 | 3. All products must use the same Rz rotation center |
| CAD_Error_Rule_4 | 4. 旋转角度由 CAD 向量计算得出 | 4. Rotation angle calculated from CAD vectors |
| CAD_Error_Rule_5 | 5. 夹爪装配仅替换对齐后的目标点位 | 5. Gripper assembly only replaces aligned target points |

---

## 4. 实施步骤

### 4.1 准备阶段
1. 在 `Strings.zh-CN.xaml` 添加 38 个中文资源键值对
2. 在 `Strings.en-US.xaml` 添加 38 个英文资源键值对

### 4.2 修改阶段
3. 替换 CadAlignmentView.xaml 中的 38 处硬编码字符串
4. 替换 emoji 为 materialDesign:PackIcon（1处）

### 4.3 验证阶段
5. 编译解决方案，确保无错误
6. 手动验证 UI 显示正确
7. 测试语言切换功能

---

## 5. 预期成果

- ✅ CadAlignmentView 多语言覆盖率：**62% → 100%**
- ✅ 消除所有 38 处硬编码中文字符串
- ✅ 符合项目规范（无 emoji、统一使用 lang:Lang）
- ✅ 技术变量名保持英文（Mox, Moy, FitRadius）
- ✅ 运行时语言切换完全支持

---

## 6. 文件修改清单

| 文件路径 | 操作 | 说明 |
|---------|------|------|
| `MainApp/Languages/Strings.zh-CN.xaml` | 修改 | 添加 38 个中文资源 |
| `MainApp/Languages/Strings.en-US.xaml` | 修改 | 添加 38 个英文资源 |
| `Module/Controls/Assembly/CadAlignmentView.xaml` | 修改 | 替换 38 处硬编码 + 1 处 emoji |

---

## 7. 风险与注意事项

### 7.1 低风险
- ✅ 仅修改资源绑定，不影响布局和功能
- ✅ 使用现有 lang:Lang 机制，无需新代码
- ✅ 变量名保持英文，避免过度翻译

### 7.2 注意事项
- ⚠️ 公式说明中的特殊字符（Σ, ², π, θ）需在两种语言中保持一致
- ⚠️ DataGrid 列标题宽度可能需要微调（中英文长度不同）
- ⚠️ 修改后需在 Design Mode 和 Runtime 都验证显示效果

---

## 8. 后续优化建议（可选）

1. **考虑将长文本提取到 ViewModel**：对于超过 50 字的说明文字，可考虑在 ViewModel 中提供属性绑定，便于动态构建
2. **添加语言切换测试用例**：为 CadAlignmentView 编写自动化测试，验证所有文本都能正确切换
3. **建立多语言检查 CI**：在持续集成中添加脚本，自动检测新增的硬编码中文字符串

---

**审批人**: 用户
**审批日期**: 2026-05-20

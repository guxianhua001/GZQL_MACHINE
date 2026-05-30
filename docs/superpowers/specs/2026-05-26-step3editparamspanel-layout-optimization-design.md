# Step3EditParamsPanel 布局优化设计文档

**日期：** 2026-05-26  
**状态：** ✅ 已批准  
**方案：** 方案A - 全面重构  

---

## 1. 项目背景与目标

### 1.1 背景
Step3EditParamsPanel 是2D/3D线条(B/C)功能的参数编辑面板，支持两种模式：
- **单点模式**：全局工艺参数配置
- **连续插补模式**：逐段参数编辑

当前存在的问题：
1. 单点模式参数分组命名不清晰（"运动与出胶"混合了不同功能）
2. 连续插补模式布局为单列Grid，与单点模式的三列彩色风格不一致
3. 连续插补模式缺少关键的运动和出胶参数（空移速度、安全高度等）
4. 批量设置功能仅支持单一胶量参数

### 1.2 目标
1. **统一UI风格**：两种模式均采用三列彩色分组布局
2. **语义化命名**：组名准确反映功能（运动参数、出胶控制、阀控参数）
3. **功能完善**：补充连续插补模式的5个关键参数
4. **增强批量操作**：从单一参数扩展为全参数批量设置
5. **多语言支持**：同步更新中英文资源文件

---

## 2. 需求详情

### 2.1 单点模式调整

#### 变更清单
| 项目 | 修改前 | 修改后 | 类型 |
|-----|-------|-------|------|
| 第1组标题 | 运动与出胶 | **运动参数** | 重命名 |
| 第2组标题 | 延时控制 | **出胶控制** | 重命名 |
| 出胶时间位置 | 第1组（运动与出胶） | 第2组（出胶控制） | 移动 |

#### 目标布局结构
```
┌─────────────────┬─────────────────┬─────────────────┐
│  运动参数(蓝色)  │  出胶控制(琥珀)  │  阀控参数(青色)  │
├─────────────────┼─────────────────┼─────────────────┤
│ 空移速度        │ 出胶时间 ⬅️移动   │ 点胶气压         │
│ 安全高度        │ 开胶距离         │ 回吸时间         │
│ 逼近高度        │ 起点延时         │                 │
│ 减速系数        │ 收胶延时         │                 │
└─────────────────┴─────────────────┴─────────────────┘
```

### 2.2 连续插补模式升级

#### 新增参数（5个）
| 参数名 | 数据类型 | 默认值 | 单位 | 所属分组 |
|-------|---------|-------|------|---------|
| JumpSpeed | double | 20.0 | mm/s | 运动参数（蓝色）|
| SafeHeight | double | 5.0 | mm | 运动参数（蓝色）|
| ApproachHeight | double | 3.0 | mm | 运动参数（蓝色）|
| DecelFactor | double | 0.30 | - | 运动参数（蓝色）|
| GlueTriggerOffsetMm | double | 0.5 | mm | 出胶控制（琥珀色）*复用现有字段*

#### 目标布局结构
```
┌─────────────────┬─────────────────┬─────────────────┐
│  运动参数(蓝色)  │  出胶控制(琥珀)  │  高度参数(青色)  │
├─────────────────┼─────────────────┼─────────────────┤
│ [新]空移速度     │ [新]开胶距离     │ 示教高度         │
│ [新]安全高度     │ 起点延时         │ 高度补偿         │
│ [新]逼近高度     │ 收胶延时         │ 有效高度(只读)   │
│ [新]减速系数     │                 │                 │
│ 插补速度         │                 │                 │
└─────────────────┴─────────────────┴─────────────────┘
```

### 2.3 批量设置功能升级

#### 按钮变更
- 文本：`"批量设胶量"` → **`"批量设置全部参数"`**
- 图标建议：`PackIcon Kind="ContentSaveAll"`

#### 对话框设计
- **交互方式**：模态对话框，显示所有可编辑参数
- **筛选逻辑**：仅应用于 `IsEnabled=true` 的选中段
- **用户操作**：
  - 每个参数带复选框，可选择要批量设置的项
  - 只填写并勾选的参数会被应用
  - 应用前显示确认信息："将更新 N 个段的 M 个参数"

#### 对话框内容示例（单点模式）
```
运动参数组：
  ☑ 空移速度: [____] mm/s
  ☑ 安全高度: [____] mm
  ☑ 逼近高度: [____] mm
  ☑ 减速系数: [____]

出胶控制组：
  ☑ 出胶时间: [____] ms
  ☑ 开胶距离: [____] mm
  ☑ 起点延时: [____] ms
  ☑ 收胶延时: [____] ms

阀控参数组：
  ☑ 点胶气压: [____] MPa
  ☑ 回吸时间: [____] ms

已选择 35 个启用段    [取消]  [应用]
```

### 2.4 命名统一
- ~~起点开胶延时~~ → **起点延时** （在UI和语言资源中统一）

---

## 3. 技术设计

### 3.1 数据模型扩展

#### DispenseSegment.cs 新增字段
```csharp
public class DispenseSegment : ObservableObject
{
    // ... 现有字段保持不变 ...

    // ===== 新增：连续插补模式运动参数 =====
    private double _jumpSpeed = 20.0;
    public double JumpSpeed
    {
        get => _jumpSpeed;
        set { SetProperty(ref _jumpSpeed, value); }
    }

    private double _safeHeight = 5.0;
    public double SafeHeight
    {
        get => _safeHeight;
        set { SetProperty(ref _safeHeight, value); }
    }

    private double _approachHeight = 3.0;
    public double ApproachHeight
    {
        get => _approachHeight;
        set { SetProperty(ref _approachHeight, value); }
    }

    private double _decelFactor = 0.30;
    public double DecelFactor
    {
        get => _decelFactor;
        set { SetProperty(ref _decelFactor, value); }
    }

    // 注意：GlueTriggerOffsetMm 字段已存在，无需新增
}
```

### 3.2 UI组件设计

#### Step3EditParamsPanel.xaml 关键变更

**单点模式部分（第240-290行区域）：**
- 调整三列StackPanel的内容分配
- 修改GroupTitle的Text绑定键
- 将出胶时间的TextBox从第1组移到第2组

**连续插补模式部分（第140-235行区域）：**
- 完全重写为三列Grid布局
- 替换单列Grid为三列彩色分组结构
- 绑定新增的5个参数字段

#### BatchSetParamsDialog.xaml（新建）
- 模态窗口，使用MaterialDesign对话框样式
- 动态生成参数列表（根据当前模式）
- 包含复选框、输入框、单位标签
- 底部操作按钮：取消/应用

### 3.3 ViewModel更新

#### CadPointEditorViewModel.cs 需要更新的内容：

1. **BatchSetGlueCommand → BatchSetAllCommand**
   - 重构命令逻辑
   - 打开新的批量设置对话框
   - 处理多参数应用逻辑

2. **新增属性绑定**
   ```csharp
   // 连续插补模式需要绑定的新属性（通过SelectedSegment）
   public double SelectedJumpSpeed => SelectedSegment?.JumpSpeed ?? 0;
   public double SelectedSafeHeight => SelectedSegment?.SafeHeight ?? 0;
   // ... 其他属性类似
   ```

3. **批量设置命令处理**
   ```csharp
   private async Task ExecuteBatchSetAll()
   {
       // 1. 筛选已启用且选中的段
       var enabledSegments = Segments.Where(s => s.IsEnabled && s.IsSelected);
       
       // 2. 打开对话框获取用户输入
       var result = await ShowBatchSetDialog(IsSinglePointMode);
       
       // 3. 应用到每个段
       foreach (var segment in enabledSegments)
       {
           if (result.SetJumpSpeed) segment.JumpSpeed = result.JumpSpeed;
           if (result.SetSafeHeight) segment.SafeHeight = result.SafeHeight;
           // ... 其他参数
       }
   }
   ```

### 3.4 多语言资源更新

#### Strings.zh-CN.xaml 新增/修改键
```xml
<!-- 组名 -->
<sys:String x:Key="Step3_Group_MotionParams">运动参数</sys:String>
<sys:String x:Key="Step3_Group_DispenseControl">出胶控制</sys:String>

<!-- 连续插补新增参数 -->
<sys:String x:Key="Step3_Label_JumpSpeed">空移速度</sys:String>
<sys:String x:Key="Step3_Label_SafeHeight">安全高度</sys:String>
<sys:String x:Key="Step3_Label_ApproachHeight">逼近高度</sys:String>
<sys:String x:Key="Step3_Label_DecelFactor">减速系数</sys:String>

<!-- 命名统一 -->
<sys:String x:Key="Step3_Label_StartDelay">起点延时</sys:String>

<!-- 批量设置 -->
<sys:String x:Key="Step3_Btn_BatchSetAll">批量设置全部参数</sys:String>
<sys:String x:Key="Step3_Dialog_Title_SinglePoint">批量设置单点模式参数</sys:String>
<sys:String x:Key="Step3_Dialog_Title_Continuous">批量设置连续插补参数</sys:String>
<sys:String x:Key="Step3_Dialog_SelectedCount">已选择 {0} 个启用段</sys:String>
<sys:String x:Key="Step3_Dialog_ConfirmApply">将更新 {0} 个段的 {1} 个参数</sys:String>
```

#### Strings.en-US.xaml 对应翻译
```xml
<sys:String x:Key="Step3_Group_MotionParams">Motion Parameters</sys:String>
<sys:String x:Key="Step3_Group_DispenseControl">Dispense Control</sys:String>
<sys:String x:Key="Step3_Label_JumpSpeed">Jump Speed</sys:String>
<sys:String x:Key="Step3_Label_SafeHeight">Safe Height</sys:String>
<sys:String x:Key="Step3_Label_ApproachHeight">Approach Height</sys:String>
<sys:String x:Key="Step3_Label_DecelFactor">Deceleration Factor</sys:String>
<sys:String x:Key="Step3_Label_StartDelay">Start Delay</sys:String>
<sys:String x:Key="Step3_Btn_BatchSetAll">Batch Set All Parameters</sys:String>
<sys:String x:Key="Step3_Dialog_Title_SinglePoint">Batch Set Single Point Params</sys:String>
<sys:String x:Key="Step3_Dialog_Title_Continuous">Batch Set Continuous Params</sys:String>
<sys:String x:Key="Step3_Dialog_SelectedCount">{0} enabled segments selected</sys:String>
<sys:String x:Key="Step3_Dialog_ConfirmApply">Will update {1} parameters for {0} segments</sys:String>
```

---

## 4. 实施计划

### 4.1 文件修改清单

| 序号 | 文件路径 | 操作类型 | 主要工作 |
|-----|---------|---------|---------|
| 1 | `Core/Models/DispenseSegment.cs` | 修改 | 扩展5个新字段 + 默认值 |
| 2 | `Module/Controls/Cad/Step3EditParamsPanel.xaml` | 修改 | 单点模式重组 + 连续插补重写 |
| 3 | `Module/Controls/Cad/Step3EditParamsPanel.xaml.cs` | 修改 | 可能添加事件处理 |
| 4 | `Module/Views/BatchSetParamsDialog.xaml` | **新建** | 批量设置对话框UI |
| 5 | `Module/Views/BatchSetParamsDialog.xaml.cs` | **新建** | 对话框逻辑代码 |
| 6 | `Module/Controls/Cad/CadPointEditorViewModel.cs` | 修改 | 新属性绑定 + 命令重构 |
| 7 | `MainApp/Languages/Strings.zh-CN.xaml` | 修改 | 新增/修改约15个资源键 |
| 8 | `MainApp/Languages/Strings.en-US.xaml` | 修改 | 英文翻译同步 |

### 4.2 实施顺序与工时估算

#### Phase 1: 数据模型层（30分钟）
- [ ] 扩展 `DispenseSegment.cs`，添加5个新属性
- [ ] 设置合理的默认值
- [ ] 确保实现INotifyPropertyChanged（应已有基类支持）
- [ ] 编译验证无错误

#### Phase 2: 单点模式UI调整（45分钟）
- [ ] 修改 `Step3EditParamsPanel.xaml` 单点模式部分的XAML
- [ ] 调整第一组：移除出胶时间，改标题为"运动参数"
- [ ] 调整第二组：添加出胶时间，改标题为"出胶控制"
- [ ] 保持第三组不变
- [ ] 更新语言资源键引用

#### Phase 3: 连续插补模式UI重写（60分钟）
- [ ] 重写连续插补模式面板为三列Grid布局
- [ ] 创建三个彩色分组StackPanel（蓝/琥珀/青）
- [ ] 绑定新增的5个参数到对应分组
- [ ] 统一"起点延时"命名
- [ ] 保留现有的采样点位列表DataGrid

#### Phase 4: 批量设置功能升级（60分钟）
- [ ] 新建 `BatchSetParamsDialog.xaml` 对话框UI
- [ ] 实现动态参数列表生成（根据当前模式）
- [ ] 添加复选框、输入框、验证逻辑
- [ ] 在ViewModel中创建 `BatchSetAllCommand`
- [ ] 实现多参数应用逻辑
- [ ] 更新工具栏按钮文本和图标

#### Phase 5: 多语言资源更新（30分钟）
- [ ] 更新 `Strings.zh-CN.xaml` 所有新增/修改的键
- [ ] 更新 `Strings.en-US.xaml` 英文翻译
- [ ] 检查重复键问题（避免之前的XAML解析错误）

#### Phase 6: 测试与验证（30分钟）
- [ ] 编译项目，确保无错误
- [ ] 手动测试单点模式布局和参数绑定
- [ ] 手动测试连续插补模式新布局和新参数
- [ ] 测试批量设置对话框的功能完整性
- [ ] 验证中英文切换正常
- [ ] 边界情况测试（未选中段、空值、超范围等）

**总预计工时：约4小时**

---

## 5. 设计决策记录

### 5.1 为什么选择三列彩色分组？
- **一致性**：与DotPointEditorView的单点模式保持统一
- **可读性**：颜色编码帮助用户快速识别功能组
- **工业审美**：符合专业工控软件的视觉规范

### 5.2 为什么扩展Segment模型而非使用全局配置？
- **灵活性**：支持不同段使用不同的运动参数（如拐角段需要更低的速度）
- **可追溯性**：参数跟随轨迹段，便于配方管理和复用
- **扩展性**：未来可能需要更精细的逐段控制

### 5.3 为什么选择多参数对话框而非下拉菜单？
- **效率**：一次设置多个参数，减少操作步骤
- **清晰度**：用户能看到所有可设置的参数全貌
- **选择性**：通过复选框让用户自由组合要设置的参数

### 5.4 关于"起点延时"命名统一的考虑
- 原本单点模式用"起点延时"，连续插补用"起点开胶延时"
- 统一为"起点延时"更简洁，且语义明确（都是指出胶前的等待时间）
- 减少用户的理解和记忆成本

---

## 6. 风险与缓解措施

### 6.1 潜在风险
1. **数据兼容性**：扩展Segment模型可能导致旧配方文件加载失败
   - **缓解**：在模型中提供默认值，使用可选反序列化
   
2. **XAML复杂度增加**：三列嵌套布局可能影响性能
   - **缓解**：使用虚拟化、避免过深的Visual Tree

3. **批量操作的原子性**：部分参数应用失败时的回滚
   - **缓解**：使用事务性更新，或在应用前完整验证

4. **多语言资源重复键**：之前遇到过此问题导致启动崩溃
   - **缓解**：使用脚本检查重复键，严格Code Review

### 6.2 回滚策略
- 如遇严重问题，可通过Git快速回退到当前版本
- 建议在feature分支开发，合并前充分测试

---

## 7. 验收标准

### 7.1 功能验收
- [ ] 单点模式三列布局正确显示，参数位置符合设计
- [ ] 连续插补模式采用三列彩色分组，新增5个参数可见可编辑
- [ ] 批量设置按钮打开多参数对话框，能正确应用到选中段
- [ ] 参数命名统一（"起点延时"），无歧义

### 7.2 UI/UX验收
- [ ] 两种模式风格一致，颜色使用符合规范
- [ ] 布局紧凑合理，无浪费空间
- [ ] 对话框交互流畅，反馈及时

### 7.3 技术验收
- [ ] 编译无错误无警告
- [ ] 多语言切换正常（中英文）
- [ ] 无内存泄漏，性能无明显下降
- [ ] 代码符合项目架构规范（MVVM、Prism）

---

## 8. 后续优化方向（不在本次范围）

1. **参数模板功能**：保存常用的参数组合为模板，一键应用
2. **参数联动验证**：如安全高度必须大于逼近高度的校验
3. **批量操作撤销**：支持Ctrl+Z撤销批量设置
4. **参数导入导出**：从Excel/CSV批量导入参数

---

**文档版本：** v1.0  
**最后更新：** 2026-05-26  
**作者：** AI Assistant (Brainstorming Skill)

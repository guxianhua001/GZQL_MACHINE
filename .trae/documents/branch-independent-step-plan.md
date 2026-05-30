# BRANCH 独立步骤实现计划

## 目标

将 BRANCH 改为**独立步骤类型**，删除其他步骤的附加 BranchConfig 机制：
1. BRANCH 作为独立步骤插入序列
2. 检查所有已执行步骤的输出参数和全局变量
3. 根据条件表达式判断跳转到哪一步
4. 删除其他步骤（GOTO/VISION/SCAN等）的 BranchConfig 附加逻辑

## 当前架构 → 目标架构

```
当前（混乱）：
Step 6: DASHBOARD  → 执行完毕 → 检查自己的 BranchConfig → 条件跳转
Step 7: BRANCH     → 无执行器 → 跳过

目标（清晰）：
Step 6: DASHBOARD  → 执行完毕 → 继续
Step 7: BRANCH     → 独立执行 → 收集所有前序步骤输出 → 评估条件 → 跳转
Step 8: WAIT       → ...
```

## 实现步骤

### 步骤1：创建 BranchStepAction

**文件**: `StationTasks/Actions/BranchStepAction.cs`（新建）

- `SupportedStepType => StepType.BRANCH`
- `ExecuteAsync` 仅记录日志，核心跳转逻辑在 ProcessStepExecutor 中

### 步骤2：修改 ProcessStepExecutor.ExecuteSingleStepAsync

**文件**: `StationTasks/Actions/ProcessStepExecutor.cs`

1. **新增 BRANCH case**：
   ```csharp
   case StepType.BRANCH:
       if (step.BranchConfig?.IsEnabled == true)
           return await ExecuteBranchLogicAsync(step, steps, currentIndex, token);
       return currentIndex + 1;
   ```

2. **删除其他步骤的 BranchConfig 检查**：
   - 移除 GOTO/VISION/SCAN/DASHBOARD/SEEK/WAIT/SCRIPT 分支中的 BranchConfig 检查代码
   - 这些步骤执行完毕后直接返回 `currentIndex + 1`

### 步骤3：注册 BranchStepAction

**文件**: `StationTasks/StationTasksModule.cs`

在 DI 注册中添加 `typeof(BranchStepAction)`

### 步骤4：修改 ConditionBranchViewModel — 收集所有前序步骤输出

**文件**: `Module/ViewModels/ConditionBranchViewModel.cs`

- `LoadPreviousStepOutputs` 遍历所有 Seq < 当前步骤的步骤
- 收集每个步骤的 BranchConfig.OutputParameters
- 收集 VisionDetail.VariableMappings 的输出
- 以 `@Output:步骤Seq_参数名` 格式提供下拉选择

### 步骤5：修改 ProcessSequenceEditorViewModel

**文件**: `Module/Editor/ProcessSequenceEditorViewModel.cs`

1. BRANCH 步骤双击打开 ConditionBranchView 编辑器
2. 在 `OpenStepDetailForStep` 中为 `StepType.BRANCH` 添加处理
3. 移除其他步骤的"条件分支"按钮/入口（不再需要附加 BranchConfig）

### 步骤6：清理 UI — 移除非 BRANCH 步骤的 BranchConfig 入口

**文件**: `Module/Editor/ProcessSequenceEditorView.xaml`

- 移除非 BRANCH 步骤行中的"条件分支"按钮
- BRANCH 步骤行保留"条件分支"按钮

### 步骤7：清理 ProcessStep 模型

**文件**: `StationTasks/Models/ProcessStep.cs`

- `BranchConfig` 属性保留（BRANCH 步骤需要使用）
- `IsBranchEnabled` 计算属性保留
- 无需删除，因为 BRANCH 步骤自己也使用 BranchConfig

## 修改文件清单

| 文件 | 操作 | 说明 |
|------|------|------|
| `StationTasks/Actions/BranchStepAction.cs` | 新建 | BRANCH 步骤动作类 |
| `StationTasks/StationTasksModule.cs` | 修改 | 注册 BranchStepAction |
| `StationTasks/Actions/ProcessStepExecutor.cs` | 修改 | 新增 BRANCH case + 删除其他步骤的 BranchConfig 检查 |
| `Module/ViewModels/ConditionBranchViewModel.cs` | 修改 | 收集所有前序步骤输出 |
| `Module/Editor/ProcessSequenceEditorViewModel.cs` | 修改 | BRANCH 双击编辑 + 移除非 BRANCH 的分支入口 |
| `Module/Editor/ProcessSequenceEditorView.xaml` | 修改 | UI 清理 |

## 风险评估

- **中风险**：删除其他步骤的 BranchConfig 检查可能影响已有 JSON 配置
- **缓解**：已有 JSON 中其他步骤的 BranchConfig 数据保留但不执行，不影响序列运行
- **性能**：BRANCH 步骤仅做条件评估，无 IO 操作，响应速度快

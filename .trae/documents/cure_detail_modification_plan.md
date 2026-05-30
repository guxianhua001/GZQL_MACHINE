
# CureDetail 改造实施计划

## 需求概述
1. 不同的固化头对应不同的DO输出点，输出点需能设置
2. ST参数组放在Expander里，默认不展开
3. 步骤序列里增加Action，固化开灯，开灯时间后，固化结束关灯，仿照PICK

## 需要修改的文件清单

### 1. 模型层修改
- **文件**: `StationTasks\Models\ProcessStep.cs`
- **修改内容**:
  - 在 `CureDetail` 类中添加固化头DO输出点配置属性
  - 在 `SubMoveAction` 枚举中添加UV相关动作 (如果不存在)

### 2. UI层修改
- **文件**: `Module\Controls\StepDetails\CureDetailView.xaml`
- **修改内容**:
  - 添加固化头DO输出点配置UI
  - 将Stage 1-4参数用Expander包裹，默认不展开
  - 为CureMoves DataGrid添加Action列（类似PickDetailView）

### 3. ViewModel层修改
- **文件**: `Module\Controls\StepDetails\CureDetailViewModel.cs`
- **修改内容**:
  - 添加DO输出点属性绑定
  - 确保SubMoveRowViewModel支持Action功能

### 4. 动作执行器创建
- **文件**: `StationTasks\Actions\CureStepAction.cs` (新建)
- **修改内容**:
  - 实现 `IProcessStepAction` 接口
  - 实现CURE步骤执行逻辑：执行SubMoves → 开灯 → 延时固化 → 关灯

### 5. 执行器集成
- **文件**: `StationTasks\Actions\ProcessStepExecutor.cs`
- **修改内容**:
  - 确保在StepType switch中添加CURE步骤处理

### 6. 多语言支持
- **文件**: `MainApp\Languages\Strings.zh-CN.xaml` 和 `Strings.en-US.xaml`
- **修改内容**:
  - 添加新UI文本的多语言资源

## 详细修改说明

### 1. CureDetail 模型扩展
在 `CureDetail` 类添加：
```csharp
// 固化头1的DO输出端口
public int UvHead1DoPort { get; set; } = 1;
// 固化头2的DO输出端口
public int UvHead2DoPort { get; set; } = 2;
```

### 2. SubMoveAction 枚举扩展
添加：
```csharp
UvOn,   // UV灯打开
UvOff,  // UV灯关闭
UvDelay, // UV固化延时
```

### 3. CureStepAction 执行逻辑
1. 执行CureMoves中的所有SubMove（包括Action和运动）
2. 根据配置的固化头和DO端口控制UV灯
3. 根据CureTimeMs延时
4. 关闭UV灯
5. 使用IMotionCard.SetDo控制DO输出

### 4. UI修改
- 添加DO端口配置输入框
- Stage 1-4放入Expander，IsExpanded=False
- DataGrid添加Action列，使用SubMoveRowViewModel的AvailableActions

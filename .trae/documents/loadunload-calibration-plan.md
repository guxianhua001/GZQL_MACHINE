# LoadUnloadView & ProductCalibrationView 功能完善计划

## 功能缺失分析

### LoadUnloadView 问题清单
| # | 问题 | 严重度 | 状态 |
|---|------|--------|------|
| A1 | `EditSitePositionAction` 空实现（仅弹对话框） | 高 | 待修 |
| A2 | `EditGripperParameterAction` 空实现（仅弹对话框） | 高 | 待修 |
| A3 | `GripperOperationAction` 空实现（await Task.CompletedTask） | 高 | 待修 |
| A4 | `View3DScanDataAction` 空实现（"not implemented"消息） | 中 | 待修 |
| A5 | **缺少急停按钮** — 工业设备安全必需 | 高 | 待修 |
| A6 | UI硬编码英文，缺少多语言 | 中 | 待修 |

### ProductCalibrationView 问题清单
| # | 问题 | 严重度 | 状态 |
|---|------|--------|------|
| B1 | `FiducialData.OnGoToPhoto` 仅弹对话框，未调用轴移动 | 高 | 待修 |
| B2 | `OnCapture` 用Random模拟数据，未调用视觉服务 | 高 | 待修 |
| B3 | `OnCorrect` 模拟补偿，未调用轴补偿移动 | 高 | 待修 |
| B4 | `OnTeachPhotoPos` 硬编码值，未读取当前轴位置 | 高 | 待修 |
| B5 | UI硬编码英文，缺少多语言 | 中 | 待修 |
| B6 | 标定数据无持久化 | 中 | 待修 |

## 实施方案

### Phase 1: ILoadUnloadController 扩展 + 急停功能
- ILoadUnloadController 增加 `MoveToPositionAsync(double x, double y, double z, double rx, double rz)` 
- ILoadUnloadController 增加 `GetCurrentPositionsAsync()` 用于Teach
- LoadUnloadView 增加急停按钮（调用 `StopMotion()`）

### Phase 2: IStageCalibrationService 新建
- 接口定义：GoToPhotoPosition, CaptureFiducial, ApplyCorrection, TeachCurrentPosition
- 实现类注入 ILoadUnloadController + 视觉服务

### Phase 3: ProductCalibrationViewModel 重构
- FiducialData 注入 IStageCalibrationService
- OnGoToPhoto → 调用 controller.MoveToPositionAsync
- OnCapture → 调用视觉服务获取基准点坐标
- OnCorrect → 计算偏移并调用轴补偿
- OnTeachPhotoPos → 读取当前轴位置
- 增加JSON持久化

### Phase 4: LoadUnloadViewModel 空实现修复
- EditSitePositionAction → 打开位置编辑对话框
- EditGripperParameterAction → 打开夹爪参数编辑对话框
- GripperOperationAction → 调用夹爪服务
- View3DScanDataAction → 导航到ZScanView

### Phase 5: 多语言 + UI优化
- 所有硬编码文本替换为 {lang:Lang} 标记
- 更新 Strings.zh-CN.xaml / Strings.en-US.xaml

### Phase 6: 测试 + 构建验证

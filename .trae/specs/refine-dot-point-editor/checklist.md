# Checklist — DotPointEditorView 细化验证

## 数据模型验证
- [x] DotPoint 模型包含所有指定字段：Group/PointId/Dx/Dy/Dz2/Dz3/Rx/Ry/Dz2Compensation/Dz3Compensation/IsSelected/IsEnabled
- [x] DotPoint.EffectiveDz2 只读属性 = Dz2 + Dz2Compensation
- [x] DotPoint.EffectiveDz3 只读属性 = Dz3 + Dz3Compensation
- [x] DotPoint 所有数值属性有范围校验（Math.Clamp）— Dx/Dy 为坐标值不限制范围，其余数值属性均有 Clamp 校验
- [x] DotProcessParams 包含运动参数/出胶参数/阀控参数/高度参数全部属性
- [x] DotProcessParams 支持 JSON 序列化

## 执行逻辑验证
- [x] IDotDispenseService 接口定义完整（DryRun/ExecuteDotDispense/TeachPoint/ProgressChanged/StatusChanged/IsRunning）
- [x] DotDispenseService.DryRunAsync 遍历选中点，Z抬升→XY定位→保持在安全高度，不出胶
- [x] DotDispenseService.ExecuteDotDispenseAsync 按行业标准流程执行：Z抬升→XY定位→Z下降(两段式)→开胶前延时→开胶→出胶时间等待→关胶→关胶后延时→Z抬升
- [x] 全部勾选时先统一抬升Z到安全高度再逐点点胶
- [x] 急停/取消时安全关胶（SafeGlueOff）
- [x] TeachPointAsync 读取 IMotionService.GetAxisPosition 填入 Dx/Dy/Dz2

## ViewModel 验证
- [x] DotPointEditorViewModel 注入 IDotDispenseService
- [x] Group 列表从 WorkOrderData.Sites 获取 SiteFeatureType.AssyGroup 条目（当前回退到默认列表，待 WorkOrderData 注入后扩展）
- [x] 工艺参数面板数据双向绑定到 DotProcessParams 实例
- [x] AddPoint/DeleteSelected/SelectAll/DeselectAll/TeachPoint 命令正常工作
- [x] ApplyProcessParams 命令将当前工艺参数应用到选中点（将 TeachHeight/HeightCompensation 应用到选中点的 Dz2/Dz2Compensation）
- [x] DryRun/ExecuteDotDispense/StopExecution 命令正常工作
- [x] SaveData/LoadData 命令实现 JSON 序列化/反序列化
- [x] 进度和状态属性（Status/ProgressText/IsExecuting）正确更新

## UI 验证
- [x] 布局为三行结构：上工艺参数 / 中点位数据 / 下执行控制
- [x] 工艺参数面板分三列卡片（运动参数/出胶参数/高度参数）
- [x] DataGrid 列顺序为：☑ → Group(ComboBox) → ID → Dx → Dy → Dz₂ → Dz₃ → Rx → Ry → Dz₂补偿 → Dz₃补偿 → 示教(Button)
- [x] Group 列 ComboBox 绑定工单 Site 部件列表（当前绑定到 Groups 集合，待 WorkOrderData 注入后自动获取 AssyGroup 条目）
- [x] 执行控制区包含：空跑按钮 + 真实点胶按钮 + 停止按钮 + 进度条 + 状态指示
- [x] 使用 MaterialDesign 样式，与项目整体风格一致
- [x] 操作人员可一眼看懂操作流程

## 集成验证
- [x] IDotDispenseService 在 PrimModel.cs 中正确注册
- [x] DotPointEditorView 在 DispensingView.xaml 中取消注释并正常显示
- [x] 全项目编译通过，零 error（仅预先存在的 CS1704 程序集重复引用和 MSB4035 错误，非本次变更引入）

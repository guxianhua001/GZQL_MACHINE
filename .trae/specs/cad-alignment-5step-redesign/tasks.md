# Tasks

- [ ] Task 1: 扩展 CorrespondencePoint 模型
  - [ ] 在 `Core/Models/CorrespondencePoint.cs` 新增 RotatedX/RotatedY/RotatedZ 属性（double, BindableBase）

- [ ] Task 2: 重构 CadAlignmentViewModel — 数据模型与步骤导航
  - [ ] 步骤数从 3 改为 5：更新 InitializeSteps()、CurrentStepTitle、CanGoNext(上限5)、ShowConnector(Number<5)、UpdateStepStates
  - [ ] 新增步骤1属性：四点拟合集合(ObservableCollection<FitPoint>)、回转中心 Mox/Moy、拟合半径 R
  - [ ] 新增步骤2属性：P1机械坐标 P1Mx/P1My、全局偏移 DeltaX/DeltaY
  - [ ] 新增步骤3属性：基准点对索引(BasePairIndex)、目标点对索引(TargetPairIndex)、基准角AlphaBase、目标角AlphaTarget、旋转角ThetaDeg
  - [ ] 新增步骤4属性：选中变换点(TransformSelectedPoint)、中间变量 Xm/Ym/dx/dy
  - [ ] 新增步骤5属性：夹爪偏移 GripperOffX/GripperOffY、最终位置 FinalGripperX/FinalGripperY
  - [ ] 清理旧属性：移除 CenterX/Y/Z、RotationAngleDeg、CorrectedAngleDeg、CorrectedCenterX/Y/Z、InputX/Y/Z、OutputX/Y/Z、RotatedPointX/Y/Z、DeviationX/Y/Z、CalibrationStatus、UseCorrectedAngle、UseCoordinateTransform、AssemblyRefRx/Rz/Y、CameraRefDx/Dy/Dz1
  - [ ] 清理旧命令：移除 EstimateRotationCenterCommand、ComputeCalibrationCommand、CalibrateCoordinateSystemCommand、ComputeDeviationCommand
  - [ ] 新增5个命令：FitRotationCenterCommand、ComputeGlobalOffsetCommand、ComputeCadRotationAngleCommand、ExecuteTransformCommand、ComputeGripperPositionCommand

- [ ] Task 3: 实现 5 个核心计算方法
  - [ ] FitRotationCenter()：最小二乘法四点圆拟合，输出 Mox/Moy/R，含默认验证数据断言
  - [ ] ComputeGlobalOffset()：ΔX = Mx-Cx, ΔY = My-Cy
  - [ ] ComputeCadRotationAngle()：atan2 向量方向角计算 θ = α_base - α_target
  - [ ] ExecuteTransform()：先平移(Xm=Cx+ΔX) 后绕中心旋转(X_new公式)，支持单点和批量两种模式
  - [ ] ComputeGripperPosition()：Gripper_X = X_new + OffX, Gripper_Y = Y_new + OffY

- [ ] Task 4: 重写 CadAlignmentView.xaml — 5 Tab 布局
  - [ ] TabControl 从 3 个 TabItem 扩展为 5 个（对应5个步骤）
  - [ ] Tab1 回转中心：四点输入 DataGrid + 拟合按钮 + 结果(Mox/Moy/R) + 公式说明
  - [ ] Tab2 全局偏移：P1机械坐标输入 + P1 CAD坐标显示 + 计算按钮 + ΔX/ΔY结果
  - [ ] Tab3 旋转角度：基准点对选择器 + 目标点对选择器 + 方向角显示 + θ 结果
  - [ ] Tab4 坐标变换：点位选择 + 中间变量展示(Xm/Ym/dx/dy) + 变换按钮 + X_new/Y_new结果 + 批量变换按钮
  - [ ] Tab5 夹爪定位：目标点坐标继承 + 夹爪偏移输入 + 定位按钮 + 最终坐标(Gripper_X/Y) + 易错要点面板
  - [ ] 底部状态栏：进度圆点从 3 个扩展为 5 个(StepDotColor1~5)
  - [ ] 保持现有样式体系：SectionCard / RefCard / ResultBorder / PrimaryActionButton 等 Style 不变

- [ ] Task 5: 扩展 StepIndicatorConverters
  - [ ] 新增 StepDotColor4 / StepDotColor5 Converter
  - [ ] 更新 AlignmentStepInfo.ShowConnector => Number < 5

- [ ] Task 6: 构建验证与全流程测试
  - [ ] dotnet build 通过（0 error）
  - [ ] 验证默认数据加载正确（6个CorrespondencePoint + 4个拟合点）
  - [ ] 逐步执行 ①→②→③→④→⑤ 验证每步计算结果与 spec 中的预期值一致

# Task Dependencies
- [Task 2] depends on [Task 1]
- [Task 3] depends on [Task 2]
- [Task 4] depends on [Task 2]
- [Task 5] depends on [Task 4]
- [Task 6] depends on [Task 3], [Task 4], [Task 5]

# Tasks

- [x] Task 1: 步骤1 — FitPoints DataGrid 追加示教按钮列
  - [x] CadAlignmentViewModel 新增 TeachFitPointCommand（参数 int rowIndex）
  - [x] TeachFitPointCommand 执行逻辑：提示用户确认轴位置 → 读取当前机械坐标 → 回填 FitX/FitY
  - [x] CadAlignmentView.xaml Tab1 DataGrid 列定义扩展：角度|X(FitX)|Y(FitY)|操作，操作列放 SecondaryActionButton(CrosshairsGps 图标)
  - [x] 按钮绑定 Command + CommandParameter={Binding Index}（FitPoint 模型新增 Index 属性）

- [x] Task 2: 步骤3 — 从CAD图形选取线段端点
  - [x] CadAlignmentViewModel 新增 PickBaselineFromCadCommand / PickTargetFromCadCommand / HasCadDrawingLoaded 属性
  - [x] 实现图形选取交互：调用 CAD 点选取服务 → 返回两点坐标 → 填入 CorrespondencePoint.CadX/CadY → 更新 BasePairIndex/TargetPairIndex
  - [x] CadAlignmentView.xaml Tab3 SectionCard1: 两个 ComboBox 旁各加「从CAD选取」SecondaryActionButton（CrosshairsGps 图标）
  - [x] 无 CAD 图形时按钮禁用（HasCadDrawingLoaded == false）

- [x] Task 3: 步骤4 — 继承步骤3目标点位快捷入口
  - [x] CadAlignmentViewModel 新增 InheritTargetFromStep3Command / CanInheritFromStep3 计算属性
  - [x] InheritTargetFromStep3Command: 将 TransformSelectedIndex 设为 TargetPairIndex 对应的第一个点索引
  - [x] CadAlignmentView.xaml Tab4 SectionCard1: ComboBox 上方添加「↓ 用步骤3目标」SecondaryActionButton（ArrowDownBoldHexagonOutline 图标）
  - [x] 按钮 CanExecute 绑定 CanInheritFromStep3

- [x] Task 4: 步骤5 — 夹爪基准点示教 + 双模式偏移
  - [x] CadAlignmentViewModel 新增 TeachX/Y/Ry/Z 属性（BindableBase double）
  - [x] 新增 CalcOffX/CalcOffY 只读计算属性（= TeachX - TransResultX / TeachY - TransResultY）
  - [x] 新增 UseCalculatedOffset bool 属性、ApplyCalcOffsetCommand、TeachGripperPositionCommand
  - [x] TeachGripperPositionCommand: 读取末端执行器坐标 → 赋值 TeachX/Y/Ry/Z
  - [x] ApplyCalcOffsetCommand: CalcOffX/Y → GripperOffX/Y, UseCalculatedOffset = true
  - [x] 修改 ComputeGripperPosition(): 根据 UseCalculatedOffset 选择使用计算偏移还是固定偏移
  - [x] CadAlignmentView.xaml Tab5 SectionCard1 重构: 示教区(4只读框+🎯示教按钮) + 双偏移区(RadioButton切换+计算偏移ΔX/ΔY+应用按钮 + 固定偏移OffX/OffY)

- [x] Task 5: 构建验证与全流程测试
  - [x] dotnet build 通过（0 error, 0 XAML error）
  - [x] 验证步骤1示教按钮可见且可点击（命令绑定正确）
  - [x] 验证步骤3「从CAD选取」按钮存在且无CAD时禁用
  - [x] 验证步骤4继承按钮在步骤3未完成时禁用
  - [x] 验证步骤5示教+双偏移UI布局正确，两种偏移模式切换正常
  - [x] 验证原有5步流程功能不受影响（回归测试）

# Task Dependencies
- [Task 2] depends on [] (独立)
- [Task 3] depends on [Task 2]
- [Task 4] depends on [Task 3]
- [Task 5] depends on [Task 1], [Task 2], [Task 3], [Task 4]

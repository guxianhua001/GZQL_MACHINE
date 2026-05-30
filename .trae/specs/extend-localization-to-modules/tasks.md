# Tasks

- [x] Task 1: 扫描并识别所有语言 key 的使用情况
  - [x] 1.1: 从 `Strings.zh-CN.xaml` 提取所有已定义的 key 列表（1187个）
  - [x] 1.2: 在所有 `.xaml` 文件中搜索 `DynamicResource Key` 引用，建立 key→文件映射
  - [x] 1.3: 在所有 `.xaml` 文件中搜索 `lang:Lang Key` 引用，合并到 key→文件映射
  - [x] 1.4: 在所有 `.cs` 文件中搜索 `GetResource("Key")` 和 `L("Key")` 引用，合并到 key→文件映射
  - [x] 1.5: 计算差集，输出未使用 key 列表

- [x] Task 2: 迁移 Module/Controls/Assembly 模块（DynamicResource + 硬编码）
  - [x] 2.1: `CadAlignmentView.xaml` — 12处硬编码→lang:Lang
  - [x] 2.2: `CadAlignmentPrincipleWindow.xaml` — 30处DynamicResource→lang:Lang + 1个bug修复(Principle_CenterLabelP→Principle_CenterLabel)
  - [x] 2.3: `AssemblyStepView.xaml` — 57处硬编码→lang:Lang（14处已有key + 43处新增key）
  - [x] 2.4: `AssemblyAxesView.xaml` — 5处硬编码→lang:Lang + emoji→PackIcon重构
  - [x] 2.5: `ZScanView.xaml` — 4处硬编码→lang:Lang
  - [x] 2.6: `DetailedDataView.xaml` — 22处DynamicResource→lang:Lang
  - [x] 2.7: `WaypointEditView.xaml` — 10处DynamicResource→lang:Lang
  - [x] 2.8: 编译验证 Assembly 模块 ✅

- [x] Task 3: 迁移 Module/Controls/Dispense 模块（硬编码）
  - [x] 3.1: `DispensingView.xaml` — 1处硬编码→lang:Lang
  - [x] 3.2: `SetupCalibrationView.xaml` — 6处硬编码→lang:Lang
  - [x] 3.3: `CadPointEditor3DView.xaml` — 17处硬编码→lang:Lang
  - [x] 3.4: `InspectionView.xaml` — 6处硬编码→lang:Lang
  - [x] 3.5: `AutoPathsGenerationView.xaml` — 12处硬编码→lang:Lang
  - [x] 3.6: `PathConfigView.xaml` — 16处硬编码→lang:Lang
  - [x] 3.7: `DispenserAxesView.xaml` — 2处硬编码→lang:Lang + emoji→PackIcon重构
  - [x] 3.8: 编译验证 Dispense 模块 ✅

- [x] Task 4: 迁移 Module/Controls/Loading 模块（硬编码）
  - [x] 4.1: `LoadUnloadView.xaml` — 40处硬编码→lang:Lang
  - [x] 4.2: `ProductCalibrationView.xaml` — 无需迁移（无硬编码文本）
  - [x] 4.3: 编译验证 Loading 模块 ✅

- [x] Task 5: 迁移 Module/Controls/StepDetails 模块（硬编码）
  - [x] 5.1: `CheckDetailView.xaml` — 21处硬编码→lang:Lang
  - [x] 5.2: `ScanDetailView.xaml` — 44处硬编码→lang:Lang
  - [x] 5.3: `SeekDetailView.xaml` — 15处硬编码→lang:Lang
  - [x] 5.4: `AlignDetailView.xaml` — 3处硬编码→lang:Lang
  - [x] 5.5: `PickDetailView.xaml` — 13处硬编码→lang:Lang
  - [x] 5.6: `VisionDetailView.xaml` — 22处硬编码→lang:Lang
  - [x] 5.7: `DataDashboardView.xaml` — 17处硬编码→lang:Lang
  - [x] 5.8: `ConditionBranchView.xaml` — 16处硬编码→lang:Lang
  - [x] 5.9: `GotoDetailView.xaml`(3处)、`WaitDetailView.xaml`(7处)、`ScriptDetailView.xaml`(13处)
  - [x] 5.10: 编译验证 StepDetails 模块 ✅

- [x] Task 6: 迁移 Module/Controls/Configuration + Grippers + StepEditor 模块
  - [x] 6.1: `Camera2DView.xaml` — 5处硬编码→lang:Lang
  - [x] 6.2: `IPQCView.xaml` — 23处硬编码→lang:Lang
  - [x] 6.3: `WorkOrderConfigView.xaml` — 7处硬编码→lang:Lang
  - [x] 6.4: `GripperControlView.xaml` — 15处硬编码→lang:Lang
  - [x] 6.5: `ProcessSequenceEditorView.xaml` — 无需迁移（无硬编码文本）
  - [x] 6.6: 编译验证 ✅

- [x] Task 7: 迁移 AlarmModule 模块
  - [x] 7.1: `AlarmThresholdView.xaml` — 14处硬编码→lang:Lang
  - [x] 7.2: `AlarmStatsView.xaml` — 7处硬编码→lang:Lang
  - [x] 7.3: `AlarmListView.xaml` — 13处硬编码→lang:Lang
  - [x] 7.4: `AlarmHistoryView.xaml` — 26处硬编码→lang:Lang
  - [x] 7.5: 编译验证 AlarmModule ✅

- [x] Task 8: 迁移 RecipeManagement + TCPIPModule 模块
  - [x] 8.1: `RecipeManagerView.xaml` — 27处硬编码→lang:Lang
  - [x] 8.2: `TcpConfigView.xaml` — 28处硬编码→lang:Lang
  - [x] 8.3: 编译验证 ✅

- [x] Task 9: 迁移 MotionControl 模块
  - [x] 9.1: `SingleAxisControlView.xaml` — 15处硬编码→lang:Lang
  - [x] 9.2: `AxisControlPanelView.xaml` — 3处硬编码→lang:Lang
  - [x] 9.3: 编译验证 MotionControl ✅

- [x] Task 10: 迁移 Framework 模块
  - [x] 10.1: `ParameterEditorView.xaml` — 8处硬编码→lang:Lang
  - [x] 10.2: `RecipeSelectionDialog.xaml` — 2处硬编码→lang:Lang
  - [x] 10.3: `CancelableOperationDialog.xaml` — 1处硬编码→lang:Lang
  - [x] 10.4: `MessageDialog.xaml` — 无需迁移（全部为Binding）
  - [x] 10.5: `BusyIndicatorView.xaml` — 1处硬编码→lang:Lang
  - [x] 10.6: `RecipeEditorDialog.xaml`(4处)、`ConfigurationView.xaml`(11处)
  - [x] 10.7: 编译验证 Framework ✅

- [x] Task 11: 迁移 ModuleCore 剩余文件
  - [x] 11.1: `ErrorDialog.xaml` — 1处硬编码→lang:Lang
  - [x] 11.2: `ErrorMessage.xaml` — 1处硬编码→lang:Lang
  - [x] 11.3: `ConfirmationDialog.xaml` — 2处硬编码→lang:Lang
  - [x] 11.4: `DeviceConfigView.xaml` — 25处硬编码→lang:Lang
  - [x] 11.5: `ThresholdWarningNotificationView.xaml` — 2处硬编码→lang:Lang
  - [x] 11.6: `WindowAutoClosedSuccess.xaml` — 2处硬编码→lang:Lang
  - [x] 11.7: `AxisSettingView.xaml` — 13处硬编码→lang:Lang
  - [x] 11.8: 编译验证 ModuleCore ✅

- [x] Task 12: 迁移 Interfaces 模块
  - [x] 12.1: `LoadingDialog.xaml` — 4处硬编码→lang:Lang
  - [x] 12.2: `NotificationDialog.xaml` — 1处硬编码→lang:Lang
  - [x] 12.3: 编译验证 Interfaces ✅

- [x] Task 13: 同步语言资源文件
  - [x] 13.1: zh-CN 新增 21 个 key（15个来自en-US + 6个代码引用缺失key）
  - [x] 13.2: en-US 新增 462 个 key（与zh-CN同步）
  - [x] 13.3: 两个文件现在拥有完全相同的 1675 个 key，0 个差异
  - [x] 13.4: 修复 en-US 中 XML 转义问题（`&` → `&amp;`）
  - [x] 13.5: 编译验证 ✅

- [x] Task 14: 全量编译验证
  - [x] 14.1: 全项目编译零 error ✅
  - [x] 14.2: 修复 ScriptDetailView.xaml 中 x:Name="ScriptCodeEditor" 误删问题
  - [x] 14.3: 所有模块 UI 文字通过 lang:Lang 绑定，语言切换可正常工作

# Task Dependencies
- [Task 1] 是核心依赖，Task 13 依赖它
- [Task 2-12] 互相独立，可并行执行
- [Task 13] 依赖 [Task 2-12] 全部完成
- [Task 14] 依赖所有前置任务

# Tasks

* [x] Task 1: 增强 ScanDetail 模型，添加 3D 相机专用配置字段

  * [x] SubTask 1.1: 在 ScanDetail 类中添加运动配置字段（ZAxisId, XAxisId, ZInitPosition, XStartPosition, ZPhotoPosition, XEndPosition, ZSafePosition, XStandbyPosition, MoveSpeed）

  * [x] SubTask 1.2: 在 ScanDetail 类中添加 IO 配置字段（TriggerIoPort, IoResetDelayMs）

  * [x] SubTask 1.3: 在 ScanDetail 类中添加通讯配置字段（CommunicationType, ConnectionName, ResponseTimeout）

  * [x] SubTask 1.4: 在 ScanDetail 类中添加数据解析字段（ParseScript, VariableMappings, TabCount, TabHeightKeys）

* [x] Task 2: 实现 Camera3DDataParser 3D相机数据解析器

  * [x] SubTask 2.1: 创建 Camera3DDataParser 类，实现 IVisionDataParser 接口

  * [x] SubTask 2.2: 实现 `Camera=3DCAMERA;VISION_RESULT:SUCCESS:val1,val2,...` 格式解析逻辑

  * [x] SubTask 2.3: 处理 FAIL 状态和非标准格式的异常情况

* [x] Task 3: 实现 Scan3DStepAction 3D相机扫描步骤动作

  * [x] SubTask 3.1: 创建 Scan3DStepAction 类，实现 IProcessStepAction，SupportedStepType = SCAN

  * [x] SubTask 3.2: 实现7步工作流编排（Z抬升→X起始→Z下降→IO触发→X终点+TCP接收→Z安全→X待机）

  * [x] SubTask 3.3: 实现 IO 触发异步自动复位（不阻塞后续流程）

  * [x] SubTask 3.4: 实现 X 轴移动期间 TCP 实时数据接收与解析

  * [x] SubTask 3.5: 实现解析结果到全局变量的映射

  * [x] SubTask 3.6: 在 StationTasksModule 中注册 Scan3DStepAction 为 IProcessStepAction 单例

* [x] Task 4: 重构 ScanDetailViewModel，迁移到 DialogHost 模式并添加 3D 相机配置

  * [x] SubTask 4.1: 移除 INavigationAware 实现，改为直接绑定 ProcessStep 属性（与 GotoDetailViewModel/VisionDetailViewModel 一致）

  * [x] SubTask 4.2: 添加运动配置属性（轴选择、位置名下拉、速度）

  * [x] SubTask 4.3: 添加 IO 配置属性（端口号、复位延时）

  * [x] SubTask 4.4: 添加通讯配置属性（通讯方式、TCP连接下拉、超时）

  * [x] SubTask 4.5: 添加数据解析属性（解析脚本、Tab数量、变量映射）

  * [x] SubTask 4.6: 添加数据解析面板属性（Tab高度结果表格）

  * [x] SubTask 4.7: 添加执行测试功能（示例数据填充、执行测试、结果展示）

  * [x] SubTask 4.8: 实现 InitializeFromStep/OnSave 方法，与 Step.ScanDetail 双向绑定

* [x] Task 5: 重构 ScanDetailView\.xaml UI 布局

  * [x] SubTask 5.1: 重新设计整体布局为 DialogHost 模态弹窗风格

  * [x] SubTask 5.2: 实现运动配置区（Z/X轴选择、位置名下拉、速度输入）

  * [x] SubTask 5.3: 实现 IO 配置区（端口号、复位延时）

  * [x] SubTask 5.4: 实现通讯配置区（通讯方式、TCP连接、超时）

  * [x] SubTask 5.5: 实现数据解析区（脚本编辑、Tab数量、变量映射表格）

  * [x] SubTask 5.6: 实现数据解析面板（Tab高度值表格，含编号/上限/下限/实测值/偏差/状态列）

  * [x] SubTask 5.7: 实现执行测试区（示例数据、执行按钮、结果展示）

* [x] Task 6: 添加 SCAN 步骤路由和 DI 注册

  * [x] SubTask 6.1: 在 ProcessSequenceEditorViewModel.NavigateToDetailView 中添加 SCAN 分支

  * [x] SubTask 6.2: 在 StationTasksModule.RegisterTypes 中注册 Scan3DStepAction

  * [x] SubTask 6.3: 在 StationTasksModule.RegisterTypes 中注册 Camera3DDataParser

* [x] Task 7: 全量编译验证

  * [x] SubTask 7.1: 执行 dotnet build 确保无编译错误

# Task Dependencies

* \[Task 2] depends on \[Task 1] — 解析器需要 ScanDetail 中的 TabCount/TabHeightKeys 字段定义

* \[Task 3] depends on \[Task 1] \[Task 2] — StepAction 需要 ScanDetail 模型和 Camera3DDataParser

* \[Task 4] depends on \[Task 1] — ViewModel 需要 ScanDetail 新字段

* \[Task 5] depends on \[Task 4] — View 绑定 ViewModel 属性

* \[Task 6] depends on \[Task 3] \[Task 4] \[Task 5] — 路由和注册依赖 Action/ViewModel/View 完成

* \[Task 7] depends on \[Task 6] — 编译验证在所有代码完成后执行


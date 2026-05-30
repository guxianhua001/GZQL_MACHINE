# Tasks

- [x] Task 1: 创建 PhotoPositionRow ViewModel 模型
  - [x] SubTask 1.1: 创建 PhotoPositionRow 类，包含 SiteFeatureName、位置名属性、速度、触发命令、TCP连接名、超时、点胶类型、Arc专用字段
  - [x] SubTask 1.2: 创建 DispenseType 枚举（Dot, Arc）
  - [x] SubTask 1.3: 实现 IPositionProvider 集成，动态加载 AvailablePositions 列表

- [x] Task 2: 创建 VisionCaptureService 拍照执行服务
  - [x] SubTask 2.1: 实现 IPositionProvider 位置读取集成
  - [x] SubTask 2.2: 实现安全检查逻辑（验证位置名有效、安全高度已配置）
  - [x] SubTask 2.3: 实现拍照执行流程：Z轴抬起→XY移动→Z下降→触发拍照→等待数据→Z抬起→返回待机
  - [x] SubTask 2.4: 实现超时处理和 RecoverableException 抛出
  - [x] SubTask 2.5: 实现 CancellationToken 支持（急停/停止打断，Z轴优先抬起）
  - [x] SubTask 2.6: 注册到 DI 容器

- [x] Task 3: 创建 BezierArcDispenseService 贝塞尔弧线点胶服务
  - [x] SubTask 3.1: 实现二次贝塞尔曲线离散化算法
  - [x] SubTask 3.2: 实现视觉坐标→机械坐标转换（平移+旋转）
  - [x] SubTask 3.3: 实现 Dot 点胶执行逻辑
  - [x] SubTask 3.4: 实现 Arc 弧形点胶执行逻辑（多段直线插补走胶）
  - [x] SubTask 3.5: 注册到 DI 容器

- [x] Task 4: 重构 AutoPathsGenerationViewModel → VisionCaptureViewModel
  - [x] SubTask 4.1: 注入 IRecipePoolService、IPositionProvider、ITCPEventService、IMotionService、VisionCaptureService、BezierArcDispenseService
  - [x] SubTask 4.2: 实现 Group/SiteFeature 加载逻辑（从 WorkOrderData 读取）
  - [x] SubTask 4.3: 实现拍照位配置表 PhotoPositionRows 集合
  - [x] SubTask 4.4: 实现执行命令（调用 VisionCaptureService）
  - [x] SubTask 4.5: 实现视觉数据显示逻辑
  - [x] SubTask 4.6: 实现 Dot/Arc 点胶执行命令
  - [x] SubTask 4.7: 实现坐标系转换参数配置和保存

- [x] Task 5: 重构 AutoPathsGenerationView.xaml → VisionCaptureView.xaml
  - [x] SubTask 5.1: Group 选择 ComboBox + SiteFeature 列表区域
  - [x] SubTask 5.2: 拍照位配置 DataGrid（SiteFeature名 | Dx位置名 | Dy位置名 | Dz₁位置名 | Y位置名 | 速度 | 触发命令 | [执行]）
  - [x] SubTask 5.3: 执行状态和数据显示区域
  - [x] SubTask 5.4: 点胶执行区域（Dot/Arc 模式切换和执行按钮）
  - [x] SubTask 5.5: 坐标系转换参数配置区域

- [x] Task 6: 集成到 DispensingView
  - [x] SubTask 6.1: 修改 DispensingView.xaml 区域7，激活 VisionCaptureView
  - [x] SubTask 6.2: 更新 PrimModel.cs 注册 VisionCaptureView/ViewModel 映射
  - [x] SubTask 6.3: 清理旧 AutoPathsGenerationView 相关代码

# Task Dependencies
- [Task 2] depends on [Task 1] (PhotoPositionRow 是 VisionCaptureService 的输入模型)
- [Task 3] depends on [Task 1] (PhotoPositionRow 包含 Arc 配置参数)
- [Task 4] depends on [Task 1, Task 2, Task 3] (ViewModel 依赖服务和模型)
- [Task 5] depends on [Task 4] (View 绑定 ViewModel)
- [Task 6] depends on [Task 5] (集成需要 View 完成)

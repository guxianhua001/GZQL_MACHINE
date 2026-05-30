# Tasks

## 阶段 1：模型与基础架构
- [ ] Task 1: 扩展 ZScanPointDetail 模型，增加 Description、Nominal、Range、DataIndex、Status 字段
  - [ ] 1.1 在 ZScanSummaryItem.cs 中修改 ZScanPointDetail 类，新增字段和属性变更通知
  - [ ] 1.2 保持向后兼容（原有字段 ZNominal 映射到 Nominal，FeatureName 映射到 Description）

## 阶段 2：UI 布局重构（ZScanDetailView.xaml）
- [ ] Task 2: 重构左侧图片可视化面板
  - [ ] 2.1 替换静态占位符为 Image 控件 + 导入按钮
  - [ ] 2.2 添加展开/缩回切换按钮（使用 `<materialDesign:PackIcon Kind="ChevronLeft/ChevronRight">`）
  - [ ] 2.3 实现 GridSplitter 或动画过渡效果支持面板宽度动态调整
  - [ ] 2.4 添加图片缩放控制（可选：缩放按钮或鼠标滚轮）

- [ ] Task 3: 增强右侧数据栏顶部区域
  - [ ] 3.1 新增运动控制按钮组：3D扫描（主色调）、停止（红色）、回待机位（次要色）
  - [ ] 3.2 使用 `<materialDesign:PackIcon>` 图标：`PlayArrow`/`Stop`/`HomeOutline`
  - [ ] 3.3 添加通讯协议下拉菜单（ComboBox），绑定 CommunicationTypes 集合
  - [ ] 3.4 添加 TCP 连接名称下拉框（条件显示：当选择 TCPIP 时）
  - [ ] 3.5 保留原有的统计信息展示（TotalPoints、ZNominalRange、ZMaxDelta、StatusText）

- [ ] Task 4: 重构数据表格列定义
  - [ ] 4.1 将 Feature 列改为 Description 列（TextBox 可编辑）
  - [ ] 4.2 新增 Nominal 列（可编辑，绑定 Nominal 字段）
  - [ ] 4.3 新增 Range 列（可编辑，绑定 Range 字段）
  - [ ] 4.4 新增 DataIndex 列（整数输入，用于配置数据接收序号）
  - [ ] 4.5 保留 Seg、Pt#、X、Y、Z_actual、ΔZ、Status 列
  - [ ] 4.6 Status 列使用颜色标识（Green=Pass, Red=Fail, Gray=Pending）

- [ ] Task 5: 完善底部操作按钮区
  - [ ] 5.1 保留 Add Row、Delete Selected、Import CSV、Export CSV 按钮
  - [ ] 5.2 移除原有的"Re-Scan This Site"按钮（功能已集成到顶部运动控制区）

## 阶段 3：ViewModel 业务逻辑增强
- [ ] Task 6: 实现图片管理功能
  - [ ] 6.1 添加 ImportImageCommand，调用 OpenFileDialog 选择图片文件
  - [ ] 6.2 添加 ImagePath 属性存储图片路径
  - [ ] 6.3 添加 IsPanelExpanded 属性控制面板展开/缩回状态
  - [ ] 6.4 添加 TogglePanelCommand 切换面板状态

- [ ] Task 7: 实现运动控制逻辑
  - [ ] 7.1 注入运动控制相关服务（IMotionControlService 或类似接口）
  - [ ] 7.2 实现 Start3DScanCommand：执行扫描序列（参考 ScanDetailViewModel 的运动参数）
    - 移动到起始位置 → 触发相机 → 等待数据 → 更新表格
  - [ ] 7.3 实现 StopCommand：立即停止运动（调用急停接口）
  - [ ] 7.4 Implement ReturnToStandbyCommand：移动轴到待机位
  - [ ] 7.5 添加 IsScanning 状态属性，控制按钮启用/禁用

- [ ] Task 8: 实现通讯配置与数据接收
  - [ ] 8.1 复用 ScanDetailViewModel 的通讯配置逻辑（ITCPClientManagerService、ITCPEventService）
  - [ ] 8.2 加载已配置的 TCP 连接列表
  - [ ] 8.3 实现 SubscribeCameraData 方法订阅 TCP 数据事件
  - [ ] 8.4 解析 `Camera=3DCAMERA;VISION_RESULT:SUCCESS:value1,value2,...` 格式
  - [ ] 8.5 按 DataIndex 匹配数值到对应行

- [ ] Task 9: 实现自动计算引擎
  - [ ] 9.1 当 ZMeasured 变化时触发 RecalculateRow 方法
  - [ ] 9.2 计算 DeltaZ = ZMeasured - Nominal
  - [ ] 9.3 判定 Status = (Math.Abs(DeltaZ) <= Range) ? "Pass" : "Fail"
  - [ ] 9.4 调用 RecalculateStatistics 更新全局统计信息
  - [ ] 9.5 监听 PointDetails 集合变化，实时更新 TotalPoints、ZMaxDelta 等

- [ ] Task 10: 完善 CSV 导入导出逻辑
  - [ ] 10.1 更新导入逻辑以支持新字段（Description、Nominal、Range、DataIndex）
  - [ ] 10.2 更新导出逻辑包含所有字段
  - [ ] 10.3 导入后自动触发重新计算

## 阶段 4：视图集成
- [ ] Task 11: 改造 ZScanView 以嵌入 ZScanDetailView
  - [ ] 11.1 移除 ZScanView.xaml 中原有的三个 Card 内容块
  - [ ] 11.2 直接在 Grid 中引用 `<views:ZScanDetailView />`
  - [ ] 11.3 调整 ViewModel 传递机制（确保 ZScanDetailView 能获取必要的服务注入）
  - [ ] 11.4 保留页面标题"Z-SCAN"

## 阶段 5：验证与优化
- [ ] Task 12: 验证功能完整性
  - [ ] 12.1 测试图片导入和面板展开/缩回
  - [ ] 12.2 测试运动控制按钮的启用/禁用逻辑
  - [ ] 12.3 测试 TCP 数据接收和解析流程
  - [ ] 12.4 测试自动计算 DeltaZ 和 Status 的准确性
  - [ ] 12.5 测试 CSV 导入导出的数据完整性
  - [ ] 12.6 验证 ZScanView 正确显示 ZScanDetailView 内容

# Task Dependencies
- [Task 2] depends on [Task 1] （模型先于 UI）
- [Task 3] depends on [Task 1]
- [Task 4] depends on [Task 1]
- [Task 6] depends on [Task 1]
- [Task 7] depends on [Task 3] （UI 按钮先于命令实现）
- [Task 8] depends on [Task 3]
- [Task 9] depends on [Task 4, Task 8] （表格和数据接收就绪后才能计算）
- [Task 10] depends on [Task 4]
- [Task 11] depends on [Task 2, Task 3, Task 4, Task 5] （所有 UI 组件完成后集成）
- [Task 12] depends on [Task 6, Task 7, Task 8, Task 9, Task 10, Task 11]

**并行执行建议**：
- Task 2, 3, 4, 5 可并行开发（UI 层）
- Task 6, 7, 8 可并行开发（业务逻辑层，依赖各自对应的 UI 任务）
- Task 9, 10 可并行开发（依赖前置任务完成后启动）

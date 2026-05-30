# Tasks

- [ ] Task 1: 迁移 VM.Halcon 项目到 .NET 9 并重命名为 Halcon
  - [ ] SubTask 1.1: 创建新的 Halcon.csproj（SDK 风格，net9.0-windows7.0 目标框架）
  - [ ] SubTask 1.2: 将 VM.Halcon 目录下所有源文件复制到新 Halcon 目录
  - [ ] SubTask 1.3: 全局替换命名空间 VM.Halcon → Halcon，VM.Halcon.Model → Halcon.Model，VM.Halcon.Config → Halcon.Config
  - [ ] SubTask 1.4: 更新 VMHWindowControl.Designer.cs 中的命名空间和控件引用
  - [ ] SubTask 1.5: 更新 halcondotnet.dll 引用路径指向 MainApp 输出目录
  - [ ] SubTask 1.6: 编译验证 Halcon 项目通过

- [ ] Task 2: 为 CadEntity 添加 ToHObject() 扩展方法
  - [ ] SubTask 2.1: 在 Core 项目中创建 CadEntityHalconExtensions 静态类
  - [ ] SubTask 2.2: 实现 CadLine.ToHObject() — GenContourPolygonXld
  - [ ] SubTask 2.3: 实现 CadArc.ToHObject() — GenArcXLD / 采样点方式
  - [ ] SubTask 2.4: 实现 CadCircle.ToHObject() — GenCircleContourXld
  - [ ] SubTask 2.5: 实现 CadLwPolyline.ToHObject() — GenContourPolygonXld
  - [ ] SubTask 2.6: 实现 CadEllipse.ToHObject() — GenEllipseContourXld
  - [ ] SubTask 2.7: 实现 CadEntityCollection.ToHObject() — 将所有图元合并为一个 HObject

- [ ] Task 3: 创建 HalconCanvasControl 替换 SkiaCanvasControl
  - [ ] SubTask 3.1: 创建 HalconCanvasControl.xaml — 使用 WindowsFormsHost 嵌入 VMHWindowControl
  - [ ] SubTask 3.2: 创建 HalconCanvasControl.xaml.cs — 初始化 VMHWindowControl，暴露 DependencyProperty
  - [ ] SubTask 3.3: 实现 Entities DP → 转换为 HObject 并在 HWindow 中渲染
  - [ ] SubTask 3.4: 实现 CoordinateChanged 事件 — 从 HWindow 鼠标坐标转换回 CAD 坐标
  - [ ] SubTask 3.5: 实现 EntitySelected 事件 — 基于坐标命中检测匹配 CadEntity
  - [ ] SubTask 3.6: 实现 FitToAll() — 根据实体包围盒设置 HWindow 视图范围
  - [ ] SubTask 3.7: 实现 ResetView() — 重置 HWindow 视图

- [ ] Task 4: 创建 HalconCanvasViewModel 替换 SkiaCanvasViewModel
  - [ ] SubTask 4.1: 创建 HalconCanvasViewModel.cs — 管理实体集合、视图状态和命令
  - [ ] SubTask 4.2: 实现 AttachControl/DetachControl 方法 — 绑定到 HalconCanvasControl
  - [ ] SubTask 4.3: 实现 FitToAll/ResetView/ZoomIn/ZoomOut 命令

- [ ] Task 5: 添加点胶机必备 ROI 交互方法
  - [ ] SubTask 5.1: 在 HalconCanvasControl 中添加 ROI 绘制模式属性（DrawMode 枚举：无/旋转矩形/圆形/线段/折线/圆弧/涂抹/擦除）
  - [ ] SubTask 5.2: 实现 ROIRectangle2 交互绘制 — 使用 ROIController 管理 ROIRectangle2
  - [ ] SubTask 5.3: 实现 ROICircle 交互绘制 — 使用 ROIController 管理 ROICircle
  - [ ] SubTask 5.4: 实现 ROILine 线段交互绘制 — 使用 ROIController 管理 ROILine，拖拽端点调整起终点
  - [ ] SubTask 5.5: 实现 ROIPolyline 折线交互绘制 — 依次点击添加顶点，拖拽手柄调整，右键/双击结束
  - [ ] SubTask 5.6: 实现 ROICircularArc 圆弧交互绘制 — 使用 ROIController 管理 ROICircularArc，拖拽手柄调整圆心/半径/角度
  - [ ] SubTask 5.7: 实现涂抹模式 — 调用 VMHWindowControl.Paint() 方法
  - [ ] SubTask 5.8: 实现擦除模式 — 调用 VMHWindowControl.Eraser() 方法
  - [ ] SubTask 5.9: 添加 ROICompleted 事件 — 绘制完成后返回 HRegion 或 XLD 轮廓

- [ ] Task 6: 更新 CadPointEditorControl 使用 Halcon 窗口
  - [ ] SubTask 6.1: 修改 CadPointEditorControl.xaml — 将 SkiaCanvasControl 替换为 HalconCanvasControl
  - [ ] SubTask 6.2: 修改 CadPointEditorControl.xaml.cs — 更新事件注册和绑定
  - [ ] SubTask 6.3: 更新 CadPointEditorViewModel — 适配 HalconCanvasViewModel

- [ ] Task 7: 移除 SkiaSharp 依赖
  - [ ] SubTask 7.1: 从 Module.csproj 移除 SkiaSharp、SkiaSharp.Views.WPF 包引用
  - [ ] SubTask 7.2: 删除 SkiaCanvasControl.xaml(.cs) 和 SkiaCanvasViewModel.cs
  - [ ] SubTask 7.3: 在 MainApp.csproj 中添加 Halcon 项目引用（替换旧 VM.Halcon 引用）

- [ ] Task 8: 编译验证
  - [ ] SubTask 8.1: 执行 dotnet build 整个解决方案，确保 0 错误

# Task Dependencies
- [Task 2] depends on [Task 1] (需要 Halcon 命名空间和 halcondotnet 引用)
- [Task 3] depends on [Task 1] + [Task 2] (需要 VMHWindowControl 和 CadEntity.ToHObject)
- [Task 4] depends on [Task 3]
- [Task 5] depends on [Task 3] (ROI 交互需要 HalconCanvasControl 和 ROIController)
- [Task 6] depends on [Task 3] + [Task 4]
- [Task 7] depends on [Task 6] (确保替换完成后再删除旧代码)
- [Task 8] depends on [Task 7]

# Halcon 替换 SkiaCanvasControl Spec

## Why
当前 CAD 可视化和 ROI 交互使用 SkiaSharp（SkiaCanvasControl），存在以下问题：
1. SkiaSharp 是纯 2D 渲染引擎，不支持工业视觉标准 ROI 交互（矩形2、圆形、圆弧等拖拽手柄）
2. 项目已有 VM.Halcon 库（含 VMHWindowControl、ROIController、ROIRectangle2、ROICircle 等），但基于 .NET Framework 4.7.2，无法直接用于 .NET 9 项目
3. 点胶机必备的 ROI 交互（矩形区域选择、圆形胶路区域、涂抹/擦除掩膜）需要 Halcon 原生支持
4. CAD 读取后需在 Halcon 窗口中渲染，以便与视觉算法统一坐标系

## What Changes
- **BREAKING**：将 VM.Halcon 项目从 .NET Framework 4.7.2 迁移到 .NET 9，并重命名为 Halcon
- **BREAKING**：用 VMHWindowControl（WindowsFormsHost 嵌入）替换 SkiaCanvasControl（WPF 原生）
- 在 Halcon 项目中新增点胶机必备 ROI 交互方法（ROIRectangle2 旋转矩形、ROICircle 圆形、ROILine 线段、ROIPolyline 折线、ROICircularArc 圆弧、涂抹/擦除掩膜）
- 将 CadEntity 集合转换为 Halcon HObject 在 HWindow 中渲染
- 移除 SkiaSharp 相关依赖（SkiaSharp、SkiaSharp.Views.WPF 包引用）
- 更新 CadPointEditorControl 使用 WindowsFormsHost + VMHWindowControl

## Impact
- Affected specs: enhance-cad-point-editor（CAD 编辑器控件需适配 Halcon 窗口）
- Affected code:
  - `VM.Halcon/` → 迁移重命名为 `Halcon/`
  - `Module/Controls/SkiaCanvasControl.xaml(.cs)` → 替换为 HalconCanvasControl
  - `Module/ViewModels/SkiaCanvasViewModel.cs` → 替换为 HalconCanvasViewModel
  - `Module/Controls/CadPointEditorControl.xaml(.cs)` → 使用 WindowsFormsHost
  - `Module/Module.csproj` → 移除 SkiaSharp 包，添加 Halcon 项目引用
  - `Core/Models/CadEntity.cs` 及子类 → 新增 ToHObject() 转换方法

## ADDED Requirements

### Requirement: VM.Halcon 项目迁移到 .NET 9 并重命名为 Halcon
系统 SHALL 将 VM.Halcon 项目从 .NET Framework 4.7.2 迁移到 .NET 9（net9.0-windows7.0），项目名和命名空间重命名为 Halcon。

#### Scenario: 迁移后编译通过
- **WHEN** 执行 `dotnet build Halcon`
- **THEN** 项目编译成功，0 错误

#### Scenario: 命名空间变更
- **WHEN** 其他项目引用 Halcon
- **THEN** 使用 `using Halcon;` 和 `using Halcon.Model;` 替代 `using VM.Halcon;`

### Requirement: HalconCanvasControl 替换 SkiaCanvasControl
系统 SHALL 提供新的 HalconCanvasControl，使用 WindowsFormsHost 嵌入 VMHWindowControl，替代基于 SkiaSharp 的 SkiaCanvasControl。

#### Scenario: CAD 图元在 Halcon 窗口中渲染
- **WHEN** 传入 ObservableCollection<CadEntity>
- **THEN** 图元通过 CadEntity.ToHObject() 转换为 HObject 并在 HWindow 中显示

#### Scenario: 缩放和平移交互
- **WHEN** 用户在 HalconCanvasControl 上滚轮缩放或中键拖拽
- **THEN** 视图跟随缩放和平移，行为与原 VMHWindowControl 一致

#### Scenario: 图元选中交互
- **WHEN** 用户左键点击 Halcon 窗口中的图元
- **THEN** 触发 EntitySelected 事件，参数为被选中的 CadEntity

#### Scenario: 实时坐标显示
- **WHEN** 鼠标在 Halcon 窗口上移动
- **THEN** 触发 CoordinateChanged 事件，参数为 CAD 坐标 (cadX, cadY)

### Requirement: 点胶机必备 ROI 交互方法
系统 SHALL 在 Halcon 项目中提供以下 ROI 交互方法，供点胶机场景使用：

#### Scenario: ROIRectangle2 旋转矩形 ROI
- **WHEN** 用户选择"旋转矩形"模式并在窗口中绘制
- **THEN** 可通过拖拽手柄调整中心(Row,Col)、角度(Phi)、长半轴(Length1)、短半轴(Length2)
- **AND** 绘制完成后返回 HRegion（GenRectangle2 生成）

#### Scenario: ROICircle 圆形 ROI
- **WHEN** 用户选择"圆形"模式并在窗口中绘制
- **THEN** 可通过拖拽手柄调整圆心(Row,Col)和半径(Radius)
- **AND** 绘制完成后返回 HRegion（GenCircle 生成）

#### Scenario: ROILine 线段 ROI
- **WHEN** 用户选择"线段"模式并在窗口中绘制
- **THEN** 可通过拖拽端点手柄调整起点(Row1,Col1)和终点(Row2,Col2)
- **AND** 绘制完成后返回线段 XLD 轮廓

#### Scenario: ROIPolyline 折线 ROI
- **WHEN** 用户选择"折线"模式并在窗口中绘制
- **THEN** 可通过依次点击添加顶点，拖拽已有顶点手柄调整位置
- **AND** 右键或双击结束绘制，返回折线 XLD 轮廓

#### Scenario: ROICircularArc 圆弧 ROI
- **WHEN** 用户选择"圆弧"模式并在窗口中绘制
- **THEN** 可通过拖拽手柄调整圆心(Row,Col)、半径(Radius)、起始角度和结束角度
- **AND** 绘制完成后返回圆弧 XLD 轮廓

#### Scenario: 涂抹模式 ROI
- **WHEN** 用户选择"涂抹"模式并按住左键拖动
- **THEN** 沿鼠标轨迹生成圆形笔刷区域并合并到掩膜区域
- **AND** 返回合并后的 HRegion

#### Scenario: 擦除模式 ROI
- **WHEN** 用户选择"擦除"模式并按住左键拖动
- **THEN** 沿鼠标轨迹从掩膜区域中差集移除圆形笔刷区域
- **AND** 返回差集后的 HRegion

### Requirement: CadEntity 转 HObject 扩展方法
系统 SHALL 为每种 CadEntity 子类提供 ToHObject() 方法，将 CAD 图元转换为 Halcon HObject。

#### Scenario: CadLine 转换
- **WHEN** 调用 CadLine.ToHObject()
- **THEN** 返回 GenContourPolygonXld 生成的线段 XLD

#### Scenario: CadArc 转换
- **WHEN** 调用 CadArc.ToHObject()
- **THEN** 返回 GenArcXLD 生成的圆弧轮廓

#### Scenario: CadCircle 转换
- **WHEN** 调用 CadCircle.ToHObject()
- **THEN** 返回 GenCircleContourXld 生成的圆轮廓

#### Scenario: CadLwPolyline 转换
- **WHEN** 调用 CadLwPolyline.ToHObject()
- **THEN** 返回 GenContourPolygonXld 生成的多段线 XLD

#### Scenario: CadEllipse 转换
- **WHEN** 调用 CadEllipse.ToHObject()
- **THEN** 返回 GenEllipseContourXld 生成的椭圆轮廓

## MODIFIED Requirements

### Requirement: CadPointEditorControl 使用 Halcon 窗口
CadPointEditorControl 的画布区域 SHALL 从 SkiaCanvasControl 替换为 WindowsFormsHost + VMHWindowControl，保持 6 步操作流程和绑定接口不变。

## REMOVED Requirements

### Requirement: SkiaCanvasControl 及 SkiaSharp 依赖
**Reason**: 完全由 Halcon 替代，SkiaSharp 不再需要
**Migration**: SkiaCanvasControl.xaml(.cs) 和 SkiaCanvasViewModel.cs 删除，Module.csproj 移除 SkiaSharp 包引用

# 双龙门标定控件 验证清单

## 机构配置
- [x] 机构拓扑示意图正确显示公共基准轴、龙门1、龙门2 的轴与相机关系
- [x] 工站选择下拉框可切换，切换后可用轴列表刷新
- [x] 龙门 1 轴名（Dx/Dy）下拉框可编辑，默认值正确
- [x] 龙门 2 X 轴名下拉框可编辑，Y 轴下拉框默认锁定为共用 Y 轴名并提示"跟随公共基准轴"
- [x] Cam1/Cam2 TCP 连接名下拉框独立配置，从 ITCPClientManagerService 获取可用连接

## 龙门 1 标定
- [x] 标定点 DataGrid 默认生成 9 个空点位，可增删
- [x] 单点示教按钮读取当前 Dx/Dy 轴坐标填入机械 X/Y，状态变为"已示教"（橙色）
- [x] 单点移动按钮移动 Dx/Dy 到该行机械坐标，移动前自动抬 Z（如配置）
- [x] 启用视觉数据时 TCP 自动接收 Cam1 数据填充视觉 X/Y
- [x] 未启用视觉数据时视觉 X/Y 列可手动输入
- [x] 自动标定流程按序执行（移动→拍照→等待→填充→延时→下一点），进度条与状态文本更新
- [x] 自动标定期间"开始"禁用、"停止"启用，点击停止后安全停止
- [x] 计算标定按钮在 ≥3 点时可用，结果正确显示 A/B/C/D/Tx/Ty/RMS/旋转角/缩放/评级

## 龙门 2 标定
- [x] 标定点 DataGrid 结构与龙门 1 一致
- [x] 单点示教/移动操作 X2 与共用 Y 轴
- [x] 龙门 2 标定时 UI 提示"Y 轴为公共基准轴，运动将同步影响龙门 1"
- [x] Cam2 TCP 数据独立接收，不与 Cam1 冲突
- [x] 计算标定结果独立存储，不覆盖龙门 1 结果

## 跨龙门 Y 基准对齐
- [x] 两套龙门均未完成仿射标定时，"跨龙门对齐"按钮禁用并提示
- [x] 两套龙门均完成标定后，"跨龙门对齐"按钮启用
- [x] 采集公共基准点功能可记录 (Y_common, Cam1_visionY, Cam2_visionY) 数据对
- [x] 跨龙门对齐计算输出 OffsetX/OffsetY/RotationDeg/Scale 与残差
- [x] 残差 > 0.05mm 时显示警告
- [x] 坐标变换验证：输入龙门 1 坐标 → 输出龙门 2 等效坐标，公式正确

## 文件操作
- [x] 保存功能将完整数据持久化到 Config/Calibration/DualGantryCalibration_<名称>.json
- [x] 另存为功能弹出文件对话框，输入新名称后保存
- [x] 导入功能弹出文件对话框，选择 JSON 文件后加载
- [x] 导出功能弹出文件对话框，选择路径后保存
- [x] 自动加载：启动时读取上次文件名并加载，文件名仅显示名称不含路径
- [x] 自动加载失败时状态栏提示"自动加载失败"，不阻塞界面

## 状态反馈与日志
- [x] 底部状态栏显示操作结果，颜色区分成功(绿)/警告(橙)/错误(红)/信息(蓝)
- [x] 状态文本支持多语言切换
- [x] 关键节点通过 ILoggerService 记录日志

## 运动控制安全性
- [x] 标定流程中触发急停，系统立即停止运动轴并取消流程
- [x] 水平移动前自动抬 Z 到安全高度（如配置 Z 轴）
- [x] 共用 Y 轴互锁：龙门 2 运动共用 Y 前检查龙门 1 状态，运动中则等待
- [x] 关键操作（计算标定、跨龙门对齐、应用变换）弹出确认对话框

## 多语言
- [x] 所有新增 UI 文本使用 `{lang:Lang Key}` 绑定
- [x] zh-CN 与 en-US 资源文件 Key 集合完全一致（0 重复、0 缺失）
- [x] 切换语言后所有文本自动更新

## UI 设计规范
- [x] 左右分栏布局，左栏 ScrollViewer，右栏上下分栏（龙门1/龙门2/跨龙门）
- [x] 使用 materialDesign:Card UniformCornerRadius="8" Padding="16"
- [x] 卡片标题使用 PackIcon + TextBlock（PrimaryHueMidBrush）
- [x] 龙门 1 主色 #1565C0，龙门 2 主色 #00897B，跨龙门主色 #6A1B9A
- [x] 状态颜色：已示教=橙、已标定=绿、错误=红
- [x] 全部使用 `<materialDesign:PackIcon>`，无 emoji
- [x] 引用 MaintenanceSharedStyles.xaml

## 集成与编译
- [x] MaintenanceView.xaml 新增第 5 个 TabItem，图标 VectorCombine
- [x] ContentControl DataTrigger Value=4 映射到 DualGantryCalibrationView
- [x] PrimModel.cs 注册 IDualGantryCalibrationService（Singleton）与 DualGantryCalibrationView（Navigation）
- [x] dotnet build GZQL_MACHINE.sln 无错误无警告
- [x] 启动应用进入维护页 → 双龙门标定 Tab，界面渲染正常无空白

## 版本记录
- [x] 版本修改记录.txt 顶部追加 v2026.06.23 双龙门标定控件记录

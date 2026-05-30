# ZScanDetailView UI细化与功能增强 Spec

## Why
当前 ZScanDetailView 仅有基础的数据展示功能，缺少实际生产环境所需的图片导入、运动控制、实时数据接收、灵活配置等关键功能。需要将其升级为完整的 3D 扫描操作与监控界面，满足工业设备的快速响应性和安全性要求。

## What Changes
- **左侧 CAD 可视化区域改造**：从静态占位符改为支持导入图片（PNG/JPG/BMP），并增加展开/缩回功能
- **右侧数据栏增强**：新增运动控制按钮组（3D扫描、停止、回待机位）、通讯协议下拉选择（TCPIP等）
- **实时数据接收**：集成 TCP 数据接收机制，参考 ScanDetailViewModel 的数据格式解析逻辑，支持配置化的数据序号映射
- **数据管理功能**：完善增删行、CSV 导入导出功能
- **测量点配置优化**：Feature 字段改为 Description（描述），支持自由输入；增加 Nominal、Range 配置；自动计算差值和状态判定
- **视图集成**：ZScanView 直接嵌入 ZScanDetailView，移除原有冗余内容

## Impact
- Affected specs: scan3d-camera-workflow, tcpip-vision-integration
- Affected code:
  - `Module/Controls/StepDetails/ZScanDetailView.xaml` - UI 布局重构
  - `Module/Controls/StepDetails/ZScanDetailViewModel.cs` - 业务逻辑增强
  - `Module/Controls/StepDetails/ZScanDetailView.xaml.cs` - 交互逻辑
  - `Module/Controls/Assembly/ZScanView.xaml` - 视图集成
  - `Module/Models/ZScanSummaryItem.cs` - 模型扩展
  - 新增：图片导入/管理相关服务类（如需要）

## ADDED Requirements

### Requirement: 左侧图片可视化面板
系统 SHALL 提供可导入图片的可视化面板，支持以下功能：
- 支持导入 PNG/JPG/BMP 格式图片
- 提供展开/缩回按钮（使用 `<materialDesign:PackIcon>` 图标）
- 展开状态显示完整图片预览，缩回状态仅显示缩略图或隐藏
- 图片支持缩放和平移操作（可选）

#### Scenario: 用户导入产品图片
- **WHEN** 用户点击"导入图片"按钮并选择有效图片文件
- **THEN** 左侧面板显示该图片，默认为展开状态
- **AND** 记录图片路径到 ViewModel

#### Scenario: 用户切换面板显示状态
- **WHEN** 用户点击展开/缩回切换按钮
- **THEN** 面板在展开（宽度 300px）和缩回（宽度 40px 或隐藏）之间切换
- **AND** 右侧数据栏自适应调整宽度

### Requirement: 运动控制按钮组
系统 SHALL 在右侧数据栏顶部提供运动控制按钮组，包含：
- **3D扫描**按钮：触发 3D 扫描流程（移动到拍照位置→触发相机→接收数据）
- **停止**按钮：紧急停止当前运动（符合工业安全要求，快速响应）
- **回待机位**按钮：控制轴返回安全待机位置

#### Scenario: 用户触发 3D 扫描
- **WHEN** 用户点击"3D扫描"按钮且设备就绪
- **THEN** 系统执行扫描序列：移动轴到起始位置→逐点扫描→触发拍照→等待数据返回
- **AND** 按钮显示加载状态，防止重复点击
- **AND** 完成后自动刷新数据表格

#### Scenario: 用户紧急停止
- **WHEN** 用户点击"停止"按钮
- **THEN** 立即中断当前运动（优先级最高，响应时间 <100ms）
- **AND** 显示停止状态提示

### Requirement: 通讯配置下拉菜单
系统 SHALL 提供通讯方式选择下拉菜单，支持：
- TCPIP（默认）
- Serial（预留扩展）
- 下拉列表动态加载已配置的连接名称（参考 ScanDetailViewModel 的 TcpConnections 加载逻辑）

#### Scenario: 选择 TCPIP 连接
- **WHEN** 用户在通讯下拉框中选择 TCPIP 并指定连接名称
- **THEN** 系统记录选择的通讯参数，用于后续数据收发

### Requirement: 3D 相机数据接收与解析
系统 SHALL 在 3D 扫描触发后自动接收相机返回数据并刷新表格，遵循以下规则：
- 数据格式参考：`Camera=3DCAMERA;VISION_RESULT:SUCCESS:14.164,10.713,9.399,...`
- 从 `VISION_RESULT:SUCCESS:` 后的数值为测量值序列
- **不允许硬编码序号**：每个测量点的数据接收序号可通过 UI 配置（新增 DataIndex 字段）
- 解析后按序号匹配到对应行的 ZMeasured 字段
- 自动重新计算 DeltaZ = ZMeasured - ZNominal
- 自动更新状态判定（Pass/Fail based on Range）

#### Scenario: 接收到 3D 相机数据
- **WHEN** 系统通过 TCP 接收到相机数据且格式正确
- **THEN** 按每行配置的 DataIndex 提取对应位置的数值
- **AND** 更新对应行的 ZMeasured、DeltaZ、Status 字段
- **AND** 刷新统计信息（TotalPoints、ZNominalRange、ZMaxDelta）
- **AND** 记录接收时间和原始数据摘要

### Requirement: 数据行管理功能
系统 SHALL 提供完整的数据行 CRUD 操作：
- **增加行**：在表格末尾追加空行，自动编号
- **删除行**：删除选中的行，更新统计信息
- **导入 CSV**：从 CSV 文件批量导入测量点配置（含坐标、标称值、范围等）
- **导出 CSV**：将当前表格数据导出为 CSV 文件

#### Scenario: 导入 CSV 文件
- **WHEN** 用户选择有效的 CSV 文件
- **THEN** 解析文件内容并替换当前表格数据
- **AND** 更新所有统计信息和状态判定
- **AND** 显示成功/失败提示

### Requirement: 测量点配置增强
系统 SHALL 将 Feature 字段改为 Description（描述），并增加以下可配置项：
- **Description**（文本输入）：测量点描述信息（原 FeatureName）
- **Nominal**（数值输入）：标称值（ZNominal）
- **Range**（数值输入）：允许偏差范围（UpperLimit/LowerLimit）
- **DataIndex**（整数输入）：3D 相机返回数据中的序号位置（0-based or 1-based）
- **自动计算字段**：
  - DeltaZ = ZMeasured - Nominal（实测值更新时自动计算）
  - Status：根据 |DeltaZ| <= Range 判定 Pass/Fail

#### Scenario: 配置测量点参数
- **WHEN** 用户编辑某行的 Description、Nominal、Range、DataIndex
- **THEN** 值即时保存到模型
- **AND** 如果 ZMeasured 已有值，立即重新计算 DeltaZ 和 Status

#### Scenario: 自动状态判定
- **WHEN** 某行的 ZMeasured 被更新（手动或自动接收）
- **THEN** 系统 DeltaZ = ZMeasured - Nominal
- **AND** 若 |DeltaZ| <= Range 则 Status = "Pass"，否则 Status = "Fail"
- **AND** 更新全局统计（ZMaxDelta 取所有行最大绝对差值）

### Requirement: ZScanView 集成 ZScanDetailView
系统 SHALL 将 ZScanView 改造为直接嵌入 ZScanDetailView 的容器：
- 移除 ZScanView 原有的 SCAN PARAMETERS、LASER SURFACE SCAN、Z-CORRECTION SUMMARY TABLE 等卡片内容
- 直接使用 `<ContentControl>` 或直接嵌入 `ZScanDetailView` 用户控件
- 保留必要的标题和导航上下文

#### Scenario: 打开 Z-SCAN 页面
- **WHEN** 用户导航到 Z-SCAN 视图
- **THEN** 显示完整的 ZScanDetailView 界面（含图片面板、数据栏、运动控制等）
- **AND** 不再显示原有的汇总表格和参数配置卡片

## MODIFIED Requirements

### Requirement: ZScanPointDetail 模型扩展
原有模型 SHALL 增加以下字段以支持新功能：
```csharp
public class ZScanPointDetail : BindableBase
{
    // ... 原有字段保持不变 ...
    private string _description;        // 替代 FeatureName，改为自由输入的描述
    private double _nominal;            // 标称值（替代 ZNominal 的语义）
    private double _range;              // 允许偏差范围
    private int _dataIndex;             // 3D相机数据序号位置
    private string _status;             // 状态判定：Pass/Fail/Pending
    
    public string Description { get; set; }
    public double Nominal { get; set; }
    public double Range { get; set; }
    public int DataIndex { get; set; }
    public string Status { get; set; }
}
```

**Reason**: 原有 FeatureName 为下拉选择模式，不够灵活；新增字段支持配置化数据接收和自动判定

### Requirement: ZScanDetailViewModel 功能扩展
ViewModel SHALL 增加以下职责：
- 图片管理（导入路径、展开/缩回状态）
- 运动控制命令（调用运动控制服务）
- TCP 通讯管理（连接选择、数据订阅）
- 数据解析（参考 ScanDetailViewModel 的解析逻辑）
- 自动计算引擎（DeltaZ、Status、统计信息）

## REMOVED Requirements

### Requirement: 原 ZScanView 卡片内容
**Reason**: 被 ZScanDetailView 完全替代，避免功能冗余
**Migration**: 删除 ZScanView 中的三个 Card 内容（SCAN PARAMETERS、LASER SURFACE SCAN、Z-CORRECTION SUMMARY TABLE），仅保留容器框架

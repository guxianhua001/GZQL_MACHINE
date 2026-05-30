# TCPIP通信与VISION模块集成 Spec

## Why
当前系统存在TCPLib底层通信库但缺乏通用配置管理UI，VISION步骤类型已在枚举中定义但无执行器和完整UI，视觉数据解析与全局变量映射尚未实现。需要建立从TCPIP通信配置→VISION步骤编辑→运行时执行→数据解析→全局变量映射的完整链路。

## What Changes
- **新建TCPIP独立项目**（Prism IModule），包含服务器端/客户端服务、通用配置管理UI、连接测试功能，清理Module目录下旧有TCPIP相关文件
- 重构VisionDetail模型，扩展通讯方式、触发命令、数据解析脚本、全局变量映射等字段
- 创建VisionDetailView/VisionDetailViewModel替代现有Camera2DView，作为VISION步骤的专用编辑UI
- 创建VisionStepAction实现IProcessStepAction，完成VISION步骤的运行时执行逻辑
- 实现基于Natasha的C#脚本数据解析引擎，支持自定义解析逻辑
- 实现VISION执行结果到全局变量的映射机制
- 在ProcessSequenceEditorViewModel中集成VISION步骤的详情入口
- 实现VISION步骤超时处理机制（重试/暂停/停止对话框）

## Impact
- Affected specs: process-sequence-editor（步骤编辑器新增VISION入口）
- Affected code:
  - **新建项目**: TCPIPModule（Prism模块，可独立注入）
  - **清理**: ModuleCore/Services/下旧SocketServerService.cs、SocketClientService.cs
  - StationTasks/Models/ProcessStep.cs — VisionDetail模型扩展
  - Module/Operators/Editor/ — 新增VisionDetailView/ViewModel
  - StationTasks/Actions/ — 新增VisionStepAction
  - StationTasks/StationTasksModule.cs — 注册VisionStepAction
  - Module/PrimModel.cs — 注册新视图
  - Module/Services/ProcessSequenceService.cs — CreateStepActions添加VisionStepAction
  - MainApp/App.xaml.cs — 注册TCPIPModule、调整RegisterTCPServices
  - Core/Models/VisionResult.cs — 已存在，可能需要扩展

## ADDED Requirements

### Requirement: TCPIP独立Prism模块项目
系统SHALL创建独立的TCPIPModule项目，作为Prism IModule可注入主应用，包含完整的服务器端和客户端功能模块。

#### Scenario: TCPIP模块注册
- **WHEN** 主应用启动时
- **THEN** TCPIPModule通过ConfigureModuleCatalog注册到Prism模块目录
- **AND** TCPIP模块的DI容器注册所有TCP服务（ITCPEventService、ITCPClientManagerService、工厂等）
- **AND** TCPIP模块的通用设置UI可通过导航访问

#### Scenario: 模块架构解耦
- **WHEN** TCPIP模块独立开发/测试
- **THEN** TCPIPModule不依赖Module项目，仅依赖Core（接口/模型）和TCPLib（底层通信）
- **AND** 其他模块通过Core.Abstraction中的接口（ITCPEventService等）使用TCP功能
- **AND** TCPIPModule可被移除而不影响其他模块编译

### Requirement: TCPIP通用配置管理UI
系统SHALL在TCPIPModule中提供TCPIP连接配置管理界面，支持服务器模式和客户端模式的参数配置。

#### Scenario: 添加TCPIP连接配置
- **WHEN** 用户点击"添加连接"按钮
- **THEN** 弹出配置对话框，可设置连接名称、模式（服务器/客户端）、IP地址、端口号、超时时间、编码方式
- **AND** 保存后配置持久化到配方池

#### Scenario: 删除TCPIP连接配置
- **WHEN** 用户选中一条配置并点击"删除"
- **THEN** 从配置列表中移除该条目并持久化

#### Scenario: TCPIP连接测试
- **WHEN** 用户点击"测试连接"按钮
- **THEN** 系统尝试建立连接，实时显示连接状态（成功/失败/超时），支持数据发送和接收测试

### Requirement: VISION步骤专用编辑UI
系统SHALL提供VISION步骤的专用详情编辑界面，支持通讯方式选择、触发命令设置、数据解析配置和全局变量映射。

#### Scenario: 打开VISION详情编辑
- **WHEN** 用户在步骤编辑器中双击VISION类型的步骤
- **THEN** 弹出VisionDetailView弹窗，显示通讯配置、触发命令、数据解析和变量映射四个区域

#### Scenario: 通讯方式选择
- **WHEN** 用户在VISION详情中选择通讯方式
- **THEN** 可选择TCPIP（从已配置的连接列表中选择）或其他通讯协议
- **AND** 选择TCPIP后，下拉显示所有已配置的TCPIP连接名称

#### Scenario: 触发拍照命令设置
- **WHEN** 用户在触发命令区域输入命令字符串
- **THEN** 命令字符串保存到VisionDetail.TriggerCommand
- **AND** 支持设置响应超时时间

#### Scenario: 数据解析脚本配置
- **WHEN** 用户在数据解析区域编辑C#脚本
- **THEN** 脚本代码保存到VisionDetail.ParseScript
- **AND** 提供默认解析模板（解析长字符串格式的返回数据）
- **AND** 支持脚本编译验证

#### Scenario: 全局变量映射
- **WHEN** 用户在变量映射区域配置映射关系
- **THEN** 可将解析结果（offsetX、offsetY等）映射到指定的全局变量名
- **AND** 映射关系保存到VisionDetail.VariableMappings

### Requirement: VISION步骤运行时执行
系统SHALL实现VisionStepAction，在流程执行时完成触发拍照→接收数据→解析数据→映射全局变量的完整链路。

#### Scenario: VISION步骤正常执行
- **WHEN** ProcessStepExecutor执行到VISION步骤
- **THEN** VisionStepAction通过选定的通讯方式发送触发命令
- **AND** 等待接收返回数据（受超时时间限制）
- **AND** 使用配置的解析脚本解析数据
- **AND** 将解析结果写入映射的全局变量

#### Scenario: VISION步骤超时处理
- **WHEN** VISION步骤在超时时间内未收到响应
- **THEN** 弹出超时处理对话框，提供"重试"、"暂停"、"停止"三个选项
- **AND** 选择重试则重新发送触发命令
- **AND** 选择暂停则暂停当前任务
- **AND** 选择停止则取消当前任务

### Requirement: C#脚本数据解析引擎
系统SHALL提供基于Natasha的C#脚本解析引擎，支持用户自定义数据解析逻辑。

#### Scenario: 默认解析脚本
- **WHEN** 用户未自定义解析脚本
- **THEN** 使用内置默认解析器，支持常见格式（逗号分隔、等号分隔的键值对）

#### Scenario: 自定义解析脚本
- **WHEN** 用户编写自定义C#脚本
- **THEN** 脚本接收string类型输入（原始返回数据），返回Dictionary<string, double>类型（解析出的键值对）
- **AND** 脚本编译错误时给出明确提示

## MODIFIED Requirements

### Requirement: VisionDetail模型扩展
现有VisionDetail模型仅包含SelectedCamera/SelectedSlot/DataRows三个字段，需扩展为支持完整VISION功能的数据模型。

新增字段：
- CommunicationType: 通讯方式（TCPIP/Serial等）
- ConnectionName: 选定的TCPIP连接名称
- TriggerCommand: 触发拍照命令字符串
- ResponseTimeout: 响应超时时间（毫秒）
- ParseScript: C#数据解析脚本代码
- VariableMappings: 全局变量映射集合（解析键名→全局变量名）

### Requirement: ProcessSequenceEditorViewModel集成VISION入口
现有NavigateToDetailView方法仅处理GOTO步骤，需添加VISION步骤的分支处理。

## REMOVED Requirements

### Requirement: 旧Camera2DView作为VISION编辑入口
**Reason**: Camera2DView功能过于简单（仅相机/Slot选择+静态数据行），无法满足通讯配置、数据解析、变量映射等需求，由新的VisionDetailView替代。
**Migration**: Camera2DView保留用于2D相机预览场景，VISION步骤编辑改用VisionDetailView。

### Requirement: ModuleCore中旧Socket服务
**Reason**: ModuleCore/Services/下的SocketServerService.cs和SocketClientService.cs与TCPLib功能重复，TCPIPModule项目将统一提供TCP服务。
**Migration**: 清理ModuleCore中的旧Socket服务文件，TCP功能由TCPIPModule通过TCPLib统一提供。

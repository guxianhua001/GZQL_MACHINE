# Tasks

- [x] Task 1: 创建TCPIP独立Prism模块项目
  - [x] 1.1 创建TCPIPModule项目（net9.0-windows7.0），添加Prism.Wpf、TCPLib、Core项目引用
  - [x] 1.2 创建TCPIPModule.cs实现IModule，在RegisterTypes中注册ITCPEventService、ITCPClientManagerService、ITCPClientFactory、ITCPServerFactory等TCP服务
  - [x] 1.3 在MainApp/App.xaml.cs的ConfigureModuleCatalog中注册TCPIPModule
  - [x] 1.4 将MainApp/App.xaml.cs中RegisterTCPServices的注册逻辑迁移到TCPIPModule.RegisterTypes
  - [x] 1.5 将MainApp/App.xaml.cs中InitializeTCPSystem的初始化逻辑迁移到TCPIPModule.OnInitialized
  - [x] 1.6 清理ModuleCore/Services/下旧SocketServerService.cs和SocketClientService.cs
  - [x] 1.7 在MainApp/MainApp.csproj中添加TCPIPModule项目引用
  - [x] 1.8 在解决方案文件中添加TCPIPModule项目

- [x] Task 2: 扩展VisionDetail模型和TCPIP配置模型
  - [x] 2.1 在ProcessStep.cs中扩展VisionDetail类，新增CommunicationType、ConnectionName、TriggerCommand、ResponseTimeout、ParseScript字段
  - [x] 2.2 新增VariableMapping类（SourceKey→GlobalVariableName映射），VisionDetail中添加VariableMappings集合
  - [x] 2.3 在Core/Models/中新增TcpConfigItem模型类（Name、Mode、IP、Port、Timeout、Encoding等），用于TCPIP配置持久化

- [x] Task 3: 创建TCPIP通用配置管理UI
  - [x] 3.1 创建TcpConfigViewModel，实现连接配置的增删改查逻辑，配置持久化到配方池ExtensionData
  - [x] 3.2 创建TcpConfigView.xaml，包含配置列表DataGrid、添加/删除/编辑按钮、参数编辑区域
  - [x] 3.3 实现连接测试功能：连接状态测试、数据发送/接收测试、实时结果显示
  - [x] 3.4 在TCPIPModule.RegisterTypes中注册TcpConfigView/ViewModel
  - [x] 3.5 在导航栏添加TCPIP设置入口

- [x] Task 4: 创建VISION步骤专用编辑UI
  - [x] 4.1 创建VisionDetailViewModel，包含通讯方式选择、TCPIP连接下拉、触发命令、超时设置、解析脚本编辑、变量映射配置
  - [x] 4.2 创建VisionDetailView.xaml，布局为四个区域：通讯配置区、触发命令区、数据解析区、变量映射区
  - [x] 4.3 实现TCPIP连接列表从ITCPClientManagerService加载
  - [x] 4.4 实现全局变量列表从IRecipePoolService.LoadGlobalVariablesAsync加载，供变量映射下拉选择
  - [x] 4.5 提供默认数据解析脚本模板
  - [x] 4.6 在PrimModel.cs中注册VisionDetailView/ViewModel

- [x] Task 5: 实现C#脚本数据解析引擎
  - [x] 5.1 创建IVisionDataParser接口（string→Dictionary<string,double>）
  - [x] 5.2 实现DefaultVisionDataParser，支持逗号分隔和键值对格式的默认解析
  - [x] 5.3 实现ScriptVisionDataParser，基于Natasha编译执行用户自定义C#脚本
  - [x] 5.4 脚本编译错误捕获和友好提示

- [x] Task 6: 创建VisionStepAction运行时执行器
  - [x] 6.1 创建VisionStepAction实现IProcessStepAction，SupportedStepType = StepType.VISION
  - [x] 6.2 实现执行流程：通过ITCPEventService发送触发命令→等待接收数据→解析数据→映射全局变量
  - [x] 6.3 实现超时处理：超时时抛出RecoverableException，弹出重试/暂停/停止对话框
  - [x] 6.4 实现全局变量写入：通过IRecipePoolService.SaveGlobalVariablesAsync持久化
  - [x] 6.5 在StationTasksModule.cs中注册VisionStepAction
  - [x] 6.6 在ProcessSequenceService.CreateStepActions中改为DI解析所有IProcessStepAction

- [x] Task 7: 集成VISION步骤到步骤编辑器
  - [x] 7.1 在ProcessSequenceEditorViewModel.NavigateToDetailView中添加VISION分支，弹出VisionDetailView
  - [x] 7.2 在ProcessStepExecutor.ExecuteSingleStepAsync中添加VISION case分支
  - [x] 7.3 在AddEditStepDialogView中确保VISION类型可选（StepType枚举已包含VISION）

- [x] Task 8: 编译验证与集成测试
  - [x] 8.1 全量编译确保无错误
  - [x] 8.2 验证TCPIPModule独立加载和TCP服务注册
  - [x] 8.3 验证TCPIP配置UI的增删改查功能
  - [x] 8.4 验证VISION步骤编辑→保存→加载的数据完整性
  - [x] 8.5 验证VisionStepAction的执行链路

# Task Dependencies
- [Task 2] depends on [Task 1] (TCPIPModule项目需先创建，TcpConfigItem模型放在Core中)
- [Task 3] depends on [Task 1] (TCPIPModule项目需先创建) and [Task 2] (TcpConfigItem模型需先定义)
- [Task 4] depends on [Task 2] (VisionDetail模型需先扩展) and [Task 3] (需要TCPIP连接列表)
- [Task 5] depends on [Task 2] (ParseScript字段需先定义)
- [Task 6] depends on [Task 2] and [Task 5] (解析引擎需先实现)
- [Task 7] depends on [Task 4] and [Task 6] (UI和执行器需先就绪)
- [Task 8] depends on [Task 7] (所有功能需先集成)

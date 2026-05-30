# Tasks

- [x] Task 1: 精简 RecipeInfo 模型，移除无关字段
  - [x] SubTask 1.1: 从 RecipeInfo 中移除 Ingredients、RecipeStep、Rating、Difficulty、EstimatedTime 字段及 Ingredient/RecipeStep 辅助类
  - [x] SubTask 1.2: 更新 RecipeInfo 拷贝构造函数，移除已删除字段的拷贝逻辑
  - [x] SubTask 1.3: 验证 RecipeManagerViewModel 中引用已删除字段的代码并适配

- [x] Task 2: 创建 IRecipeDataAccessor<TParams> 极简配方访问接口
  - [x] SubTask 2.1: 在 RecipeManagement/Interfaces/ 中创建 IRecipeDataAccessor<TParams> 接口，定义 Params、CurrentRecipeName、CurrentPoolName、SaveAsync()、SwitchRecipeAsync()、EditParametersAsync() 成员
  - [x] SubTask 2.2: 在 RecipeServiceExtensions 中添加针对 IRecipeDataAccessor 的扩展方法

- [x] Task 3: 实现暂存区 + 原子提交保存架构
  - [x] SubTask 3.1: 创建 ParameterStagingArea 类，维护 Dictionary<string, object> 暂存字典和 HashSet<string> 脏标记集合
  - [x] SubTask 3.2: 创建 RecipePoolSaveContext 类，封装批量保存的完整生命周期（AddStation、CommitAsync）
  - [x] SubTask 3.3: 修改 RecipePoolService，添加 StageStationParameters 和 CommitStagedParametersAsync 方法
  - [x] SubTask 3.4: 修改 RecipeService，将 OnParametersSaved 改为写入暂存区 + 本地文件，不再直接写配方池 JSON
  - [x] SubTask 3.5: 修改 SaveAllStationParametersAsync，内部改为收集所有工站参数 → 暂存 → 提交模式

- [x] Task 4: 创建 RecipeStationBase<TParams> 零代码配方基类（基于 StationTaskBase）
  - [x] SubTask 4.1: 在 StationTasks 项目中创建 RecipeStationBase<TParams> 抽象基类，继承 StationTaskBase，实现 IStationParameterProvider 和 IRecipeDataAccessor<TParams>
  - [x] SubTask 4.2: 在基类中封装 IRecipeService 的创建、事件订阅、参数加载等样板逻辑
  - [x] SubTask 4.3: 暴露 Params 属性（返回强类型 TParams），EditParametersCommand、SwitchRecipeCommand
  - [x] SubTask 4.4: 实现 IStationParameterProvider 接口的简化版本（StationIdentifier、CurrentPoolName、CurrentRecipeName、CurrentParameters、HasUnsavedChanges）

- [x] Task 5: 简化 IStationParameterProvider 接口
  - [x] SubTask 5.1: 从 IStationParameterProvider 中移除 LoadRecipeAsync、SwitchToRecipeAsync、EditParameters 方法
  - [x] SubTask 5.2: 添加 HasUnsavedChanges 属性
  - [x] SubTask 5.3: 更新 RecipePoolService 中引用 IStationParameterProvider 的代码

- [x] Task 6: 重构 RecipePoolService 批量切换机制
  - [x] SubTask 6.1: 创建 BatchSwitchContext 类，封装批量切换的上下文信息（目标配方名、池ID、池名称）
  - [x] SubTask 6.2: 移除 static AsyncLocal 字段（IsBatchSwitching、BatchSelectedRecipe、BatchSelectedPoolId）
  - [x] SubTask 6.3: 修改 SwitchAllStationsAsync 方法，使用 BatchSwitchContext 参数
  - [x] SubTask 6.4: 修改 RecipeService.SwitchRecipeAsync，移除对 static AsyncLocal 的依赖

- [x] Task 7: RecipeManager 异步化
  - [x] SubTask 7.1: 将 IRecipeManager 接口中的同步方法改为异步方法（GetAllRecipePoolsAsync、SaveRecipePoolAsync 等）
  - [x] SubTask 7.2: 修改 RecipeManager 实现，移除所有 .Result 和 .Wait() 调用
  - [x] SubTask 7.3: 更新所有引用 IRecipeManager 的代码以适配异步接口

- [x] Task 8: 移除死代码
  - [x] SubTask 8.1: 删除 RecipeController.cs
  - [x] SubTask 8.2: 删除 RecipeBackgroundService.cs
  - [x] SubTask 8.3: 删除 SaveParametersCoordinator.cs

- [x] Task 9: 重构 StationTasks 工站类使用 RecipeStationBase
  - [x] SubTask 9.1: 修改 LoadingTask 继承 RecipeStationBase<LoadingStationParams>，零代码使用配方
  - [x] SubTask 9.2: 修改 DispensingTask 继承 RecipeStationBase<DispenserStationParams>，零代码使用配方
  - [x] SubTask 9.3: 修改 AssemblyTask 继承 RecipeStationBase<AssemblyStationParams>，零代码使用配方
  - [x] SubTask 9.4: 更新 StationTasksModule，注册 IStationParameterProvider 和 IBatchSwitchable 服务

- [x] Task 10: 编译验证与修复
  - [x] SubTask 10.1: 编译 RecipeManagement 项目 - 0 错误
  - [x] SubTask 10.2: 编译 StationTasks 项目 - 0 错误
  - [x] SubTask 10.3: 编译 MainApp 主项目 - 0 错误
  - [x] SubTask 10.4: 修复 RecipeManagerViewModel 兼容性问题

# Task Dependencies
- [Task 1] 无依赖，可先行
- [Task 2] 无依赖，可先行，与 Task 1 并行
- [Task 3] 依赖 Task 2（IRecipeDataAccessor 接口定义）
- [Task 4] 依赖 Task 2（IRecipeDataAccessor）和 Task 5（IStationParameterProvider 简化）
- [Task 5] 无依赖，可先行，与 Task 1 并行
- [Task 6] 依赖 Task 3（暂存区架构）
- [Task 7] 无依赖，可先行，与 Task 1 并行
- [Task 8] 无依赖，可先行
- [Task 9] 依赖 Task 4（RecipeStationBase 基类）和 Task 6（批量切换重构）
- [Task 10] 依赖所有其他 Task

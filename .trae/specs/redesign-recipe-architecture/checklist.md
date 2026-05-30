* [x] RecipeInfo 模型已移除 Ingredients、RecipeStep、Rating、Difficulty、EstimatedTime 字段

* [x] IRecipeDataAccessor<TParams> 接口已创建，提供 Params、CurrentRecipeName、CurrentPoolName、SaveAsync()、SwitchRecipeAsync()、EditParametersAsync() 成员

* [x] ParameterStagingArea 类已创建，支持暂存工站参数和脏标记管理

* [x] RecipePoolSaveContext 类已创建，支持 AddStation 和 CommitAsync 批量保存

* [x] RecipePoolService 已添加 StageStationParameters 和 CommitStagedParametersAsync 方法

* [x] RecipeService.OnParametersSaved 已改为写入暂存区 + 本地文件，不再直接写配方池 JSON

* [x] SaveAllStationParametersAsync 已改为收集 → 暂存 → 提交模式

* [x] RecipeStationBase<TParams> 基类已创建（基于 StationTaskBase），工站继承后零代码使用配方

* [x] RecipeStationBase 暴露 Params 属性返回强类型 TParams

* [x] RecipeStationBase 实现 IStationParameterProvider 简化接口

* [x] IStationParameterProvider 接口已简化，移除 LoadRecipeAsync、SwitchToRecipeAsync、EditParameters

* [x] IStationParameterProvider 已添加 HasUnsavedChanges 属性

* [x] BatchSwitchContext 类已创建，封装批量切换上下文

* [x] RecipePoolService 已移除 static AsyncLocal 字段

* [x] SwitchAllStationsAsync 已改用 BatchSwitchContext 参数

* [x] RecipeService.SwitchRecipeAsync 已移除对 static AsyncLocal 的依赖

* [x] IRecipeManager 接口方法已全部异步化

* [x] RecipeManager 实现已移除所有 .Result 和 .Wait() 调用

* [x] RecipeController.cs 已删除

* [x] RecipeBackgroundService.cs 已删除

* [x] SaveParametersCoordinator.cs 已删除

* [x] LoadingTask 已改为继承 RecipeStationBase，零代码使用配方

* [x] DispensingTask 已改为继承 RecipeStationBase，零代码使用配方

* [x] AssemblyTask 已改为继承 RecipeStationBase，零代码使用配方

* [x] StationTasksModule 已更新注册 IStationParameterProvider 和 IBatchSwitchable

* [x] 项目编译无错误（RecipeManagement + StationTasks + MainApp 全部通过）


using Core.Utilities;
using Core.Abstraction;
using MaterialDesignThemes.Wpf;
using Prism.Events;
using Recipe.Events;
using Recipe.Interfaces;
using Recipe.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using Prism.Services.Dialogs;
using Core.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;

namespace Recipe
{
    public class RecipePoolService : IRecipePoolService, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        private readonly IRecipeStorage _recipeStorage;
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        protected readonly IAppSettingService _appConfig;
        private readonly IRecipeDialogService _recipeDialogService;
        private readonly IStationRegistry _stationRegistry;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly ParameterStagingArea _stagingArea = new ParameterStagingArea();
        private BatchSwitchContext _currentBatchContext;
        private string _currentRecipeName = "Default";
        string IRecipePoolService.CurrentRecipeName
        {
            get => _currentRecipeName;
            set
            {
                if (_currentRecipeName != value)
                {
                    _currentRecipeName = value;
                    OnPropertyChanged(nameof(IRecipePoolService.CurrentRecipeName));
                }
            }
        }
        public string CurrentRecipeName => ((IRecipePoolService)this).CurrentRecipeName;
        private string _currentPoolName = "Default";
        string IRecipePoolService.CurrentPoolName
        {
            get => _currentPoolName;
            set
            {
                if (_currentPoolName != value)
                {
                    _currentPoolName = value;
                    OnPropertyChanged(nameof(IRecipePoolService.CurrentPoolName));
                }
            }
        }
        public string CurrentPoolName => _currentPoolName;
        private string _currentPoolId = "Default";
        string IRecipePoolService.CurrentPoolId
        {
            get => _currentPoolId;
            set
            {
                if (_currentPoolId != value)
                {
                    _currentPoolId = value;
                    OnPropertyChanged(nameof(IRecipePoolService.CurrentPoolId));
                }
            }
        }
        public string CurrentPoolId => _currentPoolId;
        public RecipePoolService(
            IRecipeStorage recipeStorage,
            IDialogService dialogService,
            ILoggerService logger,
            ILocalizationService localization,
            IEventAggregator eventAggregator,
            IStationRegistry stationRegistry,
            IAppSettingService appConfig,
            IRecipeDialogService recipeDialogService)
        {
            _recipeStorage = recipeStorage;
            _logger = logger;
            _localization = localization;
            _eventAggregator = eventAggregator;
            _stationRegistry = stationRegistry;
            _appConfig = appConfig;
            _recipeDialogService = recipeDialogService;
            _dialogService = dialogService;
        }
        public async Task<List<RecipePool>> GetAllRecipePoolsAsync()
        {
            var poolIds = await _recipeStorage.GetAvailableRecipePoolsAsync().ConfigureAwait(false);
            var pools = new List<RecipePool>();
            foreach (var poolId in poolIds)
            {
                var pool = await _recipeStorage.LoadRecipePoolAsync(poolId).ConfigureAwait(false);
                if (pool != null) pools.Add(pool);
            }
            return pools;
        }
        public async Task CreateRecipePoolAsync(string poolId, string name)
        {
            var pool = new RecipePool { Id = poolId, Name = name, CreatedTime = DateTime.Now };
            await _recipeStorage.SaveRecipePoolAsync(pool).ConfigureAwait(false);
        }
        public async Task<bool> DeleteRecipePoolAsync(string poolName)
        {
            if (poolName == CurrentPoolName)
            {
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("RPS_Log_DeleteCurrentPoolRejected", "尝试删除当前正在使用的配方池 '{0}'，操作被拒绝。"), poolName));
                return false;
            }
            var poolToDelete = await _recipeStorage.LoadRecipePoolAsync(poolName).ConfigureAwait(false);
            bool wasDefault = poolToDelete?.IsDefault ?? false;
            await _recipeStorage.DeleteRecipePoolAsync(poolName).ConfigureAwait(false);
            if (wasDefault)
            {
                var remainingPools = await GetAllRecipePoolsAsync().ConfigureAwait(false);
                if (remainingPools.Any())
                {
                    var newDefault = remainingPools.First();
                    newDefault.IsDefault = true;
                    await _recipeStorage.SaveRecipePoolAsync(newDefault).ConfigureAwait(false);
                    _logger.Info(string.Format(_localization.GetResourceOrDefault("RPS_Log_DefaultPoolDeletedNewDefault", "默认池已删除，已将 '{0}' 设为新默认池"), newDefault.Name));
                }
            }
            return true;
        }
        public void StageStationParameters(string stationIdentifier, object parameters, bool replacePositions = false)
        {
            _stagingArea.Stage(stationIdentifier, parameters, replacePositions);
            _logger.Info(string.Format(_localization.GetResourceOrDefault("RPS_Log_ParametersStaged", "[{0}] 参数已暂存{1}"), stationIdentifier, replacePositions ? "（完整替换 Positions）" : ""));
        }
        public bool HasStagedChanges(string stationIdentifier = null)
        {
            if (stationIdentifier == null)
                return _stagingArea.HasAnyDirty();
            return _stagingArea.IsDirty(stationIdentifier);
        }
        public async Task<bool> CommitStagedParametersAsync(string poolId, string recipeName)
        {
            if (!_stagingArea.HasAnyDirty())
            {
                _logger.Info(_localization.GetResourceOrDefault("RPS_Log_NoStagedChanges", "暂存区没有待提交的参数变更"));
                return true;
            }
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var dirtyParams = _stagingArea.GetDirtyParameters();
                var context = new RecipePoolSaveContext(_recipeStorage, poolId, recipeName);
                foreach (var kv in dirtyParams)
                {
                    context.AddStation(kv.Key, kv.Value, _stagingArea.ShouldReplacePositions(kv.Key));
                }
                var success = await context.CommitAsync().ConfigureAwait(false);
                if (success)
                {
                    _stagingArea.ClearDirty();
                    _eventAggregator.GetEvent<SaveParametersCompletedEvent>().Publish(recipeName);
                    _logger.Info(string.Format(_localization.GetResourceOrDefault("RPS_Log_StagedParamsCommitted", "暂存区参数已提交到配方池: 池 '{0}' -> 配方 '{1}', 共 {2} 个工站"), poolId, recipeName, dirtyParams.Count));
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localization.GetResourceOrDefault("RPS_Log_CommitStagedParamsFailed", "提交暂存区参数失败: {0}"), ex.Message));
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        public async Task<bool> SaveStationParametersAsync(string poolId, string recipeName, string stationIdentifier, object parameters, bool replacePositions = false)
        {
            StageStationParameters(stationIdentifier, parameters, replacePositions);
            return await CommitStagedParametersAsync(poolId, recipeName).ConfigureAwait(false);
        }
        public async Task<bool> SaveAllStationParametersAsync(string poolId, string recipeName)
        {
            var stations = _stationRegistry.GetAllStations();
            if (!stations.Any())
            {
                _logger.Warn(_localization.GetResourceOrDefault("RPS_Log_NoStationsCannotSave", "没有找到任何工站，无法保存参数"));
                return false;
            }
            _logger.Info(string.Format(_localization.GetResourceOrDefault("RPS_Log_StartSaveAllStations", "开始保存所有工站参数到配方: {0}"), recipeName));
            foreach (var station in stations)
            {
                _stagingArea.Stage(station.StationIdentifier, station.CurrentParameters);
            }
            return await CommitStagedParametersAsync(poolId, recipeName).ConfigureAwait(false);
        }
        public async Task SwitchAllStationsAsync(string recipePoolName, string recipePoolId,
            string newRecipeName, bool showAlert = true)
        {
            var stations = _stationRegistry.GetAllStations();
            if (!stations.Any())
            {
                _logger.Warn(_localization.GetResourceOrDefault("RPS_Log_NoStationsCannotBatchSwitch", "没有找到任何工站，无法执行批量切换"));
                return;
            }
            if (showAlert)
            {
                var confirmed = await ShowConfirmationDialogAsync(
                    "确认批量切换",
                    $"确定要统一切换所有工站到配方 '{newRecipeName}' 吗？");
                if (!confirmed)
                {
                    _logger.Info(string.Format(_localization.GetResourceOrDefault("RPS_Log_UserCancelledBatchSwitch", "用户取消批量切换配方: {0}"), newRecipeName));
                    return;
                }
            }
            var batchContext = new BatchSwitchContext(newRecipeName, recipePoolId, recipePoolName);
            _currentBatchContext = batchContext;
            try
            {
                foreach (var station in stations)
                {
                    if (station is IBatchSwitchable switchable)
                    {
                        await switchable.SwitchToRecipeAsync(newRecipeName, batchContext).ConfigureAwait(false);
                    }
                }
                _appConfig?.TryUpdateRecipeName(newRecipeName);
                _appConfig?.Save();
                _logger.Info(string.Format(_localization.GetResourceOrDefault("RPS_Log_AllStationsSwitched", "所有工站已切换到配方: {0} (池: {1})"), newRecipeName, recipePoolName));
            }
            finally
            {
                _currentBatchContext = null;
            }
        }
        /// <summary>
        /// 使用MaterialDesign DialogHost全局弹窗显示确认对话框
        /// </summary>
        private async Task<bool> ShowConfirmationDialogAsync(string title, string message)
        {
            var tcs = new TaskCompletionSource<bool>();

            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialogView = new Views.RecipeConfirmDialogView();
                if (dialogView.DataContext is ViewModels.RecipeConfirmDialogViewModel vm)
                {
                    vm.Initialize(title, message);
                }

                var session = DialogHost.Show(dialogView, "MainDialogHost", new DialogClosingEventHandler((sender, args) =>
                {
                    tcs.SetResult(args.Parameter is bool confirmed && confirmed);
                }));
            });

            return await tcs.Task.ConfigureAwait(false);
        }
        public async Task SwitchToPoolAsync(string poolName, bool saveCurrentPool = false)
        {
            if (string.IsNullOrEmpty(poolName))
                throw new ArgumentNullException(nameof(poolName));
            if (poolName == CurrentPoolName)
            {
                _logger.Info(string.Format(_localization.GetResourceOrDefault("RPS_Log_AlreadyInPool", "已在配方池 '{0}' 中，无需切换"), poolName));
                return;
            }
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (saveCurrentPool && !string.IsNullOrEmpty(CurrentPoolName))
                {
                    var currentPool = await _recipeStorage.LoadRecipePoolAsync(CurrentPoolName).ConfigureAwait(false);
                    if (currentPool != null)
                    {
                        await _recipeStorage.SaveRecipePoolAsync(currentPool).ConfigureAwait(false);
                        _logger.Info(string.Format(_localization.GetResourceOrDefault("RPS_Log_SavedOriginalPoolState", "已保存原配方池 '{0}' 的状态"), CurrentPoolName));
                    }
                }
                var newPool = await _recipeStorage.LoadRecipePoolAsync(poolName).ConfigureAwait(false);
                if (newPool == null)
                {
                    _logger.Warn(string.Format(_localization.GetResourceOrDefault("RPS_Log_TargetPoolNotExistCreated", "目标配方池 '{0}' 不存在，创建默认池"), poolName));
                    newPool = new RecipePool { Id = poolName, Name = poolName };
                    await _recipeStorage.SaveRecipePoolAsync(newPool).ConfigureAwait(false);
                }
                await SetDefaultRecipePoolAsync(poolName).ConfigureAwait(false);
                ((IRecipePoolService)this).CurrentPoolName = poolName;
                ((IRecipePoolService)this).CurrentPoolId = newPool.Id ?? poolName;
                string targetRecipeName = newPool.CurrentRecipeName;
                if (string.IsNullOrEmpty(targetRecipeName) && newPool.Recipes?.Any() == true)
                {
                    targetRecipeName = newPool.Recipes.First().Name;
                    newPool.CurrentRecipeName = targetRecipeName;
                }
                if (!string.IsNullOrEmpty(targetRecipeName))
                {
                    await SwitchAllStationsInternalAsync(targetRecipeName, poolName).ConfigureAwait(false);
                }
                ((IRecipePoolService)this).CurrentRecipeName = targetRecipeName ?? "Default";
                _eventAggregator.GetEvent<RecipePoolChangedEvent>().Publish(poolName);
                _logger.Info(string.Format(_localization.GetResourceOrDefault("RPS_Log_SwitchedToPool", "已切换到配方池 '{0}'，当前配方 '{1}'"), poolName, targetRecipeName));
            }
            finally
            {
                _semaphore.Release();
            }
        }
        private async Task SwitchAllStationsInternalAsync(string recipeName, string poolId)
        {
            var stations = _stationRegistry.GetAllStations();
            if (!stations.Any())
            {
                _logger.Warn(_localization.GetResourceOrDefault("RPS_Log_NoStationsCannotSwitch", "没有找到任何工站，无法切换配方"));
                return;
            }
            var batchContext = new BatchSwitchContext(recipeName, poolId, poolId);
            _currentBatchContext = batchContext;
            try
            {
                foreach (var station in stations)
                {
                    if (station is IBatchSwitchable switchable)
                    {
                        await switchable.SwitchToRecipeAsync(recipeName, batchContext).ConfigureAwait(false);
                    }
                }
                _appConfig?.TryUpdateRecipeName(recipeName);
                _appConfig?.Save();
                _logger.Info(string.Format(_localization.GetResourceOrDefault("RPS_Log_AllStationsSwitchedInternal", "所有工站已切换到配方池 '{0}' 的配方 '{1}'"), poolId, recipeName));
            }
            finally
            {
                _currentBatchContext = null;
            }
        }
        private async Task SetDefaultRecipePoolAsync(string defaultPoolId)
        {
            var allPools = await GetAllRecipePoolsAsync().ConfigureAwait(false);
            foreach (var pool in allPools)
            {
                bool shouldBeDefault = (pool.Name == defaultPoolId);
                pool.IsDefault = shouldBeDefault;
                await _recipeStorage.SaveRecipePoolAsync(pool).ConfigureAwait(false);
            }
            _logger.Info(string.Format(_localization.GetResourceOrDefault("RPS_Log_DefaultPoolSet", "已将配方池 '{0}' 设置为默认池"), defaultPoolId));
        }
        public async Task<RecipePool> CopyRecipePoolAsync(string sourcePoolId, string newPoolId, string newName, string Description = "")
        {
            var sourcePool = await _recipeStorage.LoadRecipePoolAsync(sourcePoolId).ConfigureAwait(false);
            if (sourcePool == null)
                throw new InvalidOperationException($"源配方池 {sourcePoolId} 不存在");
            var newPool = DeepClone(sourcePool);
            newPool.Id = Guid.NewGuid().ToString();
            newPool.Name = newName;
            newPool.Description = Description;
            newPool.CreatedTime = DateTime.Now;
            newPool.ModifiedTime = DateTime.Now;
            newPool.IsDefault = false;
            foreach (var recipe in newPool.Recipes)
            {
                recipe.Id = Guid.NewGuid().ToString();
                recipe.CreatedTime = DateTime.Now;
                recipe.ModifiedTime = DateTime.Now;
            }
            await _recipeStorage.SaveRecipePoolAsync(newPool).ConfigureAwait(false);
            _logger.Info(string.Format(_localization.GetResourceOrDefault("RPS_Log_PoolCopied", "已复制配方池 {0} -> {1}，包含 {2} 个配方"), sourcePoolId, newPoolId, newPool.Recipes.Count));
            return newPool;
        }
        private T DeepClone<T>(T source)
        {
            if (source == null) return default;
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };
            var json = JsonSerializer.Serialize(source, options);
            return JsonSerializer.Deserialize<T>(json, options);
        }
        public async Task<bool> ImportRecipePoolAsync(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                var pool = JsonSerializer.Deserialize<RecipePool>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (pool == null) return false;
                await _semaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    await _recipeStorage.SaveRecipePoolAsync(pool).ConfigureAwait(false);
                    return true;
                }
                finally
                {
                    _semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localization.GetResourceOrDefault("RPS_Log_ImportPoolFailed", "导入配方池失败: {0}"), ex.Message));
                return false;
            }
        }
        public async Task<bool> ExportRecipePoolAsync(string poolId, string filePath)
        {
            try
            {
                var pool = await _recipeStorage.LoadRecipePoolAsync(poolId).ConfigureAwait(false);
                if (pool == null) return false;
                var json = JsonSerializer.Serialize(pool, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(_localization.GetResourceOrDefault("RPS_Log_ExportPoolFailed", "导出配方池失败: {0}"), ex.Message));
                return false;
            }
        }
        #region 辅助方法
        public async Task<List<string>> GetAllAvailableRecipesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var recipePools = await GetAllRecipePoolsAsync().ConfigureAwait(false);
                var recipes = new List<string>();
                foreach (var pool in recipePools)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var poolRecipes = await _recipeStorage.LoadAllRecipesAsync(pool.CurrentRecipePoolName).ConfigureAwait(false);
                    recipes.AddRange(poolRecipes.Select(r => r.Name));
                }
                return recipes.Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("RPS_Log_GetAllRecipesFailed", "获取所有可用配方列表失败: {0}"), ex.Message));
                return new List<string>();
            }
        }
        public async Task<(bool exists, string poolName, string poolId)> RecipeExistsInAnyPoolAsync(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
                return (false, null, null);
            try
            {
                var recipePools = await GetAllRecipePoolsAsync().ConfigureAwait(false);
                foreach (var pool in recipePools)
                {
                    var recipe = pool.GetRecipe(recipeId);
                    if (recipe != null)
                    {
                        return (true, pool.Name, pool.Id);
                    }
                }
                return (false, null, null);
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("RPS_Log_CheckRecipeExistFailed", "检查配方存在性失败: {0}"), ex.Message));
                return (false, null, null);
            }
        }
        public async Task<RecipeInfo> LoadRecipeFromAnyPoolAsync(string recipeName)
        {
            try
            {
                var recipePools = await GetAllRecipePoolsAsync().ConfigureAwait(false);
                foreach (var pool in recipePools)
                {
                    var recipe = await _recipeStorage.LoadRecipeAsync(pool.Id, recipeName).ConfigureAwait(false);
                    if (recipe != null)
                    {
                        return recipe;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(_localization.GetResourceOrDefault("RPS_Log_LoadRecipeFailed", "加载配方失败: {0}"), ex.Message));
                return null;
            }
        }
        public async Task<RecipePool> GetRecipePoolAsync(string poolId)
        {
            return await _recipeStorage.LoadRecipePoolAsync(poolId).ConfigureAwait(false);
        }
        public async Task RenameRecipePoolAsync(string oldPoolName, string newPoolName, string newDescription = "")
        {
            var pool = await _recipeStorage.LoadRecipePoolAsync(oldPoolName).ConfigureAwait(false);
            if (pool == null)
                throw new InvalidOperationException($"配方池 '{oldPoolName}' 不存在");
            bool nameChanged = pool.Name != newPoolName;
            pool.Name = newPoolName;
            pool.Description = newDescription;
            pool.ModifiedTime = DateTime.Now;
            if (nameChanged)
            {
                foreach (var recipe in pool.Recipes)
                {
                    recipe.Name = newPoolName;
                }
                if (oldPoolName == CurrentPoolName)
                {
                    ((IRecipePoolService)this).CurrentPoolName = newPoolName;
                }
                await _recipeStorage.DeleteRecipePoolAsync(oldPoolName).ConfigureAwait(false);
            }
            await _recipeStorage.SaveRecipePoolAsync(pool).ConfigureAwait(false);
            _logger.Info(string.Format(_localization.GetResourceOrDefault("RPS_Log_PoolUpdated", "配方池已更新: {0} -> {1}，同步更新了 {2} 个配方的 CurrentRecipePoolName"), oldPoolName, newPoolName, pool.Recipes.Count));
        }
        public async Task SaveRecipePoolAsync(RecipePool pool)
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                // 1. 保存前同步全局变量页面编辑内容到内存 pool
                _eventAggregator.GetEvent<SaveGlobalVariablesEvent>().Publish(pool);

                // 2. 提交位置编辑器暂存的工站参数到文件（由 SavePositionEditorEvent 触发暂存）
                //    必须在加载 filePool 之前完成，确保后续合并能读到最新位置参数
                if (_stagingArea.HasAnyDirty())
                {
                    var dirtyParams = _stagingArea.GetDirtyParameters();
                    var recipeName = pool.CurrentRecipeName ?? "Default";
                    var context = new RecipePoolSaveContext(_recipeStorage, pool.Name, recipeName);
                    foreach (var kv in dirtyParams)
                    {
                        context.AddStation(kv.Key, kv.Value, _stagingArea.ShouldReplacePositions(kv.Key));
                    }
                    var commitSuccess = await context.CommitAsync().ConfigureAwait(false);
                    if (commitSuccess)
                    {
                        _stagingArea.ClearDirty();
                        _logger.Info(string.Format(_localization.GetResourceOrDefault("RPS_Log_StagedParamsCommittedBeforeSave", "保存池前已提交位置编辑器暂存参数: 池 '{0}' -> 配方 '{1}', 共 {2} 个工站"), pool.Name, recipeName, dirtyParams.Count));
                    }
                    else
                    {
                        _logger.Warn(string.Format(_localization.GetResourceOrDefault("RPS_Log_CommitStagedBeforeSaveFailed", "保存池前提交位置编辑器暂存参数失败: 池 '{0}' -> 配方 '{1}'"), pool.Name, recipeName));
                    }
                }

                // 3. 从文件加载最新配方池（保留其他编辑器已提交的工站参数，避免陈旧内存数据覆盖文件）
                var filePool = await _recipeStorage.LoadRecipePoolAsync(pool.Name).ConfigureAwait(false);
                if (filePool != null)
                {
                    // 仅将内存 pool 的全局变量和顶层元数据合并到文件 pool
                    filePool.GlobalVariables = pool.GlobalVariables;
                    filePool.ModifiedTime = DateTime.Now;
                    pool = filePool;
                }

                await _recipeStorage.SaveRecipePoolAsync(pool).ConfigureAwait(false);
                // 保存完成后通知依赖全局变量的页面刷新链接值，如 VisionCaptureView。
                _eventAggregator.GetEvent<GlobalVariablesChangedEvent>().Publish(pool.Name);
                // 与 CommitStagedParametersAsync 对齐：通知位置缓存及 VisionCapture Photo Position 等依赖方从磁盘重载，
                // 避免 Save Pool 内联提交位置后界面仍显示旧坐标。
                _eventAggregator.GetEvent<SaveParametersCompletedEvent>().Publish(pool.CurrentRecipeName ?? "Default");
            }
            finally
            {
                _semaphore.Release();
            }
        }
        /// <summary>
        /// 解析配方池持久化键：存储层使用 pool.Name（recipe_pool_{Name}），
        /// 调用方传入 CurrentPoolId（GUID）时需映射到 CurrentPoolName，避免全局变量读写失败。
        /// </summary>
        private string ResolvePoolStorageKey(string poolIdOrName)
        {
            if (string.IsNullOrEmpty(poolIdOrName))
                return !string.IsNullOrEmpty(_currentPoolName) ? _currentPoolName : _currentPoolId;

            if (!string.IsNullOrEmpty(_currentPoolId)
                && string.Equals(poolIdOrName, _currentPoolId, StringComparison.Ordinal)
                && !string.IsNullOrEmpty(_currentPoolName)
                && !string.Equals(_currentPoolId, _currentPoolName, StringComparison.Ordinal))
            {
                return _currentPoolName;
            }

            return poolIdOrName;
        }

        public async Task UpdateRecipePoolAsync(string poolId, Action<RecipePool> updateAction)
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var storageKey = ResolvePoolStorageKey(poolId);
                var pool = await _recipeStorage.LoadRecipePoolAsync(storageKey).ConfigureAwait(false)
                         ?? new RecipePool { Id = poolId, Name = storageKey, CreatedTime = DateTime.Now };
                updateAction(pool);
                await _recipeStorage.SaveRecipePoolAsync(pool).ConfigureAwait(false);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        public async Task<List<GlobalVariable>> LoadGlobalVariablesAsync(string poolId)
        {
            var storageKey = ResolvePoolStorageKey(poolId);
            var pool = await _recipeStorage.LoadRecipePoolAsync(storageKey).ConfigureAwait(false);
            return pool?.GlobalVariables?.ToList() ?? new List<GlobalVariable>();
        }
        public async Task SaveGlobalVariablesAsync(string poolId, IEnumerable<GlobalVariable> variables)
        {
            await UpdateRecipePoolAsync(poolId, pool =>
            {
                pool.GlobalVariables = variables?.ToList() ?? new List<GlobalVariable>();
            }).ConfigureAwait(false);
        }
        public async Task<T> GetExtensionDataAsync<T>(string poolId, string key) where T : class, new()
        {
            var storageKey = ResolvePoolStorageKey(poolId);
            var pool = await _recipeStorage.LoadRecipePoolAsync(storageKey).ConfigureAwait(false);
            if (pool == null) return null;
            if (pool.ExtensionData.TryGetValue(key, out var jsonElement)
                && jsonElement.HasValue
                && jsonElement.Value.ValueKind != JsonValueKind.Null
                && jsonElement.Value.ValueKind != JsonValueKind.Undefined)
            {
                return jsonElement.Value.Deserialize<T>() ?? new T();
            }
            return null;
        }
        /// <summary>
        /// 将扩展数据写入配方池。通过 UpdateRecipePoolAsync 获取信号量，
        /// 确保与 SaveRecipePoolAsync 等其他写操作互斥，避免竞态条件导致数据回退。
        /// </summary>
        public async Task SetExtensionDataAsync<T>(string poolId, string key, T data) where T : class
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            // UpdateRecipePoolAsync 内部已解析 storageKey
            await UpdateRecipePoolAsync(poolId, pool =>
            {
                pool.ExtensionData[key] = JsonSerializer.SerializeToElement(data);
            }).ConfigureAwait(false);
        }
        #endregion
    }
}

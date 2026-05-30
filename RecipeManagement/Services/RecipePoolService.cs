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
            IEventAggregator eventAggregator,
            IStationRegistry stationRegistry,
            IAppSettingService appConfig,
            IRecipeDialogService recipeDialogService)
        {
            _recipeStorage = recipeStorage;
            _logger = logger;
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
            var pool = new RecipePool { Id = poolId, Name = name, CreatedTime = DateTime.UtcNow };
            await _recipeStorage.SaveRecipePoolAsync(pool).ConfigureAwait(false);
        }
        public async Task<bool> DeleteRecipePoolAsync(string poolName)
        {
            if (poolName == CurrentPoolName)
            {
                _logger.Warn($"尝试删除当前正在使用的配方池 '{poolName}'，操作被拒绝。");
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
                    _logger.Info($"默认池已删除，已将 '{newDefault.Name}' 设为新默认池");
                }
            }
            return true;
        }
        public void StageStationParameters(string stationIdentifier, object parameters)
        {
            _stagingArea.Stage(stationIdentifier, parameters);
            _logger.Info($"[{stationIdentifier}] 参数已暂存");
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
                _logger.Info("暂存区没有待提交的参数变更");
                return true;
            }
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var dirtyParams = _stagingArea.GetDirtyParameters();
                var context = new RecipePoolSaveContext(_recipeStorage, poolId, recipeName);
                foreach (var kv in dirtyParams)
                {
                    context.AddStation(kv.Key, kv.Value);
                }
                var success = await context.CommitAsync().ConfigureAwait(false);
                if (success)
                {
                    _stagingArea.ClearDirty();
                    _eventAggregator.GetEvent<SaveParametersCompletedEvent>().Publish(recipeName);
                    _logger.Info($"暂存区参数已提交到配方池: 池 '{poolId}' -> 配方 '{recipeName}', 共 {dirtyParams.Count} 个工站");
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.Error($"提交暂存区参数失败: {ex.Message}");
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        public async Task<bool> SaveStationParametersAsync(string poolId, string recipeName, string stationIdentifier, object parameters)
        {
            StageStationParameters(stationIdentifier, parameters);
            return await CommitStagedParametersAsync(poolId, recipeName).ConfigureAwait(false);
        }
        public async Task<bool> SaveAllStationParametersAsync(string poolId, string recipeName)
        {
            var stations = _stationRegistry.GetAllStations();
            if (!stations.Any())
            {
                _logger.Warn("没有找到任何工站，无法保存参数");
                return false;
            }
            _logger.Info($"开始保存所有工站参数到配方: {recipeName}");
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
                _logger.Warn("没有找到任何工站，无法执行批量切换");
                return;
            }
            if (showAlert)
            {
                var confirmed = await ShowConfirmationDialogAsync(
                    "确认批量切换",
                    $"确定要统一切换所有工站到配方 '{newRecipeName}' 吗？");
                if (!confirmed)
                {
                    _logger.Info($"用户取消批量切换配方: {newRecipeName}");
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
                _logger.Info($"所有工站已切换到配方: {newRecipeName} (池: {recipePoolName})");
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
                _logger.Info($"已在配方池 '{poolName}' 中，无需切换");
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
                        _logger.Info($"已保存原配方池 '{CurrentPoolName}' 的状态");
                    }
                }
                var newPool = await _recipeStorage.LoadRecipePoolAsync(poolName).ConfigureAwait(false);
                if (newPool == null)
                {
                    _logger.Warn($"目标配方池 '{poolName}' 不存在，创建默认池");
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
                _logger.Info($"已切换到配方池 '{poolName}'，当前配方 '{targetRecipeName}'");
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
                _logger.Warn("没有找到任何工站，无法切换配方");
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
                _logger.Info($"所有工站已切换到配方池 '{poolId}' 的配方 '{recipeName}'");
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
            _logger.Info($"已将配方池 '{defaultPoolId}' 设置为默认池");
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
            newPool.CreatedTime = DateTime.UtcNow;
            newPool.ModifiedTime = DateTime.UtcNow;
            newPool.IsDefault = false;
            foreach (var recipe in newPool.Recipes)
            {
                recipe.Id = Guid.NewGuid().ToString();
                recipe.CreatedTime = DateTime.UtcNow;
                recipe.ModifiedTime = DateTime.UtcNow;
            }
            await _recipeStorage.SaveRecipePoolAsync(newPool).ConfigureAwait(false);
            _logger.Info($"已复制配方池 {sourcePoolId} -> {newPoolId}，包含 {newPool.Recipes.Count} 个配方");
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
                _logger.Error($"导入配方池失败: {ex.Message}");
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
                _logger.Error($"导出配方池失败: {ex.Message}");
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
                _logger.Warn($"获取所有可用配方列表失败: {ex.Message}");
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
                _logger.Warn($"检查配方存在性失败: {ex.Message}");
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
                _logger.Warn($"加载配方失败: {ex.Message}");
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
            pool.ModifiedTime = DateTime.UtcNow;
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
            _logger.Info($"配方池已更新: {oldPoolName} -> {newPoolName}，同步更新了 {pool.Recipes.Count} 个配方的 CurrentRecipePoolName");
        }
        public async Task SaveRecipePoolAsync(RecipePool pool)
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                // 保存池前先让全局变量页面把当前编辑内容写入 pool，统一走 Save Pool 持久化。
                _eventAggregator.GetEvent<SaveGlobalVariablesEvent>().Publish(pool);
                await _recipeStorage.SaveRecipePoolAsync(pool).ConfigureAwait(false);
                // 保存完成后通知依赖全局变量的页面刷新链接值，如 VisionCaptureView。
                _eventAggregator.GetEvent<GlobalVariablesChangedEvent>().Publish(pool.Name);
            }
            finally
            {
                _semaphore.Release();
            }
        }
        public async Task UpdateRecipePoolAsync(string poolId, Action<RecipePool> updateAction)
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                var pool = await _recipeStorage.LoadRecipePoolAsync(poolId).ConfigureAwait(false)
                         ?? new RecipePool { Id = poolId, Name = poolId, CreatedTime = DateTime.UtcNow };
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
            var pool = await _recipeStorage.LoadRecipePoolAsync(poolId).ConfigureAwait(false);
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
            var pool = await _recipeStorage.LoadRecipePoolAsync(poolId).ConfigureAwait(false);
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
        public async Task SetExtensionDataAsync<T>(string poolId, string key, T data) where T : class
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var pool = await _recipeStorage.LoadRecipePoolAsync(poolId).ConfigureAwait(false)
                       ?? new RecipePool { Id = poolId, Name = poolId, CreatedTime = DateTime.UtcNow };
            pool.ExtensionData[key] = JsonSerializer.SerializeToElement(data);
            await _recipeStorage.SaveRecipePoolAsync(pool).ConfigureAwait(false);
        }
        #endregion
    }
}
using Core.Abstraction;
using Core.Utilities;
using Prism.Commands;
using Prism.Events;
using Prism.Services.Dialogs;
using Recipe.Events;
using Recipe.Interfaces;
using Recipe.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Recipe
{
    public class RecipeService<TParameters> : IRecipeService, IDisposable
          where TParameters : TaskParametersBase, new()
    {
        private TParameters _internalParameters = new TParameters();
        private string _currentRecipeName = "Default";
        private string _currentRecipePool = "Default";

        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IParameterEditor _parameterEditor;
        private readonly IParameterStorage _parameterStorage;
        private readonly IRecipeStorage _recipeStorage;
        private readonly IAppSettingService _appConfig;
        private readonly IRecipePoolService _recipePoolManager;
        private readonly IRecipeDialogService _recipeDialogService;

        private SubscriptionToken _recipeChangedToken;
        private SubscriptionToken _saveParametersToken;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        // 内部强类型事件存储（私有）
        private event EventHandler<TParameters> _parametersAppliedInternal;
        private event EventHandler<TParameters> _parametersLoadedInternal;
        private event EventHandler<string> _recipeChangedInternal;

        // 显式实现接口属性
        string IRecipeService.StationIdentifier { get; set; }
        string IRecipeService.StationName { get; set; }
        string IRecipeService.CurrentRecipeName => _currentRecipeName;
        string IRecipeService.CurrentRecipePoolName => _currentRecipePool;
        object IRecipeService.Parameters => _internalParameters;
        List<string> IRecipeService.AvailableRecipes => GetAvailableRecipes().Result;
        Task IRecipeService.InitializationTask { get; set; }

        ICommand IRecipeService.EditParametersCommand { get; set; }
        ICommand IRecipeService.SwitchRecipeCommand { get; set; }

        // 显式实现接口事件
        event EventHandler<object> IRecipeService.ParametersApplied
        {
            add => _parametersAppliedInternal += (sender, args) => value(sender, args);
            remove => _parametersAppliedInternal -= (sender, args) => value(sender, args);
        }

        event EventHandler<object> IRecipeService.ParametersLoaded
        {
            add => _parametersLoadedInternal += (sender, args) => value(sender, args);
            remove => _parametersLoadedInternal -= (sender, args) => value(sender, args);
        }

        event EventHandler<string> IRecipeService.RecipeChanged
        {
            add => _recipeChangedInternal += value;
            remove => _recipeChangedInternal -= value;
        }

        async Task IRecipeService.SaveCurrentParameters() => await SaveParametersToRecipe(_currentRecipePool, _currentRecipeName);

        public RecipeService(
            string stationIdentifier,
            string stationName,
            ILoggerService loggerService,
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IParameterEditor parameterEditor,
            IParameterStorage parameterStorage,
            IRecipeStorage recipeStorage,
            IAppSettingService appConfig,
            IRecipePoolService recipePoolManager,
            IRecipeDialogService recipeDialogService) 
        {
            ((IRecipeService)this).StationIdentifier = stationIdentifier;
            ((IRecipeService)this).StationName = stationName;
            _logger = loggerService;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _parameterEditor = parameterEditor;
            _parameterStorage = parameterStorage;
            _recipeStorage = recipeStorage;
            _appConfig = appConfig;
            _recipePoolManager = recipePoolManager;
            _recipeDialogService = recipeDialogService;
            InitializeCommands();
            SubscribeEvents();
            // 初始化当前配方池名称
            _currentRecipePool = _recipePoolManager.CurrentPoolName ?? "Default";
        }

        #region 初始化方法
        private void InitializeCommands()
        {
            ((IRecipeService)this).EditParametersCommand = new DelegateCommand(OnEditParameters);
            ((IRecipeService)this).SwitchRecipeCommand = new DelegateCommand(async () => 
                       await ((IRecipeService)this).SwitchRecipeAsync(null)); // 实际会走手动切换
            ((IRecipeService)this).InitializationTask = InitializeCurrentRecipeFromDefaultPoolAsync();
        }

        private void SubscribeEvents()
        {
            _recipeChangedToken = _eventAggregator.GetEvent<RecipeChangedEvent>()
                .Subscribe(async recipeName => await OnRecipeChanged(recipeName));

            _saveParametersToken = _eventAggregator.GetEvent<SaveParametersEvent>()
                .Subscribe(OnSaveParameters);
        }

        private async Task InitializeCurrentRecipeFromDefaultPoolAsync()
        {
            try
            {
                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 正在从当前配方池 '{_currentRecipePool}' 初始化当前配方...");

                var defaultPool = await _recipeStorage.LoadRecipePoolAsync(_currentRecipePool).ConfigureAwait(false);
                if (defaultPool == null)
                {
                    _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 配方池 '{_currentRecipePool}' 不存在，使用默认配方");
                    return;
                }

                var currentRecipeInfo = defaultPool.GetCurrentRecipeInfo();
                if (!currentRecipeInfo.IsValid)
                {
                    _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 没有有效的当前配方记录，使用默认配方");
                    return;
                }

                _currentRecipeName = currentRecipeInfo.RecipeName;
                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 从配方池 '{_currentRecipePool}' 成功加载上次选择的配方: {currentRecipeInfo.RecipeName}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{((IRecipeService)this).StationIdentifier}] 从配方池 '{_currentRecipePool}' 初始化当前配方失败: {ex.Message}");
                _currentRecipeName = "Default";
            }
        }
        #endregion

        #region 配方管理方法
        public async void OnEditParameters()
        {
            try
            {
                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 打开参数编辑窗口，当前配方: {_currentRecipeName}");

                await LoadRecipeParameters(_currentRecipeName);

                Action<TaskParametersBase> saveCallback = (savedParameters) =>
                {
                    OnParametersSaved(savedParameters);
                };

                var adapter = new ParameterEditableAdapter<TParameters>(this);
                await _parameterEditor.EditParameters(adapter, saveCallback);
            }
            catch (Exception ex)
            {
                _logger.Error($"[{((IRecipeService)this).StationIdentifier}] 打开参数编辑窗口失败: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"打开参数编辑窗口失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private async void OnParametersSaved(TaskParametersBase savedParameters)
        {
            try
            {
                if (savedParameters is TParameters parameters)
                {
                    _internalParameters = parameters;

                    await SaveParametersToRecipeSystem(_currentRecipePool, _currentRecipeName);

                    SaveParametersToFile();

                    // 触发内部事件
                    _parametersAppliedInternal?.Invoke(this, parameters);

                    _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 参数已保存并应用");

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("参数已成功保存并应用", "成功",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[{((IRecipeService)this).StationIdentifier}] 参数保存回调处理失败: {ex.Message}");
            }
        }

        private async Task OnRecipeChanged(string newRecipeName)
        {
            // 可以留空或根据需要实现
        }

        public virtual async Task LoadRecipeParameters(string poolName, string recipeName)
        {
            try
            {
                // 如果未传入池名，使用当前池
                string targetPool = string.IsNullOrEmpty(poolName) ? _currentRecipePool : poolName;

                var recipe = await GetRecipeParameters(targetPool, recipeName).ConfigureAwait(false);
                if (recipe != null)
                {
                    var stationParams = recipe.GetParameter<TParameters>(((IRecipeService)this).StationIdentifier);
                    if (stationParams != null)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            _internalParameters = stationParams;
                            _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 从配方 '{recipeName}' (池: {targetPool}) 加载了工站参数");
                            _parametersLoadedInternal?.Invoke(this, _internalParameters);
                        });
                        return;
                    }
                }

                await Task.Run(() => LoadParametersFromFile(recipeName)).ConfigureAwait(false);
                _parametersLoadedInternal?.Invoke(this, _internalParameters);
                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 从文件加载了配方 '{recipeName}' 的参数");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{((IRecipeService)this).StationIdentifier}] 从配方 '{recipeName}' 加载参数失败: {ex.Message}");
                LoadDefaultParameters();
                _parametersLoadedInternal?.Invoke(this, _internalParameters);
            }
        }
        #endregion

        #region 配方通用方法
        // 添加一个便捷重载（供内部调用，使用当前池）
        protected async Task LoadRecipeParameters(string recipeName)
        {
            await LoadRecipeParameters(_currentRecipePool, recipeName);
        }
        protected virtual async Task<RecipeInfo> GetRecipeParameters(string poolName, string recipeName)
        {
            try
            {
                var recipe = await _recipeStorage.LoadRecipeAsync(poolName, recipeName).ConfigureAwait(false);
                if (recipe != null)
                    return recipe;

                // 如果在指定池未找到，遍历所有池查找
                var recipePools = await _recipePoolManager.GetAllRecipePoolsAsync().ConfigureAwait(false);
                foreach (var pool in recipePools)
                {
                    if (pool.Name == poolName) continue;

                    recipe = await _recipeStorage.LoadRecipeAsync(pool.Name, recipeName).ConfigureAwait(false);
                    if (recipe != null)
                        return recipe;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{((IRecipeService)this).StationIdentifier}] 获取配方参数失败: {ex.Message}");
                return null;
            }
        }

        protected virtual async Task SaveCurrentRecipeToDefaultPoolAsync(string poolName, string recipeName)
        {
            try
            {
                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 正在将当前配方 '{recipeName}' 保存到配方池 {poolName}");

                var defaultPool = await _recipeStorage.LoadRecipePoolAsync(poolName);
                if (defaultPool == null)
                {
                    _logger.Warn($"[{((IRecipeService)this).StationIdentifier}] 默认配方池不存在，创建新的默认配方池");
                    defaultPool = new RecipePool
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = poolName,
                        CreatedTime = DateTime.UtcNow
                    };
                }

                defaultPool.SetCurrentRecipeInfo(((IRecipeService)this).StationIdentifier, recipeName, poolName);
                await _recipeStorage.SaveRecipePoolAsync(defaultPool);

                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 当前配方 '{recipeName}' 已保存到配方池{poolName}顶层属性");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{((IRecipeService)this).StationIdentifier}] 保存当前配方到配方池{poolName}失败: {ex.Message}");
            }
        }

        protected virtual async Task SaveParametersToRecipeSystem(string poolName, string recipeName)
        {
            try
            {
                // 保存配方池
                await _recipePoolManager.SaveStationParametersAsync(
                    poolName,
                    recipeName,
                    ((IRecipeService)this).StationIdentifier,
                    _internalParameters
                );
            }
            catch (Exception ex)
            {
                _logger.Error($"[{((IRecipeService)this).StationIdentifier}] 保存参数到配方系统失败: {ex.Message}");
            }
        }
        /// <summary>
        /// 保存单个工站的参数到本地文件
        /// </summary>
        protected virtual void SaveParametersToLocalFile(string recipeName)
        {
            try
            {
                var fixedIdentifier = $"{((IRecipeService)this).StationIdentifier}_Recipe_{recipeName}";
                // 自定义保存目录：Config\Parameters\{recipeName}\
                string customDirectory = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Config",
                    "Parameters",
                    recipeName);

                // 备份现有文件
                BackupExistingFile(fixedIdentifier, recipeName, customDirectory);

                // 保存参数到配方子目录（固定文件名，覆盖当前版本）
                _parameterStorage.Save(fixedIdentifier, _internalParameters, customDirectory);

                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 参数已保存到本地文件: {Path.Combine(customDirectory, fixedIdentifier)}.json");

                // 同时更新根目录下的默认参数文件（可选，保持兼容）
                //if (recipeName == _currentRecipeName)
                //{
                //    _parameterStorage.Save(((IRecipeService)this).StationIdentifier, _internalParameters);
                //    _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 同时更新了根目录默认参数文件");
                //}
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{((IRecipeService)this).StationIdentifier}] 保存参数到本地文件失败: {ex.Message}");
                throw;
            }
        }

        protected virtual void SaveParametersToFile()
        {
            try
            {
                if (_internalParameters == null)
                {
                    _logger.Warn($"[{((IRecipeService)this).StationIdentifier}] 参数为空，无法保存");
                    return;
                }

                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 开始保存参数到配方: {_currentRecipeName}");
                SaveParametersToLocalFile(_currentRecipeName);
                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 参数已保存到配方 '{_currentRecipeName}'");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{((IRecipeService)this).StationIdentifier}] 保存参数到配方失败: {ex.Message}");
                throw;
            }
        }

        public virtual async Task SaveParametersToRecipe(string poolName, string recipeName)
        {
            try
            {
                if (_internalParameters == null)
                {
                    _logger.Warn($"[{((IRecipeService)this).StationIdentifier}] 参数为空，无法保存");
                    return;
                }

                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 开始保存参数到配方: {recipeName}");

                await SaveParametersToRecipeSystem(poolName, recipeName);
                SaveParametersToLocalFile(recipeName);

                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 参数已保存到配方 '{recipeName}'");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{((IRecipeService)this).StationIdentifier}] 保存参数到配方失败: {ex.Message}");
                throw;
            }
        }

        public async Task SaveRecipeParameters(string poolName, string recipeName, TParameters parameters)
        {
            try
            {
                if (parameters == null)
                {
                    _logger.Warn($"[{((IRecipeService)this).StationIdentifier}] 参数为空，无法保存");
                    return;
                }

                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 开始保存参数到配方: {recipeName}");

                _internalParameters = parameters;
                await SaveParametersToRecipeSystem(poolName, recipeName);
                SaveParametersToLocalFile(recipeName);

                _parametersAppliedInternal?.Invoke(this, parameters);

                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 参数已保存到配方 '{recipeName}'");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{((IRecipeService)this).StationIdentifier}] 保存参数到配方失败: {ex.Message}");
                throw;
            }
        }

        protected virtual void LoadParametersFromFile(string recipeName)
        {
            try
            {
                var recipeSpecificIdentifier = $"{((IRecipeService)this).StationIdentifier}_Recipe_{recipeName}";
                var parameters = _parameterStorage.Load<TParameters>(recipeSpecificIdentifier);

                if (parameters != null && parameters.IsValid)
                {
                    _internalParameters = parameters;
                    _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 从本地文件加载了配方 '{recipeName}' 的参数");
                    return;
                }

                parameters = _parameterStorage.Load<TParameters>(((IRecipeService)this).StationIdentifier);
                if (parameters != null && parameters.IsValid)
                {
                    _internalParameters = parameters;
                    _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 从默认文件加载了参数");
                    return;
                }

                LoadDefaultParameters();
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{((IRecipeService)this).StationIdentifier}] 从文件加载参数失败: {ex.Message}");
                LoadDefaultParameters();
            }
        }

        public virtual async Task<string> GetCurrentRecipeNameFromDefaultPoolAsync(string poolName)
        {
            try
            {
                var defaultPool = await _recipeStorage.LoadRecipePoolAsync(poolName);
                if (defaultPool == null)
                {
                    return "Default";
                }

                var currentRecipeInfo = defaultPool.GetCurrentRecipeInfo();
                if (!currentRecipeInfo.IsValid)
                {
                    return "Default";
                }

                if (currentRecipeInfo.StationIdentifier != ((IRecipeService)this).StationIdentifier)
                {
                    return "Default";
                }

                return currentRecipeInfo.RecipeName ?? "Default";
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{((IRecipeService)this).StationIdentifier}] 从默认配方池获取当前配方名称失败: {ex.Message}");
                return "Default";
            }
        }

        public virtual async Task<CurrentRecipeInfo> GetCurrentRecipeInfoAsync()
        {
            try
            {
                var defaultPool = await _recipeStorage.LoadRecipePoolAsync(_currentRecipePool);
                if (defaultPool == null)
                {
                    return CreateDefaultInfo();
                }

                var currentRecipeInfo = defaultPool.GetCurrentRecipeInfo();
                if (!currentRecipeInfo.IsValid || currentRecipeInfo.StationIdentifier != ((IRecipeService)this).StationIdentifier)
                {
                    return CreateDefaultInfo();
                }

                currentRecipeInfo.IsValid = true;
                return currentRecipeInfo;
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{((IRecipeService)this).StationIdentifier}] 获取当前配方信息失败: {ex.Message}");
                return CreateDefaultInfo();
            }
        }
        #endregion

        #region 事件定义

        #endregion

        #region 核心切换方法
        /// <summary>
        /// 手动切换配方（可能由用户界面调用）
        /// </summary>
        public async Task SwitchRecipeAsync(string newRecipeName)
        {
            try
            {
                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 开始手动切换配方");

                var availableRecipes = await GetAvailableRecipes();
                if (!availableRecipes.Any())
                {
                    await _recipeDialogService.ShowAlertAsync("配方切换", "没有可用的配方");
                    return;
                }

                var selectedRecipe = await _recipeDialogService.ShowRecipeSelectionDialogAsync(
                    availableRecipes,
                    "选择配方",
                    $"请为工站 '{((IRecipeService)this).StationIdentifier}' 选择要切换的配方："
                );
                if (string.IsNullOrEmpty(selectedRecipe))
                {
                    _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 用户取消配方切换");
                    return;
                }

                var (existsManual, poolName, poolId) = await RecipeExistsInPool(selectedRecipe); // 内部调用 _recipeUtilityService
                if (!existsManual)
                {
                    await _recipeDialogService.ShowAlertAsync("配方切换", $"配方 '{selectedRecipe}' 不存在");
                    return;
                }

                var confirmed = await _recipeDialogService.ShowConfirmationDialogAsync(
                    "确认切换配方",
                    $"确定要切换到配方 '{selectedRecipe}' 吗？\n当前配方 '{_currentRecipeName}' 的参数将被保存。",
                   new[] { "取消", "确认切换"  } 
                 );

                if (confirmed != "确认切换")
                {
                    _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 用户取消配方切换确认");
                    return;
                }

                await SwitchToRecipe(selectedRecipe, poolName);

                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 手动切换配方完成: {_currentRecipeName} -> {selectedRecipe}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{((IRecipeService)this).StationIdentifier}] 手动切换配方失败: {ex.Message}");
                await _recipeDialogService.ShowAlertAsync("配方切换失败", $"切换配方时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 内部切换逻辑（已根据批量标志优化）
        /// </summary>
        public async Task SwitchToRecipe(string recipeName, string poolId)
        {
            try
            {
                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 开始切换到配方: {recipeName} (池: {poolId})");

                await SaveParametersToRecipe(poolId, _currentRecipeName);

                _currentRecipeName = recipeName;
                _currentRecipePool = poolId;

                await LoadRecipeParameters(recipeName);

                ApplyParametersToHardware();

                _eventAggregator.GetEvent<RecipeChangedEvent>().Publish(recipeName);

                await SaveCurrentRecipeToDefaultPoolAsync(poolId,recipeName);

                _recipeChangedInternal?.Invoke(this, recipeName);

                if (_appConfig != null)
                {
                    _appConfig.RecipeName = recipeName;
                    _appConfig.TryUpdateRecipeName(recipeName);
                }

                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 成功切换到配方: {recipeName} (池: {poolId})");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{((IRecipeService)this).StationIdentifier}] 切换配方失败: {ex.Message}");
                throw new Exception($"切换配方 '{recipeName}' 失败: {ex.Message}", ex);
            }
        }
        #endregion

        #region 辅助方法
        private CurrentRecipeInfo CreateDefaultInfo()
        {
            return new CurrentRecipeInfo
            {
                RecipeName = "Default",
                RecipePool = _currentRecipePool,
                StationIdentifier = ((IRecipeService)this).StationIdentifier,
                IsValid = false,
                IsDefault = true
            };
        }
        private void BackupExistingFile(string identifier, string recipeName, string customDirectory)
        {
            try
            {
                // 使用自定义目录构建文件路径
                var safeIdentifier = string.Join("_", identifier.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(customDirectory, $"{safeIdentifier}.json");

                if (!File.Exists(filePath))
                    return;

                // 备份目录：Config\Parameters\BackUp\{recipeName}\{yyyy-MM-dd}\
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string backupDir = Path.Combine(baseDir, "Config", "Parameters", "BackUp", recipeName, DateTime.Now.ToString("yyyy-MM-dd"));
                Directory.CreateDirectory(backupDir);

                string timestamp = DateTime.Now.ToString("HHmmss");
                string backupFileName = $"{safeIdentifier}_{timestamp}.json";
                string backupPath = Path.Combine(backupDir, backupFileName);

                File.Copy(filePath, backupPath, overwrite: true);
                _logger.Info($"已备份参数文件: {backupPath}");
            }
            catch (Exception ex)
            {
                _logger.Warn($"备份参数文件失败: {ex.Message}");
            }
        }
        private async Task<List<string>> GetAvailableRecipes()
        {
            return await _recipePoolManager.GetAllAvailableRecipesAsync();
        }

        private async Task<(bool exists, string poolName, string poolId)> RecipeExistsInPool(string recipeName)
        {
            return await _recipePoolManager.RecipeExistsInAnyPoolAsync(recipeName);
        }

        private void OnSaveParameters(string recipeName) { } // 可留空

        private void LoadDefaultParameters()
        {
            try
            {
                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 正在加载默认参数");
                _internalParameters = new TParameters();
                SetDefaultParameterValues();
                _logger.Info($"[{((IRecipeService)this).StationIdentifier}] 默认参数加载完成");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{((IRecipeService)this).StationIdentifier}] 加载默认参数失败: {ex.Message}");
                _internalParameters = new TParameters();
            }
        }

        private void SetDefaultParameterValues()
        {
            _logger.Debug($"[{((IRecipeService)this).StationIdentifier}] 参数默认值已设置");
        }

        private void ApplyParametersToHardware() { }

        public virtual void Dispose()
        {
            try
            {
                _recipeChangedToken?.Dispose();
                _saveParametersToken?.Dispose();
            }
            catch { }
        }
        #endregion
    }
}
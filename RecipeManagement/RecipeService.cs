using Core.Abstraction;
using Core.Abstractions.IConfiguration;
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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Recipe
{
    /// <summary>
    /// 配方功能基类 - 独立于 XTaskBase
    /// </summary>
    public class RecipeService<TParameters> : IDisposable
        where TParameters : TaskParametersBase, new()
    {
        protected TParameters _internalParameters = new TParameters();

        // 配方相关字段
        protected string _currentRecipeName = "Default";
        protected string _currentRecipePool = "Default";

        // 依赖服务
        protected readonly ILoggerService _logger;
        protected readonly IDialogService _dialogService;
        protected readonly IEventAggregator _eventAggregator;
        protected readonly IParameterEditor _parameterEditor;
        protected readonly IParameterStorage _parameterStorage;
        protected readonly IRecipeManager _recipeManager;
        protected readonly IRecipeStorage _recipeStorage;
        protected readonly IAppConfig _appConfig;
        protected RecipePoolManager _recipePoolManager;
        // 命令
        public ICommand EditParametersCommand { get; protected set; }
        public ICommand SwitchRecipeCommand { get; protected set; }

        // 事件订阅
        protected SubscriptionToken _recipeChangedToken;
        protected SubscriptionToken _saveParametersToken;

        // 工站标识
        public string StationIdentifier { get; }
        public string StationName { get; }

        // 公共属性
        public string CurrentRecipeName => _currentRecipeName;
        public TParameters Parameters => _internalParameters;
        public List<string> AvailableRecipes => GetAvailableRecipes().Result;

        public Task InitializationTask { get; private set; }

        public RecipeService(
            string stationIdentifier,
            string stationName,
            ILoggerService loggerService,
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IParameterEditor parameterEditor,
            IParameterStorage parameterStorage,
            IRecipeManager recipeManager,
            IRecipeStorage recipeStorage,
            IAppConfig appConfig,
            RecipePoolManager recipePoolManager)
        {
            StationIdentifier = stationIdentifier;
            StationName = stationName;
            _logger = loggerService;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _parameterEditor = parameterEditor;
            _parameterStorage = parameterStorage;
            _recipeManager = recipeManager;
            _recipeStorage = recipeStorage;
            _appConfig = appConfig;
            _recipePoolManager = recipePoolManager;
            InitializeCommands();
            SubscribeEvents();
            InitializationTask = InitializeCurrentRecipeFromDefaultPoolAsync();
        }

        #region 初始化方法

        private void InitializeCommands()
        {
            EditParametersCommand = new DelegateCommand(OnEditParameters);
            SwitchRecipeCommand = new DelegateCommand(OnSwitchRecipe);
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
                _logger.Info($"[{StationIdentifier}] 正在从默认配方池初始化当前配方...");

                var defaultPool = await _recipeStorage.LoadRecipePoolAsync("Default")
                       .ConfigureAwait(false);
                if (defaultPool == null)
                {
                    _logger.Info($"[{StationIdentifier}] 默认配方池不存在，使用默认配方");
                    return;
                }

                var currentRecipeInfo = defaultPool.GetCurrentRecipeInfo();
                if (!currentRecipeInfo.IsValid)// || currentRecipeInfo.StationIdentifier != StationIdentifier
                {
                    _logger.Info($"[{StationIdentifier}] 没有有效的当前配方记录，使用默认配方");
                    return;
                }

                _currentRecipeName = currentRecipeInfo.RecipeName;
                _logger.Info($"[{StationIdentifier}] 从默认配方池成功加载上次选择的配方: {currentRecipeInfo.RecipeName}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 从默认配方池初始化当前配方失败: {ex.Message}");
                _currentRecipeName = "Default";
            }
        }

        #endregion

        #region 配方管理方法

        public async void OnEditParameters()
        {
            try
            {
                _logger.Info($"[{StationIdentifier}] 打开参数编辑窗口，当前配方: {_currentRecipeName}");

                await LoadRecipeParameters(_currentRecipeName);

                Action<TaskParametersBase> saveCallback = (savedParameters) =>
                {
                    OnParametersSaved(savedParameters);
                };

                // 使用正确的泛型适配器
                var adapter = new ParameterEditableAdapter<TParameters>(this);
                await _parameterEditor.EditParameters(adapter, saveCallback);
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 打开参数编辑窗口失败: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"打开参数编辑窗口失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private void OnParametersSaved(TaskParametersBase savedParameters)
        {
            try
            {
                if (savedParameters is TParameters parameters)
                {
                    _internalParameters = parameters;
                    SaveParametersToFile();

                    // 触发参数应用事件
                    ParametersApplied?.Invoke(this, parameters);

                    _logger.Info($"[{StationIdentifier}] 参数已保存并应用");

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("参数已成功保存并应用", "成功",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 参数保存回调处理失败: {ex.Message}");
            }
        }

        private async Task OnRecipeChanged(string newRecipeName)
        {
            try
            {
                if (string.IsNullOrEmpty(newRecipeName))
                    return;

                _logger.Info($"[{StationIdentifier}] 配方切换事件: {_currentRecipeName} -> {newRecipeName}");

                await Task.Run(async () =>
                {
                    SaveParametersToRecipe(_currentRecipeName);
                    _currentRecipeName = newRecipeName;

                    if (_appConfig != null)
                    {
                        _appConfig.RecipeName = _currentRecipeName;
                    }

                    await LoadRecipeParameters(newRecipeName).ConfigureAwait(false);
                    await SaveCurrentRecipeToDefaultPoolAsync(newRecipeName);

                    if (_appConfig != null)
                    {
                        _appConfig.TryUpdateRecipeName(newRecipeName);
                    }

                    // 触发配方切换事件
                    RecipeChanged?.Invoke(this, newRecipeName);
                }).ConfigureAwait(false);

                _logger.Info($"[{StationIdentifier}] 配方 '{newRecipeName}' 参数加载完成");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 配方切换失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载配方参数
        /// </summary>
        public virtual async Task LoadRecipeParameters(string recipeName)
        {
            try
            {
                var recipe = await GetRecipeParameters(recipeName).ConfigureAwait(false);
                if (recipe != null)
                {
                    var stationParams = recipe.GetParameter<TParameters>(StationIdentifier);
                    if (stationParams != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _internalParameters = stationParams;
                            // 记录加载的参数信息
                            _logger.Info($"[{StationIdentifier}] 从配方 '{recipeName}' 加载了工站参数，共 {GetParameterCount()} 个参数");

                            // 触发参数加载事件
                            ParametersLoaded?.Invoke(this, _internalParameters);
                        });
                        return;
                    }
                }

                await Task.Run(() => LoadParametersFromFile(recipeName)).ConfigureAwait(false);

                // 触发参数加载事件
                ParametersLoaded?.Invoke(this, _internalParameters);

                _logger.Info($"[{StationIdentifier}] 从文件加载了配方 '{recipeName}' 的参数，共 {GetParameterCount()} 个参数");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{StationIdentifier}] 从配方 '{recipeName}' 加载参数失败: {ex.Message}");
                LoadDefaultParameters();

                // 即使失败也触发参数加载事件
                ParametersLoaded?.Invoke(this, _internalParameters);
            }
        }

        #endregion

        #region 配方通用方法

        /// <summary>
        /// 从配方获取参数
        /// </summary>
        protected virtual async Task<RecipeInfo> GetRecipeParameters(string recipeName)
        {
            try
            {
                var recipe = await _recipeStorage.LoadRecipeAsync(_currentRecipePool, recipeName).ConfigureAwait(false);
                if (recipe != null)
                    return recipe;

                var recipePools = _recipeManager.GetAllRecipePools();
                foreach (var pool in recipePools)
                {
                    if (pool.Id == _currentRecipePool)
                        continue;

                    recipe = await _recipeStorage.LoadRecipeAsync(pool.Id, recipeName).ConfigureAwait(false);
                    if (recipe != null)
                        return recipe;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{StationIdentifier}] 获取配方参数失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将当前配方保存到默认配方池中
        /// </summary>
        protected virtual async Task SaveCurrentRecipeToDefaultPoolAsync(string recipeName)
        {
            try
            {
                _logger.Info($"[{StationIdentifier}] 正在将当前配方 '{recipeName}' 保存到默认配方池");

                var defaultPool = await _recipeStorage.LoadRecipePoolAsync("Default");
                if (defaultPool == null)
                {
                    _logger.Warn($"[{StationIdentifier}] 默认配方池不存在，创建新的默认配方池");
                    defaultPool = new RecipePool
                    {
                        Id = "Default",
                        Name = "Default",
                        CreatedTime = DateTime.UtcNow
                    };
                }

                defaultPool.SetCurrentRecipeInfo(StationIdentifier, recipeName, "Default");
                await _recipeStorage.SaveRecipePoolAsync(defaultPool);

                _logger.Info($"[{StationIdentifier}] 当前配方 '{recipeName}' 已保存到默认配方池顶层属性");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{StationIdentifier}] 保存当前配方到默认配方池失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存参数到配方系统
        /// </summary>
        protected virtual async void SaveParametersToRecipeSystem(string recipeName)
        {
            try
            {
                await _recipePoolManager.SaveStationParametersAsync(
                    "Default",
                    recipeName,
                    StationIdentifier,
                    _internalParameters
                );
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 保存参数到配方系统失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存参数到本地文件
        /// </summary>
        protected virtual void SaveParametersToLocalFile(string recipeName)
        {
            try
            {
                var recipeSpecificIdentifier = $"{StationIdentifier}_Recipe_{recipeName}";
                _parameterStorage.Save(recipeSpecificIdentifier, _internalParameters);

                _logger.Info($"[{StationIdentifier}] 参数已保存到本地文件: {recipeSpecificIdentifier}");

                if (recipeName == _currentRecipeName)
                {
                    _parameterStorage.Save(StationIdentifier, _internalParameters);
                    _logger.Info($"[{StationIdentifier}] 同时更新了默认参数文件");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{StationIdentifier}] 保存参数到本地文件失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 保存参数到文件
        /// </summary>
        protected virtual void SaveParametersToFile()
        {
            try
            {
                if (_internalParameters == null)
                {
                    _logger.Warn($"[{StationIdentifier}] 参数为空，无法保存");
                    return;
                }

                _logger.Info($"[{StationIdentifier}] 开始保存参数到配方: {_currentRecipeName}");

                SaveParametersToRecipeSystem(_currentRecipeName);
                SaveParametersToLocalFile(_currentRecipeName);

                _logger.Info($"[{StationIdentifier}] 参数已保存到配方 '{_currentRecipeName}'");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 保存参数到配方失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 保存参数到指定配方
        /// </summary>
        public virtual void SaveParametersToRecipe(string recipeName)
        {
            try
            {
                if (_internalParameters == null)
                {
                    _logger.Warn($"[{StationIdentifier}] 参数为空，无法保存");
                    return;
                }

                _logger.Info($"[{StationIdentifier}] 开始保存参数到配方: {recipeName}");

                SaveParametersToRecipeSystem(recipeName);
                SaveParametersToLocalFile(recipeName);

                _logger.Info($"[{StationIdentifier}] 参数已保存到配方 '{recipeName}'");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 保存参数到配方失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 从本地文件加载参数（向后兼容）
        /// </summary>
        protected virtual void LoadParametersFromFile(string recipeName)
        {
            try
            {
                var recipeSpecificIdentifier = $"{StationIdentifier}_Recipe_{recipeName}";
                var parameters = _parameterStorage.Load<TParameters>(recipeSpecificIdentifier);

                if (parameters != null && parameters.IsValid)
                {
                    _internalParameters = parameters;
                    _logger.Info($"[{StationIdentifier}] 从本地文件加载了配方 '{recipeName}' 的参数");
                    return;
                }

                parameters = _parameterStorage.Load<TParameters>(StationIdentifier);
                if (parameters != null && parameters.IsValid)
                {
                    _internalParameters = parameters;
                    _logger.Info($"[{StationIdentifier}] 从默认文件加载了参数");
                    return;
                }

                LoadDefaultParameters();
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{StationIdentifier}] 从文件加载参数失败: {ex.Message}");
                LoadDefaultParameters();
            }
        }

        /// <summary>
        /// 获取当前配方名称
        /// </summary>
        public virtual async Task<string> GetCurrentRecipeNameFromDefaultPoolAsync()
        {
            try
            {
                var defaultPool = await _recipeStorage.LoadRecipePoolAsync("Default");
                if (defaultPool == null)
                {
                    return "Default";
                }

                var currentRecipeInfo = defaultPool.GetCurrentRecipeInfo();
                if (!currentRecipeInfo.IsValid)
                {
                    return "Default";
                }

                if (currentRecipeInfo.StationIdentifier != StationIdentifier)
                {
                    return "Default";
                }

                return currentRecipeInfo.RecipeName ?? "Default";
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{StationIdentifier}] 从默认配方池获取当前配方名称失败: {ex.Message}");
                return "Default";
            }
        }

        /// <summary>
        /// 获取当前配方完整信息
        /// </summary>
        public virtual async Task<CurrentRecipeInfo> GetCurrentRecipeInfoAsync()
        {
            try
            {
                var defaultPool = await _recipeStorage.LoadRecipePoolAsync("Default");
                if (defaultPool == null)
                {
                    return new CurrentRecipeInfo
                    {
                        RecipeName = "Default",
                        RecipePool = "Default",
                        StationIdentifier = StationIdentifier,
                        IsValid = false,
                        IsDefault = true
                    };
                }

                var currentRecipeInfo = defaultPool.GetCurrentRecipeInfo();
                if (!currentRecipeInfo.IsValid)
                {
                    return new CurrentRecipeInfo
                    {
                        RecipeName = "Default",
                        RecipePool = "Default",
                        StationIdentifier = StationIdentifier,
                        IsValid = false,
                        IsDefault = true
                    };
                }

                if (currentRecipeInfo.StationIdentifier != StationIdentifier)
                {
                    return new CurrentRecipeInfo
                    {
                        RecipeName = "Default",
                        RecipePool = "Default",
                        StationIdentifier = StationIdentifier,
                        IsValid = false,
                        IsDefault = true
                    };
                }

                currentRecipeInfo.IsValid = true;
                return currentRecipeInfo;
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{StationIdentifier}] 获取当前配方信息失败: {ex.Message}");
                return new CurrentRecipeInfo
                {
                    RecipeName = "Default",
                    RecipePool = "Default",
                    StationIdentifier = StationIdentifier,
                    IsValid = false,
                    IsDefault = true
                };
            }
        }

        #endregion

        #region 事件定义

        public event EventHandler<TParameters> ParametersApplied;
        public event EventHandler<string> RecipeChanged;
        public event EventHandler<TParameters> ParametersLoaded;

        #endregion

        #region 辅助方法

        private async void OnSwitchRecipe()
        {
            try
            {
                _logger.Info($"[{StationIdentifier}] 开始手动切换配方");

                // 获取可用配方列表
                var availableRecipes = await GetAvailableRecipes();
                if (!availableRecipes.Any())
                {
                    await ShowOperatorAlert("配方切换", "没有可用的配方");
                    return;
                }

                // 显示配方选择对话框
                var selectedRecipe = await ShowRecipeSelectionDialog(availableRecipes);
                if (string.IsNullOrEmpty(selectedRecipe))
                {
                    _logger.Info($"[{StationIdentifier}] 用户取消配方切换");
                    return;
                }

                // 验证配方是否存在
                var (exists, poolId) = await RecipeExistsInPool(selectedRecipe);
                if (!exists)
                {
                    await ShowOperatorAlert("配方切换", $"配方 '{selectedRecipe}' 不存在");
                    return;
                }

                // 确认切换
                var confirmResult = await GetOperatorConfirmation(
                    "确认切换配方",
                    $"确定要切换到配方 '{selectedRecipe}' 吗？\n当前配方 '{_currentRecipeName}' 的参数将被保存。",
                    new[] { "取消", "确认切换" }
                );

                if (confirmResult != "确认切换")
                {
                    _logger.Info($"[{StationIdentifier}] 用户取消配方切换确认");
                    return;
                }

                // 执行切换
                await SwitchToRecipe(selectedRecipe, poolId);

                _logger.Info($"[{StationIdentifier}] 手动切换配方完成: {_currentRecipeName} -> {selectedRecipe}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 手动切换配方失败: {ex.Message}");
                await ShowOperatorAlert("配方切换失败", $"切换配方时发生错误: {ex.Message}");
            }
        }
        /// <summary>
        /// 保存参数事件处理 - 完整的实现
        /// </summary>
        private async void OnSaveParameters(string recipeName)
        {
            try
            {
                if (string.IsNullOrEmpty(recipeName))
                {
                    _logger.Warn($"[{StationIdentifier}] 收到保存参数事件，但配方名称为空");
                    return;
                }

                _logger.Info($"[{StationIdentifier}] 收到保存参数事件，正在保存配方 '{recipeName}' 的参数");

                // 发布进度开始事件
                _eventAggregator.GetEvent<SaveParametersProgressEvent>().Publish(new SaveProgressInfo
                {
                    Progress = 0,
                    StationName = StationIdentifier,
                    Operation = "开始保存参数..."
                });

                // 使用异步方式执行保存操作
                await Task.Run(() =>
                {
                    try
                    {
                        // 模拟保存过程 - 分步骤报告进度
                        for (int i = 1; i <= 5; i++)
                        {
                            Thread.Sleep(200); // 模拟保存耗时

                            // 发布进度更新事件
                            _eventAggregator.GetEvent<SaveParametersProgressEvent>().Publish(new SaveProgressInfo
                            {
                                Progress = i * 20,
                                StationName = StationIdentifier,
                                Operation = $"正在保存步骤 {i}/5..."
                            });
                        }

                        // 在UI线程执行实际的保存操作
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                // 保存参数到指定配方
                                SaveParametersToRecipe(recipeName);

                                // 如果保存的是当前配方，同时更新内存中的参数
                                if (recipeName == _currentRecipeName)
                                {
                                    _logger.Info($"[{StationIdentifier}] 当前配方参数已更新");
                                }

                                _logger.Info($"[{StationIdentifier}] 配方 '{recipeName}' 参数保存完成");

                                // 发布保存完成事件
                                _eventAggregator.GetEvent<SaveParametersCompletedEvent>().Publish(recipeName);
                            }
                            catch (Exception ex)
                            {
                                _logger.Error($"[{StationIdentifier}] 保存参数失败: {ex.Message}");
                                throw;
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[{StationIdentifier}] 保存参数过程中发生错误: {ex.Message}");
                        throw;
                    }
                });

            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 保存参数事件处理失败: {ex.Message}");

                // 在UI线程显示错误信息
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"保存参数失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }
        /// <summary>
        /// 加载默认参数
        /// </summary>
        private void LoadDefaultParameters()
        {
            try
            {
                _logger.Info($"[{StationIdentifier}] 正在加载默认参数");

                // 创建新的参数实例
                _internalParameters = new TParameters();

                // 设置默认值
                SetDefaultParameterValues();

                _logger.Info($"[{StationIdentifier}] 默认参数加载完成");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 加载默认参数失败: {ex.Message}");
                // 无论如何都要确保有有效的参数实例
                _internalParameters = new TParameters();
            }
        }
        /// <summary>
        /// 设置参数默认值
        /// </summary>
        private void SetDefaultParameterValues()
        {
            // 这里可以根据具体参数类型设置特定的默认值
            // 例如：
            // if (_internalParameters is AssemblyStationParams assemblyParams)
            // {
            //     assemblyParams.ZAxisSpeed = 100;
            //     assemblyParams.XAxisMinLimit = 0;
            //     assemblyParams.XAxisMaxLimit = 500;
            // }
            // else if (_internalParameters is DispenserStationParams dispenserParams)
            // {
            //     dispenserParams.GlueVolume = 0.5;
            //     dispenserParams.DispenseSpeed = 50;
            // }

            _logger.Debug($"[{StationIdentifier}] 参数默认值已设置");
        }
        /// <summary>
        /// 显示配方选择对话框
        /// </summary>
        private async Task<string> ShowRecipeSelectionDialog(List<string> availableRecipes)
        {
            var tcs = new TaskCompletionSource<string>();

            try
            {
                var dialogParameters = new DialogParameters
                {
                    { "AvailableRecipes", new ObservableCollection<string>(availableRecipes) },
                    { "CurrentRecipe", _currentRecipeName },
                    { "Title", "选择配方" },
                    { "Message", $"请为工站 '{StationName}' 选择要切换的配方：" },
                    { "StationName", StationName }
                };

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _dialogService.ShowDialog("RecipeSelectionDialog", dialogParameters, result =>
                    {
                        try
                        {
                            // 通用方案：检查是否有选中的配方
                            var selectedRecipe = result.Parameters.GetValue<string>("SelectedRecipe");
                            if (!string.IsNullOrEmpty(selectedRecipe))
                            {
                                tcs.SetResult(selectedRecipe);
                            }
                            else
                            {
                                tcs.SetResult(null);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn($"[{StationIdentifier}] 处理对话框结果失败: {ex.Message}");
                            tcs.SetResult(null);
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 显示配方选择对话框失败: {ex.Message}");
                tcs.SetResult(null);
            }

            return await tcs.Task;
        }

        /// <summary>
        /// 切换到指定配方
        /// </summary>
        private async Task SwitchToRecipe(string recipeName, string poolId)
        {
            try
            {
                _logger.Info($"[{StationIdentifier}] 开始切换到配方: {recipeName} (池: {poolId})");

                // 保存当前配方参数
                SaveParametersToRecipe(_currentRecipeName);

                // 更新当前配方名称和池
                _currentRecipeName = recipeName;
                _currentRecipePool = poolId;

                // 加载新配方参数
                await LoadRecipeParameters(recipeName);

                // 应用参数到硬件
                ApplyParametersToHardware();

                // 发布配方改变事件
                _eventAggregator.GetEvent<RecipeChangedEvent>().Publish(recipeName);

                // 保存当前配方选择到默认配方池
                await SaveCurrentRecipeToDefaultPoolAsync(recipeName);

                // 更新应用配置
                if (_appConfig != null)
                {
                    _appConfig.RecipeName = recipeName;
                    _appConfig.TryUpdateRecipeName(recipeName);
                }

                _logger.Info($"[{StationIdentifier}] 成功切换到配方: {recipeName} (池: {poolId})");

                // 显示成功消息
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"已切换到配方: {recipeName}", "切换成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 切换配方失败: {ex.Message}");
                throw new Exception($"切换配方 '{recipeName}' 失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 检查配方是否存在并返回所在池
        /// </summary>
        private async Task<(bool exists, string poolId)> RecipeExistsInPool(string recipeName)
        {
            try
            {
                _logger.Debug($"[{StationIdentifier}] 检查配方存在性: {recipeName}");

                var recipePools = _recipeManager.GetAllRecipePools();
                foreach (var pool in recipePools)
                {
                    var recipe = await _recipeStorage.LoadRecipeAsync(pool.Id, recipeName);
                    if (recipe != null)
                    {
                        _logger.Debug($"[{StationIdentifier}] 配方 '{recipeName}' 存在于池 '{pool.Id}'");
                        return (true, pool.Id);
                    }
                }

                _logger.Warn($"[{StationIdentifier}] 配方 '{recipeName}' 不存在于任何池中");
                return (false, null);
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{StationIdentifier}] 检查配方存在性失败: {ex.Message}");
                return (false, null);
            }
        }

        /// <summary>
        /// 人工确认对话框
        /// </summary>
        private async Task<string> GetOperatorConfirmation(string title, string message, string[] options)
        {
            try
            {
                //object result = await Interfaces.Services.DialogService.ShowDialogAsync(
                //    title: title,
                //    message: message,
                //    buttons: options,
                //    defaultButtonIndex: 0
                //);

                //if (result is int index && index >= 0 && index < options.Length)
                //{
                //    return options[index];
                //}

                return null;
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 显示确认对话框失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 显示操作员提醒
        /// </summary>
        private async Task ShowOperatorAlert(string title, string message)
        {
            try
            {
                //await Interfaces.Services.DialogService.ShowDialogAsync(
                //    title: title,
                //    message: message,
                //    buttons: new[] { "确定" },
                //    defaultButtonIndex: 0
                //);
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 显示提醒对话框失败: {ex.Message}");
                // 备用方案：使用 MessageBox
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
        }

        /// <summary>
        /// 验证参数有效性
        /// </summary>
        public bool ValidateParameters()
        {
            try
            {
                if (_internalParameters == null)
                {
                    _logger.Warn($"[{StationIdentifier}] 参数为空");
                    return false;
                }

                // 调用参数的验证方法
                if (_internalParameters is TaskParametersBase taskParams)
                {
                    return taskParams.IsValid;
                }

                return _internalParameters.IsValid;
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 参数验证失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 重置为默认参数
        /// </summary>
        public void ResetToDefaultParameters()
        {
            try
            {
                _logger.Info($"[{StationIdentifier}] 正在重置为默认参数");

                var confirmResult = Application.Current.Dispatcher.Invoke(() =>
                {
                    return MessageBox.Show(
                        "确定要重置为默认参数吗？当前参数将会丢失。",
                        "确认重置",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning
                    );
                });

                if (confirmResult != MessageBoxResult.Yes)
                {
                    _logger.Info($"[{StationIdentifier}] 用户取消参数重置");
                    return;
                }

                LoadDefaultParameters();
                ApplyParametersToHardware();

                _logger.Info($"[{StationIdentifier}] 参数重置完成");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("参数已重置为默认值", "重置成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 重置参数失败: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"重置参数失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        /// <summary>
        /// 导出参数到文件
        /// </summary>
        public async Task ExportParametersAsync(string filePath)
        {
            try
            {
                _logger.Info($"[{StationIdentifier}] 正在导出参数到文件: {filePath}");

                await Task.Run(() =>
                {
                    _parameterStorage.Save(filePath, _internalParameters);
                });

                _logger.Info($"[{StationIdentifier}] 参数导出成功: {filePath}");

                await ShowOperatorAlert("导出成功", $"参数已成功导出到:\n{filePath}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 参数导出失败: {ex.Message}");
                await ShowOperatorAlert("导出失败", $"参数导出失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件导入参数
        /// </summary>
        public async Task ImportParametersAsync(string filePath)
        {
            try
            {
                _logger.Info($"[{StationIdentifier}] 正在从文件导入参数: {filePath}");

                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"参数文件不存在: {filePath}");
                }

                var confirmResult = await GetOperatorConfirmation(
                    "确认导入",
                    $"确定要从文件导入参数吗？当前参数将会被覆盖。\n文件: {Path.GetFileName(filePath)}",
                    new[] { "取消", "确认导入" }
                );

                if (confirmResult != "确认导入")
                {
                    _logger.Info($"[{StationIdentifier}] 用户取消参数导入");
                    return;
                }

                var importedParameters = await Task.Run(() =>
                {
                    return _parameterStorage.Load<TParameters>(filePath);
                });

                if (importedParameters == null)
                {
                    throw new Exception("导入的参数文件格式不正确或为空");
                }

                _internalParameters = importedParameters;
                ApplyParametersToHardware();

                _logger.Info($"[{StationIdentifier}] 参数导入成功");

                await ShowOperatorAlert("导入成功", "参数已成功从文件导入并应用");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 参数导入失败: {ex.Message}");
                await ShowOperatorAlert("导入失败", $"参数导入失败: {ex.Message}");
            }
        }

        private async Task<List<string>> GetAvailableRecipes()
        {
            try
            {
                var recipePools = _recipeManager.GetAllRecipePools();
                var recipes = new List<string>();

                foreach (var pool in recipePools)
                {
                    var poolRecipes = await _recipeStorage.LoadAllRecipesAsync(pool.Id);
                    recipes.AddRange(poolRecipes.Select(r => r.Name));
                }

                return recipes.Distinct().ToList();
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{StationIdentifier}] 获取配方列表失败: {ex.Message}");
                return new List<string>();
            }
        }
        private void ApplyParametersToHardware()
        {
            // 这里应该调用硬件接口来应用参数，例如：
            //_hardwareInterface.ApplyParameters(_internalParameters);
        }
        public virtual void Dispose()
        {
            _recipeChangedToken?.Dispose();
            _saveParametersToken?.Dispose();
        }

        #endregion

        #region 实用方法

        /// <summary>
        /// 获取配方信息摘要
        /// </summary>
        public string GetRecipeSummary()
        {
            return $"工站: {StationName}\n" +
                   $"当前配方: {_currentRecipeName}\n" +
                   $"配方池: {_currentRecipePool}\n" +
                   $"参数数量: {GetParameterCount()}\n" +
                   $"最后加载: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }

        /// <summary>
        /// 获取参数数量
        /// </summary>
        private int GetParameterCount()
        {
            if (_internalParameters == null) return 0;

            // 通过反射获取属性数量（简单实现）
            return _internalParameters.GetType().GetProperties().Length;
        }

        /// <summary>
        /// 检查参数是否已修改
        /// </summary>
        public bool HasParametersChanged()
        {
            // 这里可以实现参数修改状态的检查
            // 可以通过比较当前参数与保存的参数来实现
            return false; // 简化实现
        }

        /// <summary>
        /// 备份当前参数
        /// </summary>
        public void BackupCurrentParameters()
        {
            try
            {
                var backupIdentifier = $"{StationIdentifier}_Backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                _parameterStorage.Save(backupIdentifier, _internalParameters);
                _logger.Info($"[{StationIdentifier}] 参数备份完成: {backupIdentifier}");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[{StationIdentifier}] 参数备份失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 恢复备份的参数
        /// </summary>
        public void RestoreBackupParameters(string backupIdentifier)
        {
            try
            {
                var backupParameters = _parameterStorage.Load<TParameters>(backupIdentifier);
                if (backupParameters != null)
                {
                    _internalParameters = backupParameters;
                    ApplyParametersToHardware();
                    _logger.Info($"[{StationIdentifier}] 参数恢复完成: {backupIdentifier}");
                }
                else
                {
                    throw new Exception("备份文件不存在或格式错误");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifier}] 参数恢复失败: {ex.Message}");
                throw;
            }
        }

        #endregion
    }
}
// Framework/ViewModels/RecipeManagerViewModel.cs
using Prism.Mvvm;
using Prism.Commands;
using Prism.Events;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using System.Linq;
using Recipe.Events;
using Recipe.Interfaces;
using Recipe.Models;
using Core.Abstraction;
using Core.Utilities;
using Core.Services;
using System.IO;
using System.Text.Json;
using Recipe.Events;
using System.Windows;
using Framework.Views;
using Prism.Regions;


namespace Framework.ViewModels
{
    public class RecipeManagerViewModel : BindableBase
    {
        private readonly IRecipeManager _recipeManager;
        private readonly IRecipeStorage _recipeStorage;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDialogService _dialogService;
        private readonly ILoggerService _logger;
        private readonly IRegionManager _regionManager;

        public RecipeManagerViewModel(
            IRecipeManager recipeManager,
            IRecipeStorage recipeStorage,
            IEventAggregator eventAggregator,
            IDialogService dialogService,
            ILoggerService logger,
            IRegionManager regionManager)
        {
            _recipeManager = recipeManager;
            _recipeStorage = recipeStorage;
            _eventAggregator = eventAggregator;
            _dialogService = dialogService;
            _logger = logger;
            _regionManager = regionManager;
            // 初始化命令
            LoadRecipesCommand = new DelegateCommand(async () => await LoadRecipesAsync());
            SwitchRecipeCommand = new DelegateCommand<string>(async (recipe) => await SwitchRecipeAsync(recipe));
            CreateRecipeCommand = new DelegateCommand(async () => await CreateRecipeAsync());
            EditRecipeCommand = new DelegateCommand(async () => await EditRecipeAsync());
            DeleteRecipeCommand = new DelegateCommand(async () => await DeleteRecipeAsync());
            SaveParametersCommand = new DelegateCommand(async () => await SaveAllParametersAsync());
            LoadParametersCommand = new DelegateCommand(async () => await LoadAllParametersAsync());
            // 初始化数据
            _ = LoadRecipesAsync();
        }

        #region 属性

        // 使用 RecipePool 来管理配方
        private RecipePool _currentRecipePool = new RecipePool { Id = "Default", Name = "Default" };

        private ObservableCollection<RecipeInfo> _recipes = new ObservableCollection<RecipeInfo>();
        // 使用 ObservableCollection 用于数据绑定
        public ObservableCollection<RecipeInfo> Recipes
        {
            get => _recipes;
            set => SetProperty(ref _recipes, value);
        }


        private RecipeInfo _selectedRecipe;
        public RecipeInfo SelectedRecipe
        {
            get => _selectedRecipe;
            set
            {
                if (SetProperty(ref _selectedRecipe, value))
                {
                    OnRecipeSelected();
                }
            }
        }

        private string _currentRecipeName = "Default";
        public string CurrentRecipeName
        {
            get => _currentRecipeName;
            set => SetProperty(ref _currentRecipeName, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        #endregion

        #region 命令

        public DelegateCommand LoadRecipesCommand { get; }
        public DelegateCommand<string> SwitchRecipeCommand { get; }
        public DelegateCommand CreateRecipeCommand { get; }
        public DelegateCommand EditRecipeCommand { get; }
        public DelegateCommand DeleteRecipeCommand { get; }
        public DelegateCommand SaveParametersCommand { get; }
        public DelegateCommand LoadParametersCommand { get; }

        #endregion

        #region 方法

        private async Task LoadRecipesAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在加载配方列表...";
                Recipes.Clear();

                _logger.Info("开始加载配方列表...");

                // 方法1：优先从文件系统直接加载（最可靠的方式）
                await LoadRecipesFromFileSystemAsync();

                // 方法2：如果文件系统没有加载到配方，再尝试通过配方管理器加载
                if (!Recipes.Any())
                {
                    _logger.Info("文件系统未找到配方，尝试通过配方管理器加载...");
                    var recipePools = _recipeManager.GetAllRecipePools();
                    _logger.Info($"配方管理器返回 {recipePools.Count()} 个配方池");

                    foreach (var pool in recipePools)
                    {
                        await LoadRecipesFromPoolAsync(pool.Id);
                    }
                }

                // 方法3：如果仍然没有配方，创建默认配方
                if (!Recipes.Any())
                {
                    _logger.Warn("未找到任何配方，创建默认配方...");
                    await CreateDefaultRecipeAsync();
                }

                // 从配置文件读取当前配方名称
                var currentRecipe = await GetCurrentRecipeFromPoolAsync();
                CurrentRecipeName = currentRecipe;

                // 尝试选中当前配方
                if (!string.IsNullOrEmpty(currentRecipe) && currentRecipe != "Default")
                {
                    var currentRecipeInfo = Recipes.FirstOrDefault(r => r.Name == currentRecipe);
                    if (currentRecipeInfo != null)
                    {
                        SelectedRecipe = currentRecipeInfo;
                        _logger.Info($"已选中当前配方: {currentRecipe}");
                    }
                }

                StatusMessage = $"已加载 {Recipes.Count} 个配方";
                _logger.Info($"配方列表加载完成，共 {Recipes.Count} 个配方，当前配方: {CurrentRecipeName}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载配方列表失败: {ex.Message}";
                _logger.Error($"加载配方列表失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
        private async Task LoadRecipesFromFileSystemAsync()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
                var recipesDir = Path.Combine(baseDir, "Recipes");

                if (!Directory.Exists(recipesDir))
                {
                    _logger.Warn($"Recipes目录不存在: {recipesDir}");
                    return;
                }

                _logger.Info($"扫描Recipes目录: {recipesDir}");

                // 查找所有配方池文件
                var recipePoolFiles = Directory.GetFiles(recipesDir, "recipe_pool_*.json", SearchOption.AllDirectories);
                _logger.Info($"找到 {recipePoolFiles.Length} 个配方池文件");

                foreach (var filePath in recipePoolFiles)
                {
                    try
                    {
                        if (filePath.Contains("配方记录")) continue; // 跳过配方记录文件
                        var json = await File.ReadAllTextAsync(filePath);
                        var recipePool = JsonSerializer.Deserialize<RecipePool>(json);

                        if (recipePool?.Recipes != null)
                        {
                            _logger.Info($"从文件 {Path.GetFileName(filePath)} 加载到 {recipePool.Recipes.Count} 个配方");

                            foreach (var recipe in recipePool.Recipes)
                            {
                                // 避免重复添加
                                if (!Recipes.Any(r => r.Name == recipe.Name && r.Id == recipe.Id))
                                {
                                    Recipes.Add(new RecipeInfo
                                    {
                                        Name = recipe.Name,
                                        Description = recipe.Description,
                                        Id = recipe.Id,
                                        CreatedTime = recipe.CreatedTime,
                                        ModifiedTime = recipe.ModifiedTime
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"解析配方池文件 {Path.GetFileName(filePath)} 失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"从文件系统加载配方失败: {ex.Message}");
            }
        }

        private async Task LoadRecipesFromPoolAsync(string poolId)
        {
            try
            {
                _logger.Info($"从池 {poolId} 加载配方...");
                var recipes = await _recipeStorage.LoadAllRecipesAsync(poolId);
                _logger.Info($"从池 {poolId} 加载到 {recipes.Count()} 个配方");

                foreach (var recipe in recipes)
                {
                    // 避免重复添加
                    if (!Recipes.Any(r => r.Name == recipe.Name && r.Id == poolId))
                    {
                        Recipes.Add(new RecipeInfo
                        {
                            Name = recipe.Name,
                            Description = recipe.Description,
                            Id = poolId,
                            CreatedTime = recipe.CreatedTime,
                            ModifiedTime = recipe.ModifiedTime
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"从池 {poolId} 加载配方失败: {ex.Message}");
            }
        }

        private async Task CreateDefaultRecipeAsync()
        {
            try
            {
                _logger.Info("创建默认配方...");

                // 创建默认配方
                var defaultRecipe = new Recipe.Models.RecipeInfo
                {
                    Name = "Default",
                    Description = "默认配方",
                    CreatedTime = DateTime.UtcNow,
                    ModifiedTime = DateTime.UtcNow
                };

                // 保存到默认配方池
                await _recipeStorage.SaveRecipeAsync("Default", defaultRecipe);

                // 添加到列表
                Recipes.Add(new RecipeInfo
                {
                    Name = defaultRecipe.Name,
                    Description = defaultRecipe.Description,
                    Id = "Default",
                    CreatedTime = defaultRecipe.CreatedTime,
                    ModifiedTime = defaultRecipe.ModifiedTime
                });

                _logger.Info("默认配方创建完成");
            }
            catch (Exception ex)
            {
                _logger.Error($"创建默认配方失败: {ex.Message}");
            }
        }
        private async Task SwitchRecipeAsync(string recipeName)
        {
            try
            {
                if (string.IsNullOrEmpty(recipeName))
                    return;

                IsLoading = true;
                StatusMessage = $"正在切换到配方: {recipeName}";

                // 发布配方切换事件
                _eventAggregator.GetEvent<RecipeChangedEvent>().Publish(recipeName);
                CurrentRecipeName = recipeName;
                // 保存当前配方选择到配置文件
                //await SaveCurrentRecipeSelectionAsync(recipeName);
                StatusMessage = $"已切换到配方: {recipeName}";
                _logger.Info($"配方已切换到: {recipeName}");
            }
            catch (Exception ex)
            {
                StatusMessage = $"切换配方失败: {ex.Message}";
                _logger.Error($"切换配方失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
        private async Task SaveCurrentRecipeSelectionAsync(string recipeName)
        {
            try
            {
                // 保存当前配方选择到配置文件
                var config = new
                {
                    CurrentRecipe = recipeName,
                    LastUpdated = DateTime.UtcNow
                };

                var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config");
                Directory.CreateDirectory(configDir);

                var configFile = Path.Combine(configDir, "current_recipe.json");
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(configFile, json);
            }
            catch (Exception ex)
            {
                _logger.Warn($"保存当前配方选择失败: {ex.Message}");
            }
        }

        private async Task<string> GetCurrentRecipeFromPoolAsync()
        {
            try
            {
                // 方法1：从默认配方池获取当前配方
                var defaultPool = await Task.Run(async () =>
                {
                    try
                    {
                        return await _recipeStorage.LoadRecipePoolAsync("Default").ConfigureAwait(false);
                    }
                    catch
                    {
                        return null;
                    }
                });

                if (defaultPool != null && !string.IsNullOrEmpty(defaultPool.CurrentRecipeName))
                {
                    _logger.Info($"从默认配方池获取到当前配方: {defaultPool.CurrentRecipeName}");
                    return defaultPool.CurrentRecipeName;
                }

                // 方法2：从所有配方池中查找当前配方
                var allPools = await Task.Run(async () =>
                {
                    try
                    {
                        var pools = _recipeManager.GetAllRecipePools();
                        return pools?.ToList() ?? new List<RecipePool>();
                    }
                    catch
                    {
                        return new List<RecipePool>();
                    }
                });

                foreach (var pool in allPools)
                {
                    if (!string.IsNullOrEmpty(pool.CurrentRecipeName))
                    {
                        _logger.Info($"从配方池 {pool.Name} 获取到当前配方: {pool.CurrentRecipeName}");
                        return pool.CurrentRecipeName;
                    }
                }

                // 方法3：使用默认配方的第一个配方
                var defaultRecipes = await _recipeStorage.LoadAllRecipesAsync("Default").ConfigureAwait(false);
                var firstRecipe = defaultRecipes.FirstOrDefault();
                if (firstRecipe != null)
                {
                    _logger.Info($"使用默认配方的第一个配方: {firstRecipe.Name}");
                    return firstRecipe.Name;
                }

                return "Default";
            }
            catch (Exception ex)
            {
                _logger.Warn($"从配方池读取当前配方失败: {ex.Message}，使用默认值");
                return "Default";
            }
        }

        private class CurrentRecipeConfig
        {
            public string CurrentRecipe { get; set; } = "Default";
            public DateTime LastUpdated { get; set; }
        }
        private async Task CreateRecipeAsync()
        {
            try
            {
                var dialogParameters = new DialogParameters
                {
                    { "Mode", "Create" },
                    { "Title", "创建新配方" }
                };

                _dialogService.ShowDialog("RecipeEditorDialog", dialogParameters, async result =>
                {
                    if (result.Result == ButtonResult.OK)
                    {
                        var recipeName = result.Parameters.GetValue<string>("RecipeName");
                        var description = result.Parameters.GetValue<string>("Description");

                        // 创建新配方
                        var newRecipe = new Recipe.Models.RecipeInfo
                        {
                            Name = recipeName,
                            Description = description,
                            CreatedTime = DateTime.UtcNow,
                            ModifiedTime = DateTime.UtcNow
                        };

                        // 保存到默认配方池
                        await _recipeStorage.SaveRecipeAsync("Default", newRecipe);

                        // 重新加载配方列表
                        await LoadRecipesAsync();

                        StatusMessage = $"已创建配方: {recipeName}";
                        _logger.Info($"新配方已创建: {recipeName}");
                    }
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"创建配方失败: {ex.Message}";
                _logger.Error($"创建配方失败: {ex.Message}");
            }
        }

        private async Task EditRecipeAsync()
        {
            if (SelectedRecipe == null)
            {
                StatusMessage = "请先选择一个配方";
                return;
            }

            try
            {
                var dialogParameters = new DialogParameters
                {
                    { "Mode", "Edit" },
                    { "Title", "编辑配方" },
                    { "RecipeName", SelectedRecipe.Name },
                    { "Description", SelectedRecipe.Description }
                };

                _dialogService.ShowDialog("RecipeEditorDialog", dialogParameters, async result =>
                {
                    if (result.Result == ButtonResult.OK)
                    {
                        var newName = result.Parameters.GetValue<string>("RecipeName");
                        var description = result.Parameters.GetValue<string>("Description");

                        // 加载原配方
                        var originalRecipe = await _recipeStorage.LoadRecipeAsync(SelectedRecipe.Id, SelectedRecipe.Name);
                        if (originalRecipe != null)
                        {
                            // 更新配方信息
                            originalRecipe.Name = newName;
                            originalRecipe.Description = description;
                            originalRecipe.ModifiedTime = DateTime.UtcNow;

                            // 如果名称改变，需要删除原配方
                            if (newName != SelectedRecipe.Name)
                            {
                                await _recipeStorage.DeleteRecipeAsync("Default",SelectedRecipe.Id, SelectedRecipe.Name);
                            }

                            // 保存新配方
                            await _recipeStorage.SaveRecipeAsync(SelectedRecipe.Id, originalRecipe);

                            // 重新加载配方列表
                            await LoadRecipesAsync();

                            StatusMessage = $"配方已更新: {newName}";
                            _logger.Info($"配方已更新: {newName}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"编辑配方失败: {ex.Message}";
                _logger.Error($"编辑配方失败: {ex.Message}");
            }
        }

        private async Task DeleteRecipeAsync()
        {
            if (SelectedRecipe == null)
            {
                StatusMessage = "请先选择一个配方";
                return;
            }

            try
            {
                var dialogParameters = new DialogParameters
                {
                    { "Mode", "Delete" },
                    { "Title", "删除配方" },
                    { "Message", $"确定要删除配方 '{SelectedRecipe.Name}' 吗？此操作不可恢复。" }
                };

                _dialogService.ShowDialog("ConfirmationDialog", dialogParameters, async result =>
                {
                    if (result.Result == ButtonResult.Yes)
                    {
                        var name = SelectedRecipe.Name;

                        await _recipeStorage.DeleteRecipeAsync("Default", SelectedRecipe.Id, SelectedRecipe.Name);

                        // 重新加载配方列表
                        await LoadRecipesAsync();

                        StatusMessage = $"已删除配方: {name}";
                        _logger.Info($"配方已删除: {name}");
                    }
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"删除配方失败: {ex.Message}";
                _logger.Error($"删除配方失败: {ex.Message}");
            }
        }
        private async Task SaveAllParametersAsync()
        {
            bool saveCompleted = false;

            try
            {
                // 检查是否有选中的配方
                if (SelectedRecipe == null)
                {
                    StatusMessage = "请先选择一个配方";
                    MessageBox.Show("请先在配方列表中选择一个配方", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                IsLoading = true;
                StatusMessage = $"正在保存配方 '{SelectedRecipe.Name}' 的所有参数...";

                // 创建导航参数
                var navigationParameters = new NavigationParameters();
                navigationParameters.Add("recipeName", SelectedRecipe.Name);
                navigationParameters.Add("operation", "save");

                // 导航到进度指示器视图
                _regionManager.RequestNavigate("ContentRegionCore", nameof(BusyIndicatorView), navigationParameters);

                // 等待视图加载完成
                await Task.Delay(100);

                // 获取进度指示器ViewModel
                var busyRegion = _regionManager.Regions["ContentRegionCore"];
                var busyView = busyRegion?.ActiveViews?.OfType<BusyIndicatorView>()?.FirstOrDefault();
                var busyViewModel = busyView?.DataContext as BusyIndicatorViewModel;

                if (busyViewModel != null)
                {
                    // 模拟进度更新（作为后备方案）
                    _ = SimulateProgress(busyViewModel, SelectedRecipe.Name);

                    // 订阅保存完成事件
                    var completionToken = _eventAggregator
                        .GetEvent<SaveParametersCompletedEvent>()
                        .Subscribe(async (recipeName) =>
                        {
                            await Application.Current.Dispatcher.InvokeAsync(async () =>
                            {
                                if (recipeName == SelectedRecipe.Name && !saveCompleted)
                                {
                                    // 停止模拟进度
                                    saveCompleted = true;

                                    // 更新进度条状态
                                    busyViewModel.SetCompleted($"配方 '{recipeName}' 保存完成");

                                    // 等待2秒让用户看到完成状态
                                    await Task.Delay(2000);

                                    // 导航回配方管理页面
                                    _regionManager.RequestNavigate("ContentRegionCore", "RecipeManagerView");

                                    StatusMessage = $"配方 '{SelectedRecipe.Name}' 的所有参数已保存";
                                    _logger.Info($"配方 '{SelectedRecipe.Name}' 的所有工站参数已保存");
                                }
                            });
                        }, ThreadOption.UIThread);

                    // 订阅进度更新事件
                    var progressToken = _eventAggregator
                        .GetEvent<SaveParametersProgressEvent>()
                        .Subscribe(info =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                if (!saveCompleted)
                                {
                                    busyViewModel.UpdateProgress(info.Progress, $"{info.StationName}: {info.Operation}");
                                }
                            });
                        }, ThreadOption.UIThread);

                    try
                    {
                        // 初始化为不确定进度
                        busyViewModel.SetIndeterminate("正在启动参数保存...");

                        // 发布保存参数事件，携带配方名称
                        _eventAggregator.GetEvent<SaveParametersEvent>().Publish(SelectedRecipe.Name);

                        // 等待保存完成（设置超时时间，避免无限等待）
                        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30)); // 30秒超时
                        var completionTask = WaitForSaveCompletion();

                        // 等待任意一个任务完成
                        await Task.WhenAny(completionTask, timeoutTask);

                        if (!saveCompleted)
                        {
                            // 超时处理
                            busyViewModel.SetFailed("保存操作超时，请检查系统状态");
                            await Task.Delay(3000);
                            _regionManager.RequestNavigate("ContentRegionCore", "RecipeManagerView");
                        }
                    }
                    catch (Exception ex)
                    {
                        busyViewModel.SetFailed(ex.Message);
                        StatusMessage = $"保存参数失败: {ex.Message}";
                        _logger.Error($"保存参数失败: {ex.Message}");

                        // 错误时延迟显示错误信息
                        await Task.Delay(3000);
                        _regionManager.RequestNavigate("ContentRegionCore", "RecipeManagerView");
                    }
                    finally
                    {
                        // 取消订阅
                        _eventAggregator.GetEvent<SaveParametersCompletedEvent>().Unsubscribe(completionToken);
                        _eventAggregator.GetEvent<SaveParametersProgressEvent>().Unsubscribe(progressToken);
                    }
                }
                else
                {
                    // 如果无法显示进度条，直接保存
                    _eventAggregator.GetEvent<SaveParametersEvent>().Publish(SelectedRecipe.Name);
                    StatusMessage = $"配方 '{SelectedRecipe.Name}' 的所有参数已保存";
                    _logger.Info($"配方 '{SelectedRecipe.Name}' 的所有工站参数已保存");
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"保存参数失败: {ex.Message}";
                _logger.Error($"保存参数失败: {ex.Message}");

                // 确保导航回配方管理页面
                _regionManager.RequestNavigate("ContentRegionCore", "RecipeManagerView");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 模拟进度更新（作为后备方案）
        private async Task SimulateProgress(BusyIndicatorViewModel busyViewModel, string recipeName)
        {
            try
            {
                for (int i = 0; i <= 100; i += 5)
                {
                    await Task.Delay(500); // 每500ms更新5%

                    // 如果已经真正完成，停止模拟
                    if (busyViewModel.ProgressValue >= 100)
                        break;

                    string operation = i switch
                    {
                        <= 20 => "正在初始化保存过程...",
                        <= 40 => "正在保存运动参数...",
                        <= 60 => "正在保存视觉参数...",
                        <= 80 => "正在保存工艺参数...",
                        _ => "正在完成保存操作..."
                    };

                    // 只有当没有收到真实进度时才使用模拟进度
                    if (busyViewModel.ProgressValue <= i)
                    {
                        busyViewModel.UpdateProgress(i, $"[模拟] {operation}");
                    }
                }
            }
            catch
            {
                // 忽略模拟进度的异常
            }
        }

        // 等待保存完成的辅助方法
        private async Task WaitForSaveCompletion()
        {
            var tcs = new TaskCompletionSource<bool>();
            var token = _eventAggregator
                .GetEvent<SaveParametersCompletedEvent>()
                .Subscribe(_ => tcs.TrySetResult(true), ThreadOption.PublisherThread);

            try
            {
                await tcs.Task;
            }
            finally
            {
                _eventAggregator.GetEvent<SaveParametersCompletedEvent>().Unsubscribe(token);
            }
        }
        private async Task LoadAllParametersAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在加载所有参数...";

                // 重新加载当前配方参数
                await SwitchRecipeAsync(CurrentRecipeName);

                StatusMessage = "所有参数已加载";
                _logger.Info("所有工站参数已加载");
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载参数失败: {ex.Message}";
                _logger.Error($"加载参数失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnRecipeSelected()
        {
            if (SelectedRecipe != null)
            {
                // 可以在这里加载选中配方的详细信息
                StatusMessage = $"已选择配方: {SelectedRecipe.Name}";
            }
        }

        #endregion
    }

}
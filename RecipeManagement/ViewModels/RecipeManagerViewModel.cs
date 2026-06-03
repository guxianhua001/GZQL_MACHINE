using Core.Events;
using Core.Utilities;
using Core.Abstraction;
using MaterialDesignThemes.Wpf;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using Recipe;
using Recipe.Events;
using Recipe.Interfaces;
using Recipe.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace Recipe.ViewModels
{
    public enum NodeType { Pool, Recipe }

    public class TreeNode : BindableBase
    {
        public string Name { get; set; }
        public string Icon { get; set; }
        public object Data { get; set; }
        public NodeType Type { get; set; }
        public ObservableCollection<TreeNode> Children { get; set; } = new ObservableCollection<TreeNode>();

        private bool _isExpanded = true;
        public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        private bool _isCurrent;
        public bool IsCurrent { get => _isCurrent; set => SetProperty(ref _isCurrent, value); }
        private string _description;
        public string Description { get => _description;set => SetProperty(ref _description, value);}

        private DateTime? _modifiedTime;
        public DateTime? ModifiedTime { get => _modifiedTime;set => SetProperty(ref _modifiedTime, value); }
    }

    public class RecipeManagerViewModel : BindableBase
    {
        private readonly IRecipeStorage _recipeStorage;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDialogService _dialogService;
        private readonly ILoggerService _logger;
        private readonly IRegionManager _regionManager;
        private readonly IRecipePoolService _recipePoolService;
        private readonly IStationRegistry _stationRegistry;
        private readonly IRecipeDialogService _recipeDialogService;
        private readonly IParameterEditor _parameterEditor;
        private ObservableCollection<StationParameterGroup> _stationParameters = new();
        public ObservableCollection<StationParameterGroup> StationParameters
        {
            get => _stationParameters;
            set => SetProperty(ref _stationParameters, value);
        }
        public DelegateCommand RefreshStationParametersCommand { get; }

        #region 树形视图属性
        private ObservableCollection<TreeNode> _treeNodes = new();
        public ObservableCollection<TreeNode> TreeNodes { get => _treeNodes; set => SetProperty(ref _treeNodes, value); }

        private TreeNode _selectedNode;
        public TreeNode SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }

        public DelegateCommand<TreeNode> SelectNodeCommand { get; }
        #endregion

        public RecipeManagerViewModel(
            IRecipeStorage recipeStorage,
            IEventAggregator eventAggregator,
            IDialogService dialogService,
            ILoggerService logger,
            IRegionManager regionManager,
            IRecipePoolService recipePoolService,
            IRecipeDialogService recipeDialogService,
            IParameterEditor parameterEditor,
            IStationRegistry stationRegistry)
        {
            _recipeStorage = recipeStorage;
            _eventAggregator = eventAggregator;
            _dialogService = dialogService;
            _logger = logger;
            _regionManager = regionManager;
            _recipePoolService = recipePoolService;
            _stationRegistry = stationRegistry;
            _recipeDialogService = recipeDialogService;
            _parameterEditor = parameterEditor;

            RefreshStationParametersCommand = new DelegateCommand(ExecuteRefreshStationParameters);

            CreateRecipeCommand = new DelegateCommand(async () => await CreateRecipeAsync(), () => SelectedNode?.Type == NodeType.Pool)
                .ObservesProperty(() => SelectedNode);
            LoadRecipesCommand = new DelegateCommand(async () => await LoadRecipesAsync());
            SwitchRecipeCommand = new DelegateCommand(async () =>{ if (SelectedRecipe != null) await SwitchRecipeAsync(SelectedRecipe.Name); }, () => SelectedRecipe != null)
            .ObservesProperty(() => SelectedRecipe);
            SaveCurrentRecipeCommand = new DelegateCommand(async () => await SaveCurrentRecipeParametersAsync(), () => SelectedNode?.Type == NodeType.Recipe)
                .ObservesProperty(() => SelectedNode);
            EditRecipeCommand = new DelegateCommand(async () => await EditRecipeAsync(), () => SelectedRecipe != null)
                .ObservesProperty(() => SelectedRecipe);
            DeleteRecipeCommand = new DelegateCommand(async () => await DeleteRecipeAsync(),() => SelectedRecipe != null && SelectedRecipe.Name != "Default")
                 .ObservesProperty(() => SelectedRecipe);
            SaveRecipeCommand = new DelegateCommand<TreeNode>(async (node) => await SaveRecipeForNodeAsync(node));
            RefreshRecipeCommand = new DelegateCommand<TreeNode>(async (node) => await RefreshRecipeForNodeAsync(node));

            CreatePoolCommand = new DelegateCommand(async () => await CreatePoolAsync());
            LoadPoolsCommand = new DelegateCommand(async () => await LoadPoolsAsync());
            EditPoolCommand = new DelegateCommand(async () => await EditPoolAsync(), () => SelectedNode?.Type == NodeType.Pool)
                .ObservesProperty(() => SelectedNode);
            DeletePoolCommand = new DelegateCommand(async () => await DeletePoolAsync(), () => SelectedNode?.Type == NodeType.Pool && (SelectedNode.Data as RecipePool)?.Name != "Default")
               .ObservesProperty(() => SelectedNode);
            SwitchRecipeInPoolCommand = new DelegateCommand<RecipePool>(async (pool) => await SwitchRecipeInPoolAsync(pool));
            LoadRecipesForSelectedPoolCommand = new DelegateCommand(async () => await LoadRecipesAsync(), () => SelectedRecipePool != null).ObservesProperty(() => SelectedRecipePool);
            SaveRecipePoolCommand = new DelegateCommand(async () => await SaveRecipePoolAsync(), () => SelectedRecipePool != null).ObservesProperty(() => SelectedRecipePool);
            SwitchToCurrentPoolCommand = new DelegateCommand(async () => await SwitchRecipeInPoolAsync(SelectedRecipePool, true),
                                       () => SelectedRecipePool != null)
                   .ObservesProperty(() => SelectedRecipePool);
            // 三点菜单命令
            SwitchPoolCommand = new DelegateCommand<TreeNode>(async (node) => await SwitchPoolForNodeAsync(node));
            ToggleMenuCommand = new DelegateCommand<TreeNode>(node =>
            {
                SelectedNode = node;   // 让菜单项能拿到正确的节点
                IsMenuOpen = true;
            });

            SelectNodeCommand = new DelegateCommand<TreeNode>(OnNodeSelected);

            // 订阅服务层的属性变化
            if (_recipePoolService is INotifyPropertyChanged inpc)
            {
                inpc.PropertyChanged += (s, e) =>
                {
                    // 将服务层变更的 CurrentPoolName / CurrentRecipeName 同步到 ViewModel 的 RaisePropertyChanged
                    if (e.PropertyName == nameof(IRecipePoolService.CurrentPoolName))
                        RaisePropertyChanged(nameof(CurrentPoolName));
                    else if (e.PropertyName == nameof(IRecipePoolService.CurrentRecipeName))
                        RaisePropertyChanged(nameof(CurrentRecipeName));
                    // CurrentPoolId 同理
                };
            }
            _eventAggregator.GetEvent<StationParameterSavedEvent>().Subscribe(OnStationParameterSaved);
            _ = LoadPoolsAsync();
        }

        #region 属性
        private RecipePool _currentRecipePool = new() { Id = "Default", Name = "Default" };
        private ObservableCollection<RecipeInfo> _recipes = new();
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
                    _ = LoadStationParametersForRecipe(value);
                }
            }
        }

        private ObservableCollection<RecipePool> _recipePools;
        public ObservableCollection<RecipePool> RecipePools
        {
            get => _recipePools;
            set => SetProperty(ref _recipePools, value);
        }

        private RecipePool _selectedRecipePool;
        public RecipePool SelectedRecipePool
        {
            get => _selectedRecipePool;
            set
            {
                if (SetProperty(ref _selectedRecipePool, value))
                {
                    _ = LoadRecipesAsync();
                }
            }
        }
        // 会触发服务层通知，ViewModel 订阅后会更新
        public string CurrentPoolName
        {
            get => _recipePoolService.CurrentPoolName;
            set => _recipePoolService.CurrentPoolName = value;
        }

        public string CurrentRecipeName
        {
            get => _recipePoolService.CurrentRecipeName;
            set => _recipePoolService.CurrentRecipeName = value;
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

        private int _selectedTabIndex = 1;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        private bool _isMenuOpen;
        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set => SetProperty(ref _isMenuOpen, value);
        }

        #endregion

        #region 命令 (公共)
        public DelegateCommand LoadRecipesCommand { get; }
        public DelegateCommand SwitchRecipeCommand { get; }
        public DelegateCommand SwitchToCurrentPoolCommand { get; }
        public DelegateCommand<RecipePool> SwitchRecipeInPoolCommand { get; }
        public DelegateCommand CreateRecipeCommand { get; }
        public DelegateCommand EditRecipeCommand { get; }
        public DelegateCommand DeleteRecipeCommand { get; }
        public DelegateCommand CreatePoolCommand { get; }
        public DelegateCommand EditPoolCommand { get; }
        public DelegateCommand DeletePoolCommand { get; }
        public DelegateCommand LoadPoolsCommand { get; }
        public DelegateCommand LoadRecipesForSelectedPoolCommand { get; }
        public DelegateCommand SaveRecipePoolCommand { get; }
        public DelegateCommand SaveCurrentRecipeCommand { get; }

        // 三点菜单命令
        public DelegateCommand<TreeNode> RefreshRecipeCommand { get; }
        public DelegateCommand<TreeNode> SwitchPoolCommand { get; }
        public DelegateCommand<TreeNode> SaveRecipeCommand { get; }
        public DelegateCommand<TreeNode> ToggleMenuCommand { get; }
        #endregion

        #region 配方/池操作方法 (保持原有逻辑)
        private async Task SwitchRecipeAsync(string recipeName)
        {
            try
            {
                if (string.IsNullOrEmpty(recipeName)) return;

                // 先弹出确认框，用户确认后再执行切换
                var confirmed = await ShowGlobalConfirmAsync(
                    "确认切换配方",
                    $"确定要切换到配方 '{recipeName}' 吗？\n所有工站将统一切换到该配方。",
                    "SwapHorizontal", "#FF9800");
                if (!confirmed)
                {
                    StatusMessage = "用户取消切换配方";
                    return;
                }

                IsLoading = true;
                StatusMessage = $"正在切换到配方: {recipeName}";

                var targetRecipe = Recipes.FirstOrDefault(r => r.Name == recipeName);
                if (targetRecipe == null)
                {
                    StatusMessage = $"配方 '{recipeName}' 不存在于当前池中";
                    return;
                }

                string recipeId = targetRecipe.Id;
                var (exists, poolName, poolId) = await _recipePoolService.RecipeExistsInAnyPoolAsync(recipeId);
                if (!exists)
                {
                    StatusMessage = $"配方 '{recipeName}' 不存在";
                    return;
                }

                await _recipePoolService.SwitchAllStationsAsync(poolName, poolId, recipeName, showAlert: false);

                var currentPool = await _recipePoolService.GetRecipePoolAsync(poolName);
                if (currentPool != null)
                {
                    currentPool.CurrentRecipeName = recipeName;
                    currentPool.ModifiedTime = DateTime.UtcNow;
                    await _recipePoolService.SaveRecipePoolAsync(currentPool);
                }

                CurrentRecipeName = recipeName;

                if (SelectedRecipePool != null && poolId == SelectedRecipePool.Id)
                {
                    SelectedRecipePool.CurrentRecipeName = recipeName;
                    SelectedRecipePool.ModifiedTime = DateTime.UtcNow;
                    RaisePropertyChanged(nameof(SelectedRecipePool));
                }

                UpdateTreeCurrentState();
                _eventAggregator.GetEvent<RecipeChangedEvent>().Publish(poolName);
                StatusMessage = $"已切换到配方池: {poolName} -> 配方:{recipeName}，时间: {DateTime.Now:HH:mm:ss}";
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

        private async Task CreateRecipeAsync()
        {
            try
            {
                if (SelectedRecipePool == null)
                {
                    StatusMessage = "请先选择一个配方池";
                    return;
                }

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

                        RecipeInfo sourceRecipe = null;
                        if (SelectedRecipe != null)
                            sourceRecipe = await _recipeStorage.LoadRecipeAsync(SelectedRecipePool.Name, SelectedRecipe.Name);
                        if (sourceRecipe == null)
                        {
                            var pool = await _recipeStorage.LoadRecipePoolAsync(SelectedRecipePool.Name);
                            if (pool != null && !string.IsNullOrEmpty(pool.CurrentRecipeName))
                                sourceRecipe = await _recipeStorage.LoadRecipeAsync(SelectedRecipePool.Name, pool.CurrentRecipeName);
                        }
                        if (sourceRecipe == null)
                            sourceRecipe = await _recipeStorage.LoadRecipeAsync(SelectedRecipePool.Name, "Default");

                        var newRecipe = new RecipeInfo
                        {
                            Name = recipeName,
                            Description = description,
                            CreatedTime = DateTime.UtcNow,
                            ModifiedTime = DateTime.UtcNow,
                            Parameters = sourceRecipe?.Parameters != null ? new Dictionary<string, object>(sourceRecipe.Parameters) : new Dictionary<string, object>()
                        };

                        await _recipeStorage.SaveRecipeAsync(SelectedRecipePool.Name, newRecipe);
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
            if (SelectedRecipePool == null || SelectedRecipe == null)
            {
                StatusMessage = "请先选择配方池和配方";
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
                        var oldName = SelectedRecipe.Name;
                        var poolName = SelectedRecipePool.Name;

                        var originalRecipe = await _recipeStorage.LoadRecipeAsync(poolName, oldName);
                        if (originalRecipe == null)
                        {
                            StatusMessage = "配方不存在";
                            return;
                        }

                        bool nameChanged = newName != oldName;
                        originalRecipe.Name = newName;
                        originalRecipe.Description = description;
                        originalRecipe.ModifiedTime = DateTime.UtcNow;

                        if (nameChanged)
                            await _recipeStorage.DeleteRecipeAsync(poolName, oldName);
                        await _recipeStorage.SaveRecipeAsync(poolName, originalRecipe);

                        var pool = await _recipeStorage.LoadRecipePoolAsync(poolName);
                        if (pool != null && pool.CurrentRecipeName == oldName)
                        {
                            pool.CurrentRecipeName = newName;
                            await _recipeStorage.SaveRecipePoolAsync(pool);
                        }

                        if (CurrentRecipeName == oldName && CurrentPoolName == poolName)
                            CurrentRecipeName = newName;

                        await LoadRecipesAsync();
                        SelectedRecipe = Recipes.FirstOrDefault(r => r.Name == newName);
                        StatusMessage = $"配方已更新: {newName}";
                        _logger.Info($"配方已更新: {oldName} -> {newName}");
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
            if (SelectedRecipe == null || SelectedRecipePool == null)
            {
                StatusMessage = "请先选择一个配方";
                return;
            }

            if (SelectedRecipe.Name == SelectedRecipePool.CurrentRecipeName)
            {
                _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                {
                    { "title", "无法删除" },
                    { "message", $"配方 '{SelectedRecipe.Name}' 正在使用中，无法删除。请先切换到其他配方。" }
                }, _ => { });
                return;
            }

            _dialogService.ShowDialog("AlertDialog", new DialogParameters
            {
                { "title", "删除配方" },
                { "message",  $"确定要删除配方 '{SelectedRecipe.Name}' 吗？此操作不可恢复。" }
            }, async result =>
            {
                if (result.Result != ButtonResult.Yes) return;

                try
                {
                    string poolId = SelectedRecipePool.Name;
                    string recipeId = SelectedRecipe.Id;
                    string recipeName = SelectedRecipe.Name;

                    await _recipeStorage.DeleteRecipeAsync(poolId, recipeId);
                    var pool = await _recipeStorage.LoadRecipePoolAsync(poolId);
                    if (pool != null && pool.CurrentRecipeName == recipeName)
                    {
                        var recipes = await _recipeStorage.LoadAllRecipesAsync(poolId);
                        string newCurrent = recipes.Any() ? recipes.First().Name : "Default";
                        pool.CurrentRecipeName = newCurrent;
                        await _recipeStorage.SaveRecipePoolAsync(pool);
                        CurrentRecipeName = newCurrent;
                    }

                    await LoadRecipesAsync();
                    StatusMessage = $"已删除配方: {recipeName}";
                    _logger.Info($"配方 '{recipeName}' 已删除");
                }
                catch (Exception ex)
                {
                    StatusMessage = $"删除配方失败: {ex.Message}";
                    _logger.Error(ex, "删除配方失败");
                }
            });
        }

        private async Task LoadRecipesAsync()
        {
            if (SelectedRecipePool == null) return;
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() => IsLoading = true);
                await Application.Current.Dispatcher.InvokeAsync(() => StatusMessage = $"正在加载配方池 '{SelectedRecipePool.Name}' 的配方...");

                string poolId = SelectedRecipePool.Name;
                var recipes = await _recipeStorage.LoadAllRecipesAsync(poolId);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Recipes.Clear();
                    foreach (var r in recipes)
                        Recipes.Add(r);
                    StatusMessage = $"已加载 {Recipes.Count} 个配方";
                    IsLoading = false;
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    StatusMessage = $"加载配方失败: {ex.Message}";
                    IsLoading = false;
                });
                _logger.Error(ex, "加载配方失败");
            }
        }

        private async Task LoadPoolsAsync()
        {
            try
            {
                var pools = await _recipePoolService.GetAllRecipePoolsAsync();
                if (pools == null || !pools.Any())
                {
                    await _recipePoolService.CreateRecipePoolAsync("Default", "Default");
                    pools = await _recipePoolService.GetAllRecipePoolsAsync();
                }

                // 1. 加载使用的配方池
                var defaultPool = pools.FirstOrDefault(p => p.IsDefault);
                if (defaultPool != null)
                {
                    // 同步服务层状态（会触发 PropertyChanged，从而更新 ViewModel 代理属性）
                    _recipePoolService.CurrentPoolName = defaultPool.Name;
                    if (!string.IsNullOrEmpty(defaultPool.CurrentRecipeName))
                    {
                        _recipePoolService.CurrentRecipeName = defaultPool.CurrentRecipeName;
                    }
                }
                else
                {
                    // 容错：没有任何池标记为默认（极端情况，如数据损坏）
                    var firstPool = pools.FirstOrDefault();
                    if (firstPool != null)
                    {
                        firstPool.IsDefault = true;
                        await _recipePoolService.SaveRecipePoolAsync(firstPool);
                        _recipePoolService.CurrentPoolName = firstPool.Name;
                        if (!string.IsNullOrEmpty(firstPool.CurrentRecipeName))
                        {
                            _recipePoolService.CurrentRecipeName = firstPool.CurrentRecipeName;
                        }
                    }
                }

                // 2. 构建树节点（使用服务层状态驱动界面表现）
                TreeNodes.Clear();
                foreach (var pool in pools)
                {
                    TreeNodes.Add(new TreeNode
                    {
                        Name = pool.Name,
                        Icon = "CubeOutline",
                        Data = pool,
                        Type = NodeType.Pool,
                        IsExpanded = pool.Name == _recipePoolService.CurrentPoolName,
                        Description = pool.Description,
                        ModifiedTime = pool.ModifiedTime
                    });
                }

                // 3. 选中当前池节点，并加载其配方子节点
                var currentPoolNode = TreeNodes.FirstOrDefault(n =>
                    n.Data is RecipePool p && p.Name == _recipePoolService.CurrentPoolName);
                if (currentPoolNode != null)
                {
                    await LoadRecipesForPoolNodeAsync(currentPoolNode);
                    currentPoolNode.IsSelected = true;   // 始终选中池节点
                }

                // 4. 刷新 RecipePools 集合
                RecipePools = new ObservableCollection<RecipePool>(pools);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "加载配方池失败");
            }
        }
        private async Task SaveCurrentRecipeParametersAsync()
        {
            if (SelectedRecipe == null || SelectedRecipePool == null)
            {
                StatusMessage = "请先选择一个配方";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"正在保存配方 '{SelectedRecipe.Name}' 的参数...";
                var poolId = SelectedRecipePool.Name;
                bool success = await _recipePoolService.SaveAllStationParametersAsync(poolId, SelectedRecipe.Name);
                StatusMessage = success ? $"配方 '{SelectedRecipe.Name}' 的参数已保存" : $"保存配方 '{SelectedRecipe.Name}' 的参数失败";
                _logger.Info(success ? $"配方 '{SelectedRecipe.Name}' 的参数已手动保存" : "保存失败");
            }
            catch (Exception ex)
            {
                StatusMessage = $"保存配方参数失败: {ex.Message}";
                _logger.Error(ex, "保存配方参数失败");
            }
            finally { IsLoading = false; }
        }

        private async Task SaveRecipePoolAsync()
        {
            if (SelectedRecipePool == null)
            {
                StatusMessage = "请先选择一个配方池";
                return;
            }
            try
            {
                IsLoading = true;
                StatusMessage = $"正在保存配方池 '{SelectedRecipePool.Name}'...";

                // 从文件加载最新池（包含被编辑器修改的工站参数）
                var latestPool = await _recipePoolService.GetRecipePoolAsync(SelectedRecipePool.Name);
                if (latestPool != null)
                {
                    SelectedRecipePool = latestPool;
                }

                await _recipePoolService.SaveRecipePoolAsync(SelectedRecipePool);
                StatusMessage = $"配方池 '{SelectedRecipePool.Name}' 及全局变量已保存";
            }
            catch (Exception ex) { /* ... */ }
            finally { IsLoading = false; }
        }

        private async Task CreatePoolAsync()
        {
            var sourcePool = SelectedRecipePool;
            if (sourcePool == null)
            {
                StatusMessage = "请先选择一个配方池作为复制源";
                return;
            }

            var parameters = new DialogParameters
            {
                { "Mode", "Create" },
                { "Title", "新建配方池（复制当前池）" }
            };

            _dialogService.ShowDialog("RecipeEditorDialog", parameters, async result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var name = result.Parameters.GetValue<string>("RecipeName");
                    var description = result.Parameters.GetValue<string>("Description");
                    var id = name.Replace(" ", "_");
                    try
                    {
                        await _recipePoolService.CopyRecipePoolAsync(sourcePool.CurrentRecipePoolName, id, name, description);
                        await LoadPoolsAsync();
                        SelectedRecipePool = RecipePools.FirstOrDefault(p => p.CurrentRecipePoolName == id);
                        StatusMessage = $"已复制配方池 '{sourcePool.Name}' 为 '{name}'";
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"复制配方池失败: {ex.Message}";
                        _logger.Error(ex, "复制配方池失败");
                    }
                }
            });
        }

        private async Task EditPoolAsync()
        {
            if (SelectedRecipePool == null) return;
            var parameters = new DialogParameters
            {
                { "Mode", "Edit" },
                { "Title", "编辑配方池" },
                { "RecipeName", SelectedRecipePool.Name },
                { "Description", SelectedRecipePool.Description }
            };
            _dialogService.ShowDialog("RecipeEditorDialog", parameters, async result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var newName = result.Parameters.GetValue<string>("RecipeName");
                    var newDescription = result.Parameters.GetValue<string>("Description");
                    var oldName = SelectedRecipePool.Name;
                    await _recipePoolService.RenameRecipePoolAsync(oldName, newName, newDescription);
                    await LoadPoolsAsync();
                    //if (oldName == CurrentPoolName)
                    //    CurrentPoolName = newName;
                    StatusMessage = $"配方池 '{newName}' 已更新";
                }
            });
        }

        private async Task DeletePoolAsync()
        {
            if (SelectedRecipePool == null) return;
            if (SelectedRecipePool.Name == _recipePoolService.CurrentPoolId)
            {
                _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                {
                    { "title", "无法删除" },
                    { "message", $"配方池 '{SelectedRecipePool.Name}' 当前正在使用中，无法删除。" },
                    { "icon", PackIconKind.Warning }
                }, _ => { });
                return;
            }
            _dialogService.ShowDialog("AlertDialog", new DialogParameters
            {
                { "title", "删除配方池" },
                { "message", $"确定要删除配方池 '{SelectedRecipePool.Name}' 及其所有配方吗？此操作不可恢复。" }
            }, async result =>
            {
                if (result.Result != ButtonResult.Yes) return;
                bool success = await _recipePoolService.DeleteRecipePoolAsync(SelectedRecipePool.Name);
                if (success)
                    await LoadPoolsAsync();
            });
        }

        private async Task SwitchRecipeInPoolAsync(RecipePool pool, bool saveCurrentPool = false)
        {
            if (pool == null) return;

            // 先弹出确认框，用户确认后再执行切换
            var confirmed = await ShowGlobalConfirmAsync(
                "确认切换配方池",
                $"确定要切换到配方池 '{pool.Name}' 吗？\n当前系统将加载该配方池的配方数据。",
                "DatabaseSyncOutline", "#2196F3");
            if (!confirmed)
            {
                StatusMessage = "用户取消切换配方池";
                return;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"正在切换到配方池 '{pool.Name}'...";

                await _recipePoolService.SwitchToPoolAsync(pool.Name, saveCurrentPool);

                await LoadRecipesAsync();
                await LoadPoolsAsync();

                StatusMessage = $"已切换到配方池 '{pool.Name}'";
                RaisePropertyChanged(nameof(RecipePools));

                ShowGlobalNotification("切换配方池", $"已切换到配方池 '{pool.Name}'", "CheckCircle", "#4CAF50");
            }
            catch (Exception ex)
            {
                StatusMessage = $"切换配方池失败: {ex.Message}";
                _logger.Error(ex, "切换配方池失败");
            }
            finally { IsLoading = false; }
        }

        private async void OnStationParameterSaved(string stationIdentifier)
        {
            // 从文件重新加载当前选中的配方对象（获取最新参数）
            if (SelectedRecipePool != null && SelectedRecipe != null)
            {
                string poolName = SelectedRecipePool.Name;
                string recipeName = SelectedRecipe.Name;

                // 从存储中加载最新的配方
                var freshRecipe = await _recipeStorage.LoadRecipeAsync(poolName, recipeName);
                if (freshRecipe != null)
                {
                    // 替换内存中的旧配方对象
                    SelectedRecipe = freshRecipe;
                    // 已持有最新配方对象，避免 LoadStationParametersForRecipe 再次读盘
                    await LoadStationParametersForRecipe(freshRecipe, reloadFromStorage: false);
                }
            }
        }
        #endregion

        #region 工站参数相关
        /// <summary>
        /// 加载指定配方的工站参数，过滤掉 Positions 和 GlobalVariables
        /// </summary>
        /// <param name="recipe">配方对象</param>
        /// <param name="reloadFromStorage">为 false 时直接使用传入的 recipe，不再重复读盘</param>
        private async Task LoadStationParametersForRecipe(RecipeInfo recipe, bool reloadFromStorage = true)
        {
            if (recipe == null) { StationParameters.Clear(); return; }
            try
            {
                IsLoading = true;
                string poolId = SelectedRecipePool?.Name ?? "Default";
                var fullRecipe = reloadFromStorage
                    ? await _recipeStorage.LoadRecipeAsync(poolId, recipe.Name)
                    : recipe;
                if (fullRecipe == null) { StationParameters.Clear(); return; }

                StationParameters.Clear();
                foreach (var kvp in fullRecipe.Parameters)
                {
                    string stationId = kvp.Key;
                    object paramObj = kvp.Value;
                    if (paramObj == null) continue;

                    var group = new StationParameterGroup { StationIdentifier = stationId };

                    // 获取对应工站的参数对象，用于提取特性元数据
                    var stationTask = _stationRegistry.GetStation(stationId);

                    // 构建元数据映射，同时获取忽略特性
                    var ignoreSet = new HashSet<string>();
                    if (stationTask?.CurrentParameters != null)
                    {
                        var type = stationTask.CurrentParameters.GetType();
                        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                        {
                            if (prop.GetCustomAttribute<ParameterIgnoreAttribute>() != null)
                                ignoreSet.Add(prop.Name);
                        }
                    }

                    // 构建属性名 → (DisplayName, Description) 映射
                    Dictionary<string, (string display, string desc)> metaMap = null;
                    if (stationTask?.CurrentParameters != null)
                    {
                        var type = stationTask.CurrentParameters.GetType();
                        metaMap = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                            .Where(p => p.GetIndexParameters().Length == 0 && p.Name != "IsValid" && p.Name != "HasErrors")
                            .ToDictionary(
                                p => p.Name,
                                p => (
                                    display: p.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? p.Name,
                                    desc: p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty
                                ));
                    }

                    if (paramObj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in jsonElement.EnumerateObject())
                        {
                            var name = property.Name;
                            // 过滤 Positions 和 GlobalVariables，这些在专用 Tab 中编辑
                            if (name == "Positions" || name == "GlobalVariables") continue;
                            // 跳过嵌套对象和数组，只显示简单类型的参数值
                            if (property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array) continue;
                            if (ignoreSet.Contains(name)) continue;
                            string display = name;
                            string desc = string.Empty;
                            if (metaMap != null && metaMap.TryGetValue(name, out var meta))
                            {
                                display = meta.display;
                                desc = meta.desc;
                            }
                            group.Properties.Add(new ParameterProperty
                            {
                                Name = name,
                                DisplayName = display,
                                Description = desc,
                                Value = GetJsonElementValue(property.Value),
                                TypeName = property.Value.ValueKind.ToString()
                            });
                        }
                    }
                    else
                    {
                        var properties = paramObj.GetType()
                            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        foreach (var prop in properties)
                        {
                            if (prop.GetIndexParameters().Length > 0 || prop.Name == "IsValid" || prop.Name == "HasErrors")
                                continue;
                            if (prop.GetCustomAttribute<ParameterIgnoreAttribute>() != null)
                                continue;
                            // 过滤 Positions 和 GlobalVariables
                            if (prop.Name == "Positions" || prop.Name == "GlobalVariables") continue;

                            var displayAttr = prop.GetCustomAttribute<DisplayNameAttribute>();
                            string display = displayAttr?.DisplayName ?? prop.Name;

                            var descAttr = prop.GetCustomAttribute<DescriptionAttribute>();
                            string description = descAttr?.Description ?? string.Empty;

                            try
                            {
                                object value = prop.GetValue(paramObj);
                                group.Properties.Add(new ParameterProperty
                                {
                                    Name = prop.Name,
                                    DisplayName = display,
                                    Description = description,
                                    Value = value,
                                    TypeName = prop.PropertyType.Name
                                });
                            }
                            catch { }
                        }
                    }

                    // 为工站分组添加编辑命令
                    var provider = _stationRegistry.GetStation(stationId);
                    group.EditCommand = new DelegateCommand(() => OpenStationParameterEditor(stationId, provider));

                    StationParameters.Add(group);
                }
                StatusMessage = $"已加载 {StationParameters.Count} 个工站的参数";
            }
            catch (Exception ex) { _logger.Error($"加载工站参数失败: {ex.Message}"); }
            finally { IsLoading = false; }
        }

        /// <summary>
        /// 打开工站参数编辑界面
        /// </summary>
        private void OpenStationParameterEditor(string stationId, IStationParameterProvider provider)
        {
            try
            {
                if (provider?.CurrentParameters is TaskParametersBase parameters)
                {
                    var editable = new StationParameterEditable(stationId, parameters);
                    _ = _parameterEditor.EditParameters(editable, async (updatedParams) =>
                    {
                        // 将编辑后的参数同步保存到配方文件
                        try
                        {
                            string poolName = provider.CurrentPoolName ?? _recipePoolService.CurrentPoolName;
                            string recipeName = provider.CurrentRecipeName ?? _recipePoolService.CurrentRecipeName;
                            await _recipePoolService.SaveStationParametersAsync(poolName, recipeName, stationId, updatedParams);
                            _logger.Info($"工站 '{stationId}' 参数已同步保存到配方文件");
                        }
                        catch (Exception saveEx)
                        {
                            _logger.Error($"工站 '{stationId}' 参数保存到配方文件失败: {saveEx.Message}");
                        }

                        _eventAggregator.GetEvent<StationParameterSavedEvent>().Publish(stationId);
                    });
                }
                else
                {
                    _dialogService.ShowDialog("NotificationDialog", new DialogParameters
                    {
                        { "title", "提示" },
                        { "message", $"工站 '{stationId}' 暂不支持参数编辑" },
                        { "icon", MaterialDesignThemes.Wpf.PackIconKind.Information }
                    }, _ => { });
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"打开工站参数编辑器失败: {ex.Message}");
            }
        }

        private object GetJsonElementValue(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt32(out int i) ? i : element.TryGetInt64(out long l) ? l : element.TryGetDouble(out double d) ? d : element.GetRawText(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };

        private void ExecuteRefreshStationParameters()
        {
            if (SelectedRecipe != null)
                _ = LoadStationParametersForRecipe(SelectedRecipe);
            else
                StatusMessage = "请先选择一个配方";
        }
        #endregion

        #region 树形视图与三点菜单辅助
        private void OnNodeSelected(TreeNode node)
        {
            if (node == null) return;
            SelectedNode = node;   // 关键

            if (node.Type == NodeType.Pool)
                _ = LoadRecipesForPoolNodeAsync(node);
            else if (node.Type == NodeType.Recipe)
            {
                var parentNode = TreeNodes.FirstOrDefault(n => n.Children.Contains(node));
                if (parentNode?.Data is RecipePool pool)
                    SelectedRecipePool = pool;
                SelectedRecipe = node.Data as RecipeInfo;
            }
        }

        private async Task LoadRecipesForPoolNodeAsync(TreeNode poolNode)
        {
            if (poolNode?.Data is not RecipePool pool) return;
            try
            {
                poolNode.Children.Clear();
                var recipes = await _recipeStorage.LoadAllRecipesAsync(pool.Name);
                foreach (var r in recipes)
                {
                    poolNode.Children.Add(new TreeNode
                    {
                        Name = r.Name,
                        Icon = "FileDocumentOutline",
                        Data = r,
                        Type = NodeType.Recipe,
                        IsCurrent = r.Name == pool.CurrentRecipeName && pool.Name == CurrentPoolName,
                        Description = r.Description,           
                        ModifiedTime = r.ModifiedTime
                    });
                }
                SelectedRecipePool = pool;
                Recipes = new ObservableCollection<RecipeInfo>(recipes);
            }
            catch (Exception ex) { _logger.Error($"加载池 '{pool.Name}' 的配方失败: {ex.Message}"); }
        }

        private void UpdateTreeCurrentState()
        {
            foreach (var poolNode in TreeNodes)
            {
                bool poolCurrent = poolNode.Data is RecipePool p && p.Name == CurrentPoolName;
                poolNode.IsCurrent = poolCurrent;
                foreach (var recipeNode in poolNode.Children)
                    if (recipeNode.Data is RecipeInfo r)
                        recipeNode.IsCurrent = poolCurrent && r.Name == CurrentRecipeName;
            }
        }

        private async Task RefreshRecipeForNodeAsync(TreeNode node)
        {
            if (node?.Data is RecipePool pool)
            {
                var poolNode = TreeNodes.FirstOrDefault(n => n.Data == pool);
                if (poolNode != null)
                    await LoadRecipesForPoolNodeAsync(poolNode);
            }
        }

        private async Task SwitchPoolForNodeAsync(TreeNode node)
        {
            if (node?.Data is RecipePool pool)
                await SwitchRecipeInPoolAsync(pool);
        }

        private async Task SaveRecipeForNodeAsync(TreeNode node)
        {
            if (SelectedRecipe != null && SelectedRecipePool != null)
                await SaveCurrentRecipeParametersAsync();
            else
                StatusMessage = "请先选择一个配方";
        }

        private void OnRecipeSelected() => StatusMessage = SelectedRecipe != null ? $"已选择配方: {SelectedRecipe.Name}" : "";
        #endregion

        #region 辅助类
        /// <summary>
        /// 使用MaterialDesign DialogHost显示全局确认弹窗
        /// </summary>
        private async Task<bool> ShowGlobalConfirmAsync(string title, string message,
            string iconName, string iconColor = "#FF9800")
        {
            var tcs = new TaskCompletionSource<bool>();

            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialogView = new Views.RecipeConfirmDialogView();
                if (dialogView.DataContext is RecipeConfirmDialogViewModel vm)
                {
                    vm.Initialize(title, message, iconName, iconColor);

                    DialogHost.Show(dialogView, "MainDialogHost", new DialogClosingEventHandler((sender, args) =>
                    {
                        tcs.SetResult(args.Parameter is bool confirmed && confirmed);
                    }));
                }
            });

            return await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// 使用MaterialDesign DialogHost显示全局通知弹窗（自动关闭）
        /// </summary>
        private void ShowGlobalNotification(string title, string message,
            string iconName, string iconColor = "#2196F3")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var dialogView = new Views.RecipeConfirmDialogView();
                if (dialogView.DataContext is RecipeConfirmDialogViewModel vm)
                {
                    vm.Initialize(title, message, iconName, iconColor);
                }

                DialogHost.Show(dialogView, "MainDialogHost", new DialogClosingEventHandler((sender, args) =>
                {
                }));
            });
        }

        /// <summary>
        /// 将IStationParameterProvider适配为IParameterEditable，用于参数编辑器
        /// </summary>
        private class StationParameterEditable : IParameterEditable
        {
            private readonly TaskParametersBase _parameters;

            public StationParameterEditable(string stationId, TaskParametersBase parameters)
            {
                Identifier = stationId;
                _parameters = parameters;
            }

            public string EditTitle => $"{Identifier} - 参数编辑";
            public string Identifier { get; }
            public object Parameters => _parameters;
        }

        public class StationParameterGroup
        {
            public string StationIdentifier { get; set; }
            public ObservableCollection<ParameterProperty> Properties { get; set; } = new();
            public ICommand EditCommand { get; set; }
        }

        public class ParameterProperty
        {
            public string Name { get; set; }
            public object Value { get; set; }
            public string TypeName { get; set; }
            public string DisplayName { get; set; }   
            public string Description { get; set; }   
        }
        #endregion
    }
}
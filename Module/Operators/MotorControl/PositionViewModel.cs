using Interfaces.Mvvm;
using ModuleCore.Views;
using OpenCvSharp;
using Framework.Views;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using SmarterMotion;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using ModuleCore.Models;
using ModuleCore.Common.Authority;
using System.CodeDom.Compiler;
using Interfaces.Events;
using Core.Abstraction;
using Core.Utilities;
using Core.Models;
using System.Text.Json;
using OpenCvSharp.Aruco;
using Recipe.Interfaces;
using Recipe.Models;
using Recipe.Events;
using Core.Abstractions.IConfiguration;

namespace Framework.ViewModels
{
    public class PositionViewModel : BindableBase
    {
        private readonly ILoggerService _logger;
        private readonly IConfigurationService _configService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDialogService _dialogService;
        private readonly IRecipeManager _recipeManager;
        private readonly IRecipeStorage _recipeStorage;
        private IAppConfig _appConfig;
        private LoginModel _loginModel { get; set; }
        private SubscriptionToken _refreshToken;
        private SubscriptionToken _recipeChangedToken;

        private string _recipeName = "未选择配方";
        public string RecipeName
        {
            get => _recipeName;
            set => SetProperty(ref _recipeName, value);
        }


        public PositionViewModel(
            ILoggerService logger,
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IConfigurationService configService,
            LoginModel loginModel,
            IRecipeManager recipeManager,
            IRecipeStorage recipeStorage,
            IAppConfig appConfig)
        {
            _logger = logger;
            _eventAggregator = eventAggregator;
            _dialogService = dialogService;
            _loginModel = loginModel;
            _configService = configService;
            _recipeManager = recipeManager;
            _recipeStorage = recipeStorage;
            _appConfig = appConfig;
            // 订阅刷新事件
            _refreshToken = _eventAggregator
                .GetEvent<PositionsNeedRefreshEvent>()
                .Subscribe(OnPositionsNeedRefresh);

            // 订阅配方改变事件
            _recipeChangedToken = _eventAggregator
                .GetEvent<RecipeChangedEvent>()
                .Subscribe(OnRecipeChanged);

            DeleteCommand = new DelegateCommand(
                 executeMethod: () => ExecuteDelete(),
                 canExecuteMethod: () => CanDelete() && SelectedPosition != null
            );

            TeachCommand = new DelegateCommand(
                 executeMethod: () => ExecuteTeach(),
                 canExecuteMethod: () => CanTeach() && SelectedPosition != null
            );

            // 当 SelectedPosition 变化时，自动触发 Command.CanExecute 重新计算
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedPosition))
                {
                    DeleteCommand.RaiseCanExecuteChanged();
                    TeachCommand.RaiseCanExecuteChanged();
                    IsEditable = !_loginModel.HasPermission(Authority.Administrator);
                }
            };
        }
        // 更新配方名称的方法
        private void UpdateRecipeName()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_appConfig != null && !string.IsNullOrEmpty(_appConfig.Name))
                {
                    RecipeName = "当前配方: " + _appConfig.Name;
                }
                else
                {
                    RecipeName = "未选择配方";
                }
            });
        }
        private void OnPositionsNeedRefresh()
        {
            // 同步刷新
            if (taskId > 0 && m_AxisIdGroup?.Length > 0)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        _ = LoadPositionsAsync();
                    }
                    catch (Exception ex)
                    {
                        ShowError($"刷新失败: {ex.Message}");
                    }
                });
            }
        }

        private void OnRecipeChanged(string newRecipeName)
        {
            // 当配方改变时，重新加载位置数据
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (taskId > 0 && m_AxisIdGroup?.Length > 0)
                    {
                        _ = LoadPositionsAsync();
                        _logger.Info($"配方切换为 '{newRecipeName}'，位置数据已重新加载");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"配方切换时加载位置数据失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 获取当前有效配方 - 同步版本
        /// </summary>
        private string GetCurrentValidRecipe()
        {
            try
            {
                // 方法1: 从配方管理器获取当前配方
                var currentRecipePool = GetRecipePool("Default");
                if (currentRecipePool?.Recipes?.Count > 0)
                {
                    var currentRecipe = currentRecipePool.Recipes.FirstOrDefault();
                    if (currentRecipe != null && IsRecipeValid(currentRecipe))
                    {
                        return currentRecipe.Name;
                    }
                }

                // 方法2: 使用默认配方
                return "DefaultRecipe";
            }
            catch (Exception ex)
            {
                _logger.Warn($"获取当前配方失败，使用默认配方: {ex.Message}");
                return "DefaultRecipe";
            }
        }

        /// <summary>
        /// 同步获取配方池
        /// </summary>
        private RecipePool GetRecipePool(string poolId)
        {
            try
            {
                return _recipeManager.GetRecipePool(poolId);
            }
            catch (Exception ex)
            {
                _logger.Warn($"获取配方池失败: {ex.Message}");
                return null;
            }
        }

        // 验证配方有效性
        private bool IsRecipeValid(Recipe.Models.RecipeInfo recipe)
        {
            if (recipe == null) return false;

            // 检查配方基本信息
            if (string.IsNullOrWhiteSpace(recipe.Name) ||
                string.IsNullOrWhiteSpace(recipe.Id))
            {
                return false;
            }

            // 检查配方参数是否包含必要的位置数据配置
            var positionConfig = recipe.GetParameter<PositionRecipeConfig>("PositionConfig");
            if (positionConfig != null)
            {
                return positionConfig.IsValid();
            }

            return true;
        }

        // 配方位置配置类
        public class PositionRecipeConfig
        {
            public string[] SupportedTasks { get; set; } = Array.Empty<string>();
            public string CoordinateSystem { get; set; } = "Cartesian";
            public double MaxVelocity { get; set; } = 100.0;
            public bool AllowNegativeCoordinates { get; set; } = true;

            public bool IsValid()
            {
                return !string.IsNullOrWhiteSpace(CoordinateSystem) &&
                       MaxVelocity > 0 && MaxVelocity <= 1000;
            }
        }

        private int[] m_AxisIdGroup;
        private int taskId;
        public event Action<string, XPosition> OnAdded;

        public int TaskId
        {
            get => taskId;
            set
            {
                if (SetProperty(ref taskId, value))
                {
                    if (m_AxisIdGroup != null && m_AxisIdGroup.Length > 0)
                        _ = LoadPositionsAsync();
                }
            }
        }

        public int[] AxisIdGroup
        {
            get => m_AxisIdGroup;
            set
            {
                if (SetProperty(ref m_AxisIdGroup, value))
                {
                    if (taskId > 0 && m_AxisIdGroup != null)
                        _ = LoadPositionsAsync();
                }
            }
        }

        private string m_CurrentPointName;
        public string CurrentPointName
        {
            get => m_CurrentPointName;
            set => SetProperty(ref m_CurrentPointName, value);
        }

        private string m_CurrentAxisName;
        public string CurrentAxisName
        {
            get => m_CurrentAxisName;
            set => SetProperty(ref m_CurrentAxisName, value);
        }

        private void ShowError(string obj)
        {
            _eventAggregator.GetEvent<MessageEvent>().Publish(new()
            {
                Target = "errLog",
                Content = obj
            });
        }

        private bool _canEditConfig;
        public bool IsEditable
        {
            get => _canEditConfig;
            private set => SetProperty(ref _canEditConfig, value);
        }

        // 命令定义
        private DelegateCommand _AddCommand;
        public DelegateCommand AddCommand =>
            _AddCommand ??= new DelegateCommand(ExecuteAdd, CanAdd)
                .ObservesProperty(() => _loginModel.LoginUser.Authority);

        private bool CanAdd() =>
            _loginModel.LoginUser?.Authority >= Authority.Administrator;

        private DelegateCommand _DeleteCommand;
        public DelegateCommand DeleteCommand { get; }

        private bool CanDelete() =>
            _loginModel.LoginUser?.Authority >= Authority.Administrator
            && SelectedPosition != null;

        private DelegateCommand _TeachCommand;
        public DelegateCommand TeachCommand { get; }

        private bool CanTeach() =>
            _loginModel.LoginUser?.Authority >= Authority.Administrator
            && SelectedPosition != null;

        private DelegateCommand _CancelCommand;
        public DelegateCommand CancelCommand =>
             _CancelCommand ??= new DelegateCommand(() => ExecuteCancelTeach());

        private DelegateCommand _StopCommand;
        public DelegateCommand StopCommand =>
             _StopCommand ??= new DelegateCommand(() => ExecuteStop());

        private DelegateCommand _StartCommand;
        public DelegateCommand StartCommand =>
            _StartCommand ??= new DelegateCommand(ExecuteStart, CanOperate)
                .ObservesProperty(() => _loginModel.LoginUser.Authority);

        private bool CanOperate() =>
            _loginModel.LoginUser?.Authority >= Authority.Administrator;

        private DelegateCommand _SaveCommand;
        public DelegateCommand SaveCommand =>
             _SaveCommand ??= new DelegateCommand(() => ExecuteSave(), CanSave)
             .ObservesProperty(() => _loginModel.LoginUser.Authority);

        private bool CanSave() =>
            _loginModel.LoginUser?.Authority >= Authority.Administrator;

        // 动态列头（轴名称）
        public ObservableCollection<string> AxisHeaders { get; set; } = new ObservableCollection<string>();

        // 数据集合
        private ObservableCollection<PositionDisplayItem> _positions = new ObservableCollection<PositionDisplayItem>();
        public ObservableCollection<PositionDisplayItem> Positions
        {
            get => _positions;
            set => SetProperty(ref _positions, value);
        }

        private int _SelectedPositionIndex = -1;
        public int SelectedPositionIndex
        {
            get => _SelectedPositionIndex;
            set => SetProperty(ref _SelectedPositionIndex, value);
        }

        // 当前选中项
        private PositionDisplayItem _selectedPosition;
        public PositionDisplayItem SelectedPosition
        {
            get => _selectedPosition;
            set => SetProperty(ref _selectedPosition, value);
        }

        // 速度选项
        public ObservableCollection<double> VelocityOptions { get; } = new ObservableCollection<double> { 1, 2, 5, 10, 20, 30, 40, 50 };

        // 当前速度
        private double _selectedVelocity = 10;
        public double SelectedVelocity
        {
            get => _selectedVelocity;
            set => SetProperty(ref _selectedVelocity, value);
        }

        private int m_MoveMode = 0;
        public int MoveMode
        {
            get => m_MoveMode;
            set => SetProperty(ref m_MoveMode, value);
        }

        private void ExecuteAdd()
        {
            if (!CanAdd())
            {
                ShowError("操作被拒绝：需要管理员权限");
                return;
            }

            _dialogService.ShowDialog("AddPositionDialog", new DialogParameters(), result =>
            {
                if (result.Result != ButtonResult.OK) return;

                var name = result.Parameters.GetValue<string>("name");
                var comment = result.Parameters.GetValue<string>("comment");

                // 1. 验证输入有效性
                if (string.IsNullOrWhiteSpace(name))
                {
                    System.Windows.MessageBox.Show("名称不能为空！", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 2. 检查是否存在重复点
                var existingPosition = Positions.FirstOrDefault(p => p.Name == name);
                if (existingPosition != null)
                {
                    System.Windows.MessageBox.Show($"已存在点 {name}！", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 3. 创建新位置对象
                double[] positions = new double[m_AxisIdGroup.Length];

                // 4. 添加到数据集合
                try
                {
                    Positions.Add(new PositionDisplayItem
                    {
                        Name = name,
                        Comment = comment,
                        Positions = positions
                    });

                    // 5. 触发事件（如果需要）
                    var newPosition = new XPosition(m_AxisIdGroup, positions, m_AxisIdGroup.Length)
                    {
                        Name = comment
                    };
                    OnAdded?.Invoke(name, newPosition);
                }
                catch (Exception ex)
                {
                    ShowError($"添加失败：{ex.Message}");
                }
            });
        }

        private void ExecuteDelete()
        {
            if (!CanDelete())
            {
                ShowError("操作被拒绝：需要管理员权限");
                return;
            }
            if (SelectedPosition != null)
            {
                Positions.Remove(SelectedPosition);
            }
        }

        private void ExecuteTeach()
        {
            if (!CanTeach())
            {
                ShowError("操作被拒绝：需要管理员权限");
                return;
            }
            _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters{
                { "title", "示教确认" },
                { "message", $"确认将当前位置示教到点 [{SelectedPosition.Name}]？" }
            }, result =>
            {
                if (result.Result == ButtonResult.Yes)
                {
                    try
                    {
                        // 获取当前轴位置，考虑 IsFeedback 条件
                        double[] currentPositions = m_AxisIdGroup
                            .Select(axisId =>
                            {
                                var axis = XDevice.Instance.FindAxisById(axisId);
                                return axis.IsFeedback ? axis.POS : axis.CommandPOS;
                            })
                            .ToArray();

                        // 更新选中点的位置数据
                        SelectedPosition.Positions = currentPositions;

                        // 记录示教信息
                        _logger.Info($"点位 [{SelectedPosition.Name}] 示教完成: {string.Join(", ", currentPositions.Select(p => p.ToString("F3")))}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"示教失败: {ex.Message}");
                        ShowError($"示教失败: {ex.Message}");
                    }
                }
            });
        }

        private void ExecuteCancelTeach()
        {
            if (SelectedPosition != null)
            {
                _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters{
                { "title", "撤销确认" },
                { "message", $"撤销示教点？" }
            }, result =>
            {
                if (result.Result == ButtonResult.Yes)
                {
                    // 直接从 JSON 重新加载
                    _ = LoadPositionsAsync();
                }
            });
            }
        }

        private void ExecuteStop()
        {
            XController.Instance._MoveStop();
        }

        private void ExecuteStart()
        {
            if (!CanOperate())
            {
                ShowError("操作被拒绝：需要管理员权限");
                return;
            }
            if (SelectedPosition == null) return;

            // 直接从界面数据创建 XPosition
            var position = new XPosition(m_AxisIdGroup, SelectedPosition.Positions, m_AxisIdGroup.Length);

            for (int i = 0; i < position.AxisId.Length; i++)
            {
                if (XDevice.Instance.FindAxisById(position.AxisId[i]).IsHomeOk == false)
                {
                    if (position.AxisId[i] == 7) continue;
                    ShowError($"未初始化轴：" + XDevice.Instance.FindAxisById(position.AxisId[i]).Name);
                    return;
                }
            }

            int zIndex = XTaskManager.Instance.FindTaskById(taskId).Z_PositionAxisIdIndex;
            double zSafe = XTaskManager.Instance.FindTaskById(taskId).Z_Safe;

            int zAxis = 0;
            double zTarget = 0;

            double[] posTarget_zSafe = new double[position.AxisId.Length];
            if (zIndex >= 0)
            {
                zAxis = position.AxisId[zIndex];
                zTarget = position.Positions[zIndex];

                for (int i = 0; i < position.AxisId.Length; i++)
                {
                    if (i == zIndex)
                    {
                        posTarget_zSafe[i] = zSafe;
                    }
                    else
                    {
                        posTarget_zSafe[i] = position.Positions[i];
                    }
                }
            }

            if (m_MoveMode == 0)
            {
                //单个位置移动
                _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters{
                    { "title", "保存确认" },
                    { "message",$"确认再现点 + {m_CurrentPointName} + [轴:{CurrentAxisName }]？" }
                }, result =>
                {
                    if (result.Result == ButtonResult.Yes)
                    {
                        Task.Factory.StartNew(new Action(() =>
                        {
                            if (zIndex >= 0)
                            {
                                XController.Instance._MoveAbs(zAxis, zSafe, _selectedVelocity, false);
                                if (!XController.Instance._WaitMoveDone())
                                    return;
                            }
                            if (_SelectedPositionIndex == 0) return;
                            XController.Instance._MoveAbs(position.AxisId[_SelectedPositionIndex - 1],
                            position.Positions[_SelectedPositionIndex - 1], _selectedVelocity);
                            if (!XController.Instance._WaitMoveDone())
                                return;
                        }));
                    }
                });
            }
            else if (m_MoveMode == 1)
            {
                //多个位置移动
                _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters{
                    { "title", "保存确认" },
                    { "message",$"确认再现所有轴点[{m_CurrentPointName}]？" }
                }, result =>
                {
                    if (result.Result == ButtonResult.Yes)
                    {
                        Task.Factory.StartNew(new Action(() =>
                        {
                            if (zIndex >= 0)
                            {
                                XController.Instance._MoveAbs(zAxis, zSafe, _selectedVelocity, false);
                                if (!XController.Instance._WaitMoveDone())
                                    return;

                                XController.Instance._MoveAbs(position.AxisId, posTarget_zSafe, _selectedVelocity, false);
                                if (!XController.Instance._WaitMoveDone())
                                    return;

                                XController.Instance._MoveAbs(zAxis, zTarget, _selectedVelocity, false);
                                if (!XController.Instance._WaitMoveDone())
                                    return;
                            }
                            else
                            {
                                XController.Instance._MovePosition(position, _selectedVelocity);
                                if (!XController.Instance._WaitMoveDone())
                                    return;
                            }
                        }));
                    }
                });
            }
        }

        private bool _columnsGenerated = false;
        public event Action LoadCompleted;
        // 添加一个触发列生成的方法
        public void NotifyColumnsChanged()
        {
            RaisePropertyChanged(nameof(AxisHeaders));
            _columnsGenerated = true;
        }
        /// <summary>
        /// 异步加载位置数据 - 避免UI卡顿
        /// </summary>
        public async Task LoadPositionsAsync()
        {
            try
            {
                // 显示加载状态
                IsLoading = true;

                // 在后台线程获取数据
                var (positionData, currentRecipe) = await Task.Run(() =>
                {
                    if (taskId == 0) return (null, null);

                    // 获取当前有效配方
                    var recipe = GetCurrentValidRecipe();
                    if (string.IsNullOrEmpty(recipe))
                    {
                        _logger.Warn("无法确定当前有效配方，使用默认位置数据");
                        recipe = "Default";
                    }

                    // 构建基于配方的数据键
                    var positionDataKey = $"Position_Task_{taskId}_Recipe_{recipe}";

                    // 从 JSON 加载位置数据
                    var data = _configService.LoadConfiguration<PositionData>(positionDataKey);

                    if (data == null || data.AxisIds.Length == 0)
                    {
                        // 如果基于配方的数据不存在，尝试加载默认数据
                        data = _configService.LoadConfiguration<PositionData>($"Position_Task_{taskId}");

                        if (data == null)
                        {
                            // 如果都不存在，创建默认数据
                            data = new PositionData
                            {
                                AxisIds = m_AxisIdGroup,
                                Positions = new Dictionary<string, PositionInfo>(),
                                RecipeName = recipe
                            };
                        }
                        else
                        {
                            // 将默认数据迁移到当前配方
                            data.RecipeName = recipe;
                            _configService.SaveConfiguration(positionDataKey, "json", data);
                            _logger.Info($"已将默认位置数据迁移到配方 '{recipe}'");
                        }
                    }

                    return (data, recipe);
                });

                if (positionData == null) return;

                // 使用 Dispatcher.Invoke 在UI线程上更新集合
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        // 批量更新 - 暂停UI通知
                        Positions.Clear();
                        AxisHeaders.Clear();

                        // 生成列头
                        foreach (var axisId in m_AxisIdGroup)
                        {
                            AxisHeaders.Add(XDevice.Instance.FindAxisById(axisId).Name);
                        }

                        // 批量添加数据到界面
                        foreach (var kvp in positionData.Positions)
                        {
                            Positions.Add(new PositionDisplayItem
                            {
                                Name = kvp.Key,
                                Positions = kvp.Value.Coordinates,
                                Comment = kvp.Value.Comment
                            });
                        }
                        // 触发加载完成事件
                        LoadCompleted?.Invoke();

                        // 触发列生成
                        NotifyColumnsChanged();

                        _logger.Info($"从配方 '{currentRecipe}' 加载了 {Positions.Count} 个位置点");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"更新UI数据失败: {ex.Message}");
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"异步加载位置数据失败: {ex.Message}");
                ShowError($"加载失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // 添加加载状态属性
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        // 验证位置数据与配方的兼容性
        private bool IsPositionDataCompatible(PositionData positionData, string currentRecipe)
        {
            if (positionData == null) return false;

            // 检查配方名称是否匹配
            if (!string.IsNullOrEmpty(positionData.RecipeName) &&
                positionData.RecipeName != currentRecipe)
            {
                _logger.Warn($"位置数据是为配方 '{positionData.RecipeName}' 创建的，当前配方是 '{currentRecipe}'");
            }

            // 检查轴配置是否匹配
            if (positionData.AxisIds != null && m_AxisIdGroup != null)
            {
                if (positionData.AxisIds.Length != m_AxisIdGroup.Length)
                {
                    _logger.Error($"轴数量不匹配: 数据中有 {positionData.AxisIds.Length} 个轴，当前配置有 {m_AxisIdGroup.Length} 个轴");
                    return false;
                }

                // 检查轴ID是否一致
                for (int i = 0; i < positionData.AxisIds.Length; i++)
                {
                    if (positionData.AxisIds[i] != m_AxisIdGroup[i])
                    {
                        _logger.Error($"轴ID不匹配: 位置 {i} - 数据: {positionData.AxisIds[i]}, 当前: {m_AxisIdGroup[i]}");
                        return false;
                    }
                }
            }

            return true;
        }

        private void SavePoints()
        {
            try
            {
                // 获取当前有效配方
                var currentRecipe = GetCurrentValidRecipe();
                if (string.IsNullOrEmpty(currentRecipe))
                {
                    throw new InvalidOperationException("无法确定当前有效配方，无法保存位置数据");
                }

                // 验证数据完整性
                string[] names = Positions.Select(p =>
                {
                    if (string.IsNullOrWhiteSpace(p.Name))
                    {
                        throw new InvalidDataException($"发现未命名的点位（行号：{Positions.IndexOf(p) + 1}）");
                    }
                    return p.Name.Trim();
                }).ToArray();

                // 检查重复名称
                var duplicateNames = names.GroupBy(x => x)
                                         .Where(g => g.Count() > 1)
                                         .Select(g => g.Key)
                                         .ToList();
                if (duplicateNames.Any())
                {
                    throw new InvalidDataException($"存在重复的点位名称: {string.Join(", ", duplicateNames)}");
                }

                // 验证位置数据与当前配方的兼容性
                //if (!ValidatePositionsWithRecipe(currentRecipe))
                //{
                //    throw new InvalidOperationException("位置数据与当前配方不兼容");
                //}

                // 创建位置数据对象
                var positionData = new PositionData
                {
                    AxisIds = m_AxisIdGroup,
                    Positions = new Dictionary<string, PositionInfo>(),
                    RecipeName = currentRecipe,
                    SavedTime = DateTime.UtcNow
                };

                foreach (var position in Positions)
                {
                    positionData.Positions[position.Name] = new PositionInfo
                    {
                        Coordinates = position.Positions ?? new double[m_AxisIdGroup.Length],
                        Comment = position.Comment ?? string.Empty
                    };
                }

                // 备份当前数据
                SaveBackUpPoints(currentRecipe);

                // 使用配方特定的键保存数据
                var positionDataKey = $"Position_Task_{taskId}_Recipe_{currentRecipe}";
                _configService.SaveConfiguration(positionDataKey, "json", positionData);

                // 保存当前配方信息
                _configService.SaveConfiguration("LastUsedRecipe", "json", currentRecipe);

                _logger.Info($"位置数据已保存到配方 '{currentRecipe}': Task_{taskId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"保存位置数据失败: {ex.Message}");
                throw;
            }
        }

        // 验证位置数据与配方的兼容性
        private bool ValidatePositionsWithRecipe(string currentRecipe)
        {
            try
            {
                // 获取当前配方的配置
                var recipePool = _recipeManager.GetRecipePool("Default");
                var currentRecipeObj = recipePool?.Recipes?.FirstOrDefault(r => r.Name == currentRecipe);

                if (currentRecipeObj != null)
                {
                    var positionConfig = currentRecipeObj.GetParameter<PositionRecipeConfig>("PositionConfig");
                    if (positionConfig != null)
                    {
                        // 检查任务是否支持
                        if (positionConfig.SupportedTasks.Length > 0 &&
                            !positionConfig.SupportedTasks.Contains($"Task_{taskId}"))
                        {
                            _logger.Warn($"任务 Task_{taskId} 不在配方 '{currentRecipe}' 的支持列表中");
                            return false;
                        }

                        // 检查速度限制
                        if (SelectedVelocity > positionConfig.MaxVelocity)
                        {
                            _logger.Warn($"选择的速度 {SelectedVelocity} 超过配方限制 {positionConfig.MaxVelocity}");
                            return false;
                        }

                        // 检查坐标值
                        if (!positionConfig.AllowNegativeCoordinates)
                        {
                            foreach (var position in Positions)
                            {
                                if (position.Positions.Any(p => p < 0))
                                {
                                    _logger.Warn($"配方 '{currentRecipe}' 不允许负坐标值");
                                    return false;
                                }
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.Warn($"验证位置数据与配方兼容性时出错: {ex.Message}");
                return true; // 验证失败时默认允许保存
            }
        }

        private void SaveBackUpPoints(string currentRecipe)
        {
            try
            {
                var positionDataKey = $"Position_Task_{taskId}_Recipe_{currentRecipe}";
                var positionData = _configService.LoadConfiguration<PositionData>(positionDataKey);

                if (positionData != null)
                {
                    string backupDir = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Config",
                        "BackUp",
                        "Positions",
                        currentRecipe,
                        DateTime.Now.ToString("yyyy-MM-dd")
                    );

                    if (!Directory.Exists(backupDir))
                        Directory.CreateDirectory(backupDir);

                    string backupFile = Path.Combine(
                        backupDir,
                        $"Position_Task_{taskId}_{DateTime.Now:HH-mm-ss}.json"
                    );

                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    string jsonData = JsonSerializer.Serialize(positionData, jsonOptions);
                    File.WriteAllText(backupFile, jsonData);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"备份位置数据失败: {ex.Message}");
            }
        }

        private void ExecuteSave()
        {
            if (!CanSave())
            {
                ShowError("操作被拒绝：需要管理员权限");
                return;
            }

            // 1. 立刻把耗时工作甩到后台，UI 线程保持空闲
            _ = Task.Run(() =>
            {
                // 2. 后台线程里随便等、随便算
                var currentRecipe = GetCurrentValidRecipe();
                if (string.IsNullOrEmpty(currentRecipe))
                {
                    DispatcherInvoke(() => ShowError("当前配方无效，无法保存位置数据"));
                    return;
                }

                // 3. 真正需要 UI 时再回主线程
                DispatcherInvoke(() =>
                {
                    _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters
                    {
                        { "title", "保存确认" },
                        { "message", $"确认要保存所有点位变更到当前配方吗？\n当前配方: {currentRecipe}" }
                    }, result =>
                    {
                        if (result.Result == ButtonResult.Yes)
                        {
                            // 4. 再次甩回后台执行保存
                            _ = Task.Run(() =>
                            {
                                try
                                {
                                    SavePoints();
                                    DispatcherInvoke(() =>
                                    {
                                        _eventAggregator.GetEvent<PositionSavedEvent>().Publish();
                                        ShowSuccess("位置数据保存成功");
                                    });
                                    _logger.Info($"位置数据保存成功 (配方: {currentRecipe})");
                                }
                                catch (Exception ex)
                                {
                                    DispatcherInvoke(() => ShowError($"保存失败：{ex.Message}"));
                                }
                            });
                        }
                    });
                });
            });
        }

        // 辅助：简化 Dispatcher 调用
        private void DispatcherInvoke(Action action) =>
            Application.Current.Dispatcher.Invoke(action);

        // 添加保存状态属性
        private bool _isSaving;
        public bool IsSaving
        {
            get => _isSaving;
            set => SetProperty(ref _isSaving, value);
        }

        private void ShowSuccess(string message)
        {
            _eventAggregator.GetEvent<MessageEvent>().Publish(new()
            {
                Target = "successLog",
                Content = message
            });
        }

        // 定义保存完成事件
        public class PositionSavedEvent : PubSubEvent { }

        public void Dispose()
        {
            // 显式取消订阅
            _eventAggregator.GetEvent<PositionsNeedRefreshEvent>()
                .Unsubscribe(_refreshToken);
        }
    }
}
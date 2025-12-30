using Core.Services;
using HandyControl.Controls;
using Interfaces;
using MaterialDesignThemes.Wpf;
using ModuleCore.Common.Authority;
using ModuleCore.Models;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using SmarterMotion;
using Stations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Framework
{
    public abstract class BaseMapViewModel<TIdentifier> : BindableBase
    {
        protected readonly IDialogService _dialogService;
        protected readonly RecipePool _recipePool;
        protected readonly AppConfig _appConfig;
        protected readonly TaskInstanceManager _taskManager;
        private LoginModel _loginModel { get; set; }
        // 核心数据源
        public ObservableCollection<PointViewModel> AllPoints { get; } = new();
        public ObservableCollection<PointViewModel> MapPoints { get; } = new();
        public ObservableCollection<ObservableCollection<PointViewModel>> BasePoints { get; } = new();

        // 通用命令
        public DelegateCommand GenerateBaseCommand { get; }
        public DelegateCommand AddOffsetArrayCommand { get; }
        public DelegateCommand ClearAllCommand { get; }
        public DelegateCommand InitializeBaseOffsetsCommand { get; }
        public DelegateCommand SaveRecipeCommand { get; }
        public DelegateCommand LoadRecipeCommand { get; }
        public DelegateCommand<PointViewModel> MoveToPointCommand { get; }
        public DelegateCommand<PointViewModel> SelectPointCommand { get; }
        public DelegateCommand ExportGridCommand { get; }
        public DelegateCommand ImportGridCommand { get; }

        // ...其他通用命令
        private readonly IFileService _fileService;
        private readonly ISnackbarMessageQueue _snackbarMessageQueue;
        // 添加文件过滤器常量
        private const string GridFileFilter = "JSON 文件|*.json|CSV 文件|*.csv|Excel 文件|*.xlsx";

        // ...其他通用命令

        // 权限属性（用于前端绑定）
        private bool _canEditConfig;
        public bool CanEditConfig
        {
            get => _canEditConfig;
            private set => SetProperty(ref _canEditConfig, value);
        }
        private bool _canDeletePoints;
        public bool CanDeletePoints
        {
            get => _canDeletePoints;
            private set => SetProperty(ref _canDeletePoints, value);
        }
        private bool _canMovePoints;
        public bool CanMovePoints
        {
            get => _canMovePoints;
            private set => SetProperty(ref _canMovePoints, value);
        }

        // 新增生成规则枚举
        public enum GenerationRuleType
        {
            [Description("相对偏移规则")]
            RelativeOffset,
            [Description("绝对坐标规则")]
            AbsoluteCoordinate
        }

        // 当前选择的生成规则
        private BaseMapViewModel<object>.GenerationRuleType _selectedGenerationRule;
        public BaseMapViewModel<object>.GenerationRuleType SelectedGenerationRule
        {
            get => _selectedGenerationRule;
            set => SetProperty(ref _selectedGenerationRule, value);
        }

        // 生成规则选项列表（用于 ComboBox 绑定）
        public static Array GenerationRuleValues => Enum.GetValues(typeof(GenerationRuleType));


        // 添加抽象属性标识当前处理的Pin编号（1-4）
        protected abstract int PinIndex { get; }

        protected BaseMapViewModel(
            IDialogService dialogService,
            RecipePool recipePool,
            AppConfig appConfig,
            TaskInstanceManager taskManager,
            LoginModel loginModel,
            IFileService fileService,          // 依赖注入
            ISnackbarMessageQueue snackbarQueue)
        {
            _dialogService = dialogService;
            _recipePool = recipePool;
            _appConfig = appConfig;
            _taskManager = taskManager;
            _fileService = fileService;
            _loginModel = loginModel;
            _snackbarMessageQueue = snackbarQueue;
            // 新增选择命令初始化
            SelectPointCommand = new DelegateCommand<PointViewModel>(point =>
            {
                SelectedPoint = point;
                Debug.WriteLine($"选中点：{point.Index}"); // 调试输出
            });
            // 订阅全局属性变更
            _recipePool.PropertyChanged += OnRecipePoolChanged;

            // 普通用户只能查看
            GenerateBaseCommand = new DelegateCommand(
                executeMethod: GenerateBaseArray,
                canExecuteMethod: () => CanEditConfig
            ).ObservesProperty(() => CanEditConfig);
            // 需要管理员权限的操作
            AddOffsetArrayCommand = new DelegateCommand(
                executeMethod: AddOffsetArray,
                canExecuteMethod: () => CanEditConfig
            );
            ClearAllCommand = new DelegateCommand(
                executeMethod: ClearAll,
                canExecuteMethod: () => CanDeletePoints
            ).ObservesProperty(() => _loginModel.LoginUser);

            InitializeBaseOffsetsCommand = new DelegateCommand(
                executeMethod: InitializeBaseOffsets,
                canExecuteMethod: () => CanEditConfig
            ).ObservesProperty(() => _loginModel.LoginUser);

            SaveRecipeCommand = new DelegateCommand(
                executeMethod: ExecuteSaveRecipe, 
                canExecuteMethod: () => CanEditConfig);
            LoadRecipeCommand = new DelegateCommand(
                executeMethod: ExecuteLoadRecipe, 
                canExecuteMethod: () => CanEditConfig);
            ExportGridCommand = new DelegateCommand(
                executeMethod: ExecuteExportGrid, 
                canExecuteMethod: () => CanEditConfig);
            ImportGridCommand = new DelegateCommand(
                executeMethod: ExecuteImportGrid,
                canExecuteMethod: () => CanEditConfig);
            MoveToPointCommand = new DelegateCommand<PointViewModel>(
                executeMethod: point => ExecuteWithPermission(() => MoveToPoint(point), Authority.Operator),
                canExecuteMethod: point => CanMovePoints);

            // 初始化权限状态
            UpdatePermissionStates();
            // 监听登录模型变化
            _loginModel.PropertyChanged += LoginModel_PropertyChanged;
            if (!CanEditConfig)
            {
                ExecuteLoadRecipe();
            }
        }
        private void LoginModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LoginModel.LoginUser) ||
                e.PropertyName == nameof(LoginModel.HasPermission))
            {
                UpdatePermissionStates();
            }
        }
        private void UpdatePermissionStates()
        {
            CanEditConfig = _loginModel.HasPermission(Authority.Administrator);
            CanDeletePoints = _loginModel.HasPermission(Authority.Administrator);
            CanMovePoints = _loginModel.HasPermission(Authority.Operator);

            // 强制刷新所有相关命令
            GenerateBaseCommand.RaiseCanExecuteChanged();
            AddOffsetArrayCommand.RaiseCanExecuteChanged();
            ClearAllCommand.RaiseCanExecuteChanged();
            InitializeBaseOffsetsCommand.RaiseCanExecuteChanged();
            SaveRecipeCommand.RaiseCanExecuteChanged();
            LoadRecipeCommand.RaiseCanExecuteChanged();
            ExportGridCommand.RaiseCanExecuteChanged();
            ImportGridCommand.RaiseCanExecuteChanged();
        }
        // 权限执行封装方法
        private void ExecuteWithPermission(Action action, Authority requiredRole, string operationName = "")
        {
            if (!_loginModel.HasPermission(requiredRole))
            {
                ShowMessage($"操作需要 {requiredRole} 权限");
                return;
            }
            try
            {
                // 记录操作日志
                Debug.WriteLine($"{_loginModel.LoginUser?.Name} 执行: {operationName}");
                action();
            }
            catch (Exception ex)
            {
                ShowMessage($"{operationName} 失败: {ex.Message}");
            }
        }


        // 通用方法实现
        protected virtual void ExecuteSaveRecipe()
        {
            try
            {
                // 序列化数据
                var baseJson = JsonConvert.SerializeObject(BasePoints);
                var pinJson = JsonConvert.SerializeObject(new
                {
                    Rows,
                    Columns,
                    StartX,
                    StartY,
                    GlobalOffsetX,
                    GlobalOffsetY,
                    GlobalOffsetCount,
                    Points = AllPoints
                });

                // 使用显式方法保存配置
                _recipePool.CurrentRecipe.SetPinConfig(PinIndex, true, baseJson);  // 保存基础配置
                _recipePool.CurrentRecipe.SetPinConfig(PinIndex, false, pinJson);  // 保存详细配置

                // 保存通用配置
                SaveCurrentConfig();
                ExecuteLoadRecipe(); // 加载配方，确保数据一致性
                ShowMessage($"配方保存完成");
            }
            catch (Exception ex)
            {
                ShowMessage($"配方加载失败:" + ex.Message);
            }
        }

        protected virtual void ExecuteLoadRecipe()
        {
            try
            {
                // 使用显式方法 GetPinConfig 替代索引器
                string baseJson = _recipePool.CurrentRecipe.GetPinConfig(PinIndex, true); // 获取基础配置
                string pinJson = _recipePool.CurrentRecipe.GetPinConfig(PinIndex, false); // 获取详细配置
                // 1. 加载基础配置（BasePoints）
                if (!string.IsNullOrEmpty(baseJson))
                {
                    // 反序列化逻辑...
                    var deserialized = JsonConvert.DeserializeObject<List<List<dynamic>>>(baseJson);

                    BasePoints.Clear();
                    foreach (var rowData in deserialized)
                    {
                        var newRow = new ObservableCollection<PointViewModel>();
                        foreach (var pointData in rowData)
                        {
                            newRow.Add(new PointViewModel
                            {
                                X = pointData.X,
                                Y = pointData.Y,
                                Row = pointData.Row,
                                Column = pointData.Column,
                                IsActive = pointData.IsActive ?? true, // 默认值处理
                                Order = pointData.Order ?? 0,
                                IsRotate = pointData.IsRotate ?? false,
                                XOffset = pointData.XOffset ?? 0,
                                YOffset = pointData.YOffset ?? 0,
                            });
                        }
                        BasePoints.Add(newRow);
                    }
                }
                // 2. 加载详细配置（AllPoints）
                if (!string.IsNullOrEmpty(pinJson))
                {
                    // 反序列化逻辑...
                    var json = pinJson;
                    var data = JsonConvert.DeserializeAnonymousType(json, new
                    {
                        Rows = 3,
                        Columns = 3,
                        StartX = 0.0,
                        StartY = 0.0,
                        GlobalOffsetX = 0.0,
                        GlobalOffsetY = 0.0,
                        GlobalOffsetCount = 0,
                        Points = new List<PointViewModel>()
                    });
                    // 恢复基础配置
                    Rows = data.Rows;
                    Columns = data.Columns;
                    StartX = data.StartX;
                    StartY = data.StartY;
                    GlobalOffsetX = data.GlobalOffsetX;
                    GlobalOffsetY = data.GlobalOffsetY;
                    GlobalOffsetCount = data.GlobalOffsetCount;

                    // 重建点集合
                    AllPoints.Clear();
                    foreach (var (point, index) in data.Points.Select((p, i) => (p, i)))
                    {
                        AllPoints.Add(new PointViewModel
                        {
                            X = point.X,
                            Y = point.Y,
                            Index = index,
                            IsActive = point.IsActive,
                            Order = point.Order,
                            IsRotate = point.IsRotate,
                            XOffset = point.XOffset,
                            YOffset = point.YOffset
                        });
                    }
                    UpdateCanvasSize();
                    //ShowMessage($"加载配方:{_recipePool.Name} 完成");
                }
                else
                {
                    ShowMessage($"配方:{_recipePool.CurrentRecipe.Name} 未找到");
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON解析失败: {ex.Message}");
                ShowMessage($"JSON解析失败: {ex.Message}");
                throw;
            }
        }
        // 移动逻辑实现
        protected virtual void MoveToPoint(PointViewModel point)
        {
            // 这里实现实际运动控制逻辑
            Debug.WriteLine($"移动到坐标 ({point.X}, {point.Y})");
            // 更新选中点
            SelectedPoint = point;
        }
        // 其他通用方法...

        private void OnRecipePoolChanged(object sender, PropertyChangedEventArgs e)
        {
            // 通用变更处理
             var pool = sender.GetType();
            Debug.WriteLine($"属性 {e.PropertyName} 已变更");
        }
        public void Dispose()
        {
            _loginModel.PropertyChanged -= LoginModel_PropertyChanged;
            _recipePool.PropertyChanged -= OnRecipePoolChanged;
        }
        // 可扩展的点过滤逻辑
        private bool ShouldSkipPoint(PointViewModel point)
        {
            // 跳过原点 (0,0)
            return point.X == 0 && point.Y == 0;
        }
        // 生成基础阵列(按示教方式)
        private void GenerateBaseArray()
        {
            AllPoints.Clear();

            if (SelectedGenerationRule == BaseMapViewModel<object>.GenerationRuleType.RelativeOffset)
            {
                GenerateRelativePoints();
            }
            else
            {
                GenerateAbsolutePoints();
            }
            UpdateMapPoints();
        }
        // 相对偏移生成逻辑
        private void GenerateRelativePoints()
        {
            AllPoints.Clear();
            bool isBasePointFound = false; // 是否找到基准点
            double baseX = 0;              // 基准点X坐标
            double baseY = 0;              // 基准点Y坐标

            // 第一次遍历：寻找第一个非 (0,0) 的基准点
            foreach (var row in BasePoints)
            {
                foreach (var point in row)
                {
                    if (!ShouldSkipPoint(point))
                    {
                        baseX = point.X;
                        baseY = point.Y;
                        isBasePointFound = true;
                        break;
                    }
                }
                if (isBasePointFound) break;
            }

            if (!isBasePointFound)
            {
                ShowMessage("未找到有效基准点（所有点均为原点 (0,0)）");
                return;
            }

            // 第二次遍历：生成相对偏移点
            bool isFirstValidPoint = true;
            foreach (var row in BasePoints)
            {
                foreach (var point in row)
                {
                    if (ShouldSkipPoint(point)) continue;

                    double finalX = point.X;
                    double finalY = point.Y;

                    // 第一个有效点作为基准点，后续点计算相对偏移
                    if (!isFirstValidPoint)
                    {
                        finalX += baseX;
                        finalY += baseY;
                    }
                    else
                    {
                        isFirstValidPoint = false;
                    }

                    AllPoints.Add(new PointViewModel
                    {
                        X = finalX,
                        Y = finalY,
                        Index = AllPoints.Count,
                        Row = point.Row,
                        Column = point.Column,
                        IsActive = point.IsActive,
                        Order = point.Order,
                        IsRotate = point.IsRotate,
                        XOffset = point.XOffset,
                        YOffset = point.YOffset
                    });
                }
            }
            UpdateMapPoints();
        }
        // 绝对坐标生成逻辑
        private void GenerateAbsolutePoints()
        {
            AllPoints.Clear();

            foreach (var row in BasePoints)
            {
                foreach (var point in row)
                {
                    // 跳过无效点（可配置条件）
                    if (ShouldSkipPoint(point)) continue;

                    AllPoints.Add(new PointViewModel
                    {
                        X = point.X,
                        Y = point.Y,
                        Index = AllPoints.Count,
                        Row = point.Row,
                        Column = point.Column,
                        IsActive = point.IsActive,
                        Order = point.Order,
                        IsRotate = point.IsRotate
                    });
                }
            }

            UpdateMapPoints();
        }

        // 初始化基础偏移配置
        private void InitializeBaseOffsets()
        {
            BasePoints.Clear();
            for (int i = 0; i < Rows; i++)
            {
                var row = new ObservableCollection<PointViewModel>();
                for (int j = 0; j < Columns; j++)
                {
                    row.Add(new PointViewModel());
                }
                BasePoints.Add(row);
            }
        }
        // 添加偏移阵列
        private void AddOffsetArray()
        {
            if (!AllPoints.Any() || GlobalOffsetCount <= 0) return;

            // 获取基础阵列原点（第一个阵列）
            var basePoints = AllPoints
                .Take(Rows * Columns)
                .ToList();

            // 生成多层偏移阵列
            int num = 0;
            for (int offsetIndex = 1; offsetIndex <= GlobalOffsetCount; offsetIndex++)
            {
                foreach (var basePoint in basePoints)
                {
                    // 计算累积偏移量
                    double totalOffsetX = GlobalOffsetX * offsetIndex;
                    double totalOffsetY = GlobalOffsetY * offsetIndex;

                    AllPoints.Add(new PointViewModel
                    {
                        X = basePoint.X + totalOffsetX,
                        Y = basePoint.Y + totalOffsetY,
                        Index = AllPoints.Count,
                        // 同步其他属性
                        IsActive = basePoint.IsActive,
                        Order = num++,
                        IsRotate = basePoint.IsRotate,
                    });
                }
            }
            int order = 1;
            foreach (var point in AllPoints.OrderBy(p => p.Order)) // 或者其他排序依据
            {
                point.Order = order++;
            }
            UpdateMapPoints();
        }

        private void UpdateAllPoints()
        {
            AllPoints.Clear();
            MapPoints.Clear();

            UpdateMapPoints(); // 新增
            RaisePropertyChanged(nameof(MaxIndex));
        }
        // 更新地图点集合
        private void UpdateMapPoints()
        {
            MapPoints.Clear();

            if (!AllPoints.Any()) return;

            // 计算真实坐标范围
            var minX = AllPoints.Min(p => p.X);
            var minY = AllPoints.Min(p => p.Y);
            var maxX = AllPoints.Max(p => p.X);
            var maxY = AllPoints.Max(p => p.Y);

            // 计算缩放比例
            var scaleX = (CanvasWidth - 2 * MapPadding) / (maxX - minX + MapBaseSpacing);
            var scaleY = (CanvasHeight - 2 * MapPadding) / (maxY - minY + MapBaseSpacing);
            var scale = Math.Min(scaleX, scaleY);

            // 生成地图坐标
            foreach (var point in AllPoints)
            {
                MapPoints.Add(new PointViewModel
                {
                    // 保持原始数据
                    OriginalX = point.X,
                    OriginalY = point.Y,

                    // 计算显示坐标
                    X = (point.X - minX) * scale + MapPadding,
                    Y = (point.Y - minY) * scale + MapPadding,

                    // 其他属性保持不变
                    Index = point.Index,
                    ArrayIndex = point.ArrayIndex,
                    Row = point.Row,
                    Column = point.Column
                });
            }
        }
        // 修改Canvas尺寸计算
        private void UpdateCanvasSize()
        {
            if (!AllPoints.Any())
            {
                CanvasWidth = 800;  // 默认尺寸
                CanvasHeight = 600;
                return;
            }

            // 动态计算画布尺寸
            var minX = AllPoints.Min(p => p.OriginalX);
            var minY = AllPoints.Min(p => p.OriginalY);
            var maxX = AllPoints.Max(p => p.OriginalX);
            var maxY = AllPoints.Max(p => p.OriginalY);
            // 计算基础尺寸
            var baseWidth = (maxX - minX) + 2 * MapBaseSpacing;
            var baseHeight = (maxY - minY) + 2 * MapBaseSpacing;

            // 保持宽高比
            const double baseAspectRatio = 16.0 / 9.0;
            CanvasWidth = Math.Max(800, baseWidth);
            CanvasHeight = Math.Max(600, CanvasWidth / baseAspectRatio);

            UpdateMapPoints();

        }
        private void SaveCurrentConfig()
        {
            if (string.IsNullOrEmpty(_appConfig.Name))
            {
                _appConfig.Load();
                _appConfig.Name = "未命名配方";
            }
            if (string.IsNullOrEmpty(_appConfig.LastSelectedRecipePath))
            {
                string _configPath = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Recipes",
                        _appConfig.Name + ".json");
                _recipePool.CurrentRecipe.FilePath = _configPath;// @"Recipes\" + _appConfig.Name + ".json";
            }

            //// 获取当前应用程序的启动路径
            //string basePath = $"Recipes/" + _appConfig.Name + ".json";

            _recipePool.CurrentRecipe.FilePath = _appConfig.LastSelectedRecipePath;
            _recipePool.CurrentRecipe.Name = _appConfig.Name;
            _recipePool.CurrentRecipe.SaveToFile();
            //_appConfig.SyncWithRecipePool(_recipePool.CurrentRecipe);
        }
        private void ClearAll()
        {
            UpdateAllPoints();
        }
        private void ShowMessage(string message)
        {
            _dialogService.ShowDialog("NotificationDialog", new DialogParameters
            {
                { "title", "提示" },
                { "message", message },
                { "icon", PackIconKind.AlertCircle }
            }, result =>
            {
                // 处理回调结果
                if (result.Result == ButtonResult.OK)
                {
                    // 用户点击确认后的逻辑
                }
            });
        }
        private void ExecuteExportGrid()
        {
            try
            {
                // 检查是否有数据可导出
                if (!BasePoints.Any() || !BasePoints[0].Any())
                {
                    ShowMessage("无网格数据可导出");
                    return;
                }

                // 弹出文件保存对话框
                var filePath = _fileService.SaveFileDialog(
                    "导出网格配置",
                    $"GridConfig_Pin{PinIndex}_{DateTime.Now:yyyyMMdd}",
                    GridFileFilter);

                if (string.IsNullOrEmpty(filePath)) return;

                // 构建导出数据结构
                var exportData = new
                {
                    Metadata = new
                    {
                        PinIndex,
                        CreationTime = DateTime.Now,
                        Rows = BasePoints.Count,
                        Columns = BasePoints[0].Count
                    },
                    Points = BasePoints.SelectMany((row, rowIdx) =>
                        row.Select((point, colIdx) => new
                        {
                            Row = rowIdx,
                            Column = colIdx,
                            point.X,
                            point.Y,
                            point.XOffset,
                            point.YOffset,
                            point.IsActive,
                            point.Order
                        }))
                };

                // 根据文件类型选择导出方式
                if (filePath.EndsWith(".json"))
                {
                    var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
                    File.WriteAllText(filePath, json);
                }
                else if (filePath.EndsWith(".csv"))
                {
                    var csvBuilder = new StringBuilder();
                    // 添加标题行
                    csvBuilder.AppendLine("Row,Column,X,Y,XOffset,YOffset,IsActive,Order");

                    // 添加数据行
                    foreach (var point in exportData.Points)
                    {
                        csvBuilder.AppendLine(
                            $"{point.Row},{point.Column},{point.X},{point.Y}," +
                            $"{point.XOffset},{point.YOffset},{point.IsActive},{point.Order}");
                    }

                    File.WriteAllText(filePath, csvBuilder.ToString());
                }

                _snackbarMessageQueue.Enqueue($"网格数据已成功导出到: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                ShowMessage($"导出失败: {ex.Message}");
            }
        }

        private void ExecuteImportGrid()
        {
            try
            {
                // 弹出文件选择对话框
                var filePath = _fileService.OpenFileDialog("选择网格配置文件", GridFileFilter);
                if (string.IsNullOrEmpty(filePath)) return;

                // 清除现有数据
                BasePoints.Clear();

                // 根据文件类型处理
                if (filePath.EndsWith(".json"))
                {
                    var json = File.ReadAllText(filePath);
                    dynamic data = JsonConvert.DeserializeObject(json);

                    // 验证基本结构
                    if (data?.Points == null)
                        throw new InvalidDataException("无效的JSON结构");

                    // 重建网格
                    int maxRow = 0, maxCol = 0;
                    foreach (var point in data.Points)
                    {
                        maxRow = Math.Max(maxRow, (int)point.Row);
                        maxCol = Math.Max(maxCol, (int)point.Column);
                    }

                    for (int r = 0; r <= maxRow; r++)
                    {
                        var row = new ObservableCollection<PointViewModel>();
                        for (int c = 0; c <= maxCol; c++)
                        {
                            row.Add(new PointViewModel());
                        }
                        BasePoints.Add(row);
                    }

                    // 填充数据
                    foreach (var point in data.Points)
                    {
                        var row = (int)point.Row;
                        var col = (int)point.Column;
                        BasePoints[row][col] = new PointViewModel
                        {
                            X = point.X,
                            Y = point.Y,
                            XOffset = point.XOffset ?? 0,
                            YOffset = point.YOffset ?? 0,
                            IsActive = point.IsActive ?? true,
                            Order = point.Order ?? 0
                        };
                    }
                }
                else if (filePath.EndsWith(".csv") || filePath.EndsWith(".xlsx"))
                {
                    // CSV/Excel导入实现类似，需要处理表格解析
                    // 使用第三方库如CsvHelper/EPPlus
                }

                // 更新UI
                UpdateAllPoints();
                GenerateBaseArray();
                _snackbarMessageQueue.Enqueue("网格配置导入成功", null, null, TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                ShowMessage($"导入失败: {ex.Message}");
            }
        }
        // 添加序列化模型（替代动态类型）
        public class SerializablePoint
        {
            public double X { get; set; }
            public double Y { get; set; }
            public int Row { get; set; }
            public int Column { get; set; }
            public bool IsActive { get; set; }
            public int Order { get; set; }
            public bool IsRotate { get; set; }
        }

        #region Properties
        private double _startX = 0;
        private double _startY = 0;
        private double _globalOffsetX = 0;
        private double _globalOffsetY = 0;
        private int _globalOffsetCount = 2;
        private int _currentIndex;
        private double _canvasWidth = 500;
        private double _canvasHeight = 500;
        // 添加地图显示相关属性
        private double _mapBaseSpacing = 100; // 地图基础间距
        public double MapBaseSpacing
        {
            get => _mapBaseSpacing;
            set => SetProperty(ref _mapBaseSpacing, value, UpdateMapPoints);
        }

        private double _mapPadding = 50; // 地图边距
        public double MapPadding
        {
            get => _mapPadding;
            set => SetProperty(ref _mapPadding, value, UpdateMapPoints);
        }
        private int _rows = 3;
        public int Rows
        {
            get => _rows;
            set => SetProperty(ref _rows, Math.Max(1, value));
        }

        private int _columns = 3;
        public int Columns
        {
            get => _columns;
            set => SetProperty(ref _columns, Math.Max(1, value));
        }
        public double StartX
        {
            get => _startX;
            set => SetProperty(ref _startX, value, UpdateAllPoints);
        }

        public double StartY
        {
            get => _startY;
            set => SetProperty(ref _startY, value, UpdateAllPoints);
        }

        public double GlobalOffsetX
        {
            get => _globalOffsetX;
            set => SetProperty(ref _globalOffsetX, value);
        }

        public double GlobalOffsetY
        {
            get => _globalOffsetY;
            set => SetProperty(ref _globalOffsetY, value);
        }
        public int GlobalOffsetCount
        {
            get => _globalOffsetCount;
            set => SetProperty(ref _globalOffsetCount, value);
        }
        public int CurrentIndex
        {
            get => _currentIndex;
            set
            {
                if (SetProperty(ref _currentIndex, Math.Clamp(value, 0, MaxIndex)))
                {
                    foreach (var point in AllPoints)
                    {
                        point.IsChecked = point.Index <= _currentIndex;
                    }
                }
            }
        }
        public int MaxIndex => AllPoints.Count - 1;
        public double CanvasWidth { get => _canvasWidth; set => SetProperty(ref _canvasWidth, value); }
        public double CanvasHeight { get => _canvasHeight; set => SetProperty(ref _canvasHeight, value); }
        // 在MapViewModel中添加自适应缩放逻辑
        private double _zoomFactor = 1.0;
        public double ZoomFactor
        {
            get => _zoomFactor;
            set => SetProperty(ref _zoomFactor, Math.Clamp(value, 0.5, 5.0));
        }
        private PointViewModel _selectedPoint;
        public PointViewModel SelectedPoint
        {
            get => _selectedPoint;
            set
            {
                if (SetProperty(ref _selectedPoint, value))
                {
                    Debug.WriteLine($"当前选中点已更新：{value?.Index}");
                }
            }
        }
        #endregion
    }
}

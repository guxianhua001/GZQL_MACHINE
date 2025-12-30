using Core.Abstraction;
using Core.Services;
using HandyControl.Controls;
using Interfaces;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using ModuleCore.Common.Authority;
using ModuleCore.Models;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Stations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.AxHost;

namespace Framework
{
    public abstract class CamHighCmpViewModel<TIdentifier> : BindableBase
    {
        #region 字段属性
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
        public int MaxIndex => AllPoints.Count - 1;
        #endregion
        protected readonly IDialogService _dialogService;
        protected readonly RecipePool _recipePool;
        protected readonly AppConfig _appConfig;
        //protected readonly StationState IStation.State => (StationState)State; _taskManager;
        private LoginModel _loginModel { get; set; }
        // 核心数据源
        public ObservableCollection<PointViewModel> AllPoints { get; } = new();
        public ObservableCollection<ObservableCollection<PointViewModel>> BasePoints { get; } = new();
        public ObservableCollection<ObservableCollection<double>> EncoderPoints { get; } = new();
        public ObservableCollection<PointViewModel> AllMarkPoints { get; } = new();
        public ObservableCollection<ObservableCollection<PointViewModel>> CameraMarkPoints { get; } = new();
        public ObservableCollection<ObservableCollection<double>> MarkEncoderPoints { get; } = new();
        public ObservableCollection<ColumnSpacing> ColumnSpacings { get; } = new();

        // 拍照位命令
        public DelegateCommand UpdateEncoderPointsCommand { get; }
        // 通用命令
        public DelegateCommand GenerateBaseCommand { get; }
        public DelegateCommand AddOffsetArrayCommand { get; }
        public DelegateCommand ClearAllCommand { get; }
        public DelegateCommand InitializeBaseOffsetsCommand { get; }
        public DelegateCommand SaveRecipeCommand { get; }
        public DelegateCommand LoadRecipeCommand { get; }
        public DelegateCommand<PointViewModel> MoveToPointCommand { get; }
        public DelegateCommand<PointViewModel> MoveToMarkPointCommand { get; }
        public DelegateCommand<PointViewModel> SelectPointCommand { get; }
        public DelegateCommand ExportGridCommand { get; }
        public DelegateCommand ImportGridCommand { get; }
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

        // 添加抽象属性标识当前处理的拍照位编号（1-4）
        protected abstract int CamIndex { get; }

        protected CamHighCmpViewModel(
           IDialogService dialogService,
           RecipePool recipePool,
           AppConfig appConfig,
           TaskInstanceManager taskManager,
           LoginModel loginModel)
        {
            _dialogService = dialogService;
            _recipePool = recipePool;
            _appConfig = appConfig;
            //_taskManager = taskManager;
            _loginModel = loginModel;
            // 订阅全局属性变更
            _recipePool.PropertyChanged += OnRecipePoolChanged;
            UpdateEncoderPointsCommand = new DelegateCommand(UpdateEncoderPoints);

            // 普通用户只能查看
            GenerateBaseCommand = new DelegateCommand(
                executeMethod: GenerateBaseArray,
                canExecuteMethod: () => CanEditConfig
            ).ObservesProperty(() => CanEditConfig);
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
            MoveToPointCommand = new DelegateCommand<PointViewModel>(
                executeMethod: point => ExecuteWithPermission(() => MoveToPoint(point), Authority.Operator),
                canExecuteMethod: point => CanMovePoints);
            MoveToMarkPointCommand = new DelegateCommand<PointViewModel>(
              executeMethod: point => ExecuteWithPermission(() => MoveToPoint(point), Authority.Operator),
              canExecuteMethod: point => CanMovePoints);
            ExportGridCommand = new DelegateCommand(
               executeMethod: ExecuteExportGrid,
               canExecuteMethod: () => CanEditConfig);
            ImportGridCommand = new DelegateCommand(
                executeMethod: ExecuteImportGrid,
                canExecuteMethod: () => CanEditConfig);
            // 初始化权限状态
            UpdatePermissionStates();
            // 监听登录模型变化
            _loginModel.PropertyChanged += LoginModel_PropertyChanged;
            if (!CanEditConfig)
            {
                ExecuteLoadRecipe();
            }
        }
        private void OnRecipePoolChanged(object sender, PropertyChangedEventArgs e)
        {
            // 通用变更处理
            var pool = sender.GetType();
            Debug.WriteLine($"属性 {e.PropertyName} 已变更");
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
            ClearAllCommand.RaiseCanExecuteChanged();
            InitializeBaseOffsetsCommand.RaiseCanExecuteChanged();
            SaveRecipeCommand.RaiseCanExecuteChanged();
            LoadRecipeCommand.RaiseCanExecuteChanged();
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
        protected virtual void ExecuteSaveRecipe()
        {
            try
            {
                // 序列化数据
                var baseJson = JsonConvert.SerializeObject(BasePoints);//基础点位配置
                var pinJson = JsonConvert.SerializeObject(new          //所有拍照点配置
                {
                    Rows,
                    Columns,
                    Points = AllPoints
                });
                // 序列化编码器配置（EncoderPoints）
                var encoderData = EncoderPoints
                    .Select(row => row.ToList())
                    .ToList();
                var encoderJson = JsonConvert.SerializeObject(encoderData);
                // 序列化相机Mark点配置（新增）
                var cameraMarkJson = JsonConvert.SerializeObject(CameraMarkPoints
                    .Select(row => row
                        .Select(p => new
                        {
                            p.X,
                            p.Y,
                            p.IsActive,
                            p.IsCameraMark
                        }).ToList())
                    .ToList());
                // 序列化MARK点编码器配置（EncoderPoints）
                var markEncoderPoints = MarkEncoderPoints
                    .Select(row => row.ToList())
                    .ToList();
                var markEncoderJson = JsonConvert.SerializeObject(markEncoderPoints);
                // 使用显式方法保存配置
                _recipePool.CurrentRecipe.SetCamConfig(CamIndex, true, baseJson);// 保存基础拍照位配置
                _recipePool.CurrentRecipe.SetCamConfig(CamIndex, false, pinJson);// 保存所有拍照位配置
                _recipePool.CurrentRecipe.SetEncoderConfig(CamIndex, encoderJson); // 保存编码器配置
                _recipePool.CurrentRecipe.SetCameraMarkConfig(CamIndex, cameraMarkJson); // 保存相机标记点配置
                _recipePool.CurrentRecipe.SetCameraMarkEncoderConfig(CamIndex, markEncoderJson); // 保存相机标记点编码器配置
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
                string baseJson = _recipePool.CurrentRecipe.GetCamConfig(CamIndex, true); // 获取拍照位配置
                string camPointJson = _recipePool.CurrentRecipe.GetCamConfig(CamIndex, false); // 获取编码器配置
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
                                Column = pointData.Column
                            });
                        }
                        BasePoints.Add(newRow);
                    }
                    //重建点集合
                    GenerateBaseArray();
                }
                // 2. 加载详细配置（AllPoints）
                if (!string.IsNullOrEmpty(camPointJson))
                {
                    // 反序列化逻辑...
                    var json = camPointJson;
                    var data = JsonConvert.DeserializeAnonymousType(json, new
                    {
                        Rows = 3,
                        Columns = 3,
                        Points = new List<PointViewModel>()
                    });
                    // 恢复基础配置
                    Rows = data.Rows;
                    Columns = data.Columns;

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
                        });
                    }
                }
                // 3：加载编码器配置（EncoderPoints）
                string encoderJson = _recipePool.CurrentRecipe.GetEncoderConfig(CamIndex);
                if (!string.IsNullOrEmpty(encoderJson))
                {
                    var encoderData = JsonConvert.DeserializeObject<List<List<double>>>(encoderJson);
                    EncoderPoints.Clear();
                    foreach (var row in encoderData)
                    {
                        EncoderPoints.Add(new ObservableCollection<double>(row));
                    }
                }
                // 4. 加载相机Mark点配置
                string markJson = _recipePool.CurrentRecipe.GetCameraMarkConfig(CamIndex);
                if (!string.IsNullOrEmpty(markJson))
                {
                    CameraMarkPoints.Clear();
                    var markData = JsonConvert.DeserializeObject<List<List<dynamic>>>(markJson);

                    // 用于为AllMarkPoints生成全局索引
                    int globalIndex = 0;
                    foreach (var (row, rowIndex) in markData.Select((r, i) => (r, i)))
                    {
                        var newRow = new ObservableCollection<PointViewModel>();
                        foreach (var (point, colIndex) in row.Select((p, i) => (p, i)))
                        {
                            var pvm = new PointViewModel
                            {
                                X = point.X,
                                Y = point.Y,
                                IsActive = point.IsActive,
                                IsCameraMark = point.IsCameraMark ?? true,  // 确保相机标记默认值
                                Row = point.Row ?? rowIndex,                // 优先使用数据中的行号
                                Column = point.Column ?? colIndex,          // 优先使用数据中的列号
                                Index = globalIndex++
                            };
                            newRow.Add(pvm);
                            //AllMarkPoints.Add(pvm);  // 直接添加到全局集合
                        }
                        CameraMarkPoints.Add(newRow);
                    }
                    GenerateMarkPointArray();
                }
                // 5：加载编码器配置（MarkEncoderPoints）
                string markEncoderJson = _recipePool.CurrentRecipe.GetCameraMarkEncoderConfig(CamIndex);
                if (!string.IsNullOrEmpty(markEncoderJson))
                {
                    var markEncoderData = JsonConvert.DeserializeObject<List<List<double>>>(markEncoderJson);
                    MarkEncoderPoints.Clear();
                    foreach (var row in markEncoderData)
                    {
                        MarkEncoderPoints.Add(new ObservableCollection<double>(row));
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON解析失败: {ex.Message}");
                ShowMessage($"JSON解析失败: {ex.Message}");
                throw;
            }
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
        // 可扩展的点过滤逻辑
        private bool ShouldSkipPoint(PointViewModel point)
        {
            // 跳过原点 (0,0)
            return point.X == 0 && point.Y == 0;
        }
        // 生成拍照点阵列(相对位置)
        private void GenerateBaseArray()
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
            int rowIndex = 0;
            int columnIndex = 0;
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
                        Row = rowIndex,
                        Column = columnIndex,
                        IsActive = point.IsActive,
                        Order = point.Order,
                        IsRotate = point.IsRotate
                    });
                    columnIndex++;
                }
                columnIndex = 0;
                rowIndex++;
            }
            UpdateEncoderPoints();
            GenerateMarkPointArray();
            // 计算列间距
            _recipePool.CurrentRecipe.deltaYArray = CalculateYDeltaArray();
        }
        private void GenerateMarkPointArray()
        {
            AllMarkPoints.Clear();
            bool isBasePointFound = false; // 是否找到基准点
            double baseX = 0;              // 基准点X坐标
            double baseY = 0;              // 基准点Y坐标

            // 第一次遍历：寻找第一个非 (0,0) 的基准点
            foreach (var row in CameraMarkPoints)
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
                //ShowMessage("未找到有效基准点（所有点均为原点 (0,0)）");
                return;
            }

            // 第二次遍历：生成相对偏移点
            bool isFirstValidPoint = true;
            int rowIndex = 0;
            int columnIndex = 0;
            foreach (var row in CameraMarkPoints)
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

                    AllMarkPoints.Add(new PointViewModel
                    {
                        X = finalX,
                        Y = finalY,
                        Index = AllMarkPoints.Count,
                        Row = rowIndex,
                        Column = columnIndex,
                        IsActive = point.IsActive,
                        IsCameraMark = point.IsCameraMark
                    });
                    columnIndex++;
                }
                columnIndex = 0;
                rowIndex++;
            }
            UpdateMarkEncoderPoints();
            // 计算列间距
            _recipePool.CurrentRecipe.deltaMarkYArray = CalculateMarkYDeltaArray();
        }
        // 初始化基础偏移配置
        private void InitializeBaseOffsets()
        {
            BasePoints.Clear();
            EncoderPoints.Clear();
            CameraMarkPoints.Clear();
            MarkEncoderPoints.Clear();

            // Initialize base points and encoders based on current Rows/Columns
            for (int i = 0; i < Rows; i++)
            {
                var baseRow = new ObservableCollection<PointViewModel>();
                var encoderRow = new ObservableCollection<double>();

                for (int j = 0; j < Columns; j++)
                {
                    baseRow.Add(new PointViewModel());
                    encoderRow.Add(0.0);
                }

                BasePoints.Add(baseRow);
                EncoderPoints.Add(encoderRow);
            }

            // Camera mark points (fixed 3x3 grid)
            for (int i = 0; i < 3; i++)
            {
                var markRow = new ObservableCollection<PointViewModel>();
                var markEncoderRow = new ObservableCollection<double>();

                for (int j = 0; j < 3; j++)
                {
                    markRow.Add(new PointViewModel { IsCameraMark = true });
                    markEncoderRow.Add(0.0);
                }

                CameraMarkPoints.Add(markRow);
                MarkEncoderPoints.Add(markEncoderRow);
            }
        }
        private void UpdateEncoderPoints()
        {
            EncoderPoints.Clear();

            // 获取所有有效点的行列信息
            var allRowGroups = AllPoints
                .GroupBy(p => p.Row)
                .OrderBy(g => g.Key)
                .ToList();

            // 计算最大列数（需加1，因为列索引从0开始）
            int maxColumns = allRowGroups.Any()
                ? allRowGroups.Max(g => g.Max(p => p.Column)) + 1
                : 0;

            foreach (var rowGroup in allRowGroups)
            {
                // 生成当前行编码器值
                var sortedPoints = rowGroup
                    .OrderBy(p => p.Column)
                    .ToList();

                var encoderRow = new ObservableCollection<double>();

                // 填充有效列
                foreach (var point in sortedPoints)
                {
                    int encoderValue = ConvertPositionToEncoder(point.X);
                    encoderRow.Add(encoderValue);
                }

                // 补齐缺失列
                while (encoderRow.Count < maxColumns)
                {
                    encoderRow.Add(0.0);
                }

                EncoderPoints.Add(encoderRow);
            }

            // 补齐空行（如果存在没有点的行）
            int maxRows = allRowGroups.Any()
                ? allRowGroups.Max(g => g.Key) + 1
                : 0;

            while (EncoderPoints.Count < maxRows)
            {
                EncoderPoints.Add(new ObservableCollection<double>(
                    new double[maxColumns]
                ));
            }

            RaisePropertyChanged(nameof(EncoderPoints));
        }
        private void UpdateMarkEncoderPoints()
        {
            MarkEncoderPoints.Clear();

            // 获取所有有效点的行列信息
            var allRowGroups = AllMarkPoints
                .GroupBy(p => p.Row)
                .OrderBy(g => g.Key)
                .ToList();

            // 计算最大列数（需加1，因为列索引从0开始）
            int maxColumns = allRowGroups.Any()
                ? allRowGroups.Max(g => g.Max(p => p.Column)) + 1
                : 0;

            foreach (var rowGroup in allRowGroups)
            {
                // 生成当前行编码器值
                var sortedPoints = rowGroup
                    .OrderBy(p => p.Column)
                    .ToList();

                var encoderRow = new ObservableCollection<double>();

                // 填充有效列
                foreach (var point in sortedPoints)
                {
                    int encoderValue = ConvertPositionToEncoder(point.X);
                    encoderRow.Add(encoderValue);
                }

                // 补齐缺失列
                while (encoderRow.Count < maxColumns)
                {
                    encoderRow.Add(0.0);
                }

                MarkEncoderPoints.Add(encoderRow);
            }

            // 补齐空行（如果存在没有点的行）
            int maxRows = allRowGroups.Any()
                ? allRowGroups.Max(g => g.Key) + 1
                : 0;

            while (MarkEncoderPoints.Count < maxRows)
            {
                MarkEncoderPoints.Add(new ObservableCollection<double>(
                    new double[maxColumns]
                ));
            }

            RaisePropertyChanged(nameof(MarkEncoderPoints));
        }
        private void UpdateAllPoints()
        {
            AllPoints.Clear();
            RaisePropertyChanged(nameof(MaxIndex));
        }
        private void ClearAll()
        {
            UpdateAllPoints();
        }
        // 移动逻辑实现
        protected virtual void MoveToPoint(PointViewModel point)
        {
            // 这里实现实际运动控制逻辑
            Debug.WriteLine($"移动到坐标 ({point.X}, {point.Y})");
        }
        // 导出逻辑实现
        private void ExecuteExportGrid()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV文件|*.csv|JSON文件|*.json",
                    Title = "导出网格配置"
                };
                if (saveDialog.ShowDialog() == true)
                {
                    switch (Path.GetExtension(saveDialog.FileName).ToLower())
                    {
                        case ".csv":
                            ExportToCsv(saveDialog.FileName);
                            break;
                        case ".json":
                            ExportToJson(saveDialog.FileName);
                            break;
                    }
                    ShowMessage($"导出成功：{Path.GetFileName(saveDialog.FileName)}");
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"导出失败：{ex.Message}");
            }
        }
        // 导入逻辑实现 
        private void ExecuteImportGrid()
        {
            try
            {
                var openDialog = new OpenFileDialog
                {
                    Filter = "CSV文件|*.csv|JSON文件|*.json",
                    Title = "导入网格配置"
                };
                if (openDialog.ShowDialog() == true)
                {
                    switch (Path.GetExtension(openDialog.FileName).ToLower())
                    {
                        case ".csv":
                            ImportFromCsv(openDialog.FileName);
                            break;
                        case ".json":
                            ImportFromJson(openDialog.FileName);
                            break;
                    }
                    ShowMessage($"导入成功：{Path.GetFileName(openDialog.FileName)}");
                }
            }
            catch (Exception ex)
            {
                ShowMessage($"导入失败：{ex.Message}");
            }
        }
        // 导出方法
        private void ExportToCsv(string filePath)
        {
            var lines = new List<string>();
            // 添加带类型标识的标题
            var header = "Type,Row,Column,X,Y,IsActive,Encoder,IsCameraMark,MarkEncoder";
            lines.Add(header);

            // 导出基础拍照点
            for (int row = 0; row < BasePoints.Count; row++)
            {
                for (int col = 0; col < BasePoints[row].Count; col++)
                {
                    var point = BasePoints[row][col];
                    var encoder = EncoderPoints.ElementAtOrDefault(row)?[col] ?? 0;

                    // 生成基础点记录
                    lines.Add($"Base," +
                              $"{row},{col}," +
                              $"{point.X:F2},{point.Y:F2}," +
                              $"{point.IsActive}," +
                              $"{encoder},,"); // 最后两个字段留空
                }
            }

            // 导出相机Mark点（独立处理3x3网格）
            for (int row = 0; row < CameraMarkPoints.Count; row++)
            {
                for (int col = 0; col < CameraMarkPoints[row].Count; col++)
                {
                    var point = CameraMarkPoints[row][col];
                    if (ShouldSkipPoint(point)) continue;
                    var markEncoder = MarkEncoderPoints.ElementAtOrDefault(row)?[col] ?? 0;

                    // 生成Mark点记录
                    lines.Add($"CameraMark," +
                              $"{row},{col}," +
                              $"{point.X:F2},{point.Y:F2}," +
                              $"{point.IsActive}," +
                              $",{point.IsCameraMark},{markEncoder}"); // 编码器字段留空
                }
            }

            File.WriteAllLines(filePath, lines, Encoding.UTF8);
        }
        // 导入方法
        private void ImportFromCsv(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            if (lines.Length < 2) return;

            // 清空并重新初始化数据结构
            BasePoints.Clear();
            EncoderPoints.Clear();
            CameraMarkPoints.Clear();
            MarkEncoderPoints.Clear();
            InitializeBaseOffsets();

            foreach (var line in lines.Skip(1))
            {
                var parts = line.Split(',');
                if (parts.Length < 9) continue;

                var type = parts[0];
                var row = int.Parse(parts[1]);
                var col = int.Parse(parts[2]);

                switch (type)
                {
                    case "Base" when row < BasePoints.Count && col < BasePoints[row].Count:
                        // 填充基础点
                        BasePoints[row][col].X = double.Parse(parts[3]);
                        BasePoints[row][col].Y = double.Parse(parts[4]);
                        BasePoints[row][col].IsActive = bool.Parse(parts[5]);
                        EncoderPoints[row][col] = double.Parse(parts[6]);
                        break;

                    case "CameraMark" when row < 3 && col < 3: // 硬编码3x3限制
                        // 填充Mark点
                        CameraMarkPoints[row][col].X = double.Parse(parts[3]);
                        CameraMarkPoints[row][col].Y = double.Parse(parts[4]);
                        CameraMarkPoints[row][col].IsActive = bool.Parse(parts[5]);
                        CameraMarkPoints[row][col].IsCameraMark = bool.Parse(parts[7]);
                        MarkEncoderPoints[row][col] = double.Parse(parts[8]);
                        break;
                }
            }
        }

        // 重构网格数据结构
        private (ObservableCollection<ObservableCollection<PointViewModel>>,
            ObservableCollection<ObservableCollection<double>>,
            ObservableCollection<ObservableCollection<double>>)
            ReconstructGrids(
                Dictionary<(int, int), PointViewModel> baseDict,
                Dictionary<(int, int), double> encoderDict,
                Dictionary<(int, int), double> markEncoderDict,
                int maxRow, int maxCol)
        {
            int rows = Math.Max(maxRow + 1, 5);  // 至少5行
            int cols = Math.Max(maxCol + 1, 9);  // 固定9列

            var baseGrid = new ObservableCollection<ObservableCollection<PointViewModel>>();
            var encoderGrid = new ObservableCollection<ObservableCollection<double>>();
            var markEncoderGrid = new ObservableCollection<ObservableCollection<double>>();
            for (int r = 0; r < rows; r++)
            {
                var baseRow = new ObservableCollection<PointViewModel>();
                var encoderRow = new ObservableCollection<double>();
                var markEncoderRow = new ObservableCollection<double>();
                for (int c = 0; c < cols; c++)
                {
                    // 基础点填充
                    if (baseDict.TryGetValue((r, c), out var point))
                    {
                        baseRow.Add(point);
                    }
                    else
                    {
                        baseRow.Add(new PointViewModel
                        {
                            Row = r,
                            Column = c,
                            IsActive = false
                        });
                    }

                    // 编码器填充
                    encoderRow.Add(encoderDict.GetValueOrDefault((r, c), 0));
                    markEncoderRow.Add(markEncoderDict.GetValueOrDefault((r, c), 0));
                }

                baseGrid.Add(baseRow);
                encoderGrid.Add(encoderRow);
                markEncoderGrid.Add(markEncoderRow);
            }

            return (baseGrid, encoderGrid, markEncoderGrid);
        }
        // 导入方法
        private void ImportFromJson(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var data = JsonConvert.DeserializeAnonymousType(json, new
            {
                Base = new List<List<dynamic>>(),
                Encoder = new List<List<double>>()
            });

            // 转换为ObservableCollection
            var baseGrid = new ObservableCollection<ObservableCollection<PointViewModel>>(
                data.Base.Select(row =>
                    new ObservableCollection<PointViewModel>(
                        row.Select(p => new PointViewModel
                        {
                            X = p.X,
                            Y = p.Y,
                            IsActive = p.IsActive
                        })
                    )
                )
            );

            var encoderGrid = new ObservableCollection<ObservableCollection<double>>(
                data.Encoder.Select(row =>
                    new ObservableCollection<double>(row)
                )
            );

            // 赋值给属性
            BasePoints.Clear();
            EncoderPoints.Clear();

            foreach (var row in baseGrid) BasePoints.Add(row);
            foreach (var row in encoderGrid) EncoderPoints.Add(row);
        }
        // 导出Json方法
        private void ExportToJson(string filePath)
        {
            try
            {
                var exportData = new
                {
                    Metadata = new
                    {
                        Rows = Rows,
                        Columns = Columns,
                        ExportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    },
                    BasePoints = BasePoints.Select((row, rowIndex) =>
                        row.Select((point, colIndex) => new
                        {
                            Row = rowIndex,
                            Column = colIndex,
                            point.X,
                            point.Y,
                            point.IsActive,
                            point.IsRotate,
                            point.Order
                        }).ToList()
                    ).ToList(),
                    EncoderPoints = EncoderPoints.Select(row =>
                        row.Select(value => value).ToList()
                    ).ToList(),
                    CameraMarkPoints = CameraMarkPoints.Select(row =>
                        row.Select(point => new
                        {
                            point.X,
                            point.Y,
                            point.IsActive
                        }).ToList()
                    ).ToList(),
                    MarkEncoderPoints = MarkEncoderPoints.Select(row =>
                        row.Select(value => value).ToList()
                    ).ToList()
                };
                var json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
                File.WriteAllText(filePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                ShowMessage($"导出失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 将轴位置(mm)转换为编码器值
        /// </summary>
        public int ConvertPositionToEncoder(double axisPosition)
        {
            int encoderPosition = (int)((-1024 * axisPosition - 1) / 3) + 1;
            return encoderPosition;
        }
        public double[] CalculateYDeltaArray()
        {
            const int startRowIndex = 1; // 起始行设置为第2排（索引从0开始）

            List<double> deltas = new List<double>();

            // 计算有效行范围：从startRowIndex开始，最多取3行（2、3、4排）
            for (int i = startRowIndex; i < startRowIndex + (Rows - 1); i++) // 固定取3行
            {
                // 安全检查
                if (i >= BasePoints.Count || BasePoints[i].Count == 0)
                {
                    deltas.Add(0);
                    continue;
                }

                // 计算基准值和差值
                var currentPoint = BasePoints[i][0];
                if (i == startRowIndex)
                {
                    // 添加基准值
                    deltas.Add(currentPoint.Y);
                }
                else
                {
                    // 添加与前序行的差值
                    var prevPoint = BasePoints[i - 1][0];
                    deltas.Add(currentPoint.Y - prevPoint.Y);
                }
            }

            return deltas.ToArray();
        }
        public double[] CalculateMarkYDeltaArray()
        {
            const int startRowIndex = 1; // 起始行设置为第2排（索引从0开始）

            List<double> deltas = new List<double>();

            // 计算有效行范围：从startRowIndex开始，最多取3行（2、3、4排）
            for (int i = startRowIndex; i < startRowIndex + 3; i++) // 固定取3行
            {
                // 安全检查
                if (i >= CameraMarkPoints.Count)
                {
                    continue;
                }
                // 计算基准值和差值
                var currentPoint = CameraMarkPoints[i][0];
                if (i == startRowIndex)
                {
                    // 添加基准值
                    deltas.Add(currentPoint.Y);
                }
            }

            return deltas.ToArray();
        }
        // 扩展方法：增加行列检查
        private PointViewModel SafeGetPoint(int row, int col)
        {
            return (row < BasePoints.Count && col < BasePoints[row].Count)
                ? BasePoints[row][col]
                : new PointViewModel();
        }

        // 动态根据ROWS属性生成数组
        public double[] GenerateDynamicYArray()
        {
            List<double> result = new List<double>();
            const int startRow = 1; // 从第三排开始

            if (Rows <= startRow) return new double[0];

            // 获取基准行Y值
            double baseY = SafeGetPoint(startRow, 0).Y;
            result.Add(baseY);

            // 动态添加后续行的差值
            for (int row = startRow + 1; row < Rows; row++)
            {
                double currentY = SafeGetPoint(row, 0).Y;
                result.Add(currentY - baseY);
            }

            return result.ToArray();
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
    }
}

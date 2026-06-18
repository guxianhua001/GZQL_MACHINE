using Core.Abstraction;
using Core.Events;
using Core.Models;
using Core.Services;
using Core.Utilities;
using Framework.Dialogs;
using MaterialDesignThemes.Wpf;
using Framework.Models;
using MotionControl.Interfaces;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Recipe.Events;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Recipe.ViewModels
{
    public class MultiStationPositionEditorViewModel : BindableBase, IDialogAware
    {
        #region Private Fields
        private readonly IRecipePoolService _recipePoolService;
        private readonly IAxisConfigurationService _axisConfig;
        private readonly IStationRegistry _stationRegistry;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IMotionService _motionService;
        private readonly Core.Abstraction.ILocalizationService _localization;

        private const double DefaultVelocity = 10.0;

        private StationItem _selectedStation;
        private DataRowView _selectedRow;
        /// <summary>表格当前选中的轴列名（点击轴坐标单元格时更新）</summary>
        private string _selectedAxisColumnName;
        private double _selectedSpeed = 10.0;
        private DataTable _positionsTable;
        private bool _isMoving;

        private JsonObject _currentStationNode;
        private string _currentStationIdentifier;

        private SubscriptionToken _recipeChangedToken;
        private SubscriptionToken _poolChangedToken;
        private SubscriptionToken _stationRegisteredToken;
        private SubscriptionToken _savePositionEditorToken;
        #endregion

        #region Public Properties
        /// <summary>
        /// 当前选中的工站，切换时自动加载该工站的位置数据
        /// </summary>
        public StationItem SelectedStation
        {
            get => _selectedStation;
            set
            {
                if (SetProperty(ref _selectedStation, value))
                {
                    _currentStationIdentifier = value?.Identifier;
                    RaiseHardwareCommandCanExecuteChanged();
                    _ = LoadPositionsForCurrentStationAsync();
                }
            }
        }

        /// <summary>
        /// 工站列表，从IStationRegistry动态获取
        /// </summary>
        public ObservableCollection<StationItem> Stations { get; }

        public DataRowView SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (SetProperty(ref _selectedRow, value))
                {
                    (DeletePositionCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    (MoveUpCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    (MoveDownCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    RaiseHardwareCommandCanExecuteChanged();
                }
            }
        }

        /// <summary>当前选中的轴列（如 X、Y），用于单轴 GOTO</summary>
        public string SelectedAxisColumnName
        {
            get => _selectedAxisColumnName;
            private set => SetProperty(ref _selectedAxisColumnName, value);
        }

        public double SelectedSpeed { get => _selectedSpeed; set => SetProperty(ref _selectedSpeed, value); }
        public bool IsMoving { get => _isMoving; private set => SetProperty(ref _isMoving, value); }
        public ObservableCollection<double> SpeedOptions { get; } = new ObservableCollection<double> { 1, 5, 10, 20, 30, 40, 50 };

        public DataTable PositionsTable
        {
            get => _positionsTable;
            private set => SetProperty(ref _positionsTable, value);
        }

        public string Title => "Multi-Station Position Editor";
        #endregion

        #region Commands
        public ICommand SaveCommand { get; }
        public ICommand AddPositionCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand DeletePositionCommand { get; }
        public ICommand TeachCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ReplayCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        #endregion

        #region Constructor
        public MultiStationPositionEditorViewModel(
            IRecipePoolService recipePoolService,
            IAxisConfigurationService axisConfig,
            IStationRegistry stationRegistry,
            ILoggerService loggerService,
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IMotionService motionService,
            Core.Abstraction.ILocalizationService localization)
        {
            _recipePoolService = recipePoolService;
            _axisConfig = axisConfig;
            _stationRegistry = stationRegistry;
            _logger = loggerService;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _motionService = motionService;
            _localization = localization;

            Stations = new ObservableCollection<StationItem>();
            PositionsTable = new DataTable();

            // 从工站注册表加载已有工站
            LoadStationsFromRegistry();

            // 订阅工站注册事件，动态添加新工站
            _stationRegisteredToken = _eventAggregator.GetEvent<StationRegisteredEvent>()
                .Subscribe(OnStationRegistered, ThreadOption.PublisherThread, true);

            // 订阅配方/配方池切换事件，实现热刷新
            _recipeChangedToken = _eventAggregator.GetEvent<RecipeChangedEvent>().Subscribe(OnRecipeChanged);
            _poolChangedToken = _eventAggregator.GetEvent<RecipePoolChangedEvent>().Subscribe(OnPoolChanged);

            // 订阅保存池事件：保存池前将当前编辑的位置数据暂存到 RecipePoolService，
            // 由 SaveRecipePoolAsync 统一提交到文件，避免位置编辑器参数丢失
            _savePositionEditorToken = _eventAggregator.GetEvent<SavePositionEditorEvent>().Subscribe(OnSavePositionEditorRequested);

            SaveCommand = new DelegateCommand(Save);
            AddPositionCommand = new DelegateCommand(AddPosition);
            CloseCommand = new DelegateCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel)));
            DeletePositionCommand = new DelegateCommand(DeleteSelected, CanDeleteSelected);
            TeachCommand = new DelegateCommand(Teach, CanExecuteHardwareOperation);
            UndoCommand = new DelegateCommand(Undo);
            // STOP 为安全操作，始终可用，不绑定选中行或运动前置条件
            StopCommand = new DelegateCommand(Stop);
            ReplayCommand = new DelegateCommand(Replay, CanExecuteHardwareOperation);
            MoveUpCommand = new DelegateCommand(MoveUp, CanMoveUp);
            MoveDownCommand = new DelegateCommand(MoveDown, CanMoveDown);

            // 默认选中第一个工站
            if (Stations.Any()) SelectedStation = Stations.First();
        }
        #endregion

        #region Station Management
        /// <summary>
        /// 从IStationRegistry加载所有已注册工站到Stations集合
        /// </summary>
        private void LoadStationsFromRegistry()
        {
            Stations.Clear();
            foreach (var station in _stationRegistry.GetAllStations())
            {
                Stations.Add(new StationItem
                {
                    Identifier = station.StationIdentifier,
                    Name = station.StationIdentifier,
                    RecipeName = station.CurrentRecipeName ?? "Default"
                });
            }
        }

        /// <summary>
        /// 新工站注册时，动态添加到选择列表
        /// </summary>
        private void OnStationRegistered(IStationParameterProvider station)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!Stations.Any(s => s.Identifier == station.StationIdentifier))
                {
                    Stations.Add(new StationItem
                    {
                        Identifier = station.StationIdentifier,
                        Name = station.StationIdentifier,
                        RecipeName = station.CurrentRecipeName ?? "Default"
                    });
                }
            });
        }
        #endregion

        #region Data Loading
        private async Task LoadPositionsForCurrentStationAsync()
        {
            if (string.IsNullOrEmpty(_currentStationIdentifier)) return;
            try
            {
                _currentStationNode = null;

                var pool = await _recipePoolService.GetRecipePoolAsync(_recipePoolService.CurrentPoolName);
                if (pool == null)
                {
                    // 配方池不存在时，创建空节点以允许新建保存
                    _currentStationNode = CreateEmptyStationNode();
                    PositionsTable = CreateEmptyTable();
                    return;
                }

                string currentRecipeName = pool.CurrentRecipeName;
                if (string.IsNullOrEmpty(currentRecipeName))
                {
                    _currentStationNode = CreateEmptyStationNode();
                    PositionsTable = CreateEmptyTable();
                    return;
                }

                var recipe = pool.GetRecipeByName(currentRecipeName);
                if (recipe == null || !recipe.Parameters.TryGetValue(_currentStationIdentifier, out var paramObj))
                {
                    // 配方中没有当前工站的参数时，创建空节点以允许新建保存
                    _currentStationNode = CreateEmptyStationNode();
                    PositionsTable = CreateEmptyTable();
                    return;
                }

                var dt = CreateEmptyTable();
                var axes = _axisConfig.GetAxesForStation(_currentStationIdentifier).ToList();

                if (paramObj is JsonElement jsonElement)
                {
                    _currentStationNode = JsonNode.Parse(jsonElement.GetRawText()).AsObject();
                }
                else if (paramObj is JsonObject jsonObj)
                {
                    _currentStationNode = jsonObj;
                }
                else
                {
                    _currentStationNode = JsonNode.Parse(JsonSerializer.Serialize(paramObj)).AsObject();
                }

                // 确保 Positions 节点存在
                if (!_currentStationNode.ContainsKey("Positions"))
                {
                    _currentStationNode["Positions"] = new JsonObject();
                }

                var posNode = _currentStationNode["Positions"];
                if (posNode is JsonObject positionsObj)
                {
                    foreach (var kvp in positionsObj)
                    {
                        var row = dt.NewRow();
                        row["PositionName"] = kvp.Key;
                        row["IsReadOnly"] = IsBuiltInPosition(kvp.Key);

                        if (kvp.Value is JsonObject positionObj)
                        {
                            // 兼容两种位置格式：
                            // 1. 带 Axes 子对象：{ "Axes": { "Rx": 2, ... }, "Comment": "..." }
                            // 2. 轴值直接在位置下：{ "X": 0, "Y": 0, ..., "Comment": "..." }
                            JsonObject axisSource;
                            if (positionObj.TryGetPropertyValue("Axes", out var axesNode) && axesNode is JsonObject axesObj)
                                axisSource = axesObj;
                            else
                                axisSource = positionObj;

                            foreach (var axis in axes)
                            {
                                var valNode = axisSource[axis.Name];
                                if (valNode is JsonValue jsonVal && jsonVal.TryGetValue(out double dVal))
                                {
                                    row[axis.Name] = dVal;
                                }
                                else
                                {
                                    row[axis.Name] = DBNull.Value;
                                }
                            }

                            var commentNode = positionObj["Comment"];
                            row["Comment"] = commentNode?.ToString() ?? "";
                        }

                        dt.Rows.Add(row);
                    }
                }

                PositionsTable = dt;
            }
            catch (Exception ex)
            {
                _logger.Error($"加载工站 '{_currentStationIdentifier}' 位置数据失败: {ex.Message}");
                // 异常时也创建空节点，确保保存功能可用
                _currentStationNode = CreateEmptyStationNode();
                PositionsTable = CreateEmptyTable();
            }
            finally
            {
                Application.Current?.Dispatcher.Invoke(RaiseHardwareCommandCanExecuteChanged);
            }
        }

        /// <summary>
        /// 创建空的工站参数节点，包含空的 Positions 子对象
        /// 确保即使配方中没有当前工站参数，保存功能仍然可用
        /// </summary>
        private JsonObject CreateEmptyStationNode()
        {
            return new JsonObject { ["Positions"] = new JsonObject() };
        }

        /// <summary>
        /// 创建包含标准列定义的空表格
        /// </summary>
        private DataTable CreateEmptyTable()
        {
            var dt = new DataTable();
            dt.Columns.Add("PositionName", typeof(string));
            dt.Columns.Add("IsReadOnly", typeof(bool));

            if (!string.IsNullOrEmpty(_currentStationIdentifier))
            {
                var axes = _axisConfig.GetAxesForStation(_currentStationIdentifier).ToList();
                foreach (var axis in axes)
                {
                    dt.Columns.Add(axis.Name, typeof(double));
                }
            }

            dt.Columns.Add("Comment", typeof(string));
            return dt;
        }

        private bool IsBuiltInPosition(string name) => name == "StandbyPosition" || name == "SafePosition";
        #endregion

        #region Position Management
        private void AddPosition()
        {
            if (PositionsTable == null) return;
            string newName = "NewPosition";
            int index = 1;
            while (PositionsTable.Rows.Cast<DataRow>().Any(r => r["PositionName"].ToString() == newName + index)) index++;
            newName = newName + index;

            var newRow = PositionsTable.NewRow();
            newRow["PositionName"] = newName;
            newRow["IsReadOnly"] = false;
            PositionsTable.Rows.Add(newRow);
            SelectedRow = PositionsTable.DefaultView[PositionsTable.Rows.Count - 1];
        }

        private bool CanDeleteSelected() => SelectedRow != null && !(bool)SelectedRow["IsReadOnly"];

        private async void DeleteSelected()
        {
            if (SelectedRow == null) return;
            var positionName = SelectedRow["PositionName"].ToString();
            _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters { { "title", "Confirm Delete" }, { "message", $"Are you sure you want to delete position '{positionName}'?" } }, result =>
            {
                if (result.Result == ButtonResult.Yes)
                {
                    PositionsTable.Rows.Remove(SelectedRow.Row);
                    SelectedRow = null;
                }
            });
        }
        #endregion

        #region Teach & Replay & Stop
        /// <summary>
        /// 示教/Goto 需选中行且已选择工站；选中行或工站变化时需刷新按钮使能
        /// </summary>
        private bool CanExecuteHardwareOperation()
            => SelectedRow != null && !string.IsNullOrEmpty(_currentStationIdentifier);

        private void RaiseHardwareCommandCanExecuteChanged()
        {
            (TeachCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            (ReplayCommand as DelegateCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// 根据工站标识与轴名称解析逻辑轴号（hwcfg TaskId + AxisConfig）
        /// </summary>
        private int FindAxisLogicalId(string stationIdentifier, string axisName)
        {
            if (string.IsNullOrEmpty(stationIdentifier) || string.IsNullOrEmpty(axisName))
                return -1;

            var taskConfig = _motionService.GetTaskConfigurations()
                .FirstOrDefault(tc => tc.Type == stationIdentifier);
            if (taskConfig == null)
                return -1;

            var axisConfig = _motionService.GetAxisConfigurations()
                .FirstOrDefault(a => a.TaskId == taskConfig.TaskId && a.Name == axisName);
            return axisConfig?.LogicalId ?? -1;
        }

        /// <summary>
        /// 从 DataTable 当前行提取轴位置字典，供 Goto 使用
        /// </summary>
        private Dictionary<string, double> ExtractAxisPositionsFromRow(DataRowView row)
        {
            var result = new Dictionary<string, double>();
            if (row == null || string.IsNullOrEmpty(_currentStationIdentifier)) return result;

            var axes = _axisConfig.GetAxesForStation(_currentStationIdentifier);
            foreach (var axis in axes)
            {
                var cellValue = row[axis.Name];
                if (cellValue != DBNull.Value && cellValue != null)
                    result[axis.Name] = Convert.ToDouble(cellValue);
            }
            return result;
        }

        /// <summary>
        /// 弹出 Yes/No 确认框（示教前安全确认）
        /// </summary>
        private Task<bool> ShowYesNoConfirmationAsync(string title, string message)
        {
            var tcs = new TaskCompletionSource<bool>();
            _dialogService.ShowDialog("ConfirmationDialog",
                new DialogParameters { { "title", title }, { "message", message } },
                result => tcs.TrySetResult(result.Result == ButtonResult.Yes));
            return tcs.Task;
        }

        /// <summary>
        /// 表格单元格切换时更新选中轴列（供单轴 GOTO 使用）
        /// 仅在有效轴列时更新，失焦/无效时不覆盖（避免点击按钮时清空）
        /// </summary>
        public void SetSelectedAxisColumn(int tableColumnIndex)
        {
            if (PositionsTable == null || tableColumnIndex < 0 || tableColumnIndex >= PositionsTable.Columns.Count)
                return; // 失焦等场景不覆盖

            var columnName = PositionsTable.Columns[tableColumnIndex].ColumnName;
            // 非轴列也不覆盖，保留上一次有效选择
            if (columnName is "PositionName" or "Comment" or "IsReadOnly")
                return;

            SelectedAxisColumnName = columnName;
        }

        /// <summary>
        /// CustomDialog 异步弹出（多功能自定义弹窗）
        /// </summary>
        private Task<IDialogResult> ShowCustomDialogAsync(DialogParameters parameters)
            => _dialogService.ShowDialogAsync("CustomDialog", parameters);

        private string Loc(string key, string fallback)
            => _localization.GetResourceOrDefault(key, fallback);

        private async void Teach()
        {
            if (SelectedRow == null || !CanExecuteHardwareOperation()) return;

            var positionName = SelectedRow["PositionName"]?.ToString() ?? string.Empty;
            var confirmed = await ShowYesNoConfirmationAsync(
                Loc("MultiStationPos_ConfirmTeachTitle", "确认示教"),
                _localization.GetResource("MultiStationPos_ConfirmTeachMessage", positionName));
            if (!confirmed) return;

            try
            {
                var axes = _axisConfig.GetAxesForStation(_currentStationIdentifier);
                foreach (var axis in axes)
                {
                    var axisId = FindAxisLogicalId(_currentStationIdentifier, axis.Name);
                    if (axisId < 0) continue;

                    try
                    {
                        var position = _motionService.GetAxisPosition(axisId);
                        if (PositionsTable.Columns.Contains(axis.Name))
                            SelectedRow[axis.Name] = position;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"Teach: 读取轴 {axis.Name}(ID={axisId}) 位置失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Teach failed: {ex.Message}");
                await _dialogService.ShowDialogAsync("NotificationDialog",
                    new DialogParameters {
                        { "message", _localization.GetResource("MultiStationPos_TeachFailed", ex.Message) },
                        { "icon", PackIconKind.Error }
                    });
            }
        }

        private async void Replay()
        {
            if (SelectedRow == null || !CanExecuteHardwareOperation()) return;

            var positionName = SelectedRow["PositionName"]?.ToString() ?? string.Empty;
            var targetPositions = ExtractAxisPositionsFromRow(SelectedRow);
            if (targetPositions.Count == 0)
            {
                await _dialogService.ShowDialogAsync("NotificationDialog",
                    new DialogParameters {
                        { "message", Loc("MultiStationPos_NoAxisValues", "选中行没有有效的轴坐标，无法前往。") },
                        { "icon", PackIconKind.Warning }
                    });
                return;
            }

            var btnSingle = Loc("MultiStationPos_GotoSingleAxis", "单轴移动");
            var btnSimultaneous = Loc("MultiStationPos_GotoSimultaneous", "多轴同时启动");
            var btnCancel = Loc("Cancel", "取消");

            var dialogResult = await ShowCustomDialogAsync(new DialogParameters
            {
                { "title", Loc("MultiStationPos_ConfirmGotoTitle", "确认前往") },
                { "message", _localization.GetResource("MultiStationPos_ConfirmGotoMessage", positionName) },
                { "iconKind", PackIconKind.Target },
                { "buttons", new ObservableCollection<DialogButton>
                    {
                        new DialogButton { Text = btnSingle, BackgroundHex = "#7B1FA2", IconKind = PackIconKind.ArrowRight, ButtonIndex = 0 },
                        new DialogButton { Text = btnSimultaneous, BackgroundHex = "#512DA8", IconKind = PackIconKind.FastForward, ButtonIndex = 1 },
                        new DialogButton { Text = btnCancel, BackgroundHex = "#757575", IconKind = PackIconKind.Close, ButtonIndex = 2 }
                    }
                }
            });

            int? choiceIndex = null;
            if (dialogResult?.Result == ButtonResult.OK &&
                dialogResult.Parameters.TryGetValue<int>("buttonIndex", out var index))
                choiceIndex = index;

            if (choiceIndex == null || choiceIndex == 2) return;

            // 回零检查：未回零的轴不允许移动（SkipHomeCheck 的龙门从轴跳过检查）
            var notHomedAxes = new List<string>();
            // 伺服使能检查：未使能的轴不允许移动
            var notEnabledAxes = new List<string>();
            var axisConfigs = _motionService.GetAxisConfigurations();

            foreach (var kvp in targetPositions)
            {
                var axisId = FindAxisLogicalId(_currentStationIdentifier, kvp.Key);
                if (axisId >= 0)
                {
                    // SkipHomeCheck 的轴（如龙门从轴）跳过回零检查
                    var axisCfg = axisConfigs.FirstOrDefault(a => a.LogicalId == axisId);
                    if (axisCfg == null || axisCfg.SkipHomeCheck)
                        continue;

                    var homeResult = await _motionService.CheckHomeDoneAsync(axisId);
                    
                    if (homeResult != 1)
                        notHomedAxes.Add(kvp.Key);

                    var axisState = _motionService.GetAxisState(axisId);
                    if (axisState != null && !axisState.IsEnabled)
                        notEnabledAxes.Add(kvp.Key);
                }
            }

            if (notHomedAxes.Count > 0)
            {
                var axisList = string.Join(", ", notHomedAxes);
                await _dialogService.ShowDialogAsync("NotificationDialog",
                    new DialogParameters {
                        { "message", string.Format(_localization.GetResourceOrDefault("MultiStationPos_AxisNotHomed", "Axis not homed: {0}"), axisList) },
                        { "icon", PackIconKind.AlertCircleOutline }
                    });
                return;
            }

            if (notEnabledAxes.Count > 0)
            {
                var axisList = string.Join(", ", notEnabledAxes);
                await _dialogService.ShowDialogAsync("NotificationDialog",
                    new DialogParameters {
                        { "message", _localization.GetResource("MultiStationPos_AxisNotEnabled", axisList) },
                        { "icon", PackIconKind.AlertCircleOutline }
                    });
                return;
            }

            try
            {
                IsMoving = true;
                var velocity = SelectedSpeed > 0 ? SelectedSpeed : DefaultVelocity;

                if (choiceIndex == 0)
                    await GotoSelectedAxisAsync(targetPositions, velocity);
                else if (choiceIndex == 1)
                    await GotoSimultaneousAsync(targetPositions, velocity);
            }
            catch (Exception ex)
            {
                _logger.Error($"Goto failed: {ex.Message}");
                await _dialogService.ShowDialogAsync("NotificationDialog",
                    new DialogParameters {
                        { "message", _localization.GetResource("MultiStationPos_GotoFailed", ex.Message) },
                        { "icon", PackIconKind.Error }
                    });
            }
            finally
            {
                IsMoving = false;
            }
        }

        /// <summary>
        /// 单轴移动：仅移动表格当前选中轴列对应的目标位置
        /// </summary>
        private async Task GotoSelectedAxisAsync(Dictionary<string, double> targetPositions, double velocity)
        {
            if (string.IsNullOrEmpty(SelectedAxisColumnName))
            {
                await _dialogService.ShowDialogAsync("NotificationDialog",
                    new DialogParameters {
                        { "message", Loc("MultiStationPos_SelectAxisCell", "请先在表格中选中要移动的轴列单元格。") },
                        { "icon", PackIconKind.Warning }
                    });
                return;
            }

            if (!targetPositions.TryGetValue(SelectedAxisColumnName, out var targetPos))
            {
                await _dialogService.ShowDialogAsync("NotificationDialog",
                    new DialogParameters {
                        { "message", Loc("MultiStationPos_NoAxisValue", "选中轴列没有有效的目标坐标。") },
                        { "icon", PackIconKind.Warning }
                    });
                return;
            }

            var axisId = FindAxisLogicalId(_currentStationIdentifier, SelectedAxisColumnName);
            if (axisId >= 0)
                await _motionService.MoveAbsAsync(axisId, targetPos, velocity);
        }

        /// <summary>
        /// 多轴同时启动：各轴并行下发 MoveAbsAsync，非插补联动
        /// </summary>
        private async Task GotoSimultaneousAsync(Dictionary<string, double> targetPositions, double velocity)
        {
            var moveTasks = new List<Task>();

            foreach (var kvp in targetPositions)
            {
                var axisId = FindAxisLogicalId(_currentStationIdentifier, kvp.Key);
                if (axisId >= 0)
                    moveTasks.Add(_motionService.MoveAbsAsync(axisId, kvp.Value, velocity));
            }

            if (moveTasks.Count > 0)
                await Task.WhenAll(moveTasks);
        }

        /// <summary>
        /// 停止当前工站所有轴（安全操作，始终可用）
        /// </summary>
        private void Stop()
        {
            if (string.IsNullOrEmpty(_currentStationIdentifier)) return;

            foreach (var axis in _axisConfig.GetAxesForStation(_currentStationIdentifier))
            {
                var axisId = FindAxisLogicalId(_currentStationIdentifier, axis.Name);
                if (axisId < 0) continue;

                try
                {
                    _motionService.StopAxis(axisId);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Stop axis {axis.Name}(ID={axisId}) failed: {ex.Message}");
                }
            }
        }

        private void Undo() => _ = LoadPositionsForCurrentStationAsync();
        #endregion

        #region Move Row
        private bool CanMoveUp() => SelectedRow != null && PositionsTable.Rows.IndexOf(SelectedRow.Row) > 0;
        private bool CanMoveDown() => SelectedRow != null && PositionsTable.Rows.IndexOf(SelectedRow.Row) < PositionsTable.Rows.Count - 1;

        private void MoveUp()
        {
            if (!CanMoveUp()) return;
            int currentIndex = PositionsTable.Rows.IndexOf(SelectedRow.Row);
            var currentRow = SelectedRow.Row;
            var newRow = PositionsTable.NewRow();
            newRow.ItemArray = currentRow.ItemArray;
            PositionsTable.Rows.Remove(currentRow);
            PositionsTable.Rows.InsertAt(newRow, currentIndex - 1);
            SelectedRow = PositionsTable.DefaultView[currentIndex - 1];
        }

        private void MoveDown()
        {
            if (!CanMoveDown()) return;
            int currentIndex = PositionsTable.Rows.IndexOf(SelectedRow.Row);
            var currentRow = SelectedRow.Row;
            var newRow = PositionsTable.NewRow();
            newRow.ItemArray = currentRow.ItemArray;
            PositionsTable.Rows.Remove(currentRow);
            PositionsTable.Rows.InsertAt(newRow, currentIndex + 1);
            SelectedRow = PositionsTable.DefaultView[currentIndex + 1];
        }
        #endregion

        #region Save
        /// <summary>
        /// 保存当前工站 Positions 到配方文件。
        /// 性能要点：仅 Commit 阶段读盘一次；避免保存前重复 GetRecipePoolAsync。
        /// </summary>
        private async void Save()
        {
            if (string.IsNullOrEmpty(_currentStationIdentifier))
            {
                await _dialogService.ShowDialogAsync("NotificationDialog", new DialogParameters {
                    { "message", "未选择工站，无法保存。" },
                    { "icon", PackIconKind.Warning }
                });
                return;
            }

            var axes = _axisConfig.GetAxesForStation(_currentStationIdentifier).ToList();

            // 构建新的 Positions 节点
            var newPosObj = new JsonObject();
            foreach (DataRow row in PositionsTable.Rows)
            {
                var name = row["PositionName"].ToString();
                if (string.IsNullOrEmpty(name)) continue;

                // 保持 Axes 子对象格式，与配方文件结构一致
                var axesObj = new JsonObject();
                foreach (var axis in axes)
                {
                    var cellValue = row[axis.Name];
                    if (cellValue != DBNull.Value && cellValue != null)
                    {
                        axesObj[axis.Name] = Convert.ToDouble(cellValue);
                    }
                }

                var positionObj = new JsonObject();
                positionObj["Axes"] = axesObj;
                positionObj["Comment"] = row["Comment"]?.ToString() ?? "";
                newPosObj[name] = positionObj;
            }

            // 基于已加载的工站节点合并 Positions，避免保存前额外读盘
            // Commit 阶段会再读一次配方池并写入，保证与其他工站参数合并正确
            JsonObject stationNodeToSave;
            try
            {
                stationNodeToSave = _currentStationNode != null
                    ? JsonNode.Parse(_currentStationNode.ToJsonString()).AsObject()
                    : CreateEmptyStationNode();
                stationNodeToSave["Positions"] = newPosObj;
            }
            catch (Exception ex)
            {
                _logger.Error($"构建工站保存节点失败: {ex.Message}，将仅保存位置数据");
                stationNodeToSave = CreateEmptyStationNode();
                stationNodeToSave["Positions"] = newPosObj;
            }

            var pool = await _recipePoolService.GetRecipePoolAsync(_recipePoolService.CurrentPoolName);
            // 与 LoadPositionsForCurrentStationAsync 一致：优先使用配方池文件中的 CurrentRecipeName
            string currentRecipeName = pool?.CurrentRecipeName;
            if (string.IsNullOrEmpty(currentRecipeName))
                currentRecipeName = _recipePoolService.CurrentRecipeName;
            if (string.IsNullOrEmpty(currentRecipeName))
                currentRecipeName = "Default";

            await _recipePoolService.SaveStationParametersAsync(
                _recipePoolService.CurrentPoolName,
                currentRecipeName,
                _currentStationIdentifier,
                stationNodeToSave
            );

            _currentStationNode = stationNodeToSave;

            // 通知其他组件位置参数已更新（如自定义编辑器流程的位置缓存）
            _eventAggregator.GetEvent<StationParameterSavedEvent>().Publish(_currentStationIdentifier);

            await _dialogService.ShowDialogAsync("NotificationDialog", new DialogParameters {
                { "title", "Success" },
                { "message", $"Positions saved successfully." },
                { "icon", PackIconKind.SuccessBold }
            });
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK));
        }

        /// <summary>
        /// 构建当前工站的位置参数节点（JsonObject），供暂存或保存使用。
        /// 提取自 Save 方法的构建逻辑，避免重复代码。
        /// </summary>
        /// <returns>待保存的工站节点；若未选择工站则返回 null</returns>
        private JsonObject BuildStationNodeToSave()
        {
            if (string.IsNullOrEmpty(_currentStationIdentifier)) return null;

            var axes = _axisConfig.GetAxesForStation(_currentStationIdentifier).ToList();

            // 构建新的 Positions 节点
            var newPosObj = new JsonObject();
            foreach (DataRow row in PositionsTable.Rows)
            {
                var name = row["PositionName"].ToString();
                if (string.IsNullOrEmpty(name)) continue;

                // 保持 Axes 子对象格式，与配方文件结构一致
                var axesObj = new JsonObject();
                foreach (var axis in axes)
                {
                    var cellValue = row[axis.Name];
                    if (cellValue != DBNull.Value && cellValue != null)
                    {
                        axesObj[axis.Name] = Convert.ToDouble(cellValue);
                    }
                }

                var positionObj = new JsonObject();
                positionObj["Axes"] = axesObj;
                positionObj["Comment"] = row["Comment"]?.ToString() ?? "";
                newPosObj[name] = positionObj;
            }

            // 基于已加载的工站节点合并 Positions，避免保存前额外读盘
            JsonObject stationNodeToSave;
            try
            {
                stationNodeToSave = _currentStationNode != null
                    ? JsonNode.Parse(_currentStationNode.ToJsonString()).AsObject()
                    : CreateEmptyStationNode();
                stationNodeToSave["Positions"] = newPosObj;
            }
            catch (Exception ex)
            {
                _logger.Error($"构建工站保存节点失败: {ex.Message}，将仅保存位置数据");
                stationNodeToSave = CreateEmptyStationNode();
                stationNodeToSave["Positions"] = newPosObj;
            }

            return stationNodeToSave;
        }

        /// <summary>
        /// 将当前编辑的位置数据暂存到 RecipePoolService（同步操作，不涉及文件 I/O）。
        /// 由 SavePositionEditorEvent 触发，在保存池前调用，确保位置编辑器参数不丢失。
        /// </summary>
        private void StageCurrentPositions()
        {
            if (string.IsNullOrEmpty(_currentStationIdentifier)) return;

            var stationNodeToSave = BuildStationNodeToSave();
            if (stationNodeToSave == null) return;

            // 仅暂存到暂存区，由 SaveRecipePoolAsync 统一提交到文件
            _recipePoolService.StageStationParameters(_currentStationIdentifier, stationNodeToSave);
            _currentStationNode = stationNodeToSave;
            _logger.Info($"[{_currentStationIdentifier}] 位置参数已暂存（由 Save Pool 触发）");
        }

        /// <summary>
        /// SavePositionEditorEvent 事件处理：保存池前暂存当前位置编辑器参数。
        /// 参数为当前配方池名称，用于校验是否为当前激活的池。
        /// </summary>
        private void OnSavePositionEditorRequested(string poolName)
        {
            // 仅处理当前激活池的保存请求
            if (string.IsNullOrEmpty(poolName) || poolName != _recipePoolService.CurrentPoolName)
                return;

            try
            {
                StageCurrentPositions();
            }
            catch (Exception ex)
            {
                _logger.Error($"暂存位置编辑器参数失败: {ex.Message}");
            }
        }

        #endregion

        #region Events
        /// <summary>
        /// 配方切换事件处理，重新加载当前工站的位置数据
        /// </summary>
        private void OnRecipeChanged(string recipeName)
        {
            _ = LoadPositionsForCurrentStationAsync();
        }

        /// <summary>
        /// 配方池切换事件处理，重新加载当前工站的位置数据
        /// </summary>
        private void OnPoolChanged(string poolName)
        {
            _ = LoadPositionsForCurrentStationAsync();
        }
        #endregion

        #region IDialogAware
        public bool CanCloseDialog() => true;
        public void OnDialogClosed()
        {
            _recipeChangedToken?.Dispose();
            _poolChangedToken?.Dispose();
            _stationRegisteredToken?.Dispose();
            _savePositionEditorToken?.Dispose();
            if (_isMoving)
            {
                Stop();
                _isMoving = false;
            }
        }
        public void OnDialogOpened(IDialogParameters parameters) { }
        public event Action<IDialogResult> RequestClose;
        #endregion
    }

    /// <summary>
    /// 工站选择项模型，用于ComboBox展示
    /// </summary>
    public class StationItem
    {
        public string Identifier { get; set; }
        public string Name { get; set; }
        public string RecipeName { get; set; }
    }
}

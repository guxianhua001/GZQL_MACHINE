using Core.Abstraction;
using Core.Models;
using MotionControl.Interfaces;
using StationTasks.Models;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Recipe.Events;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace Module.ViewModels
{
    /// <summary>
    /// 工站选项项，用于 Station ComboBox 绑定
    /// </summary>
    public class StationItem
    {
        public string StationId { get; set; }
        public string DisplayName { get; set; }
    }

    /// <summary>
    /// GOTO 步骤详细配置 ViewModel，以模态弹窗形式展示
    /// 使用 SubMoveRowViewModel 为每行提供独立的轴/位置列表
    /// </summary>
    public class GotoDetailViewModel : BindableBase, IDialogCloseable
    {
        private readonly IPositionProvider _positionProvider;
        private readonly IRecipePoolService _recipePoolService;
        private readonly IStationRegistry _stationRegistry;
        private readonly IEventAggregator _eventAggregator;

        /// <summary> 全局变量变更防抖：方法4 IF/ELSE 循环中 VISION 频繁写 GV，避免 Ofs 列闪烁/丢失 </summary>
        private DispatcherTimer _globalVarReloadTimer;

        private ProcessStep _step;

        /// <summary>请求关闭对话框时触发</summary>
        public event Action<object> RequestClose;

        /// <summary>是否可以关闭对话框</summary>
        public bool CanCloseDialog() => true;

        /// <summary>
        /// 当前编辑的工艺步骤，设置时自动初始化子移动行列表
        /// </summary>
        public ProcessStep Step
        {
            get => _step;
            set
            {
                if (SetProperty(ref _step, value))
                    InitializeFromStep();
            }
        }

        /// <summary>
        /// 步骤描述信息，显示 ComponentFeature → SiteFeature
        /// </summary>
        public string StepDescription => _step == null ? "—" : $"Seq{_step.Seq} - {_step.CompFeature ?? "—"} → {_step.SiteFeature ?? "—"}";

        /// <summary>
        /// 是否为回零模式（基于 step.GotoMode，支持双向绑定）
        /// setter 由 RadioButton.IsChecked 双向绑定触发，自动切换 GotoMode
        /// </summary>
        public bool IsHomeMode
        {
            get => _step?.GotoMode == StationTasks.Models.GotoModeEnum.Home;
            set
            {
                if (_step == null) return;
                var newMode = value ? StationTasks.Models.GotoModeEnum.Home : StationTasks.Models.GotoModeEnum.Absolute;
                if (_step.GotoMode == newMode) return;
                _step.GotoMode = newMode;
                RaisePropertyChanged(nameof(IsHomeMode));
                RaisePropertyChanged(nameof(IsAbsoluteMode));
            }
        }

        /// <summary>绝对定位模式（支持双向绑定，与 IsHomeMode 互斥）</summary>
        public bool IsAbsoluteMode
        {
            get => _step?.GotoMode == StationTasks.Models.GotoModeEnum.Absolute;
            set
            {
                if (_step == null) return;
                var newMode = value ? StationTasks.Models.GotoModeEnum.Absolute : StationTasks.Models.GotoModeEnum.Home;
                if (_step.GotoMode == newMode) return;
                _step.GotoMode = newMode;
                RaisePropertyChanged(nameof(IsHomeMode));
                RaisePropertyChanged(nameof(IsAbsoluteMode));
            }
        }

        private ObservableCollection<SubMoveRowViewModel> _subMoveRows = new ObservableCollection<SubMoveRowViewModel>();
        /// <summary>
        /// 子移动行列表，每行包含独立的轴/位置选项
        /// </summary>
        public ObservableCollection<SubMoveRowViewModel> SubMoveRows
        {
            get => _subMoveRows;
            set => SetProperty(ref _subMoveRows, value ?? new ObservableCollection<SubMoveRowViewModel>());
        }

        private ObservableCollection<StationItem> _stationItems;
        /// <summary>
        /// 工站选项列表，从 IStationRegistry 动态获取
        /// </summary>
        public ObservableCollection<StationItem> StationItems
        {
            get => _stationItems;
            set => SetProperty(ref _stationItems, value);
        }

        private ObservableCollection<GlobalVariable> _globalVariables;
        /// <summary>
        /// 全局变量列表，从 IRecipePoolService 加载
        /// </summary>
        public ObservableCollection<GlobalVariable> GlobalVariables
        {
            get => _globalVariables;
            set => SetProperty(ref _globalVariables, value);
        }

        private ObservableCollection<string> _offsetVariableOptions;
        /// <summary>
        /// 偏移变量选项列表，包含 "Manual" 和全局变量名
        /// </summary>
        public ObservableCollection<string> OffsetVariableOptions
        {
            get => _offsetVariableOptions;
            set => SetProperty(ref _offsetVariableOptions, value);
        }

        /// <summary>
        /// 可链接到 Ofs 的全局变量列表（仅 Double 类型），供 GlobalVariableLinkControl 使用
        /// </summary>
        private ObservableCollection<GlobalVariable> _linkableOffsetVariables;
        public ObservableCollection<GlobalVariable> LinkableOffsetVariables
        {
            get => _linkableOffsetVariables;
            set => SetProperty(ref _linkableOffsetVariables, value);
        }

        private SubMoveRowViewModel _selectedSubMoveRow;
        /// <summary>
        /// 当前选中的子移动行
        /// </summary>
        public SubMoveRowViewModel SelectedSubMoveRow
        {
            get => _selectedSubMoveRow;
            set => SetProperty(ref _selectedSubMoveRow, value);
        }

        public ICommand AddSubMoveCommand { get; }
        public ICommand DeleteSubMoveCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        /// <summary> 关闭弹窗命令 </summary>
        public ICommand CloseCommand { get; }
        /// <summary> 保存并关闭弹窗命令 </summary>
        public ICommand SaveCommand { get; }
        /// <summary> 取消链接偏移变量命令 </summary>
        public ICommand UnlinkOffsetCommand { get; }

        public GotoDetailViewModel(
            IPositionProvider positionProvider,
            IRecipePoolService recipePoolService,
            IStationRegistry stationRegistry,
            IEventAggregator eventAggregator)
        {
            _positionProvider = positionProvider;
            _recipePoolService = recipePoolService;
            _stationRegistry = stationRegistry;
            _eventAggregator = eventAggregator;

            AddSubMoveCommand = new DelegateCommand(OnAddSubMove);
            DeleteSubMoveCommand = new DelegateCommand(OnDeleteSubMove, () => SelectedSubMoveRow != null)
                .ObservesProperty(() => SelectedSubMoveRow);
            MoveUpCommand = new DelegateCommand(OnMoveUp, () => SelectedSubMoveRow != null && SubMoveRows.IndexOf(SelectedSubMoveRow) > 0)
                .ObservesProperty(() => SelectedSubMoveRow);
            MoveDownCommand = new DelegateCommand(OnMoveDown, () => SelectedSubMoveRow != null && SubMoveRows.IndexOf(SelectedSubMoveRow) < SubMoveRows.Count - 1)
                .ObservesProperty(() => SelectedSubMoveRow);
            CloseCommand = new DelegateCommand(OnClose);
            SaveCommand = new DelegateCommand(OnSave);
            UnlinkOffsetCommand = new DelegateCommand(OnUnlinkOffset);

            // 订阅全局变量变更事件，使用 UIThread 确保在 UI 线程刷新显示
            _eventAggregator.GetEvent<GlobalVariablesChangedEvent>()
                .Subscribe(OnGlobalVariablesChanged, ThreadOption.UIThread);

            LoadStations();
            LoadGlobalVariablesAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 从 Step 初始化子移动行列表，为每行加载对应的轴/位置选项
        /// Home 模式下强制将所有 SubMove 的 PositionName 设为 "Home"
        /// </summary>
        private void InitializeFromStep()
        {
            if (_step == null) return;

            var subMoves = _step.SubMoves ?? new ObservableCollection<SubMove>();

            if (IsHomeMode)
            {
                foreach (var move in subMoves)
                    move.PositionName = "Home";
            }

            var rows = new ObservableCollection<SubMoveRowViewModel>();
            foreach (var move in subMoves)
            {
                var row = new SubMoveRowViewModel(move, _positionProvider);
                row.SetLinkableVariables(LinkableOffsetVariables);
                row.UpdateOffsetDisplayValue();
                rows.Add(row);
                if (!string.IsNullOrEmpty(move.StationId))
                    row.LoadAxesAndPositionsAsync(move.StationId).ConfigureAwait(false);
            }

            SubMoveRows = rows;
            RaisePropertyChanged(nameof(StepDescription));
            // 强制刷新模式属性，确保UI正确显示已保存的GotoMode
            RaisePropertyChanged(nameof(IsHomeMode));
            RaisePropertyChanged(nameof(IsAbsoluteMode));
        }

        /// <summary>
        /// 从 IStationRegistry 加载所有已注册工站到 StationItems 集合
        /// </summary>
        private void LoadStations()
        {
            var stations = _stationRegistry.GetAllStations();
            StationItems = new ObservableCollection<StationItem>(
                stations.Select(s => new StationItem
                {
                    StationId = s.StationIdentifier,
                    DisplayName = s.StationIdentifier
                }));
        }

        /// <summary>
        /// 从 IRecipePoolService 加载全局变量，筛选 Double 类型供 Ofs 链接使用。
        /// 已加载时就地更新 Value，避免 Clear 导致链接偏移量短暂丢失（方法4 循环场景）。
        /// </summary>
        private async Task LoadGlobalVariablesAsync()
        {
            try
            {
                var poolId = !string.IsNullOrEmpty(_recipePoolService.CurrentPoolName)
                    ? _recipePoolService.CurrentPoolName
                    : _recipePoolService.CurrentPoolId;
                if (string.IsNullOrEmpty(poolId)) return;

                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);

                // 就地刷新已有变量值，保持对象引用稳定，防止 Ofs 链接控件显示丢失
                if (GlobalVariables != null && GlobalVariables.Count > 0)
                {
                    foreach (var loaded in variables)
                    {
                        var existing = GlobalVariables.FirstOrDefault(v =>
                            string.Equals(v.Name, loaded.Name, StringComparison.OrdinalIgnoreCase));
                        if (existing != null)
                            existing.Value = loaded.Value;
                    }

                    if (LinkableOffsetVariables != null)
                    {
                        foreach (var loaded in variables.Where(v => v.Type == GlobalVariableType.Double))
                        {
                            var existing = LinkableOffsetVariables.FirstOrDefault(v =>
                                string.Equals(v.Name, loaded.Name, StringComparison.OrdinalIgnoreCase));
                            if (existing != null)
                                existing.Value = loaded.Value;
                        }
                    }

                    RefreshAllOffsetDisplayValues();
                    return;
                }

                GlobalVariables = new ObservableCollection<GlobalVariable>(variables);

                var doubleVars = variables
                    .Where(v => v.Type == GlobalVariableType.Double)
                    .ToList();

                UnsubscribeVariableValueChanges();

                if (LinkableOffsetVariables == null)
                    LinkableOffsetVariables = new ObservableCollection<GlobalVariable>(doubleVars);
                else
                {
                    LinkableOffsetVariables.Clear();
                    foreach (var v in doubleVars)
                        LinkableOffsetVariables.Add(v);
                }

                SubscribeVariableValueChanges();

                var options = new List<string> { "Manual" };
                options.AddRange(variables.Select(v => v.Name));
                OffsetVariableOptions = new ObservableCollection<string>(options);
            }
            catch
            {
                GlobalVariables = new ObservableCollection<GlobalVariable>();
                LinkableOffsetVariables = new ObservableCollection<GlobalVariable>();
                OffsetVariableOptions = new ObservableCollection<string> { "Manual" };
            }

            RefreshAllOffsetDisplayValues();
        }

        /// <summary>
        /// 订阅 Double 类型变量的 PropertyChanged 事件，值变化时实时刷新行显示
        /// </summary>
        private void SubscribeVariableValueChanges()
        {
            if (LinkableOffsetVariables == null) return;
            foreach (var v in LinkableOffsetVariables)
                v.PropertyChanged += OnGlobalVariablePropertyChanged;
        }

        /// <summary>
        /// 取消所有 Double 变量的 PropertyChanged 订阅
        /// </summary>
        private void UnsubscribeVariableValueChanges()
        {
            if (LinkableOffsetVariables == null) return;
            foreach (var v in LinkableOffsetVariables)
                v.PropertyChanged -= OnGlobalVariablePropertyChanged;
        }

        /// <summary>
        /// 全局变量 Value 属性变化时，刷新链接了该变量的行的 OffsetDisplayValue
        /// </summary>
        private void OnGlobalVariablePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(GlobalVariable.Value)) return;
            if (sender is GlobalVariable changedVar)
            {
                foreach (var row in SubMoveRows)
                {
                    if (string.Equals(row.OffsetVariableName, changedVar.Name, StringComparison.OrdinalIgnoreCase))
                        row.UpdateOffsetDisplayValue();
                }
            }
        }

        /// <summary>
        /// 刷新所有行的链接变量引用和显示值
        /// 用于变量加载完成后同步到已创建的 SubMoveRowViewModel
        /// </summary>
        private void RefreshAllOffsetDisplayValues()
        {
            foreach (var row in SubMoveRows)
            {
                row.SetLinkableVariables(LinkableOffsetVariables);
                row.UpdateOffsetDisplayValue();
            }
        }

        /// <summary>
        /// 全局变量变更回调：防抖后刷新，避免方法4 循环执行时 Ofs 列频繁 Clear 导致显示丢失
        /// </summary>
        private void OnGlobalVariablesChanged(string poolId)
        {
            if (_globalVarReloadTimer == null)
            {
                _globalVarReloadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                _globalVarReloadTimer.Tick += async (_, _) =>
                {
                    _globalVarReloadTimer.Stop();
                    await LoadGlobalVariablesAsync();
                };
            }

            _globalVarReloadTimer.Stop();
            _globalVarReloadTimer.Start();
        }

        /// <summary>
        /// 取消选中行的偏移变量链接
        /// </summary>
        private void OnUnlinkOffset()
        {
            if (SelectedSubMoveRow != null)
                SelectedSubMoveRow.OffsetVariableName = null;
        }

        /// <summary>
        /// 添加新的子移动行，默认选择第一个工站
        /// </summary>
        private void OnAddSubMove()
        {
            var newMove = new SubMove
            {
                SubSeq = $"{_step?.Seq}{(char)('a' + SubMoveRows.Count)}",
                StationId = StationItems?.FirstOrDefault()?.StationId ?? "",
                Axis = "",
                PositionName = IsHomeMode ? "Home" : "",
                Description = "",
                Offset = 0,
                OffsetVariableName = "",
                Speed = 10,
                HomeMode = 0,  // 默认使用卡内配置回零
                HomeMinVel = 5,
                HomeMaxVel = 10
            };
            var row = new SubMoveRowViewModel(newMove, _positionProvider);
            row.SetLinkableVariables(LinkableOffsetVariables);
            if (!string.IsNullOrEmpty(newMove.StationId))
                row.LoadAxesAndPositionsAsync(newMove.StationId).ConfigureAwait(false);
            SubMoveRows.Add(row);
        }

        private void OnDeleteSubMove()
        {
            if (SelectedSubMoveRow != null)
                SubMoveRows.Remove(SelectedSubMoveRow);
        }

        private void OnMoveUp()
        {
            int idx = SubMoveRows.IndexOf(SelectedSubMoveRow);
            if (idx > 0) SubMoveRows.Move(idx, idx - 1);
        }

        private void OnMoveDown()
        {
            int idx = SubMoveRows.IndexOf(SelectedSubMoveRow);
            if (idx < SubMoveRows.Count - 1) SubMoveRows.Move(idx, idx + 1);
        }

        /// <summary>
        /// 关闭弹窗
        /// </summary>
        private void OnClose()
        {
            RequestClose?.Invoke(false);
        }

        /// <summary>
        /// 保存当前子移动行列表到 Step 并关闭弹窗
        /// Home 模式下强制 PositionName = "Home"
        /// SiteFeature 由编程人员在UI上选择，不受模式影响
        /// </summary>
        private void OnSave()
        {
            if (_step != null)
            {
                var moves = SubMoveRows.Select(r => r.SubMove).ToList();
                if (IsHomeMode)
                {
                    foreach (var move in moves)
                        move.PositionName = "Home";
                }
                _step.SubMoves = new ObservableCollection<SubMove>(moves);
            }
            OnClose();
        }
    }
}

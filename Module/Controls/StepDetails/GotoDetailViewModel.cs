using Core.Abstraction;
using Core.Models;
using MotionControl.Interfaces;
using StationTasks.Models;
using Prism.Commands;
using Prism.Mvvm;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

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
    public class GotoDetailViewModel : BindableBase
    {
        private readonly IPositionProvider _positionProvider;
        private readonly IRecipePoolService _recipePoolService;
        private readonly IStationRegistry _stationRegistry;

        private ProcessStep _step;

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

        public GotoDetailViewModel(
            IPositionProvider positionProvider,
            IRecipePoolService recipePoolService,
            IStationRegistry stationRegistry)
        {
            _positionProvider = positionProvider;
            _recipePoolService = recipePoolService;
            _stationRegistry = stationRegistry;

            AddSubMoveCommand = new DelegateCommand(OnAddSubMove);
            DeleteSubMoveCommand = new DelegateCommand(OnDeleteSubMove, () => SelectedSubMoveRow != null)
                .ObservesProperty(() => SelectedSubMoveRow);
            MoveUpCommand = new DelegateCommand(OnMoveUp, () => SelectedSubMoveRow != null && SubMoveRows.IndexOf(SelectedSubMoveRow) > 0)
                .ObservesProperty(() => SelectedSubMoveRow);
            MoveDownCommand = new DelegateCommand(OnMoveDown, () => SelectedSubMoveRow != null && SubMoveRows.IndexOf(SelectedSubMoveRow) < SubMoveRows.Count - 1)
                .ObservesProperty(() => SelectedSubMoveRow);
            CloseCommand = new DelegateCommand(OnClose);
            SaveCommand = new DelegateCommand(OnSave);

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
        /// 从 IRecipePoolService 加载全局变量，构建偏移变量选项列表
        /// </summary>
        private async Task LoadGlobalVariablesAsync()
        {
            try
            {
                var poolId = _recipePoolService.CurrentPoolName;
                if (string.IsNullOrEmpty(poolId)) return;

                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
                GlobalVariables = new ObservableCollection<GlobalVariable>(variables);

                var options = new List<string> { "Manual" };
                options.AddRange(variables.Select(v => v.Name));
                OffsetVariableOptions = new ObservableCollection<string>(options);
            }
            catch
            {
                GlobalVariables = new ObservableCollection<GlobalVariable>();
                OffsetVariableOptions = new ObservableCollection<string> { "Manual" };
            }
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
                OffsetVariableName = "Manual",
                Speed = 10,
                HomeMode = 0,  // 默认使用卡内配置回零
                HomeMinVel = 5,
                HomeMaxVel = 20
            };
            var row = new SubMoveRowViewModel(newMove, _positionProvider);
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
            try
            {
                var session = MaterialDesignThemes.Wpf.DialogHost.GetDialogSession("MainDialogHost");
                session?.Close(false);
            }
            catch (InvalidOperationException) { }
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

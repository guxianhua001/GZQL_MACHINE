using Core.Models;
using Core.Services;
using Core.Utilities;
using MotionControl.Interfaces;
using StationTasks.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Core.Abstraction;
using Prism.Ioc;
using Recipe.Interfaces;
using Module.UserControls.Grippers;

namespace Module.ViewModels
{
    public class ReleaseDetailViewModel : BindableBase, INavigationAware, IDialogCloseable
    {
        private readonly IRegionManager _regionManager;
        private readonly IContainerProvider _containerProvider;
        private readonly IAxisConfigurationService _axisConfig;
        private readonly ILoggerService _logger;
        private readonly IGripperService _gripperService;
        private readonly IDialogService _dialogService;
        private readonly IBaseDialogService _baseDialogService;
        private readonly IPositionProvider _positionProvider;
        private readonly IStationRegistry _stationRegistry;
        private readonly IRecipePoolService _recipePoolService;
        private ProcessStep _step;

        /// <summary>请求关闭对话框时触发</summary>
        public event Action<object> RequestClose;

        /// <summary>是否可以关闭对话框</summary>
        public bool CanCloseDialog() => true;

        /// <summary> 当前编辑的工艺步骤，设置时自动初始化 ReleaseDetail 和 SubMoveRows </summary>
        public ProcessStep Step
        {
            get => _step;
            set
            {
                if (SetProperty(ref _step, value) && value != null)
                {
                    if (_step.ReleaseDetail == null)
                        _step.ReleaseDetail = new ReleaseDetail();
                    if (_step.ReleaseDetail.ReleaseMoves == null)
                        _step.ReleaseDetail.ReleaseMoves = new ObservableCollection<SubMove>();
                    InitializeSubMoveRows();
                    RaisePropertyChanged(nameof(VacuumPressure));
                    RaisePropertyChanged(nameof(ReleasePosition));
                    RaisePropertyChanged(nameof(ReleaseDelayTime));
                    RaisePropertyChanged(nameof(IsVacuumOn));
                    RaisePropertyChanged(nameof(ReleaseMoves));
                    RaisePropertyChanged(nameof(StepDescription));
                }
            }
        }

        public string StepDescription => _step == null ? "—" : $"Seq{_step.Seq} - {_step.CompFeature ?? "—"} → {_step.SiteFeature ?? "—"}";

        public int VacuumPressure
        {
            get => _step?.ReleaseDetail?.VacuumPressure ?? 0;
            set { if (_step?.ReleaseDetail != null) _step.ReleaseDetail.VacuumPressure = value; }
        }
        public double ReleasePosition
        {
            get => _step?.ReleaseDetail?.ReleasePosition ?? 0;
            set { if (_step?.ReleaseDetail != null) _step.ReleaseDetail.ReleasePosition = value; }
        }
        public int ReleaseDelayTime
        {
            get => _step?.ReleaseDetail?.ReleaseDelayTime ?? 300;
            set { if (_step?.ReleaseDetail != null) _step.ReleaseDetail.ReleaseDelayTime = value; }
        }
        public bool IsVacuumOn
        {
            get => _step?.ReleaseDetail?.IsVacuumOn ?? false;
            set { if (_step?.ReleaseDetail != null) _step.ReleaseDetail.IsVacuumOn = value; }
        }
        private string _vacuumStatusText;
        public string VacuumStatusText
        {
            get => _vacuumStatusText ?? (_vacuumStatusText = L("ReleaseDetail_VacuumStatus_Off"));
            set => SetProperty(ref _vacuumStatusText, value);
        }

        private System.Windows.Media.Brush _vacuumStatusBrush;
        /// <summary> 真空状态文字颜色，ON=绿色，OFF=灰色 </summary>
        public System.Windows.Media.Brush VacuumStatusBrush
        {
            get => _vacuumStatusBrush ?? (_vacuumStatusBrush = System.Windows.Media.Brushes.Gray);
            set => SetProperty(ref _vacuumStatusBrush, value);
        }

        public ObservableCollection<SubMove> ReleaseMoves
        {
            get
            {
                if (_step?.ReleaseDetail == null) return new ObservableCollection<SubMove>();
                if (_step.ReleaseDetail.ReleaseMoves == null)
                    _step.ReleaseDetail.ReleaseMoves = new ObservableCollection<SubMove>();
                return _step.ReleaseDetail.ReleaseMoves;
            }
        }

        private ObservableCollection<SubMoveRowViewModel> _subMoveRows;
        public ObservableCollection<SubMoveRowViewModel> SubMoveRows
        {
            get => _subMoveRows;
            set => SetProperty(ref _subMoveRows, value);
        }
        private SubMoveRowViewModel _selectedSubMoveRow;
        public SubMoveRowViewModel SelectedSubMoveRow
        {
            get => _selectedSubMoveRow;
            set => SetProperty(ref _selectedSubMoveRow, value);
        }
        private ObservableCollection<StationItem> _stationItems;
        public ObservableCollection<StationItem> StationItems
        {
            get => _stationItems;
            set => SetProperty(ref _stationItems, value);
        }

        private ObservableCollection<GlobalVariable> _availableGlobalVariables = new ObservableCollection<GlobalVariable>();
        /// <summary> 可选全局变量列表，供 Offset 列 ComboBox 绑定使用 </summary>
        public ObservableCollection<GlobalVariable> AvailableGlobalVariables
        {
            get => _availableGlobalVariables;
            set => SetProperty(ref _availableGlobalVariables, value);
        }

        public ICommand AddMoveCommand { get; }
        public ICommand DeleteMoveCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand VacuumOnCommand { get; }
        public ICommand VacuumOffCommand { get; }
        public ICommand QuickReleaseCommand { get; }
        public ICommand OpenGripperControlCommand { get; }

        /// <summary>
        /// 获取多语言文本（便捷方法）
        /// </summary>
        private string L(string key) => _containerProvider.Resolve<ILocalizationService>().GetResource(key);

        public ReleaseDetailViewModel(
            IRegionManager regionManager,
            IContainerProvider containerProvider,
            IAxisConfigurationService axisConfig,
            ILoggerService logger,
            IGripperService gripperService,
            IDialogService dialogService,
            IBaseDialogService baseDialogService,
            IPositionProvider positionProvider,
            IStationRegistry stationRegistry,
            IRecipePoolService recipePoolService)
        {
            _regionManager = regionManager;
            _containerProvider = containerProvider;
            _axisConfig = axisConfig;
            _logger = logger;
            _gripperService = gripperService;
            _dialogService = dialogService;
            _baseDialogService = baseDialogService;
            _positionProvider = positionProvider;
            _stationRegistry = stationRegistry;
            _recipePoolService = recipePoolService;

            AddMoveCommand = new DelegateCommand(OnAddSubMove);
            DeleteMoveCommand = new DelegateCommand(OnDeleteSubMove, () => SelectedSubMoveRow != null).ObservesProperty(() => SelectedSubMoveRow);
            MoveUpCommand = new DelegateCommand(OnMoveUp, () => SelectedSubMoveRow != null && SubMoveRows.IndexOf(SelectedSubMoveRow) > 0).ObservesProperty(() => SelectedSubMoveRow);
            MoveDownCommand = new DelegateCommand(OnMoveDown, () => SelectedSubMoveRow != null && SubMoveRows.IndexOf(SelectedSubMoveRow) < SubMoveRows.Count - 1).ObservesProperty(() => SelectedSubMoveRow);
            CloseCommand = new DelegateCommand(OnClose);
            SaveCommand = new DelegateCommand(OnSave);
            VacuumOnCommand = new DelegateCommand(() =>
            {
                IsVacuumOn = true;
                VacuumStatusText = L("ReleaseDetail_VacuumStatus_On");
                VacuumStatusBrush = System.Windows.Media.Brushes.Green;
            });
            VacuumOffCommand = new DelegateCommand(() =>
            {
                IsVacuumOn = false;
                VacuumStatusText = L("ReleaseDetail_VacuumStatus_Off");
                VacuumStatusBrush = System.Windows.Media.Brushes.Gray;
            });
            QuickReleaseCommand = new DelegateCommand(async () => await OnQuickReleaseAsync());
            OpenGripperControlCommand = new DelegateCommand(OnOpenGripperControl);

            LoadStations();
            LoadGlobalVariablesAsync().ConfigureAwait(false);
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
        /// 从 IRecipePoolService 加载全局变量列表，供 Offset 列 ComboBox 绑定
        /// </summary>
        private async Task LoadGlobalVariablesAsync()
        {
            try
            {
                var poolId = _recipePoolService.CurrentPoolName;
                if (string.IsNullOrEmpty(poolId))
                {
                    AvailableGlobalVariables = new ObservableCollection<GlobalVariable>();
                    return;
                }

                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
                AvailableGlobalVariables = new ObservableCollection<GlobalVariable>(variables);
            }
            catch
            {
                AvailableGlobalVariables = new ObservableCollection<GlobalVariable>();
            }
        }

        /// <summary>
        /// 从 Step.ReleaseDetail.ReleaseMoves 初始化 SubMoveRows，为每行加载轴/位置选项
        /// </summary>
        private void InitializeSubMoveRows()
        {
            if (_step?.ReleaseDetail?.ReleaseMoves == null) return;
            var rows = new ObservableCollection<SubMoveRowViewModel>();
            foreach (var move in _step.ReleaseDetail.ReleaseMoves)
            {
                var row = new SubMoveRowViewModel(move, _positionProvider);
                rows.Add(row);
                if (!string.IsNullOrEmpty(move.StationId))
                    row.LoadAxesAndPositionsAsync(move.StationId).ConfigureAwait(false);
            }
            SubMoveRows = rows;
        }

        /// <summary>
        /// 将 SubMoveRows 同步回 Step.ReleaseDetail.ReleaseMoves
        /// </summary>
        private void SyncRowsToStep()
        {
            if (_step?.ReleaseDetail == null) return;
            _step.ReleaseDetail.ReleaseMoves = new ObservableCollection<SubMove>(
                SubMoveRows.Select(r => r.SubMove));
        }

        private void OnAddSubMove()
        {
            var newMove = new SubMove
            {
                SubSeq = ((char)('a' + SubMoveRows.Count)).ToString(),
                Axis = "",
                PositionName = "",
                Offset = 0,
                OffsetVariableName = "",
                Speed = 50,
                Description = ""
            };
            var row = new SubMoveRowViewModel(newMove, _positionProvider);
            SubMoveRows.Add(row);
        }

        private void OnDeleteSubMove()
        {
            if (SelectedSubMoveRow != null)
                SubMoveRows.Remove(SelectedSubMoveRow);
            UpdateSequences();
        }

        private void OnMoveUp()
        {
            int idx = SubMoveRows.IndexOf(SelectedSubMoveRow);
            SubMoveRows.Move(idx, idx - 1);
            UpdateSequences();
        }

        private void OnMoveDown()
        {
            int idx = SubMoveRows.IndexOf(SelectedSubMoveRow);
            SubMoveRows.Move(idx, idx + 1);
            UpdateSequences();
        }

        private void UpdateSequences()
        {
            for (int i = 0; i < SubMoveRows.Count; i++)
                SubMoveRows[i].SubSeq = ((char)('a' + i)).ToString();
        }

        /// <summary>
        /// 关闭弹窗（DialogHost 模式）
        /// </summary>
        private void OnClose()
        {
            RequestClose?.Invoke(false);
        }

        /// <summary>
        /// 保存当前子移动行列表到 Step 并关闭弹窗
        /// </summary>
        private void OnSave()
        {
            SyncRowsToStep();
            OnClose();
        }

        private async Task OnQuickReleaseAsync()
        {
            var result = await ShowConfirmationAsync(
                L("ReleaseDetail_ConfirmRelease_Title"),
                string.Format(L("ReleaseDetail_ConfirmRelease_Msg"), ReleasePosition));
            if (result != ButtonResult.Yes) return;

            try
            {
                await _gripperService.ReleaseAsync(ReleasePosition);
                _logger.Info(string.Format(L("ReleaseDetail_Log_ReleaseDone"), ReleasePosition));
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(L("ReleaseDetail_Log_ReleaseFailed"), ex.Message));
                ShowAlert(L("ReleaseDetail_Alert_ReleaseFailed"), ex.Message);
            }
        }

        private async void OnOpenGripperControl()
        {
            // 通过容器解析 ViewModel，创建 View 并绑定
            var viewModel = _containerProvider.Resolve<GripperControlViewModel>();
            var view = new GripperControlView { DataContext = viewModel };

            // 初始化夹爪控制面板（传入外部位置参数，启动监控）
            viewModel.Initialize(null, ReleasePosition);

            // 使用 BaseDialogService 弹出，风格统一跟随主题
            var title = L("ElectricGripperManualOperation");
            await _baseDialogService.ShowDialog(view, title, "RobotIndustrial");

            // 关闭后回收资源
            viewModel.Dispose();
        }

        private async Task<ButtonResult> ShowConfirmationAsync(string title, string message)
        {
            var tcs = new TaskCompletionSource<ButtonResult>();
            _dialogService.ShowDialog("ConfirmationDialog", new DialogParameters
            {
                { "title", title },
                { "message", message }
            }, result => tcs.SetResult(result.Result));
            return await tcs.Task;
        }

        private void ShowAlert(string title, string message)
        {
            _dialogService.ShowDialog("NotificationDialog", new DialogParameters
            {
                { "title", title },
                { "message", message }
            }, result => { });
        }

        #region INavigationAware
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _step = navigationContext.Parameters.GetValue<ProcessStep>("step");
            if (_step == null) return;

            if (_step.ReleaseDetail == null)
                _step.ReleaseDetail = new ReleaseDetail();
            if (_step.ReleaseDetail.ReleaseMoves == null)
                _step.ReleaseDetail.ReleaseMoves = new ObservableCollection<SubMove>();

            InitializeSubMoveRows();

            RaisePropertyChanged(nameof(VacuumPressure));
            RaisePropertyChanged(nameof(ReleasePosition));
            RaisePropertyChanged(nameof(ReleaseDelayTime));
            RaisePropertyChanged(nameof(IsVacuumOn));
            RaisePropertyChanged(nameof(ReleaseMoves));
            RaisePropertyChanged(nameof(StepDescription));
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
        #endregion
    }
}

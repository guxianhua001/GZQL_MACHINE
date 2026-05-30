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

namespace Module.ViewModels
{
    public class CureDetailViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;
        private readonly IContainerProvider _containerProvider;
        private readonly IAxisConfigurationService _axisConfig;
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private readonly IPositionProvider _positionProvider;
        private readonly IStationRegistry _stationRegistry;
        private ProcessStep _step;

        public ProcessStep Step
        {
            get => _step;
            set
            {
                if (SetProperty(ref _step, value) && value != null)
                {
                    if (_step.CureDetail == null)
                        _step.CureDetail = new CureDetail();
                    if (_step.CureDetail.CureMoves == null)
                        _step.CureDetail.CureMoves = new ObservableCollection<SubMove>();
                    InitializeSubMoveRows();
                    RaisePropertyChanged(nameof(UvHeadIndex));
                    RaisePropertyChanged(nameof(CureTimeMs));
                    RaisePropertyChanged(nameof(Stage1DurationMs));
                    RaisePropertyChanged(nameof(Stage1Intensity));
                    RaisePropertyChanged(nameof(Stage2DurationMs));
                    RaisePropertyChanged(nameof(Stage2Intensity));
                    RaisePropertyChanged(nameof(Stage3DurationMs));
                    RaisePropertyChanged(nameof(Stage3Intensity));
                    RaisePropertyChanged(nameof(Stage4DurationMs));
                    RaisePropertyChanged(nameof(Stage4Intensity));
                    RaisePropertyChanged(nameof(UvHead1DoPort));
                    RaisePropertyChanged(nameof(UvHead2DoPort));
                    RaisePropertyChanged(nameof(IsHead1Selected));
                    RaisePropertyChanged(nameof(IsHead2Selected));
                    RaisePropertyChanged(nameof(CureMoves));
                    RaisePropertyChanged(nameof(StepDescription));
                }
            }
        }

        public string StepDescription => _step == null ? "—" : $"Seq{_step.Seq} - {_step.CompFeature ?? "—"} → {_step.SiteFeature ?? "—"}";

        public int UvHeadIndex
        {
            get => _step?.CureDetail?.UvHeadIndex ?? 1;
            set { if (_step?.CureDetail != null) _step.CureDetail.UvHeadIndex = value; }
        }
        public bool IsHead1Selected
        {
            get => UvHeadIndex == 1;
            set { if (value) UvHeadIndex = 1; }
        }
        public bool IsHead2Selected
        {
            get => UvHeadIndex == 2;
            set { if (value) UvHeadIndex = 2; }
        }
        public int CureTimeMs
        {
            get => _step?.CureDetail?.CureTimeMs ?? 5000;
            set { if (_step?.CureDetail != null) _step.CureDetail.CureTimeMs = value; }
        }
        public int Stage1DurationMs
        {
            get => _step?.CureDetail?.Stage1DurationMs ?? 1000;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage1DurationMs = value; }
        }
        public double Stage1Intensity
        {
            get => _step?.CureDetail?.Stage1Intensity ?? 50.0;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage1Intensity = value; }
        }
        public int Stage2DurationMs
        {
            get => _step?.CureDetail?.Stage2DurationMs ?? 1000;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage2DurationMs = value; }
        }
        public double Stage2Intensity
        {
            get => _step?.CureDetail?.Stage2Intensity ?? 80.0;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage2Intensity = value; }
        }
        public int Stage3DurationMs
        {
            get => _step?.CureDetail?.Stage3DurationMs ?? 1000;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage3DurationMs = value; }
        }
        public double Stage3Intensity
        {
            get => _step?.CureDetail?.Stage3Intensity ?? 100.0;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage3Intensity = value; }
        }
        public int Stage4DurationMs
        {
            get => _step?.CureDetail?.Stage4DurationMs ?? 2000;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage4DurationMs = value; }
        }
        public double Stage4Intensity
        {
            get => _step?.CureDetail?.Stage4Intensity ?? 80.0;
            set { if (_step?.CureDetail != null) _step.CureDetail.Stage4Intensity = value; }
        }
        /// <summary>
        /// 固化头1的DO输出端口
        /// </summary>
        public int UvHead1DoPort
        {
            get => _step?.CureDetail?.UvHead1DoPort ?? 1;
            set { if (_step?.CureDetail != null) _step.CureDetail.UvHead1DoPort = value; }
        }
        /// <summary>
        /// 固化头2的DO输出端口
        /// </summary>
        public int UvHead2DoPort
        {
            get => _step?.CureDetail?.UvHead2DoPort ?? 2;
            set { if (_step?.CureDetail != null) _step.CureDetail.UvHead2DoPort = value; }
        }

        public ObservableCollection<SubMove> CureMoves
        {
            get
            {
                if (_step?.CureDetail == null) return new ObservableCollection<SubMove>();
                if (_step.CureDetail.CureMoves == null)
                    _step.CureDetail.CureMoves = new ObservableCollection<SubMove>();
                return _step.CureDetail.CureMoves;
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

        public ICommand AddMoveCommand { get; }
        public ICommand DeleteMoveCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand CloseCommand { get; }
        public ICommand SaveCommand { get; }

        public CureDetailViewModel(
            IRegionManager regionManager,
            IContainerProvider containerProvider,
            IAxisConfigurationService axisConfig,
            ILoggerService logger,
            IDialogService dialogService,
            IPositionProvider positionProvider,
            IStationRegistry stationRegistry)
        {
            _regionManager = regionManager;
            _containerProvider = containerProvider;
            _axisConfig = axisConfig;
            _logger = logger;
            _dialogService = dialogService;
            _positionProvider = positionProvider;
            _stationRegistry = stationRegistry;

            AddMoveCommand = new DelegateCommand(OnAddSubMove);
            DeleteMoveCommand = new DelegateCommand(OnDeleteSubMove, () => SelectedSubMoveRow != null).ObservesProperty(() => SelectedSubMoveRow);
            MoveUpCommand = new DelegateCommand(OnMoveUp, () => SelectedSubMoveRow != null && SubMoveRows.IndexOf(SelectedSubMoveRow) > 0).ObservesProperty(() => SelectedSubMoveRow);
            MoveDownCommand = new DelegateCommand(OnMoveDown, () => SelectedSubMoveRow != null && SubMoveRows.IndexOf(SelectedSubMoveRow) < SubMoveRows.Count - 1).ObservesProperty(() => SelectedSubMoveRow);
            CloseCommand = new DelegateCommand(OnClose);
            SaveCommand = new DelegateCommand(OnSave);

            LoadStations();
        }

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

        private void InitializeSubMoveRows()
        {
            if (_step?.CureDetail?.CureMoves == null) return;
            var rows = new ObservableCollection<SubMoveRowViewModel>();
            foreach (var move in _step.CureDetail.CureMoves)
            {
                var row = new SubMoveRowViewModel(move, _positionProvider);
                rows.Add(row);
                if (!string.IsNullOrEmpty(move.StationId))
                    row.LoadAxesAndPositionsAsync(move.StationId).ConfigureAwait(false);
            }
            SubMoveRows = rows;
        }

        private void SyncRowsToStep()
        {
            if (_step?.CureDetail == null) return;
            _step.CureDetail.CureMoves = new ObservableCollection<SubMove>(
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

        private void OnClose()
        {
            try
            {
                var session = MaterialDesignThemes.Wpf.DialogHost.GetDialogSession("MainDialogHost");
                session?.Close(false);
            }
            catch (InvalidOperationException) { }
        }

        private void OnSave()
        {
            SyncRowsToStep();
            OnClose();
        }

        #region INavigationAware
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _step = navigationContext.Parameters.GetValue<ProcessStep>("step");
            if (_step == null) return;

            if (_step.CureDetail == null)
                _step.CureDetail = new CureDetail();
            if (_step.CureDetail.CureMoves == null)
                _step.CureDetail.CureMoves = new ObservableCollection<SubMove>();

            InitializeSubMoveRows();

            RaisePropertyChanged(nameof(UvHeadIndex));
            RaisePropertyChanged(nameof(CureTimeMs));
            RaisePropertyChanged(nameof(Stage1DurationMs));
            RaisePropertyChanged(nameof(Stage1Intensity));
            RaisePropertyChanged(nameof(Stage2DurationMs));
            RaisePropertyChanged(nameof(Stage2Intensity));
            RaisePropertyChanged(nameof(Stage3DurationMs));
            RaisePropertyChanged(nameof(Stage3Intensity));
            RaisePropertyChanged(nameof(Stage4DurationMs));
            RaisePropertyChanged(nameof(Stage4Intensity));
            RaisePropertyChanged(nameof(UvHead1DoPort));
            RaisePropertyChanged(nameof(UvHead2DoPort));
            RaisePropertyChanged(nameof(IsHead1Selected));
            RaisePropertyChanged(nameof(IsHead2Selected));
            RaisePropertyChanged(nameof(CureMoves));
            RaisePropertyChanged(nameof(StepDescription));
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
        #endregion
    }
}

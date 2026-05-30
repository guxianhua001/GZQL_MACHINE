using Core.Models;
using Core.Services;
using Core.Utilities;
using StationTasks.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Module.ViewModels
{
    public class AlignDetailViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;
        private IRegion _currentRegion;
        //private readonly IStationMetadataProvider _stationMetadataProvider;
        private readonly IRecipeServiceFactory _recipeServiceFactory;
        private readonly ILoggerService _logger;
        private readonly IAxisConfigurationService _axisConfig;
        private object _currentView;
        private ProcessStep _step;

        public string StepDescription => _step == null ? "— → —" : $"{_step.CompFeature ?? "—"} → {_step.SiteFeature ?? "—"}";

        private ObservableCollection<SubMove> _subMoves = new ObservableCollection<SubMove>();
        public ObservableCollection<SubMove> SubMoves
        {
            get => _subMoves;
            set => SetProperty(ref _subMoves, value ?? new ObservableCollection<SubMove>());
        }

        //private ObservableCollection<StationInfo> _stations;
        //private StationInfo _selectedStation;
        private ObservableCollection<string> _availableAxes;
        private ObservableCollection<string> _availablePositions;
        private Dictionary<string, FlexiblePosition> _currentPositionsDict = new();

        //public ObservableCollection<StationInfo> Stations
        //{
        //    get => _stations;
        //    set => SetProperty(ref _stations, value);
        //}

        //public StationInfo SelectedStation
        //{
        //    get => _selectedStation;
        //    set
        //    {
        //        if (SetProperty(ref _selectedStation, value))
        //            LoadPositionsForStationAsync().ConfigureAwait(false);
        //    }
        //}

        public ObservableCollection<string> AvailableAxes
        {
            get => _availableAxes;
            set => SetProperty(ref _availableAxes, value);
        }

        public ObservableCollection<string> AvailablePositions
        {
            get => _availablePositions;
            set => SetProperty(ref _availablePositions, value);
        }

        private SubMove _selectedSubMove;
        public SubMove SelectedSubMove
        {
            get => _selectedSubMove;
            set => SetProperty(ref _selectedSubMove, value);
        }

        public ICommand AddSubMoveCommand { get; }
        public ICommand DeleteSubMoveCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand CloseCommand { get; }

        public AlignDetailViewModel(
            IRegionManager regionManager,
            IRecipeServiceFactory recipeServiceFactory,
            IAxisConfigurationService axisConfig,
            ILoggerService logger)
        {
            _regionManager = regionManager;
            //_stationMetadataProvider = stationMetadataProvider;
            _recipeServiceFactory = recipeServiceFactory;
            _axisConfig = axisConfig;
            _logger = logger;

            SubMoves.CollectionChanged += SubMoves_CollectionChanged;

            AddSubMoveCommand = new DelegateCommand(OnAddSubMove);
            DeleteSubMoveCommand = new DelegateCommand(OnDeleteSubMove, () => SelectedSubMove != null)
                .ObservesProperty(() => SelectedSubMove);
            MoveUpCommand = new DelegateCommand(OnMoveUp, () => SelectedSubMove != null && SubMoves.IndexOf(SelectedSubMove) > 0)
                .ObservesProperty(() => SelectedSubMove);
            MoveDownCommand = new DelegateCommand(OnMoveDown, () => SelectedSubMove != null && SubMoves.IndexOf(SelectedSubMove) < SubMoves.Count - 1)
                .ObservesProperty(() => SelectedSubMove);
            CloseCommand = new DelegateCommand(OnClose);

            InitializeStations();
        }

        private void InitializeStations()
        {
            //var allStations = _stationMetadataProvider.GetAllStations();
            //Stations = new ObservableCollection<StationInfo>(allStations);
            //if (Stations.Any())
            //    SelectedStation = Stations.First();
        }

        private void OnAddSubMove()
        {
            var newMove = new SubMove
            {
                SubSeq = $"{_step?.Seq}{(char)('a' + SubMoves.Count)}",
                Axis = AvailableAxes?.FirstOrDefault() ?? "",
                PositionName = "",
                Description = "",
                Offset = 0,
                Speed = 20
            };
            SubMoves.Add(newMove);
        }

        private void OnDeleteSubMove()
        {
            if (SelectedSubMove != null)
                SubMoves.Remove(SelectedSubMove);
        }

        private void OnMoveUp()
        {
            int idx = SubMoves.IndexOf(SelectedSubMove);
            if (idx > 0) SubMoves.Move(idx, idx - 1);
        }

        private void OnMoveDown()
        {
            int idx = SubMoves.IndexOf(SelectedSubMove);
            if (idx < SubMoves.Count - 1) SubMoves.Move(idx, idx + 1);
        }

        private void OnClose()
        {
            SubMoves.CollectionChanged -= SubMoves_CollectionChanged;
            foreach (var move in SubMoves)
                move.PropertyChanged -= SubMove_PropertyChanged;
            if (_currentRegion != null && _currentView != null)
                _currentRegion.Remove(_currentView);
        }

        private async Task LoadPositionsForStationAsync()
        {
            //if (SelectedStation == null) return;
            try
            {
                //var recipeService = _recipeServiceFactory.Create(SelectedStation.Identifier, SelectedStation.Name);
                //await recipeService.InitializationTask;
                //await recipeService.LoadRecipeParameters(recipeService.CurrentRecipePoolName, recipeService.CurrentRecipeName);
                //var paramObj = recipeService.Parameters;
                //var positionsDict = GetPositionsDictionary(paramObj);
                //if (positionsDict != null)
                //{
                //    _currentPositionsDict = positionsDict;
                //    AvailablePositions = new ObservableCollection<string>(positionsDict.Keys);
                //}
                //else
                //{
                //    AvailablePositions = new ObservableCollection<string>();
                //}

                //var axes = _axisConfig.GetAxesForStation(SelectedStation.Identifier).Select(a => a.Name).ToList();
                //AvailableAxes = new ObservableCollection<string>(axes);
            }
            catch (Exception ex)
            {
                //_logger.Error($"加载工站 '{SelectedStation.Name}' 轴/位置列表失败: {ex.Message}");
                AvailableAxes = new ObservableCollection<string>();
                AvailablePositions = new ObservableCollection<string>();
            }
        }

        private Dictionary<string, FlexiblePosition> GetPositionsDictionary(object paramObj)
        {
            var prop = paramObj.GetType().GetProperty("Positions");
            if (prop != null && prop.PropertyType == typeof(Dictionary<string, FlexiblePosition>))
                return prop.GetValue(paramObj) as Dictionary<string, FlexiblePosition>;
            return null;
        }

        private void SubMoves_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (SubMove item in e.NewItems)
                    item.PropertyChanged += SubMove_PropertyChanged;
            }
            if (e.OldItems != null)
            {
                foreach (SubMove item in e.OldItems)
                    item.PropertyChanged -= SubMove_PropertyChanged;
            }
        }

        private void SubMove_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SubMove.PositionName))
            {
                var move = sender as SubMove;
                if (move != null && !string.IsNullOrEmpty(move.PositionName) && _currentPositionsDict.TryGetValue(move.PositionName, out var pos))
                {
                    move.Description = pos.Comment ?? "";
                }
                else
                {
                    move.Description = "";
                }
            }
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _step = navigationContext.Parameters.GetValue<ProcessStep>("step");
            SubMoves = _step?.SubMoves ?? new ObservableCollection<SubMove>();
            SubMoves.CollectionChanged += SubMoves_CollectionChanged;
            foreach (var move in SubMoves)
                move.PropertyChanged += SubMove_PropertyChanged;
            RaisePropertyChanged(nameof(StepDescription));
            //if (SelectedStation != null)
                LoadPositionsForStationAsync().ConfigureAwait(false);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
    }
}
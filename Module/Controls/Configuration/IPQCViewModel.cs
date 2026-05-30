using Core.Models;
using StationTasks.Models;
using Module.Views;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace Module.ViewModels
{
    public class IPQCViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;
        private IRegion _currentRegion;
        private object _currentView;

        // ----- IPQC 参数配置 -----
        private ObservableCollection<string> _sites;
        private string _selectedSite;
        private ObservableCollection<string> _checkTypes;
        private string _selectedCheckType;
        private ObservableCollection<string> _recipes;
        private string _selectedRecipe;
        private ObservableCollection<string> _cameras;
        private string _selectedCamera;
        private double _toleranceXY;
        private double _toleranceZ;
        private int _maxRetries;

        public ObservableCollection<string> Sites { get => _sites; set => SetProperty(ref _sites, value); }
        public string SelectedSite { get => _selectedSite; set => SetProperty(ref _selectedSite, value); }
        public ObservableCollection<string> CheckTypes { get => _checkTypes; set => SetProperty(ref _checkTypes, value); }
        public string SelectedCheckType { get => _selectedCheckType; set => SetProperty(ref _selectedCheckType, value); }
        public ObservableCollection<string> Recipes { get => _recipes; set => SetProperty(ref _recipes, value); }
        public string SelectedRecipe { get => _selectedRecipe; set => SetProperty(ref _selectedRecipe, value); }
        public ObservableCollection<string> Cameras { get => _cameras; set => SetProperty(ref _cameras, value); }
        public string SelectedCamera { get => _selectedCamera; set => SetProperty(ref _selectedCamera, value); }
        public double ToleranceXY { get => _toleranceXY; set => SetProperty(ref _toleranceXY, value); }
        public double ToleranceZ { get => _toleranceZ; set => SetProperty(ref _toleranceZ, value); }
        public int MaxRetries { get => _maxRetries; set => SetProperty(ref _maxRetries, value); }

        // ----- 运动流程表格 -----
        private ObservableCollection<InspectionMove> _inspectionMoves;
        private InspectionMove _selectedInspectionMove;
        private ObservableCollection<string> _availableAxes;

        public ObservableCollection<InspectionMove> InspectionMoves
        {
            get => _inspectionMoves;
            set => SetProperty(ref _inspectionMoves, value);
        }
        public InspectionMove SelectedInspectionMove
        {
            get => _selectedInspectionMove;
            set => SetProperty(ref _selectedInspectionMove, value);
        }
        public ObservableCollection<string> AvailableAxes
        {
            get => _availableAxes;
            set => SetProperty(ref _availableAxes, value);
        }

        // ----- 检测结果统计 -----
        private int _totalAssemblies;
        private int _passCount;
        private int _failCount;
        private string _lastMeasureTime;
        private ObservableCollection<MeasurementDataRecord> _measurementData;

        public int TotalAssemblies { get => _totalAssemblies; set => SetProperty(ref _totalAssemblies, value); }
        public int PassCount { get => _passCount; set => SetProperty(ref _passCount, value); }
        public int FailCount { get => _failCount; set => SetProperty(ref _failCount, value); }
        public string LastMeasureTime { get => _lastMeasureTime; set => SetProperty(ref _lastMeasureTime, value); }
        public ObservableCollection<MeasurementDataRecord> MeasurementData
        {
            get => _measurementData;
            set => SetProperty(ref _measurementData, value);
        }

        // ----- 命令 -----
        public ICommand AddMoveCommand { get; }
        public ICommand DeleteMoveCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand RunInspectionCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand CloseCommand { get; }

        public IPQCViewModel(IRegionManager regionManager)
        {
            _regionManager = regionManager;

            // 初始化下拉列表
            Sites = new ObservableCollection<string> { "ASSY-001", "ASSY-002", "ASSY-003" };
            CheckTypes = new ObservableCollection<string> { "Pre-assembly check", "组装后check", "点胶check" };
            Recipes = new ObservableCollection<string> { "Final_v2.0", "Precision_v1.2" };
            Cameras = new ObservableCollection<string> { "Top Camera", "Bottom Camera", "Side Camera" };
            AvailableAxes = new ObservableCollection<string> { "X", "Y", "Z", "Rx", "Ry", "Rz" };

            SelectedSite = Sites.FirstOrDefault();
            SelectedCheckType = CheckTypes.FirstOrDefault();
            SelectedRecipe = Recipes.FirstOrDefault();
            SelectedCamera = Cameras.FirstOrDefault();
            ToleranceXY = 10.0;
            ToleranceZ = 5.0;
            MaxRetries = 3;

            // 初始化运动步骤
            InspectionMoves = new ObservableCollection<InspectionMove>();
            // 添加两条示例步骤
            InspectionMoves.Add(new InspectionMove { SubSeq = "a", Axis = "X", Offset = 100.5, Speed = 30, ActionType = "Move", Description = "Move to first pillar" });
            InspectionMoves.Add(new InspectionMove { SubSeq = "b", Axis = "Z", Offset = -5.0, Speed = 10, ActionType = "Measure", Description = "Lower and measure height" });

            // 初始化示例测量数据（参考图片样式）
            LoadSampleMeasurementData();

            // 命令绑定
            AddMoveCommand = new DelegateCommand(OnAddMove);
            DeleteMoveCommand = new DelegateCommand(OnDeleteMove, () => SelectedInspectionMove != null).ObservesProperty(() => SelectedInspectionMove);
            MoveUpCommand = new DelegateCommand(OnMoveUp, () => SelectedInspectionMove != null && InspectionMoves.IndexOf(SelectedInspectionMove) > 0).ObservesProperty(() => SelectedInspectionMove);
            MoveDownCommand = new DelegateCommand(OnMoveDown, () => SelectedInspectionMove != null && InspectionMoves.IndexOf(SelectedInspectionMove) < InspectionMoves.Count - 1).ObservesProperty(() => SelectedInspectionMove);
            RunInspectionCommand = new DelegateCommand(OnRunInspection);
            ExportCsvCommand = new DelegateCommand(OnExportCsv);
            CloseCommand = new DelegateCommand(OnClose);
        }

        private void LoadSampleMeasurementData()
        {
            MeasurementData = new ObservableCollection<MeasurementDataRecord>
            {
                new MeasurementDataRecord
                {
                    Timestamp = "2025-12-12 19:00:00",
                    AssemblyId = "ASSY-D-000003",
                    Status = "PASS",
                    Operator = "mortzea",
                    Recipe = "Final_v2.0",
                    TotalCycleTime = 198.9,
                    ActuatorSeq = 1,
                    ActuatorId = "ACT-SN-E01",
                    Pillar1XY = 27.1,
                    Pillar2XY = -15.3,
                    ZPosition = 9.1,
                    Parallelism = -4.8,
                    Engagement = 0.015,
                    ZPeakForce = 1.81,
                    RadialForceX = 0.68,
                    RadialForceY = 0.65
                },
                new MeasurementDataRecord
                {
                    Timestamp = "2025-12-12 19:00:00",
                    AssemblyId = "ASSY-D-000003",
                    Status = "PASS",
                    Operator = "mortzea",
                    Recipe = "Final_v2.0",
                    TotalCycleTime = 198.9,
                    ActuatorSeq = 2,
                    ActuatorId = "ACT-SN-E02",
                    Pillar1XY = 27.0,
                    Pillar2XY = -11.2,
                    ZPosition = -2.5,
                    Parallelism = -5.5,
                    Engagement = 0.019,
                    ZPeakForce = 1.79,
                    RadialForceX = 0.66,
                    RadialForceY = 0.64
                },
                new MeasurementDataRecord
                {
                    Timestamp = "2025-12-12 19:00:00",
                    AssemblyId = "ASSY-D-000003",
                    Status = "FAIL",
                    Operator = "mortzea",
                    Recipe = "Final_v2.0",
                    TotalCycleTime = 198.9,
                    ActuatorSeq = 4,
                    ActuatorId = "ACT-SN-F01",
                    Pillar1XY = 28.5,
                    Pillar2XY = -14.2,
                    ZPosition = -4.0,
                    Parallelism = 0.028,
                    Engagement = 2.15,
                    ZPeakForce = 0.95,
                    RadialForceX = 0.70
                }
            };

            // 计算统计数据
            TotalAssemblies = MeasurementData.Select(m => m.AssemblyId).Distinct().Count();
            PassCount = MeasurementData.Count(m => m.Status == "PASS");
            FailCount = MeasurementData.Count(m => m.Status == "FAIL");
            LastMeasureTime = MeasurementData.Max(m => m.Timestamp);
        }

        private void OnAddMove()
        {
            var newMove = new InspectionMove
            {
                SubSeq = ((char)('a' + InspectionMoves.Count)).ToString(),
                Axis = AvailableAxes.FirstOrDefault() ?? "X",
                Offset = 0,
                Speed = 50,
                ActionType = "Move",
                Description = ""
            };
            InspectionMoves.Add(newMove);
            UpdateSequences();
        }

        private void OnDeleteMove()
        {
            if (SelectedInspectionMove != null)
                InspectionMoves.Remove(SelectedInspectionMove);
            UpdateSequences();
        }

        private void OnMoveUp()
        {
            int idx = InspectionMoves.IndexOf(SelectedInspectionMove);
            InspectionMoves.Move(idx, idx - 1);
            UpdateSequences();
        }

        private void OnMoveDown()
        {
            int idx = InspectionMoves.IndexOf(SelectedInspectionMove);
            InspectionMoves.Move(idx, idx + 1);
            UpdateSequences();
        }

        private void UpdateSequences()
        {
            for (int i = 0; i < InspectionMoves.Count; i++)
                InspectionMoves[i].SubSeq = ((char)('a' + i)).ToString();
        }

        private void OnRunInspection()
        {
            // 实际执行检查逻辑：根据配置的运动步骤控制硬件，获取测量数据
            // 这里模拟添加一条新记录
            var newRecord = new MeasurementDataRecord
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                AssemblyId = $"ASSY-{DateTime.Now:yyyyMMddHHmmss}",
                Status = new Random().Next(0, 2) == 0 ? "PASS" : "FAIL",
                Operator = Environment.UserName,
                Recipe = SelectedRecipe,
                TotalCycleTime = 210.5,
                ActuatorSeq = MeasurementData.Count + 1,
                ActuatorId = $"ACT-{Guid.NewGuid().ToString().Substring(0, 8)}",
                Pillar1XY = 27.5,
                Pillar2XY = -12.0,
                ZPosition = 1.2,
                Parallelism = -5.1,
                Engagement = 0.022,
                ZPeakForce = 1.85,
                RadialForceX = 0.69,
                RadialForceY = 0.67
            };
            MeasurementData.Insert(0, newRecord);
            // 更新统计
            TotalAssemblies = MeasurementData.Select(m => m.AssemblyId).Distinct().Count();
            PassCount = MeasurementData.Count(m => m.Status == "PASS");
            FailCount = MeasurementData.Count(m => m.Status == "FAIL");
            LastMeasureTime = newRecord.Timestamp;
        }

        private void OnExportCsv()
        {
            // 导出 MeasurementData 为 CSV 文件
            // 实际实现使用 SaveFileDialog 和 CSV 写入
        }

        private void OnClose()
        {
            if (_currentRegion != null && _currentView != null)
                _currentRegion.Remove(_currentView);
        }

        #region INavigationAware
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _currentRegion = navigationContext.NavigationService.Region;
            _currentView = navigationContext.NavigationService.Region.Views.FirstOrDefault(v => v is IPQCView);
            if (_currentView == null)
                _currentView = this;
        }
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
        #endregion
    }

    // 运动步骤模型（扩展 SubMove，增加 ActionType）
    public class InspectionMove : SubMove
    {
        private string _actionType;
        public string ActionType
        {
            get => _actionType;
            set => SetProperty(ref _actionType, value);
        }
    }

    // 测量记录模型
    public class MeasurementDataRecord : BindableBase
    {
        private string _timestamp;
        private string _assemblyId;
        private string _status;
        private string _operator;
        private string _recipe;
        private double _totalCycleTime;
        private int _actuatorSeq;
        private string _actuatorId;
        private double _pillar1XY;
        private double _pillar2XY;
        private double _zPosition;
        private double _parallelism;
        private double _engagement;
        private double _zPeakForce;
        private double _radialForceX;
        private double _radialForceY;

        public string Timestamp { get => _timestamp; set => SetProperty(ref _timestamp, value); }
        public string AssemblyId { get => _assemblyId; set => SetProperty(ref _assemblyId, value); }
        public string Status { get => _status; set => SetProperty(ref _status, value); }
        public string Operator { get => _operator; set => SetProperty(ref _operator, value); }
        public string Recipe { get => _recipe; set => SetProperty(ref _recipe, value); }
        public double TotalCycleTime { get => _totalCycleTime; set => SetProperty(ref _totalCycleTime, value); }
        public int ActuatorSeq { get => _actuatorSeq; set => SetProperty(ref _actuatorSeq, value); }
        public string ActuatorId { get => _actuatorId; set => SetProperty(ref _actuatorId, value); }
        public double Pillar1XY { get => _pillar1XY; set => SetProperty(ref _pillar1XY, value); }
        public double Pillar2XY { get => _pillar2XY; set => SetProperty(ref _pillar2XY, value); }
        public double ZPosition { get => _zPosition; set => SetProperty(ref _zPosition, value); }
        public double Parallelism { get => _parallelism; set => SetProperty(ref _parallelism, value); }
        public double Engagement { get => _engagement; set => SetProperty(ref _engagement, value); }
        public double ZPeakForce { get => _zPeakForce; set => SetProperty(ref _zPeakForce, value); }
        public double RadialForceX { get => _radialForceX; set => SetProperty(ref _radialForceX, value); }
        public double RadialForceY { get => _radialForceY; set => SetProperty(ref _radialForceY, value); }
    }
}
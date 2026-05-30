using Core.Models;
using Core.Services;
using Core.Utilities;
using Microsoft.Win32;
using StationTasks.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Module.ViewModels
{
    public class Camera2DViewModel : BindableBase, INavigationAware
    {
        private readonly IRegionManager _regionManager;
        private IRegion _currentRegion;
        private readonly ICamera2DService _camera2DService; // 模拟2D相机服务
        private readonly ILoggerService _logger;
        private object _currentView;
        private ProcessStep _step;

        public string StepDescription => _step?.Seq.ToString() ?? "?";

        // 相机和部件列表
        private ObservableCollection<string> _cameraList;
        private ObservableCollection<string> _slotList;
        private string _selectedCamera;
        private string _selectedSlot;

        public ObservableCollection<string> CameraList { get => _cameraList; set => SetProperty(ref _cameraList, value); }
        public ObservableCollection<string> SlotList { get => _slotList; set => SetProperty(ref _slotList, value); }
        public string SelectedCamera { get => _selectedCamera; set => SetProperty(ref _selectedCamera, value); }
        public string SelectedSlot { get => _selectedSlot; set => SetProperty(ref _selectedSlot, value); }

        // 表格数据
        private ObservableCollection<Camera2DDataRow> _dataRows;
        public ObservableCollection<Camera2DDataRow> DataRows
        {
            get => _dataRows;
            set => SetProperty(ref _dataRows, value);
        }

        // 工站选择
        //private ObservableCollection<StationInfo> _stations;
        //private StationInfo _selectedStation;
        //public ObservableCollection<StationInfo> Stations { get => _stations; set => SetProperty(ref _stations, value); }
        //public StationInfo SelectedStation
        //{
        //    get => _selectedStation;
        //    set => SetProperty(ref _selectedStation, value);
        //}

        // 命令
        public ICommand CaptureCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand CloseCommand { get; }

        public Camera2DViewModel(
            IRegionManager regionManager,
          
            ILoggerService logger)
        {
            _regionManager = regionManager;
            //_stationMetadataProvider = stationMetadataProvider;
            //_camera2DService = camera2DService;
            _logger = logger;

            CaptureCommand = new DelegateCommand(OnCapture);
            ExportCsvCommand = new DelegateCommand(OnExportCsv);
            CloseCommand = new DelegateCommand(OnClose);

            InitializeLists();
            InitializeStations();
            InitializeSampleData(); // 示例数据，实际由 Capture 刷新
        }

        private void InitializeLists()
        {
            CameraList = new ObservableCollection<string> { "Side Camera", "Top Camera", "Bottom Camera" };
            SlotList = new ObservableCollection<string> { "Slot 1", "Slot 2", "Slot 3", "Slot 4" };
            SelectedCamera = CameraList.FirstOrDefault();
            SelectedSlot = SlotList.FirstOrDefault();
        }

        private void InitializeStations()
        {
            //var allStations = _stationMetadataProvider.GetAllStations();
            //Stations = new ObservableCollection<StationInfo>(allStations);
            //if (Stations.Any())
            //    SelectedStation = Stations.First();
        }

        private void InitializeSampleData()
        {
            // 模拟参考图片中的示例数据
            DataRows = new ObservableCollection<Camera2DDataRow>
            {
                new Camera2DDataRow
                {
                    Type = "Reference",
                    X = 0.000, Y = 0.000, U = 0.000, Distance = 0.000,
                    X2 = 0.000, Y2 = 0.000, U2 = 0.000, Distance2 = 0.000
                },
                new Camera2DDataRow
                {
                    Type = "Deviation",
                    X = -1.228, Y = -0.339, U = 0.622, Distance = 1.952,
                    X2 = 0.002, Y2 = -0.001, U2 = -0.010, Distance2 = 1.952
                },
                new Camera2DDataRow
                {
                    Type = "Compensation",
                    X = 0.000, Y = 0.000, U = 0.000, Distance = 0.000,
                    X2 = 0.000, Y2 = 0.000, U2 = 0.010, Distance2 = 0.000
                }
            };
        }

        private async void OnCapture()
        {
            try
            {
                // 从2D相机获取实际数据
                var result = await _camera2DService.CaptureAsync(SelectedCamera, SelectedSlot);
                if (result != null)
                {
                    // 更新 Deviation 行（第二行）
                    if (DataRows.Count >= 2 && DataRows[1].Type == "Deviation")
                    {
                        DataRows[1].X = result.DeviationX;
                        DataRows[1].Y = result.DeviationY;
                        DataRows[1].U = result.DeviationU;
                        DataRows[1].Distance = result.Distance;
                        DataRows[1].X2 = result.DeviationX2;
                        DataRows[1].Y2 = result.DeviationY2;
                        DataRows[1].U2 = result.DeviationU2;
                        DataRows[1].Distance2 = result.Distance2;
                    }
                    // 可选：更新 Compensation 行（第三行）根据某种算法
                    if (DataRows.Count >= 3 && DataRows[2].Type == "Compensation")
                    {
                        DataRows[2].X = -result.DeviationX;
                        DataRows[2].Y = -result.DeviationY;
                        DataRows[2].U = -result.DeviationU;
                        DataRows[2].Distance = result.Distance;
                        DataRows[2].X2 = -result.DeviationX2;
                        DataRows[2].Y2 = -result.DeviationY2;
                        DataRows[2].U2 = -result.DeviationU2;
                        DataRows[2].Distance2 = result.Distance2;
                    }
                    _logger.Info($"2D camera capture completed for {SelectedCamera}, {SelectedSlot}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Capture failed: {ex.Message}");
            }
        }

        private void OnExportCsv()
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = $"Camera2D_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Type,X(mm),Y(mm),U(deg),Distance(mm),X2(mm),Y2(mm),U2(deg),Distance2(mm)");
                    foreach (var row in DataRows)
                    {
                        sb.AppendLine($"{row.Type},{row.X:F3},{row.Y:F3},{row.U:F3},{row.Distance:F3},{row.X2:F3},{row.Y2:F3},{row.U2:F3},{row.Distance2:F3}");
                    }
                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    _logger.Info($"2D camera data exported to {dialog.FileName}");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Export failed: {ex.Message}");
                }
            }
        }

        private void OnClose()
        {
            // 保存数据到 step.Camera2DDetail（需要在 ProcessStep 中添加该属性）
            if (_step != null && _step.VisionDetail == null)
                _step.VisionDetail = new VisionDetail();
            if (_step?.VisionDetail != null)
            {
                _step.VisionDetail.SelectedCamera = SelectedCamera;
                _step.VisionDetail.SelectedSlot = SelectedSlot;
                _step.VisionDetail.DataRows = DataRows;
            }

            if (_currentRegion != null && _currentView != null)
                _currentRegion.Remove(_currentView);
        }

        #region INavigationAware
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _step = navigationContext.Parameters.GetValue<ProcessStep>("step");
            if (_step == null) return;

            // 确保 Camera2DDetail 存在
            if (_step.VisionDetail == null)
                _step.VisionDetail = new VisionDetail();

            // 加载保存的设置
            SelectedCamera = _step.VisionDetail.SelectedCamera;
            SelectedSlot = _step.VisionDetail.SelectedSlot;
            DataRows = _step.VisionDetail.DataRows ?? new ObservableCollection<Camera2DDataRow>();
            if (DataRows.Count == 0)
                InitializeSampleData();

            RaisePropertyChanged(nameof(SelectedCamera));
            RaisePropertyChanged(nameof(SelectedSlot));
            RaisePropertyChanged(nameof(DataRows));
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
        #endregion
    }

    // 模拟2D相机服务接口
    public interface ICamera2DService
    {
        Task<Camera2DCaptureResult> CaptureAsync(string cameraName, string slot);
    }

    public class Camera2DCaptureResult
    {
        public double DeviationX { get; set; }
        public double DeviationY { get; set; }
        public double DeviationU { get; set; }
        public double Distance { get; set; }
        public double DeviationX2 { get; set; }
        public double DeviationY2 { get; set; }
        public double DeviationU2 { get; set; }
        public double Distance2 { get; set; }
    }
}
using Core.Models;
using Core.Services;
using Microsoft.Win32;
using Module.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Module.ViewModels
{
    public class CadPointEditor3DViewModel : BindableBase
    {
        private readonly IDialogService _dialogService;

        private ObservableCollection<CadPoint> _points;
        private string _selectedFilePath;
        private string _layerName = "T001L001";
        private CadPoint _selectedPoint;
        private string _generationStatus = "Waiting...";
        private bool _showPath = true;

        // 视图变换属性
        private double _zoomFactor = 1.0;
        private double _panOffsetX = 0.0;
        private double _panOffsetY = 0.0;

        // 站点列表
        private ObservableCollection<string> _assySites;
        public ObservableCollection<string> AssySites { get => _assySites; set => SetProperty(ref _assySites, value); }

        // 选中的全局站点（用于 Site Select）
        private string _selectedSite;
        public string SelectedSite
        {
            get => _selectedSite;
            set => SetProperty(ref _selectedSite, value);
        }

        // Z correction 状态
        private bool _zCorrectionApplied = false;
        public bool ZCorrectionApplied
        {
            get => _zCorrectionApplied;
            set => SetProperty(ref _zCorrectionApplied, value);
        }

        public CadPointEditor3DViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
            Points = new ObservableCollection<CadPoint>();

            // 初始化站点列表
            AssySites = new ObservableCollection<string>
            {
                "ASSY_001", "ASSY_002", "ASSY_003", "ASSY_004", "ASSY_005", "ASSY_006"
            };
            SelectedSite = "ASSY_001";
            ImportDxfCommand = new DelegateCommand(OnImportDxf);
            GeneratePathCommand = new DelegateCommand(OnGeneratePath);
            AddPointCommand = new DelegateCommand(OnAddPoint);
            DeletePointCommand = new DelegateCommand(OnDeletePoint, () => SelectedPoint != null).ObservesProperty(() => SelectedPoint);
            SavePointsCommand = new DelegateCommand(OnSavePoints);
            ExecutePathCommand = new DelegateCommand(OnExecutePath);

            ZoomInCommand = new DelegateCommand(() => ZoomFactor *= 1.1);
            ZoomOutCommand = new DelegateCommand(() => ZoomFactor /= 1.1);
            ResetViewCommand = new DelegateCommand(() => { OnResetView(); });

            TeachMapFiducialCommand = new DelegateCommand(OnTeachMapFiducial);
            TeachDispensingFiducialCommand = new DelegateCommand(OnTeachDispensingFiducial);
            DryRunCommand = new DelegateCommand(OnDryRun);
            ImportCalibrationCommand = new DelegateCommand(OnImportCalibration);
            OpenCalibrationPageCommand = new DelegateCommand(OnOpenCalibrationPage);
        }

        public ObservableCollection<CadPoint> Points
        {
            get => _points;
            set => SetProperty(ref _points, value);
        }

        public string SelectedFilePath
        {
            get => _selectedFilePath;
            set => SetProperty(ref _selectedFilePath, value);
        }

        public string LayerName
        {
            get => _layerName;
            set => SetProperty(ref _layerName, value);
        }

        public CadPoint SelectedPoint
        {
            get => _selectedPoint;
            set => SetProperty(ref _selectedPoint, value);
        }

        public string GenerationStatus
        {
            get => _generationStatus;
            set => SetProperty(ref _generationStatus, value);
        }

        public bool ShowPath
        {
            get => _showPath;
            set => SetProperty(ref _showPath, value);
        }

        public double ZoomFactor
        {
            get => _zoomFactor;
            set
            {
                if (SetProperty(ref _zoomFactor, value))
                    OnPointsChanged();
            }
        }

        public double PanOffsetX
        {
            get => _panOffsetX;
            set
            {
                if (SetProperty(ref _panOffsetX, value))
                    OnPointsChanged();
            }
        }

        public double PanOffsetY
        {
            get => _panOffsetY;
            set
            {
                if (SetProperty(ref _panOffsetY, value))
                    OnPointsChanged();
            }
        }

        // 基准点结构
        public class FiducialPoint : BindableBase
        {
            private double _x, _y, _z;
            public double X { get => _x; set => SetProperty(ref _x, value); }
            public double Y { get => _y; set => SetProperty(ref _y, value); }
            public double Z { get => _z; set => SetProperty(ref _z, value); }
        }

        private FiducialPoint _mapFiducial = new FiducialPoint();
        private FiducialPoint _dispensingFiducial = new FiducialPoint();

        public FiducialPoint MapFiducial
        {
            get => _mapFiducial;
            set => SetProperty(ref _mapFiducial, value);
        }

        public FiducialPoint DispensingFiducial
        {
            get => _dispensingFiducial;
            set => SetProperty(ref _dispensingFiducial, value);
        }

        private string _calibrationFilePath;
        public string CalibrationFilePath
        {
            get => _calibrationFilePath;
            set => SetProperty(ref _calibrationFilePath, value);
        }
        public ICommand ImportDxfCommand { get; }
        public ICommand GeneratePathCommand { get; }
        public ICommand AddPointCommand { get; }
        public ICommand DeletePointCommand { get; }
        public ICommand SavePointsCommand { get; }
        public ICommand ExecutePathCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ResetViewCommand { get; }
        public ICommand TeachMapFiducialCommand { get; }
        public ICommand TeachDispensingFiducialCommand { get; }
        public ICommand DryRunCommand { get; }
        public ICommand ImportCalibrationCommand { get; }
        public ICommand OpenCalibrationPageCommand { get; }

        private void OnImportDxf()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "DXF files (*.dxf)|*.dxf|All files (*.*)|*.*",
                Title = "Select DXF File"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var rawPoints = DxfParser.ExtractPoints(openFileDialog.FileName, LayerName);
                    var newPoints = rawPoints.Select((p, idx) => new CadPoint(p.X, p.Y, p.Z)
                    {
                        Id = $"DISP_{idx:000}",
                        AssySite = SelectedSite
                    }).ToList();
                    Points.Clear();
                    foreach (var pt in newPoints)
                        Points.Add(pt);
                    SelectedFilePath = openFileDialog.FileName;
                    OnPointsChanged();
                }
                catch (Exception ex)
                {
                    _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", $"Failed to import DXF: {ex.Message}" } }, null);
                }
            }
        }

        private void OnGeneratePath()
        {
            GenerationStatus = "Generating...";
            // 模拟路径生成
            Task.Delay(1000).ContinueWith(t =>
            {
                GenerationStatus = "Ready";
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (Points.Count == 0)
                    {
                        AddDemoPoints();
                    }
                    OnPointsChanged();
                });
            });
        }

        private void AddDemoPoints()
        {
            var demoPoints = new[]
            {
                new CadPoint(0, 0, 0) { Id = "DISP_000", AssySite = SelectedSite },
                new CadPoint(10, 5, 0) { Id = "DISP_001", AssySite = SelectedSite },
                new CadPoint(20, 10, 0) { Id = "DISP_002", AssySite = SelectedSite },
                new CadPoint(30, 8, 0) { Id = "DISP_003", AssySite = SelectedSite }
            };
            foreach (var pt in demoPoints)
                Points.Add(pt);
        }

        private void OnResetView()
        {
            ZoomFactor = 1.0;
            PanOffsetX = 0.0;
            PanOffsetY = 0.0;
        }

        private void OnAddPoint()
        {
            // 生成新的ID
            int nextIndex = Points.Count;
            string newId = $"DISP_{nextIndex:000}";
            var newPoint = new CadPoint(0, 0, 0)
            {
                Id = newId,
                AssySite = SelectedSite
            };
            Points.Add(newPoint);
            OnPointsChanged();
        }

        private void OnDeletePoint()
        {
            if (SelectedPoint != null)
            {
                Points.Remove(SelectedPoint);
                // 重新编号 ID
                for (int i = 0; i < Points.Count; i++)
                {
                    Points[i].Id = $"DISP_{i:000}";
                }
                OnPointsChanged();
            }
        }

        private void OnSavePoints()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = "exported_points.csv"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (var writer = new System.IO.StreamWriter(saveFileDialog.FileName))
                    {
                        writer.WriteLine("ID,X,Y,Z,AssySite");
                        foreach (var p in Points)
                            writer.WriteLine($"{p.Id},{p.X:F6},{p.Y:F6},{p.Z:F6},{p.AssySite}");
                    }
                    _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Points saved successfully." } }, null);
                }
                catch (Exception ex)
                {
                    _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", $"Failed to save: {ex.Message}" } }, null);
                }
            }
        }

        private void OnExecutePath()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Executing 2D path dispensing..." } }, null);
        }

        public event EventHandler PointsChanged;
        private void OnPointsChanged() => PointsChanged?.Invoke(this, EventArgs.Empty);

        private void OnTeachMapFiducial()
        {
            MapFiducial.X = 10.0;
            MapFiducial.Y = 20.0;
            MapFiducial.Z = 5.0;
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Map fiducial taught." } }, null);
        }

        private void OnTeachDispensingFiducial()
        {
            DispensingFiducial.X = 15.0;
            DispensingFiducial.Y = 25.0;
            DispensingFiducial.Z = 5.5;
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Dispensing fiducial taught." } }, null);
        }

        private void OnDryRun()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Dry run: tracing path without dispensing." } }, null);
        }

        private void OnImportCalibration()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select Calibration File"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                CalibrationFilePath = openFileDialog.FileName;
                // 这里可以解析 JSON 文件并更新 ZCorrectionApplied 状态
                ZCorrectionApplied = true; // 模拟应用成功
                _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Calibration loaded and Z correction applied." } }, null);
            }
        }
        private void OnOpenCalibrationPage()
        {
            //打开对话框（需要注册对话框）
            _dialogService.ShowDialog("CalibrationView", null, null);
        }
    }
}
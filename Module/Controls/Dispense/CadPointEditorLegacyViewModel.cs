using Core.Models;
using Core.Services;
using Microsoft.Win32;
using Module.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace Module.ViewModels
{
    /// <summary>
    /// 旧版 CadPointEditorViewModel（已弃用，保留仅供参考）
    /// 新版本请使用 Module.ViewModels.CadPointEditorViewModel
    /// </summary>
    public class CadPointEditorLegacyViewModel : BindableBase
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

        private bool _isTeachFirstPointMode = true;
        public bool IsTeachFirstPointMode
        {
            get => _isTeachFirstPointMode;
            set
            {
                if (SetProperty(ref _isTeachFirstPointMode, value) && value)
                    IsTeachAllPointsMode = false;
                RaisePropertyChanged(nameof(IsTeachAllPointsMode));
            }
        }

        private bool _isTeachAllPointsMode;
        public bool IsTeachAllPointsMode
        {
            get => _isTeachAllPointsMode;
            set
            {
                if (SetProperty(ref _isTeachAllPointsMode, value) && value)
                    IsTeachFirstPointMode = false;
                RaisePropertyChanged(nameof(IsTeachAllPointsMode));
            }
        }

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
        // 第二个表格的数据集合
        private ObservableCollection<AssyCalibrationPoint> _calibrationPoints;
        public ObservableCollection<AssyCalibrationPoint> CalibrationPoints
        {
            get => _calibrationPoints;
            set => SetProperty(ref _calibrationPoints, value);
        }
        // 初始化 CalibrationPoints
        private void InitializeCalibrationPoints()
        {
            CalibrationPoints = new ObservableCollection<AssyCalibrationPoint>();
            foreach (var point in Points)
            {
                CalibrationPoints.Add(new AssyCalibrationPoint
                {
                    Id = point.Id,
                    X = point.X,
                    Y = point.Y,
                    Z = point.Z,
                    Rx = 0,
                    Rz = 0,
                    Ofs = 0,
                    AssySite = point.AssySite
                });
            }
        }

        // 在 Points 变化时同步更新 CalibrationPoints 的行数和 ID
        private void SyncCalibrationPoints()
        {
            // 如果 Points 数量增加，添加新行
            while (CalibrationPoints.Count < Points.Count)
            {
                var newPoint = Points[CalibrationPoints.Count];
                CalibrationPoints.Add(new AssyCalibrationPoint
                {
                    Id = newPoint.Id,
                    X = newPoint.X,
                    Y = newPoint.Y,
                    Z = newPoint.Z,
                    Rx = 0,
                    Rz = 0,
                    Ofs = 0,
                    AssySite = newPoint.AssySite
                });
            }
            // 如果 Points 数量减少，删除多余行
            while (CalibrationPoints.Count > Points.Count)
            {
                CalibrationPoints.RemoveAt(CalibrationPoints.Count - 1);
            }
            // 更新对应行的 ID 和 AssySite（如果发生变化）
            for (int i = 0; i < Points.Count; i++)
            {
                CalibrationPoints[i].Id = Points[i].Id;
                CalibrationPoints[i].AssySite = Points[i].AssySite;
            }
        }

        public CadPointEditorLegacyViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
            Points = new ObservableCollection<CadPoint>();

            // 初始化站点列表
            AssySites = new ObservableCollection<string>
            {
                "ASSY_001", "ASSY_002", "ASSY_003", "ASSY_004", "ASSY_005", "ASSY_006"
            };
            SelectedSite = "ASSY_001";
            Offset = 0;
            ImportDxfCommand = new DelegateCommand(OnImportDxf);
            GeneratePathCommand = new DelegateCommand(OnGeneratePath);
            AddPointCommand = new DelegateCommand(OnAddPoint);
            DeletePointCommand = new DelegateCommand(OnDeletePoint, () => SelectedPoint != null).ObservesProperty(() => SelectedPoint);
            ExecutePathCommand = new DelegateCommand(OnExecutePath);

            ZoomInCommand = new DelegateCommand(() => ZoomFactor *= 1.1);
            ZoomOutCommand = new DelegateCommand(() => ZoomFactor /= 1.1);
            ResetViewCommand = new DelegateCommand(() => { OnResetView(); });

            TeachMapFiducialCommand = new DelegateCommand(OnTeachMapFiducial);
            TeachDispensingFiducialCommand = new DelegateCommand(OnTeachDispensingFiducial);
            DryRunCommand = new DelegateCommand(OnDryRun);
            ImportCalibrationCommand = new DelegateCommand(OnImportCalibration);

            TeachFromPathPointCommand = new DelegateCommand<CadPoint>(OnTeachFromPathPoint);
            TeachRealTimePointCommand = new DelegateCommand<AssyCalibrationPoint>(OnTeachRealTimePoint);

            AutoCalculateOtherPointsCommand = new DelegateCommand(OnAutoCalculateOtherPoints);
            SaveTableCommand = new DelegateCommand(OnSaveTable);

            ImportCADPointsCSVCommand = new DelegateCommand(OnImportCADPointsCSV);
            ExportCADPointsCSVCommand = new DelegateCommand(OnExportCADPointsCSV);
            AddMachinePointCommand = new DelegateCommand(OnAddMachinePoint);
            DeleteSelectedMachinePointCommand = new DelegateCommand(OnDeleteSelectedMachinePoint);
            ImportMachinePointsCSVCommand = new DelegateCommand(OnImportMachinePointsCSV);
            ExportMachinePointsCSVCommand = new DelegateCommand(OnExportMachinePointsCSV);

            InitializeCalibrationPoints();
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
        private double _offset = 0.0;
        public double Offset
        {
            get => _offset;
            set => SetProperty(ref _offset, value);
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
        public ICommand ExecutePathCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ResetViewCommand { get; }
        public ICommand TeachMapFiducialCommand { get; }
        public ICommand TeachDispensingFiducialCommand { get; }
        public ICommand DryRunCommand { get; }
        public ICommand ImportCalibrationCommand { get; }
        public ICommand TeachFromPathPointCommand { get; }
        public ICommand TeachRealTimePointCommand { get; }
        public ICommand AutoCalculateOtherPointsCommand { get; }
        public ICommand SaveTableCommand { get; }
        public ICommand ImportCADPointsCSVCommand { get; }
        public ICommand ExportCADPointsCSVCommand { get; }
        public ICommand AddMachinePointCommand { get; }
        public ICommand DeleteSelectedMachinePointCommand { get; }
        public ICommand ImportMachinePointsCSVCommand { get; }
        public ICommand ExportMachinePointsCSVCommand { get; }

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
                    SyncCalibrationPoints();
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
            //_dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Map fiducial taught." } }, null);
        }

        private void OnTeachDispensingFiducial()
        {
            DispensingFiducial.X = 15.0;
            DispensingFiducial.Y = 25.0;
            DispensingFiducial.Z = 5.5;
            //_dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Dispensing fiducial taught." } }, null);
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

        // 实现命令
        private void OnTeachFromPathPoint(CadPoint point)
        {
            if (point != null)
            {
                MapFiducial.X = point.X;
                MapFiducial.Y = point.Y;
                MapFiducial.Z = point.Z;
                _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", $"Path point {point.Id} coordinates set to Map Fiducial." } }, null);
            }
        }

        private void OnTeachRealTimePoint(AssyCalibrationPoint point)
        {
            if (point != null)
            {
                // 模拟获取实时坐标
                point.X = 10.0 + new Random().NextDouble() * 20;
                point.Y = 20.0 + new Random().NextDouble() * 30;
                point.Z = 5.0 + new Random().NextDouble() * 2;
                point.Rx = new Random().NextDouble() * 0.5;
                point.Rz = new Random().NextDouble() * 0.5;
                _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", $"Real-time coordinates captured for {point.Id}." } }, null);
            }
        }
        private void OnAutoCalculateOtherPoints()
        {
            if (Points.Count == 0 || CalibrationPoints.Count == 0)
            {
                _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "No points to calculate." } }, null);
                return;
            }

            // 计算偏移：假设首点 (CAD point) 与 Dispensing Fiducial 的差值应用于所有点
            double dx = DispensingFiducial.X - MapFiducial.X;
            double dy = DispensingFiducial.Y - MapFiducial.Y;
            double dz = DispensingFiducial.Z - MapFiducial.Z;

            for (int i = 0; i < Points.Count && i < CalibrationPoints.Count; i++)
            {
                var cadPoint = Points[i];
                var machinePoint = CalibrationPoints[i];
                machinePoint.X = cadPoint.X + dx;
                machinePoint.Y = cadPoint.Y + dy;
                machinePoint.Z = cadPoint.Z + dz;
                // 假设 Rx, Rz 不做偏移，或可设定默认值
                // machinePoint.Rx = 0; // 可保留原值或按需调整
                // machinePoint.Rz = 0;
            }

            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Other points auto-calculated based on fiducial offset." } }, null);
        }

        private void OnSaveTable()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = "machine_points.csv"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (var writer = new System.IO.StreamWriter(saveFileDialog.FileName))
                    {
                        writer.WriteLine("ID,X,Y,Z,Rx,Rz,AssySite");
                        foreach (var p in CalibrationPoints)
                        {
                            writer.WriteLine($"{p.Id},{p.X:F6},{p.Y:F6},{p.Z:F6},{p.Rx:F6},{p.Rz:F6},{p.AssySite}");
                        }
                    }
                    _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Machine points saved successfully." } }, null);
                }
                catch (Exception ex)
                {
                    _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", $"Failed to save: {ex.Message}" } }, null);
                }
            }
        }
        private void OnImportCADPointsCSV()
        {
            // 打开文件对话框，读取 CSV，解析并填充 Points
            var openFileDialog = new OpenFileDialog { Filter = "CSV files (*.csv)|*.csv" };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var lines = File.ReadAllLines(openFileDialog.FileName);
                    var newPoints = new List<CadPoint>();
                    // 跳过标题行（如果有）
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var parts = lines[i].Split(',');
                        if (parts.Length >= 4)
                        {
                            var point = new CadPoint(double.Parse(parts[0]), double.Parse(parts[1]), double.Parse(parts[2]))
                            {
                                Id = parts.Length > 3 ? parts[3] : $"DISP_{i:000}",
                                AssySite = parts.Length > 4 ? parts[4] : SelectedSite
                            };
                            newPoints.Add(point);
                        }
                    }
                    Points.Clear();
                    foreach (var p in newPoints) Points.Add(p);
                    SyncCalibrationPoints();
                    _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "CSV imported successfully." } }, null);
                }
                catch (Exception ex)
                {
                    _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", $"Import failed: {ex.Message}" } }, null);
                }
            }
        }

        private void OnExportCADPointsCSV()
        {
            var saveFileDialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "cad_points.csv" };
            if (saveFileDialog.ShowDialog() == true)
            {
                using (var writer = new StreamWriter(saveFileDialog.FileName))
                {
                    writer.WriteLine("X,Y,Z,ID,AssySite");
                    foreach (var p in Points)
                        writer.WriteLine($"{p.X},{p.Y},{p.Z},{p.Id},{p.AssySite}");
                }
                _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "CSV exported successfully." } }, null);
            }
        }

        private void OnAddMachinePoint()
        {
            var newId = $"MACH_{CalibrationPoints.Count:000}";
            CalibrationPoints.Add(new AssyCalibrationPoint
            {
                Id = newId,
                X = 0,
                Y = 0,
                Z = 0,
                Rx = 0,
                Rz = 0,
                Ofs = 0,
                AssySite = SelectedSite
            });
        }

        private void OnDeleteSelectedMachinePoint()
        {
            //if (SelectedCalibrationPoint != null)
            //    CalibrationPoints.Remove(SelectedCalibrationPoint);
        }

        private void OnImportMachinePointsCSV()
        {
            // 类似 CAD Points 的导入，但字段为 ID,X,Y,Z,Rx,Rz,Ofs,AssySite
            // 可根据实际 CSV 格式实现
        }

        private void OnExportMachinePointsCSV()
        {
            var saveFileDialog = new SaveFileDialog { Filter = "CSV files (*.csv)|*.csv", FileName = "machine_points.csv" };
            if (saveFileDialog.ShowDialog() == true)
            {
                using (var writer = new StreamWriter(saveFileDialog.FileName))
                {
                    writer.WriteLine("ID,X,Y,Z,Rx,Rz,Ofs,AssySite");
                    foreach (var p in CalibrationPoints)
                        writer.WriteLine($"{p.Id},{p.X},{p.Y},{p.Z},{p.Rx},{p.Rz},{p.Ofs},{p.AssySite}");
                }
                _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "CSV exported successfully." } }, null);
            }
        }
    }
}
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace Module.ViewModels
{
    public class AutoPathsGenerationViewModel : BindableBase
    {
        private readonly IDialogService _dialogService;

        private string _selectedGroup = "ASSY_001";
        private string _selectedSubAssy = "Chassis";
        private string _selectedType = "Dot";
        private ObservableCollection<CapturedPoint> _points;
        private ObservableCollection<AutoPathPoint> _autoPathPoints;
        private string _executeStatus = "Waiting for scan";

        public ObservableCollection<string> Groups { get; } = new ObservableCollection<string>
        {
            "ASSY_001", "ASSY_002", "ASSY_003", "ASSY_004", "ASSY_005", "ASSY_006"
        };

        public ObservableCollection<string> SubAssyOptions { get; } = new ObservableCollection<string>
        {
            "Chassis", "Pillar"
        };

        public ObservableCollection<string> TypeOptions { get; } = new ObservableCollection<string>
        {
            "Dot", "2D Path", "3D Path"
        };

        public string SelectedGroup
        {
            get => _selectedGroup;
            set => SetProperty(ref _selectedGroup, value);
        }

        public string SelectedSubAssy
        {
            get => _selectedSubAssy;
            set => SetProperty(ref _selectedSubAssy, value);
        }

        public string SelectedType
        {
            get => _selectedType;
            set
            {
                if (SetProperty(ref _selectedType, value))
                {
                    // 当类型变为非 Dot 时，自动生成路径点（如果有点）
                    if (value != "Dot" && Points.Count > 0)
                        GenerateAutoPathPoints();
                }
            }
        }

        private bool _isZCorrectionEnabled = true;
        public bool IsZCorrectionEnabled
        {
            get => _isZCorrectionEnabled;
            set => SetProperty(ref _isZCorrectionEnabled, value);
        }
        public ObservableCollection<CapturedPoint> Points
        {
            get => _points;
            set => SetProperty(ref _points, value);
        }

        public ObservableCollection<AutoPathPoint> AutoPathPoints
        {
            get => _autoPathPoints;
            set => SetProperty(ref _autoPathPoints, value);
        }

        public string ExecuteStatus
        {
            get => _executeStatus;
            set => SetProperty(ref _executeStatus, value);
        }

        // 命令
        public ICommand MoveCommand { get; }
        public ICommand CameraCaptureCommand { get; }
        public ICommand EditPositionCommand { get; }
        public ICommand CapturePointCommand { get; }
        public ICommand DeleteSelectedPointCommand { get; }
        public ICommand ExportAsDxfCommand { get; }
        public ICommand ExtractPathCommand { get; }
        public ICommand SelectStartCommand { get; }
        public ICommand SelectEndCommand { get; }

        public AutoPathsGenerationViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;

            Points = new ObservableCollection<CapturedPoint>();
            AutoPathPoints = new ObservableCollection<AutoPathPoint>();

            // 添加示例点
            Points.Add(new CapturedPoint { PointId = "P001", X = 100.500, Y = 200.300, Z = 50.000 });
            Points.Add(new CapturedPoint { PointId = "P002", X = 110.200, Y = 210.100, Z = 50.200 });
            Points.Add(new CapturedPoint { PointId = "P003", X = 120.100, Y = 220.000, Z = 50.100 });

            // 初始化命令
            MoveCommand = new DelegateCommand(OnMove);
            CameraCaptureCommand = new DelegateCommand(OnCameraCapture);
            EditPositionCommand = new DelegateCommand(OnEditPosition);
            CapturePointCommand = new DelegateCommand(OnCapturePoint);
            DeleteSelectedPointCommand = new DelegateCommand(OnDeleteSelectedPoint);
            ExportAsDxfCommand = new DelegateCommand(OnExportAsDxf);
            ExtractPathCommand = new DelegateCommand(OnExtractPath);
            SelectStartCommand = new DelegateCommand(OnSelectStart);
            SelectEndCommand = new DelegateCommand(OnSelectEnd);
        }

        private void OnMove()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Move camera command (simulated)." } }, null);
        }

        private void OnCameraCapture()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Camera capture command (simulated)." } }, null);
        }

        private void OnEditPosition()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Edit position command (simulated)." } }, null);
        }

        private void OnCapturePoint()
        {
            // 模拟捕获当前点坐标（可从设备获取）
            double newX = 100.0 + Points.Count * 5.0;
            double newY = 200.0 + Points.Count * 6.0;
            double newZ = 50.0 + Points.Count * 0.1;
            var newPoint = new CapturedPoint
            {
                PointId = $"P{(Points.Count + 1):000}",
                X = newX,
                Y = newY,
                Z = newZ
            };
            Points.Add(newPoint);
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", $"Point {newPoint.PointId} captured." } }, null);

            // 如果当前类型不是 Dot，则自动重新生成路径点
            if (SelectedType != "Dot")
                GenerateAutoPathPoints();
        }

        private void OnDeleteSelectedPoint()
        {
            var toDelete = Points.Where(p => p.IsSelected).ToList();
            foreach (var p in toDelete)
                Points.Remove(p);
            // 重新编号
            for (int i = 0; i < Points.Count; i++)
                Points[i].PointId = $"P{(i + 1):000}";

            // 更新路径点
            if (SelectedType != "Dot")
                GenerateAutoPathPoints();
        }

        private void OnExportAsDxf()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Export as DXF Layer not implemented." } }, null);
        }

        private void OnExtractPath()
        {
            GenerateAutoPathPoints();
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Path extracted from capture points." } }, null);
        }

        private void GenerateAutoPathPoints()
        {
            AutoPathPoints.Clear();
            for (int i = 0; i < Points.Count; i++)
            {
                var pt = Points[i];
                // 简单偏移量计算示例（可自定义逻辑）
                double offset = 0.0;
                if (SelectedType == "3D Path")
                    offset = 0.2; // 示例偏移
                else if (SelectedType == "2D Path")
                    offset = 0.0;

                AutoPathPoints.Add(new AutoPathPoint
                {
                    PointId = pt.PointId,
                    X = pt.X,
                    Y = pt.Y,
                    Z = pt.Z,
                    Offset = offset
                });
            }
        }

        private void OnSelectStart()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "Start point selected." } }, null);
            ExecuteStatus = "Start point selected. Ready to execute.";
        }

        private void OnSelectEnd()
        {
            _dialogService.ShowDialog("MessageDialog", new DialogParameters { { "message", "End point selected." } }, null);
            ExecuteStatus = "End point selected. Ready to execute.";
        }
    }

    public class CapturedPoint : BindableBase
    {
        private string _pointId;
        private double _x;
        private double _y;
        private double _z;
        private bool _isSelected;

        public string PointId
        {
            get => _pointId;
            set => SetProperty(ref _pointId, value);
        }

        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        public double Z
        {
            get => _z;
            set => SetProperty(ref _z, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public class AutoPathPoint : BindableBase
    {
        private string _pointId;
        private double _x;
        private double _y;
        private double _z;
        private double _offset;

        public string PointId
        {
            get => _pointId;
            set => SetProperty(ref _pointId, value);
        }

        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        public double Z
        {
            get => _z;
            set => SetProperty(ref _z, value);
        }

        public double Offset
        {
            get => _offset;
            set => SetProperty(ref _offset, value);
        }
    }
}
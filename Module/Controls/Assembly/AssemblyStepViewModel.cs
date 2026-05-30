using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace Module.ViewModels
{
    public class AssemblyStepViewModel : BindableBase
    {
        // 站点集合
        public ObservableCollection<string> StationNumbers { get; } = new ObservableCollection<string>
        {
            "ASSY_001", "ASSY_002", "ASSY_003", "ASSY_004", "ASSY_005", "ASSY_006"
        };

        // 特征集合
        public ObservableCollection<string> TopCCDFeatures { get; } = new ObservableCollection<string>
        {
            "TAB_001", "PILLAR_001", "OTHER_FEATURE"
        };
        public ObservableCollection<string> SideCCDFeatures { get; } = new ObservableCollection<string>
        {
            "SLOT", "GROOVE"
        };
        public ObservableCollection<string> BottomCCDFeatures { get; } = new ObservableCollection<string>
        {
            "SLOT_D", "PIN"
        };

        // UV 头选项
        public ObservableCollection<string> UVHeads { get; } = new ObservableCollection<string>
        {
            "Head 1", "Head 2"
        };

        // 强度曲线模型
        public class IntensityStage : BindableBase
        {
            private string _stage;
            private double _duration;
            private double _power;
            private string _note;

            public string Stage { get => _stage; set => SetProperty(ref _stage, value); }
            public double Duration { get => _duration; set => SetProperty(ref _duration, value); }
            public double Power { get => _power; set => SetProperty(ref _power, value); }
            public string Note { get => _note; set => SetProperty(ref _note, value); }
        }

        // 属性
        private string _selectedMoveSite;
        private string _realTimePositions = "Dx: 0.00 Dy: 0.00 Dz1: 0.00 Dz2: 0.00 Rx: 0.00 Rz: 0.00 Ry: 0.00 Y: 0.00";
        private string _selectedTopCCDFeature1;
        private string _selectedTopCCDFeature2;
        private string _selectedSideCCDFeature;
        private string _selectedBottomCCDFeature;
        private string _topCCD1TargetPosition = "Target: 0.00 0.00 0.00";
        private string _topCCD2TargetPosition = "Target: 0.00 0.00 0.00";
        private string _sideCCDTargetPosition = "Target: 0.00 0.00 0.00";
        private string _bottomCCDTargetPosition = "Target: 0.00 0.00 0.00";
        private string _selectedTabSite;
        private string _tabCompensation = "X: 0.00 Y: 0.00 Rx: 0.00 Rz: 0.00";
        private string _pinCompensation = "X: 0.00 Y: 0.00 Z: 0.00 Ry: 0.00";
        private bool _passActionContinue = true;
        private bool _failActionRetry = true;
        private int _maxRetries = 3;
        private string _maxExceededAction = "Alarm";
        private string _axisStatus = "X: 0.00 Y: 0.00 Z: 0.00 Rx: 0.00 Rz: 0.00 Ry: 0.00 Y: 0.00";
        private string _selectedAssemblySite;
        private string _targetAssemblyPosition = "X: 0.00 Y: 0.00 Z: 0.00 Rx: 0.00 Rz: 0.00 Ry: 0.00";
        private string _selectedAlignSite;
        private bool _autoMode;
        private bool _stepMode;
        private string _currentStepStatus = "Waiting to start";
        private string _realTimeDeviation = "X: 0.00 Y: 0.00 Z: 0.00 Rx: 0.00 Rz: 0.00 Ry: 0.00";
        private string _finalAssemblyPosition = "X: 0.00 Y: 0.00 Z: 0.00 Rx: 0.00 Rz: 0.00 Ry: 0.00";
        private double _forceSensor1, _forceSensor2, _forceSensor3, _forceSensor4, _forceSensor5, _forceSensor6;
        private string _selectedUVHead;
        private double _cureTime = 5.0;
        private double _intensity = 800.0;
        private ObservableCollection<IntensityStage> _intensityProfile;
        private int _totalAssemblyCount = 128;
        private int _passCount = 124;
        private int _failCount = 4;
        private string _lastMeasurementTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 命令
        public ICommand MoveToSiteCommand { get; }
        public ICommand MoveCameraToFeatureCommand { get; }
        public ICommand MoveTopCCD1Command { get; }
        public ICommand CaptureTopCCD1Command { get; }
        public ICommand MoveTopCCD2Command { get; }
        public ICommand CaptureTopCCD2Command { get; }
        public ICommand MoveSideCCDCommand { get; }
        public ICommand CaptureSideCCDCommand { get; }
        public ICommand MoveBottomCCDCommand { get; }
        public ICommand CaptureBottomCCDCommand { get; }
        public ICommand EditBottomCCDPositionCommand { get; }
        public ICommand TabDetectCommand { get; }
        public ICommand PillarDetectCommand { get; }
        public ICommand ViewTabDataCommand { get; }
        public ICommand SlotRotaryCommand { get; }
        public ICommand PinAdjCommand { get; }
        public ICommand SlotCheckCommand { get; }
        public ICommand EditPinParametersCommand { get; }
        public ICommand ViewPinDataCommand { get; }
        public ICommand EditAssemblyPositionCommand { get; }
        public ICommand PlaceNoCompCommand { get; }
        public ICommand PlaceStartCommand { get; }
        public ICommand PlaceStopCommand { get; }
        public ICommand EditUVParametersCommand { get; }
        public ICommand StartCureCommand { get; }
        public ICommand StopCureCommand { get; }
        public ICommand ViewDetailedDataCommand { get; }
        public ICommand ExportCSVCommand { get; }

        public AssemblyStepViewModel()
        {
            // 初始化命令
            MoveToSiteCommand = new DelegateCommand(OnMoveToSite);
            MoveCameraToFeatureCommand = new DelegateCommand(OnMoveCameraToFeature);
            MoveTopCCD1Command = new DelegateCommand(OnMoveTopCCD1);
            CaptureTopCCD1Command = new DelegateCommand(OnCaptureTopCCD1);
            MoveTopCCD2Command = new DelegateCommand(OnMoveTopCCD2);
            CaptureTopCCD2Command = new DelegateCommand(OnCaptureTopCCD2);
            MoveSideCCDCommand = new DelegateCommand(OnMoveSideCCD);
            CaptureSideCCDCommand = new DelegateCommand(OnCaptureSideCCD);
            MoveBottomCCDCommand = new DelegateCommand(OnMoveBottomCCD);
            CaptureBottomCCDCommand = new DelegateCommand(OnCaptureBottomCCD);
            EditBottomCCDPositionCommand = new DelegateCommand(OnEditBottomCCDPosition);
            TabDetectCommand = new DelegateCommand(OnTabDetect);
            PillarDetectCommand = new DelegateCommand(OnPillarDetect);
            ViewTabDataCommand = new DelegateCommand(OnViewTabData);
            SlotRotaryCommand = new DelegateCommand(OnSlotRotary);
            PinAdjCommand = new DelegateCommand(OnPinAdj);
            SlotCheckCommand = new DelegateCommand(OnSlotCheck);
            EditPinParametersCommand = new DelegateCommand(OnEditPinParameters);
            ViewPinDataCommand = new DelegateCommand(OnViewPinData);
            EditAssemblyPositionCommand = new DelegateCommand(OnEditAssemblyPosition);
            PlaceNoCompCommand = new DelegateCommand(OnPlaceNoComp);
            PlaceStartCommand = new DelegateCommand(OnPlaceStart);
            PlaceStopCommand = new DelegateCommand(OnPlaceStop);
            EditUVParametersCommand = new DelegateCommand(OnEditUVParameters);
            StartCureCommand = new DelegateCommand(OnStartCure);
            StopCureCommand = new DelegateCommand(OnStopCure);
            ViewDetailedDataCommand = new DelegateCommand(OnViewDetailedData);
            ExportCSVCommand = new DelegateCommand(OnExportCSV);

            // 初始化强度曲线
            IntensityProfile = new ObservableCollection<IntensityStage>
            {
                new IntensityStage { Stage = "ST 1", Duration = 1.0, Power = 400.0, Note = "Ramp-up" },
                new IntensityStage { Stage = "ST 2", Duration = 3.0, Power = 800.0, Note = "Full cure" },
                new IntensityStage { Stage = "ST 3", Duration = 1.0, Power = 200.0, Note = "Ramp-down" }
            };
        }

        // 公共属性（带通知）
        public string SelectedMoveSite { get => _selectedMoveSite; set => SetProperty(ref _selectedMoveSite, value); }
        public string RealTimePositions { get => _realTimePositions; set => SetProperty(ref _realTimePositions, value); }
        public string SelectedTopCCDFeature1 { get => _selectedTopCCDFeature1; set => SetProperty(ref _selectedTopCCDFeature1, value); }
        public string SelectedTopCCDFeature2 { get => _selectedTopCCDFeature2; set => SetProperty(ref _selectedTopCCDFeature2, value); }
        public string SelectedSideCCDFeature { get => _selectedSideCCDFeature; set => SetProperty(ref _selectedSideCCDFeature, value); }
        public string SelectedBottomCCDFeature { get => _selectedBottomCCDFeature; set => SetProperty(ref _selectedBottomCCDFeature, value); }
        public string TopCCD1TargetPosition { get => _topCCD1TargetPosition; set => SetProperty(ref _topCCD1TargetPosition, value); }
        public string TopCCD2TargetPosition { get => _topCCD2TargetPosition; set => SetProperty(ref _topCCD2TargetPosition, value); }
        public string SideCCDTargetPosition { get => _sideCCDTargetPosition; set => SetProperty(ref _sideCCDTargetPosition, value); }
        public string BottomCCDTargetPosition { get => _bottomCCDTargetPosition; set => SetProperty(ref _bottomCCDTargetPosition, value); }
        public string SelectedTabSite { get => _selectedTabSite; set => SetProperty(ref _selectedTabSite, value); }
        public string TabCompensation { get => _tabCompensation; set => SetProperty(ref _tabCompensation, value); }
        public string PinCompensation { get => _pinCompensation; set => SetProperty(ref _pinCompensation, value); }
        public bool PassActionContinue { get => _passActionContinue; set => SetProperty(ref _passActionContinue, value); }
        public bool FailActionRetry { get => _failActionRetry; set => SetProperty(ref _failActionRetry, value); }
        public int MaxRetries { get => _maxRetries; set => SetProperty(ref _maxRetries, value); }
        public string MaxExceededAction { get => _maxExceededAction; set => SetProperty(ref _maxExceededAction, value); }
        public string AxisStatus { get => _axisStatus; set => SetProperty(ref _axisStatus, value); }
        public string SelectedAssemblySite { get => _selectedAssemblySite; set => SetProperty(ref _selectedAssemblySite, value); }
        public string TargetAssemblyPosition { get => _targetAssemblyPosition; set => SetProperty(ref _targetAssemblyPosition, value); }
        public string SelectedAlignSite { get => _selectedAlignSite; set => SetProperty(ref _selectedAlignSite, value); }
        public bool AutoMode { get => _autoMode; set => SetProperty(ref _autoMode, value); }
        public bool StepMode { get => _stepMode; set => SetProperty(ref _stepMode, value); }
        public string CurrentStepStatus { get => _currentStepStatus; set => SetProperty(ref _currentStepStatus, value); }
        public string RealTimeDeviation { get => _realTimeDeviation; set => SetProperty(ref _realTimeDeviation, value); }
        public string FinalAssemblyPosition { get => _finalAssemblyPosition; set => SetProperty(ref _finalAssemblyPosition, value); }
        public double ForceSensor1 { get => _forceSensor1; set => SetProperty(ref _forceSensor1, value); }
        public double ForceSensor2 { get => _forceSensor2; set => SetProperty(ref _forceSensor2, value); }
        public double ForceSensor3 { get => _forceSensor3; set => SetProperty(ref _forceSensor3, value); }
        public double ForceSensor4 { get => _forceSensor4; set => SetProperty(ref _forceSensor4, value); }
        public double ForceSensor5 { get => _forceSensor5; set => SetProperty(ref _forceSensor5, value); }
        public double ForceSensor6 { get => _forceSensor6; set => SetProperty(ref _forceSensor6, value); }
        public string SelectedUVHead { get => _selectedUVHead; set => SetProperty(ref _selectedUVHead, value); }
        public double CureTime { get => _cureTime; set => SetProperty(ref _cureTime, value); }
        public double Intensity { get => _intensity; set => SetProperty(ref _intensity, value); }
        public ObservableCollection<IntensityStage> IntensityProfile { get => _intensityProfile; set => SetProperty(ref _intensityProfile, value); }
        public int TotalAssemblyCount { get => _totalAssemblyCount; set => SetProperty(ref _totalAssemblyCount, value); }
        public int PassCount { get => _passCount; set => SetProperty(ref _passCount, value); }
        public int FailCount { get => _failCount; set => SetProperty(ref _failCount, value); }
        public string LastMeasurementTime { get => _lastMeasurementTime; set => SetProperty(ref _lastMeasurementTime, value); }

        // 命令实现（模拟）
        private void OnMoveToSite() => MessageBox.Show($"Moving to {SelectedMoveSite}");
        private void OnMoveCameraToFeature() => MessageBox.Show("Move camera to selected feature");
        private void OnMoveTopCCD1() => MessageBox.Show($"Moving Top CCD to {SelectedTopCCDFeature1}");
        private void OnCaptureTopCCD1() => MessageBox.Show($"Capturing image for {SelectedTopCCDFeature1}");
        private void OnMoveTopCCD2() => MessageBox.Show($"Moving Top CCD to {SelectedTopCCDFeature2}");
        private void OnCaptureTopCCD2() => MessageBox.Show($"Capturing image for {SelectedTopCCDFeature2}");
        private void OnMoveSideCCD() => MessageBox.Show($"Moving Side CCD to {SelectedSideCCDFeature}");
        private void OnCaptureSideCCD() => MessageBox.Show($"Capturing side image for {SelectedSideCCDFeature}");
        private void OnMoveBottomCCD() => MessageBox.Show($"Moving Bottom CCD to {SelectedBottomCCDFeature}");
        private void OnCaptureBottomCCD() => MessageBox.Show($"Capturing bottom image for {SelectedBottomCCDFeature}");
        private void OnEditBottomCCDPosition() => MessageBox.Show("Edit Bottom CCD position dialog");
        private void OnTabDetect() => MessageBox.Show("Tab detection started");
        private void OnPillarDetect() => MessageBox.Show("Pillar detection started");
        private void OnViewTabData() => MessageBox.Show("View TAB alignment data");
        private void OnSlotRotary() => MessageBox.Show("Slot rotary alignment");
        private void OnPinAdj() => MessageBox.Show("PIN adjustment");
        private void OnSlotCheck() => MessageBox.Show("Slot check");
        private void OnEditPinParameters() => MessageBox.Show("Edit PIN parameters");
        private void OnViewPinData() => MessageBox.Show("View PIN alignment data");
        private void OnEditAssemblyPosition() => MessageBox.Show("Edit assembly position");
        private void OnPlaceNoComp() => MessageBox.Show("Place without compensation");
        private void OnPlaceStart() => MessageBox.Show("Placement started");
        private void OnPlaceStop() => MessageBox.Show("Placement stopped");
        private void OnEditUVParameters() => MessageBox.Show("Edit UV parameters");
        private void OnStartCure() => MessageBox.Show("UV curing started");
        private void OnStopCure() => MessageBox.Show("UV curing stopped");
        private void OnViewDetailedData()
        {
            // 创建详细数据视图和视图模型
            var detailedView = new Views.DetailedDataView();
            var detailedViewModel = new DetailedDataViewModel();

            // 创建窗口并设置属性
            var window = new Window
            {
                Title = "Detection and Measurement Detailed Data",
                Content = detailedView,
                DataContext = detailedViewModel,
                Width = 1200,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Style = (Style)Application.Current.FindResource("MaterialDesignWindow")
            };

            // 安全设置所有者
            if (Application.Current.MainWindow != null && Application.Current.MainWindow != window)
            {
                window.Owner = Application.Current.MainWindow;
            }
            else
            {
                // 如果主窗口无效，则使用当前活动窗口作为备用
                var activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                if (activeWindow != null && activeWindow != window)
                    window.Owner = activeWindow;
            }

            // 设置关闭回调（如果 ViewModel 需要）
            detailedViewModel.CloseAction = () => window.Close();

            window.ShowDialog();
        }
        private void OnExportCSV() => MessageBox.Show("Export CSV file");
    }
}
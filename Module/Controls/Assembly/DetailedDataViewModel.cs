using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;

namespace Module.ViewModels
{
    public class DetailedDataViewModel : BindableBase
    {
        private ObservableCollection<MeasurementRecord> _measurementRecords;
        public ObservableCollection<MeasurementRecord> MeasurementRecords
        {
            get { return _measurementRecords; }
            set { SetProperty(ref _measurementRecords, value); }
        }

        // 关闭窗口的命令
        public DelegateCommand CloseCommand { get; }

        // 用于关闭窗口的回调（由窗口创建者设置）
        public Action CloseAction { get; set; }

        public DetailedDataViewModel()
        {
            CloseCommand = new DelegateCommand(ExecuteClose);
            LoadSampleData();
        }

        private void ExecuteClose()
        {
            CloseAction?.Invoke();
        }

        private void LoadSampleData()
        {
            MeasurementRecords = new ObservableCollection<MeasurementRecord>
            {
                new MeasurementRecord
                {
                    Timestamp = DateTime.Parse("2025-12-12T11:00:00Z"),
                    OutgoingSubAssemblyId = "ASSY-D-000003",
                    MachineStatus = "PASS",
                    OperatorId = "morteza",
                    RecipeName = "Final_v2.0",
                    OverallCycleTime = 198.9,
                    ActuatorInstance = 1,
                    ActuatorUniqueId = "ACT-SN-E01",
                    ActuatorCycleTime = 27.1,
                    Alarms = "",
                    Warnings = "",
                    IpqcPillar1Xy = -15.3,
                    IpqcPillar2Xy = 9.1,
                    IpqcZPosition = -4.8,
                    IpqcParallelism = 0.015,
                    EngagementStatus = "PASS",
                    PeakForceZ = 1.81,
                    PeakForceRadialLeft = 0.68,
                    PeakForceRadialRight = 0.65,
                    OtherCq = "..."
                },
                new MeasurementRecord
                {
                    Timestamp = DateTime.Parse("2025-12-12T11:00:00Z"),
                    OutgoingSubAssemblyId = "ASSY-D-000003",
                    MachineStatus = "PASS",
                    OperatorId = "morteza",
                    RecipeName = "Final_v2.0",
                    OverallCycleTime = 198.9,
                    ActuatorInstance = 2,
                    ActuatorUniqueId = "ACT-SN-E02",
                    ActuatorCycleTime = 27.0,
                    Alarms = "",
                    Warnings = "",
                    IpqcPillar1Xy = -11.2,
                    IpqcPillar2Xy = -2.5,
                    IpqcZPosition = -5.5,
                    IpqcParallelism = 0.019,
                    EngagementStatus = "PASS",
                    PeakForceZ = 1.79,
                    PeakForceRadialLeft = 0.66,
                    PeakForceRadialRight = 0.64,
                    OtherCq = "..."
                },
                new MeasurementRecord
                {
                    Timestamp = DateTime.Parse("2025-12-12T11:00:00Z"),
                    OutgoingSubAssemblyId = "ASSY-D-000003",
                    MachineStatus = "PASS",
                    OperatorId = "morteza",
                    RecipeName = "Final_v2.0",
                    OverallCycleTime = 198.9,
                    ActuatorInstance = 3,
                    ActuatorUniqueId = "ACT-SN-E03",
                    ActuatorCycleTime = 27.3,
                    Alarms = "",
                    Warnings = "",
                    IpqcPillar1Xy = -9.8,
                    IpqcPillar2Xy = 1.4,
                    IpqcZPosition = -6.1,
                    IpqcParallelism = 0.021,
                    EngagementStatus = "PASS",
                    PeakForceZ = 1.80,
                    PeakForceRadialLeft = 0.67,
                    PeakForceRadialRight = 0.66,
                    OtherCq = "..."
                },
                new MeasurementRecord
                {
                    Timestamp = DateTime.Parse("2025-12-12T11:00:00Z"),
                    OutgoingSubAssemblyId = "ASSY-D-000003",
                    MachineStatus = "PASS",
                    OperatorId = "morteza",
                    RecipeName = "Final_v2.0",
                    OverallCycleTime = 198.9,
                    ActuatorInstance = 4,
                    ActuatorUniqueId = "ACT-SN-F01",
                    ActuatorCycleTime = 28.5,
                    Alarms = "E301",
                    Warnings = "W15",
                    IpqcPillar1Xy = 15.1,
                    IpqcPillar2Xy = -14.2,
                    IpqcZPosition = -4.0,
                    IpqcParallelism = 0.028,
                    EngagementStatus = "PASS",
                    PeakForceZ = 2.15,
                    PeakForceRadialLeft = 0.95,
                    PeakForceRadialRight = 0.70,
                    OtherCq = "..."
                },
                new MeasurementRecord
                {
                    Timestamp = DateTime.Parse("2025-12-12T11:00:00Z"),
                    OutgoingSubAssemblyId = "ASSY-D-000003",
                    MachineStatus = "PASS",
                    OperatorId = "morteza",
                    RecipeName = "Final_v2.0",
                    OverallCycleTime = 198.9,
                    ActuatorInstance = 5,
                    ActuatorUniqueId = "ACT-SN-F02",
                    ActuatorCycleTime = 27.2,
                    Alarms = "",
                    Warnings = "",
                    IpqcPillar1Xy = 3.1,
                    IpqcPillar2Xy = -3.8,
                    IpqcZPosition = -5.2,
                    IpqcParallelism = -0.016,
                    EngagementStatus = "PASS",
                    PeakForceZ = 1.72,
                    PeakForceRadialLeft = 0.62,
                    PeakForceRadialRight = 0.61,
                    OtherCq = "..."
                },
                new MeasurementRecord
                {
                    Timestamp = DateTime.Parse("2025-12-12T11:00:00Z"),
                    OutgoingSubAssemblyId = "ASSY-D-000003",
                    MachineStatus = "PASS",
                    OperatorId = "morteza",
                    RecipeName = "Final_v2.0",
                    OverallCycleTime = 198.9,
                    ActuatorInstance = 6,
                    ActuatorUniqueId = "ACT-SN-F03",
                    ActuatorCycleTime = 27.8,
                    Alarms = "",
                    Warnings = "",
                    IpqcPillar1Xy = -0.5,
                    IpqcPillar2Xy = -1.2,
                    IpqcZPosition = -6.5,
                    IpqcParallelism = -0.013,
                    EngagementStatus = "PASS",
                    PeakForceZ = 1.77,
                    PeakForceRadialLeft = 0.64,
                    PeakForceRadialRight = 0.63,
                    OtherCq = "..."
                }
            };
        }
    }

    // 测量记录实体类
    public class MeasurementRecord
    {
        public DateTime Timestamp { get; set; }
        public string OutgoingSubAssemblyId { get; set; }
        public string MachineStatus { get; set; }
        public string OperatorId { get; set; }
        public string RecipeName { get; set; }
        public double OverallCycleTime { get; set; }
        public int ActuatorInstance { get; set; }
        public string ActuatorUniqueId { get; set; }
        public double ActuatorCycleTime { get; set; }
        public string Alarms { get; set; }
        public string Warnings { get; set; }
        public double IpqcPillar1Xy { get; set; }
        public double IpqcPillar2Xy { get; set; }
        public double IpqcZPosition { get; set; }
        public double IpqcParallelism { get; set; }
        public string EngagementStatus { get; set; }
        public double PeakForceZ { get; set; }
        public double PeakForceRadialLeft { get; set; }
        public double PeakForceRadialRight { get; set; }
        public string OtherCq { get; set; }
    }
}
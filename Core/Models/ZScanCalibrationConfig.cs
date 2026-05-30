using System;

namespace Core.Models
{
    public class ZScanCalibrationConfig
    {
        private string _configName = string.Empty;
        private double _cameraZOffset;
        private double _needleZOffset;
        private DateTime _lastCalibrationTime;
        private string _operator = string.Empty;
        private double _baseZ;
        private double _measuredMZ;
        private double _deltaZ;
        private double _currentZHeight;
        private double _zHeightDifference;
        private double _baseDispenseHeight;
        private double _dispenseHeight;
        private ZScanGlobalVariableLink _needleCompensationLink;

        public string ConfigName
        {
            get => _configName;
            set => _configName = value ?? string.Empty;
        }

        public double CameraZOffset
        {
            get => _cameraZOffset;
            set => _cameraZOffset = value;
        }

        public double NeedleZOffset
        {
            get => _needleZOffset;
            set => _needleZOffset = value;
        }

        public DateTime LastCalibrationTime
        {
            get => _lastCalibrationTime;
            set => _lastCalibrationTime = value;
        }

        public string Operator
        {
            get => _operator;
            set => _operator = value ?? string.Empty;
        }

        public double TotalZOffset => CameraZOffset + NeedleZOffset;

        public double BaseZ { get => _baseZ; set => _baseZ = value; }

        public double MeasuredMZ { get => _measuredMZ; set => _measuredMZ = value; }

        public double DeltaZ { get => _deltaZ; set => _deltaZ = value; }

        public double CurrentZHeight { get => _currentZHeight; set => _currentZHeight = value; }
        public double ZHeightDifference { get => _zHeightDifference; set => _zHeightDifference = value; }
        public double BaseDispenseHeight { get => _baseDispenseHeight; set => _baseDispenseHeight = value; }
        public double DispenseHeight { get => _dispenseHeight; set => _dispenseHeight = value; }

        public ZScanGlobalVariableLink NeedleCompensationLink
        {
            get => _needleCompensationLink;
            set => _needleCompensationLink = value;
        }
    }
}

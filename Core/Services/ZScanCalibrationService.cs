using Core.Abstraction;
using System;

namespace Core.Services
{
    public class ZScanCalibrationService : IZScanCalibrationService
    {
        private double _cameraZOffset;
        private double _needleZOffset;
        private double _baseZ;
        private double _measuredMZ;

        public double CameraZOffset => _cameraZOffset;
        public double NeedleZOffset => _needleZOffset;
        public double TotalZOffset => _cameraZOffset + _needleZOffset;
        public double BaseZ => _baseZ;
        public double MeasuredMZ => _measuredMZ;

        public event Action CalibrationChanged;

        public void CalibrateCameraZ(double measuredZ, double referenceZ)
        {
            _cameraZOffset = referenceZ - measuredZ;
            CalibrationChanged?.Invoke();
        }

        public void ApplyNeedleCompensation(double deltaZ)
        {
            _needleZOffset = deltaZ;
            CalibrationChanged?.Invoke();
        }

        public double GetCompensatedZ(double measuredZ)
        {
            return measuredZ + TotalZOffset;
        }

        public void ResetCalibration()
        {
            _cameraZOffset = 0;
            _needleZOffset = 0;
            _baseZ = 0;
            _measuredMZ = 0;
            CalibrationChanged?.Invoke();
        }

        public void SetBaseZ(double baseZ)
        {
            _baseZ = baseZ;
            CalibrationChanged?.Invoke();
        }

        public void TeachNeedleMZ(double measuredMZ)
        {
            _measuredMZ = measuredMZ;
            CalibrationChanged?.Invoke();
        }

        public double CalculateDispenseHeight(double baseDispenseHeight, double currentZHeight, double needleCompensation)
        {
            double zHeightDiff = CalculateZHeightDifference(_baseZ, currentZHeight);
            return baseDispenseHeight + zHeightDiff + needleCompensation;
        }

        public double CalculateZHeightDifference(double baseZ, double currentZHeight)
        {
            return baseZ - currentZHeight;
        }
    }
}

using Core.Abstraction;
using System;
using System.Collections.Generic;

namespace Core.Services
{
    /// <summary>
    /// Z-SCAN 标定服务实现，内部用 Dictionary 存储双针头各自的标定状态
    /// </summary>
    public class ZScanCalibrationService : IZScanCalibrationService
    {
        /// <summary> 每根针头的标定状态 </summary>
        private class NeedleCalibrationState
        {
            public double CameraZOffset;
            public double NeedleZOffset;
            public double BaseZ;
            public double MeasuredMZ;
        }

        private readonly Dictionary<int, NeedleCalibrationState> _needleStates = new Dictionary<int, NeedleCalibrationState>
        {
            [0] = new NeedleCalibrationState(),
            [1] = new NeedleCalibrationState()
        };

        private int _currentNeedleIndex;

        private NeedleCalibrationState Current => _needleStates[_currentNeedleIndex];

        public double CameraZOffset => Current.CameraZOffset;
        public double NeedleZOffset => Current.NeedleZOffset;
        public double TotalZOffset => Current.CameraZOffset + Current.NeedleZOffset;
        public double BaseZ => Current.BaseZ;
        public double MeasuredMZ => Current.MeasuredMZ;

        public event Action CalibrationChanged;

        /// <summary> 切换当前活动针头（0=Dz1, 1=Dz2） </summary>
        public void SetCurrentNeedle(int needleIndex)
        {
            if (needleIndex < 0 || needleIndex > 1) return;
            if (_currentNeedleIndex != needleIndex)
            {
                _currentNeedleIndex = needleIndex;
                CalibrationChanged?.Invoke();
            }
        }

        public void CalibrateCameraZ(double measuredZ, double referenceZ)
        {
            Current.CameraZOffset = referenceZ - measuredZ;
            CalibrationChanged?.Invoke();
        }

        public void ApplyNeedleCompensation(double deltaZ)
        {
            Current.NeedleZOffset = deltaZ;
            CalibrationChanged?.Invoke();
        }

        public double GetCompensatedZ(double measuredZ)
        {
            return measuredZ + TotalZOffset;
        }

        public void ResetCalibration()
        {
            var state = Current;
            state.CameraZOffset = 0;
            state.NeedleZOffset = 0;
            state.BaseZ = 0;
            state.MeasuredMZ = 0;
            CalibrationChanged?.Invoke();
        }

        public void SetBaseZ(double baseZ)
        {
            Current.BaseZ = baseZ;
            CalibrationChanged?.Invoke();
        }

        public void TeachNeedleMZ(double measuredMZ)
        {
            Current.MeasuredMZ = measuredMZ;
            CalibrationChanged?.Invoke();
        }

        /// <summary> 从持久化配置恢复当前针头的标定状态 </summary>
        public void RestoreState(double cameraZOffset, double needleZOffset, double baseZ, double measuredMZ)
        {
            var state = Current;
            state.CameraZOffset = cameraZOffset;
            state.NeedleZOffset = needleZOffset;
            state.BaseZ = baseZ;
            state.MeasuredMZ = measuredMZ;
            CalibrationChanged?.Invoke();
        }

        /// <summary>计算点胶高度：基准点胶高度 + Z高度差 + 针头补偿</summary>
        public double CalculateDispenseHeight(double baseZ, double baseDispenseHeight, double currentZHeight, double needleCompensation)
        {
            double zHeightDiff = CalculateZHeightDifference(baseZ, currentZHeight);
            return baseDispenseHeight + zHeightDiff + needleCompensation;
        }

        public double CalculateZHeightDifference(double baseZ, double currentZHeight)
        {
            return baseZ - currentZHeight;
        }
    }
}

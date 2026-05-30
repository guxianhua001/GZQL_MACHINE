using Core.Services;
using Xunit;

namespace MotionControl.Tests
{
    public class ZScanCalibrationServiceTests
    {
        [Fact]
        public void Calibrate_WithValidData_UpdatesCameraZOffset()
        {
            var service = new ZScanCalibrationService();
            double measuredZ = 4.8;
            double referenceZ = 5.0;

            service.CalibrateCameraZ(measuredZ, referenceZ);

            Assert.Equal(0.2, service.CameraZOffset, 3);
        }

        [Fact]
        public void Calibrate_CameraZOffset_IsReferenceMinusMeasured()
        {
            var service = new ZScanCalibrationService();
            service.CalibrateCameraZ(5.3, 5.0);

            Assert.Equal(-0.3, service.CameraZOffset, 3);
        }

        [Fact]
        public void ApplyNeedleCompensation_AddsToOffset()
        {
            var service = new ZScanCalibrationService();
            service.CalibrateCameraZ(4.8, 5.0);
            service.ApplyNeedleCompensation(0.1);

            Assert.Equal(0.1, service.NeedleZOffset, 3);
            Assert.Equal(0.3, service.TotalZOffset, 3);
        }

        [Fact]
        public void GetCompensatedZ_ReturnsMeasuredPlusTotalOffset()
        {
            var service = new ZScanCalibrationService();
            service.CalibrateCameraZ(4.8, 5.0);
            service.ApplyNeedleCompensation(0.1);

            double result = service.GetCompensatedZ(5.0);

            Assert.Equal(5.3, result, 3);
        }

        [Fact]
        public void ResetCalibration_ClearsAllOffsets()
        {
            var service = new ZScanCalibrationService();
            service.CalibrateCameraZ(4.8, 5.0);
            service.ApplyNeedleCompensation(0.1);

            service.ResetCalibration();

            Assert.Equal(0.0, service.CameraZOffset, 3);
            Assert.Equal(0.0, service.NeedleZOffset, 3);
            Assert.Equal(0.0, service.TotalZOffset, 3);
        }

        [Fact]
        public void Calibration_WithNeedleChange_UpdatesNeedleOffset()
        {
            var service = new ZScanCalibrationService();
            service.ApplyNeedleCompensation(0.2);

            Assert.Equal(0.2, service.NeedleZOffset, 3);

            service.ApplyNeedleCompensation(0.3);

            Assert.Equal(0.3, service.NeedleZOffset, 3);
        }

        [Fact]
        public void CalibrationChanged_EventFired_OnCalibrate()
        {
            var service = new ZScanCalibrationService();
            bool eventFired = false;
            service.CalibrationChanged += () => eventFired = true;

            service.CalibrateCameraZ(4.8, 5.0);

            Assert.True(eventFired);
        }

        [Fact]
        public void CalibrationChanged_EventFired_OnNeedleCompensation()
        {
            var service = new ZScanCalibrationService();
            bool eventFired = false;
            service.CalibrationChanged += () => eventFired = true;

            service.ApplyNeedleCompensation(0.1);

            Assert.True(eventFired);
        }

        [Fact]
        public void CalibrationChanged_EventFired_OnReset()
        {
            var service = new ZScanCalibrationService();
            bool eventFired = false;
            service.CalibrationChanged += () => eventFired = true;

            service.ResetCalibration();

            Assert.True(eventFired);
        }

        [Fact]
        public void TotalZOffset_DefaultZero()
        {
            var service = new ZScanCalibrationService();

            Assert.Equal(0.0, service.TotalZOffset, 3);
        }
    }
}

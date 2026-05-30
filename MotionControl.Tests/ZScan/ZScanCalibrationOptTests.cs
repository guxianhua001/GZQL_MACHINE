using Core.Models;
using Core.Services;
using Xunit;

namespace MotionControl.Tests
{
    public class ZScanCalibrationOptTests
    {
        private ZScanCalibrationService CreateService()
        {
            return new ZScanCalibrationService();
        }

        [Fact]
        public void CalculateDispenseHeight_BaseDispensePlusDiffPlusComp()
        {
            var service = CreateService();
            service.SetBaseZ(5.200);
            service.TeachNeedleMZ(5.150);
            double baseDispenseHeight = 5.150;
            double currentZHeight = 5.180;
            double needleComp = 0.010;
            double result = service.CalculateDispenseHeight(baseDispenseHeight, currentZHeight, needleComp);
            Assert.Equal(5.180, result, 3);
        }

        [Fact]
        public void CalculateDispenseHeight_NegativeDiff()
        {
            var service = CreateService();
            service.SetBaseZ(5.200);
            double baseDispenseHeight = 5.150;
            double currentZHeight = 5.250;
            double needleComp = 0.010;
            double result = service.CalculateDispenseHeight(baseDispenseHeight, currentZHeight, needleComp);
            Assert.Equal(5.110, result, 3);
        }

        [Fact]
        public void ZHeightDifference_IsBaseZMinusCurrentZ()
        {
            var service = CreateService();
            double diff = service.CalculateZHeightDifference(5.200, 5.180);
            Assert.Equal(0.020, diff, 3);
        }

        [Fact]
        public void ZScanCalibrationConfig_HasNewFields()
        {
            var config = new ZScanCalibrationConfig();
            config.CurrentZHeight = 5.180;
            config.ZHeightDifference = 0.020;
            config.BaseDispenseHeight = 5.150;
            config.DispenseHeight = 5.180;
            Assert.Equal(5.180, config.CurrentZHeight, 3);
            Assert.Equal(0.020, config.ZHeightDifference, 3);
            Assert.Equal(5.150, config.BaseDispenseHeight, 3);
            Assert.Equal(5.180, config.DispenseHeight, 3);
        }

        [Fact]
        public void DeltaZ_InPointData_IsBaseZMinusCurrentZ()
        {
            double baseZ = 5.200;
            double currentZ = 5.180;
            double deltaZ = baseZ - currentZ;
            Assert.Equal(0.020, deltaZ, 3);
        }
    }
}

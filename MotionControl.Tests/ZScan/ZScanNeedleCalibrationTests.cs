using Core.Services;
using MotionControl.Interfaces;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace MotionControl.Tests
{
    public class ZScanNeedleCalibrationTests
    {
        private ZScanCalibrationService CreateService()
        {
            return new ZScanCalibrationService();
        }

        [Fact]
        public void SetBaseZ_UpdatesBaseZValue()
        {
            var service = CreateService();
            service.SetBaseZ(5.200);
            Assert.Equal(5.200, service.BaseZ, 3);
        }

        [Fact]
        public void TeachNeedleMZ_UpdatesMeasuredMZ()
        {
            var service = CreateService();
            service.SetBaseZ(5.200);
            service.TeachNeedleMZ(5.150);
            Assert.Equal(5.150, service.MeasuredMZ, 3);
        }

        [Fact]
        public void CalculateDispenseHeight_ReturnsCorrectValue()
        {
            var service = CreateService();
            service.SetBaseZ(5.200);
            service.TeachNeedleMZ(5.150);
            double baseDispenseHeight = 5.150;
            double currentZHeight = 5.150;
            double needleComp = 0.010;
            double dispenseHeight = service.CalculateDispenseHeight(5.200, baseDispenseHeight, currentZHeight, needleComp);
            Assert.Equal(5.210, dispenseHeight, 3);
        }

        [Fact]
        public void CalculateDispenseHeight_WithGlobalVarComp()
        {
            var service = CreateService();
            service.SetBaseZ(5.200);
            service.TeachNeedleMZ(5.150);
            double baseDispenseHeight = 5.150;
            double currentZHeight = 5.150;
            double needleComp = 0.020;
            double dispenseHeight = service.CalculateDispenseHeight(5.200, baseDispenseHeight, currentZHeight, needleComp);
            Assert.Equal(5.220, dispenseHeight, 3);
        }

        [Fact]
        public void SetBaseZ_FiresCalibrationChanged()
        {
            var service = CreateService();
            bool fired = false;
            service.CalibrationChanged += () => fired = true;
            service.SetBaseZ(5.200);
            Assert.True(fired);
        }

        [Fact]
        public void TeachNeedleMZ_FiresCalibrationChanged()
        {
            var service = CreateService();
            bool fired = false;
            service.CalibrationChanged += () => fired = true;
            service.TeachNeedleMZ(5.150);
            Assert.True(fired);
        }

        [Fact]
        public void ResetCalibration_ClearsBaseZAndMZ()
        {
            var service = CreateService();
            service.SetBaseZ(5.200);
            service.TeachNeedleMZ(5.150);
            service.ResetCalibration();
            Assert.Equal(0, service.BaseZ);
            Assert.Equal(0, service.MeasuredMZ);
        }

        [Fact]
        public async Task NeedleTeachService_MoveToBaseZ_CallsMotionService()
        {
            var motionMock = new Mock<IMotionService>();
            motionMock.Setup(m => m.MoveAbsAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var service = new Module.Services.NeedleTeachService(motionMock.Object);
            await service.MoveNeedleToBaseZAsync(1, 5.200, 10.0);

            motionMock.Verify(m => m.MoveAbsAsync(1, 5.200, 10.0, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task NeedleTeachService_TeachCurrentPosition_ReturnsZPosition()
        {
            var motionMock = new Mock<IMotionService>();
            motionMock.Setup(m => m.GetAxisPosition(It.IsAny<int>()))
                .Returns(5.150);

            var service = new Module.Services.NeedleTeachService(motionMock.Object);
            double mz = await service.TeachCurrentPositionAsync(1);

            Assert.Equal(5.150, mz, 3);
        }
    }
}

using Core.Models;
using Module.Models;
using Xunit;

namespace MotionControl.Tests
{
    public class ZScanPointDetailTests
    {
        [Fact]
        public void ZScanPointDetail_GlobalVariableLink_DefaultNull()
        {
            var point = new ZScanPointDetail();

            Assert.Null(point.ZActualLink);
            Assert.False(point.IsZActualLinked);
        }

        [Fact]
        public void ZScanPointDetail_SetGlobalVariableLink_UpdatesIsLinked()
        {
            var point = new ZScanPointDetail
            {
                ZActualLink = new ZScanGlobalVariableLink
                {
                    IsLinked = true,
                    VariableName = "Z_Height_1",
                    VariableType = GlobalVariableType.Double
                }
            };

            Assert.NotNull(point.ZActualLink);
            Assert.True(point.IsZActualLinked);
            Assert.Equal("Z_Height_1", point.ZActualLink.VariableName);
        }

        [Fact]
        public void ZScanPointDetail_UnsetGlobalVariableLink_IsLinkedFalse()
        {
            var point = new ZScanPointDetail
            {
                ZActualLink = new ZScanGlobalVariableLink
                {
                    IsLinked = false,
                    VariableName = "",
                    VariableType = GlobalVariableType.Double
                }
            };

            Assert.False(point.IsZActualLinked);
        }

        [Fact]
        public void ZScanPointDetail_DeltaZCalculation_Basic()
        {
            var point = new ZScanPointDetail
            {
                ZMeasured = 5.012,
                Nominal = 5.000
            };

            var deltaZ = point.ZMeasured - point.Nominal;
            Assert.Equal(0.012, deltaZ, 3);
        }

        [Fact]
        public void ZScanPointDetail_DeltaZCalculation_WithCalibrationOffset()
        {
            double cameraOffset = 0.5;
            double needleOffset = 0.3;
            double totalOffset = cameraOffset + needleOffset;

            double measuredZ = 5.012;
            double compensatedZ = measuredZ + totalOffset;
            double nominal = 5.000;
            double deltaZ = compensatedZ - nominal;

            Assert.Equal(5.812, compensatedZ, 3);
            Assert.Equal(0.812, deltaZ, 3);
        }
    }
}

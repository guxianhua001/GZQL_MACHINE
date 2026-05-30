using Core.Models;
using Newtonsoft.Json;
using Xunit;

namespace MotionControl.Tests
{
    public class ZScanCalibrationConfigTests
    {
        [Fact]
        public void CalibrationConfig_DefaultValues_AreCorrect()
        {
            var config = new ZScanCalibrationConfig();

            Assert.Equal(string.Empty, config.ConfigName);
            Assert.Equal(0.0, config.CameraZOffset);
            Assert.Equal(0.0, config.NeedleZOffset);
            Assert.Equal(default, config.LastCalibrationTime);
            Assert.Equal(string.Empty, config.Operator);
        }

        [Fact]
        public void CalibrationConfig_TotalOffset_IncludesNeedleCompensation()
        {
            var config = new ZScanCalibrationConfig
            {
                CameraZOffset = 0.5,
                NeedleZOffset = 0.3
            };

            Assert.Equal(0.8, config.TotalZOffset);
        }

        [Fact]
        public void CalibrationConfig_TotalOffset_BothZero_ReturnsZero()
        {
            var config = new ZScanCalibrationConfig();

            Assert.Equal(0.0, config.TotalZOffset);
        }

        [Fact]
        public void CalibrationConfig_TotalOffset_NegativeValues_SumCorrectly()
        {
            var config = new ZScanCalibrationConfig
            {
                CameraZOffset = -0.2,
                NeedleZOffset = 0.1
            };

            Assert.Equal(-0.1, config.TotalZOffset);
        }

        [Fact]
        public void CalibrationConfig_Serialization_RoundTrip()
        {
            var original = new ZScanCalibrationConfig
            {
                ConfigName = "TestCalibration",
                CameraZOffset = 1.23,
                NeedleZOffset = 0.45,
                LastCalibrationTime = new DateTime(2026, 5, 27, 10, 30, 0),
                Operator = "TestUser"
            };

            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<ZScanCalibrationConfig>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(original.ConfigName, deserialized.ConfigName);
            Assert.Equal(original.CameraZOffset, deserialized.CameraZOffset);
            Assert.Equal(original.NeedleZOffset, deserialized.NeedleZOffset);
            Assert.Equal(original.LastCalibrationTime, deserialized.LastCalibrationTime);
            Assert.Equal(original.Operator, deserialized.Operator);
            Assert.Equal(original.TotalZOffset, deserialized.TotalZOffset);
        }

        [Fact]
        public void CalibrationConfig_NeedleOffset_AppliedCorrectly()
        {
            var config = new ZScanCalibrationConfig
            {
                CameraZOffset = 1.0,
                NeedleZOffset = 0.0
            };

            Assert.Equal(1.0, config.TotalZOffset);

            config.NeedleZOffset = 0.5;
            Assert.Equal(1.5, config.TotalZOffset);
        }
    }
}

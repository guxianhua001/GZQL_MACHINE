using Core.Models;
using Core.Services;
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Xunit;

namespace MotionControl.Tests
{
    public class ZScanConfigServiceTests : IDisposable
    {
        private readonly string _testDir;

        public ZScanConfigServiceTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"ZScanTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }

        [Fact]
        public void SaveAndLoad_RoundTrip_PreservesAllData()
        {
            var service = new ZScanConfigService(_testDir);
            var config = new ZScanConfigFile
            {
                DefaultTableName = "Table1",
                Tables = new System.Collections.Generic.List<ZScanTableConfig>
                {
                    new ZScanTableConfig
                    {
                        TableName = "Table1",
                        DataFormat = ZScanDataFormat.Double,
                        Calibration = new ZScanCalibrationConfig
                        {
                            CameraZOffset = 1.5,
                            NeedleZOffset = 0.3
                        }
                    }
                }
            };

            service.Save(config);
            var loaded = service.Load();

            Assert.NotNull(loaded);
            Assert.Equal("Table1", loaded.DefaultTableName);
            Assert.Single(loaded.Tables);
            Assert.Equal("Table1", loaded.Tables[0].TableName);
            Assert.Equal(ZScanDataFormat.Double, loaded.Tables[0].DataFormat);
            Assert.Equal(1.5, loaded.Tables[0].Calibration.CameraZOffset);
            Assert.Equal(0.3, loaded.Tables[0].Calibration.NeedleZOffset);
        }

        [Fact]
        public void Load_NonExistentFile_ReturnsDefault()
        {
            var service = new ZScanConfigService(_testDir);
            var loaded = service.Load("NonExistent.json");

            Assert.NotNull(loaded);
            Assert.Empty(loaded.Tables);
            Assert.Equal(string.Empty, loaded.DefaultTableName);
        }

        [Fact]
        public void Save_CreatesDirectory_IfNotExists()
        {
            var nestedDir = Path.Combine(_testDir, "Nested", "Config");
            var service = new ZScanConfigService(nestedDir);
            var config = new ZScanConfigFile();

            service.Save(config);

            Assert.True(Directory.Exists(nestedDir));
        }

        [Fact]
        public void Save_MultipleTables_AllPreserved()
        {
            var service = new ZScanConfigService(_testDir);
            var config = new ZScanConfigFile
            {
                Tables = new System.Collections.Generic.List<ZScanTableConfig>
                {
                    new ZScanTableConfig { TableName = "T1", DataFormat = ZScanDataFormat.Double },
                    new ZScanTableConfig { TableName = "T2", DataFormat = ZScanDataFormat.DoubleArray },
                    new ZScanTableConfig { TableName = "T3", DataFormat = ZScanDataFormat.Double }
                }
            };

            service.Save(config);
            var loaded = service.Load();

            Assert.Equal(3, loaded.Tables.Count);
            Assert.Equal("T1", loaded.Tables[0].TableName);
            Assert.Equal(ZScanDataFormat.DoubleArray, loaded.Tables[1].DataFormat);
            Assert.Equal("T3", loaded.Tables[2].TableName);
        }

        [Fact]
        public void DefaultPath_ContainsZScan()
        {
            var service = new ZScanConfigService(_testDir);
            var configPath = service.GetConfigPath();

            Assert.Contains("ZScan", configPath);
        }

        [Fact]
        public void Save_WithPoints_PreservesPointData()
        {
            var service = new ZScanConfigService(_testDir);
            var config = new ZScanConfigFile
            {
                Tables = new System.Collections.Generic.List<ZScanTableConfig>
                {
                    new ZScanTableConfig
                    {
                        TableName = "PointsTable",
                        Points = new System.Collections.Generic.List<ZScanPointData>
                        {
                            new ZScanPointData { Segment = 1, PointNumber = 1, X = 10.5, Y = 20.3, Nominal = 5.0, DataIndex = 0 },
                            new ZScanPointData { Segment = 1, PointNumber = 2, X = 11.0, Y = 21.0, Nominal = 5.1, DataIndex = 1 }
                        }
                    }
                }
            };

            service.Save(config);
            var loaded = service.Load();

            Assert.Equal(2, loaded.Tables[0].Points.Count);
            Assert.Equal(10.5, loaded.Tables[0].Points[0].X);
            Assert.Equal(5.1, loaded.Tables[0].Points[1].Nominal);
            Assert.Equal(1, loaded.Tables[0].Points[1].DataIndex);
        }

        [Fact]
        public void Save_WithGlobalVariableLink_PreservesLink()
        {
            var service = new ZScanConfigService(_testDir);
            var config = new ZScanConfigFile
            {
                Tables = new System.Collections.Generic.List<ZScanTableConfig>
                {
                    new ZScanTableConfig
                    {
                        TableName = "LinkedTable",
                        ZActualLink = new ZScanGlobalVariableLink
                        {
                            IsLinked = true,
                            VariableName = "Z_Arc_Heights",
                            VariableType = GlobalVariableType.DoubleArray
                        }
                    }
                }
            };

            service.Save(config);
            var loaded = service.Load();

            Assert.True(loaded.Tables[0].ZActualLink.IsLinked);
            Assert.Equal("Z_Arc_Heights", loaded.Tables[0].ZActualLink.VariableName);
            Assert.Equal(GlobalVariableType.DoubleArray, loaded.Tables[0].ZActualLink.VariableType);
        }
    }
}

using Core.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using Xunit;

namespace MotionControl.Tests
{
    public class ZScanTableConfigTests
    {
        [Fact]
        public void ZScanTableConfig_DefaultFormat_IsDouble()
        {
            var config = new ZScanTableConfig();

            Assert.Equal(ZScanDataFormat.Double, config.DataFormat);
        }

        [Fact]
        public void ZScanTableConfig_ArrayFormat_SupportsDoubleArray()
        {
            var config = new ZScanTableConfig
            {
                DataFormat = ZScanDataFormat.DoubleArray
            };

            Assert.Equal(ZScanDataFormat.DoubleArray, config.DataFormat);
        }

        [Fact]
        public void ZScanGlobalVariableLink_DefaultValues()
        {
            var link = new ZScanGlobalVariableLink();

            Assert.False(link.IsLinked);
            Assert.Equal(string.Empty, link.VariableName);
            Assert.Equal(GlobalVariableType.Double, link.VariableType);
        }

        [Fact]
        public void ZScanGlobalVariableLink_LinkedState()
        {
            var link = new ZScanGlobalVariableLink
            {
                IsLinked = true,
                VariableName = "Z_Actual_1",
                VariableType = GlobalVariableType.DoubleArray
            };

            Assert.True(link.IsLinked);
            Assert.Equal("Z_Actual_1", link.VariableName);
            Assert.Equal(GlobalVariableType.DoubleArray, link.VariableType);
        }

        [Fact]
        public void ZScanTableConfig_GlobalVariableLink_Serialization()
        {
            var config = new ZScanTableConfig
            {
                TableName = "ArcScan1",
                DataFormat = ZScanDataFormat.DoubleArray,
                ZActualLink = new ZScanGlobalVariableLink
                {
                    IsLinked = true,
                    VariableName = "Z_Arc_Heights",
                    VariableType = GlobalVariableType.DoubleArray
                }
            };

            var json = JsonConvert.SerializeObject(config);
            var deserialized = JsonConvert.DeserializeObject<ZScanTableConfig>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("ArcScan1", deserialized.TableName);
            Assert.Equal(ZScanDataFormat.DoubleArray, deserialized.DataFormat);
            Assert.NotNull(deserialized.ZActualLink);
            Assert.True(deserialized.ZActualLink.IsLinked);
            Assert.Equal("Z_Arc_Heights", deserialized.ZActualLink.VariableName);
        }

        [Fact]
        public void ZScanConfigFile_Collection_Serialization()
        {
            var configFile = new ZScanConfigFile
            {
                DefaultTableName = "Table1",
                Tables = new List<ZScanTableConfig>
                {
                    new ZScanTableConfig { TableName = "Table1", DataFormat = ZScanDataFormat.Double },
                    new ZScanTableConfig { TableName = "ArcTable", DataFormat = ZScanDataFormat.DoubleArray }
                }
            };

            var json = JsonConvert.SerializeObject(configFile);
            var deserialized = JsonConvert.DeserializeObject<ZScanConfigFile>(json);

            Assert.NotNull(deserialized);
            Assert.Equal("Table1", deserialized.DefaultTableName);
            Assert.Equal(2, deserialized.Tables.Count);
            Assert.Equal("Table1", deserialized.Tables[0].TableName);
            Assert.Equal(ZScanDataFormat.Double, deserialized.Tables[0].DataFormat);
            Assert.Equal("ArcTable", deserialized.Tables[1].TableName);
            Assert.Equal(ZScanDataFormat.DoubleArray, deserialized.Tables[1].DataFormat);
        }

        [Fact]
        public void ZScanConfigFile_SharedTablesAndDualNeedleCalibration_Serialization()
        {
            var configFile = new ZScanConfigFile
            {
                Needle1Calibration = new ZScanCalibrationConfig { BaseZ = 5.1, CameraZOffset = 0.01 },
                Needle2Calibration = new ZScanCalibrationConfig { BaseZ = 6.2, CameraZOffset = 0.02 },
                Tables = new List<ZScanTableConfig>
                {
                    new ZScanTableConfig { TableName = "SharedTable1" }
                },
                DefaultTableName = "SharedTable1"
            };

            var json = JsonConvert.SerializeObject(configFile);
            var deserialized = JsonConvert.DeserializeObject<ZScanConfigFile>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(5.1, deserialized.Needle1Calibration.BaseZ, 3);
            Assert.Equal(6.2, deserialized.Needle2Calibration.BaseZ, 3);
            Assert.Single(deserialized.Tables);
            Assert.Equal("SharedTable1", deserialized.DefaultTableName);
        }

        [Fact]
        public void ZScanDataFormat_EnumValues()
        {
            Assert.Equal(0, (int)ZScanDataFormat.Double);
            Assert.Equal(1, (int)ZScanDataFormat.DoubleArray);
        }
    }
}

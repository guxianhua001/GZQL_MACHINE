using Core.Models;
using Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace MotionControl.Tests
{
    public class ZScanPersistenceTests
    {
        private string GetTempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ZScanTest_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(dir);
            return dir;
        }

        [Fact]
        public void SaveWithTimestamp_CreatesFileWithTimestampName()
        {
            var dir = GetTempDir();
            try
            {
                var service = new ZScanConfigService(dir);
                var config = new ZScanConfigFile
                {
                    Tables = new List<ZScanTableConfig>
                    {
                        new ZScanTableConfig { TableName = "TestTable" }
                    }
                };

                string savedPath = service.SaveWithTimestamp(config);

                Assert.True(File.Exists(savedPath));
                Assert.Contains("ZScan_", Path.GetFileName(savedPath));
                Assert.EndsWith(".json", Path.GetFileName(savedPath));
                Assert.Matches(@"ZScan_\d{8}_\d{6}\.json", Path.GetFileName(savedPath));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void SaveWithTimestamp_SetsLastSavedFilePath()
        {
            var dir = GetTempDir();
            try
            {
                var service = new ZScanConfigService(dir);
                var config = new ZScanConfigFile();

                string path = service.SaveWithTimestamp(config);

                Assert.Equal(path, service.LastSavedFilePath);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void LoadLastFromRecipePool_NoFile_ReturnsEmpty()
        {
            var dir = GetTempDir();
            try
            {
                var service = new ZScanConfigService(dir);
                var result = service.LoadLastFromRecipePool();
                Assert.NotNull(result);
                Assert.Empty(result.Tables);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void LoadLastFromRecipePool_WithLastSavedFile_ReturnsConfig()
        {
            var dir = GetTempDir();
            try
            {
                var service = new ZScanConfigService(dir);
                var config = new ZScanConfigFile
                {
                    Tables = new List<ZScanTableConfig>
                    {
                        new ZScanTableConfig { TableName = "AutoLoad" }
                    }
                };

                service.SaveWithTimestamp(config);
                var loaded = service.LoadLastFromRecipePool();

                Assert.Single(loaded.Tables);
                Assert.Equal("AutoLoad", loaded.Tables[0].TableName);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void SaveToRecipePool_SavesToRecipePoolPath()
        {
            var dir = GetTempDir();
            try
            {
                var service = new ZScanConfigService(dir);
                var config = new ZScanConfigFile
                {
                    Tables = new List<ZScanTableConfig>
                    {
                        new ZScanTableConfig { TableName = "RecipeData" }
                    }
                };

                service.SaveToRecipePool(config, "RecipeA");
                string recipeFile = Path.Combine(dir, "RecipePool", "RecipeA_ZScan.json");

                Assert.True(File.Exists(recipeFile));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void SaveWithTimestamp_RoundTrip_DataPreserved()
        {
            var dir = GetTempDir();
            try
            {
                var service = new ZScanConfigService(dir);
                var config = new ZScanConfigFile
                {
                    DefaultTableName = "Table1",
                    Tables = new List<ZScanTableConfig>
                    {
                        new ZScanTableConfig
                        {
                            TableName = "Table1",
                            DataFormat = ZScanDataFormat.DoubleArray,
                            Points = new List<ZScanPointData>
                            {
                                new ZScanPointData
                                {
                                    Segment = 1, PointNumber = 1,
                                    X = 10.5, Y = 20.3,
                                    PointType = ZScanDataFormat.DoubleArray,
                                    GlobalVariableLink = new ZScanGlobalVariableLink
                                    {
                                        IsLinked = true,
                                        VariableName = "GV_Arc",
                                        VariableType = GlobalVariableType.DoubleArray
                                    }
                                }
                            }
                        }
                    }
                };

                string path = service.SaveWithTimestamp(config);
                var service2 = new ZScanConfigService(dir);
                var loaded = service2.Load(Path.GetFileName(path));

                Assert.Equal("Table1", loaded.DefaultTableName);
                Assert.Single(loaded.Tables);
                Assert.Equal(ZScanDataFormat.DoubleArray, loaded.Tables[0].Points[0].PointType);
                Assert.True(loaded.Tables[0].Points[0].GlobalVariableLink.IsLinked);
                Assert.Equal("GV_Arc", loaded.Tables[0].Points[0].GlobalVariableLink.VariableName);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}

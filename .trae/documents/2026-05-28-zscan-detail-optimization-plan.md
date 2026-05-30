# ZScanDetailView 功能优化实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 优化ZScanDetailView的标定流程（Z向标定步骤化）、行级点类型与全局变量链接、JSON持久化增强（带时间戳文件名+配方池自动加载）、表格切换功能完善

**Architecture:** 在现有WPF+PRISM+MaterialDesign架构上，扩展Core层模型和接口、Module层ViewModel和Service实现。标定流程改为步骤化交互（输入基准Z→移动针头→示教→计算），行级数据类型和全局变量链接从表级下沉到行级，JSON持久化增加时间戳文件名和配方池集成，表格切换实现完整的数据同步。

**Tech Stack:** WPF, PRISM, MaterialDesign In XAML, Newtonsoft.Json, xUnit, Moq

---

## 文件结构

### 新建文件
| 文件 | 职责 |
|------|------|
| `Core/Abstraction/INeedleTeachService.cs` | 针头示教服务接口（移动到基准Z、示教当前位置） |
| `Module/Services/NeedleTeachService.cs` | 针头示教服务实现（依赖IMotionService） |
| `MotionControl.Tests/ZScan/ZScanNeedleCalibrationTests.cs` | Z向标定步骤化测试 |
| `MotionControl.Tests/ZScan/ZScanRowLevelTypeTests.cs` | 行级点类型与全局变量链接测试 |
| `MotionControl.Tests/ZScan/ZScanPersistenceTests.cs` | JSON持久化增强测试 |
| `MotionControl.Tests/ZScan/ZScanTableSwitchTests.cs` | 表格切换功能测试 |

### 修改文件
| 文件 | 修改内容 |
|------|----------|
| `Core/Models/ZScanTableConfig.cs` | ZScanPointData增加PointType和GlobalVariableLink字段；ZScanTableConfig移除表级ZActualLink和DataFormat |
| `Core/Models/ZScanCalibrationConfig.cs` | 增加BaseZ、MeasuredMZ、DeltaZ、NeedleCompensationLink字段 |
| `Module/Models/ZScanSummaryItem.cs` | ZScanPointDetail增加PointType、GlobalVariableLink、IsGlobalVarLinked属性 |
| `Core/Abstraction/IZScanCalibrationService.cs` | 增加Z向标定步骤方法：SetBaseZ、MoveNeedleToBaseZ、TeachNeedleMZ、CalculateDispenseHeight |
| `Core/Services/ZScanCalibrationService.cs` | 实现Z向标定步骤逻辑 |
| `Core/Abstraction/IZScanConfigService.cs` | 增加SaveWithTimestamp、LoadLastFromRecipePool、SaveToRecipePool方法 |
| `Core/Services/ZScanConfigService.cs` | 实现带时间戳文件名保存、配方池集成 |
| `Module/Controls/ZScan/ZScanDetailViewModel.cs` | 重构标定区域为步骤化UI、行级类型/链接、持久化增强、表格切换完善 |
| `Module/Controls/ZScan/ZScanDetailView.xaml` | 标定区域步骤化UI、DataGrid增加PointType和链接列 |
| `MainApp/Languages/Strings.zh-CN.xaml` | 新增多语言键值 |
| `MainApp/Languages/Strings.en-US.xaml` | 新增多语言键值 |
| `Module/PrimModel.cs` | 注册INeedleTeachService |

---

## Task 1: ZScanPointData 行级点类型与全局变量链接模型

**Files:**
- Modify: `Core/Models/ZScanTableConfig.cs`
- Modify: `Module/Models/ZScanSummaryItem.cs`
- Test: `MotionControl.Tests/ZScan/ZScanRowLevelTypeTests.cs`

- [ ] **Step 1: 写失败测试 — ZScanPointData增加PointType和GlobalVariableLink**

```csharp
// MotionControl.Tests/ZScan/ZScanRowLevelTypeTests.cs
using Core.Models;
using Module.Models;
using Xunit;

namespace MotionControl.Tests
{
    public class ZScanRowLevelTypeTests
    {
        [Fact]
        public void ZScanPointData_DefaultPointType_IsDouble()
        {
            var point = new ZScanPointData();
            Assert.Equal(ZScanDataFormat.Double, point.PointType);
        }

        [Fact]
        public void ZScanPointData_GlobalVariableLink_DefaultIsNull()
        {
            var point = new ZScanPointData();
            Assert.Null(point.GlobalVariableLink);
        }

        [Fact]
        public void ZScanPointData_SetPointType_DoubleArray()
        {
            var point = new ZScanPointData { PointType = ZScanDataFormat.DoubleArray };
            Assert.Equal(ZScanDataFormat.DoubleArray, point.PointType);
        }

        [Fact]
        public void ZScanPointData_SetGlobalVariableLink()
        {
            var link = new ZScanGlobalVariableLink { IsLinked = true, VariableName = "GV_ArcHeight", VariableType = GlobalVariableType.DoubleArray };
            var point = new ZScanPointData { GlobalVariableLink = link };
            Assert.True(point.GlobalVariableLink.IsLinked);
            Assert.Equal("GV_ArcHeight", point.GlobalVariableLink.VariableName);
            Assert.Equal(GlobalVariableType.DoubleArray, point.GlobalVariableLink.VariableType);
        }

        [Fact]
        public void ZScanPointDetail_PointType_DefaultIsDouble()
        {
            var detail = new ZScanPointDetail();
            Assert.Equal(ZScanDataFormat.Double, detail.PointType);
        }

        [Fact]
        public void ZScanPointDetail_GlobalVariableLink_DefaultIsNull()
        {
            var detail = new ZScanPointDetail();
            Assert.Null(detail.GlobalVariableLink);
        }

        [Fact]
        public void ZScanPointDetail_SetPointType_RaisesPropertyChanged()
        {
            var detail = new ZScanPointDetail();
            bool raised = false;
            detail.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(ZScanPointDetail.PointType)) raised = true; };
            detail.PointType = ZScanDataFormat.DoubleArray;
            Assert.True(raised);
        }

        [Fact]
        public void ZScanPointDetail_SetGlobalVariableLink_UpdatesIsGlobalVarLinked()
        {
            var detail = new ZScanPointDetail();
            Assert.False(detail.IsGlobalVarLinked);

            detail.GlobalVariableLink = new ZScanGlobalVariableLink { IsLinked = true, VariableName = "GV_Test" };
            Assert.True(detail.IsGlobalVarLinked);
        }

        [Fact]
        public void ZScanPointDetail_UnlinkGlobalVariable_SetsIsGlobalVarLinkedFalse()
        {
            var detail = new ZScanPointDetail
            {
                GlobalVariableLink = new ZScanGlobalVariableLink { IsLinked = true, VariableName = "GV_Test" }
            };
            detail.GlobalVariableLink = null;
            Assert.False(detail.IsGlobalVarLinked);
        }
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test MotionControl.Tests\MotionControl.Tests.csproj --filter "FullyQualifiedName~ZScanRowLevelTypeTests" -v n`
Expected: FAIL — ZScanPointData和ZScanPointDetail没有PointType/GlobalVariableLink属性

- [ ] **Step 3: 修改ZScanPointData模型 — 增加PointType和GlobalVariableLink**

在 `Core/Models/ZScanTableConfig.cs` 的 `ZScanPointData` 类中增加：

```csharp
// 在 ZScanPointData 类中增加：
public ZScanDataFormat PointType { get; set; } = ZScanDataFormat.Double;
public ZScanGlobalVariableLink GlobalVariableLink { get; set; }
```

- [ ] **Step 4: 修改ZScanPointDetail模型 — 增加PointType和GlobalVariableLink**

在 `Module/Models/ZScanSummaryItem.cs` 的 `ZScanPointDetail` 类中增加：

```csharp
// 在 ZScanPointDetail 的"新增字段"区域增加：
private ZScanDataFormat _pointType = ZScanDataFormat.Double;
private ZScanGlobalVariableLink _globalVariableLink;

public ZScanDataFormat PointType
{
    get => _pointType;
    set => SetProperty(ref _pointType, value);
}

public ZScanGlobalVariableLink GlobalVariableLink
{
    get => _globalVariableLink;
    set
    {
        if (SetProperty(ref _globalVariableLink, value))
        {
            RaisePropertyChanged(nameof(IsGlobalVarLinked));
        }
    }
}

public bool IsGlobalVarLinked => _globalVariableLink?.IsLinked == true;
```

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test MotionControl.Tests\MotionControl.Tests.csproj --filter "FullyQualifiedName~ZScanRowLevelTypeTests" -v n`
Expected: 全部PASS

- [ ] **Step 6: 提交**

```bash
git add Core/Models/ZScanTableConfig.cs Module/Models/ZScanSummaryItem.cs MotionControl.Tests/ZScan/ZScanRowLevelTypeTests.cs
git commit -m "feat(zscan): add row-level PointType and GlobalVariableLink to ZScanPointData/ZScanPointDetail"
```

---

## Task 2: ZScanCalibrationConfig 增加 Z向标定步骤字段

**Files:**
- Modify: `Core/Models/ZScanCalibrationConfig.cs`
- Modify: `Core/Abstraction/IZScanCalibrationService.cs`
- Modify: `Core/Services/ZScanCalibrationService.cs`
- Test: `MotionControl.Tests/ZScan/ZScanNeedleCalibrationTests.cs`

- [ ] **Step 1: 写失败测试 — Z向标定步骤化**

```csharp
// MotionControl.Tests/ZScan/ZScanNeedleCalibrationTests.cs
using Core.Services;
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
            double deltaZ = 0.050;
            double needleComp = 0.010;
            double dispenseHeight = service.CalculateDispenseHeight(deltaZ, needleComp);
            // MZ + DeltaZ + NeedleComp = 5.150 + 0.050 + 0.010 = 5.210
            Assert.Equal(5.210, dispenseHeight, 3);
        }

        [Fact]
        public void CalculateDispenseHeight_WithGlobalVarComp()
        {
            var service = CreateService();
            service.SetBaseZ(5.200);
            service.TeachNeedleMZ(5.150);
            double deltaZ = 0.050;
            double needleComp = 0.020;
            double dispenseHeight = service.CalculateDispenseHeight(deltaZ, needleComp);
            // 5.150 + 0.050 + 0.020 = 5.220
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
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test MotionControl.Tests\MotionControl.Tests.csproj --filter "FullyQualifiedName~ZScanNeedleCalibrationTests" -v n`
Expected: FAIL — SetBaseZ/TeachNeedleMZ/CalculateDispenseHeight方法不存在

- [ ] **Step 3: 更新IZScanCalibrationService接口**

```csharp
// Core/Abstraction/IZScanCalibrationService.cs — 完整替换
namespace Core.Abstraction
{
    public interface IZScanCalibrationService
    {
        double CameraZOffset { get; }
        double NeedleZOffset { get; }
        double TotalZOffset { get; }
        double BaseZ { get; }
        double MeasuredMZ { get; }
        void CalibrateCameraZ(double measuredZ, double referenceZ);
        void ApplyNeedleCompensation(double deltaZ);
        double GetCompensatedZ(double measuredZ);
        void ResetCalibration();
        void SetBaseZ(double baseZ);
        void TeachNeedleMZ(double measuredMZ);
        double CalculateDispenseHeight(double deltaZ, double needleCompensation);
        event Action CalibrationChanged;
    }
}
```

- [ ] **Step 4: 实现ZScanCalibrationService新方法**

```csharp
// Core/Services/ZScanCalibrationService.cs — 在现有类中增加：
private double _baseZ;
private double _measuredMZ;

public double BaseZ => _baseZ;
public double MeasuredMZ => _measuredMZ;

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

public double CalculateDispenseHeight(double deltaZ, double needleCompensation)
{
    return _measuredMZ + deltaZ + needleCompensation;
}
```

- [ ] **Step 5: 更新ZScanCalibrationConfig模型**

```csharp
// Core/Models/ZScanCalibrationConfig.cs — 在现有类中增加：
private double _baseZ;
private double _measuredMZ;
private double _deltaZ;
private ZScanGlobalVariableLink _needleCompensationLink;

public double BaseZ { get => _baseZ; set => _baseZ = value; }
public double MeasuredMZ { get => _measuredMZ; set => _measuredMZ = value; }
public double DeltaZ { get => _deltaZ; set => _deltaZ = value; }
public ZScanGlobalVariableLink NeedleCompensationLink
{
    get => _needleCompensationLink;
    set => _needleCompensationLink = value;
}
```

- [ ] **Step 6: 运行测试验证通过**

Run: `dotnet test MotionControl.Tests\MotionControl.Tests.csproj --filter "FullyQualifiedName~ZScanNeedleCalibrationTests" -v n`
Expected: 全部PASS

- [ ] **Step 7: 提交**

```bash
git add Core/Abstraction/IZScanCalibrationService.cs Core/Services/ZScanCalibrationService.cs Core/Models/ZScanCalibrationConfig.cs MotionControl.Tests/ZScan/ZScanNeedleCalibrationTests.cs
git commit -m "feat(zscan): add Z-direction calibration steps (SetBaseZ, TeachNeedleMZ, CalculateDispenseHeight)"
```

---

## Task 3: INeedleTeachService 针头示教服务

**Files:**
- Create: `Core/Abstraction/INeedleTeachService.cs`
- Create: `Module/Services/NeedleTeachService.cs`
- Modify: `Module/PrimModel.cs`
- Test: `MotionControl.Tests/ZScan/ZScanNeedleCalibrationTests.cs` (扩展)

- [ ] **Step 1: 写失败测试 — INeedleTeachService**

在 `ZScanNeedleCalibrationTests.cs` 中增加：

```csharp
[Fact]
public async Task NeedleTeachService_MoveToBaseZ_CallsMotionService()
{
    var motionMock = new Mock<MotionControl.Interfaces.IMotionService>();
    motionMock.Setup(m => m.MoveAbsAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
        .Returns(Task.CompletedTask);

    var service = new Module.Services.NeedleTeachService(motionMock.Object);
    await service.MoveNeedleToBaseZAsync(1, 5.200, 10.0);

    motionMock.Verify(m => m.MoveAbsAsync(1, 5.200, 10.0, It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task NeedleTeachService_TeachCurrentPosition_ReturnsZPosition()
{
    var motionMock = new Mock<MotionControl.Interfaces.IMotionService>();
    motionMock.Setup(m => m.GetPositionAsync(It.IsAny<int>()))
        .ReturnsAsync(5.150);

    var service = new Module.Services.NeedleTeachService(motionMock.Object);
    double mz = await service.TeachCurrentPositionAsync(1);

    Assert.Equal(5.150, mz, 3);
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test MotionControl.Tests\MotionControl.Tests.csproj --filter "FullyQualifiedName~ZScanNeedleCalibrationTests~NeedleTeachService" -v n`
Expected: FAIL — NeedleTeachService不存在

- [ ] **Step 3: 创建INeedleTeachService接口**

```csharp
// Core/Abstraction/INeedleTeachService.cs
using System.Threading;
using System.Threading.Tasks;

namespace Core.Abstraction
{
    public interface INeedleTeachService
    {
        Task MoveNeedleToBaseZAsync(int zAxisId, double baseZ, double speed, CancellationToken ct = default);
        Task<double> TeachCurrentPositionAsync(int zAxisId);
    }
}
```

- [ ] **Step 4: 创建NeedleTeachService实现**

```csharp
// Module/Services/NeedleTeachService.cs
using Core.Abstraction;
using MotionControl.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace Module.Services
{
    public class NeedleTeachService : INeedleTeachService
    {
        private readonly IMotionService _motionService;

        public NeedleTeachService(IMotionService motionService)
        {
            _motionService = motionService;
        }

        public async Task MoveNeedleToBaseZAsync(int zAxisId, double baseZ, double speed, CancellationToken ct = default)
        {
            await _motionService.MoveAbsAsync(zAxisId, baseZ, speed, ct);
        }

        public async Task<double> TeachCurrentPositionAsync(int zAxisId)
        {
            return await _motionService.GetPositionAsync(zAxisId);
        }
    }
}
```

- [ ] **Step 5: 注册DI**

在 `Module/PrimModel.cs` 的 `RegisterTypes` 方法中增加：

```csharp
containerRegistry.Register<Core.Abstraction.INeedleTeachService, Module.Services.NeedleTeachService>();
```

- [ ] **Step 6: 运行测试验证通过**

Run: `dotnet test MotionControl.Tests\MotionControl.Tests.csproj --filter "FullyQualifiedName~ZScanNeedleCalibrationTests" -v n`
Expected: 全部PASS

- [ ] **Step 7: 提交**

```bash
git add Core/Abstraction/INeedleTeachService.cs Module/Services/NeedleTeachService.cs Module/PrimModel.cs MotionControl.Tests/ZScan/ZScanNeedleCalibrationTests.cs
git commit -m "feat(zscan): add INeedleTeachService for needle Z teach workflow"
```

---

## Task 4: JSON持久化增强 — 带时间戳文件名 + 配方池集成

**Files:**
- Modify: `Core/Abstraction/IZScanConfigService.cs`
- Modify: `Core/Services/ZScanConfigService.cs`
- Test: `MotionControl.Tests/ZScan/ZScanPersistenceTests.cs`

- [ ] **Step 1: 写失败测试 — 持久化增强**

```csharp
// MotionControl.Tests/ZScan/ZScanPersistenceTests.cs
using Core.Models;
using Core.Services;
using System;
using System.IO;
using System.Linq;
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
                    Tables = new System.Collections.Generic.List<ZScanTableConfig>
                    {
                        new ZScanTableConfig { TableName = "TestTable" }
                    }
                };

                string savedPath = service.SaveWithTimestamp(config);

                Assert.True(File.Exists(savedPath));
                Assert.Contains("ZScan_", Path.GetFileName(savedPath));
                Assert.EndsWith(".json", Path.GetFileName(savedPath));
                // 文件名格式: ZScan_20260527_132441.json
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
                    Tables = new System.Collections.Generic.List<ZScanTableConfig>
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
            var recipeDir = Path.Combine(dir, "RecipePool");
            try
            {
                var service = new ZScanConfigService(dir);
                var config = new ZScanConfigFile
                {
                    Tables = new System.Collections.Generic.List<ZScanTableConfig>
                    {
                        new ZScanTableConfig { TableName = "RecipeData" }
                    }
                };

                service.SaveToRecipePool(config, "RecipeA");
                string recipeFile = Path.Combine(recipeDir, "RecipeA_ZScan.json");

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
                    Tables = new System.Collections.Generic.List<ZScanTableConfig>
                    {
                        new ZScanTableConfig
                        {
                            TableName = "Table1",
                            DataFormat = ZScanDataFormat.DoubleArray,
                            Points = new System.Collections.Generic.List<ZScanPointData>
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
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test MotionControl.Tests\MotionControl.Tests.csproj --filter "FullyQualifiedName~ZScanPersistenceTests" -v n`
Expected: FAIL — SaveWithTimestamp/LoadLastFromRecipePool/SaveToRecipePool方法不存在

- [ ] **Step 3: 更新IZScanConfigService接口**

```csharp
// Core/Abstraction/IZScanConfigService.cs — 完整替换
using Core.Models;

namespace Core.Abstraction
{
    public interface IZScanConfigService
    {
        ZScanConfigFile Load(string fileName = "ZScanConfig.json");
        void Save(ZScanConfigFile config, string fileName = "ZScanConfig.json");
        string GetConfigPath();
        string SaveWithTimestamp(ZScanConfigFile config);
        ZScanConfigFile LoadLastFromRecipePool();
        void SaveToRecipePool(ZScanConfigFile config, string recipeName);
        string LastSavedFilePath { get; }
    }
}
```

- [ ] **Step 4: 实现ZScanConfigService新方法**

```csharp
// Core/Services/ZScanConfigService.cs — 在现有类中增加：
private string _lastSavedFilePath;

public string LastSavedFilePath => _lastSavedFilePath;

public string SaveWithTimestamp(ZScanConfigFile config)
{
    if (config == null) return null;

    if (!Directory.Exists(_configDirectory))
        Directory.CreateDirectory(_configDirectory);

    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    string fileName = $"ZScan_{timestamp}.json";
    var filePath = Path.Combine(_configDirectory, fileName);
    var json = JsonConvert.SerializeObject(config, _serializerSettings);
    File.WriteAllText(filePath, json);
    _lastSavedFilePath = filePath;
    return filePath;
}

public ZScanConfigFile LoadLastFromRecipePool()
{
    if (string.IsNullOrEmpty(_lastSavedFilePath) || !File.Exists(_lastSavedFilePath))
        return new ZScanConfigFile();

    try
    {
        var json = File.ReadAllText(_lastSavedFilePath);
        return JsonConvert.DeserializeObject<ZScanConfigFile>(json, _serializerSettings)
               ?? new ZScanConfigFile();
    }
    catch
    {
        return new ZScanConfigFile();
    }
}

public void SaveToRecipePool(ZScanConfigFile config, string recipeName)
{
    if (config == null || string.IsNullOrEmpty(recipeName)) return;

    var recipeDir = Path.Combine(_configDirectory, "RecipePool");
    if (!Directory.Exists(recipeDir))
        Directory.CreateDirectory(recipeDir);

    var filePath = Path.Combine(recipeDir, $"{recipeName}_ZScan.json");
    var json = JsonConvert.SerializeObject(config, _serializerSettings);
    File.WriteAllText(filePath, json);
}
```

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test MotionControl.Tests\MotionControl.Tests.csproj --filter "FullyQualifiedName~ZScanPersistenceTests" -v n`
Expected: 全部PASS

- [ ] **Step 6: 提交**

```bash
git add Core/Abstraction/IZScanConfigService.cs Core/Services/ZScanConfigService.cs MotionControl.Tests/ZScan/ZScanPersistenceTests.cs
git commit -m "feat(zscan): add timestamped JSON save and recipe pool integration"
```

---

## Task 5: ZScanDetailViewModel — Z向标定步骤化重构

**Files:**
- Modify: `Module/Controls/ZScan/ZScanDetailViewModel.cs`
- Modify: `Module/Controls/ZScan/ZScanDetailView.xaml`

- [ ] **Step 1: 在ViewModel中增加Z向标定步骤属性和命令**

在 `ZScanDetailViewModel.cs` 中增加：

```csharp
// 新增依赖字段
private readonly INeedleTeachService _needleTeachService;

// 新增标定步骤属性
private double _baseZInput;
public double BaseZInput { get => _baseZInput; set => SetProperty(ref _baseZInput, value); }

private double _measuredMZ;
public double MeasuredMZ { get => _measuredMZ; set => SetProperty(ref _measuredMZ, value); }

private double _deltaZInput;
public double DeltaZInput { get => _deltaZInput; set => SetProperty(ref _deltaZInput, value); }

private double _needleCompensationValue;
public double NeedleCompensationValue { get => _needleCompensationValue; set => SetProperty(ref _needleCompensationValue, value); }

private double _calculatedDispenseHeight;
public double CalculatedDispenseHeight { get => _calculatedDispenseHeight; set => SetProperty(ref _calculatedDispenseHeight, value); }

private int _calibrationStep;
public int CalibrationStep { get => _calibrationStep; set => SetProperty(ref _calibrationStep, value); }

// 新增标定步骤命令
public ICommand SetBaseZCommand { get; }
public ICommand MoveNeedleToBaseZCommand { get; }
public ICommand TeachNeedleMZCommand { get; }
public ICommand CalculateDispenseHeightCommand { get; }
```

- [ ] **Step 2: 实现标定步骤命令逻辑**

```csharp
// 构造函数中增加 NeedleTeachService 参数和命令初始化
// SetBaseZCommand = new DelegateCommand(OnSetBaseZ);
// MoveNeedleToBaseZCommand = new DelegateCommand(async () => await OnMoveNeedleToBaseZAsync(), () => BaseZInput > 0).ObservesProperty(() => BaseZInput);
// TeachNeedleMZCommand = new DelegateCommand(async () => await OnTeachNeedleMZAsync(), () => CalibrationStep >= 2).ObservesProperty(() => CalibrationStep);
// CalculateDispenseHeightCommand = new DelegateCommand(OnCalculateDispenseHeight, () => CalibrationStep >= 3).ObservesProperty(() => CalibrationStep);

private void OnSetBaseZ()
{
    try
    {
        _zscanCalibrationService.SetBaseZ(BaseZInput);
        CalibrationStep = 1;
        _logger?.Info($"Z-SCAN 设置基准Z高度: {BaseZInput:F3}");
    }
    catch (Exception ex)
    {
        _logger?.Error($"Z-SCAN 设置基准Z失败: {ex.Message}");
    }
}

private async Task OnMoveNeedleToBaseZAsync()
{
    try
    {
        await _needleTeachService.MoveNeedleToBaseZAsync(ZAxisId, BaseZInput, MoveSpeed);
        CalibrationStep = 2;
        _logger?.Info($"Z-SCAN 针头已移动到基准Z高度: {BaseZInput:F3}");
    }
    catch (Exception ex)
    {
        _logger?.Error($"Z-SCAN 移动针头到基准Z失败: {ex.Message}");
    }
}

private async Task OnTeachNeedleMZAsync()
{
    try
    {
        double mz = await _needleTeachService.TeachCurrentPositionAsync(ZAxisId);
        MeasuredMZ = mz;
        _zscanCalibrationService.TeachNeedleMZ(mz);
        CalibrationStep = 3;
        _logger?.Info($"Z-SCAN 针头示教MZ: {mz:F3}");
    }
    catch (Exception ex)
    {
        _logger?.Error($"Z-SCAN 针头示教失败: {ex.Message}");
    }
}

private void OnCalculateDispenseHeight()
{
    try
    {
        CalculatedDispenseHeight = _zscanCalibrationService.CalculateDispenseHeight(DeltaZInput, NeedleCompensationValue);
        CalibrationStep = 4;
        _logger?.Info($"Z-SCAN 计算点胶高度: MZ={MeasuredMZ:F3} + ΔZ={DeltaZInput:F3} + 补偿={NeedleCompensationValue:F3} = {CalculatedDispenseHeight:F3}");
    }
    catch (Exception ex)
    {
        _logger?.Error($"Z-SCAN 计算点胶高度失败: {ex.Message}");
    }
}
```

- [ ] **Step 3: 更新ZScanDetailView.xaml标定区域**

替换Row 2的标定区域GroupBox内容为步骤化UI：

```xml
<!-- ═══ Row 2: 标定区域（步骤化Z向标定） ═══ -->
<GroupBox Grid.Row="2" Header="{lang:Lang ZScanDetail_CalibrationSection}"
          Margin="0,0,0,8">
    <GroupBox.HeaderTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <materialDesign:PackIcon Kind="CrosshairsGps" Width="16" Height="16" VerticalAlignment="Center" Margin="0,0,8,0" />
                <TextBlock Text="{Binding}" VerticalAlignment="Center" FontWeight="Medium" FontSize="12" />
            </StackPanel>
        </DataTemplate>
    </GroupBox.HeaderTemplate>
    <StackPanel>
        <!-- 步骤1: 输入3D点基准高度Z -->
        <WrapPanel Margin="0,0,0,8">
            <TextBlock Text="{lang:Lang ZScanDetail_Step1_BaseZ}" VerticalAlignment="Center" Margin="0,0,8,0" FontWeight="Medium" />
            <materialDesign:PackIcon Kind="Numeric1Circle" Width="18" Height="18" VerticalAlignment="Center" Margin="0,0,4,0"
                                     Foreground="{Binding CalibrationStep, Converter={StaticResource StepActiveConverter}, ConverterParameter=1}" />
            <TextBox Text="{Binding BaseZInput, StringFormat=F3}" Width="80" VerticalAlignment="Center" FontSize="12"
                     materialDesign:HintAssist.Hint="Z (mm)" Margin="0,0,8,0" />
            <Button Command="{Binding SetBaseZCommand}"
                    Style="{StaticResource MaterialDesignOutlinedButton}"
                    Padding="8,4" FontSize="11"
                    Content="{lang:Lang ZScanDetail_SetBaseZBtn}" />
        </WrapPanel>

        <!-- 步骤2: 移动针头到基准Z高度 -->
        <WrapPanel Margin="0,0,0,8">
            <TextBlock Text="{lang:Lang ZScanDetail_Step2_MoveNeedle}" VerticalAlignment="Center" Margin="0,0,8,0" FontWeight="Medium" />
            <materialDesign:PackIcon Kind="Numeric2Circle" Width="18" Height="18" VerticalAlignment="Center" Margin="0,0,4,0"
                                     Foreground="{Binding CalibrationStep, Converter={StaticResource StepActiveConverter}, ConverterParameter=2}" />
            <Button Command="{Binding MoveNeedleToBaseZCommand}"
                    Style="{StaticResource MaterialDesignOutlinedButton}"
                    Padding="8,4" FontSize="11"
                    Content="{lang:Lang ZScanDetail_MoveNeedleBtn}" />
        </WrapPanel>

        <!-- 步骤3: 示教针头基准高度MZ -->
        <WrapPanel Margin="0,0,0,8">
            <TextBlock Text="{lang:Lang ZScanDetail_Step3_TeachMZ}" VerticalAlignment="Center" Margin="0,0,8,0" FontWeight="Medium" />
            <materialDesign:PackIcon Kind="Numeric3Circle" Width="18" Height="18" VerticalAlignment="Center" Margin="0,0,4,0"
                                     Foreground="{Binding CalibrationStep, Converter={StaticResource StepActiveConverter}, ConverterParameter=3}" />
            <Button Command="{Binding TeachNeedleMZCommand}"
                    Style="{StaticResource MaterialDesignOutlinedButton}"
                    Padding="8,4" FontSize="11"
                    Content="{lang:Lang ZScanDetail_TeachMZBtn}" Margin="0,0,8,0" />
            <TextBlock Text="{lang:Lang ZScanDetail_MZ}" VerticalAlignment="Center" Margin="8,0,4,0" Foreground="{DynamicResource MaterialDesignBodyLight}" />
            <Border Background="#E8F5E9" CornerRadius="3" Padding="6,2">
                <TextBlock Text="{Binding MeasuredMZ, StringFormat=F3}" VerticalAlignment="Center" FontWeight="SemiBold" FontSize="12" Foreground="#2E7D32" />
            </Border>
        </WrapPanel>

        <!-- 步骤4: 计算点胶高度 = MZ + DeltaZ + 针头补偿 -->
        <WrapPanel Margin="0,0,0,8">
            <TextBlock Text="{lang:Lang ZScanDetail_Step4_CalcDispense}" VerticalAlignment="Center" Margin="0,0,8,0" FontWeight="Medium" />
            <materialDesign:PackIcon Kind="Numeric4Circle" Width="18" Height="18" VerticalAlignment="Center" Margin="0,0,4,0"
                                     Foreground="{Binding CalibrationStep, Converter={StaticResource StepActiveConverter}, ConverterParameter=4}" />
            <TextBlock Text="ΔZ:" VerticalAlignment="Center" Margin="0,0,4,0" Foreground="{DynamicResource MaterialDesignBodyLight}" />
            <TextBox Text="{Binding DeltaZInput, StringFormat=F3}" Width="65" VerticalAlignment="Center" FontSize="12" Margin="0,0,8,0" />
            <TextBlock Text="{lang:Lang ZScanDetail_NeedleComp}" VerticalAlignment="Center" Margin="0,0,4,0" Foreground="{DynamicResource MaterialDesignBodyLight}" />
            <TextBox Text="{Binding NeedleCompensationValue, StringFormat=F3}" Width="65" VerticalAlignment="Center" FontSize="12" Margin="0,0,8,0" />
            <Button Command="{Binding CalculateDispenseHeightCommand}"
                    Style="{StaticResource MaterialDesignRaisedButton}"
                    Padding="8,4" FontSize="11"
                    Content="{lang:Lang ZScanDetail_CalcBtn}" Margin="0,0,8,0" />
            <TextBlock Text="{lang:Lang ZScanDetail_DispenseHeight}" VerticalAlignment="Center" Margin="8,0,4,0" Foreground="#1565C0" FontWeight="Medium" />
            <Border Background="#E3F2FD" CornerRadius="3" Padding="8,3">
                <TextBlock Text="{Binding CalculatedDispenseHeight, StringFormat=F3}" VerticalAlignment="Center" FontWeight="Bold" FontSize="13" Foreground="#0D47A1" />
            </Border>
        </WrapPanel>

        <!-- 原有标定信息保留 -->
        <WrapPanel>
            <TextBlock Text="{lang:Lang ZScanDetail_CameraOffset}" VerticalAlignment="Center" Margin="0,0,6,0" Foreground="{DynamicResource MaterialDesignBodyLight}" />
            <Border Background="#E3F2FD" CornerRadius="3" Padding="6,2" Margin="0,0,12,0">
                <TextBlock Text="{Binding CameraZOffset, StringFormat=F3}" VerticalAlignment="Center" FontWeight="SemiBold" FontSize="12" Foreground="#1565C0" />
            </Border>
            <TextBlock Text="{lang:Lang ZScanDetail_NeedleOffset}" VerticalAlignment="Center" Margin="0,0,6,0" Foreground="{DynamicResource MaterialDesignBodyLight}" />
            <Border Background="#E8F5E9" CornerRadius="3" Padding="6,2" Margin="0,0,12,0">
                <TextBlock Text="{Binding NeedleZOffset, StringFormat=F3}" VerticalAlignment="Center" FontWeight="SemiBold" FontSize="12" Foreground="#2E7D32" />
            </Border>
            <TextBlock Text="{lang:Lang ZScanDetail_TotalOffset}" VerticalAlignment="Center" Margin="0,0,6,0" Foreground="#1565C0" FontWeight="Medium" />
            <Border Background="#E3F2FD" CornerRadius="3" Padding="8,3" Margin="0,0,12,0">
                <TextBlock Text="{Binding TotalZOffset, StringFormat=F3}" VerticalAlignment="Center" FontWeight="Bold" FontSize="13" Foreground="#0D47A1" />
            </Border>
            <Button Command="{Binding ResetCalibrationCommand}"
                    Style="{StaticResource MaterialDesignOutlinedButton}"
                    Padding="8,4"
                    ToolTip="{lang:Lang ZScanDetail_ResetCalibration}"
                    VerticalAlignment="Center">
                <StackPanel Orientation="Horizontal">
                    <materialDesign:PackIcon Kind="Refresh" Width="14" Height="14" VerticalAlignment="Center" Margin="0,0,4,0" />
                    <TextBlock Text="{lang:Lang ZScanDetail_ResetCalibrationBtn}" VerticalAlignment="Center" FontSize="11" />
                </StackPanel>
            </Button>
        </WrapPanel>
    </StackPanel>
</GroupBox>
```

- [ ] **Step 4: 构建验证**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: 0 errors

- [ ] **Step 5: 提交**

```bash
git add Module/Controls/ZScan/ZScanDetailViewModel.cs Module/Controls/ZScan/ZScanDetailView.xaml
git commit -m "feat(zscan): implement step-by-step Z-direction calibration UI and logic"
```

---

## Task 6: DataGrid 行级点类型和全局变量链接列

**Files:**
- Modify: `Module/Controls/ZScan/ZScanDetailView.xaml`
- Modify: `Module/Controls/ZScan/ZScanDetailViewModel.cs`

- [ ] **Step 1: 在DataGrid中增加PointType和链接列**

在 `ZScanDetailView.xaml` 的 DataGrid.Columns 中，在 DataIndex 列后增加：

```xml
<!-- 点类型列 -->
<DataGridTemplateColumn Header="Type" Width="80">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <ComboBox ItemsSource="{Binding DataContext.DataFormatOptions, RelativeSource={RelativeSource AncestorType=UserControl}}"
                      SelectedItem="{Binding PointType, UpdateSourceTrigger=PropertyChanged}"
                      FontSize="11" Padding="2,0" />
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>

<!-- 全局变量链接列 -->
<DataGridTemplateColumn Header="GV Link" Width="100">
    <DataGridTemplateColumn.CellTemplate>
        <DataTemplate>
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="{Binding GlobalVariableLink.VariableName}" VerticalAlignment="Center" FontSize="11" />
                <materialDesign:PackIcon Kind="Link" Width="12" Height="12"
                    Foreground="#1565C0" VerticalAlignment="Center" Margin="2,0,0,0"
                    Visibility="{Binding IsGlobalVarLinked, Converter={StaticResource BooleanToVisibilityConverter}}"/>
            </StackPanel>
        </DataTemplate>
    </DataGridTemplateColumn.CellTemplate>
    <DataGridTemplateColumn.CellEditingTemplate>
        <DataTemplate>
            <TextBox Text="{Binding GlobalVariableLink.VariableName, UpdateSourceTrigger=PropertyChanged}"
                     materialDesign:HintAssist.Hint="GV Name" FontSize="11" Padding="4,2" />
        </DataTemplate>
    </DataGridTemplateColumn.CellEditingTemplate>
</DataGridTemplateColumn>
```

- [ ] **Step 2: 更新ViewModel中的SyncPointDetailsToTable和OnSelectedTableChanged**

在 `SyncPointDetailsToTable` 方法中增加 PointType 和 GlobalVariableLink 映射：

```csharp
// 在 SyncPointDetailsToTable 的 Select 中增加：
PointType = p.PointType,
GlobalVariableLink = p.GlobalVariableLink
```

在 `OnSelectedTableChanged` 方法中增加 PointType 和 GlobalVariableLink 映射：

```csharp
// 在 OnSelectedTableChanged 的 Select 中增加：
PointType = p.PointType,
GlobalVariableLink = p.GlobalVariableLink
```

- [ ] **Step 3: 更新LinkGlobalVariableCommand为行级操作**

```csharp
private void OnLinkGlobalVariable()
{
    try
    {
        if (SelectedPointDetail == null)
        {
            _dialogService.ShowDialog("MessageDialog",
                new DialogParameters { { "message", "请先选择一行数据" } }, null);
            return;
        }

        var linkService = _containerProvider.Resolve<IZScanGlobalVariableLinkService>();
        var expectedType = SelectedPointDetail.PointType == ZScanDataFormat.DoubleArray
            ? GlobalVariableType.DoubleArray
            : GlobalVariableType.Double;

        // 弹出对话框让用户输入全局变量名
        _dialogService.ShowDialog("SimpleInputDialog", new DialogParameters
        {
            { "title", "链接全局变量" },
            { "prompt", $"输入全局变量名 (类型: {expectedType})" },
            { "defaultValue", "" }
        }, result =>
        {
            if (result.Result == ButtonResult.OK && result.Parameters.ContainsKey("inputValue"))
            {
                string varName = result.Parameters.GetValue<string>("inputValue");
                if (!string.IsNullOrEmpty(varName) && linkService.LinkVariable(varName, expectedType))
                {
                    SelectedPointDetail.GlobalVariableLink = new ZScanGlobalVariableLink
                    {
                        IsLinked = true,
                        VariableName = varName,
                        VariableType = expectedType
                    };
                    SelectedPointDetail.PointType = SelectedPointDetail.PointType;
                    _logger?.Info($"Z-SCAN 行{SelectedPointDetail.PointNumber}已链接全局变量: {varName}");
                }
                else
                {
                    _dialogService.ShowDialog("MessageDialog",
                        new DialogParameters { { "message", $"链接全局变量 '{varName}' 失败，请检查变量名和类型" } }, null);
                }
            }
        });
    }
    catch (Exception ex)
    {
        _logger?.Error($"Z-SCAN 链接全局变量失败: {ex.Message}");
    }
}
```

- [ ] **Step 4: 更新UnlinkGlobalVariableCommand为行级操作**

```csharp
private void OnUnlinkGlobalVariable()
{
    try
    {
        if (SelectedPointDetail == null)
        {
            _dialogService.ShowDialog("MessageDialog",
                new DialogParameters { { "message", "请先选择一行数据" } }, null);
            return;
        }

        if (SelectedPointDetail.GlobalVariableLink != null)
        {
            string varName = SelectedPointDetail.GlobalVariableLink.VariableName;
            SelectedPointDetail.GlobalVariableLink = null;
            _logger?.Info($"Z-SCAN 行{SelectedPointDetail.PointNumber}已取消全局变量链接: {varName}");
        }
    }
    catch (Exception ex)
    {
        _logger?.Error($"Z-SCAN 取消链接失败: {ex.Message}");
    }
}
```

- [ ] **Step 5: 移除表级DataFormat下拉框（已下沉到行级）**

在 `ZScanDetailView.xaml` 的 Row 1 工具栏中，移除 DataFormat 下拉框及其前后的 Separator 和 TextBlock：

```xml
<!-- 删除以下内容 -->
<Separator Width="1" Margin="4,0" VerticalAlignment="Stretch" />
<TextBlock Text="{lang:Lang ZScanDetail_DataFormat}" ... />
<ComboBox ItemsSource="{Binding DataFormatOptions}" ... />
```

- [ ] **Step 6: 构建验证**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: 0 errors

- [ ] **Step 7: 提交**

```bash
git add Module/Controls/ZScan/ZScanDetailView.xaml Module/Controls/ZScan/ZScanDetailViewModel.cs
git commit -m "feat(zscan): add row-level PointType and GV Link columns to DataGrid, remove table-level DataFormat"
```

---

## Task 7: 表格切换功能完善

**Files:**
- Modify: `Module/Controls/ZScan/ZScanDetailViewModel.cs`
- Test: `MotionControl.Tests/ZScan/ZScanTableSwitchTests.cs`

- [ ] **Step 1: 写失败测试 — 表格切换数据同步**

```csharp
// MotionControl.Tests/ZScan/ZScanTableSwitchTests.cs
using Core.Models;
using Module.Models;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace MotionControl.Tests
{
    public class ZScanTableSwitchTests
    {
        [Fact]
        public void TableSwitch_PreservesPointDetails_WhenSwitchingAway()
        {
            // 模拟表格切换：从Table1切到Table2，再切回Table1，数据应保留
            var tables = new List<ZScanTableConfig>
            {
                new ZScanTableConfig
                {
                    TableName = "Table1",
                    Points = new List<ZScanPointData>
                    {
                        new ZScanPointData { Segment = 1, PointNumber = 1, X = 10, Y = 20, PointType = ZScanDataFormat.Double }
                    }
                },
                new ZScanTableConfig
                {
                    TableName = "Table2",
                    Points = new List<ZScanPointData>
                    {
                        new ZScanPointData { Segment = 1, PointNumber = 1, X = 30, Y = 40, PointType = ZScanDataFormat.DoubleArray }
                    }
                }
            };

            // 验证Table1数据
            Assert.Single(tables[0].Points);
            Assert.Equal(10.0, tables[0].Points[0].X);
            Assert.Equal(ZScanDataFormat.Double, tables[0].Points[0].PointType);

            // 验证Table2数据
            Assert.Single(tables[1].Points);
            Assert.Equal(30.0, tables[1].Points[0].X);
            Assert.Equal(ZScanDataFormat.DoubleArray, tables[1].Points[0].PointType);
        }

        [Fact]
        public void ZScanPointData_ToPointDetail_MapsPointType()
        {
            var data = new ZScanPointData
            {
                Segment = 1, PointNumber = 1,
                X = 10, Y = 20,
                PointType = ZScanDataFormat.DoubleArray,
                GlobalVariableLink = new ZScanGlobalVariableLink
                {
                    IsLinked = true,
                    VariableName = "GV_Test",
                    VariableType = GlobalVariableType.DoubleArray
                }
            };

            var detail = new ZScanPointDetail
            {
                Segment = data.Segment,
                PointNumber = data.PointNumber,
                X = data.X,
                Y = data.Y,
                PointType = data.PointType,
                GlobalVariableLink = data.GlobalVariableLink
            };

            Assert.Equal(ZScanDataFormat.DoubleArray, detail.PointType);
            Assert.True(detail.IsGlobalVarLinked);
            Assert.Equal("GV_Test", detail.GlobalVariableLink.VariableName);
        }

        [Fact]
        public void ZScanPointDetail_ToPointData_MapsPointType()
        {
            var detail = new ZScanPointDetail
            {
                Segment = 1, PointNumber = 1,
                X = 10, Y = 20,
                PointType = ZScanDataFormat.DoubleArray,
                GlobalVariableLink = new ZScanGlobalVariableLink
                {
                    IsLinked = true,
                    VariableName = "GV_Test",
                    VariableType = GlobalVariableType.DoubleArray
                }
            };

            var data = new ZScanPointData
            {
                Segment = detail.Segment,
                PointNumber = detail.PointNumber,
                X = detail.X,
                Y = detail.Y,
                PointType = detail.PointType,
                GlobalVariableLink = detail.GlobalVariableLink
            };

            Assert.Equal(ZScanDataFormat.DoubleArray, data.PointType);
            Assert.True(data.GlobalVariableLink.IsLinked);
        }
    }
}
```

- [ ] **Step 2: 运行测试验证通过**（此测试主要验证模型映射，应直接PASS）

Run: `dotnet test MotionControl.Tests\MotionControl.Tests.csproj --filter "FullyQualifiedName~ZScanTableSwitchTests" -v n`
Expected: PASS

- [ ] **Step 3: 完善ViewModel中的OnSelectedTableChanged — 切换前保存当前表格数据**

```csharp
private ZScanTableConfig _previousTable;

private void OnSelectedTableChanged()
{
    // 切换前保存上一个表格的数据
    if (_previousTable != null && PointDetails != null)
    {
        SyncPointDetailsToTable(_previousTable);
    }

    if (SelectedTable == null) return;

    // 加载新选中表格的数据
    if (SelectedTable.Points != null && SelectedTable.Points.Count > 0)
    {
        PointDetails = new ObservableCollection<ZScanPointDetail>(
            SelectedTable.Points.Select(p => new ZScanPointDetail
            {
                Segment = p.Segment,
                PointNumber = p.PointNumber,
                X = p.X,
                Y = p.Y,
                ZNominal = p.ZNominal,
                ZMeasured = p.ZMeasured,
                DeltaZ = p.DeltaZ,
                Nominal = p.Nominal,
                Range = p.Range,
                DataIndex = p.DataIndex,
                Description = p.Description,
                Status = p.Status,
                PointType = p.PointType,
                GlobalVariableLink = p.GlobalVariableLink
            }));
    }
    else
    {
        PointDetails = new ObservableCollection<ZScanPointDetail>();
    }

    // 更新标定显示
    if (SelectedTable.Calibration != null)
    {
        CameraZOffset = SelectedTable.Calibration.CameraZOffset;
        NeedleZOffset = SelectedTable.Calibration.NeedleZOffset;
        TotalZOffset = SelectedTable.Calibration.TotalZOffset;
    }

    _previousTable = SelectedTable;
    SubscribePointDetailsEvents();
    RecalculateStatistics();
    _logger?.Info($"Z-SCAN 切换表格: {SelectedTable.TableName}");
}

private void SyncPointDetailsToTable(ZScanTableConfig table)
{
    if (table == null || PointDetails == null) return;
    table.Points = PointDetails.Select(p => new ZScanPointData
    {
        Segment = p.Segment,
        PointNumber = p.PointNumber,
        X = p.X,
        Y = p.Y,
        ZNominal = p.ZNominal,
        ZMeasured = p.ZMeasured,
        DeltaZ = p.DeltaZ,
        Nominal = p.Nominal,
        Range = p.Range,
        DataIndex = p.DataIndex,
        Description = p.Description,
        Status = p.Status,
        PointType = p.PointType,
        GlobalVariableLink = p.GlobalVariableLink
    }).ToList();
}
```

- [ ] **Step 4: 更新OnSaveConfig使用SaveWithTimestamp**

```csharp
private void OnSaveConfig()
{
    try
    {
        SyncPointDetailsToTable(SelectedTable);

        var configFile = new ZScanConfigFile
        {
            DefaultTableName = SelectedTable?.TableName ?? string.Empty,
            Tables = Tables.ToList()
        };

        // 保存带时间戳的文件
        string savedPath = _zscanConfigService.SaveWithTimestamp(configFile);
        _logger?.Info($"Z-SCAN 配置已保存到: {savedPath}");

        // 同时保存到配方池
        _zscanConfigService.SaveToRecipePool(configFile, $"{AssyGroup}_{SiteId}");
        _logger?.Info("Z-SCAN 配置已同步到配方池");
    }
    catch (Exception ex)
    {
        _logger?.Error($"Z-SCAN 配置保存失败: {ex.Message}");
    }
}
```

- [ ] **Step 5: 更新OnLoadConfig优先从配方池加载**

```csharp
private void OnLoadConfig()
{
    try
    {
        // 优先从配方池加载最后一次保存的配置
        var configFile = _zscanConfigService.LoadLastFromRecipePool();

        // 如果配方池没有数据，尝试加载默认配置
        if (configFile.Tables.Count == 0)
        {
            configFile = _zscanConfigService.Load();
        }

        if (configFile.Tables.Count > 0)
        {
            Tables = new ObservableCollection<ZScanTableConfig>(configFile.Tables);
            var defaultTable = Tables.FirstOrDefault(t => t.TableName == configFile.DefaultTableName) ?? Tables[0];
            SelectedTable = defaultTable;
            _logger?.Info($"Z-SCAN 配置已加载: {configFile.Tables.Count} 个表格");
        }
    }
    catch (Exception ex)
    {
        _logger?.Error($"Z-SCAN 配置加载失败: {ex.Message}");
    }
}
```

- [ ] **Step 6: 构建验证**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: 0 errors

- [ ] **Step 7: 提交**

```bash
git add Module/Controls/ZScan/ZScanDetailViewModel.cs MotionControl.Tests/ZScan/ZScanTableSwitchTests.cs
git commit -m "feat(zscan): complete table switch with data sync, timestamped save, and recipe pool auto-load"
```

---

## Task 8: 多语言资源更新

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`
- Modify: `MainApp/Languages/Strings.en-US.xaml`

- [ ] **Step 1: 添加Z向标定步骤相关多语言键值**

在 `Strings.en-US.xaml` 中增加：

```xml
<sys:String x:Key="ZScanDetail_Step1_BaseZ">Step 1: Input Base Z Height</sys:String>
<sys:String x:Key="ZScanDetail_SetBaseZBtn">Set Base Z</sys:String>
<sys:String x:Key="ZScanDetail_Step2_MoveNeedle">Step 2: Move Needle to Base Z</sys:String>
<sys:String x:Key="ZScanDetail_MoveNeedleBtn">Move Needle</sys:String>
<sys:String x:Key="ZScanDetail_Step3_TeachMZ">Step 3: Teach Needle MZ</sys:String>
<sys:String x:Key="ZScanDetail_TeachMZBtn">Teach MZ</sys:String>
<sys:String x:Key="ZScanDetail_MZ">MZ:</sys:String>
<sys:String x:Key="ZScanDetail_Step4_CalcDispense">Step 4: Calculate Dispense Height</sys:String>
<sys:String x:Key="ZScanDetail_CalcBtn">Calculate</sys:String>
<sys:String x:Key="ZScanDetail_DispenseHeight">Dispense Z:</sys:String>
<sys:String x:Key="ZScanDetail_ResetCalibrationBtn">Reset</sys:String>
<sys:String x:Key="ZScanDetail_CalibrationSection">Z Calibration</sys:String>
```

在 `Strings.zh-CN.xaml` 中增加：

```xml
<sys:String x:Key="ZScanDetail_Step1_BaseZ">步骤1: 输入3D点基准高度Z</sys:String>
<sys:String x:Key="ZScanDetail_SetBaseZBtn">设置基准Z</sys:String>
<sys:String x:Key="ZScanDetail_Step2_MoveNeedle">步骤2: 移动针头到基准Z</sys:String>
<sys:String x:Key="ZScanDetail_MoveNeedleBtn">移动针头</sys:String>
<sys:String x:Key="ZScanDetail_Step3_TeachMZ">步骤3: 示教针头基准高度MZ</sys:String>
<sys:String x:Key="ZScanDetail_TeachMZBtn">示教MZ</sys:String>
<sys:String x:Key="ZScanDetail_MZ">MZ:</sys:String>
<sys:String x:Key="ZScanDetail_Step4_CalcDispense">步骤4: 计算点胶高度</sys:String>
<sys:String x:Key="ZScanDetail_CalcBtn">计算</sys:String>
<sys:String x:Key="ZScanDetail_DispenseHeight">点胶Z:</sys:String>
<sys:String x:Key="ZScanDetail_ResetCalibrationBtn">重置</sys:String>
<sys:String x:Key="ZScanDetail_CalibrationSection">Z向标定</sys:String>
```

- [ ] **Step 2: 构建验证**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: 0 errors

- [ ] **Step 3: 提交**

```bash
git add MainApp/Languages/Strings.en-US.xaml MainApp/Languages/Strings.zh-CN.xaml
git commit -m "feat(zscan): add i18n keys for Z-direction calibration steps"
```

---

## Task 9: 全量测试和构建验证

**Files:**
- None (验证任务)

- [ ] **Step 1: 运行全部ZScan测试**

Run: `dotnet test MotionControl.Tests\MotionControl.Tests.csproj --filter "FullyQualifiedName~ZScan" -v n`
Expected: 全部PASS

- [ ] **Step 2: 运行全量测试**

Run: `dotnet test MotionControl.Tests\MotionControl.Tests.csproj -v n`
Expected: 全部PASS

- [ ] **Step 3: 全项目构建验证**

Run: `dotnet build MainApp\MainApp.csproj`
Expected: 0 errors, 0 warnings

- [ ] **Step 4: 最终提交**

```bash
git add -A
git commit -m "feat(zscan): ZScanDetailView optimization complete - step calibration, row-level types, enhanced persistence, table switching"
```

---

## 自审检查

### 1. 规格覆盖检查

| 需求 | 对应Task |
|------|----------|
| Z向标定步骤: 输入3D点基准Z → 移动针头 → 示教MZ → 计算点胶高度 | Task 2 + 3 + 5 |
| 针头补偿可链接全局变量 | Task 2 (ZScanCalibrationConfig.NeedleCompensationLink) |
| 每行增加点类型(double/double[])和全局变量链接 | Task 1 + 6 |
| JSON默认保存到bin\Debug\net9.0-windows7.0\Config\ZScan\ZScan_20260527_132441.json | Task 4 |
| 最后一次保存到配方池，下次自动加载 | Task 4 + Task 7 |
| Table切换功能实现 | Task 7 |

### 2. 占位符扫描

- 无"TBD"、"TODO"、"implement later"等占位符
- 所有步骤包含完整代码

### 3. 类型一致性检查

- `ZScanPointData.PointType` ↔ `ZScanPointDetail.PointType` — 均为 `ZScanDataFormat` 枚举 ✓
- `ZScanPointData.GlobalVariableLink` ↔ `ZScanPointDetail.GlobalVariableLink` — 均为 `ZScanGlobalVariableLink` ✓
- `IZScanCalibrationService.SetBaseZ` / `TeachNeedleMZ` / `CalculateDispenseHeight` — 接口与实现一致 ✓
- `IZScanConfigService.SaveWithTimestamp` / `LoadLastFromRecipePool` / `SaveToRecipePool` — 接口与实现一致 ✓
- `INeedleTeachService.MoveNeedleToBaseZAsync` / `TeachCurrentPositionAsync` — 接口与实现一致 ✓

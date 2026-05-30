# 轴参数读取/写入重构 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 重构轴参数管理，实现从卡读取参数并写入文件、写入到卡同时保存文件、合并所有轴参数到单一JSON文件、移除独立的加载/保存参数按钮、将"应用设置"改为"应用插补系"。

**Architecture:** 采用分层架构，从底层IMotionCard接口添加读取方法，到AxisParameterService实现业务逻辑（从卡读取+写文件、写卡+写文件），再到ViewModel和View层简化操作流程。所有轴参数合并为一个JSON文件，使用Core的JsonParameterStorage实现持久化。

**Tech Stack:** WPF + Prism + MaterialDesignInXaml + LTDMC SDK + Newtonsoft.Json

---

## 文件结构

| 操作 | 文件路径 | 职责 |
|------|---------|------|
| 修改 | `MotionControl/Interfaces/IMotionCard.cs` | 添加从卡读取参数的接口方法 |
| 修改 | `MotionControl/Card/MotionCardBase.cs` | 添加从卡读取参数的抽象方法 |
| 修改 | `MotionControl/Card/Leisai/LeisaiMotionCard.cs` | 实现从卡读取参数的具体逻辑 |
| 修改 | `MotionControl/Services/IAxisParameterService.cs` | 重构接口：移除旧方法，添加新方法 |
| 修改 | `MotionControl/Services/AxisParameterService.cs` | 实现从卡读取+写文件、写卡+写文件、合并JSON |
| 修改 | `MotionControl/ViewModels/AxisSettingViewModel.cs` | 移除加载/保存命令，重构从卡读取/写入到卡逻辑 |
| 修改 | `MotionControl/Views/AxisSettingView.xaml` | 移除加载/保存按钮，"应用设置"改为"应用插补系" |
| 修改 | `MainApp/Languages/Strings.zh-CN.xaml` | 更新/添加中文语言资源 |
| 修改 | `MainApp/Languages/Strings.en-US.xaml` | 更新/添加英文语言资源 |

---

### Task 1: IMotionCard 接口添加从卡读取参数的方法

**Files:**
- Modify: `MotionControl/Interfaces/IMotionCard.cs:48-64`

- [ ] **Step 1: 在 IMotionCard 接口的 `#region 轴参数设置` 区域内添加读取方法**

在 `#region 轴参数设置（用于 AxisSetting 参数配置）` 中，紧跟现有 Set 方法之后，添加以下 Get 方法：

```csharp
#region 轴参数读取（用于 AxisSetting 从卡读取参数）

int GetPulseEquivalent(int axisId, ref double pulsePerUnit);

int GetEmergencyStopMode(int axisId, ref bool enabled, ref int logicLevel);

int GetAxisIOMap(int axisId, int ioType, ref int mapIoType, ref int mapIoIndex, ref double filterTime);

int GetHomeProfile(int axisId, ref int mode, ref double lowSpeed, ref double highSpeed, ref double accTime, ref double decTime, ref double offset);

int GetProfileUnit(int axisId, ref double startVel, ref double maxVel, ref double accTime, ref double decTime, ref double stopVel);

int GetSProfile(int axisId, int reserved, ref double sPara);

int GetDecStopTime(int axisId, ref double decStopTime);

int GetVectorProfileUnit(int coordId, ref double startVel, ref double maxVel, ref double accTime, ref double decTime, ref double endVel);

int GetVectorSProfile(int coordId, int reserved, ref double sPara);

#endregion
```

---

### Task 2: MotionCardBase 添加对应抽象方法

**Files:**
- Modify: `MotionControl/Card/MotionCardBase.cs:48-56`

- [ ] **Step 1: 在 MotionCardBase 类中添加与 IMotionCard 新接口对应的抽象方法**

在 `SetDecStopTime` 抽象方法之后添加：

```csharp
// 轴参数读取（用于 AxisSetting 从卡读取参数）
public abstract int GetPulseEquivalent(int axisId, ref double pulsePerUnit);
public abstract int GetEmergencyStopMode(int axisId, ref bool enabled, ref int logicLevel);
public abstract int GetAxisIOMap(int axisId, int ioType, ref int mapIoType, ref int mapIoIndex, ref double filterTime);
public abstract int GetHomeProfile(int axisId, ref int mode, ref double lowSpeed, ref double highSpeed, ref double accTime, ref double decTime, ref double offset);
public abstract int GetProfileUnit(int axisId, ref double startVel, ref double maxVel, ref double accTime, ref double decTime, ref double stopVel);
public abstract int GetSProfile(int axisId, int reserved, ref double sPara);
public abstract int GetDecStopTime(int axisId, ref double decStopTime);
public abstract int GetVectorProfileUnit(int coordId, ref double startVel, ref double maxVel, ref double accTime, ref double decTime, ref double endVel);
public abstract int GetVectorSProfile(int coordId, int reserved, ref double sPara);
```

---

### Task 3: LeisaiMotionCard 实现从卡读取参数

**Files:**
- Modify: `MotionControl/Card/Leisai/LeisaiMotionCard.cs:530-583`

- [ ] **Step 1: 在 LeisaiMotionCard 的 `#region 轴参数设置` 之后添加 `#region 轴参数读取`**

在 `#endregion` (轴参数设置) 之后，类结束大括号之前，添加以下实现：

```csharp
#region 轴参数读取

/// <summary>
/// 读取脉冲当量
/// </summary>
public override int GetPulseEquivalent(int axisId, ref double pulsePerUnit)
{
    lock (_lockObj)
    {
        try { return LTDMC.dmc_get_equiv(_cardId, (ushort)axisId, ref pulsePerUnit); }
        catch { return -1; }
    }
}

/// <summary>
/// 读取急停模式
/// </summary>
public override int GetEmergencyStopMode(int axisId, ref bool enabled, ref int logicLevel)
{
    lock (_lockObj)
    {
        try
        {
            ushort enbale = 0, emgLogic = 0;
            short result = LTDMC.dmc_get_emg_mode(_cardId, (ushort)axisId, ref enbale, ref emgLogic);
            enabled = enbale != 0;
            logicLevel = emgLogic;
            return result;
        }
        catch { return -1; }
    }
}

/// <summary>
/// 读取轴IO映射
/// </summary>
public override int GetAxisIOMap(int axisId, int ioType, ref int mapIoType, ref int mapIoIndex, ref double filterTime)
{
    lock (_lockObj)
    {
        try
        {
            ushort mType = 0, mIndex = 0;
            double filter = 0;
            short result = LTDMC.dmc_get_axis_io_map(_cardId, (ushort)axisId, (ushort)ioType, ref mType, ref mIndex, ref filter);
            mapIoType = mType;
            mapIoIndex = mIndex;
            filterTime = filter;
            return result;
        }
        catch { return -1; }
    }
}

/// <summary>
/// 读取回零参数
/// </summary>
public override int GetHomeProfile(int axisId, ref int mode, ref double lowSpeed, ref double highSpeed, ref double accTime, ref double decTime, ref double offset)
{
    lock (_lockObj)
    {
        try
        {
            ushort homeMode = 0;
            double lowVel = 0, highVel = 0, tAcc = 0, tDec = 0, offsetPos = 0;
            short result = LTDMC.nmc_get_home_profile(_cardId, (ushort)axisId, ref homeMode, ref lowVel, ref highVel, ref tAcc, ref tDec, ref offsetPos);
            mode = homeMode;
            lowSpeed = lowVel;
            highSpeed = highVel;
            accTime = tAcc;
            decTime = tDec;
            offset = offsetPos;
            return result;
        }
        catch { return -1; }
    }
}

/// <summary>
/// 读取运动参数（速度曲线）
/// </summary>
public override int GetProfileUnit(int axisId, ref double startVel, ref double maxVel, ref double accTime, ref double decTime, ref double stopVel)
{
    lock (_lockObj)
    {
        try { return LTDMC.dmc_get_profile_unit(_cardId, (ushort)axisId, ref startVel, ref maxVel, ref accTime, ref decTime, ref stopVel); }
        catch { return -1; }
    }
}

/// <summary>
/// 读取S曲线参数
/// </summary>
public override int GetSProfile(int axisId, int reserved, ref double sPara)
{
    lock (_lockObj)
    {
        try { return LTDMC.dmc_get_s_profile(_cardId, (ushort)axisId, (ushort)reserved, ref sPara); }
        catch { return -1; }
    }
}

/// <summary>
/// 读取减速停止时间
/// </summary>
public override int GetDecStopTime(int axisId, ref double decStopTime)
{
    lock (_lockObj)
    {
        try { return LTDMC.dmc_get_dec_stop_time(_cardId, (ushort)axisId, ref decStopTime); }
        catch { return -1; }
    }
}

/// <summary>
/// 读取插补系运动参数
/// </summary>
public override int GetVectorProfileUnit(int coordId, ref double startVel, ref double maxVel, ref double accTime, ref double decTime, ref double endVel)
{
    lock (_lockObj)
    {
        try { return LTDMC.dmc_get_vector_profile_unit(_cardId, (ushort)coordId, ref startVel, ref maxVel, ref accTime, ref decTime, ref endVel); }
        catch { return -1; }
    }
}

/// <summary>
/// 读取插补系S曲线参数
/// </summary>
public override int GetVectorSProfile(int coordId, int reserved, ref double sPara)
{
    lock (_lockObj)
    {
        try { return LTDMC.dmc_conti_get_s_profile(_cardId, (ushort)coordId, (ushort)reserved, ref sPara); }
        catch { return -1; }
    }
}

#endregion
```

---

### Task 4: IAxisParameterService 接口重构

**Files:**
- Modify: `MotionControl/Services/IAxisParameterService.cs`

- [ ] **Step 1: 重构接口，移除旧方法，添加新方法**

将整个接口替换为：

```csharp
using MotionControl.Models;
using Core.Abstraction;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MotionControl.Services
{
    public interface IAxisParameterService
    {
        /// <summary>
        /// 从hwcfg.xml加载所有轴信息
        /// </summary>
        IReadOnlyList<AxisInfo> LoadAllAxes();

        /// <summary>
        /// 从控制卡读取单个轴参数，同时写入配置文件
        /// </summary>
        Task ReadFromCardAsync(AxisInfo axis);

        /// <summary>
        /// 将单个轴参数写入控制卡，同时写入配置文件
        /// </summary>
        Task WriteToCardAsync(AxisInfo axis);

        /// <summary>
        /// 将所有轴参数写入控制卡，同时写入配置文件
        /// </summary>
        Task WriteAllToCardAsync(IProgressReporter progressReporter = null);

        /// <summary>
        /// 从控制卡读取所有轴参数，同时写入配置文件
        /// </summary>
        Task ReadAllFromCardAsync(IProgressReporter progressReporter = null);

        /// <summary>
        /// 保存所有轴参数到单一JSON文件
        /// </summary>
        void SaveAllAxisParameters(IEnumerable<AxisInfo> axes);

        /// <summary>
        /// 从单一JSON文件加载所有轴参数
        /// </summary>
        Dictionary<string, AxisParams> LoadAllAxisParameters();

        /// <summary>
        /// 加载插补系统列表
        /// </summary>
        IEnumerable<InterpolationSystem> LoadInterpolationSystems();

        /// <summary>
        /// 从控制卡读取插补系参数，同时写入配置文件
        /// </summary>
        void ReadInterpolationFromCard(InterpolationSystem system);

        /// <summary>
        /// 将插补系参数写入控制卡，同时写入配置文件
        /// </summary>
        void WriteInterpolationToCard(InterpolationSystem system);

        /// <summary>
        /// 保存所有插补系参数到单一JSON文件
        /// </summary>
        void SaveAllInterpolationSystems(IEnumerable<InterpolationSystem> systems);

        /// <summary>
        /// 从单一JSON文件加载所有插补系参数
        /// </summary>
        List<InterpolationSystemConfig> LoadAllInterpolationSystems();

        /// <summary>
        /// 获取轴最大速度
        /// </summary>
        double GetAxisSpeed(int cardId, int axisId);

        /// <summary>
        /// 获取插补速度
        /// </summary>
        double GetInterpolationSpeeds(int cardId, int coordId);
    }
}
```

- [ ] **Step 2: 在 Models 中添加 InterpolationSystemConfig 数据类**

在 `MotionControl/Models/AxisConfiguration.cs` 文件末尾（`InterpolationParams` 类之后）添加：

```csharp
/// <summary>
/// 插补系配置持久化模型（用于JSON序列化）
/// </summary>
public class InterpolationSystemConfig
{
    public int ActCardId { get; set; }
    public int CoordId { get; set; }
    public List<string> Axes { get; set; } = new List<string>();
    public InterpolationParams Params { get; set; } = new InterpolationParams();
}

/// <summary>
/// 所有轴参数持久化模型（用于JSON序列化，合并为一个文件）
/// </summary>
public class AllAxisParametersData
{
    /// <summary>
    /// Key格式: "{cardId}-{axisId}"
    /// </summary>
    public Dictionary<string, AxisParams> Axes { get; set; } = new Dictionary<string, AxisParams>();
}
```

---

### Task 5: AxisParameterService 实现核心业务逻辑

**Files:**
- Modify: `MotionControl/Services/AxisParameterService.cs`

- [ ] **Step 1: 重写 AxisParameterService，实现从卡读取+写文件、写卡+写文件、合并JSON**

将整个 `AxisParameterService.cs` 替换为：

```csharp
using MotionControl.Models;
using Core.Utilities;
using Core.Abstraction;
using Core.Services;
using MotionControl.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MotionControl.Services
{
    public class AxisParameterService : IAxisParameterService
    {
        private readonly IMotionCardFactory _factory;
        private readonly ILoggerService _logger;
        private readonly JsonParameterStorage _storage;
        private const string HW_CONFIG_PATH = "Config/HWConfig/hwcfg.xml";
        private const string AXIS_PARAMS_FILE = "AllAxisParameters";
        private const string INTERP_SYSTEMS_FILE = "AllInterpolationSystems";
        private const string PARAMS_DIR = "Config/Parameters";

        public AxisParameterService(IMotionCardFactory factory, ILoggerService logger)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _storage = new JsonParameterStorage();
        }

        public IReadOnlyList<AxisInfo> LoadAllAxes()
        {
            var axes = new List<AxisInfo>();
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HW_CONFIG_PATH);
                if (!File.Exists(configPath))
                {
                    _logger.Warn($"hwcfg.xml not found at {configPath}");
                    return axes;
                }

                XDocument doc = XDocument.Load(configPath);
                var axisElements = doc.Descendants("Axis");
                var savedParams = LoadAllAxisParameters();

                foreach (var axisElem in axisElements)
                {
                    int setAxisId = int.Parse(axisElem.Attribute("setAxisId")?.Value ?? "0");
                    string name = axisElem.Attribute("name")?.Value ?? $"Axis_{setAxisId}";

                    int cardId = 0;
                    int axisId = setAxisId;

                    var cardAttr = axisElem.Attribute("cardId");
                    if (cardAttr != null)
                        cardId = int.Parse(cardAttr.Value);

                    var axisIdAttr = axisElem.Attribute("axisId");
                    if (axisIdAttr != null)
                        axisId = int.Parse(axisIdAttr.Value);

                    var axisInfo = new AxisInfo(cardId, axisId, name);

                    string key = $"{cardId}-{axisId}";
                    if (savedParams.ContainsKey(key))
                        axisInfo.Params = savedParams[key];

                    axes.Add(axisInfo);
                }

                _logger.Info($"Loaded {axes.Count} axes from hwcfg.xml");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load axes from hwcfg.xml");
            }

            return axes;
        }

        /// <summary>
        /// 从控制卡读取单个轴参数，同时写入配置文件
        /// </summary>
        public async Task ReadFromCardAsync(AxisInfo axis)
        {
            if (axis == null) throw new ArgumentNullException(nameof(axis));

            await Task.Run(() =>
            {
                var card = _factory.GetCard(axis.CardId);
                if (card == null)
                    throw new InvalidOperationException($"Motion card {axis.CardId} not available");

                var params_data = axis.Params ?? new AxisParams();

                double pulsePerUnit = 0;
                int result = card.GetPulseEquivalent(axis.AxisId, ref pulsePerUnit);
                if (result == 0) params_data.PulsePerUnit = pulsePerUnit;
                else _logger.Warn($"GetPulseEquivalent failed for axis {axis.AxisId}, code: {result}");

                bool emgEnabled = false;
                int emgLogic = 0;
                result = card.GetEmergencyStopMode(axis.AxisId, ref emgEnabled, ref emgLogic);
                if (result == 0)
                {
                    params_data.EmergencyStop.Enabled = emgEnabled;
                    params_data.EmergencyStop.LogicLevel = emgLogic == 1 ? LogicLevel.High : LogicLevel.Low;
                }
                else _logger.Warn($"GetEmergencyStopMode failed for axis {axis.AxisId}, code: {result}");

                if (params_data.EmergencyStop.MappedIO != null)
                {
                    var mappedIO = params_data.EmergencyStop.MappedIO;
                    int mapIoType = 0, mapIoIndex = 0;
                    double filterTime = 0;
                    result = card.GetAxisIOMap(axis.AxisId, mappedIO.IoType, ref mapIoType, ref mapIoIndex, ref filterTime);
                    if (result == 0)
                    {
                        mappedIO.MapIoType = (short)mapIoType;
                        mappedIO.MapIoIndex = (short)mapIoIndex;
                        mappedIO.FilterTime = filterTime;
                    }
                    else _logger.Warn($"GetAxisIOMap failed for axis {axis.AxisId}, code: {result}");
                }

                int homeMode = 0;
                double lowSpeed = 0, highSpeed = 0, homeAcc = 0, homeDec = 0, homeOffset = 0;
                result = card.GetHomeProfile(axis.AxisId, ref homeMode, ref lowSpeed, ref highSpeed, ref homeAcc, ref homeDec, ref homeOffset);
                if (result == 0)
                {
                    params_data.Homing.Mode = homeMode;
                    params_data.Homing.LowSpeed = lowSpeed;
                    params_data.Homing.HighSpeed = highSpeed;
                    params_data.Homing.AccelerationTime = homeAcc;
                    params_data.Homing.DecelerationTime = homeDec;
                    params_data.Homing.Offset = homeOffset;
                }
                else _logger.Warn($"GetHomeProfile failed for axis {axis.AxisId}, code: {result}");

                double startVel = 0, maxVel = 0, accTime = 0, decTime = 0, stopVel = 0;
                result = card.GetProfileUnit(axis.AxisId, ref startVel, ref maxVel, ref accTime, ref decTime, ref stopVel);
                if (result == 0)
                {
                    params_data.Motion.StartSpeed = startVel;
                    params_data.Motion.MaxSpeed = maxVel;
                    params_data.Motion.AccelerationTime = accTime;
                    params_data.Motion.DecelerationTime = decTime;
                    params_data.Motion.StopSpeed = stopVel;
                }
                else _logger.Warn($"GetProfileUnit failed for axis {axis.AxisId}, code: {result}");

                double sPara = 0;
                result = card.GetSProfile(axis.AxisId, 0, ref sPara);
                if (result == 0) params_data.Motion.SProfileTime = sPara;
                else _logger.Warn($"GetSProfile failed for axis {axis.AxisId}, code: {result}");

                double decStopTime = 0;
                result = card.GetDecStopTime(axis.AxisId, ref decStopTime);
                if (result == 0) params_data.Motion.DecStopTime = decStopTime;
                else _logger.Warn($"GetDecStopTime failed for axis {axis.AxisId}, code: {result}");

                axis.Params = params_data;

                _logger.Info($"Read parameters from card for axis {axis.Name} (Card:{axis.CardId}, Axis:{axis.AxisId})");
            });
        }

        /// <summary>
        /// 将单个轴参数写入控制卡，同时写入配置文件
        /// </summary>
        public async Task WriteToCardAsync(AxisInfo axis)
        {
            if (axis == null) throw new ArgumentNullException(nameof(axis));
            if (axis.Params == null) throw new InvalidOperationException("Axis parameters not loaded");

            await Task.Run(() =>
            {
                var card = _factory.GetCard(axis.CardId);
                if (card == null)
                    throw new InvalidOperationException($"Motion card {axis.CardId} not available");

                var params_data = axis.Params;

                int result = card.SetPulseEquivalent(axis.AxisId, params_data.PulsePerUnit);
                if (result != 0) _logger.Warn($"SetPulseEquivalent failed for axis {axis.AxisId}, code: {result}");

                result = card.SetEmergencyStopMode(
                    axis.AxisId,
                    params_data.EmergencyStop.Enabled,
                    params_data.EmergencyStop.LogicLevel == LogicLevel.High ? 1 : 0);
                if (result != 0) _logger.Warn($"SetEmergencyStopMode failed for axis {axis.AxisId}, code: {result}");

                if (params_data.EmergencyStop.MappedIO != null)
                {
                    var mappedIO = params_data.EmergencyStop.MappedIO;
                    result = card.SetAxisIOMap(
                        axis.AxisId,
                        mappedIO.IoType,
                        mappedIO.MapIoType,
                        mappedIO.MapIoIndex,
                        mappedIO.FilterTime);
                    if (result != 0) _logger.Warn($"SetAxisIOMap failed for axis {axis.AxisId}, code: {result}");
                }

                var homing = params_data.Homing;
                result = card.SetHomeProfile(
                    axis.AxisId,
                    homing.Mode,
                    homing.LowSpeed,
                    homing.HighSpeed,
                    homing.AccelerationTime,
                    homing.DecelerationTime,
                    homing.Offset);
                if (result != 0) _logger.Warn($"SetHomeProfile failed for axis {axis.AxisId}, code: {result}");

                var motion = params_data.Motion;
                result = card.SetProfileUnit(
                    axis.AxisId,
                    motion.StartSpeed,
                    motion.MaxSpeed,
                    motion.AccelerationTime,
                    motion.DecelerationTime,
                    motion.StopSpeed);
                if (result != 0) _logger.Warn($"SetProfileUnit failed for axis {axis.AxisId}, code: {result}");

                result = card.SetSProfile(axis.AxisId, 0, motion.SProfileTime);
                if (result != 0) _logger.Warn($"SetSProfile failed for axis {axis.AxisId}, code: {result}");

                result = card.SetDecStopTime(axis.AxisId, motion.DecStopTime);
                if (result != 0) _logger.Warn($"SetDecStopTime failed for axis {axis.AxisId}, code: {result}");

                _logger.Info($"Wrote parameters to card for axis {axis.Name} (Card:{axis.CardId}, Axis:{axis.AxisId})");
            });
        }

        /// <summary>
        /// 将所有轴参数写入控制卡，同时写入配置文件
        /// </summary>
        public async Task WriteAllToCardAsync(IProgressReporter progressReporter = null)
        {
            var allAxes = LoadAllAxes();
            int totalAxes = allAxes.Count;
            int completed = 0;

            foreach (var axis in allAxes)
            {
                try
                {
                    progressReporter?.Report((double)completed / totalAxes, $"Writing axis: {axis.Name}");
                    await WriteToCardAsync(axis);
                    completed++;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to write parameters for axis {axis.Name}");
                }
            }

            SaveAllAxisParameters(allAxes);
            progressReporter?.Report(1.0, "All axes written to card and saved");
            _logger.Info($"Wrote parameters for {completed}/{totalAxes} axes to card and saved to file");
        }

        /// <summary>
        /// 从控制卡读取所有轴参数，同时写入配置文件
        /// </summary>
        public async Task ReadAllFromCardAsync(IProgressReporter progressReporter = null)
        {
            var allAxes = LoadAllAxes();
            int totalAxes = allAxes.Count;
            int completed = 0;

            foreach (var axis in allAxes)
            {
                try
                {
                    progressReporter?.Report((double)completed / totalAxes, $"Reading axis: {axis.Name}");
                    await ReadFromCardAsync(axis);
                    completed++;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Failed to read parameters for axis {axis.Name}");
                }
            }

            SaveAllAxisParameters(allAxes);
            progressReporter?.Report(1.0, "All axes read from card and saved");
            _logger.Info($"Read parameters for {completed}/{totalAxes} axes from card and saved to file");
        }

        /// <summary>
        /// 保存所有轴参数到单一JSON文件
        /// </summary>
        public void SaveAllAxisParameters(IEnumerable<AxisInfo> axes)
        {
            try
            {
                var data = new AllAxisParametersData();
                foreach (var axis in axes)
                {
                    if (axis.Params != null)
                    {
                        string key = $"{axis.CardId}-{axis.AxisId}";
                        data.Axes[key] = axis.Params;
                    }
                }

                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PARAMS_DIR);
                _storage.Save(AXIS_PARAMS_FILE, data, configDir);

                _logger.Info($"Saved all axis parameters to single file");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save all axis parameters");
                throw;
            }
        }

        /// <summary>
        /// 从单一JSON文件加载所有轴参数
        /// </summary>
        public Dictionary<string, AxisParams> LoadAllAxisParameters()
        {
            try
            {
                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PARAMS_DIR);
                var data = _storage.Load<AllAxisParametersData>(AXIS_PARAMS_FILE, configDir);
                return data?.Axes ?? new Dictionary<string, AxisParams>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load all axis parameters");
                return new Dictionary<string, AxisParams>();
            }
        }

        public IEnumerable<InterpolationSystem> LoadInterpolationSystems()
        {
            var systems = new List<InterpolationSystem>();
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HW_CONFIG_PATH);
                if (!File.Exists(configPath))
                {
                    _logger.Warn($"hwcfg.xml not found at {configPath}");
                    return systems;
                }

                XDocument doc = XDocument.Load(configPath);
                var systemElements = doc.Descendants("InterpolationSystems")
                    .Elements("System");

                var savedSystems = LoadAllInterpolationSystems();

                foreach (var sysElem in systemElements)
                {
                    int coordId = int.Parse(sysElem.Attribute("coordId")?.Value ?? "0");
                    int actCardId = int.Parse(sysElem.Attribute("actCardId")?.Value ?? "0");

                    var system = new InterpolationSystem
                    {
                        CoordId = coordId,
                        ActCardId = actCardId,
                        Axes = new List<string>(),
                        Params = new InterpolationParams()
                    };

                    var axisElems = sysElem.Elements("Axis");
                    foreach (var axElem in axisElems)
                    {
                        string configId = axElem.Attribute("configId")?.Value;
                        if (!string.IsNullOrEmpty(configId))
                            system.Axes.Add(configId);
                    }

                    var savedSystem = savedSystems.FirstOrDefault(s => s.CoordId == coordId && s.ActCardId == actCardId);
                    if (savedSystem != null)
                    {
                        system.Axes = savedSystem.Axes ?? new List<string>();
                        system.Params = savedSystem.Params ?? new InterpolationParams();
                    }

                    systems.Add(system);
                }

                _logger.Info($"Loaded {systems.Count} interpolation systems from hwcfg.xml");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load interpolation systems from hwcfg.xml");
            }

            return systems;
        }

        /// <summary>
        /// 从控制卡读取插补系参数，同时写入配置文件
        /// </summary>
        public void ReadInterpolationFromCard(InterpolationSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));

            try
            {
                var card = _factory.GetCard(system.ActCardId);
                if (card == null)
                    throw new InvalidOperationException($"Motion card {system.ActCardId} not available");

                double startVel = 0, maxVel = 0, accTime = 0, decTime = 0, endVel = 0;
                int result = card.GetVectorProfileUnit(system.CoordId, ref startVel, ref maxVel, ref accTime, ref decTime, ref endVel);
                if (result == 0)
                {
                    system.Params.StartVelocity = startVel;
                    system.Params.InterpolationVelocity = maxVel;
                    system.Params.AccelerationTime = accTime;
                    system.Params.DecelerationTime = decTime;
                    system.Params.EndVelocity = endVel;
                }
                else _logger.Warn($"GetVectorProfileUnit failed for coord {system.CoordId}, code: {result}");

                double sPara = 0;
                result = card.GetVectorSProfile(system.CoordId, 0, ref sPara);
                if (result == 0) system.Params.SProfileTime = sPara;
                else _logger.Warn($"GetVectorSProfile failed for coord {system.CoordId}, code: {result}");

                _logger.Info($"Read interpolation parameters from card for system {system.CoordId}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to read interpolation parameters for system {system.CoordId}");
                throw;
            }
        }

        /// <summary>
        /// 将插补系参数写入控制卡，同时写入配置文件
        /// </summary>
        public void WriteInterpolationToCard(InterpolationSystem interpolationSystem)
        {
            if (interpolationSystem == null) throw new ArgumentNullException(nameof(interpolationSystem));
            if (interpolationSystem.Params == null) throw new InvalidOperationException("Interpolation parameters not loaded");

            try
            {
                var card = _factory.GetCard(interpolationSystem.ActCardId);
                if (card == null)
                    throw new InvalidOperationException($"Motion card {interpolationSystem.ActCardId} not available");

                var interpParams = interpolationSystem.Params;

                int result = card.SetVectorProfileUnit(
                    interpolationSystem.CoordId,
                    interpParams.StartVelocity,
                    interpParams.InterpolationVelocity,
                    interpParams.AccelerationTime,
                    interpParams.DecelerationTime,
                    interpParams.EndVelocity);
                if (result != 0) _logger.Warn($"SetVectorProfileUnit failed for coord {interpolationSystem.CoordId}, code: {result}");

                result = card.SetVectorSProfile(interpolationSystem.CoordId, 0, interpParams.SProfileTime);
                if (result != 0) _logger.Warn($"SetVectorSProfile failed for coord {interpolationSystem.CoordId}, code: {result}");

                _logger.Info($"Wrote interpolation parameters to card for system {interpolationSystem.CoordId}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to write interpolation parameters for system {interpolationSystem.CoordId}");
                throw;
            }
        }

        /// <summary>
        /// 保存所有插补系参数到单一JSON文件
        /// </summary>
        public void SaveAllInterpolationSystems(IEnumerable<InterpolationSystem> systems)
        {
            try
            {
                var configList = systems.Select(s => new InterpolationSystemConfig
                {
                    ActCardId = s.ActCardId,
                    CoordId = s.CoordId,
                    Axes = s.Axes,
                    Params = s.Params
                }).ToList();

                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PARAMS_DIR);
                _storage.Save(INTERP_SYSTEMS_FILE, configList, configDir);

                _logger.Info($"Saved all interpolation systems to single file");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save all interpolation systems");
                throw;
            }
        }

        /// <summary>
        /// 从单一JSON文件加载所有插补系参数
        /// </summary>
        public List<InterpolationSystemConfig> LoadAllInterpolationSystems()
        {
            try
            {
                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PARAMS_DIR);
                var data = _storage.Load<List<InterpolationSystemConfig>>(INTERP_SYSTEMS_FILE, configDir);
                return data ?? new List<InterpolationSystemConfig>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load all interpolation systems");
                return new List<InterpolationSystemConfig>();
            }
        }

        public double GetAxisSpeed(int cardId, int axisId)
        {
            try
            {
                var allParams = LoadAllAxisParameters();
                string key = $"{cardId}-{axisId}";
                if (allParams.ContainsKey(key))
                    return allParams[key]?.Motion?.MaxSpeed ?? 10.0;
                return 10.0;
            }
            catch
            {
                return 10.0;
            }
        }

        public double GetInterpolationSpeeds(int cardId, int coordId)
        {
            try
            {
                var systems = LoadInterpolationSystems();
                var system = systems.FirstOrDefault(s => s.ActCardId == cardId && s.CoordId == coordId);
                if (system != null && system.Params != null)
                    return system.Params.InterpolationVelocity;
                return 50.0;
            }
            catch
            {
                return 50.0;
            }
        }
    }
}
```

---

### Task 6: AxisSettingViewModel 重构

**Files:**
- Modify: `MotionControl/ViewModels/AxisSettingViewModel.cs`

- [ ] **Step 1: 移除不再需要的命令声明和字段**

移除以下命令声明：
- `SaveParamsCommand`
- `LoadParamsCommand`
- `ImportCommand`
- `ExportCommand`

移除以下方法：
- `SaveParams()`
- `LoadFromFile()`
- `ImportJson()`
- `ExportJson()`

- [ ] **Step 2: 添加新的命令声明**

添加以下命令声明替换被移除的：

```csharp
public DelegateCommand ReadAllFromCardCommand { get; }
```

- [ ] **Step 3: 重构构造函数中的命令初始化**

将构造函数中的命令初始化部分替换为：

```csharp
DownloadParamsCommand = new DelegateCommand(WriteToCard);
UploadParamsCommand = new DelegateCommand(ReadFromCard);
DownloadAllParametersCommand = new DelegateCommand(WriteAllToCard);
ReadAllFromCardCommand = new DelegateCommand(ReadAllFromCard);
ManageSystemAxesCommand = new DelegateCommand(OnManageSystemAxes);
AddSystemCommand = new DelegateCommand(OnAddSystem);
DeleteSystemCommand = new DelegateCommand(OnDeleteSystem);
SaveSystemParamsCommand = new DelegateCommand(OnSaveSystemParams);
ApplySystemParamsCommand = new DelegateCommand(OnApplyInterpolationSystem);
LoadSystemParamsCommand = new DelegateCommand(LoadSystemConfigurations);
```

- [ ] **Step 4: 替换 DownloadSelectedAxis 为 WriteToCard**

```csharp
/// <summary>
/// 写入到卡：将选中轴参数写入控制卡并保存到配置文件
/// </summary>
private async void WriteToCard()
{
    if (SelectedAxis == null) return;

    try
    {
        await _parameterService.WriteToCardAsync(SelectedAxis);
        _parameterService.SaveAllAxisParameters(Axes);
        ParametersChanged = false;
        MessageBox.Show("参数已写入控制卡并保存!");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"写入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

- [ ] **Step 5: 替换 UploadSelectedAxis 为 ReadFromCard**

```csharp
/// <summary>
/// 从卡读取：从控制卡读取选中轴参数并保存到配置文件
/// </summary>
private async void ReadFromCard()
{
    if (SelectedAxis == null) return;

    try
    {
        await _parameterService.ReadFromCardAsync(SelectedAxis);
        _parameterService.SaveAllAxisParameters(Axes);
        CurrentAxisParams = SelectedAxis.Params;
        RaisePropertyChanged(nameof(CurrentAxisParams));
        ParametersChanged = false;
        MessageBox.Show("参数已从控制卡读取并保存!");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"读取失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

- [ ] **Step 6: 替换 DownloadAllParameters 为 WriteAllToCard**

```csharp
/// <summary>
/// 写入所有轴参数到控制卡并保存到配置文件
/// </summary>
private async void WriteAllToCard()
{
    var dialog = new ParameterProgressDialog("写入所有轴参数");

    try
    {
        if (Application.Current.MainWindow != null &&
            Application.Current.MainWindow.IsLoaded)
        {
            dialog.Owner = Application.Current.MainWindow;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.Show();

        await _parameterService.WriteAllToCardAsync(new ProgressReporterAdapter(dialog));
        ParametersChanged = false;
        dialog.Close();
        MessageBox.Show("所有轴参数已写入控制卡并保存!");
    }
    catch (Exception ex)
    {
        dialog.Close();
        MessageBox.Show($"写入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

- [ ] **Step 7: 添加 ReadAllFromCard 方法**

```csharp
/// <summary>
/// 从控制卡读取所有轴参数并保存到配置文件
/// </summary>
private async void ReadAllFromCard()
{
    var dialog = new ParameterProgressDialog("从控制卡读取所有轴参数");

    try
    {
        if (Application.Current.MainWindow != null &&
            Application.Current.MainWindow.IsLoaded)
        {
            dialog.Owner = Application.Current.MainWindow;
        }
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        dialog.Show();

        await _parameterService.ReadAllFromCardAsync(new ProgressReporterAdapter(dialog));
        ParametersChanged = false;
        dialog.Close();

        if (SelectedAxis != null)
        {
            CurrentAxisParams = SelectedAxis.Params;
            RaisePropertyChanged(nameof(CurrentAxisParams));
        }

        MessageBox.Show("所有轴参数已从控制卡读取并保存!");
    }
    catch (Exception ex)
    {
        dialog.Close();
        MessageBox.Show($"读取失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

- [ ] **Step 8: 替换 OnApplySystemParams 为 OnApplyInterpolationSystem**

```csharp
/// <summary>
/// 应用插补系：将插补系参数写入控制卡并保存到配置文件
/// </summary>
private void OnApplyInterpolationSystem()
{
    if (SelectedSystem != null)
    {
        try
        {
            _parameterService.WriteInterpolationToCard(SelectedSystem);
            _parameterService.SaveAllInterpolationSystems(InterpolationSystems);
            MessageBox.Show("插补系参数已写入控制卡并保存",
                           "应用插补系", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"应用失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
```

- [ ] **Step 9: 更新 OnSaveSystemParams 方法**

```csharp
/// <summary>
/// 保存插补系配置到文件
/// </summary>
private void OnSaveSystemParams()
{
    if (SelectedSystem != null)
    {
        try
        {
            _parameterService.SaveAllInterpolationSystems(InterpolationSystems);
            MessageBox.Show($"插补系配置已保存",
              "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
```

- [ ] **Step 10: 更新 LoadSystemConfigurations 方法**

```csharp
/// <summary>
/// 从配置文件加载插补系参数
/// </summary>
private void LoadSystemConfigurations()
{
    try
    {
        var savedSystems = _parameterService.LoadAllInterpolationSystems();
        foreach (var savedSystem in savedSystems)
        {
            var system = InterpolationSystems.FirstOrDefault(s =>
                s.CoordId == savedSystem.CoordId && s.ActCardId == savedSystem.ActCardId);
            if (system != null)
            {
                system.Axes = savedSystem.Axes ?? new List<string>();
                system.Params = savedSystem.Params ?? new InterpolationParams();

                if (SelectedSystem?.CoordId == savedSystem.CoordId)
                {
                    UpdateAxesInSystem();
                }
            }
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"加载插补系配置失败: {ex.Message}",
            "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
```

- [ ] **Step 11: 移除不再需要的 using 引用**

移除 `using Newtonsoft.Json;`（如果ViewModel中不再直接使用JsonConvert）

---

### Task 7: AxisSettingView.xaml 更新

**Files:**
- Modify: `MotionControl/Views/AxisSettingView.xaml:478-555`

- [ ] **Step 1: 移除单轴模式下的导出/导入按钮**

删除 XAML 中第 487-498 行的 ExportCommand 和 ImportCommand 按钮。

- [ ] **Step 2: 移除单轴模式下的加载参数/保存参数按钮**

删除 XAML 中第 530-541 行的 LoadParamsCommand 和 SaveParamsCommand 按钮。

- [ ] **Step 3: 添加"从卡读取所有轴"按钮**

在 DownloadAllParametersCommand 按钮之后添加：

```xml
<Button Command="{Binding ReadAllFromCardCommand}" Style="{DynamicResource MaterialDesignOutlinedButton}" Background="#FF1565C0" Foreground="#1565C0" Margin="6,0,0,0" Visibility="{Binding IsSingleAxisMode, Converter={StaticResource BoolToVisibilityConverter}}" materialDesign:ButtonAssist.CornerRadius="4">
    <Button.ToolTip><TextBlock Text="{lang:Lang AxisSetting_ReadAllFromCardToolTip}"/></Button.ToolTip>
    <StackPanel Orientation="Horizontal">
        <materialDesign:PackIcon Kind="PlaylistPlus" Width="16" Height="16" VerticalAlignment="Center"/>
        <TextBlock Text="{lang:Lang AxisSetting_ReadAllFromCard}" Margin="4,0,0,0" VerticalAlignment="Center" FontSize="12"/>
    </StackPanel>
</Button>
```

- [ ] **Step 4: 将插补系模式下的"应用设置"按钮文本改为"应用插补系"**

将第 549-554 行的 ApplySystemParamsCommand 按钮中：
- `Kind="CheckCircle"` 改为 `Kind="ShapePolygonPlus"`
- `{lang:Lang AxisSetting_ApplySettings}` 改为 `{lang:Lang AxisSetting_ApplyInterpolationSystem}`

- [ ] **Step 5: 移除插补系模式下的加载参数按钮**

删除 XAML 中第 543-548 行的 LoadSystemParamsCommand 按钮。

更新后的底部按钮区域完整代码：

```xml
<Border Grid.Row="2" BorderThickness="0,1,0,0" BorderBrush="{DynamicResource MaterialDesignDivider}" Padding="0,10,0,5">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="*"/>
        </Grid.ColumnDefinitions>

        <StackPanel Grid.Column="0" Orientation="Horizontal" HorizontalAlignment="Left">
            <Button Command="{Binding ManageSystemAxesCommand}" Style="{DynamicResource MaterialDesignOutlinedButton}" ToolTip="{lang:Lang AxisSetting_ManageAxesToolTip}" Margin="6,0,0,0" Visibility="{Binding IsSystemMode, Converter={StaticResource BoolToVisibilityConverter}}" materialDesign:ButtonAssist.CornerRadius="4">
                <StackPanel Orientation="Horizontal">
                    <materialDesign:PackIcon Kind="AxisArrowInfo" Width="16" Height="16" VerticalAlignment="Center"/>
                    <TextBlock Text="{lang:Lang AxisSetting_ManageAxes}" Margin="4,0,0,0" VerticalAlignment="Center" FontSize="12"/>
                </StackPanel>
            </Button>
        </StackPanel>

        <Border Grid.Column="1" Width="1" Background="#FFE0E4E8" Margin="12,0"/>

        <StackPanel Grid.Column="2" Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Command="{Binding UploadParamsCommand}" Style="{DynamicResource MaterialDesignOutlinedButton}" ToolTip="{lang:Lang AxisSetting_ReadFromCardToolTip}" Visibility="{Binding IsSingleAxisMode, Converter={StaticResource BoolToVisibilityConverter}}" materialDesign:ButtonAssist.CornerRadius="4" BorderBrush="#1565C0" Foreground="#1565C0">
                <StackPanel Orientation="Horizontal">
                    <materialDesign:PackIcon Kind="Upload" Width="16" Height="16" VerticalAlignment="Center"/>
                    <TextBlock Text="{lang:Lang AxisSetting_ReadFromCard}" VerticalAlignment="Center" Margin="4,0,0,0" FontSize="12"/>
                </StackPanel>
            </Button>
            <Button Command="{Binding DownloadParamsCommand}" Style="{DynamicResource MaterialDesignOutlinedButton}" ToolTip="{lang:Lang AxisSetting_WriteToCardToolTip}" Margin="6,0,0,0" Visibility="{Binding IsSingleAxisMode, Converter={StaticResource BoolToVisibilityConverter}}" materialDesign:ButtonAssist.CornerRadius="4" BorderBrush="#1565C0" Foreground="#1565C0">
                <StackPanel Orientation="Horizontal">
                    <materialDesign:PackIcon Kind="Download" Width="16" Height="16" VerticalAlignment="Center"/>
                    <TextBlock Text="{lang:Lang AxisSetting_WriteToCard}" VerticalAlignment="Center" Margin="4,0,0,0" FontSize="12"/>
                </StackPanel>
            </Button>

            <Button Command="{Binding DownloadAllParametersCommand}" Style="{DynamicResource MaterialDesignRaisedButton}" Background="#FF1E88E5" Foreground="White" Margin="6,0,0,0" Visibility="{Binding IsSingleAxisMode, Converter={StaticResource BoolToVisibilityConverter}}" materialDesign:ButtonAssist.CornerRadius="4">
                <Button.ToolTip><TextBlock Text="{lang:Lang AxisSetting_SetAllAxesToolTip}"/></Button.ToolTip>
                <StackPanel Orientation="Horizontal">
                    <materialDesign:PackIcon Kind="PlaylistPlay" Width="16" Height="16" VerticalAlignment="Center"/>
                    <TextBlock Text="{lang:Lang AxisSetting_SetAllAxes}" Margin="4,0,0,0" VerticalAlignment="Center" FontSize="12"/>
                </StackPanel>
            </Button>
            <Button Command="{Binding ReadAllFromCardCommand}" Style="{DynamicResource MaterialDesignOutlinedButton}" Background="#FF1565C0" Foreground="#1565C0" Margin="6,0,0,0" Visibility="{Binding IsSingleAxisMode, Converter={StaticResource BoolToVisibilityConverter}}" materialDesign:ButtonAssist.CornerRadius="4">
                <Button.ToolTip><TextBlock Text="{lang:Lang AxisSetting_ReadAllFromCardToolTip}"/></Button.ToolTip>
                <StackPanel Orientation="Horizontal">
                    <materialDesign:PackIcon Kind="PlaylistPlus" Width="16" Height="16" VerticalAlignment="Center"/>
                    <TextBlock Text="{lang:Lang AxisSetting_ReadAllFromCard}" Margin="4,0,0,0" VerticalAlignment="Center" FontSize="12"/>
                </StackPanel>
            </Button>

            <Button Command="{Binding SaveSystemParamsCommand}" Style="{DynamicResource MaterialDesignRaisedButton}" Background="{DynamicResource MaterialDesign.Brush.Primary.Dark}" Foreground="White" ToolTip="{lang:Lang AxisSetting_SaveToFileToolTip}" Margin="6,0,0,0" Visibility="{Binding IsSystemMode, Converter={StaticResource BoolToVisibilityConverter}}" materialDesign:ButtonAssist.CornerRadius="4">
                <StackPanel Orientation="Horizontal">
                    <materialDesign:PackIcon Kind="ContentSave" Width="16" Height="16" VerticalAlignment="Center"/>
                    <TextBlock Text="{lang:Lang Save_Parameters}" VerticalAlignment="Center" Margin="4,0,0,0" FontSize="12"/>
                </StackPanel>
            </Button>
            <Button Command="{Binding ApplySystemParamsCommand}" Style="{DynamicResource MaterialDesignRaisedButton}" Background="#FF1E88E5" Foreground="White" ToolTip="{lang:Lang AxisSetting_ApplySystemSettingsToolTip}" Margin="6,0,0,0" Visibility="{Binding IsSystemMode, Converter={StaticResource BoolToVisibilityConverter}}" materialDesign:ButtonAssist.CornerRadius="4">
                <StackPanel Orientation="Horizontal">
                    <materialDesign:PackIcon Kind="ShapePolygonPlus" Width="16" Height="16" VerticalAlignment="Center"/>
                    <TextBlock Text="{lang:Lang AxisSetting_ApplyInterpolationSystem}" Margin="4,0,0,0" VerticalAlignment="Center" FontSize="12"/>
                </StackPanel>
            </Button>
        </StackPanel>
    </Grid>
</Border>
```

---

### Task 8: 多语言资源更新

**Files:**
- Modify: `MainApp/Languages/Strings.zh-CN.xaml`
- Modify: `MainApp/Languages/Strings.en-US.xaml`

- [ ] **Step 1: 在中文语言文件中添加/修改以下键值**

在 `AxisSetting_ApplySettings` 所在位置附近：

```xml
<sys:String x:Key="AxisSetting_ApplyInterpolationSystem">应用插补系</sys:String>
<sys:String x:Key="AxisSetting_ReadAllFromCard">从卡读取所有轴</sys:String>
<sys:String x:Key="AxisSetting_ReadAllFromCardToolTip">从运动控制卡读取所有轴参数并保存到配置文件</sys:String>
```

保留 `AxisSetting_ApplySettings` 键（可能其他地方引用），但不再在 View 中使用。

- [ ] **Step 2: 在英文语言文件中添加/修改以下键值**

在 `AxisSetting_ApplySettings` 所在位置附近：

```xml
<sys:String x:Key="AxisSetting_ApplyInterpolationSystem">Apply Interp System</sys:String>
<sys:String x:Key="AxisSetting_ReadAllFromCard">Read All From Card</sys:String>
<sys:String x:Key="AxisSetting_ReadAllFromCardToolTip">Read all axis parameters from motion card and save to config file</sys:String>
```

---

## 自检清单

### 1. 需求覆盖

| 需求 | 对应 Task |
|------|-----------|
| 从卡读取参数并写入到文件 | Task 3 (IMotionCard Get方法) + Task 5 (ReadFromCardAsync) |
| 写入到卡同时写入到配置文件 | Task 5 (WriteToCardAsync + SaveAllAxisParameters) |
| 删除加载参数、保存参数按钮 | Task 6 (移除命令) + Task 7 (移除XAML按钮) |
| 应用设置改成应用插补系 | Task 7 (XAML文本修改) + Task 8 (多语言) |
| 所有轴保存到一个文件 | Task 4 (AllAxisParametersData模型) + Task 5 (SaveAllAxisParameters/LoadAllAxisParameters) |

### 2. 占位符扫描

无 TBD、TODO、implement later 等占位符。

### 3. 类型一致性

- `ReadFromCardAsync` 返回 `Task`，ViewModel 中使用 `async void` 调用 ✓
- `WriteToCardAsync` 返回 `Task`，ViewModel 中使用 `async void` 调用 ✓
- `AllAxisParametersData.Axes` 使用 `Dictionary<string, AxisParams>`，Key 格式为 `{cardId}-{axisId}` ✓
- `InterpolationSystemConfig` 属性与 `InterpolationSystem` 一致 ✓
- `JsonParameterStorage.Save<T>` 和 `Load<T>` 泛型约束为 `class`，`AllAxisParametersData` 和 `List<InterpolationSystemConfig>` 均满足 ✓

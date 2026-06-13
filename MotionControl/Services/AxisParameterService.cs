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
        private const string PARAMS_DIR = "Config/AxisSettings";

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

                    // 从 hwcfg.xml 读取 setCardId 和 actAxisId（与 HardwareConfigParser 保持一致）
                    int cardId = 0;
                    int axisId = setAxisId;

                    var cardAttr = axisElem.Attribute("setCardId");
                    if (cardAttr != null)
                        cardId = int.Parse(cardAttr.Value);

                    var axisIdAttr = axisElem.Attribute("actAxisId");
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

                    string axesAttr = sysElem.Attribute("axes")?.Value;
                    if (!string.IsNullOrWhiteSpace(axesAttr))
                    {
                        system.Axes = axesAttr.Split(',')
                            .Select(a => a.Trim())
                            .Where(a => !string.IsNullOrEmpty(a))
                            .ToList();
                    }
                    else
                    {
                        var axisElems = sysElem.Elements("Axis");
                        foreach (var axElem in axisElems)
                        {
                            string configId = axElem.Attribute("configId")?.Value;
                            if (!string.IsNullOrEmpty(configId))
                                system.Axes.Add(configId);
                        }
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

        /// <summary>
        /// 将插补系轴配置同步到hwcfg.xml（更新axes属性）
        /// axes属性格式："卡号-轴号,卡号-轴号"
        /// </summary>
        public void SyncInterpolationAxesToHwConfig(IEnumerable<InterpolationSystem> systems)
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HW_CONFIG_PATH);
                if (!File.Exists(configPath))
                {
                    _logger.Warn($"hwcfg.xml not found at {configPath}");
                    return;
                }

                XDocument doc = XDocument.Load(configPath);
                var systemElements = doc.Descendants("InterpolationSystems")
                    .Elements("System");

                foreach (var system in systems)
                {
                    var sysElem = systemElements.FirstOrDefault(e =>
                        (int?)e.Attribute("coordId") == system.CoordId &&
                        (int?)e.Attribute("actCardId") == system.ActCardId);

                    if (sysElem != null)
                    {
                        string axesValue = system.Axes != null && system.Axes.Any()
                            ? string.Join(",", system.Axes)
                            : "";

                        var existingAttr = sysElem.Attribute("axes");
                        if (existingAttr != null)
                            existingAttr.Value = axesValue;
                        else
                            sysElem.Add(new XAttribute("axes", axesValue));

                        var oldAxisElems = sysElem.Elements("Axis").ToList();
                        foreach (var old in oldAxisElems) old.Remove();

                        foreach (var axisId in system.Axes ?? Enumerable.Empty<string>())
                        {
                            sysElem.Add(new XElement("Axis", new XAttribute("configId", axisId)));
                        }
                    }
                }

                doc.Save(configPath);
                _logger.Info("Synced interpolation axes to hwcfg.xml");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to sync interpolation axes to hwcfg.xml");
                throw;
            }
        }

        /// <summary>
        /// 从hwcfg.xml读取插补系轴配置（axes属性格式："卡号-轴号,卡号-轴号"）
        /// </summary>
        public void LoadInterpolationAxesFromHwConfig(IEnumerable<InterpolationSystem> systems)
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HW_CONFIG_PATH);
                if (!File.Exists(configPath))
                {
                    _logger.Warn($"hwcfg.xml not found at {configPath}");
                    return;
                }

                XDocument doc = XDocument.Load(configPath);
                var systemElements = doc.Descendants("InterpolationSystems")
                    .Elements("System");

                foreach (var system in systems)
                {
                    var sysElem = systemElements.FirstOrDefault(e =>
                        (int?)e.Attribute("coordId") == system.CoordId &&
                        (int?)e.Attribute("actCardId") == system.ActCardId);

                    if (sysElem != null)
                    {
                        var axesList = new List<string>();

                        string axesAttr = sysElem.Attribute("axes")?.Value;
                        if (!string.IsNullOrWhiteSpace(axesAttr))
                        {
                            axesList = axesAttr.Split(',')
                                .Select(a => a.Trim())
                                .Where(a => !string.IsNullOrEmpty(a))
                                .ToList();
                        }

                        if (!axesList.Any())
                        {
                            foreach (var axElem in sysElem.Elements("Axis"))
                            {
                                string configId = axElem.Attribute("configId")?.Value;
                                if (!string.IsNullOrEmpty(configId))
                                    axesList.Add(configId);
                            }
                        }

                        system.Axes = axesList;
                    }
                }

                _logger.Info("Loaded interpolation axes from hwcfg.xml");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load interpolation axes from hwcfg.xml");
            }
        }
    }
}

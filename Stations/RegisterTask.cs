using HSMS;
using Unity;
using Unity.Resolution;
using Interfaces;
using Interfaces.Mvvm;
using Interfaces.Services;
using NLog;
using Prism.Events;
using Prism.Ioc;
using SmarterMotion;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Core.Utilities;
using System.Threading.Tasks;
using Core.Abstraction;
using Microsoft.Xaml.Behaviors.Layout;
using Core.Services;

namespace Stations
{
    // 设备初始化接口
    public interface IDeviceInitializer
    {
        void InitializeHardware();
        void ReportAlarm(/* ... */);
    }

    // 任务管理接口
    public interface ITaskCoordinator
    {
        void RegisterTasks();
        void BindTasksToStations();
    }
    public class EmptyTaskParameters : TaskParametersBase
    {
        // 空参数类，无需任何实现
        public override string Identifier => "EmptyTaskParameters";
    }
    public class RegisterTask : XTask<EmptyTaskParameters>, IDeviceInitializer, ITaskCoordinator
    {
        private XCommandCardLeisai3000 cardLeisai;
        private string[] axisCfg;
        private int cardnum = 0;
        private XmlDocument xmlDoc;
        private string appStartupPath;
        private double moveSpeedPercent = 10;
        private double jogSpeedPercent = 10;
        private readonly List<IDeviceManager> _registeredTasks = new();
        private readonly ILoggerService _logger;
        private readonly IContainerExtension _container;
        private readonly IEventAggregator _ea;
        private readonly ISecsGemService _secsGemService;
        private readonly IAlarmService _alarmService;
        private readonly IConfigurationService _configurationService;
        private readonly AppConfig _appConfig;
        private readonly TaskInstanceManager _taskInstanceManager;

        public RegisterTask(IContainerExtension container,
                            IEventAggregator ea,
                            ISecsGemService secService,
                            IAlarmService alarmService,
                            ILoggerService loggerService,
                            IConfigurationService configurationService,
                            TaskInstanceManager taskManager,
                            AppConfig appConfig)
        : base(0, "SystemRegisterTask")  // 调用基类构造函数
        {
            _container = container;
            _ea = ea;
            _secsGemService = secService;
            _alarmService = alarmService;
            _taskInstanceManager = taskManager;
            _appConfig = appConfig;
            _logger = loggerService;
            // 初始化其他成员
            cardLeisai = new XCommandCardLeisai3000();
            xmlDoc = new XmlDocument();
            appStartupPath = AppDomain.CurrentDomain.BaseDirectory;
            // ... 其他初始化
            _configurationService = configurationService;
        }
        private void ShowMessage(string infoMsg)
        {
            _ea.GetEvent<MessageEvent>().Publish(new()
            {
                Target = "errLog",
                Content = infoMsg
            });
        }
        private void ShowError(string errMsg)
        {
            _ea.GetEvent<MessageEvent>().Publish(new()
            {
                Target = "errLog",
                Content = errMsg
            });
        }

        #region IDeviceInitializer 实现
        public void InitializeHardware()
        {
            try
            {
                //硬件初始化
                BindCard();
                BindAxis();
                BindDi();
                BindDo();
                BindStation();
                BindStationTask();
                //注册轴,DI,DO
                RegisterAxis();
                RegisterDI();
                RegisterDO();
                //初始化参数
                InitSpeedRation();
                LoadPositionConfig();
                //初始化服务
                StartSystemServices();
                //注册设备到任务
                RegisterTasks();
                ReportAlarm();
                SystemInitialize();
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"初始化Task任务出错" + " => Error:" + ex.Message);
                ShowError($"初始化Task任务出错" + " => Error:" + ex.Message);
            }

        }
        /// <summary>
        /// 系统启动初始化
        /// </summary>
        public void SystemInitialize()
        {
            // 重置所有工站间通信信号
            StationEvents.ResetAllSignals();

            _logger.Info("工站间通信信号已重置");
        }
        #endregion
        #region ITaskCoordinator 实现
        [AttributeUsage(AttributeTargets.Class)]
        public class TaskIdAttribute : Attribute
        {
            public int TaskId { get; }

            public TaskIdAttribute(int taskId)
            {
                TaskId = taskId;
            }
        }
        public void RegisterTasks()
        {
            // 动态发现所有任务（通过反射或配置）
            var taskTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IDeviceManager).IsAssignableFrom(t) && !t.IsAbstract);

            foreach (var type in taskTypes)
            {
                // 获取任务ID属性
                var taskIdAttr = type.GetCustomAttribute<TaskIdAttribute>();
                if (taskIdAttr == null)
                {
                    // 如果没有TaskId属性，跳过或使用默认ID
                    continue;
                }

                int taskId = taskIdAttr.TaskId;
                var task = _taskInstanceManager.GetOrCreateTask(type, taskId) as IDeviceManager;
                if (task != null)
                {
                    _registeredTasks.Add(task);
                    task.RegisterDevice(); // 注册设备信息，例如轴、DI/DO等
                }
            }
        }
        public void BindTasksToStations()
        {
            //var stationConfig = _configLoader.LoadStationConfig();
            //foreach (var mapping in stationConfig.TaskMappings)
            //{
            //    var task = _registeredTasks.FirstOrDefault(t => t.TaskId == mapping.TaskId);
            //    if (task != null)
            //    {
            //        XTaskManager.Instance.BindTask(mapping.TaskId, task, task.Name);
            //        XStationManager.Instance.Stations[mapping.StationId].BindTask(mapping.TaskId);
            //    }
            //}
        }
        #endregion
        #region 私有方法
        private void LoadPositionConfig()
        {
            string currentProduct = _appConfig.Name;

            // 初始化位置配置 - 完全使用 JSON
            string configPath = AppDomain.CurrentDomain.BaseDirectory;

            // 不再需要设置 XPositionManager 的路径
            // 直接使用 IConfigurationService 加载位置数据

            for (int i = 0; i < XTaskManager.Instance.Tasks.Count; i++)
            {
                int taskId = i + 1;

                // 从 JSON 加载位置数据到内存
                LoadPositionsFromJson(taskId);

                _logger.Info($"Task ID:{taskId} 位置参数配置完成");
            }

            // 初始化参数文件
            //foreach (var item in XSettingManager.Instance.SettingMap.Values)
            //{
            //    item.Product = _appConfig.Name;
            //    item.LastProduct = _appConfig.LastRecipeName;
            //}
        }
        private void LoadPositionsFromJson(int taskId)
        {
            try
            {
                var positionData = _configurationService.LoadConfiguration<PositionData>($"Position_Task_{taskId}");

                if (positionData != null)
                {
                    _logger.Info($"从JSON加载位置数据成功: Task_{taskId}, 共 {positionData.Positions.Count} 个点位");

                    // 这里可以根据需要将位置数据缓存到其他服务中
                    // 例如：_positionCacheService.CachePositionData(taskId, positionData);
                }
                else
                {
                    _logger.Info($"未找到位置数据: Task_{taskId}，将使用默认空数据");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"从JSON加载位置数据失败 Task_{taskId}: {ex.Message}");
                // 可以在这里创建默认的位置数据
                CreateDefaultPositionData(taskId);
            }
        }
        // 新增方法：创建默认位置数据
        private void CreateDefaultPositionData(int taskId)
        {
            try
            {
                // 获取任务的轴ID组（这里需要根据你的系统架构调整）
                int[] axisIds = GetAxisIdsForTask(taskId);

                if (axisIds != null && axisIds.Length > 0)
                {
                    var defaultPositionData = new PositionData
                    {
                        AxisIds = axisIds,
                        Positions = new Dictionary<string, PositionInfo>()
                    };

                    // 保存默认数据
                    _configurationService.SaveConfiguration($"Position_Task_{taskId}", "json", defaultPositionData);
                    _logger.Info($"创建默认位置数据: Task_{taskId}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"创建默认位置数据失败 Task_{taskId}: {ex.Message}");
            }
        }

        // 辅助方法：根据任务ID获取轴ID组
        private int[] GetAxisIdsForTask(int taskId)
        {
            // 这里需要根据你的系统架构实现
            // 可能从配置文件、数据库或其他服务获取
            // 返回一个默认的轴ID数组
            return new int[] { 9, 10, 11 }; // 示例默认值
        }
        private void StartSystemServices()
        {
            //告警
            XAlarmReporter.Instance.IsProductingPageCreated = true;
            XAlarmReporter.Instance.IsAlarmingPageCreated = true;
            XAlarmReporter.Instance.Start();
            XController.Instance.Start();
            XMachine.Instance.Start();
        }
        #endregion

        /// <summary>
        /// 绑定卡
        /// </summary>
        private void BindCard()
        {
            #region 初始化运动控制卡
            string xml = appStartupPath + @"Config\HWConfig\hwcfg.xml";
            cardLeisai.Initial();
            xmlDoc.Load(xml);
            //加载轴卡配置文件
            XmlNode configNode = xmlDoc.SelectSingleNode("MotionSystem/MotionState");
            //解析轴卡数量
            XmlNodeList numNodes = configNode.SelectNodes("CardNum");
            foreach (XmlNode item in numNodes)
            {
                cardnum = Convert.ToInt32(item.Attributes["cardnum"].Value);
            }
            axisCfg = new string[cardnum];
            //解析轴卡
            XmlNode infoNode = xmlDoc.SelectSingleNode("MotionSystem/MotionState");
            //解析运动控制卡
            int setCardId = 0; int actCardId = 0;
            string cmd = string.Empty;
            string name = string.Empty;
            XmlNodeList cardNodes = infoNode.SelectNodes("MotionCard");
            foreach (XmlNode item in cardNodes)
            {
                setCardId = Convert.ToInt32(item.Attributes["setCardId"].Value);
                actCardId = Convert.ToInt32(item.Attributes["actCardId"].Value);
                cmd = item.Attributes["XCommandCard"].Value;
                name = item.Attributes["name"].Value;
                //需手动填入cardLeisai 待修改成根据品牌判断
                XDevice.Instance.BindCard(setCardId, actCardId, cardLeisai, name);
            }
            //解析配置文件
            ushort index = 0;
            XmlNodeList cfgNodes = configNode.SelectNodes("Config");
            foreach (XmlNode item in cfgNodes)
            {
                // 移除开头的反斜杠
                string relativePath = item.Attributes["path"].Value.TrimStart('\\', '/');
                axisCfg[index] = Path.Combine(appStartupPath, relativePath);
                int ret = cardLeisai.LoadParam(index, axisCfg[index]);
                if (ret == 0)
                {
                    IMessage.Logger.Log(LogLevel.Info, $"软件加载[加载总线卡{index}（ini文件参数）]完成");
                    ShowMessage($"软件加载[加载总线卡{index}（ini文件参数）]完成");
                }
                else
                {
                    if (XDevice.Instance.FindCardById(index + 1).SoftReset(index) == 0)
                    {
                        ret = cardLeisai.LoadParam(index, axisCfg[index]);
                        if (ret == 0)
                        {
                            IMessage.Logger.Log(LogLevel.Info, $"软件加载[加载总线卡{index}（ini文件参数）]完成");
                            ShowMessage($"软件加载[加载总线卡{index}（ini文件参数）]完成");
                        }
                        else
                        {
                            IMessage.Logger.Error($"软件加载[加载总线卡{index}（ini文件参数）]错误代码, {ret}");
                            ShowError($"软件加载[加载总线卡{index}（ini文件参数）]错误, {ret}");
                            ReportAlarm(XAlarmLevel.TIP, 1011, (int)XSysAlarmId.CARD_INIT_FAIL, AlarmCategory.SYSTEM.ToString(), XSysAlarmId.CARD_INIT_FAIL.ToString(), "ini文件参数错误");
                        }
                    }
                    else
                    {
                        string errinfo = "dmc_download_configfile == " + ret.ToString();
                        IMessage.Logger.Error($"软件加载[加载总线卡{index}（ini文件参数）]错误, {errinfo}");
                        ShowError($"软件加载[加载总线卡{index}（ini文件参数）]错误, {errinfo}");
                        ReportAlarm(XAlarmLevel.TIP, 1011, (int)XSysAlarmId.CARD_INIT_FAIL, AlarmCategory.SYSTEM.ToString(), XSysAlarmId.CARD_INIT_FAIL.ToString(), "ini文件参数错误");
                    }
                }
                index++;
            }
            #endregion
        }
        /// <summary>
        /// 绑定轴配置文件和轴方向，注册轴到控制器
        /// </summary>
        private void BindAxis()
        {
            //解析轴
            XmlNode axiss = xmlDoc.SelectSingleNode("MotionSystem/MotionState/AxisConfig/Axes");
            XmlNodeList axisnodelist = axiss.ChildNodes;  //得到该节点的子节点
            for (int i = 0; i < axisnodelist.Count; i++)
            {
                int setCardId = Convert.ToInt32(axisnodelist[i].Attributes["setCardId"].Value);
                int setAxisId = Convert.ToInt32(axisnodelist[i].Attributes["setAxisId"].Value);
                int actAxisId = Convert.ToInt32(axisnodelist[i].Attributes["actAxisId"].Value);
                double lead = Convert.ToDouble(axisnodelist[i].Attributes["lead"].Value);
                string name = axisnodelist[i].Attributes["name"].Value;
                string dir = axisnodelist[i].Attributes["axisDirection"].Value;
                XAxisDirection axisDirection = (XAxisDirection)Enum.Parse(typeof(XAxisDirection), dir);
                //绑定轴
                XDevice.Instance.BindAxis(setCardId, setAxisId, actAxisId, lead, name, axisDirection);
                //注册轴
                XController.Instance.RegisterAxis(setAxisId);
            }
            XController.Instance.StationId = 1;
        }
        /// <summary>
        /// 绑定工站任务
        /// </summary>
        private void BindStation()
        {
            //解析任务
            int stationId = 0;
            XmlNode tasks = xmlDoc.SelectSingleNode("MotionSystem/MotionState/Station");
            XmlNodeList tasknodelist = tasks.ChildNodes;  //得到该节点的子节点
            for (int i = 0; i < tasknodelist.Count; i++)
            {
                //解析工站号
                stationId = Convert.ToInt32(tasknodelist[i].Attributes["stationId"].Value);
                string name = tasknodelist[i].Attributes["name"].Value;
                XStationManager.Instance.BindStation(stationId, name);
            }
            //解析信号灯
            XmlNode light = xmlDoc.SelectSingleNode("MotionSystem/MotionState/Light");
            XmlNodeList lightnodelist = light.ChildNodes;  //得到该节点的子节点
            foreach (XmlNode item in lightnodelist)
            {
                XmlAttributeCollection ac = item.Attributes;
                foreach (XmlAttribute att in ac)
                {
                    string str = att.Name;//节点下的属性名称
                    int id = Convert.ToInt32(att.Value);
                    if (str == "greenLight")
                    {
                        XStationManager.Instance.Stations[stationId].SetLightGreenDo(id);
                    }
                    else if (str == "orangeLight")
                    {
                        XStationManager.Instance.Stations[stationId].SetLightOrangeDo(id);
                    }
                    else if (str == "redLight")
                    {
                        XStationManager.Instance.Stations[stationId].SetLightRedDo(id);
                    }
                    else if (str == "buzzer")
                    {
                        XStationManager.Instance.Stations[stationId].SetBuzzerDo(id);
                    }
                }
            }
        }

        private void BindDi()
        {
            //解析输入点IO
            XmlNode inputs = xmlDoc.SelectSingleNode("MotionSystem/MotionState/Inputs");
            XmlNodeList inputnodelist = inputs.ChildNodes;  //得到该节点的子节点
            for (int i = 0; i < inputnodelist.Count; i++)
            {
                int setCardId = Convert.ToInt32(inputnodelist[i].Attributes["setCardId"].Value);
                int setDiId = Convert.ToInt32(inputnodelist[i].Attributes["setDiId"].Value);
                int channel = Convert.ToInt32(inputnodelist[i].Attributes["channel"].Value);
                int actDiId = Convert.ToInt32(inputnodelist[i].Attributes["actDiId"].Value);
                string name = inputnodelist[i].Attributes["name"].Value;
                string cardname = inputnodelist[i].Attributes["cardname"].Value;
                //绑定DI
                XDevice.Instance.BindDi(setCardId, setDiId, channel, actDiId, name, cardname);
                //注册DI
                XController.Instance.RegisterDi(setDiId);
            }

        }
        private void BindDo()
        {
            //解析输出点IO
            XmlNode outputs = xmlDoc.SelectSingleNode("MotionSystem/MotionState/Outputs");
            XmlNodeList outputnodelist = outputs.ChildNodes;  //得到该节点的子节点
            for (int i = 0; i < outputnodelist.Count; i++)
            {
                int setCardId = Convert.ToInt32(outputnodelist[i].Attributes["setCardId"].Value);
                int setDoId = Convert.ToInt32(outputnodelist[i].Attributes["setDoId"].Value);
                int channel = Convert.ToInt32(outputnodelist[i].Attributes["channel"].Value);
                int actDoId = Convert.ToInt32(outputnodelist[i].Attributes["actDoId"].Value);
                string name = outputnodelist[i].Attributes["name"].Value;
                string cardname = outputnodelist[i].Attributes["cardname"].Value;
                //绑定DO
                XDevice.Instance.BindDo(setCardId, setDoId, channel, actDoId, name, cardname);
                //注册DO
                XController.Instance.RegisterDo(setDoId);
            }
        }
        private void BindStationTask()
        {
            XmlNode tasksNode = xmlDoc.SelectSingleNode("MotionSystem/MotionState/Task");
            if (tasksNode == null) return;

            XmlNodeList taskNodes = tasksNode.ChildNodes;

            var registeredTaskTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IDeviceManager).IsAssignableFrom(t) && !t.IsAbstract)
                .ToDictionary(t => t.Name, t => t);

            foreach (XmlNode taskNode in taskNodes)
            {
                int taskId = Convert.ToInt32(taskNode.Attributes["taskId"].Value);
                string taskName = taskNode.Attributes["name"].Value;
                string taskTypeName = taskNode.Attributes["type"].Value;
                int stationId = Convert.ToInt32(taskNode.Attributes["stationId"].Value);

                if (!registeredTaskTypes.TryGetValue(taskTypeName, out Type taskType))
                {
                    _logger.Error($"未知的任务类型: {taskTypeName}");
                    continue;
                }

                // 使用统一的任务实例管理器
                IMotionTask taskInstance = _taskInstanceManager.GetOrCreateTask(taskType, taskId) as IMotionTask;

                if (taskInstance != null)
                {
                    // 绑定任务到系统
                    XTaskManager.Instance.BindTask(taskId, taskInstance, taskName);
                    XStationManager.Instance.Stations[stationId].BindTask(taskId);
                    _logger.Info($"绑定任务: ID={taskId}, Name={taskName}, Type={taskTypeName}");
                }
            }
        }

        /// <summary>
        /// 用户自定义注册轴对应的Task
        /// </summary>
        private void RegisterAxis()
        {
            XmlNode axiss = xmlDoc.SelectSingleNode("MotionSystem/MotionState/AxisConfig/Axes");
            if (axiss == null) return;

            int registeredCount = 0;
            XmlNodeList axisnodelist = axiss.ChildNodes;

            foreach (XmlNode axisNode in axisnodelist)
            {
                int setAxisId = Convert.ToInt32(axisNode.Attributes["setAxisId"].Value);
                int taskId = Convert.ToInt32(axisNode.Attributes["taskId"].Value);
                string axisName = axisNode.Attributes["name"]?.Value ?? $"轴{setAxisId}";

                // 使用统一的任务实例管理器获取实例
                ITask taskInstance = _taskInstanceManager.GetTask(taskId);

                if (taskInstance is IMotionTask deviceManager)
                {
                    deviceManager.RegisterAxis(setAxisId);
                    registeredCount++;
                    _logger.Debug($"为任务 {taskId} 注册轴 {setAxisId} ({axisName})");
                }
                else
                {
                    _logger.Warn($"任务 {taskId} 未实现 IDeviceManager 接口，无法注册轴");
                }
            }

            _logger.Info($"绑定运动轴到系统完成，共注册 {registeredCount} 个轴");
        }
        /// <summary>
        /// 初始化速度百分比
        /// </summary>
        private void InitSpeedRation()
        {
            //解析速度
            XmlNode tasks = xmlDoc.SelectSingleNode("MotionSystem/MotionState/SpeedRatio");
            XmlNodeList tasknodelist = tasks.ChildNodes;  //得到该节点的子节点
            for (int i = 0; i < tasknodelist.Count; i++)
            {
                //解析速度百分比
                if (tasknodelist[i].Attributes["name"].Value == "SpeedRatio")
                {
                    int ratio = Convert.ToInt32(tasknodelist[i].Attributes["MotionSpeedRatio"].Value);
                    moveSpeedPercent = ratio;
                }
                if (tasknodelist[i].Attributes["name"].Value == "JogSpeed")
                {
                    int ratio = Convert.ToInt32(tasknodelist[i].Attributes["JogVelocityPercent"].Value);
                    jogSpeedPercent = ratio;
                }
            }
            for (int i = 0; i < XDevice.Instance.AxisMap.Count; i++)
            {
                XDevice.Instance.AxisMap[i].MotionSpeedRatio = moveSpeedPercent / 100;
                XDevice.Instance.AxisMap[i].JogVelocityPercent = jogSpeedPercent / 100;
            }
            IMessage.Logger.Log(LogLevel.Warn, $"运行速度调整为: {moveSpeedPercent}%");
        }
        public void ReportAlarm()
        {
            //注册告警事件
            RegisterAlarm xAlarm = new RegisterAlarm(_alarmService, _secsGemService);
            xAlarm.RegisterEvent();
        }

        /// <summary>
        /// 用户自定义注册DI
        /// </summary>
        private void RegisterDI()
        {
            //设置按钮
            XmlNode signal = xmlDoc.SelectSingleNode("MotionSystem/MotionState/Signal");
            XmlNodeList signalnodelist = signal.ChildNodes;  //得到该节点的子节点
            foreach (XmlNode item in signalnodelist)
            {
                XmlAttributeCollection ac = item.Attributes;
                foreach (XmlAttribute att in ac)
                {
                    string str = att.Name;//节点下的属性名称

                    if (str == "signalStart")
                    {
                        int signalStartId = Convert.ToInt32(att.Value);
                        XMachine.Instance.SetStartDi(signalStartId);
                    }
                    if (str == "signalStop")
                    {
                        int signalStopId = Convert.ToInt32(att.Value);
                        XMachine.Instance.SetStopDi(signalStopId);
                    }
                    if (str == "signalReset")
                    {
                        int signalResetId = Convert.ToInt32(att.Value);
                        XMachine.Instance.SetResetDi(signalResetId);
                    }
                    if (str == "signalEStop")
                    {
                        if (!string.IsNullOrEmpty(att.Value))
                        {
                            int signalResetId = Convert.ToInt32(att.Value);
                            XMachine.Instance.AddEStopDi(signalResetId);
                        }
                    }
                    if (str == "signalDoor")
                    {
                        if (!string.IsNullOrEmpty(att.Value))
                        {
                            int signalResetId = Convert.ToInt32(att.Value);
                            XMachine.Instance.AddDoorDi(signalResetId);
                        }
                    }
                    if (str == "signalMagneticni")
                    {
                        if (!string.IsNullOrEmpty(att.Value))
                        {
                            int signalResetId = Convert.ToInt32(att.Value);
                            XMachine.Instance.AddDoorMagneticni(signalResetId);
                        }
                    }
                    if (str == "signalGrating")
                    {
                        if (!string.IsNullOrEmpty(att.Value))
                        {
                            int signalResetId = Convert.ToInt32(att.Value);
                            XMachine.Instance.AddCurtainDi(signalResetId);
                        }
                    }
                    if (str == "signalNoHave")
                    {
                        if (!string.IsNullOrEmpty(att.Value))
                        {
                            int signalResetId = Convert.ToInt32(att.Value);//复位时要无信号
                            XMachine.Instance.AddConditionsCOpenOnInitialization(signalResetId);
                        }
                    }
                    if (str == "signalHaved")
                    {
                        if (!string.IsNullOrEmpty(att.Value))
                        {
                            int signalResetId = Convert.ToInt32(att.Value);//复位时要有信号
                            XMachine.Instance.AddConditionsCCloseOnInitialization(signalResetId);
                        }
                    }
                    if (str == "signalCondition")
                    {
                        if (!string.IsNullOrEmpty(att.Value))
                        {
                            int signalResetId = Convert.ToInt32(att.Value);//启动条件 气源
                            XMachine.Instance.AddConditionsOnStart(signalResetId);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 用户自定义注册DO
        /// </summary>
        private void RegisterDO()
        {
            //设置蜂鸣器、指示灯、安全门锁
            XmlNode output = xmlDoc.SelectSingleNode("MotionSystem/MotionState/Output");
            XmlNodeList outputnodelist = output.ChildNodes;  //得到该节点的子节点
            foreach (XmlNode item in outputnodelist)
            {
                XmlAttributeCollection ac = item.Attributes;
                foreach (XmlAttribute att in ac)
                {
                    string str = att.Name;//节点下的属性名称
                    if (att.Value == "")
                    {
                        break;
                    } 
                    if (str == "StartLight")
                    {
                        int outId = Convert.ToInt32(att.Value);
                        XMachine.Instance.SetStartLightDo(outId);
                    }
                    if (str == "StopLight")
                    {
                        int outId = Convert.ToInt32(att.Value);
                        XMachine.Instance.SetStopLightDo(outId);
                    }
                    if (str == "ResetLight")
                    {
                        int outId = Convert.ToInt32(att.Value);
                        XMachine.Instance.SetResetLight1Do(outId);
                    }
                    if (str == "OpenDoorLightLight")
                    {
                        if (!string.IsNullOrEmpty(att.Value))
                        {
                            int outId = Convert.ToInt32(att.Value);
                            XMachine.Instance.SetOpenDoorLightDo(outId);
                        }
                    }
                    if (str == "CloseDoorLightLight")
                    {
                        if (!string.IsNullOrEmpty(att.Value))
                        {
                            int outId = Convert.ToInt32(att.Value);
                            XMachine.Instance.SetCloseDoorLightDo(outId);
                        }
                    }
                    if (str == "SafeDoor")
                    {
                        int outId = Convert.ToInt32(att.Value);
                        XMachine.Instance.AddSafeDoorDo(outId);
                    }
                }
            }
        }

        #region 抽象类实现


        protected override void ExecuteHoming()
        {
          
        }

        protected override void InitProcessVar()
        {
           
        }

        protected override void OnErrorOccurred()
        {
          
        }
        #endregion
    }

}

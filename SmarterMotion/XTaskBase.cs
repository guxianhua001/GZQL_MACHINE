using System.Collections.Generic;
using System;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Interfaces;
using Interfaces.Events;
using Prism.Events;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using System.IO;
using SmarterMotion.Events;
using Core.Abstraction;
using Core.Services;
using Framework.Services;

namespace SmarterMotion
{
    // 使用泛型重构任务基类
    public abstract class XTaskBase<TParams> : XTaskEventHandler, ITask, IMotionTask
        where TParams : TaskParametersBase, new()
    {
        private readonly IParameterStorage _parameterStorage;
        public IEventAggregator _eventAggregator;
        // 参数属性
        private TParams _parameters;
        public TParams Parameters
        {
            get => _parameters;
            set
            {
                _parameters = value;
                OnParametersChanged();
            }
        }
        // 构造函数，接收任务ID和任务名称
        public XTaskBase(int taskId, string taskName, IEventAggregator eventAggregator = null, IParameterStorage parameterStorage = null)
        {
            TaskId = taskId;
            Name = taskName;
            _parameterStorage = parameterStorage ?? new JsonParameterStorage();
            InitializeParameters();
            _eventAggregator = eventAggregator;
        }
        // ITask 实现
        public int TaskId { get; set; }
        public string Name { get; set; }
        public int Step { get; set; }
        public int LastStep { get; set; }
        // 泛型参数基类接口实现
        public TaskParametersBase ParametersBase => Parameters;
        private bool _isRunning = false;
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                _isRunning = value;
            }
        }
        private bool _isPaused = false;
        private bool _isStopped = false;
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                _isPaused = value;
            }
        }
        public bool IsStopped
        {
            get => _isStopped;
            set
            {
                _isStopped = value;
            }
        }
        public int stationId;
        public Thread _thread;
        public CancellationTokenSource _cts;
        public XMove xMove;
        public XSetDo xSetDo;
        public XWaitDi xWaitDi;
        public XStation xStation;
        public XSetting xSetting;
        public bool m_isStationActive = false;
        public bool m_bPauseFinished = false;
        public bool IsAutuRunNeedHome = false;
        public double Z_Safe { get; set; }
        public int Z_PositionAxisIdIndex { get; set; } = -1;
        public string SetStepStr { get; set; }

        // ---------- 公共事件 ----------
        public event Action<string, Color> OnStep;
        public event Action<int, int> OnCount;
        public event Action<string, double> MillisecondsCount;
        public event Action<int> UploadAlarmAction;
        public event Action<XSetting> SettingChanged;

        public static Action<string> LogOutPut;

        public static event OnLogRaised LogRaised;

        public static event OnWarningChangedTaskDelegate OnWarningTaskChanged;
        // ---------- 公共属性 ----------
        public bool IsSubTask { get; set; }
        public string LogPath { get; set; }
        public bool TaskHomeOK { get; set; }
        // ► 带料初始化控制属性 ◄
        public bool IsMaterialInitialization { get; set; } = false;
        public XStation Station
        {
            get { return XStationManager.Instance.FindStationById(stationId); }
        }
        public XSetting Setting
        {
            get { return xSetting; }
            set
            {
                xSetting = value;
                if (SettingChanged != null)
                {
                    SettingChanged(this.xSetting);
                }
            }
        }
        public int StationId
        {
            get { return this.stationId; }
            set
            {
                this.stationId = value;
                xStation = XStationManager.Instance.FindStationById(stationId);
                xMove = new XMove(xStation);
                xSetDo = new XSetDo(xStation);
                xWaitDi = new XWaitDi(xStation);
            }
        }
        public bool IsStationActive
        {
            get
            {
                return m_isStationActive;
            }
            set
            {
                m_isStationActive = value;
            }
        }
        // ---------- 设备管理 ----------
        private Dictionary<int, IAxis> axisMap = new Dictionary<int, IAxis>();
        private Dictionary<int, IDigitalOutput> doMap = new Dictionary<int, IDigitalOutput>();
        private Dictionary<int, IDigitalInput> diMap = new Dictionary<int, IDigitalInput>();
        public Dictionary<int, IAxis> AxisMap
        {
            get { return this.axisMap; }
        }
        public Dictionary<int, IDigitalOutput> DoMap
        {
            get { return this.doMap; }
        }

        public Dictionary<int, IDigitalInput> DiMap
        {
            get { return this.diMap; }
        }
        public Dictionary<int, IAxis> PositionTableAxisMap
        {
            get { return this.positionTableAxisMap; }
        }
        protected Dictionary<int, IAxis> positionTableAxisMap = new Dictionary<int, IAxis>();
        // ---------- 抽象方法 ----------
        protected abstract void ExecuteHoming();
        protected abstract void InitProcessVar();
        // ---------- 设备注册 ----------
        //protected abstract void RegisterDevice();

        //protected abstract void RegisterAxis(int setAxisId);

        #region HandleEvent

        public override int HandleEvent(XEvent xEvent)
        {
            xMove.HandleEvent(xEvent);
            xSetDo.HandleEvent(xEvent);
            xWaitDi.HandleEvent(xEvent);

            return 0;
        }
        // 显式实现ITask接口的HandleEvent方法
        int ITask.HandleEvent(IEvent xEvent)
        {
            // 将IEvent转换为XEvent
            if (xEvent is XEvent xEvt)
            {
                return HandleEvent(xEvt);
            }
            return -1; // 处理失败
        }
        #endregion

        // ---------- 公共方法 ----------
        // 初始化参数
        public void InitializeParameters()
        {
            // 生成参数唯一标识符（通常使用类名）
            string identifier = this.GetType().Name;
            try
            {
                // 使用新接口加载参数
                Parameters = _parameterStorage.Load<TParams>(identifier);
                // 如果参数文件不存在或加载失败，创建新参数
                if (Parameters == null)
                {
                    Parameters = new TParams();

                    // 订阅参数变更事件
                    Parameters.PropertyChanged += (s, e) => SaveParameters();

                    // 保存初始参数
                    SaveParameters();
                }
                else
                {
                    // 订阅参数变更事件
                    Parameters.PropertyChanged += (s, e) => SaveParameters();
                }
            }
            catch (Exception ex)
            {
                // 参数加载失败时创建新参数
                Parameters = new TParams();
                Parameters.PropertyChanged += (s, e) => SaveParameters();

                string message = $"参数加载失败，已使用默认值: {ex.Message}";
                LogOutputHandleAsync(message);
            }
        }

        // 保存参数
        public void SaveParameters()
        {
            if (Parameters == null) return;
            try
            {
                // 生成参数唯一标识符
                string identifier = this.GetType().Name;

                // 使用新接口保存参数
                _parameterStorage.Save(identifier, Parameters);
            }
            catch (Exception ex)
            {
                string message = $"参数保存失败: {ex.Message}";
                LogOutputHandleAsync(message);
            }
        }
        /// <summary>
        /// 启动，调用Running
        /// </summary>
        public void Start(object runMode)
        {
            _cts?.Cancel();

            _thread = new Thread(new ParameterizedThreadStart(Running));
            _thread.IsBackground = true;
            _thread.Start(runMode);
        }
        /// <summary>
        /// 复位，调用Homing
        /// </summary>
        public void Reset()
        {
            IsAutuRunNeedHome = false;
            _cts = new CancellationTokenSource();
            _thread = new Thread(() => Homing(_cts.Token));
            _thread.IsBackground = true;
            _thread.Start();
        }

        /// <summary>
        /// 任务线程取消
        /// </summary>
        public void Cancel()
        {
            TaskStop();
            _cts?.Cancel();
        }
        public void Pause()
        {
           OnPaused();
        }
        public void Continue()
        {
            OnResumed();
        }
        // ---------- 设备操作方法 ----------
        #region 位置参数
        /// <summary>
        /// 获取位置数据存储目录
        /// </summary>
        private string GetPositionDataDirectory()
        {
            return Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Config",
                "Position");
        }
        /// <summary>
        /// 获取当前配方名称 - 虚方法，派生类可以重写
        /// </summary>
        protected virtual string GetCurrentRecipeName()
        {
            // 默认实现，派生类应该重写这个方法
            return "DefaultRecipe";
        }
        /// <summary>
        /// 加载位置数据 - 包含配方名称
        /// </summary>
        public PositionData LoadPositionData()
        {
            try
            {
                // 使用配方特定的标识符
                string identifier = $"Position_Task_{TaskId}_Recipe_{GetCurrentRecipeName()}";
                string positionDirectory = GetPositionDataDirectory();

                var positionData = _parameterStorage.Load<PositionData>(identifier, positionDirectory);

                // 如果配方特定的数据不存在，尝试加载默认数据
                if (positionData == null)
                {
                    string defaultIdentifier = $"Position_Task_{TaskId}";
                    positionData = _parameterStorage.Load<PositionData>(defaultIdentifier, positionDirectory);

                    if (positionData != null)
                    {
                        // 将默认数据迁移到当前配方
                        SavePositionData(positionData);
                        string message = $"将默认位置数据迁移到配方 '{GetCurrentRecipeName()}'";
                        LogOutputHandleAsync(message);
                    }
                }

                if (positionData == null)
                {
                    return new PositionData
                    {
                        AxisIds = Array.Empty<int>(),
                        Positions = new Dictionary<string, PositionInfo>()
                    };
                }

                return positionData;
            }
            catch (Exception ex)
            {
                LogOutputHandleAsync($"加载位置数据失败: {ex.Message}");
                return new PositionData
                {
                    AxisIds = Array.Empty<int>(),
                    Positions = new Dictionary<string, PositionInfo>()
                };
            }
        }

        /// <summary>
        /// 保存位置数据 - 包含配方名称
        /// </summary>
        public void SavePositionData(PositionData positionData)
        {
            try
            {
                // 使用配方特定的标识符
                string identifier = $"Position_Task_{TaskId}_Recipe_{GetCurrentRecipeName()}";
                string positionDirectory = GetPositionDataDirectory();

                // 设置配方名称到位置数据
                positionData.RecipeName = GetCurrentRecipeName();

                _parameterStorage.Save(identifier, positionData, positionDirectory);

                string message = $"位置数据已保存到配方 '{GetCurrentRecipeName()}'";
                LogOutputHandleAsync(message);
            }
            catch (Exception ex)
            {
                LogOutputHandleAsync($"保存位置数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取示教位置
        /// </summary>
        public double GetPosition(int axisId, string pointName)
        {
            try
            {
                var positionData = LoadPositionData();
                if (positionData == null || !positionData.Positions.ContainsKey(pointName))
                {
                    ReportAlarm(XAlarmLevel.TIP, -1, (int)AlarmCategory.SYSTEM, AlarmCategory.SYSTEM.ToString(),
                               $"TaskId:{TaskId} 配置文件中不存在点位：{pointName}");
                    return -1;
                }

                var positionInfo = positionData.Positions[pointName];
                int index = Array.IndexOf(positionData.AxisIds, axisId);

                if (index < 0 || index >= positionInfo.Coordinates.Length)
                {
                    ReportAlarm(XAlarmLevel.TIP, -1, (int)AlarmCategory.SYSTEM, AlarmCategory.SYSTEM.ToString(),
                               $"点位 {pointName} 中不包含轴 {axisId}");
                    return -1;
                }

                return positionInfo.Coordinates[index];
            }
            catch (Exception ex)
            {
                LogOutputHandleAsync($"获取点位失败: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 获取完整点位信息
        /// </summary>
        public XPosition GetPosition(string positionName)
        {
            try
            {
                var positionData = LoadPositionData();
                if (positionData == null || !positionData.Positions.ContainsKey(positionName))
                {
                    return null;
                }

                var positionInfo = positionData.Positions[positionName];
                return new XPosition(positionData.AxisIds, positionInfo.Coordinates, positionData.AxisIds.Length)
                {
                    Name = positionInfo.Comment
                };
            }
            catch (Exception ex)
            {
                LogOutputHandleAsync($"{ex.Message}\nStackTrace:{ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// 检查点位是否存在
        /// </summary>
        public bool PositionExists(string positionName)
        {
            var positionData = LoadPositionData();
            return positionData?.Positions.ContainsKey(positionName) == true;
        }
        #endregion

        #region Device

        public void RegisterAxis(int axisSetId, bool IsShownInPositionTable = true)
        {
            if (axisMap.ContainsKey(axisSetId))
            {
                return;
            }
            axisMap.Add(axisSetId, XDevice.Instance.FindAxisById(axisSetId));
            XDevice.Instance.FindAxisById(axisSetId).TaskId = TaskId;

            if (IsShownInPositionTable)
            {
                if (positionTableAxisMap.ContainsKey(axisSetId))
                {
                    return;
                }
                positionTableAxisMap.Add(axisSetId, XDevice.Instance.FindAxisById(axisSetId));
            }
        }
        public void RegisterDo(int doSetId)
        {
            if (doMap.ContainsKey(doSetId))
            {
                return;
            }
            doMap.Add(doSetId, XDevice.Instance.FindDoById(doSetId));
            XDevice.Instance.FindDoById(doSetId).TaskId = TaskId;
        }
        public void RegisterDi(int diSetId)
        {
            if (diMap.ContainsKey(diSetId))
            {
                return;
            }
            diMap.Add(diSetId, XDevice.Instance.FindDiById(diSetId));
            XDevice.Instance.FindDiById(diSetId).TaskId = TaskId;
        }
        #endregion

        #region 任务所在工站的相应操作

        /// <summary>
        /// 设置工站的状态：等待运行，复位完成后调用
        /// 对应用户控件XStationStateBar显示
        /// </summary>
        protected void SetStation_StateWaitRun()
        {
            xStation.SetState(XStationState.WAITRUN);
        }
        protected void SetStation_StateWAITRESET()
        {
            xStation.SetState(XStationState.WAITRESET);
        }
        protected void SetStation_StateCLEARAWAY()
        {
            xStation.SetState(XStationState.CLEAR);
        }
        protected void SetStation_StateSTOP()
        {
            xStation.SetState(XStationState.STOP);
        }
        #region 秒表，用以记录Task所在Station的CycleTime，开始和停止需用户操作
        /// <summary>
        /// 秒表开始
        /// </summary>
        protected void SetStation_StopWatchStart()
        {
            xStation.StopWatch_Start();
        }
        /// <summary>
        /// 秒表复位
        /// </summary>
        protected void SetStation_StopWatchReset()
        {
            xStation.StopWatch_Reset();
        }
        /// <summary>
        /// 秒表停止
        /// </summary>
        protected void SetStation_StopWatchStop()
        {
            xStation.StopWatch_Stop();
        }
        /// <summary>
        /// 秒表重启动
        /// </summary>
        protected void SetStation_StopWatchRestart()
        {
            xStation.StopWatch_ReStart();
        }
        /// <summary>
        /// 获取秒表开始后的毫秒数
        /// </summary>
        /// <returns></returns>
        protected double GetStation_StopWatchElapsedMilliseconds()
        {
            return xStation.ElapsedMilliseconds;
        }

        #endregion


        protected bool m_bStationActive = false;

        protected bool CheckIsActived()
        {
            if (xStation.State == XStationState.RUNNING || xStation.State == XStationState.PAUSE || xStation.State == XStationState.TIP)
            {
                m_isStationActive = true;
            }
            else
            {
                m_isStationActive = false;
            }
            return m_isStationActive;
        }
        protected void CheckIsPause()
        {
            while (xStation.State == XStationState.PAUSE)
            {
                Thread.Sleep(100);
            }
        }
        protected void CheckCycleStop()
        {
            if (xStation.State == XStationState.ALARM || xStation.State == XStationState.ESTOP || xStation.State == XStationState.STOP)
            {
                IsStopped = true;
                return;
            }
            IsStopped = false;
        }
        #endregion

        #region 发送告警

        private XAlarmEventArgs _alarmEventArgs = new XAlarmEventArgs(0, Int32.MinValue, "NONE", "NONE");
        /// <summary>
        /// 发送告警
        /// </summary>
        /// <param name="alarmLevel">报警等级</param>
        /// <param name="alarmCode">报警代号</param>
        /// <param name="append">用户自定义的额外报警信息</param>
        private void PostAlarm(XAlarmLevel alarmLevel, XAlarmEventArgs args, string append = "")
        {
            PostEvent(xStation, alarmLevel, args, append);
        }
        //ReportAlarm(XAlarmLevel.PAUSE, (int)AlarmCode.获取前站数据失败, AlarmCategory.TRAY.ToString(), 
        //                            AlarmCode.获取前站数据失败.ToString(), "PAM1");
        public void ReportAlarm(XAlarmLevel level, int intvalue, int code, string category, string description, string append = "")
        {
            _alarmEventArgs = new XAlarmEventArgs(intvalue, Int32.MinValue, "NONE", "NONE");
            _alarmEventArgs.Code = code;
            _alarmEventArgs.IntValue = intvalue;
            _alarmEventArgs.Category = category;
            _alarmEventArgs.Description = description;
            PostAlarm(level, _alarmEventArgs, append);
        }
        protected void ClearAlarm()
        {
            //if (this.stationId == XAlarmReporter.Instance.CurrentAlarm.StationId)
            {
                XAlarmReporter.Instance.ClearAlarm();
            }
        }

        #endregion

        #region 轴操作
        /// <summary>
        /// 多轴使能
        /// </summary>
        /// <param name="axisId"></param>
        /// <param name="sts"></param>
        /// <returns></returns>
        protected int SetServo(int[] axisId, bool sts)
        {
            int ret;
            for (int i = 0; i < axisId.Length; i++)
            {
                ret = XDevice.Instance.FindAxisById(axisId[i]).SetServo(sts);
                if (ret != 0)
                {
                    return -1;
                }
                Thread.Sleep(500);
            }
            return 0;

        }
        /// <summary>
        /// 单轴使能
        /// </summary>
        /// <param name="axisId"></param>
        /// <param name="sts"></param>
        /// <returns></returns>
        protected int SetServo(int axisId, bool sts)
        {
            return XDevice.Instance.FindAxisById(axisId).SetServo(sts);
        }
        /// <summary>
        /// 多轴回零
        /// </summary>
        /// <param name="axisId"></param>
        /// <returns></returns>
        protected int MoveHome(int[] axisId)
        {
            return xMove.MoveHome(axisId, axisId.Length);
        }
        /// <summary>
        /// 单轴回零
        /// </summary>
        /// <param name="axisId"></param>
        /// <returns></returns>
        protected int MoveHome(int axisId)
        {
            return xMove.MoveHome(new int[] { axisId }, 1);
        }
        /// <summary>
        /// 多轴绝对运动，不同速度
        /// </summary>
        /// <param name="axisId"></param>
        /// <param name="pos"></param>
        /// <param name="vel"></param>
        /// <returns></returns>
        protected int MoveAbs(int[] axisId, double[] pos, double[] vel, bool checkLmt = true)
        {
            return xMove.MoveAbs(axisId, pos, vel, axisId.Length, checkLmt);
        }
        /// <summary>
        /// 单轴绝对运动
        /// </summary>
        /// <param name="axisId"></param>
        /// <param name="pos"></param>
        /// <param name="vel"></param>
        /// <returns></returns>
        protected int MoveAbs(int axisId, double pos, double vel, bool checkLmt = true)
        {
            return xMove.MoveAbs(new int[] { axisId }, new double[] { pos }, new double[] { vel }, 1, checkLmt);
        }
        /// <summary>
        /// 多轴绝对运动，相同速度
        /// </summary>
        /// <param name="axisId"></param>
        /// <param name="pos"></param>
        /// <param name="vel"></param>
        /// <returns></returns>
        protected int MoveAbs(int[] axisId, double[] pos, double vel, bool checkLmt = true)
        {
            double[] vels = new double[axisId.Length];
            for (int i = 0; i < axisId.Length; i++)
            {
                vels[i] = vel;
            }
            return xMove.MoveAbs(axisId, pos, vels, axisId.Length, checkLmt);
        }
        /// <summary>
        /// XY 轴直线插补
        /// </summary>
        /// <param name="actCardId">卡号</param>
        /// <param name="coordId">坐标系号</param>
        /// <param name="axisId">轴号数组</param>
        /// <param name="pos">位置数组</param>
        /// <param name="vel">速度</param>
        /// <returns></returns>
        protected int MoveLineAbs(ushort actCardId, int coordId, int[] axisId, double[] pos, double vel)
        {
            return xMove.MoveLineAbs(actCardId, coordId, axisId, pos, vel);
        }
        /// <summary>
        /// 多轴根据示教点位运动，不同速度
        /// </summary>
        /// <param name="position"></param>
        /// <param name="vel"></param>
        /// <returns></returns>
        protected int MovePosition(XPosition position, double[] vel, bool checkLmt = true)
        {
            int[] axisId = position.AxisId;
            double[] pos = position.Positions;
            return xMove.MoveAbs(axisId, pos, vel, position.Count, checkLmt);
        }
        /// <summary>
        /// 多轴根据示教点位运动，相同速度
        /// </summary>
        /// <param name="position"></param>
        /// <param name="vel"></param>
        /// <returns></returns>
        protected int MovePosition(XPosition position, double vel, bool checkLmt = true)
        {
            int[] axisId = position.AxisId;
            double[] pos = position.Positions;
            int count = position.Count;
            double[] vels = new double[count];
            for (int i = 0; i < count; i++)
            {
                vels[i] = vel;
            }
            return xMove.MoveAbs(axisId, pos, vels, count, checkLmt);
        }
        protected int SetHomeMode(int axisId, int homeMode)
        {
            return XDevice.Instance.FindAxisById((ushort)axisId).SetHomeMode(homeMode);
        }
        protected int ClearPosition(int axisId, int position)
        {
            return XDevice.Instance.FindAxisById((ushort)axisId).ClearPosition();
        }
        protected double GetPosition(int axisId, bool IsCmd)
        {
            return IsCmd ? XDevice.Instance.FindAxisById((ushort)axisId).CommandPOS : XDevice.Instance.FindAxisById((ushort)axisId).POS;
        }

        protected int CleanALM(int axisId)
        {
            return XDevice.Instance.FindAxisById((ushort)axisId).CleanALM();
        }

        protected bool CheckMoveDone(ushort actCardId, ushort axisId)
        {
            return XDevice.Instance.FindCardById(actCardId).CheckMoveDone(axisId) == 0 ? true : false;
        }
        /// <summary>
        /// 多轴相对运动，不同速度
        /// </summary>
        /// <param name="axisId"></param>
        /// <param name="distance"></param>
        /// <param name="vel"></param>
        /// <returns></returns>
        protected int MoveRel(int[] axisId, double[] distance, double[] vel, bool checkLmt = true)
        {
            return xMove.MoveRel(axisId, distance, vel, axisId.Length, checkLmt);
        }
        /// <summary>
        /// 单轴相对运动
        /// </summary>
        /// <param name="axisId"></param>
        /// <param name="distance"></param>
        /// <param name="vel"></param>
        /// <returns></returns>
        protected int MoveRel(int axisId, double distance, double vel, bool checkLmt = true)
        {
            return xMove.MoveRel(new int[] { axisId }, new double[] { distance }, new double[] { vel }, 1, checkLmt);
        }
        /// <summary>
        /// 单轴JOG运动
        /// </summary>
        /// <param name="axisId"></param>
        /// <param name="distance"></param>
        /// <param name="vel"></param>
        /// <returns></returns>
        protected int MoveJog(int axisId, int isStart, bool checkLmt = true)
        {
            return xMove.MoveJog(new int[] { axisId }, new int[] { isStart }, 1, checkLmt);
        }
        /// <summary>
        /// 多轴相对运动，相同速度
        /// </summary>
        /// <param name="axisId"></param>
        /// <param name="distance"></param>
        /// <param name="vel"></param>
        /// <returns></returns>
        protected int MoveRel(int[] axisId, double[] distance, double vel, bool checkLmt = true)
        {
            double[] vels = new double[axisId.Length];
            for (int i = 0; i < axisId.Length; i++)
            {
                vels[i] = vel;
            }
            return xMove.MoveRel(axisId, distance, vels, axisId.Length, checkLmt);
        }

        /// <summary>
        /// 运动停止
        /// </summary>
        /// <returns></returns>
        protected int MoveStop()
        {
            return xMove.MoveStop();
        }
        protected int MoveEStop()
        {
            return xMove.MoveEStop();
        }
        /// <summary>
        /// 判断运动是否完成，每次运动时必须调用
        /// </summary>
        /// <returns></returns>
        protected bool WaitMoveDone(int timeout = -1)
        {
            return xMove.WaitEvent(timeout) == 0 ? true : false;
        }

        protected int SetAxisAccAndDec(int axisId, double dMinVel, double dMaxVel, double acc, double dec, double dStopVel)
        {
            return XDevice.Instance.FindAxisById(axisId).SetAxisAccAndDec(dMinVel, dMaxVel, acc, dec, dStopVel);
        }
        protected int SetAxisAccAndDec(int axisId, double dMinVel, double dVel, double acc, double dec, double dStopVel, double sPara)
        {
            return XDevice.Instance.FindAxisById(axisId).SetAxisAccAndDec(dMinVel, dVel, acc, dec, dStopVel, sPara);
        }
        protected int SetStopDec(int axisId, double dec)
        {
            return XDevice.Instance.FindAxisById(axisId).SetStopDec(dec);
        }
        protected int SetAxisJogAccAndDec(int axisId, double acc, double dec, double vel)
        {
            return XDevice.Instance.FindAxisById(axisId).SetAxisJogParam(acc, dec, vel);
        }

        protected bool SetLimit(int actCardId, int axisId, int pos)
        {
            return XDevice.Instance.FindCardById(actCardId).SetLimit(axisId, pos);
        }
        #endregion

        #region Do操作

        /// <summary>
        /// 设置多个Do状态，不同状态
        /// </summary>
        /// <param name="doId"></param>
        /// <param name="doStsType"></param>
        /// <returns></returns>
        public int SetDo(int[] doId, DOSTSTYPE[] doStsType)
        {
            return xSetDo.SetDo(doId, doStsType, doId.Length);
        }
        /// <summary>
        /// 设置单个Do状态
        /// </summary>
        /// <param name="doId"></param>
        /// <param name="doStsType"></param>
        /// <returns></returns>
        public int SetDo(int doId, DOSTSTYPE doStsType)
        {
            return xSetDo.SetDo(new int[] { doId }, new DOSTSTYPE[] { doStsType }, 1);
        }
        /// <summary>
        /// 设置多个Do状态，相同状态
        /// </summary>
        /// <param name="doId"></param>
        /// <param name="doStsType"></param>
        /// <returns></returns>
        public int SetDo(int[] doId, DOSTSTYPE doStsType)
        {
            DOSTSTYPE[] types = new DOSTSTYPE[doId.Length];
            for (int i = 0; i < doId.Length; i++)
            {
                types[i] = doStsType;
            }
            return xSetDo.SetDo(doId, types, doId.Length);
        }

        #endregion

        #region Di操作

        public bool GetDi(int diId)
        {
            return XDevice.Instance.FindDiById(diId).STS;
        }

        /// <summary>
        /// 等待多个Di信号，不同状态，超时后暂停或停止 false为超时
        /// </summary>
        /// <param name="diId"></param>
        /// <param name="diStsType"></param>
        /// <param name="timeout">超时时间，单位毫秒</param>
        /// <param name="timeoutToBeContinue">若为true，超时后停止，必须复位；若为false，超时后暂停，可继续</param>
        /// <returns></returns>
        public bool WaitDi(int[] diId, DISTSTYPE[] diStsType, int timeout, bool timeoutToBeContinue = false, string append = "")
        {
            return xWaitDi.WaitDi(diId, diStsType, diId.Length, timeout, timeoutToBeContinue, append) == 0 ? true : false;
        }
        /// <summary>
        /// 等待多个Di信号，超时后暂停或停止
        /// </summary>
        /// <param name="diId"></param>
        /// <param name="diStsType"></param>
        /// <param name="timeout">超时时间，单位毫秒</param>
        /// <param name="timeoutToBeContinue">若为false，超时后停止，必须复位；若为true，超时后暂停，可继续</param>
        /// <returns></returns>
        public bool WaitDi(int diId, DISTSTYPE diStsType, int timeout, bool timeoutToBeContinue = false, string append = "")
        {
            return xWaitDi.WaitDi(new int[] { diId }, new DISTSTYPE[] { diStsType }, 1, timeout, timeoutToBeContinue, append) == 0 ? true : false;
        }
        /// <summary>
        /// 等待多个Di信号，相同状态，超时后暂停或停止
        /// </summary>
        /// <param name="diId"></param>
        /// <param name="diStsType"></param>
        /// <param name="timeout">超时时间，单位毫秒</param>
        /// <param name="timeoutToBeContinue">若为true，超时后停止，必须复位；若为false，超时后暂停，可继续</param>
        /// <returns></returns>
        public bool WaitDi(int[] diId, DISTSTYPE diStsType, int timeout, bool timeoutToBeContinue = false, string append = "")
        {
            DISTSTYPE[] types = new DISTSTYPE[diId.Length];
            for (int i = 0; i < diId.Length; i++)
            {
                types[i] = diStsType;
            }
            return xWaitDi.WaitDi(diId, types, diId.Length, timeout, timeoutToBeContinue, append) == 0 ? true : false;
        }
        /// <summary>
        /// 等待多个Di信号，不同状态，超时后无动作
        /// </summary>
        /// <param name="diId"></param>
        /// <param name="diStsType"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public bool WaitDiSignal(int[] diId, DISTSTYPE[] diStsType, int timeout)
        {
            return xWaitDi.WaitDiSignal(diId, diStsType, diId.Length, timeout) == 0 ? true : false;
        }
        /// <summary>
        /// 等待单个Di信号，超时后无动作
        /// </summary>
        /// <param name="diId"></param>
        /// <param name="diStsType"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public bool WaitDiSignal(int diId, DISTSTYPE diStsType, int timeout)
        {
            return xWaitDi.WaitDiSignal(new int[] { diId }, new DISTSTYPE[] { diStsType }, 1, timeout) == 0 ? true : false;
        }
        /// <summary>
        /// 等待多个Di信号，相同状态，超时后无动作
        /// </summary>
        /// <param name="diId"></param>
        /// <param name="diStsType"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public bool WaitDiSignal(int[] diId, DISTSTYPE diStsType, int timeout)
        {
            DISTSTYPE[] types = new DISTSTYPE[diId.Length];
            for (int i = 0; i < diId.Length; i++)
            {
                types[i] = diStsType;
            }
            return xWaitDi.WaitDiSignal(diId, types, diId.Length, timeout) == 0 ? true : false;
        }
        /// <summary>
        /// 延时通断定时器，检测Di信号是否持续，满足则返回true
        /// </summary>
        /// <param name="diSts"></param>
        /// <param name="LastCheckTime"></param>
        /// <returns></returns>
        public bool CheckDiSts(bool diSts, int LastCheckTime = 1)
        {
            return xWaitDi.CheckDiSts(diSts, LastCheckTime) == 0 ? true : false;
        }

        #endregion

        #region 启动点表
        protected int APS_pt_start(int[] axisId, int axisCount, int ptbId, int ptbCount)
        {
            return xMove.APS_pt_start(axisId, axisCount, ptbId, ptbCount);
        }
        #endregion

        #region 逻辑功能

        /// <summary>
        /// 执行动作命令函数
        /// </summary>
        /// <param name="func">具体动作函数</param>
        protected virtual void DoCommand(Action act)
        {
            //执行动作
            act?.Invoke();
        }
        /// <summary>
        /// 异步执行动作
        /// </summary>
        /// <param name="act"></param>
        protected virtual async void RunThread(Action act)
        {
            await Task.Run(act);
        }
        /// <summary>
        /// 执行动作命令函数
        /// </summary>
        /// <param name="func">具体动作函数</param>
        protected virtual int DoCommand(Func<int> func)
        {
            //执行动作
            int ret = func.Invoke();
            return ret;
        }
        /// <summary>
        /// 执行动作命令函数
        /// </summary>
        /// <param name="act">具体动作函数</param>
        protected virtual int DoCommand<T>(Func<T, int> act, T obj)
        {
            //执行动作
            int ret = act.Invoke(obj);
            return ret;
        }
        /// <summary>
        /// 延时函数
        /// </summary>
        /// <param name="timeSlice">延时时间片（单位：ms）</param>
        /// <param name="updateInter">扫描精度</param>
        protected void Delay(double timeSlice, int updateInter = 1)
        {
            HighPerformanceStopWatch sw = new HighPerformanceStopWatch();
            double st = sw.SlicedTime;
            while (st <= timeSlice)
            {
                st = sw.SlicedTime;
                Thread.Sleep(updateInter);
            }
        }
        /// <summary>
        /// 等待条件满足（异步友好版本）
        /// </summary>
        /// <param name="checkFunc">检查函数</param>
        /// <param name="timeoutMs">超时时间（毫秒），-1表示无限等待</param>
        /// <param name="checkIntervalMs">检查间隔（毫秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>条件是否满足</returns>
        protected bool WaitUntil(Func<bool> checkFunc, double timeoutMs = -1, int checkIntervalMs = 1, CancellationToken cancellationToken = default)
        {
            if (checkFunc == null)
                throw new ArgumentNullException(nameof(checkFunc));

            var sw = new HighPerformanceStopWatch();
            double elapsed = 0;

            if (timeoutMs < 0)
            {
                // 无限等待模式
                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;

                    if (checkFunc())
                        return true;

                    Thread.Sleep(checkIntervalMs);
                }
            }
            else
            {
                // 有限超时模式
                while (elapsed <= timeoutMs)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;

                    if (checkFunc())
                        return true;

                    Thread.Sleep(checkIntervalMs);
                    elapsed = sw.SlicedTime;
                }

                return false;
            }
        }
        /// <summary>
        /// 异步等待条件满足
        /// </summary>
        /// <param name="checkFunc">检查函数</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <param name="checkIntervalMs">检查间隔（毫秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>条件是否满足</returns>
        protected async Task<bool> WaitUntilAsync(Func<bool> checkFunc, double timeoutMs = -1, int checkIntervalMs = 1, CancellationToken cancellationToken = default)
        {
            if (checkFunc == null)
                throw new ArgumentNullException(nameof(checkFunc));

            var sw = new HighPerformanceStopWatch();
            double elapsed = 0;

            if (timeoutMs < 0)
            {
                // 无限等待模式
                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;

                    if (checkFunc())
                        return true;

                    await Task.Delay(checkIntervalMs, cancellationToken);
                }
            }
            else
            {
                // 有限超时模式
                while (elapsed <= timeoutMs)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;

                    if (checkFunc())
                        return true;

                    await Task.Delay(checkIntervalMs, cancellationToken);
                    elapsed = sw.SlicedTime;
                }

                return false;
            }
        }

        /// <summary>
        /// 读取数字输入信号（带滤波功能,常开新号）
        /// </summary>
        /// <param name="cardId">运动控制卡ID</param>
        /// <param name="ioIndex">IO点位索引</param>
        /// <param name="filterCount">滤波次数（连续读取多少次相同的值才认为有效）</param>
        /// <param name="pollIntervalMs">每次读取间隔(ms)</param>
        /// <returns>true-有信号(物理信号为0), false-无信号(物理信号为1)</returns>
        public bool ReadDigitalInputWithFilter(int cardId, int ioIndex, int filterCount = 3, int pollIntervalMs = 10)
        {
            if (filterCount < 1) filterCount = 1;
            if (pollIntervalMs < 0) pollIntervalMs = 0;

            int consistentReadings = 0;
            bool? lastValue = null;

            while (consistentReadings < filterCount)
            {
                // 读取原始IO信号（0-有信号，1-无信号）
                short rawValue = LTDMC.dmc_read_inbit((ushort)cardId, (ushort)ioIndex);
                bool currentValue = (rawValue == 0);

                // 信号状态变化检测
                if (lastValue.HasValue && currentValue != lastValue)
                {
                    consistentReadings = 0; // 状态变化，重置计数器
                }

                lastValue = currentValue;
                consistentReadings++;

                if (consistentReadings < filterCount)
                {
                    Thread.Sleep(pollIntervalMs);
                }
            }

            return lastValue.Value;
        }
        /// <summary>
        /// 读取数字输入信号（带滤波功能,常开新号）
        /// </summary>
        /// <param name="cardId">运动控制卡ID</param>
        /// <param name="ioIndex">IO点位索引数组</param>
        /// <param name="filterCount">滤波次数（连续读取多少次相同的值才认为有效）</param>
        /// <param name="pollIntervalMs">每次读取间隔(ms)</param>
        /// <returns>true-有信号(物理信号为0), false-无信号(物理信号为1)</returns>
        public bool ReadDigitalInputWithFilter(int cardId, int[] ioIndex, int filterCount = 3, int pollIntervalMs = 10)
        {
            if (filterCount < 1) filterCount = 1;
            if (pollIntervalMs < 0) pollIntervalMs = 0;

            int consistentReadings = 0;
            bool? lastValue = null;

            while (consistentReadings < filterCount)
            {
                // 读取原始IO信号（0-有信号，1-无信号）
                short rawValue = 1;
                for (int i = 0; i < ioIndex.Length; i++)
                {
                    rawValue = LTDMC.dmc_read_inbit((ushort)cardId, (ushort)ioIndex[i]);
                    if (rawValue == 1)
                        break;
                }
                bool currentValue = (rawValue == 0);

                // 信号状态变化检测
                if (lastValue.HasValue && currentValue != lastValue)
                {
                    consistentReadings = 0; // 状态变化，重置计数器
                }

                lastValue = currentValue;
                consistentReadings++;

                if (consistentReadings < filterCount)
                {
                    Thread.Sleep(pollIntervalMs);
                }
            }

            return lastValue.Value;
        }
        /// <summary>
        /// 读取数字输入信号（带滤波功能,常闭新号）
        /// </summary>
        /// <param name="cardId">运动控制卡ID</param>
        /// <param name="ioIndex">IO点位索引</param>
        /// <param name="filterCount">滤波次数（连续读取多少次相同的值才认为有效）</param>
        /// <param name="pollIntervalMs">每次读取间隔(ms)</param>
        /// <returns>true-有信号(物理信号为1), false-无信号(物理信号为0)</returns>
        public bool ReadDigitalInputWithFilter2(int cardId, int ioIndex, int filterCount = 3, int pollIntervalMs = 10)
        {
            if (filterCount < 1) filterCount = 1;
            if (pollIntervalMs < 0) pollIntervalMs = 0;

            int consistentReadings = 0;
            bool? lastValue = null;

            while (consistentReadings < filterCount)
            {
                // 读取原始IO信号（1-有信号，0-无信号）
                short rawValue = LTDMC.dmc_read_inbit((ushort)cardId, (ushort)ioIndex);
                bool currentValue = (rawValue == 1);

                // 信号状态变化检测
                if (lastValue.HasValue && currentValue != lastValue)
                {
                    consistentReadings = 0; // 状态变化，重置计数器
                }

                lastValue = currentValue;
                consistentReadings++;

                if (consistentReadings < filterCount)
                {
                    Thread.Sleep(pollIntervalMs);
                }
            }

            return lastValue.Value;
        }
        /// <summary>
        /// 读取数字输入信号（带滤波功能,常闭新号）
        /// </summary>
        /// <param name="cardId">运动控制卡ID</param>
        /// <param name="ioIndex">IO点位索引</param>
        /// <param name="filterCount">滤波次数（连续读取多少次相同的值才认为有效）</param>
        /// <param name="pollIntervalMs">每次读取间隔(ms)</param>
        /// <returns>true-有信号(物理信号为1), false-无信号(物理信号为0)</returns>
        public bool ReadDigitalInputWithFilter2(int cardId, int[] ioIndex, int filterCount = 3, int pollIntervalMs = 10)
        {
            if (filterCount < 1) filterCount = 1;
            if (pollIntervalMs < 0) pollIntervalMs = 0;

            int consistentReadings = 0;
            bool? lastValue = null;

            while (consistentReadings < filterCount)
            {
                // 读取原始IO信号（1-有信号，0-无信号）
                short rawValue = 0;
                for (int i = 0; i < ioIndex.Length; i++)
                {
                    rawValue = LTDMC.dmc_read_inbit((ushort)cardId, (ushort)ioIndex[i]);
                    if (rawValue == 1)
                        break;
                }
                bool currentValue = (rawValue == 1);

                // 信号状态变化检测
                if (lastValue.HasValue && currentValue != lastValue)
                {
                    consistentReadings = 0; // 状态变化，重置计数器
                }

                lastValue = currentValue;
                consistentReadings++;

                if (consistentReadings < filterCount)
                {
                    Thread.Sleep(pollIntervalMs);
                }
            }

            return lastValue.Value;
        }
        /// <summary>
        /// 硬件读取处IO防抖
        /// </summary>
        public bool ReadSensorWithDebounce(int cardId, int bit)
        {
            const int debounceCount = 3;
            int count = 0;
            for (int i = 0; i < debounceCount; i++)
            {
                if (LTDMC.dmc_read_inbit((ushort)cardId, (ushort)bit) == 0)
                    count++;
                Thread.Sleep(5);
            }
            return count >= debounceCount ? true : false;
        }
        #endregion

        public void SetPauseRequest()
        {
            IsPaused = true;
        }
        public void ClearPauseRequest()
        {
            IsPaused = false;
        }

        /// <summary>
        /// 任务运行，需用户重写
        /// </summary>
        protected virtual void Running(object runMode) { }

        /// <summary>
        /// 任务复位，需用户重写
        /// </summary>
        protected virtual void Homing(CancellationToken cancellation)
        {
            TaskHomeOK = false;
            //Goto(0);
        }

        protected virtual void TaskStop() { }
        protected virtual void OnPaused() { }
        protected virtual void OnResumed(){ }
        protected virtual void OnParametersChanged()
        {
            // 参数变更时的自定义逻辑
        }
        /// <summary>
        /// 任务初始化
        /// </summary>
        public virtual void Initialize()
        {

        }
        /// <summary>
        /// 任务退出
        /// </summary>
        protected virtual void Exit()
        {
            Cancel();
        }
        protected virtual void WriteLog(string message)
        {

        }
        // 设置任务ID的虚方法
        public virtual void SetTaskId(int taskId)
        {
            TaskId = taskId;
        }
        public static class MaterialColors
        {
            public static readonly Color Gray500 = Color.FromRgb(0x9E, 0x9E, 0x9E);
            public static readonly Color Red500 = Color.FromRgb(0xE5, 0x39, 0x35);
            public static readonly Color Blue500 = Color.FromRgb(0x21, 0x96, 0xF3);
        }
        private static readonly Color DefaultGray = Color.FromRgb(0x9E, 0x9E, 0x9E);
        protected void Goto(int step, string message = "", Color? color = null)
        {
            int newStep = step;

            string logMessage = $"【{Name}】 station: action [{Step}] → [{newStep}]";
            string taskMessage = $"Action [{Step}] → [{newStep}] {message}";

            LastStep = Step;
            Step = newStep;

            SetStep(taskMessage, color ?? DefaultGray);

            IMessage.Logger.Info($"{logMessage}, record history: {LastStep}");
        }

        public async void SetStep(string step, Color color, bool flag = false)
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                LogOutputHandleAsync(step);

                if (flag)
                    OnWarningTaskChanged?.Invoke("异常:", step, 1);

                SetStepStr = step;
                if (OnStep != null)
                {
                    OnStep(step, color);
                }
                // 触发全局事件
                _eventAggregator?.GetEvent<TaskStepChangedEvent>().Publish(new TaskStepChangedEventArgs
                {
                    Task = this, // 使用 this 作为 ITask 实现
                    StepMessage = step,
                    Color = color
                });
                WriteLog(step);
            });
        }
        public static async void LogOutputHandleAsync(string msg)
        {
            await System.Threading.Tasks.Task.Run(() =>
            {
                LogRaised?.Invoke(msg);
            });
        }
        public void SetCount(int currentnum, int total)
        {
            if (OnCount != null)
            {
                OnCount(currentnum, total);
            }
        }
        public void SetUploadAlarm(int alarmid)
        {
            UploadAlarmAction?.Invoke(alarmid);
        }
        public void SetElapsedMilliseconds(string currentTime, double totalMilliseconds)
        {
            if (MillisecondsCount != null)
            {
                MillisecondsCount(currentTime, totalMilliseconds);
            }
        }

        #region 错误处理
        // 添加错误状态标志
        private bool _hasShownErrorDialog;
        public bool HasShownErrorDialog
        {
            get => _hasShownErrorDialog;
            set => _hasShownErrorDialog = value;
        }

        IStation IMotionTask.Station => this.Station;

        // 错误处理委托（由派生类实现）
        protected abstract void OnErrorOccurred();

        // 公共错误处理方法
        protected virtual void HandleErrorState()
        {
            // 运行时弹出错误对话框，仅在首次进入ERROR状态时显示
            if (this.Station.State != XStationState.RUNNING ) return;
            if (!HasShownErrorDialog)
            {
                HasShownErrorDialog = true;
                var result = DialogService.ShowBlockingDialog(
                    message: $"[{Name}] 出现程序错误，不可继续\r\n",
                    title: "程序错误",
                    yesButtonText: "继续运行",
                    noButtonText: "暂停任务",
                    extraButtonText: "停止任务",
                    icon: PackIconKind.Alert,
                    showYesButton: true,
                    showNoButton: true,
                    showExtraButton: true);
                switch (result)
                {
                    case 0: 
                        IMessage.Logger.Info($"任务 [{Name}] 进入错误处理状态,用户选择继续运行");
                        break;

                    case 1: 
                        PostEvent(this.Station, XEventID.PAUSE);
                        IMessage.Logger.Info($"任务 [{Name}] 进入错误处理状态,用户选择暂停任务");
                        break;

                    case 2:
                        PostEvent(this.Station, XEventID.STOPMUSTRESET);
                        IMessage.Logger.Info($"任务 [{Name}] 进入错误处理状态,用户选择停止任务");
                        break;

                    case -1: // 对话框被关闭/取消
                        PostEvent(this.Station, XEventID.PAUSE);
                        break;
                }
            }
            // 添加日志记录
            IMessage.Logger.Error($"任务 [{Name}] 进入错误处理状态");
        }

        // 重置错误状态（当离开ERROR状态时调用）
        protected void ResetErrorState()
        {
            HasShownErrorDialog = false;
        }
        // 显示超时对话框，并返回用户操作结果
        public int ShowBlockingDialog(string message, string title, string yesMessage, string noMessage, bool isShowYesButton, bool isShowNoButton, string extraMessage = "", bool isShowExtraButton = false)
        {
            int status = -1;
            var result = DialogService.ShowBlockingDialog(
               title: title,
               message: message + "\r\n",
               yesButtonText: yesMessage,
               noButtonText: noMessage,
               extraButtonText: extraMessage,
               showExtraButton: isShowExtraButton,
               showYesButton: isShowYesButton,
               showNoButton: isShowNoButton,
               icon: PackIconKind.ClockAlert
             );
            if ((int)result == 0)
            {
                // 用户点击YES
                status = 1;
                IMessage.Logger.Info("用户YES操作");
            }
            else if ((int)result == 1)
            {
                // 用户点击NO
                IMessage.Logger.Info("用户NO操作");
                status = 2;
            }
            else if ((int)result == 2)
            {
                // 用户点击EXTRA
                IMessage.Logger.Info("用户EXTRA操作");
                status = 3;
            }
            else
            {
                // 对话框被关闭/取消
                IMessage.Logger.Info("对话框被关闭");
            }
            return status;
        }

        #endregion
    }
}

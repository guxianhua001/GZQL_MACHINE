using Core.Abstraction;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmarterMotion
{
    public delegate void OnStationStateRaised(XStationState obj);
    public class XStation : XStationEventHandler, IStation
    {
        public event Action RedLightON;
        public event Action OrangeLightON;
        public event Action GreenLightON;
        public event Action AllLightsOFF;
        public event Action PauseOn;
        public event Action ContinueOn;
        public event Action<XStationState> OnStationStateChanged;

        public event OnStationStateRaised StationStateRaised;

        // 任务列表使用接口类型 (支持泛型任务)
        private List<IMotionTask> tasks = new List<IMotionTask>();
        public List<IMotionTask> Tasks => tasks;
        private XDo redLight = null;
        private XDo orangeLight = null;
        private XDo greenLight = null;
        private XDo buzzer = null;
        private Timer m_Timer;
        private LightType m_LightType;
        private int m_LightFlashFlag;
        private bool m_LightFlash;
        private XStationState m_State;
        private XStationState m_LastState;
        private int stationId;
        private string name;
        private object m_RunMode;
        private Stopwatch m_StopWatch = new Stopwatch();
        private int currenttaskid;
        private bool m_isAllHomeOk = false;
        private bool m_isEnable = true;
        private bool m_isBuzzing = false;
        private bool m_isEnableBuzzer = false;
        private object obj = new object();
        private object objLed = new object();
        //private bool m_IsStationRuning = false;

        public XStation(int stationId, string name)
        {
            this.stationId = stationId;
            this.name = name;
            m_Timer = new Timer(new TimerCallback(Changing), null, 0, 800);
            m_LightFlash = false;
            m_LightFlashFlag = 1;
            RedLightON += new Action(OnlyRedLightOn);
            OrangeLightON += new Action(OnlyOrangeLightOn);
            GreenLightON += new Action(OnlyGreenLightOn);
            AllLightsOFF += new Action(AllLightsOff);
            m_State = XStationState.WAITRESET;
            m_LastState = XStationState.NONE;
            SetState(m_State);
            IsEnable = true;
        }

        public int StationId { get { return this.stationId; } }

        public string Name { get { return this.name; } }

        public bool IsEnable
        {
            get
            {
                return m_isEnable;
            }
            set
            {
                m_isEnable = value;
            }
        }
        public bool IsEnableBuzzer
        {
            get
            {
                return m_isEnableBuzzer;
            }
            set
            {
                m_isEnableBuzzer = value;
            }
        }
        public bool IsBuzzing
        {
            get
            {
                return m_isBuzzing;
            }
            set
            {
                m_isBuzzing = value;
            }
        }
        public XStationState State
        {
            get
            {
                lock (this)
                {
                    return m_State;
                }
            }
        }
        public bool IsAllHomeOk
        {
            get
            {
                return m_isAllHomeOk;
            }
            set
            {
                m_isAllHomeOk = value;
            }
        }

        public int TaskCouts
        {
            get { return tasks.Count(); }
        }
        public void BindTask(int taskId)
        {
            IMotionTask task = XTaskManager.Instance.FindTaskById(taskId) as IMotionTask;
            task.StationId = StationId;

            if (!tasks.Any(t => t.GetType() == task.GetType()))
            {
                tasks.Add(task);
            }
        }
        // 任务参数访问方法
        public TParams GetTaskParameters<TParams>(int taskId)
            where TParams : TaskParametersBase
        {
            var task = tasks.FirstOrDefault(t => t.TaskId == taskId);
            return task?.ParametersBase as TParams;
        }
        // 强类型任务获取方法
        public TTask GetTask<TTask>() where TTask : ITask
        {
            return tasks.OfType<TTask>().FirstOrDefault();
        }
        // 运行时更新任务参数
        public bool UpdateTaskParameters<TParams>(int taskId, Action<TParams> updateAction)
            where TParams : TaskParametersBase
        {
            var parameters = GetTaskParameters<TParams>(taskId);
            if (parameters == null) return false;
            updateAction(parameters);
            //task.SaveParameters(); // 假设任务实现了参数保存方法
            return true;
        }

        #region Signal

        public void SetLightGreenDo(int setDoId)
        {
            greenLight = XDevice.Instance.FindDoById(setDoId);
        }

        public void SetLightOrangeDo(int setDoId)
        {
            orangeLight = XDevice.Instance.FindDoById(setDoId);
        }

        public void SetLightRedDo(int setDoId)
        {
            redLight = XDevice.Instance.FindDoById(setDoId);
        }
        public void SetBuzzerDo(int setDoId)
        {
            buzzer = XDevice.Instance.FindDoById(setDoId);
        }
        #endregion


        #region HandleEvent

        public override int HandleEvent(XEvent xEvent)
        {
            currenttaskid = xEvent.CurrentTaskID;
            switch (xEvent.EventID)
            {

                case XEventID.RST:
                    if (xEvent.CurrentTaskID == -2) //全部任务  目前只测试控制不同任务单独回原点 其他方法待后续完善
                    {
                        foreach (ITask task in tasks)
                        {
                            task.HandleEvent(xEvent);
                        }
                    }
                    else
                    {
                        foreach (ITask task in tasks)
                        {
                            if (task.TaskId == xEvent.CurrentTaskID)
                            {
                                task.IsStopped = false;
                                task.IsPaused = false;
                                task.HandleEvent(xEvent);
                                break;
                            }
                        }
                    }
                    break;
                case XEventID.START:
                    PrimOnStart();
                    break;
                case XEventID.RESET:
                    PrimOnReset();
                    break;
                case XEventID.ALARM:
                    foreach (ITask task in tasks)
                    {
                        task.HandleEvent(xEvent);
                    }
                    PrimOnAlarm((XAlarmLevel)xEvent.EventArgs.AlarmLevel);
                    break;
                case XEventID.ESTOP:
                    foreach (ITask task in tasks)
                    {
                        task.HandleEvent(xEvent);
                    }
                    PrimOnEStop();
                    break;
                case XEventID.PAUSE:
                    foreach (ITask task in tasks)
                    {
                        task.IsPaused = true;
                        task.HandleEvent(xEvent);
                    }
                    PrimOnPause();
                    break;
                case XEventID.CONTINUE:
                    foreach (ITask task in tasks)
                    {
                        task.IsPaused = false;
                        task.HandleEvent(xEvent);
                    }
                    PrimOnContinue();
                    break;
                case XEventID.STOPMUSTRESET:
                    foreach (ITask task in tasks)
                    {
                        task.HandleEvent(xEvent);
                    }
                    PrimOnStop();
                    break;
                case XEventID.BUZZ:
                    PrimOnBuzzer();
                    break;
                case XEventID.RESETBUZZ:
                    PrimOnResetBuzzer();
                    break;
            }
            return 0;
        }

        private void PrimOnStart()
        {
            if (m_State == XStationState.WAITRUN)
            {
                SetState(XStationState.RUNNING);
                foreach (var task in tasks)
                {
                    task.Start(m_RunMode);   //XTask的Start();
                }
            }
        }

        private void PrimOnReset()
        {
            PrimOnResetBuzzer();

            if (m_State == XStationState.ESTOP ||
               m_State == XStationState.ALARM ||
               m_State == XStationState.STOP ||
               m_State == XStationState.WAITRESET ||
               m_State == XStationState.WAITRUN ||
                m_State == XStationState.PAUSE )
            {
                SetState(XStationState.RESETING);
                if (currenttaskid == -2)
                {
                    foreach (ITask task in tasks)
                    {
                        task.Reset();
                    }
                }
                else
                {
                    foreach (ITask task in tasks)
                    {
                        if (task.TaskId == currenttaskid)
                        {
                            task.Reset();
                            break;
                        }
                    }
                }
            }
        }

        private void PrimOnEStop()
        {
            SetState(XStationState.ESTOP);
            foreach (ITask task in tasks)
            {
                task.Cancel();
            }
            PrimOnBuzzer();
        }

        private void PrimOnAlarm(XAlarmLevel level)
        {
            switch (level)
            {
                case XAlarmLevel.STOP:
                    if (m_State != XStationState.ESTOP)
                    {
                        SetState(XStationState.ALARM);
                    }
                    break;
                case XAlarmLevel.PAUSE:
                    PrimOnPause();
                    break;
            }
            PrimOnBuzzer();
        }

        private void PrimOnStop()
        {
            if (m_State != XStationState.ESTOP &&
                m_State != XStationState.ALARM)
            {
                SetState(XStationState.STOP);
                foreach (ITask task in tasks)
                {
                    task.Cancel();
                }
            }
            PrimOnResetBuzzer();
        }

        private void PrimOnPause()
        {
            if (m_State == XStationState.RUNNING)
            {
                SetState(XStationState.PAUSE);
            }
            foreach (ITask task in tasks)
            {
                task.Pause(); 
            }
            if (PauseOn != null)
            {
                PauseOn();
            }
        }

        private void PrimOnContinue()
        {
            if (m_State == XStationState.PAUSE)
            {
                SetState(XStationState.RUNNING);
            }
            foreach (ITask task in tasks)
            {
                task.Continue();
            }
            PrimOnResetBuzzer();
            if (ContinueOn != null)
            {
                ContinueOn();
            }
        }
        private CancellationTokenSource _buzzerCts;
        private void PrimOnBuzzer()
        {
            if (buzzer == null) return;
            _buzzerCts?.Cancel();
            _buzzerCts = new CancellationTokenSource();
            Task.Run(async () =>
            {
                try
                {
                    using (_buzzerCts.Token.Register(() =>
                        buzzer.SetDo(0))) // 强制终止时确保关闭蜂鸣器
                    {
                        m_isBuzzing = true;

                        while (m_isBuzzing && !_buzzerCts.Token.IsCancellationRequested)
                        {
                            if (!m_isEnableBuzzer)
                            {
                                m_isBuzzing = false;
                                break;
                            }
                            // 蜂鸣启动（使用异步防止阻塞）
                            buzzer.SetDo(1);
                            // 黄灯亮
                            OnOrangeLightOn();
                            // 分段检查控制信号（提升响应速度）
                            await WaitWithCancellation(2000, _buzzerCts.Token);

                            // 停止蜂鸣（即使未到2000ms，取消时会提前触发）
                            buzzer.SetDo(0);
                            // 黄灯灭
                            OnOrangeLightOff();
                            // 间隔检测
                            await WaitWithCancellation(1000, _buzzerCts.Token);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常的取消操作
                }
                finally
                {
                    buzzer.SetDo(0); // 最终确保关闭
                    m_isBuzzing = false;
                }
            }, _buzzerCts.Token);
        }
        // 可中断的异步等待方法
        private async Task WaitWithCancellation(int milliseconds, CancellationToken ct)
        {
            var delayTask = Task.Delay(milliseconds, ct);
            var checkTask = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested && m_isBuzzing)
                {
                    await Task.Delay(50); // 每50ms检查一次状态
                }
            }, ct);
            await Task.WhenAny(delayTask, checkTask);
            ct.ThrowIfCancellationRequested();
        }
        private void PrimOnResetBuzzer()
        {
            _buzzerCts?.Cancel();
            m_isBuzzing = false;
            if (buzzer != null)
            {
                buzzer.SetDo(0);
            }
        }
        #endregion


        #region Light

        private enum LightType
        {
            RED,
            ORANGE,
            GREEN
        }

        private void Changing(object state)
        {
            //if (m_State != m_LastState)
            {
                if (OnStationStateChanged != null)
                {
                    OnStationStateChanged(m_State);
                }
            }
            if (m_LightFlash == false)
            {
                m_LightFlashFlag = 1;
                return;
            }
            switch (m_LightFlashFlag)
            {
                case 0:
                    if (AllLightsOFF != null)
                    {
                        AllLightsOFF();
                    }
                    m_LightFlashFlag = 1;
                    break;
                case 1:
                    SetLightOn();
                    m_LightFlashFlag = 0;
                    break;
            }
            m_LastState = m_State;
        }

        private void SetLightOn()
        {
            lock(objLed)
            {
                switch (m_LightType)
                {
                    case LightType.RED:
                        if (RedLightON != null)
                        {
                            RedLightON();
                        }
                        break;
                    case LightType.ORANGE:
                        if (OrangeLightON != null)
                        {
                            OrangeLightON();
                        }
                        break;
                    case LightType.GREEN:
                        if (GreenLightON != null)
                        {
                            GreenLightON();
                        }
                        break;
                }
            }
        }

        private void OnlyRedLightOn()
        {
            if (redLight != null)
            {
                redLight.SetDo(1);
            }
            if (orangeLight != null)
            {
                orangeLight.SetDo(0);
            }
            if (greenLight != null)
            {
                greenLight.SetDo(0);
            }
        }

        private void OnlyOrangeLightOn()
        {
            if (redLight != null)
            {
                redLight.SetDo(0);
            }
            if (orangeLight != null)
            {
                orangeLight.SetDo(1);
            }
            if (greenLight != null)
            {
                greenLight.SetDo(0);
            }
        }

        private void OnlyGreenLightOn()
        {
            if (redLight != null)
            {
                redLight.SetDo(0);
            }
            if (orangeLight != null)
            {
                orangeLight.SetDo(0);
            }
            if (greenLight != null)
            {
                greenLight.SetDo(1);
            }
        }
        private void OnOrangeLightOn()
        {
            if (orangeLight != null)
            {
                orangeLight.SetDo(1);
            }
        }
        private void OnOrangeLightOff()
        {
            if (orangeLight != null)
            {
                orangeLight.SetDo(0);
            }
        }
        private void AllLightsOff()
        {
            if (redLight != null)
            {
                redLight.SetDo(0);
            }
            if (orangeLight != null)
            {
                orangeLight.SetDo(0);
            }
            if (greenLight != null)
            {
                greenLight.SetDo(0);
            }
        }

        #endregion

        public void SetState(XStationState sts)
        {
            m_LightFlash = false;
            lock (this)
            {
                m_State = sts;
            }
            if (OnStationStateChanged != null)
            {
                OnStationStateChanged(m_State);
            }
            StationStateRaised?.Invoke(m_State);
            switch (sts)
            {
                case XStationState.ESTOP:
                    m_LightType = LightType.RED;
                    m_LightFlash = true;
                    StopWatch_Stop();
                    break;
                case XStationState.ALARM:
                    m_LightType = LightType.RED;
                    m_LightFlash = true;
                    StopWatch_Stop();
                    break;
                case XStationState.RESETING:
                    m_LightType = LightType.ORANGE;
                    m_LightFlash = false;
                    for (int i = 0; i < tasks.Count; i++)
                    {
                        if (!tasks[i].TaskHomeOK)
                        {
                            m_isAllHomeOk = false;
                            break;
                        }
                        m_isAllHomeOk = true;
                    }
                    if (m_isAllHomeOk)
                    {
                        SetState(XStationState.WAITRUN);
                    }
                    SetLightOn();
                    StopWatch_Reset();
                    break;
                case XStationState.RUNNING:
                    m_LightType = LightType.GREEN;
                    m_LightFlash = false;
                    SetLightOn();
                    //StopWatch_Start();
                    break;
                case XStationState.PAUSE:
                    m_LightType = LightType.GREEN;
                    m_LightFlash = true;
                    StopWatch_Stop();
                    break;
                case XStationState.STOP:
                    m_LightType = LightType.RED;
                    m_LightFlash = true;
                    StopWatch_Stop();
                    break;
                case XStationState.WAITRESET:
                    m_LightType = LightType.ORANGE;
                    m_LightFlash = true;
                    StopWatch_Stop();
                    break;
                case XStationState.WAITRUN:
                    if (m_isAllHomeOk)
                    {
                        m_LightType = LightType.ORANGE;
                        m_LightFlash = false;
                        SetLightOn();
                        StopWatch_Stop();
                    }
                    break;
                case XStationState.CLEAR:
                    m_LightType = LightType.GREEN;
                    m_LightFlash = false;
                    SetLightOn();
                    StopWatch_Stop();
                    break;
            }
        }


        #region StopWatch

        public void StopWatch_Start()
        {
            this.m_StopWatch.Start();
        }

        public void StopWatch_Reset()
        {
            this.m_StopWatch.Reset();
        }

        public void StopWatch_ReStart()
        {
            this.m_StopWatch.Restart();
        }

        public void StopWatch_Stop()
        {
            this.m_StopWatch.Stop();
        }

        public double ElapsedMilliseconds
        {
            get { return this.m_StopWatch.ElapsedMilliseconds; }
        }

        StationState IStation.State => ToStationState(m_State);
        // 枚举映射方法
        public static StationState ToStationState(XStationState xState)
        {
            return xState switch
            {
                XStationState.NONE => StationState.None,
                XStationState.ESTOP => StationState.Estop,
                XStationState.ALARM => StationState.Alarm,
                XStationState.STOP => StationState.Stop,
                XStationState.WAITRESET => StationState.WaitReset,
                XStationState.RESETING => StationState.Resetting,
                XStationState.WAITRUN => StationState.WaitRun,
                XStationState.RUNNING => StationState.Running,
                XStationState.PAUSE => StationState.Pause,
                XStationState.CLEAR => StationState.Clear,
                XStationState.TIP => StationState.Tip,
                _ => StationState.None
            };
        }

        public int Id => StationId;

        public int TaskId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        #endregion

        /// <summary>
        /// 工站开始，工站绑定的所有任务开始，运行Task.Running()
        /// </summary>
        public void Start(object runMode)
        {
            if (!IsEnable)
                return;
            m_RunMode = runMode;
            switch (m_State)
            {
                case XStationState.WAITRUN:
                    //foreach (XTask task in tasks)
                    //{
                    //    task.Start(runMode);
                    //}
                    PostEvent(this, XEventID.START);
                    break;
                case XStationState.PAUSE:

                    PostEvent(this, XEventID.CONTINUE, true);
                    break;
            }

        }
        /// <summary>
        /// 工站暂停，工站绑定的所有任务暂停
        /// </summary>
        public void Pause()
        {
            if (!IsEnable)
                return;
            PostEvent(this, XEventID.PAUSE, true);
        }
        /// <summary>
        /// 工站继续，工站绑定的所有任务继续
        /// </summary>
        public void Continue()
        {
            if (!IsEnable)
                return;

            PostEvent(this, XEventID.CONTINUE, true);
        }
        /// <summary>
        /// 工站复位，工站绑定的所有任务复位，运行Task.Homing()
        /// </summary>
        public void Reset(int currenttaskid)
        {
            if (!IsEnable)
                return;
            PostEvent(this, XEventID.RST, true, currenttaskid);
            PostEvent(this, XEventID.RESET, true, currenttaskid);
        }
        public void Reset()
        {
            if (!IsEnable)
                return;
            PostEvent(this, XEventID.RST);
            PostEvent(this, XEventID.RESET);
        }

        public void ResetBuzz()
        {
            if (!IsEnable)
                return;
            PostEvent(this, XEventID.RESETBUZZ);

        }
        /// <summary>
        /// 工站停止，工站绑定的所有任务停止
        /// </summary>
        public void Stop()
        {
            if (!IsEnable)
                return;
            //foreach (XTask task in tasks)
            //{
            //    task.Cancel();
            //}
            PostEvent(this, XEventID.STOPMUSTRESET, true);
            PostEvent(this, XEventID.RESETBUZZ);
        }

    }



}

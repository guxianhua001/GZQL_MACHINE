
using Core.Abstraction;
using Interfaces;
using Interfaces.Services;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using static System.Collections.Specialized.BitVector32;

namespace SmarterMotion
{
    public enum MachineModeType
    {
        Production,
        Engineering,
        IPQC,
        None
    }
    public sealed class XMachine : XMachineEventHandler
    {
        private XDi signalEStop = null;
        public List<XDi> signalSafeDoorList = new List<XDi>();//门锁di
        public List<XDi> signalCurtainList = new List<XDi>();//光栅di
        private List<XDi> signalEStopList = new List<XDi>();
        public List<XDi> signalDoorMagneticList = new List<XDi>();//门磁di
        public List<XDi> conditionContanstOpenOnInitList = new List<XDi>();//有信号 不能复位
        public List<XDi> conditionContanstCloseOnInitList = new List<XDi>();//无信号 不能复位
        public List<XDi> conditionsOnStart = new List<XDi>();//启动条件  true启动
        public List<XDi> conditionsOffStart = new List<XDi>();//启动条件 false启动
        public List<XDo> qSafeDoorList = new List<XDo>();//安全门锁do
        private XDi signalReset = null;
        private XDi signalStart = null;
        private XDi signalStop = null;
        private XDi signalOpenDoor = null;
        private XDi signalCloseDoor = null;
        private XDo qStartBtnLight = null;
        private XDo qStopBtnLight = null;
        private XDo qResetLight1 = null;//复位按钮
        private XDo qResetLight2 = null;//复位按钮
        private XDo qOpendoorBtnLight = null;
        private XDo qClosedoorBtnLight = null;
        private XDo qFeedMachineDoor = null;//上料机上料处安全门
        public XDo qBuzz;
        private bool m_SafeDoorEnabled;//启用安全门锁
        private bool m_CurtainEnabled;//启用光幕
        private bool m_BuzzerEnabled;//启用蜂鸣器
        private bool m_MagnetismEnabled;//启用门磁
        private bool eStop;
        private bool lastEStop;
        private bool beerStatus;
        private bool lastBeer;
        public bool isDangerAlarm;
        private bool m_isHold = false;//host通知hold
        private bool m_isPause = false;
        private MachineModeType machinemode = MachineModeType.None;
        private Thread _thread;
        private CancellationTokenSource _cts1;
        private Thread _dispenserProcess;
        private CancellationTokenSource _cts2;
        private static readonly XMachine instance = new XMachine();
        private Thread _resetProcess;
        public event Action OnStarting;
        public event Action OnStopping;
        public event Action OnPausing;
        public event Action OnReseting;
        public event Action OnAlarm;
        public event Action ResetAlarm;
        XMachine()
        {

        }
        public static XMachine Instance
        {
            get { return instance; }
        }
        public bool HostToEqpHoldMachine
        {
            get
            {
                return m_isHold;
            }
            set
            {
                m_isHold = value;
            }
        }
        public MachineModeType MachineMode
        {
            get { return machinemode; }
            set { machinemode = value; }
        }
        public override int HandleEvent(XEvent xEvent)
        {
            switch (xEvent.EventID)
            {
                case XEventID.SIGNAL:
                    PrimOnSignal();
                    break;
            }
            return 0;
        }
        protected bool CheckSignalStatus(int filterCount = 500, int pollIntervalMs = 10)
        {
            if (filterCount < 1) filterCount = 1;
            if (pollIntervalMs < 0) pollIntervalMs = 0;

            int consistentReadings = 0;
            bool? lastValue = null;
            bool currentValue = false;
            while (consistentReadings < filterCount)
            {
                // 信号状态变化检测
                signalReset.Update();
                currentValue = signalReset.STS;
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
        public void Start()
        {
            Stop();
            if (signalEStop != null)
            {
                //for (int i = 0; i < XDevice.Instance.CardMap.Count; i++)
                //{
                //    XDevice.Instance.CardMap[i + 1].Update();
                //}
                //for (int i = 0; i < signalEStopList.Count; i++)
                //{
                //    signalEStopList[i].Update();
                //}
            }
            _cts1 = new CancellationTokenSource();
            _thread = new Thread(() => T_PrimOnSignal(_cts1.Token));
            _thread.IsBackground = true;
            _thread.Start();
            _cts2 = new CancellationTokenSource();
            _dispenserProcess = new Thread(() => Update(_cts2.Token));
            _dispenserProcess.IsBackground = true;
            _dispenserProcess.Start();
        }
        public void Stop()
        {
            _cts1?.Cancel();
            _cts2?.Cancel();
        }
        private void Update(CancellationToken cancellation)
        {
            while (!cancellation.IsCancellationRequested)
            {

                foreach (KeyValuePair<int, IAxis> kvp in XDevice.Instance.AxisMap)
                {
                    kvp.Value.Update();   //XleisaiAxis.Update();
                }
                foreach (KeyValuePair<int, XCard> kvp in XDevice.Instance.CardMap)
                {
                    kvp.Value.Update();   //XCard.Update() => XCommandCardLeisai.Update();
                }
                foreach (KeyValuePair<int, XDi> kvp in XDevice.Instance.DiMap)
                {
                    kvp.Value.Update();   // XDi.Update() => XCommandCardLeisai.GetDi();
                }
                foreach (KeyValuePair<int, XDo> kvp in XDevice.Instance.DoMap)
                {
                    kvp.Value.Update();   // XDo.Update() => XCommandCardLeisai.GetDo();
                }
                XDevice.Instance.CardMap[1].CheckEtherCatStatus((ushort)0);
                Thread.Sleep(2);
            }

        }
        private void UpdateAxisMap()
        {
            while (true)
            {
                foreach (KeyValuePair<int, IAxis> kvp in XDevice.Instance.AxisMap)
                {
                    kvp.Value.Update();   //XleisaiAxis.Update();
                }
                Thread.Sleep(2);
            }
        }
        private void UpdateCardMap()
        {
            while (true)
            {
                foreach (KeyValuePair<int, XCard> kvp in XDevice.Instance.CardMap)
                {
                    kvp.Value.Update();   //XCard.Update() => XCommandCardLeisai.Update();
                }
                Thread.Sleep(2);
            }
        }
        private void UpdateIOMap()
        {
            while (true)
            {
                foreach (KeyValuePair<int, XDi> kvp in XDevice.Instance.DiMap)
                {
                    kvp.Value.Update();   // XDi.Update() => XCommandCardLeisai.GetDi();
                }
                foreach (KeyValuePair<int, XDo> kvp in XDevice.Instance.DoMap)
                {
                    kvp.Value.Update();   // XDo.Update() => XCommandCardLeisai.GetDo();
                }
                Thread.Sleep(2);
            }
        }

        public bool CheckStartCondition()
        {
        Start:
            if (this.DoorEnabled)
            {
                foreach (XDi di in this.signalSafeDoorList)
                {
                    if (di.STS == false)
                    {
                        var vm = new ErrorDialogViewModel
                        {
                            Title = "Di信号超时报警",
                            ErrorMessage = $"{di.Name} 未检测到安全门信号 \r\n若选择继续 设备将继续检查启动条件" + "\r\n" +
                     $"若选择停止 设备不能启动"
                        };

                        var dialog = new ErrorDialog(vm);
                        var result = dialog.ShowDialog();

                        if (result == true) // 对应ButtonResult.Yes
                        {
                            // 处理继续逻辑
                            goto Start;
                        }
                        else
                        {
                            // 处理暂停逻辑
                            goto STOP;
                        }
                    }
                }
            }
            foreach (XDi di in this.conditionsOnStart)
            {
                if (di.STS == false)
                {
                    var vm = new ErrorDialogViewModel
                    {
                        Title = "Di信号超时报警",
                        ErrorMessage = $"{di.Name} 未检测到信号 \r\n若选择继续 设备将继续检查启动条件" + "\r\n" +
                        $"若选择停止 设备不能启动"
                    };

                    var dialog = new ErrorDialog(vm);
                    var result = dialog.ShowDialog();

                    if (result == true) // 对应ButtonResult.Yes
                    {
                        // 处理继续逻辑
                        goto Start;
                    }
                    else
                    {
                        // 处理暂停逻辑
                        goto STOP;
                    }
                }
            }
            foreach (XDi di in this.conditionsOffStart)
            {
                if (di.STS == true)
                {
                    var vm = new ErrorDialogViewModel
                    {
                        Title = "Di信号超时报警",
                        ErrorMessage = $"{di.Name} 检测到有信号 \r\n若选择继续 设备将继续检查启动条件" + "\r\n" +
                        $"若选择停止 设备不能启动"
                    };

                    var dialog = new ErrorDialog(vm);
                    var result = dialog.ShowDialog();

                    if (result == true) // 对应ButtonResult.Yes
                    {
                        // 处理继续逻辑
                        goto Start;
                    }
                    else
                    {
                        // 处理暂停逻辑
                        goto STOP;
                    }
                }
            }
            return true;
        STOP:
            return false;
        }
        public bool CheckResetCondition()
        {
            foreach (XDi di in conditionContanstCloseOnInitList)
            {
                if (di.STS == false)
                {
                    //if (new DialogService().ShowDialogAndReturnResult("DI信号报警", $"{di.Name} 无信号,复位条件不满足,设备不能复位 \r\n若选择继续 将继续检查DI信号" + "\r\n" +
                    //      $"若选择暂停,设备将不启动..."))
                    //{
                    //    return false;
                    //}
                    //else
                    //{
                    //    return false;
                    //}
                }
            }
            foreach (XDi di in conditionContanstOpenOnInitList)
            {
                if (di.STS)
                {
                    //if (new DialogService().ShowDialogAndReturnResult("DI信号报警", $"{di.Name} 有信号,复位条件不满足,设备不能复位 \r\n若选择继续 将继续检查DI信号" + "\r\n" +
                    //      $"若选择暂停,设备将不启动..."))
                    //{
                    //    return false;
                    //}
                    //else
                    //{
                    //    return false;
                    //}
                }
            }
            return true;
        }
        private XDi _estop;
        private void T_PrimOnSignal(CancellationToken cancellation)
        {
            while (!cancellation.IsCancellationRequested)
            {
                if (XDevice.Instance.CardMap[1].IsConnect == false)
                {
                    while (!XDevice.Instance.CardMap[1].IsConnect)
                    {
                        Thread.Sleep(1000);
                    }
                }
                if (signalEStopList.Count > 0)
                {
                    foreach (XDi di in signalEStopList)
                    {
                        if (di.STS) //急停状态  !di.STS
                        {
                            Thread.Sleep(20);
                            eStop = di.STS;
                        }
                        if (eStop)
                        {
                            _estop = di;
                            break;
                        }
                    }
                    if (eStop == true && lastEStop == false)
                    {
                        SetRedBtnLight();
                        foreach (XStation station in XStationManager.Instance.Stations.Values)
                        {
                            PostEventEStop(station);
                        }
                        XEventArgs e = new XEventArgs();
                        e.StationId = 0;
                        e.IntValue = 1;
                        e.AlarmLevel = (int)XAlarmLevel.STOP;
                        XController.Instance.AlarmEventServer.PostEvent(XAlarmReporter.Instance, XEventID.ESTOP, e, null, true);
                        IMessage.Logger.Warn($"急停拍下:{_estop.Name}");
                    }
                    else if (eStop == false && lastEStop == true)
                    {
                        foreach (XStation station in XStationManager.Instance.Stations.Values)
                        {
                            PostEvent(station, XEventID.WAITRESET);
                        }
                        SetRedBtnLight();
                    }
                    lastEStop = eStop;
                }

                if (signalStop != null)
                {
                    signalStop.Update();
                    if (signalStop.STS == true)
                    {
                        SetRedBtnLight();
                        foreach (XStation station in XStationManager.Instance.Stations.Values)
                        {
                            PostEvent(station, XEventID.PAUSE);
                        }
                    }
                }
                if (signalStart != null)
                {
                    if (signalStart.STS == true)
                    {
                    Start:
                        if (m_isHold)
                        {
                            goto STOP;
                        }
                        if (this.m_SafeDoorEnabled)
                        {
                            foreach (XDi di in signalSafeDoorList)
                            {
                                if (di.STS == false)
                                {
                                    //if (new DialogService().ShowDialogAndReturnResult("DI信号报警", $"{di.Name} 未检测到安全门信号,设备不能q启动 \r\n若选择继续 将继续检查DI启动信号" + "\r\n" +
                                    //          $"若选择暂停,设备将不启动..."))
                                    //{
                                    //    goto Start;
                                    //}
                                    //else
                                    //{
                                    //    goto STOP;
                                    //}
                                }
                            }
                        }
                        foreach (XDi di in conditionsOnStart)
                        {
                            if (di.STS == false)
                            {
                                //if (new DialogService().ShowDialogAndReturnResult("Di信号超时报警", $"{di.Name} 未检测到信号,设备不能启动 \r\n若选择继续 将继续检查DI启动信号" + "\r\n" +
                                //             $"若选择暂停,设备将不启动..."))
                                //{
                                //    goto Start;
                                //}
                                //else
                                //{
                                //    goto STOP;
                                //}
                            }
                        }
                        foreach (XDi di in conditionsOffStart)
                        {
                            if (di.STS == true)
                            {
                                //if (new DialogService().ShowDialogAndReturnResult("Di信号超时报警", $"{di.Name} 检测到有信号,设备不能启动 \r\n若选择继续 将继续检查DI启动信号" + "\r\n" +
                                //             $"若选择暂停,设备将不启动..."))
                                //{
                                //    goto Start;
                                //}
                                //else
                                //{
                                //    goto STOP;
                                //}
                            }
                        }
                        foreach (XStation station in XStationManager.Instance.Stations.Values)
                        {
                            if (station.State != XStationState.WAITRUN)
                            {
                                if (station.State == XStationState.PAUSE)
                                {
                                    PostEvent(station, XEventID.CONTINUE);
                                }
                                SetGreenBtnLight();
                                goto STOP;
                            }
                        }
                        foreach (XStation station in XStationManager.Instance.Stations.Values)
                        {
                            PostEvent(station, XEventID.START);
                        }
                        SetGreenBtnLight();
                        if (OnStarting != null)
                        {
                            OnStarting();
                        }
                    STOP:
                        Thread.Sleep(2);
                    }
                }

                Thread.Sleep(1);
            }
        }

        private int PrimOnSignal()
        {
            if (eStop == false)
            {
                if (signalReset != null)
                {
                    if (signalReset.STS == false)
                    {
                        ResetBuzzer();
                        foreach (XStation station in XStationManager.Instance.Stations.Values)
                        {
                            PostEvent(station, XEventID.RESETBUZZ);
                        }
                        Thread.Sleep(2);
                    }
                }
            }
            if (qBuzz != null)
            {
                beerStatus = false;
                foreach (XStation station in XStationManager.Instance.Stations.Values)
                {
                    if (station.State == XStationState.ESTOP || station.State == XStationState.ALARM)
                    {
                        beerStatus = true;
                        break;
                    }
                }
                if (beerStatus && lastBeer == false)
                {
                    foreach (XStation station in XStationManager.Instance.Stations.Values)
                    {
                        PostEvent(station, XEventID.BUZZ);
                    }
                }
                lastBeer = beerStatus;
            }
            if (this.m_SafeDoorEnabled)
            {
                foreach (XDi di in signalSafeDoorList)
                {
                    if (!di.STS)
                    {
                        di.Update();
                        if (di.STS == false)
                        {
                            bool bRun = false;
                            foreach (XStation station in XStationManager.Instance.Stations.Values)
                            {
                                if (station.State == XStationState.RUNNING)
                                {
                                    bRun = true;
                                    PostEvent(station, XAlarmLevel.PAUSE, XSysAlarmId.DOOR_OPEN);
                                }
                            }
                            if (bRun)
                            {
                                Thread.Sleep(120);
                                foreach (XStation station in XStationManager.Instance.Stations.Values)
                                {
                                    PostEvent(station, XEventID.BUZZ);
                                }
                                if (OnPausing != null)
                                {
                                    OnPausing();
                                }
                                if (OnAlarm != null)
                                {
                                    OnAlarm();
                                }

                                IMessage.Logger.Warn($"触发门限:{di.Name}");

                            }
                        }
                    }
                }
            }
            if (this.m_CurtainEnabled)
            {
                foreach (XDi di in signalCurtainList)
                {
                    if (di.PLF)
                    {
                        di.Update();
                        if (di.STS == false)
                        {
                            bool bRun = false;
                            foreach (XStation station in XStationManager.Instance.Stations.Values)
                            {
                                if (station.State == XStationState.RUNNING)
                                {
                                    bRun = true;
                                    PostEvent(station, XAlarmLevel.PAUSE, XSysAlarmId.CURTAIN_ACT);
                                }
                            }
                            if (bRun)
                            {
                                foreach (XStation station in XStationManager.Instance.Stations.Values)
                                {
                                    PostEvent(station, XEventID.BUZZ);
                                }
                                if (OnPausing != null)
                                {
                                    OnPausing();
                                }
                                if (OnAlarm != null)
                                {
                                    OnAlarm();
                                }
                            }
                        }
                    }
                }
            }
            if (this.m_MagnetismEnabled)
            {
                foreach (XDi di in signalDoorMagneticList)
                {
                    if (di.PLF)
                    {
                        di.Update();
                        if (di.STS == false)
                        {
                            bool bRun = false;
                            foreach (XStation station in XStationManager.Instance.Stations.Values)
                            {
                                if (station.State == XStationState.RUNNING)
                                {
                                    bRun = true;
                                    PostEvent(station, XAlarmLevel.PAUSE, XSysAlarmId.DOOR_OPEN);
                                }
                            }
                            if (bRun)
                            {
                                foreach (XStation station in XStationManager.Instance.Stations.Values)
                                {
                                    PostEvent(station, XEventID.BUZZ);
                                }
                                if (OnPausing != null)
                                {
                                    OnPausing();
                                }
                                if (OnAlarm != null)
                                {
                                    OnAlarm();
                                }
                            }
                        }
                    }
                }
            }
            if (qOpendoorBtnLight != null)
            {
                if (signalOpenDoor.PLF)
                {
                    SetOpenDoorLight();
                    //foreach (XDo di in qSafeDoorList)
                    //{
                    //    di.SetDo(0);
                    //}
                    if (qFeedMachineDoor != null)
                        qFeedMachineDoor.SetDo(0);
                }
            }
            if (qClosedoorBtnLight != null)
            {
                if (signalCloseDoor.PLF)
                {
                    SetCloseDoorLight();
                    //ResetBuzzer();
                    //foreach (XDo di in qSafeDoorList)
                    //{
                    //    di.SetDo(1);
                    //}
                    if (qFeedMachineDoor != null)
                        qFeedMachineDoor.SetDo(1);
                }
            }
            return 0;
        }

        private int PrimOnReset()
        {
            if (eStop == false)
            {
                if (signalReset != null)
                {
                    if (signalReset.STS == true)
                    {
                        if (CheckSignalStatus())
                        {
                        RESET:
                            if (m_isHold)
                            {
                                goto NORESET;
                            }
                            if (this.m_SafeDoorEnabled)
                            {
                                foreach (XDi di in signalSafeDoorList)
                                {
                                    if (di.STS == false)
                                    {
                                        //if (new DialogService().ShowDialogAndReturnResult("安全门信号报警", "未检测到安全门信号 \r\n若选择继续 将继续检查安全门信号" + "\r\n" +
                                        //  $"若选择暂停,设备将不启动..."))
                                        //{
                                        //    goto RESET;
                                        //}
                                        //else
                                        //{
                                        //    goto NORESET;
                                        //}
                                    }
                                }
                            }
                            foreach (XDi di in conditionContanstCloseOnInitList)
                            {
                                if (di.STS == false)
                                {
                                    //if (new DialogService().ShowDialogAndReturnResult("DI信号报警", $"{di.Name} 无信号,复位条件不满足,设备不能复位 \r\n若选择继续 将继续检查DI信号" + "\r\n" +
                                    //      $"若选择暂停,设备将不启动..."))
                                    //{
                                    //    goto RESET;
                                    //}
                                    //else
                                    //{
                                    //    goto NORESET;
                                    //}
                                }
                            }
                            foreach (XDi di in conditionContanstOpenOnInitList)
                            {
                                if (di.STS)
                                {
                                    //if (new DialogService().ShowDialogAndReturnResult("DI信号报警", $"{di.Name} 有信号,复位条件不满足,设备不能复位 \r\n若选择继续 将继续检查DI信号" + "\r\n" +
                                    //      $"若选择暂停,设备将不启动..."))
                                    //{
                                    //    goto RESET;
                                    //}
                                    //else
                                    //{
                                    //    goto NORESET;
                                    //}
                                }
                            }
                            foreach (XStation station in XStationManager.Instance.Stations.Values)
                            {
                                PostEvent(station, XEventID.RST);
                                PostEvent(station, XEventID.RESET);
                            }
                            SetResetBtnLight();
                            if (OnReseting != null)
                            {
                                OnReseting();
                            }
                        }
                    NORESET:
                        Thread.Sleep(2);
                    }
                }
            }
            return 0;
        }

        public void SetEStopDi(int setDiId)
        {
            signalEStop = XDevice.Instance.FindDiById(setDiId);
        }
        public void AddEStopDi(int setDiId)
        {
            signalEStopList.Add(XDevice.Instance.FindDiById(setDiId));
        }
        public void SetBerrDo(int setDoId)
        {
            qBuzz = XDevice.Instance.FindDoById(setDoId);
        }
        public void AddDoorDi(int setDiId)
        {
            signalSafeDoorList.Add(XDevice.Instance.FindDiById(setDiId));
        }
        public void AddCurtainDi(int setDiId)
        {
            signalCurtainList.Add(XDevice.Instance.FindDiById(setDiId));
        }
        public void AddSafeDoorDo(int setDoId)
        {
            qSafeDoorList.Add(XDevice.Instance.FindDoById(setDoId));
        }
        public void AddDoorMagneticni(int setDiId)
        {
            signalDoorMagneticList.Add(XDevice.Instance.FindDiById(setDiId));
        }
        public void AddConditionsCOpenOnInitialization(int setDiId)
        {
            conditionContanstOpenOnInitList.Add(XDevice.Instance.FindDiById(setDiId));
        }
        public void AddConditionsCCloseOnInitialization(int setDiId)
        {
            conditionContanstCloseOnInitList.Add(XDevice.Instance.FindDiById(setDiId));
        }
        public void AddConditionsOnStart(int setDiId)
        {
            conditionsOnStart.Add(XDevice.Instance.FindDiById(setDiId));
        }
        public void AddConditionsOffStart(int setDiId)
        {
            conditionsOffStart.Add(XDevice.Instance.FindDiById(setDiId));
        }
        public void SetResetDi(int setDiId)
        {
            signalReset = XDevice.Instance.FindDiById(setDiId);
        }

        public void SetStartDi(int setDiId)
        {
            signalStart = XDevice.Instance.FindDiById(setDiId);
        }

        public void SetStopDi(int setDiId)
        {
            signalStop = XDevice.Instance.FindDiById(setDiId);
        }
        public void SetOpenDoor(int setDiId)
        {
            signalOpenDoor = XDevice.Instance.FindDiById(setDiId);
        }
        public void SetCloseDoor(int setDiId)
        {
            signalCloseDoor = XDevice.Instance.FindDiById(setDiId);
        }
        public void SetStartLightDo(int setDoid)
        {
            qStartBtnLight = XDevice.Instance.FindDoById(setDoid);
        }
        public void SetStopLightDo(int setDoid)
        {
            qStopBtnLight = XDevice.Instance.FindDoById(setDoid);
            qStopBtnLight.SetDo(1);
        }
        public void SetResetLight1Do(int setDoid)
        {
            qResetLight1 = XDevice.Instance.FindDoById(setDoid);
        }
        public void SetResetLight2Do(int setDoid)
        {
            qResetLight2 = XDevice.Instance.FindDoById(setDoid);
        }
        public void SetFeedMachineDoor(int setDoid)
        {
            qFeedMachineDoor = XDevice.Instance.FindDoById(setDoid);
        }
        public void SetOpenDoorLightDo(int setDoid)
        {
            qOpendoorBtnLight = XDevice.Instance.FindDoById(setDoid);
        }
        public void SetCloseDoorLightDo(int setDoid)
        {
            qClosedoorBtnLight = XDevice.Instance.FindDoById(setDoid);
        }

        public void OpenDoor()
        {
            SetOpenDoorLight();
            foreach (XDo di in qSafeDoorList)
            {
                di.SetDo(0);
            }
        }
        public void CloseDoor()
        {
            SetCloseDoorLight();
            foreach (XDo di in qSafeDoorList)
            {
                di.SetDo(1);
            }
        }
        public void SetOpenDoorLight()
        {
            if (qOpendoorBtnLight != null)
               qOpendoorBtnLight.SetDo(1);
            if (qClosedoorBtnLight != null)
               qClosedoorBtnLight.SetDo(0);
        }
        public void SetCloseDoorLight()
        {
            if (qOpendoorBtnLight != null)
                qOpendoorBtnLight.SetDo(0);
            if (qClosedoorBtnLight != null)
                qClosedoorBtnLight.SetDo(1);
        }
        public void SetGreenBtnLight()
        {
            if (qStartBtnLight != null)
                qStartBtnLight.SetDo(1);
            if (qStartBtnLight != null)
                qStopBtnLight.SetDo(0);
            if (qStopBtnLight != null)
                qResetLight1.SetDo(0);
            if (qResetLight2 != null)
                qResetLight2.SetDo(0);
        }
        public void SetRedBtnLight()
        {
            if (qStartBtnLight != null)
                qStartBtnLight.SetDo(0);
            if (qStopBtnLight != null)
                qStopBtnLight.SetDo(1);
            if (qResetLight1 != null)
                qResetLight1.SetDo(0);
            if (qResetLight2 != null)
                qResetLight2.SetDo(0);
        }
        public void SetResetBtnLight()
        {
            if (qStartBtnLight != null)
                qStartBtnLight.SetDo(0);
            if (qStopBtnLight != null)
                qStopBtnLight.SetDo(0);
            if (qResetLight1 != null)
                qResetLight1.SetDo(1);
            if (qResetLight2 != null)
                qResetLight2.SetDo(1);
        }
        public void ResetResetBtnLight()
        {
            if (qStartBtnLight != null)
                qResetLight1.SetDo(0);
            if (qStopBtnLight != null)
                qResetLight2.SetDo(0);
        }
        public void SetBuzzer()
        {
            if (qBuzz != null)
            {
                if (m_BuzzerEnabled)
                {
                    isDangerAlarm = true;
                    foreach (XStation station in XStationManager.Instance.Stations.Values)
                    {
                        if (station.State == XStationState.RUNNING || station.State == XStationState.PAUSE)
                        {
                            station.SetState(XStationState.TIP);
                        }
                    }
                }
                else
                {
                    isDangerAlarm = false;
                }
                Task.Factory.StartNew(() =>
                {
                    while (isDangerAlarm)
                    {
                        if (!m_BuzzerEnabled)
                        {
                            ResetBuzzer();
                            break;
                        }
                        qBuzz.SetDo(1);
                        Thread.Sleep(2000);
                        qBuzz.SetDo(0);
                        Thread.Sleep(1000);
                    }
                    qBuzz.SetDo(0);
                });
            }
        }
        public void ResetBuzzer()
        {
            if (qBuzz != null)
                qBuzz.SetDo(0);
            isDangerAlarm = false;
            foreach (XStation station in XStationManager.Instance.Stations.Values)
            {
                if (station.State == XStationState.RUNNING)
                {
                    station.SetState(XStationState.RUNNING);
                }
            }
            if (ResetAlarm != null)
            {
                ResetAlarm();
            }
        }
        public bool DoorEnabled
        {
            get { return this.m_SafeDoorEnabled; }
            set { this.m_SafeDoorEnabled = value; }
        }
        public bool MagnetismEnabled
        {
            get { return this.m_MagnetismEnabled; }
            set { this.m_MagnetismEnabled = value; }
        }
        public bool CurtainEnabled
        {
            get { return this.m_CurtainEnabled; }
            set { this.m_CurtainEnabled = value; }
        }
        public bool BuzzerEnabled
        {
            get { return this.m_BuzzerEnabled; }
            set { this.m_BuzzerEnabled = value; }
        }
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
    }
}

using Prism.Events;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using Interfaces.Services;
using System.Runtime.InteropServices;
using System.Text;
using SmarterMotion;
using CHSMS;
using System.Xml;
using HSMS;
using Interfaces;
using System.Windows;
using Interfaces.Views;
using MaterialDesignThemes.Wpf;
using Interfaces.Events;
using Framework.Services;

namespace SECS
{    
    public class CEID   
    {
        public string ID="1";
        public string Discription = "Discription";
        //public string Units="0";
        public string M_Ceed = "T";
        public List<string> LsRepid=new List<string>();
        public CEID()
        {
            //LsRepid.Add("0");
        }
    }
    public class REPID 
    {
        public string ID="1";
        public string Discription= "Discription";
        public string Units="0";
        public List<string> LsVID = new List<string>();
        public REPID()
        {
            //LsVID.Add("0");
        }
    }
    public class VID   
    {
        public string ID="1";
        public string Discription= "Discription";
        public string Units="0";
    }
    public class ALID   
    {
        public string ID="1";
        public string Discription= "Discription";
        public string Units="0";
        public string M_Ceed = "T";
        public string ALCD="0";
        public string Remark = "";
    }
    public class ECID  
    {
        public string ID="1";
        public string Discription= "Discription";
        public string Value="0";
        public string Min="0";  //最小值
        public string Max="0";  //最大值
        public string Ecdef="0";  //默认值
        public string Unit="mm";  //单位
    }
    public class SVID  
    {
        public string ID="1";
        public string Discription= "Discription";
        public string Units="0";
        public SVID()
        {

        }
    }
    public class SECS    //MES  
    {
        private HSMSIF oHSMS;
        private StreamFunction oStrFun;
        private delegate void ShowLogHandler(String text);
        private ShowLogHandler ShowLog;
        public Action<string> openProjectAction = null;   //加载程式
        public Func<bool> AStartMachine = null;
        //
        public List<CEID> LsCEID = new List<CEID>();   //event ID 事件ID
        public List<REPID> LsREPID = new List<REPID>();//report ID 
        public List<VID> LsVID = new List<VID>();      //变量ID
        public List<ALID> LsALID = new List<ALID>();   //报警ID 
        public List<ECID> LsECID = new List<ECID>();   //设备常量ID
        public List<SVID> LsSVID = new List<SVID>();   //设备状态变量ID

        private string xmlFilePath = @"Config\SECS\Configure.xml";
        //
        private readonly string _codepath = @"OutStation\";
        private Boolean StartFlag = true;
        public bool BOpen { get => bOpen; }   //打开状态
        private bool bOpen = false;
        public bool IsConnect { get; private set; }  //连接状态
        private Thread tdReceive;

        delegate void SetTextCallback(string text);
        private delegate void InvokeCallBackDelegate();
        private AutoResetEvent aEvent_GetMap = new AutoResetEvent(false);   //获取map sn
        public AutoResetEvent aEvent_OutStation = new AutoResetEvent(false);   //获取到host信息
        public AutoResetEvent aEvent_PPSelect = new AutoResetEvent(false);   //切换程序
        public AutoResetEvent aEvent_UploadOK = new AutoResetEvent(false);   //上抛SN成功
        private List<int> curALID = new List<int>();
        private static object oLock = new object();
        int stationID = 1;
        private EquipmentState equipmentState = EquipmentState.Idle;
        private ISecsGemService _secGemService;
        private IEventAggregator _eventAggregator;
        private IRecipeManagerService _recipeManagerService;
        private bool _isInitialized;
        private EquipmentStatusHub statusHub;

        public SECS ()
        {
            oHSMS = new HSMSIF();
        }
        public void InitializeSECS(ISecsGemService secsGemService,
                                IEventAggregator eventAggregator,
                                IRecipeManagerService recipeManagerService)
        {
            _eventAggregator = eventAggregator;
            _recipeManagerService = recipeManagerService;
            _secGemService = secsGemService;
            InitializeConfiguration();
            // 在设备启动时初始化
            _eventAggregator.GetEvent<SystemInitializedEvent>().Subscribe(InitializeStatusMonitoring);
        }
        public void InitializeConfiguration()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, xmlFilePath);
            if (!oHSMS.Config(configPath))
            {
                IMessage.Logger.Error($"SECS配置文件读取失败: {configPath}");
                return;
            }
            IMessage.Logger.Info($"SECS配置加载成功");
        }

        private void InitializeStatusMonitoring()
        {
            if (_isInitialized) return;

            // 确保此时 XStationManager 已完成初始化
            if (XStationManager.Instance == null)
            {
                IMessage.Logger.Error("XStationManager 仍未初始化，无法启动 SECS 状态监控");
                return;
            }
            try
            {
                // 初始化设备状态监控
                statusHub = new EquipmentStatusHub();
                var hardwareBridge = new EquipmentStateBridge(XStationManager.Instance, stationID);
                statusHub.RegisterSource(hardwareBridge);
                statusHub.StateChanged += (sender, e) =>
                {
                    // 使用日志记录代替直接访问
                    IMessage.Logger.Info($"设备状态变更: {e.NewState} @ {e.ChangeTime:HH:mm:ss}");
                    equipmentState = e.NewState;
                };
                // 获取初始状态
                var current = statusHub.CurrentState;
                IMessage.Logger.Info($"初始设备状态: {current}");
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"SECS状态监控初始化失败: {ex.Message}");
            }
        }

        public bool IniSECS()   //初始化SECS
        {
            try
            {
                LsCEID = (List<CEID>)OpenID("CEID");
                if (LsCEID == null)
                {
                    LsCEID = new List<CEID>();
                    LsCEID.Add(new CEID());
                }
                LsREPID = (List<REPID>)OpenID("REPID");
                if (LsREPID == null)
                {
                    LsREPID = new List<REPID>();
                    LsREPID.Add(new REPID());
                }
                LsVID = (List<VID>)OpenID("VID");
                if (LsVID == null)
                {
                    LsVID = new List<VID>();
                    LsVID.Add(new VID());
                }
                LsALID = (List<ALID>)OpenID("ALID");
                if (LsALID == null)
                {
                    LsALID = new List<ALID>();
                    LsALID.Add(new ALID());
                }
                LsECID = (List<ECID>)OpenID("ECID");
                if (LsECID == null)
                {
                    LsECID = new List<ECID>();
                    LsECID.Add(new ECID());
                }
                LsSVID = (List<SVID>)OpenID("SVID");
                if (LsSVID == null)
                {
                    LsSVID = new List<SVID>();
                    LsSVID.Add(new SVID());
                }
                OpenSecs();// 现场启用
                tdReceive = new Thread(new ThreadStart(Receive));
                tdReceive.Start();
                return true;
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"初始化SECS: {ex.Message}");
                return false;
            }
        }
        public void OpenSecs()
        {
            //open link
            if (oHSMS != null && !bOpen)
            {
                if (oHSMS.Enable("0"))
                {
                    bOpen = !bOpen;
                }
            }
            StartFlag = true;
        }
        public void CloseSecs()
        {
            try
            {
                if (bOpen)
                {
                    oHSMS.Disable("00");
                }
                StartFlag = false;
                IsConnect = false;
                bOpen = false;
            }
            catch (Exception ex)
            {
            }
        }
        public bool Connect()
        {
            MSG_S1F13 s1f13 = new MSG_S1F13();
            s1f13.MDLN = "SECS Test";
            s1f13.SOFTREV = "V1.00";
            s1f13.ToBuffer();
            oHSMS.Response((StreamFunction)s1f13);
            return true;
        }
        private void Setlog(string text)
        {
            IMessage.Logger.Error($"SECS Setlog: {text}");
        }
        public static string Secslist(string st)   //LOG
        {
            lock(oLock)
            {
                string path = AppDomain.CurrentDomain.BaseDirectory + @"Secslist";
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                DateTime MyStartDate = DateTime.Now;

                path = AppDomain.CurrentDomain.BaseDirectory + @"Secslist\" + "SECS" + DateTime.Now.ToString("yyyyMMdd") + ".log";

                if (!File.Exists(path))
                    using (StreamWriter sw = new StreamWriter(path)) { }

                using (StreamWriter sw = File.AppendText(path))
                    sw.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " " + st);

                IMessage.Logger.Info($"SECS2 Log {st}");
                return st;
            }
        }

        bool lastStus = false;
        private void Receive()
        {
            while (StartFlag)
            {
                try
                {
                    if (IsConnect && !lastStus)
                    {
                        IMessage.Logger.Info($"连接SECS,成功!");
                    }
                    else if (!IsConnect && lastStus)
                    {
                        IMessage.Logger.Warn($"连接SECS,超时!");
                    }
                    bool temp = IsConnect;
                    lastStus = temp;
                    IsConnect = oHSMS.Connected();
                    if (OnStatusChangeEvent != null)
                    {
                        OnStatusChangeEvent(IsConnect);
                    }
                    if (!IsConnect)   //未连接
                    {
                        OpenSecs();
                        Thread.Sleep(100);
                        continue;
                    }
                    if (oHSMS.Receive(ref oStrFun, "0"))
                    {
                        Process_MSG();
                    }
                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    IMessage.Logger.Error($"SECS信息处理异常,处理任务终止{ex}");
                }
            }
        }
        private void Process_MSG()
        {
            if (oStrFun.DeviceID != 0)
            {
                MSG_S9F1 s9f1 = new MSG_S9F1();
                string str = "Unrecognized Device ID";
                byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(str);
                s9f1.MHEAD = byteArray;
                s9f1.ToBuffer();
                oHSMS.Response((StreamFunction)s9f1);
                Secslist(s9f1.ToLog());
                return;
            }
            switch (oStrFun.StrmFunc)
            {
                case "S001F000":
                    ON_RCV_S1F0();
                    break;
                case "S001F001":
                    ON_RCV_S1F1();//是否在线
                    break;
                case "S001F002":
                    ON_RCV_S1F2();
                    break;
                case "S001F003":
                    ON_RCV_S1F3();//host查询svid设备状态
                    break;
                case "S001F011":
                    ON_RCV_S1F11();//host查询svid列表名称和单位
                    break;
                case "S001F013":
                    ON_RCV_S1F13();//建立通信
                    break;
                case "S001F014":
                    ON_RCV_S1F14();//host通信答复
                    break;
                case "S001F015":
                    ON_RCV_S1F15();//host让设备离线
                    break;
                case "S001F017":
                    ON_RCV_S1F17();//host让设备上线
                    break;
                case "S002F013":
                    ON_RCV_S2F13();//host获取设备常量ECID
                    break;
                case "S002F015":
                    ON_RCV_S2F15();
                    break;
                case "S002F017":
                    ON_RCV_S2F17();//host获取时间
                    break;
                case "S002F018":
                    ON_RCV_S2F18();
                    break;
                case "S002F021":
                    ON_RCV_S2F21();//host下发远程命令
                    break;
                case "S002F029":
                    ON_RCV_S2F29();//host获取常量的名字、范围、默认值、单位  ECID
                    break;
                case "S002F031":
                    ON_RCV_S2F31();//时间同步
                    break;
                case "S002F033":
                    ON_RCV_S2F33();
                    break;
                case "S002F035":
                    ON_RCV_S2F35();
                    break;
                case "S002F037":
                    ON_RCV_S2F37();
                    break;
                case "S002F041":
                    ON_RCV_S2F41();//host 远程操作设备
                    break;
                case "S005F002":
                    ON_RCV_S5F2();
                    break;
                case "S005F003":
                    ON_RCV_S5F3();//停用/启用告警
                    break;
                case "S005F005":
                    ON_RCV_S5F5();
                    break;
                case "S005F007":
                    ON_RCV_S5F7();
                    break;
                case "S006F012":
                    ON_RCV_S6F12();
                    break;
                case "S006F015":
                    ON_RCV_S6F15();
                    break;
                case "S007F003":
                    ON_RCV_S7F3();
                    break;
                case "S007F004":
                    ON_RCV_S7F4();
                    break;
                case "S007F005":
                    ON_RCV_S7F5();//host请求配方
                    break;
                case "S007F006":
                    ON_RCV_S7F6();//收到host的配方
                    break;
                case "S007F017":
                    ON_RCV_S7F17();//收到host删除配方请求
                    break;
                case "S007F018":
                    ON_RCV_S7F18();//host删除配方后回复设备
                    break;
                case "S007F019":
                    ON_RCV_S7F19();//host请求配方列表
                    break;
                case "S010F003":
                    ON_RCV_S10F3();//显示host信息
                    break;
                
                case "S014F002":
                    ON_RCV_S14F2();//收到mapping
                    break;
                default:
                    On_RCV_Unknown(oStrFun.StrmFunc);
                    break;
            }
        }
        private void ON_RCV_S1F0()//终止业务
        {
            MSG_S1F0 s1f0 = new MSG_S1F0();
            oStrFun.m_BUFFERS.CopyTo(s1f0.m_BUFFERS, 0);
            s1f0.m_Length = oStrFun.m_Length;
            s1f0.FromBuffer();
            Secslist(s1f0.ToLog());
        }
        private void ON_RCV_S1F1()//是否在线
        {
            MSG_S1F1 s1f1 = new MSG_S1F1();
            oStrFun.m_BUFFERS.CopyTo(s1f1.m_BUFFERS, 0);
            s1f1.m_Length = oStrFun.m_Length;
            s1f1.FromBuffer();
            Secslist(s1f1.ToLog());
            MSG_S1F2 s1f2 = new MSG_S1F2();

            s1f2.MDLN = "Rotary Board Machine";   //设备名称
            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string v1 = " V" + version;
            s1f2.SOFTREV = $"{v1}";   //软件版本
            s1f2.ToBuffer();
            Secslist(s1f2.ToLog());
            oHSMS.Response((StreamFunction)s1f2);
        }

        private void ON_RCV_S1F2()
        {
            MSG_S1F2 s1f2 = new MSG_S1F2();
            oStrFun.m_BUFFERS.CopyTo(s1f2.m_BUFFERS, 0);
            s1f2.m_Length = oStrFun.m_Length;
            s1f2.FromBuffer();
            Secslist(s1f2.ToLog());

        }
        private void ON_RCV_S1F3()//host查询svid的值
        {
            MSG_S1F3 s1f3 = new MSG_S1F3();
            oStrFun.m_BUFFERS.CopyTo(s1f3.m_BUFFERS, 0);
            s1f3.m_Length = oStrFun.m_Length;
            s1f3.FromBuffer();
            Secslist(s1f3.ToLog());
            MSG_S1F4 s1f4 = new MSG_S1F4();
            byte Length = s1f3.L_count;
            s1f4.L_count = Length;

            if (Length != 0)
            {
                for (int k1 = 0; k1 < Length; k1++)
                {
                    for (int k2 = 0; k2 < LsSVID.Count; k2++)   //遍历SVID
                    {
                        if (s1f3.arrSVID[k1].ToString() == LsSVID[k2].ID.ToString())
                        {
                            switch (LsSVID[k2].ID.ToString())
                            {
                                case "1"://时间
                                    s1f4.arrSV.Add(DateTime.Now.ToString("yyMMddHHmmss"));
                                    break;
                                case "2"://控制模式
                                    s1f4.arrSV.Add((byte)_secGemService.controlMode);
                                    break;
                                case "3"://状态
                                    if (statusHub.CurrentState == EquipmentState.Idle) //IDLE
                                        s1f4.arrSV.Add((byte)0);
                                    if (statusHub.CurrentState == EquipmentState.Running) //RUN
                                        s1f4.arrSV.Add((byte)1);
                                    if (statusHub.CurrentState == EquipmentState.Alarm)  //ALARM
                                        s1f4.arrSV.Add((byte)3);
                                    else
                                        s1f4.arrSV.Add((byte)2);  //STOP
                                    break;
                                case "4":
                                    s1f4.arrSV.Add(_secGemService.OutUnitCode);//产品SN
                                    break;
                                case "5":
                                     s1f4.arrSV.Add(_secGemService.OutUnitCode);//出站码
                                    break;
                                case "6":
                                    s1f4.arrSV.Add(_secGemService.UPH);//uph  IniStatus.Instance.UPH
                                    break;
                                case "7":
                                    s1f4.arrSV.Add(_secGemService.CurrentRecipe);//当前配方 niStatus.Instance.CurProduct
                                    break;
                            }

                        }
                    }
                }
            }
            else
            {
                for (int k1 = 0; k1 < Length; k1++)
                {
                    for (int k2 = 0; k2 < LsSVID.Count; k2++)   //遍历SVID
                    {
                        if (s1f3.arrSVID[k1].ToString() == LsSVID[k2].ID.ToString())
                        {
                            switch (LsSVID[k2].ID.ToString())
                            {
                                case "1"://时间
                                    s1f4.arrSV.Add(DateTime.Now.ToString("yyMMddHHmmss"));
                                    break;
                                case "2"://状态
                                    s1f4.arrSV.Add((byte)_secGemService.controlMode);   //控制模式
                                    break;

                                case "3":
                                    if (statusHub.CurrentState == EquipmentState.Idle) //IDLE
                                        s1f4.arrSV.Add((byte)0);
                                    if (statusHub.CurrentState == EquipmentState.Running)  //RUN
                                        s1f4.arrSV.Add((byte)1);
                                    if (statusHub.CurrentState == EquipmentState.Alarm)  //ALARM
                                        s1f4.arrSV.Add((byte)3);
                                    else
                                        s1f4.arrSV.Add((byte)2);  //STOP
                                    break;
                                case "4":
                                    s1f4.arrSV.Add(_secGemService.OutUnitCode);//产品SN
                                    break;
                                case "5":
                                    s1f4.arrSV.Add(_secGemService.OutUnitCode);//产品SN
                                    break;
                                case "6":
                                    s1f4.arrSV.Add(_secGemService.UPH);//uph
                                    break;
                                case "7":
                                    s1f4.arrSV.Add(_secGemService.CurrentRecipe);//当前 recipe
                                    break;

                            }

                        }
                    }
                }
            }
            s1f4.ToBuffer();
            Secslist(s1f4.ToLog());
            oHSMS.Response((StreamFunction)s1f4);
        }

        private void ON_RCV_S1F11()//HOST查询svid列表名称和单位
        {
            MSG_S1F11 s1f11 = new MSG_S1F11();
            oStrFun.m_BUFFERS.CopyTo(s1f11.m_BUFFERS, 0);
            s1f11.m_Length = oStrFun.m_Length;
            s1f11.FromBuffer();
            Secslist(s1f11.ToLog());
            MSG_S1F12 s1f12 = new MSG_S1F12();
            byte Length = 0;//s1f11.L_count;
            s1f12.L_count = Length;

            if (Length != 0)
            {
                for (int k1 = 0; k1 < Length; k1++)
                {
                    s1f12.arrSVID.Add(s1f11.arrSVID[k1]);
                    for (int k2 = 0; k2 < LsSVID.Count; k2++)
                    {
                        if (s1f11.arrSVID[k1].ToString() == LsSVID[k2].ID.ToString())
                        {
                            s1f12.arrSVNAME.Add(LsSVID[k2].Discription);
                            s1f12.arrUNITS.Add(LsSVID[k2].Units.ToString());   //单位
                        }
                    }
                }
            }
            else
            {
                for (int k2 = 0; k2 < LsSVID.Count; k2++)
                {
                    s1f12.arrSVID.Add(LsSVID[k2].ID.ToString());
                    s1f12.arrSVNAME.Add(LsSVID[k2].Discription);
                    s1f12.arrUNITS.Add(LsSVID[k2].Units.ToString());   //单位
                }
                s1f12.L_count = (byte)(LsSVID.Count);

            }
            s1f12.ToBuffer();
            Secslist(s1f12.ToLog());
            oHSMS.Response((StreamFunction)s1f12);

        }

        private void ON_RCV_S1F13()//建立通信
        {
            MSG_S1F13 s1f13 = new MSG_S1F13();
            oStrFun.m_BUFFERS.CopyTo(s1f13.m_BUFFERS, 0);
            s1f13.m_Length = oStrFun.m_Length;
            s1f13.FromBuffer();
            String MDLN = s1f13.MDLN;
            String SOFT = s1f13.SOFTREV;
            Secslist(s1f13.ToLog());

            MSG_S1F14 s1f14 = new MSG_S1F14();

            s1f14.MDLN = "DialPin Machine";
            string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string v1 = " V" + version;
            s1f14.SOFTREV = v1;
            s1f14.COMMACK = 0;
            s1f14.ToBuffer();
            Secslist(s1f14.ToLog());
            oHSMS.Response((StreamFunction)s1f14);

        }
        private void ON_RCV_S1F14()//host建立通信答复
        {
            MSG_S1F14 s1f14 = new MSG_S1F14();
            oStrFun.m_BUFFERS.CopyTo(s1f14.m_BUFFERS, 0);
            s1f14.m_Length = oStrFun.m_Length;
            s1f14.FromBuffer();
            String MDLN = s1f14.MDLN;
            String SOFT = s1f14.SOFTREV;
            Secslist(s1f14.ToLog());
        }

        #region s1f15   //HOST让设备离线
        private void ON_RCV_S1F15()
        {
            MSG_S1F15 s1f15 = new MSG_S1F15();
            oStrFun.m_BUFFERS.CopyTo(s1f15.m_BUFFERS, 0);
            s1f15.m_Length = oStrFun.m_Length;
            s1f15.FromBuffer();
            Secslist(s1f15.ToLog());
            if (true)
            {
                MSG_S1F16 s1f16 = new MSG_S1F16();
                s1f16.OFLACK = 0;
                s1f16.ToBuffer();
                Secslist(s1f16.ToLog());
                oHSMS.Response((StreamFunction)s1f16);
                //EFEMFileStatus("Connect,1,1", "Secs_Status");
            }
            else
            {
                MSG_S1F16 s1f16 = new MSG_S1F16();
                s1f16.OFLACK = 0;
                s1f16.ToBuffer();
                Secslist(s1f16.ToLog());
                oHSMS.Response((StreamFunction)s1f16);
                //this.BeginInvoke(new SetTextCallback(offine), new object[] { "" });

                //label101.Text = "OFFLINE";
            }

        }

        private void offine(string str)
        {
            //label81.Text = "Offline";
        }

        #endregion

        #region s1f17   //HOST让设备上线
        private void ON_RCV_S1F17()
        {
            MSG_S1F17 s1f17 = new MSG_S1F17();
            oStrFun.m_BUFFERS.CopyTo(s1f17.m_BUFFERS, 0);
            s1f17.m_Length = oStrFun.m_Length;
            s1f17.FromBuffer();
            Secslist(s1f17.ToLog());
            MSG_S1F18 s1f18 = new MSG_S1F18();
            if (true)
            {
                s1f18.ONLACK = 2;//设备已经在线
            }
            else
            {
                //this.BeginInvoke(new SetTextCallback(onine), new object[] { "" });

                s1f18.ONLACK = 0;


            }
            s1f18.ToBuffer();
            Secslist(s1f18.ToLog());
            oHSMS.Response((StreamFunction)s1f18);

        }
        #endregion
        private void ON_RCV_S2F13()//HOST获取设备常量   ECID
        {
            MSG_S2F13 s2f13 = new MSG_S2F13();
            oStrFun.m_BUFFERS.CopyTo(s2f13.m_BUFFERS, 0);
            s2f13.m_Length = oStrFun.m_Length;
            s2f13.FromBuffer();
            Secslist(s2f13.ToLog());
            MSG_S2F14 s2f14 = new MSG_S2F14();
            byte Length = s2f13.L_count;
            s2f14.L_count = Length;
            if (Length != 0)
            {
                for (int k1 = 0; k1 < Length; k1++)
                {
                    for (int k2 = 0; k2 < LsECID.Count; k2++)
                    {
                        if (s2f13.arrECID[k1].ToString() == LsECID[k2].ID.ToString())
                        {
                            switch (LsECID[k2].ID.ToString())
                            {
                                case "1":  //ID =1
                                    s2f14.arrECV.Add(LsECID[k2].Value.ToString());
                                    break;
                                case "2":  //ID=2
                                    s2f14.arrECV.Add(LsECID[k2].Value.ToString());
                                    break;
                            }
                        }
                    }
                }
            }
            else
            {
                for (int k2 = 0; k2 < LsECID.Count; k2++)
                {
                    switch (LsECID[k2].ID.ToString())
                    {
                        case "1":  //ID =1
                            s2f14.arrECV.Add(LsECID[k2].Value.ToString());
                            break;
                        case "2":  //ID=2
                            s2f14.arrECV.Add(LsECID[k2].Value.ToString());
                            break;
                    }
                }
                s2f14.L_count = (byte)(LsECID.Count);
            }
            s2f14.ToBuffer();
            Secslist(s2f14.ToLog());
            //int SystemByte = oHSMS.Request((StreamFunction)s2f14);
            oHSMS.Response((StreamFunction)s2f14);
        }
        private void ON_RCV_S2F15()//host定义常量  ECID      //暂时没用 ***********************************
        {
            MSG_S2F15 s2f15 = new MSG_S2F15();
            oStrFun.m_BUFFERS.CopyTo(s2f15.m_BUFFERS, 0);
            s2f15.m_Length = oStrFun.m_Length;
            s2f15.FromBuffer();
            Secslist(s2f15.ToLog());
            MSG_S2F16 s2f16 = new MSG_S2F16();
            /* 0 =确认
             1 =拒绝，至少一个常数不存在。
             2 =拒绝，忙
             3 =拒绝，至少一个常数超出范围。
             > 3其他特定于设备的错误
             4-63保留*/
            s2f16.ECV = 0;

            for (int k1 = 0; k1 < s2f15.arrECID.Count; k1++)
            {
                for (int k2 = 0; k2 < LsECID.Count; k2++)
                {
                    if (s2f15.arrECID[k1].ToString() == LsECID[k2].ID.ToString())
                    {
                        s2f16.ECV = 0;
                        break;
                    }
                    else
                    {
                        s2f16.ECV = 1;
                    }
                }
            }

            if (s2f16.ECV == 0)
            {
                for (int k1 = 0; k1 < s2f15.arrECID.Count; k1++)
                {
                    //for (int i = 0; i < LsECID.Count; i++)
                    //{
                    //    if (LsECID[i].ID == s2f15.arrECID[k1].ToString())
                    //    {

                    //        //LsECID.RemoveAt(i);
                    //        //break;
                    //    }
                    //}
                    //for (int i = 0; i < LsECID.Count; i++)
                    //{
                    //    if (s2f15.arrECID[k1].ToString() == LsECID[i].ID)
                    //    {
                    //        ECID ecid = new ECID();
                    //        ecid.Discription = LsECID[i].Discription;
                    //        ecid.ID = LsECID[i].ID;
                    //        LsECID.Add(ecid);
                    //    }

                    //}
                    //DeleteREP("ECID", s2f15.arrECID[k1].ToString());
                    //LsECID = new List<ECID>();   //重新定义ecid    后期可从文件读取创建
                    //for (int k2 = 0; k2 < dataGridView5.Rows.Count - 1; k2++)
                    //{
                    //    if (s2f15.arrECID[k1].ToString() == dataGridView5.Rows[k2].Cells[0].Value.ToString())
                    //    {
                    //        Createfile(dataGridView5.Rows[k2].Cells[1].Value.ToString(), "ECID", s2f15.arrECID[k1].ToString());
                    //        Createfile(s2f15.arrECV[k1].ToString(), "ECID", s2f15.arrECID[k1].ToString());
                    //    }
                    //}
                }
            }

            s2f16.ToBuffer();
            Secslist(s2f16.ToLog());
            int SystemByte = oHSMS.Request((StreamFunction)s2f16);
            //this.BeginInvoke(new SetTextCallback(reopenfile), new object[] { "ECID" });
        }
        #region s2f17  //host获取时间
        private void ON_RCV_S2F17()
        {
            MSG_S2F17 s2f17 = new MSG_S2F17();
            oStrFun.m_BUFFERS.CopyTo(s2f17.m_BUFFERS, 0);
            s2f17.m_Length = oStrFun.m_Length;
            s2f17.FromBuffer();
            Secslist(s2f17.ToLog());

            MSG_S2F18 s2f18 = new MSG_S2F18();
            //YYMMDDhhmmss    YYYYMMDDhhmmsscc
            s2f18.DATETIME = DateTime.Now.ToString("yyMMddHHmmss");
            s2f18.ToBuffer();
            Secslist(s2f18.ToLog());
            oHSMS.Response((StreamFunction)s2f18);
            //int SystemByte = oHSMS.Request((StreamFunction)s2f18);
        }

        private void ON_RCV_S2F18()
        {
            MSG_S2F18 s2f18 = new MSG_S2F18();
            oStrFun.m_BUFFERS.CopyTo(s2f18.m_BUFFERS, 0);
            s2f18.m_Length = oStrFun.m_Length;
            s2f18.FromBuffer();
            Secslist(s2f18.ToLog());
        }
        #endregion
        private void ON_RCV_S2F21()//HOST下发远程命令
        {
            MSG_S2F21 s2f21 = new MSG_S2F21();
            oStrFun.m_BUFFERS.CopyTo(s2f21.m_BUFFERS, 0);
            s2f21.m_Length = oStrFun.m_Length;
            s2f21.FromBuffer();
            Secslist(s2f21.ToLog());

            MSG_S2F22 s2f22 = new MSG_S2F22();
            /*0 =完成或完成
              1 =命令不存在
              2 =现在无法执行
              > 2 =其他设备-特定错误
              3-63保留*/
            s2f22.CMDA = 2;
            s2f22.ToBuffer();
            Secslist(s2f21.ToLog());
            oHSMS.Response((StreamFunction)s2f22);
            //int SystemByte = oHSMS.Request((StreamFunction)s2f22);
        }
        #region s2f29
        private void ON_RCV_S2F29()//host获取常量的名字、范围、默认值、单位  ECID
        {
            MSG_S2F29 s2f29 = new MSG_S2F29();
            oStrFun.m_BUFFERS.CopyTo(s2f29.m_BUFFERS, 0);
            s2f29.m_Length = oStrFun.m_Length;
            s2f29.FromBuffer();
            Secslist(s2f29.ToLog());

            MSG_S2F30 s2f30 = new MSG_S2F30();
            byte Length = s2f29.L_count;
            s2f30.L_count = Length;

            if (Length != 0)
            {
                for (int k1 = 0; k1 < Length; k1++)
                {
                    s2f30.arrECID.Add(s2f29.arrECID[k1]);
                    for (int k2 = 0; k2 < LsECID.Count; k2++)
                    {
                        if (s2f29.arrECID[k1].ToString() == LsECID[k2].ID)
                        {
                            s2f30.arrECNAME.Add(LsECID[k2].Discription.ToString());   //名字
                            if (LsECID[k2].Min != "")     //最小值
                            {
                                s2f30.arrECMIN.Add((uint)Int32.Parse(LsECID[k2].Min));
                            }
                            else
                            {
                                s2f30.arrECMIN.Add((uint)0);
                            }
                            if (LsECID[k2].Max != "")
                            {
                                s2f30.arrECMAX.Add((uint)Int32.Parse(LsECID[k2].Max));
                            }
                            else
                            {
                                s2f30.arrECMAX.Add((uint)0);
                            }
                            if (LsECID[k2].Ecdef != "")  //标准值
                            {
                                s2f30.arrECDEF.Add("0");
                                //if (LsECID[k2].Ecdef == "1")
                                //    s2f30.arrECDEF.Add("mm");
                                //if (LsECID[k2].Ecdef == "2")
                                //    s2f30.arrECDEF.Add("V1.00");
                            }
                            else
                            {
                                s2f30.arrECDEF.Add("");
                            }
                            if (LsECID[k2].Unit != null && LsECID[k2].Unit != "")    //单位
                                s2f30.arrUNITS.Add(LsECID[k2].Unit);
                            else
                                s2f30.arrUNITS.Add("");
                        }
                    }
                }
            }
            else
            {
                for (int k2 = 0; k2 < LsECID.Count; k2++)
                {
                    if (LsECID[k2].ID != "")
                    {
                        s2f30.arrECID.Add((uint)Int32.Parse(LsECID[k2].ID));
                    }

                    s2f30.arrECNAME.Add(LsECID[k2].Discription);    //添加变量名称
                    if (LsECID[k2].Min != "")
                    {
                        s2f30.arrECMIN.Add(LsECID[k2].Min); //(uint)Int32.Parse(
                    }
                    else
                    {
                        s2f30.arrECMIN.Add("0");  //(uint)0
                    }
                    if (LsECID[k2].Max != "")
                    {
                        s2f30.arrECMAX.Add(LsECID[k2].Max);
                    }
                    else
                    {
                        s2f30.arrECMAX.Add("0");
                    }
                    if (LsECID[k2].Ecdef != "")
                    {
                        s2f30.arrECDEF.Add(LsECID[k2].Ecdef);
                        //if (dataGridView5.Rows[k2].Cells[0].Value.ToString() == "1")
                        //    s2f30.arrECDEF.Add("ABT2000");
                        //if (dataGridView5.Rows[k2].Cells[0].Value.ToString() == "2")
                        //    s2f30.arrECDEF.Add("V1.00");
                    }
                    else
                    {
                        s2f30.arrECDEF.Add("");
                    }
                    if (LsECID[k2].Unit != null && LsECID[k2].Unit != "")    //单位
                        s2f30.arrUNITS.Add(LsECID[k2].Unit);
                    else
                        s2f30.arrUNITS.Add("");
                }
                s2f30.L_count = (byte)(LsECID.Count);
            }
            s2f30.ToBuffer();
            Secslist(s2f30.ToLog());
            int SystemByte = oHSMS.Request((StreamFunction)s2f30);
        }
        #endregion
        #region s2f31
        private void ON_RCV_S2F31()//时间同步
        {
            MSG_S2F31 s2f31 = new MSG_S2F31();
            oStrFun.m_BUFFERS.CopyTo(s2f31.m_BUFFERS, 0);
            s2f31.m_Length = oStrFun.m_Length;
            s2f31.FromBuffer();
            Secslist(s2f31.ToLog());

            MSG_S2F32 s2f32 = new MSG_S2F32();
            SetTime(s2f31.DATETIME);
            s2f32.TIACK = 0;
            s2f32.ToBuffer();
            Secslist(s2f32.ToLog());
            oHSMS.Response((StreamFunction)s2f32);
        }
        [DllImport("Kernel32.dll")]
        private extern static uint SetLocalTime(ref SYSTEMTIME lpSystemTime);

        [DllImport("Kernel32.dll")]
        private extern static uint GetLocalTime(ref SYSTEMTIME lpSystemTime);
        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }
        public void SetTime(string SqlServerTime)
        {
            SYSTEMTIME st = new SYSTEMTIME();
            st.wYear = Convert.ToUInt16(SqlServerTime.ToString().Substring(0, 4));
            st.wMonth = Convert.ToUInt16(SqlServerTime.ToString().Substring(4, 2));
            st.wDay = Convert.ToUInt16(SqlServerTime.ToString().Substring(6, 2));
            st.wHour = Convert.ToUInt16(SqlServerTime.ToString().Substring(8, 2));
            st.wMinute = Convert.ToUInt16(SqlServerTime.ToString().Substring(10, 2));
            st.wSecond = Convert.ToUInt16(SqlServerTime.ToString().Substring(11, 2));
            SetLocalTime(ref st);
        }
        #endregion

        private void ON_RCV_S2F33()//Host定义事件  VID   Delete Report***********************************************
        {
            MSG_S2F33 s2f33 = new MSG_S2F33();
            oStrFun.m_BUFFERS.CopyTo(s2f33.m_BUFFERS, 0);
            s2f33.m_Length = oStrFun.m_Length;
            s2f33.FromBuffer();
            Secslist(s2f33.ToLog());
            try
            {
                MSG_S2F34 s2f34 = new MSG_S2F34();
                int m_svid = 0;
                if (s2f33.L_count1 != 0 && s2f33.L_count3 != 0)
                {
                    for (int k0 = 0; k0 < s2f33.L_count1; k0++)
                    {
                        for (int k1 = 0; k1 < int.Parse(s2f33.arrL_count3[k0].ToString()); k1++)
                        {
                            for (int k2 = 0; k2 < LsVID.Count; k2++)
                            {
                                if (s2f33.arrVID[k1 + m_svid].ToString() == LsVID[k2].ID)    //查询是否有这个事件
                                {
                                    s2f34.DRACK = 0;
                                    break;
                                }
                                else
                                {
                                    s2f34.DRACK = 4;
                                }
                            }
                        }
                        m_svid = m_svid + int.Parse(s2f33.arrL_count3[k0].ToString());
                    }
                    m_svid = 0;
                    bool bhave = false;
                    if (s2f34.DRACK != 4)
                    {
                        for (int k1 = 0; k1 < s2f33.arrRPTID.Count; k1++)
                        {
                            bhave = false;
                            for (int i = 0; i < LsREPID.Count; i++)
                            {
                                if (LsREPID[i].ID == s2f33.arrRPTID[k1].ToString())
                                {
                                    for (int k2 = 0; k2 < int.Parse(s2f33.arrL_count3[k1].ToString()); k2++)
                                    {
                                        if(!LsREPID[i].LsVID.Contains(s2f33.arrVID[k2 + m_svid].ToString()))
                                        {
                                            LsREPID[i].LsVID.Add(s2f33.arrVID[k2 + m_svid].ToString());
                                        }
                                    }
                                    m_svid = m_svid + int.Parse(s2f33.arrL_count3[k1].ToString());
                                    bhave = true;
                                }
                            }
                            if(!bhave)
                            {
                                REPID repid = new REPID();
                                repid.ID = s2f33.arrRPTID[k1].ToString();
                                for (int k2 = 0; k2 < int.Parse(s2f33.arrL_count3[k1].ToString()); k2++)
                                {
                                    repid.LsVID.Add(s2f33.arrVID[k2 + m_svid].ToString());
                                }
                                LsREPID.Add(repid);
                                m_svid = m_svid + int.Parse(s2f33.arrL_count3[k1].ToString());
                                
                            }
                        }
                    }
                }
                else
                {
                    s2f34.DRACK = 2;
                }
                SaveID("REPID");
                s2f34.ToBuffer();
                Secslist(s2f34.ToLog());
                oHSMS.Response((StreamFunction)s2f34);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        #region s2f35
        private void ON_RCV_S2F35()//host链接事件id和REPID  **************************************
        {
            MSG_S2F35 s2f35 = new MSG_S2F35();
            oStrFun.m_BUFFERS.CopyTo(s2f35.m_BUFFERS, 0);
            s2f35.m_Length = oStrFun.m_Length;
            s2f35.FromBuffer();
            Secslist(s2f35.ToLog());
            try
            {
                int m_rtpid = 0;
                MSG_S2F36 s2f36 = new MSG_S2F36();
                if (s2f35.L_count3 != 0)
                {
                    for (int k1 = 0; k1 < s2f35.arrCEID.Count; k1++)
                    {
                        for (int k2 = 0; k2 < LsCEID.Count; k2++)
                        {
                            if (s2f35.arrCEID[k1].ToString() == LsCEID[k2].ID)   //查询是否存在
                            {
                                for (int k3 = 0; k3 < int.Parse(s2f35.arrL_count3[k1].ToString()); k3++)
                                {
                                    if (!LsCEID[k2].LsRepid.Contains(s2f35.arrRPTID[k3+ m_rtpid].ToString()))
                                    {
                                        LsCEID[k2].LsRepid.Add(s2f35.arrRPTID[k3 + m_rtpid].ToString());
                                    }
                                }
                                m_rtpid = m_rtpid + int.Parse(s2f35.arrL_count3[k1].ToString());
                                s2f36.LRACK = 0;//接受
                                break;
                            }
                            else
                            {
                                s2f36.LRACK = 4;//未找到CEID
                            }
                        }
                    }
                }
                else
                {
                    //Deletefiel("REPID");
                    s2f36.LRACK = 2;//长度误差
                }
                SaveID("CEID");   //保存ceid的更改
                s2f36.ToBuffer();
                Secslist(s2f36.ToLog());
                oHSMS.Response((StreamFunction)s2f36);
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }
        #endregion
        #region s2f37
        private void ON_RCV_S2F37()//Enable/Disable 事件上报   此消息的目的是让主机启用或禁用  报告一组事件 （CEID）
        {
            MSG_S2F37 s2f37 = new MSG_S2F37();
            oStrFun.m_BUFFERS.CopyTo(s2f37.m_BUFFERS, 0);
            s2f37.m_Length = oStrFun.m_Length;
            s2f37.FromBuffer();
            Secslist(s2f37.ToLog());
            try
            {
                MSG_S2F38 s2f38 = new MSG_S2F38();
                byte Length = s2f37.L_count1;   //CEIDCOUNT=0禁用全部CEID事件
                string m_ceed = "";
                if (Length != 0)
                {
                    for (int k1 = 0; k1 < s2f37.arrCEID.Count; k1++)
                    {
                        for (int k2 = 0; k2 < LsCEID.Count; k2++)
                        {
                            if (s2f37.arrCEID[k1].ToString() == LsCEID[k2].ID)
                            {
                                s2f38.ERACK = 0;
                                break;
                            }
                            else
                            {
                                s2f38.ERACK = 4;//未找到CEID
                            }
                        }
                    }

                    if (s2f38.ERACK == 0)
                    {
                        s2f38.ERACK = 0;
                        if (s2f37.CEED)
                        { m_ceed = "T"; }
                        else
                        { m_ceed = "F"; }
                        int count = s2f37.arrCEID.Count;
                        for (int k3 = 0; k3 < count; k3++)
                        {
                            for (int k2 = 0; k2 < LsCEID.Count; k2++)
                            {
                                if (s2f37.arrCEID[k3].ToString() == LsCEID[k2].ID)
                                {
                                    LsCEID[k2].M_Ceed = m_ceed;
                                }
                            }
                        }
                    }
                }
                else
                {
                    s2f38.ERACK = 0;
                    if (s2f37.CEED)
                    { m_ceed = "T"; }//启用
                    else
                    { m_ceed = "F"; }//禁用
                    for (int k2 = 0; k2 < LsCEID.Count; k2++)
                    {
                        LsCEID[k2].M_Ceed = m_ceed;
                    }
                }
                SaveID("CEID");
                s2f38.ToBuffer();
                Secslist(s2f38.ToLog());
                oHSMS.Response((StreamFunction)s2f38);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        #endregion
        #region s2f41
        private void ON_RCV_S2F41()//HOST Command  主机请求设备执行指定的远程命令  与关联的参数
        {
            MSG_S2F41 s2f41 = new MSG_S2F41();
            oStrFun.m_BUFFERS.CopyTo(s2f41.m_BUFFERS, 0);
            s2f41.m_Length = oStrFun.m_Length;
            s2f41.FromBuffer();
            Secslist(s2f41.ToLog());
            MSG_S2F42 s2f42 = new MSG_S2F42();
            s2f42.L_count = 2;
            s2f42.HCACK = 0;
            s2f42.L_count1 = 0;
            if (s2f41.RCMD == "Upload-ok")
            {
                //上抛SN成功
                aEvent_UploadOK.Set();
                s2f42.ToBuffer();
                Secslist(s2f42.ToLog());
                oHSMS.Response((StreamFunction)s2f42);
                IMessage.Logger.Info($"【SEC/GEM】EAP上抛SN成功");
            }
            else if (s2f41.RCMD == "PP-SELECT")    //远程命令 切换配方
            {
                string val = s2f41.CPVAL.Trim(' '); // 配方名称
                IMessage.Logger.Info($"【SEC/GEM】EAP请求切换配方:{val}");
                if (string.IsNullOrEmpty(_secGemService.RecipeList.Find(x => x == val)))
                {
                    s2f42.HCACK = 1;
                    s2f42.ToBuffer();
                    Secslist(s2f42.ToLog());
                    oHSMS.Response((StreamFunction)s2f42);

                    IMessage.Logger.Warn($"【SEC/GEM】EAP请求切换配方失败,不存在此配方:{val}");
                    // 避免 UI 阻塞
                    Task.Run(() =>
                    {
                        int? result = DialogService.ShowBlockingDialog(
                              title: "远程命令",
                              message: $"EAP请求切换配方失败,不存在此配方:{val} \r\nRECV:{s2f42.ToLog()}",
                              yesButtonText: "确定",
                              noButtonText: "忽略",
                               showNoButton: false,
                              icon: PackIconKind.ClockAlert
                        );
                    });
                }
                else
                {
                    if (statusHub.CurrentState != EquipmentState.Idle)
                    {
                        s2f42.HCACK = 1; // 拒绝 - 设备忙
                        Task.Run(() =>
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                int? result = DialogService.ShowBlockingDialog(
                                      title: "远程命令",
                                      message: $"【SEC/GEM】EAP请求切换配方:{val},设备不在IDLE状态 \r\nRECV:{s2f42.ToLog()}",
                                      yesButtonText: "确定",
                                      noButtonText: "取消",
                                      showYesButton: true,
                                      showNoButton: false,
                                      icon: PackIconKind.ClockAlert
                                   );
                            });
                        });
                    }
                    else
                    {
                        // 使用配方服务切换（而不是直接发布事件）
                        bool success = _recipeManagerService.SwitchRecipe(val);
                        s2f42.HCACK = success ? (byte)0 : (byte)1;
                    }

                    s2f42.ToBuffer();
                    oHSMS.Response((StreamFunction)s2f42);
                    Secslist(s2f42.ToLog());

                    if (s2f42.HCACK == 0)
                    {
                        IMessage.Logger.Info($"【SEC/GEM】EAP成功切换配方: {val}");
                        // 事件将由RecipeManagerService自动发布
                    }
                }
            }
            else if (s2f41.RCMD == "Hold")
            {
                XMachine.Instance.HostToEqpHoldMachine = true;
                IMessage.Logger.Info($"RECV EAP->EQP 通知设备Hold");
                // 发布Hold事件
                _eventAggregator.GetEvent<SecsCommandEvent>()
                    .Publish(new SecsCommandParameter
                    {
                        CommandType = SecsCommandType.Hold,
                        LogMessage = $"RECV Host->EQP 通知设备Hold"
                    });
                s2f42.ToBuffer();
                Secslist(s2f42.ToLog());
                oHSMS.Response((StreamFunction)s2f42);

                Task.Run(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        int? result = DialogService.ShowBlockingDialog(
                           title: "远程命令",
                           message: $"【SEC/GEM】远程Host通知设备Hold \r\nRECV:{s2f42.ToLog()}",
                           yesButtonText: "确定",
                           noButtonText: "取消",
                           showYesButton: true,
                           showNoButton: false,
                           icon: PackIconKind.ClockAlert
                        );
                    });
                });
            }
            else if (s2f41.RCMD == "Release")
            {
                XMachine.Instance.HostToEqpHoldMachine = false;
                s2f42.ToBuffer();
                Secslist(s2f42.ToLog());
                oHSMS.Response((StreamFunction)s2f42);
                IMessage.Logger.Warn($"RECV Host->EQP 通知设备Release");
                Task.Run(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        int? result = DialogService.ShowBlockingDialog(
                             title: "远程命令",
                             message: $"【SEC/GEM】远程Host通知设备Release \r\nRECV:{s2f42.ToLog()}",
                             yesButtonText: "继续",
                             noButtonText: "暂停",
                             showYesButton: true,
                             showNoButton: true,
                             icon: PackIconKind.ClockAlert
                         );
                        if (result == 0)
                        {
                            // 发布Release事件
                            _eventAggregator.GetEvent<SecsCommandEvent>()
                                .Publish(new SecsCommandParameter
                                {
                                    CommandType = SecsCommandType.Release,
                                    LogMessage = $"RECV Host->EQP 通知设备Release"
                                });
                            IMessage.Logger.Warn($"【Hold弹窗按钮】继续(Host->EQP 通知设备Release)");
                        }
                        else
                        {
                            // 发布Hold事件
                            _eventAggregator.GetEvent<SecsCommandEvent>()
                                .Publish(new SecsCommandParameter
                                {
                                    CommandType = SecsCommandType.Hold,
                                    LogMessage = $"RECV Host->EQP 通知设备Hold"
                                });
                            IMessage.Logger.Warn($"【Hold弹窗按钮】暂停(Host->EQP 通知设备Release)");
                        }
                    });
                });
            }
            else if (s2f41.RCMD == "Stop")
            {
                s2f42.ToBuffer();
                Secslist(s2f42.ToLog());
                oHSMS.Response((StreamFunction)s2f42);
                IMessage.Logger.Info($"RECV Host->EQP 通知设备Stop");
                // 发布Stop事件
                _eventAggregator.GetEvent<SecsCommandEvent>()
                    .Publish(new SecsCommandParameter
                    {
                        CommandType = SecsCommandType.Stop,
                        LogMessage = $"RECV Host->EQP 通知设备Stop"
                    });
                Task.Run(() =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        int? result = DialogService.ShowBlockingDialog(
                                title: "远程命令",
                                message: $"【SEC/GEM】远程Host通知设备Stop \r\nRECV:{s2f42.ToLog()}",
                                yesButtonText: "确定",
                                noButtonText: "取消",
                                showYesButton: true,
                                showNoButton: false,
                                icon: PackIconKind.ClockAlert
                        );
                    });
                });
            }
            else if (s2f41.RCMD == "Start")
            {
                IMessage.Logger.Info($"RECV Host->EQP 通知设备Start");
                if (statusHub.CurrentState == EquipmentState.Idle)
                {
                    s2f42.HCACK = 1;
                    s2f42.ToBuffer();
                    Secslist(s2f42.ToLog());
                    oHSMS.Response((StreamFunction)s2f42);
                    IMessage.Logger.Warn($"RECV Host->EQP 通知设备Start,设备没有复位,不能启动!");
                }
                if (statusHub.CurrentState == EquipmentState.Running)
                {
                    s2f42.HCACK = 0;
                    s2f42.ToBuffer();
                    Secslist(s2f42.ToLog());
                    oHSMS.Response((StreamFunction)s2f42);
                    IMessage.Logger.Warn($"RECV Host->EQP 通知设备Start,设备已经运行!");
                }
                if (statusHub.CurrentState == EquipmentState.Idle)
                {
                    Task.Run(() =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            int? result = DialogService.ShowBlockingDialog(
                                  title: "远程命令",
                                  message: $"【SEC/GEM】远程Host通知设备Start \r\nRECV:{s2f42.ToLog()}",
                                  yesButtonText: "启动",
                                  noButtonText: "停止",
                                  icon: PackIconKind.ClockAlert
                            );
                            if (result == 0)
                            {
                                // 发布Start事件
                                _eventAggregator.GetEvent<SecsCommandEvent>()
                                    .Publish(new SecsCommandParameter
                                    {
                                        CommandType = SecsCommandType.Start,
                                        LogMessage = $"RECV Host->EQP 通知设备Start"
                                    });
                                s2f42.HCACK = 0;
                                s2f42.ToBuffer();
                                Secslist(s2f42.ToLog());
                                oHSMS.Response((StreamFunction)s2f42);
                            }
                            else
                            {
                                // 发布Stop事件
                                _eventAggregator.GetEvent<SecsCommandEvent>()
                                    .Publish(new SecsCommandParameter
                                    {
                                        CommandType = SecsCommandType.Stop,
                                        LogMessage = $"RECV Host->EQP 通知设备Stop"
                                    });
                                s2f42.HCACK = 1;
                                s2f42.ToBuffer();
                                Secslist(s2f42.ToLog());
                                oHSMS.Response((StreamFunction)s2f42);
                            }
                        });
                    });
                }
            }
            else
            {
                s2f42.HCACK = 1;
                s2f42.ToBuffer();
                Secslist(s2f42.ToLog());
                oHSMS.Response((StreamFunction)s2f42);
            }
        }
        #endregion
        public void s5f1(uint text)//报警上传
        {
            MSG_S5F1 s5f1 = new MSG_S5F1();

            for (int k1 = 0; k1 < LsALID.Count; k1++)
            {
                if (LsALID[k1].ID == text.ToString())
                {
                    if (LsALID[k1].M_Ceed == "T")
                    {
                        s5f1.ALCD = (byte)(Int32.Parse(LsALID[k1].ALCD) + 128); //128 第8bit
                        s5f1.ALID = (uint)Int32.Parse(LsALID[k1].ID);
                        //Alarmcode_lbl.Text = dataGridView7.Rows[k1].Cells[0].Value.ToString();
                        s5f1.ALTX = LsALID[k1].Discription;
                        s5f1.ToBuffer();
                        Secslist(s5f1.ToLog());
                        int SystemByte = oHSMS.Request((StreamFunction)s5f1);
                        if(!curALID.Contains((int)s5f1.ALID))
                            curALID.Add((int)s5f1.ALID);   //记录报警ID
                    }
                }
            }
        }
        private void S5F1_Clear(string text)   //报警清除
        {
            if(curALID.Count== 0)
            {
                return;
            }
            MSG_S5F1 s5f1 = new MSG_S5F1();
            for (int i = 0; i < curALID.Count; i++)
            {
                for (int k1 = 0; k1 < LsALID.Count; k1++)
                {
                    if (LsALID[k1].ID == curALID[i].ToString())
                    {
                        if (LsALID[k1].M_Ceed == "T")
                        {
                            //s5f1.ALCD = (byte)(128); //128 第8bit
                            s5f1.ALCD = (byte)0;
                            //s5f1.ALID = (uint)Int32.Parse(LsALID[k1].ID);
                            s5f1.ALID = (uint)Int32.Parse(LsALID[k1].ID);
                            //Alarmcode_lbl.Text = "0"; //清Alarm
                            s5f1.ALTX = LsALID[k1].Discription;
                            s5f1.ToBuffer();
                            Secslist(s5f1.ToLog());
                            int SystemByte = oHSMS.Request((StreamFunction)s5f1);
                        }
                    }
                }
            }
            curALID.Clear();
        }
        private void ON_RCV_S5F2()  //报警报告确认
        {
            MSG_S5F2 s5f2 = new MSG_S5F2();
            oStrFun.m_BUFFERS.CopyTo(s5f2.m_BUFFERS, 0);
            s5f2.m_Length = oStrFun.m_Length;
            s5f2.FromBuffer();
            Secslist(s5f2.ToLog());
        }
        #region s5f3
        private void ON_RCV_S5F3()//停用/启用告警
        {
            MSG_S5F3 s5f3 = new MSG_S5F3();
            oStrFun.m_BUFFERS.CopyTo(s5f3.m_BUFFERS, 0);
            s5f3.m_Length = oStrFun.m_Length;
            s5f3.FromBuffer();
            Secslist(s5f3.ToLog());
            MSG_S5F4 s5f4 = new MSG_S5F4();

            string Aled = "";

            if (s5f3.ALED == 0)
            {
                Aled = "F";//禁用
            }
            else
            {
                Aled = "T";//启用
            }
            if (s5f3.ALID != 0)
            {
                for (int k1 = 0; k1 < LsALID.Count; k1++)
                {
                    if (LsALID[k1].ID == s5f3.ALID.ToString())
                    {
                        if (LsALID[k1].M_Ceed != Aled)   //   T/F
                        {
                            LsALID[k1].M_Ceed = Aled;
                        }
                        s5f4.ACKC5 = 0;
                    }
                }
            }
            else
            {
                for (int k1 = 0; k1 < LsALID.Count; k1++) //禁用全部告警
                {
                    if (LsALID[k1].M_Ceed != Aled)
                    {
                        LsALID[k1].M_Ceed = Aled;
                    }
                    s5f4.ACKC5 = 0;
                }
            }
            SaveID("ALID");    //报警启用或者禁用保存
            s5f4.ToBuffer();
            Secslist(s5f4.ToLog());
            oHSMS.Response((StreamFunction)s5f4);

        }
        public void ReadType(List<string> typeList, string path)
        {

            typeList.Clear();

            if (File.Exists(path))
            {
                FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                System.IO.StreamReader sr = new StreamReader(fs);
                while (!sr.EndOfStream)
                {
                    string temp = sr.ReadLine();

                    if (temp != "" || temp != "\r\n")
                    {
                        typeList.Add(temp);
                    }

                }
                //--
                sr.Close();
            }

        }
        public void WriteType(string path, List<string> type)
        {
            if (File.Exists(path))
            {
                FileStream fs = new FileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.ReadWrite);
                System.IO.StreamWriter sw = new StreamWriter(fs);
                for (int i = 0; i < type.Count; i++)
                    sw.WriteLine(type[i]);
                //--
                sw.Close();
            }

        }

        #endregion
        #region s5f5
        /// <summary>
        /// 此消息要求设备发送二进制和模拟警报
        ///向主机提供信息。
        /// </summary>
        private void ON_RCV_S5F5()//告警列表查询
        {
            MSG_S5F5 s5f5 = new MSG_S5F5();
            oStrFun.m_BUFFERS.CopyTo(s5f5.m_BUFFERS, 0);
            s5f5.m_Length = oStrFun.m_Length;
            s5f5.FromBuffer();
            Secslist(s5f5.ToLog());
            MSG_S5F6 s5f6 = new MSG_S5F6();
            int lcount = s5f5.L_count;
            s5f6.L_count = (byte)lcount;
            int k3 = 0;
            if (lcount != 0)
            {
                for (int k1 = 0; k1 < s5f5.ALID.Length; k1++)
                {
                    for (int k2 = 0; k2 < LsALID.Count; k2++)
                    {
                        if (s5f5.ALID[k1].ToString() == LsALID[k2].ID)
                        {
                            s5f6.arrALCD.Add((byte)Int32.Parse(LsALID[k2].ALCD));
                            s5f6.arrALID.Add((uint)Int32.Parse(LsALID[k2].ID));
                            s5f6.arrALTX.Add(LsALID[k2].Discription);
                            k3 = 1;
                            break;
                        }
                    }
                    if (k3 == 0)
                    {
                        s5f6.arrALCD.Add((byte)0);
                        s5f6.arrALID.Add((uint)Int32.Parse(s5f5.ALID[k1].ToString()));
                        s5f6.arrALTX.Add("");
                    }
                    else
                    {
                        k3 = 0;
                    }
                }
            }
            else
            {
                for (int k1 = 0; k1 < LsALID.Count; k1++)
                {
                    s5f6.arrALCD.Add((byte)Int32.Parse(LsALID[k1].ALCD));
                    s5f6.arrALID.Add((uint)Int32.Parse(LsALID[k1].ID));
                    s5f6.arrALTX.Add(LsALID[k1].Discription);
                    k3 = k3 + 1;
                }
                s5f6.L_count = (byte)k3;
            }

            s5f6.ToBuffer();
            Secslist(s5f6.ToLog());
            oHSMS.Response((StreamFunction)s5f6);

        }
        #endregion
        #region s5f7
        private void ON_RCV_S5F7()//host查询启用的警告
        {
            MSG_S5F7 s5f7 = new MSG_S5F7();
            oStrFun.m_BUFFERS.CopyTo(s5f7.m_BUFFERS, 0);
            s5f7.m_Length = oStrFun.m_Length;
            s5f7.FromBuffer();
            Secslist(s5f7.ToLog());
            MSG_S5F8 s5f8 = new MSG_S5F8();

            int k3 = 0;

            for (int k2 = 0; k2 < LsALID.Count; k2++)
            {
                if ("T" == LsALID[k2].M_Ceed)
                {
                    s5f8.arrALCD.Add((byte)Int32.Parse(LsALID[k2].ALCD));
                    s5f8.arrALID.Add((uint)Int32.Parse(LsALID[k2].ID));
                    s5f8.arrALTX.Add(LsALID[k2].Discription);
                    k3 = k3 + 1;
                }
            }
            s5f8.L_count = (byte)k3;
            s5f8.ToBuffer();
            Secslist(s5f8.ToLog());
            oHSMS.Response((StreamFunction)s5f8);

        }
        #endregion
        /// <summary>
        /// 此消息的目的是让设备发送定义的
        ///在发生时向主机链接并启用报告组
        ///事件 （CEID）
        ///如果 S6，F11 是多块，则必须在 S6，F5/S6，F6 之前  查询/授权交易
        ///出站上传产品sn到EAP  ceid=5
        ///设备状态切换 发送  ceid=6
        /// 1 时间
        /// 2 控制模式
        /// 3 设备状态
        /// 4.Product SN(产品码)
        /// 5 出站上传 和产能上传
        /// </summary>
        /// <param name="ceid"></param>
        public void s6f11(uint ceid)//事件上报      
        {
            MSG_S6F11 s6f11 = new MSG_S6F11();    //设备状态切换 发送  ceid=6
            s6f11.CEID = ceid;                    //出站上传产品sn到EAP  ceid=5
            int L_count = 0;                      //发生报警 发送报警前 切换设备状态后 调用ceid=3   PublicData.Status = 2; PublicData.GetInstance().evensend(3);PublicData.GetInstance().errorsend(51);
            int L_count2 = 0;                     //PublicData.errorlist.Add("51");
            for (int k1 = 0; k1 < LsCEID.Count; k1++) //CEID事件
            {
                if (LsCEID[k1].ID == ceid.ToString() && LsCEID[k1].M_Ceed == "T")//启用事件
                {
                    for (int i = 0; i < LsCEID[k1].LsRepid.Count; i++)
                    {
                        if (LsCEID[k1].LsRepid[i] != "" && LsCEID[k1].LsRepid[i] != null)
                        {
                            L_count = L_count + 1;
                            s6f11.arrRPTID.Add((uint)Int32.Parse(LsCEID[k1].LsRepid[i]));
                        }
                    }                    
                    s6f11.L_count1 = (byte)L_count;
                }
            }
            for (int k1 = 0; k1 < s6f11.arrRPTID.Count; k1++)//REPID
            {
                for (int k2 = 0; k2 < LsREPID.Count; k2++)
                {
                    if (s6f11.arrRPTID[k1].ToString() == LsREPID[k2].ID)
                    {

                        for (int k3 = 0; k3 < LsREPID[k2].LsVID.Count; k3++)
                        {
                            s6f11.arrL_count3.Add((uint)Int32.Parse(LsREPID[k2].LsVID[k3]));
                            L_count2 = L_count2 + 1;
                            break;
                        }
                        s6f11.arrRPT.Add(L_count2);//给RPTID赋值
                        L_count2 = 0;
                    }
                }
            }

            for (int k1 = 0; k1 < s6f11.arrL_count3.Count; k1++) //给VID赋值
            {
                switch (s6f11.arrL_count3[k1].ToString())
                {

                    case "1"://时间
                        s6f11.arrVID.Add(DateTime.Now.ToString("yyMMddHHmmss"));
                        break;
                    case "2"://Control state (0=local 1=remote)
                        s6f11.arrVID.Add((byte)_secGemService.controlMode);
                        break;
                    case "3"://Process Status(0=idle 1=run 2=stop 3=alarm)
                        if (statusHub.CurrentState == EquipmentState.Idle) //IDLE
                            s6f11.arrVID.Add((byte)0);
                        if (statusHub.CurrentState == EquipmentState.Running)  //RUN
                            s6f11.arrVID.Add((byte)1);
                        if (statusHub.CurrentState == EquipmentState.Alarm)  //ALARM
                            s6f11.arrVID.Add((byte)3);
                        else
                            s6f11.arrVID.Add((byte)2);  //STOP
                        break;
                    case "4":
                        s6f11.arrVID.Add(_secGemService.OutUnitCode);//上传单个二维码
                        break;
                    case "5":
                        s6f11.arrVID.Add(_secGemService.OutUnitCode);//出站产品码(多个)
                        break;
                    case "6":
                        s6f11.arrVID.Add(_secGemService.UPH);//产能
                        break;
                    case "7":
                        s6f11.arrVID.Add(_secGemService.CurrentRecipe);//当前recipe
                        break;
                }
            }
            s6f11.ToBuffer();
            Secslist(s6f11.ToLog());
            int SystemByte = oHSMS.Request((StreamFunction)s6f11);  // 傳送 S1F111 StreamFunction 並回傳此訊息的識別碼
        }
        /// <summary>
        /// 事件报告确认
        /// </summary>
        private void ON_RCV_S6F12()
        {
            MSG_S6F12 s6f12 = new MSG_S6F12();
            oStrFun.m_BUFFERS.CopyTo(s6f12.m_BUFFERS, 0);
            s6f12.m_Length = oStrFun.m_Length;
            s6f12.FromBuffer();
            Secslist(s6f12.ToLog());
            aEvent_OutStation.Set();
        }
        #region s6f15
        /// <summary>
        /// 此消息的目的是让主机请求给定的报告从设备分组
        /// </summary>
        private void ON_RCV_S6F15()//host获取特定的事件报告
        {
            MSG_S6F15 s6f15 = new MSG_S6F15();
            oStrFun.m_BUFFERS.CopyTo(s6f15.m_BUFFERS, 0);
            s6f15.m_Length = oStrFun.m_Length;
            s6f15.FromBuffer();
            Secslist(s6f15.ToLog());
            MSG_S6F16 s6f16 = new MSG_S6F16();

            s6f16.L_count = 3;
            s6f16.DATAID = (byte)0;
            s6f16.CEID = s6f15.CEID;
            s6f16.L_count1 = 1;
            int L_count1 = 0;
            for (int k1 = 0; k1 < LsCEID.Count; k1++) //CEID
            {
                if (LsCEID[k1].ID == s6f16.CEID.ToString() && LsCEID[k1].M_Ceed == "T")
                {
                    for (int k2 = 0; k2 < LsCEID[k1].LsRepid.Count; k2++)
                    {
                        if (LsCEID[k1].LsRepid[k2] != "" && LsCEID[k1].LsRepid[k2] != null)
                        {
                            L_count1 = L_count1 + 1;
                            s6f16.arrRPTID.Add((uint)Int32.Parse(LsCEID[k1].LsRepid[k2]));
                        }
                    }
                    s6f16.L_count1 = (byte)L_count1;
                }
            }
            for (int k1 = 0; k1 < s6f16.arrRPTID.Count; k1++)//REPID
            {
                for (int k2 = 0; k2 < LsREPID.Count; k2++)
                {
                    if (s6f16.arrRPTID[k1].ToString() == LsREPID[k2].ID)
                    {
                        for (int k3 = 0; k3 < LsREPID[k2].LsVID.Count; k3++)
                        {
                            if (LsREPID[k2].LsVID[k3] != "" && LsREPID[k2].LsVID[k3] != null)
                            {
                                switch (LsREPID[k2].LsVID[k3])
                                {
                                    case "1"://时间
                                        s6f16.L_count3 = 1;
                                        s6f16.arrSVID.Add(DateTime.Now.ToString("yyMMddHHmmss"));
                                        break;

                                    case "2"://状态
                                        s6f16.L_count3 = 1;
                                        s6f16.arrSVID.Add((byte)_secGemService.controlMode);
                                        break;

                                    case "3":
                                        s6f16.L_count3 = 1;
                                        if (statusHub.CurrentState == EquipmentState.Idle) //IDLE
                                            s6f16.arrSVID.Add((byte)0);
                                        if (statusHub.CurrentState == EquipmentState.Running)  //RUN
                                            s6f16.arrSVID.Add((byte)1);
                                        if (statusHub.CurrentState == EquipmentState.Alarm)  //ALARM
                                            s6f16.arrSVID.Add((byte)3);
                                        else
                                            s6f16.arrSVID.Add((byte)2);  //STOP
                                        break;
                                    case "4":
                                        s6f16.L_count3 = 1;
                                        s6f16.arrSVID.Add(_secGemService.OutUnitCode);//上传单个二维码
                                        break;
                                    case "5":
                                        s6f16.L_count3 = 1;
                                        s6f16.arrSVID.Add(_secGemService.OutUnitCode);//出站产品码(多个)
                                        break;
                                    case "6":
                                        s6f16.L_count3 = 1;
                                        s6f16.arrSVID.Add(_secGemService.TotalCount);//产能
                                        break;
                                    case "7":
                                        s6f16.L_count3 = 1;
                                        s6f16.arrSVID.Add(_secGemService.CurrentRecipe);//当前recipe
                                        break;
                                }
                            }
                        }

                    }
                }
            }
            s6f16.L_count3 = (byte)s6f16.arrSVID.Count;
            s6f16.ToBuffer();
            Secslist(s6f16.ToLog());
            oHSMS.Response((StreamFunction)s6f16);
        }
        #endregion
        #region s7f1
        //向机器申请需要上传的Recipe Name和所需磁盘上文件的大小
        private void ON_RCV_S7F1()
        {

        }
        #endregion

        #region s7f3
        private void ON_RCV_S7F3()//Host发送完整配方    接收配方   ************************************************
        {
            MSG_S7F3 s7f3 = new MSG_S7F3();
            oStrFun.m_BUFFERS.CopyTo(s7f3.m_BUFFERS, 0);
            s7f3.m_Length = oStrFun.m_Length;
            s7f3.FromBuffer();
            Secslist(s7f3.ToLog());
            try
            {
                if (statusHub.CurrentState == EquipmentState.Idle)
                {
                    string PPID = s7f3.PPID.Trim(' ');
                    IMessage.Logger.Info($"【Host->Eqp】接收配方名称:{PPID}");

                    for (int i = 0; i < _secGemService.RecipeList.Count; i++)
                    {
                        if (PPID == _secGemService.RecipeList[i])   //存在程式
                        {
                            IMessage.Logger.Info($"【Host->Eqp】接收配方名称:{PPID},本地存在此程式");
                            //ProductParam pinfo = new ProductParam();
                            //pinfo.LoadConfig();
                            string[] data1 = s7f3.PPBODY.Split(',');
                            if (data1 == null)
                            {
                                break;
                            }
                            for (int k = 0; k < data1.Length; k++)
                            {
                                string[] data2 = data1[k].Split('/');//:
                                if (data2 == null || data2.Length < 2) 
                                    break;
                                double val = 0;
                                string name = data2[0];
                                if (double.TryParse(data2[1], out val))
                                {
                                    switch (name)
                                    {
                                        case "Carry_RowNum":
                                            //pinfo.GetProductInfo.Carry_RowNum = (int)val;
                                            break;
                                        case "Carry_ColNum":
                                            //pinfo.GetProductInfo.Carry_ColNum = (int)val;
                                            break;
                                        case "Carry_RowGap":
                                            //pinfo.GetProductInfo.Carry_RowGap = val;
                                            break;
                                        case "Carry_ColGap":
                                            //pinfo.GetProductInfo.Carry_ColGap = val;
                                            break;
                                        case "Tray_RowNum":
                                            //pinfo.GetProductInfo.Tray_RowNum = (int)val;
                                            break;
                                        case "Tray_ColNum":
                                            //pinfo.GetProductInfo.Tray_ColNum = (int)val;
                                            break;
                                        case "Tray_RowGap":
                                            //pinfo.GetProductInfo.Tray_RowGap = (int)val;
                                            break;
                                        case "Tray_ColGap":
                                            //pinfo.GetProductInfo.Tray_ColGap = val;
                                            break;
                                        case "Magazine_FloorNum":
                                            //pinfo.GetProductInfo.Magazine_FloorNum = (int)val;
                                            break;
                                        case "Magazine_Gap":
                                            //pinfo.GetProductInfo.Magazine_Gap = (int)val;
                                            break;
                                        default:
                                            break;
                                    }
                                }
                            }
                            //pinfo.WriteXml();
                            IMessage.Logger.Info($"【Host->Eqp】接收配方名称:{PPID},本地切换配方完成");
                        }
                    }
                    MSG_S7F4 s7f4 = new MSG_S7F4();
                    s7f4.ACKC7 = 0;
                    s7f4.ToBuffer();
                    Secslist(s7f4.ToLog());
                    oHSMS.Response((StreamFunction)s7f4);
                }
                else
                {
                    MSG_S7F4 s7f4 = new MSG_S7F4();
                    s7f4.ACKC7 = 5;
                    s7f4.ToBuffer();
                    Secslist(s7f4.ToLog());
                    oHSMS.Response((StreamFunction)s7f4);
                }
            }
            catch (Exception ex)
            {
                MSG_S9F7 s9f7 = new MSG_S9F7();
                string str = "Illegal Data";
                byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(str);
                s9f7.MHEAD = byteArray;
                s9f7.ToBuffer();
                oHSMS.Response((StreamFunction)s9f7);
                Secslist(s9f7.ToLog());
                IMessage.Logger.Info($"【Host->Eqp】接收配方,Illegal Data");
            }
        }
        private void Send_RCV_S7F3(string ppid, string ppbody)//设备send recipe
        {
            //设备发送完整配方
            MSG_S7F3 s7f3 = new MSG_S7F3();
            s7f3.PPID = ppid;//"123"
            s7f3.PPBODY = ppbody;//"abc:22,ddd:333"
            s7f3.ToBuffer();
            Secslist(s7f3.ToLog());
            int SystemByte = oHSMS.Request((StreamFunction)s7f3);
        }

        private void ON_RCV_S7F4()
        {
            MSG_S7F4 s7f4 = new MSG_S7F4();
            oStrFun.m_BUFFERS.CopyTo(s7f4.m_BUFFERS, 0);
            s7f4.m_Length = oStrFun.m_Length;
            s7f4.FromBuffer();
            Secslist(s7f4.ToLog());
        }


        #endregion
        #region s7f5
        //HOST发送需要获取哪个Recipe,机器通过回复S7F6把对应的Recipe下载到本地
        public void ON_RCV_S7F5()//HOST请求配方   
        {
            MSG_S7F5 s7f5 = new MSG_S7F5();
            oStrFun.m_BUFFERS.CopyTo(s7f5.m_BUFFERS, 0);
            s7f5.m_Length = oStrFun.m_Length;
            s7f5.FromBuffer();
            Secslist(s7f5.ToLog());
            string PPID = s7f5.PPID.Trim(' ');//配方名
            MSG_S7F6 s7f6 = new MSG_S7F6();

            // 查找请求的配方是否存在
            //Recipe recipe = _recipeManagerService.GetRecipeByName(PPID);

            //if (recipe != null)   //存在配方
            //{
            //    // 构建力控参数字符串 - 使用与S7F3相同的参数格式
            //    s7f6.PPID = PPID;
            //    StringBuilder paramBuilder = new StringBuilder();

            //    // 运动参数
            //    paramBuilder.Append($"XMoveDistance:{recipe.XMoveDistance},");
            //    paramBuilder.Append($"YMoveDistance:{recipe.YMoveDistance},");
            //    paramBuilder.Append($"IsReturnDialEnabled:{(recipe.IsReturnDialEnabled ? 1 : 0)},");
            //    paramBuilder.Append($"ReturnDialDistance:{recipe.ReturnDialDistance},");
            //    paramBuilder.Append($"MoveSpeed:{recipe.MoveSpeed},");
            //    paramBuilder.Append($"MoveHomeSpeed:{recipe.MoveHomeSpeed},");

            //    // 力控参数
            //    paramBuilder.Append($"Frequency:{recipe.Frequency},");
            //    paramBuilder.Append($"ForwardHomeTorque:{recipe.ForwardHomeTorque},");
            //    paramBuilder.Append($"ForwardTorqueTarget:{recipe.ForwardTorqueTarget},");
            //    paramBuilder.Append($"ForwardMaxTorque:{recipe.ForwardMaxTorque},");
            //    paramBuilder.Append($"NegativeHomeTorque:{recipe.NegativeHomeTorque},");
            //    paramBuilder.Append($"NegativeTorqueTarget:{recipe.NegativeTorqueTarget},");
            //    paramBuilder.Append($"NegativeMaxTorque:{recipe.NegativeMaxTorque}");

            //    // 生产参数
            //    paramBuilder.Append($",StackCount:{recipe.StackCount}");
            //    paramBuilder.Append($",CodingSelectIndex:{recipe.CodingSelectIndex}");

            //    s7f6.PPBODY = paramBuilder.ToString();
            //    IMessage.Logger.Info($"【SEC/GEM】回复配方请求:{PPID}, 参数:{s7f6.PPBODY}");
            //}
            //else
            //{
            //    s7f6.PPID = PPID;
            //    s7f6.PPBODY = "";
            //    IMessage.Logger.Warn($"【SEC/GEM】请求的配方不存在:{PPID}");
            //}

            s7f6.ToBuffer();
            oHSMS.Response((StreamFunction)s7f6);
            Secslist(s7f6.ToLog());
        }

        //private void Send_RCV_S7F5(string ppid)//设备请求配方
        //{
        //    //linghu todo 设备请求配方
        //    MSG_S7F5 s7f5 = new MSG_S7F5();
        //    s7f5.PPID = ppid;
        //    s7f5.ToBuffer();
        //    Secslist(s7f5.ToLog());
        //    int SystemByte = oHSMS.Request((StreamFunction)s7f5);
        //}

        string m_ppbody = "";
        string s7f6ppid = "";
        private void ON_RCV_S7F6()//收到host的配方
        {
            MSG_S7F6 s7f6 = new MSG_S7F6();
            oStrFun.m_BUFFERS.CopyTo(s7f6.m_BUFFERS, 0);
            s7f6.m_Length = oStrFun.m_Length;
            s7f6.FromBuffer();
            string recipeName = s7f6.PPID.Trim();
            m_ppbody = s7f6.PPBODY = "XMoveDistance:2,YMoveDistance:3,IsReturnDialEnabled:0,ReturnDialDistance:0,MoveSpeed:2.6,MoveHomeSpeed:5,Frequency:2000,ForwardHomeTorque:0.5,ForwardTorqueTarget:2.5,ForwardMaxTorque:6,NegativeHomeTorque:-0.5,NegativeTorqueTarget:-2.5,NegativeMaxTorque:-6,StackCount:5,CodingSelectIndex:1";
            Secslist(s7f6.ToLog());
            try
            {
                List<RecipeParameter> parameters = new List<RecipeParameter>();

                // 解析配方数据 (格式: "参数1:值1,参数2:值2,...")
                if (!string.IsNullOrEmpty(m_ppbody))
                {
                    var paramPairs = m_ppbody.Split(',');
                    foreach (var pair in paramPairs)
                    {
                        var items = pair.Split(':');
                        if (items.Length >= 2)
                        {
                            parameters.Add(new RecipeParameter
                            {
                                Name = items[0].Trim(),
                                Value = items[1].Trim()
                            });
                        }
                    }
                }
                // 更新或创建配方
                //Recipe recipe = _recipeManagerService.GetRecipeByName(recipeName);
                //if (recipe == null)
                //{
                //    recipe = new Recipe { Name = recipeName };
                //    _recipeManagerService.UpdateRecipe(recipe);
                //}
                // 更新配方参数
                _recipeManagerService.UpdateRecipeParameters(recipeName, parameters);

                IMessage.Logger.Info($"【SEC/GEM】成功更新配方: {recipeName}");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"配方更新失败: {recipeName}, 错误: {ex.Message}");
                // 通知主机错误
                MSG_S9F7 s9f7 = new MSG_S9F7();
                string str = "Recipe processing error";
                byte[] byteArray = Encoding.ASCII.GetBytes(str);
                s9f7.MHEAD = byteArray;
                s9f7.ToBuffer();
                oHSMS.Response((StreamFunction)s9f7);
            }
            Secslist(s7f6.ToLog());
        }
        #endregion
        #region s7f17
        //Delete Process Program Send(删除Recipe)
        private void ON_RCV_S7F17()//收到host删除配方请求  *************************************************
        {
            MSG_S7F17 s7f17 = new MSG_S7F17();
            oStrFun.m_BUFFERS.CopyTo(s7f17.m_BUFFERS, 0);
            s7f17.m_Length = oStrFun.m_Length;
            s7f17.FromBuffer();
            Secslist(s7f17.ToLog());
            IMessage.Logger.Info($"收到Host删除配方请求:{s7f17.PPID}");
            Send_RCV_S7F18();
        }

        //Delete Process Program Send(删除Recipe)
        private void Send_RCV_S7F17(string ppid)//请求host删除配方
        {
            //linghu todo 设备请求host删除配方
            MSG_S7F17 s7f17 = new MSG_S7F17();
            s7f17.L_count = 1;
            s7f17.arrPPID.Add(ppid);
            s7f17.ToBuffer();
            Secslist(s7f17.ToLog());
            int SystemByte = oHSMS.Request((StreamFunction)s7f17);
        }

        private void Send_RCV_S7F18()//设备删除配方后回复host
        {
            MSG_S7F18 s7f18 = new MSG_S7F18();
            s7f18.ACKC7 = 0; // 0=接受1=未授予权限2=长度误差3=矩阵溢出4=未找到PPID
            s7f18.ToBuffer();
            Secslist(s7f18.ToLog());
            oHSMS.Response((StreamFunction)s7f18);

        }
        private void ON_RCV_S7F18()//host删除配方后回复设备
        {
            MSG_S7F18 s7f18 = new MSG_S7F18();
            oStrFun.m_BUFFERS.CopyTo(s7f18.m_BUFFERS, 0);
            s7f18.m_Length = oStrFun.m_Length;
            s7f18.FromBuffer();
            Secslist(s7f18.ToLog());
        }
        #endregion

        //获取当前机器上的所有Recipe Name,机器通过回复S7F20显示当前所有的Recipe的列表
        private void ON_RCV_S7F19()//host请求配方列表
        {
            MSG_S7F19 s7f19 = new MSG_S7F19();
            oStrFun.m_BUFFERS.CopyTo(s7f19.m_BUFFERS, 0);
            s7f19.m_Length = oStrFun.m_Length;
            s7f19.FromBuffer();
            Secslist(s7f19.ToLog());
            send_S7F20();

        }
        //List<string> EPPD = new List<string>();

        private void send_S7F20()//设备回复配方列表***********************************
        {
            MSG_S7F20 s7f20 = new MSG_S7F20();
            for (int i = 0; i < _secGemService.RecipeList.Count; i++)
            {
                s7f20.arrPPID.Add(_secGemService.RecipeList[i]);
            }
            s7f20.L_count = (byte)(_secGemService.RecipeList.Count);
            s7f20.ToBuffer();
            Secslist(s7f20.ToLog());
            oHSMS.Response((StreamFunction)s7f20);
        }

        private void ON_RCV_S10F3()//显示host信息
        {

            MSG_S10F3 s10f3 = new MSG_S10F3();
            oStrFun.m_BUFFERS.CopyTo(s10f3.m_BUFFERS, 0);
            s10f3.m_Length = oStrFun.m_Length;
            s10f3.FromBuffer();
            Secslist(s10f3.ToLog());
            MSG_S10F4 s10f4 = new MSG_S10F4();
            s10f4.ACKC10 = 0;
            s10f4.ToBuffer();
            oHSMS.Response((StreamFunction)s10f4);
            Secslist(s10f4.ToLog());
            //MessageBox.Show(s10f3.TEXT, "Terminal Numbeer: " + s10f3.TID, MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.ServiceNotification);
            IMessage.Logger.Info($"【Host->EQP】" + s10f3.TEXT + "Terminal Numbeer: " + s10f3.TID);
        }
        public string ConvertEnconding(string str)
        {
            var utf8 = Encoding.UTF8; //SQLite源编码
            var defaultCode = Encoding.Default; //目标编码
            var utf8Bytes = utf8.GetBytes(str);
            var defaultBytes = Encoding.Convert(utf8, defaultCode, utf8Bytes);
            var defaultChars = new char[defaultCode.GetCharCount(defaultBytes, 0, defaultBytes.Length)];
            defaultCode.GetChars(defaultBytes, 0, defaultBytes.Length, defaultChars, 0);
            return new string(defaultChars);
        }
        /// <summary>
        /// 请求maping
        /// </summary>
        /// <param name="modelCode">治具码</param>
        private void ON_RCV_S14F1(string modelCode)
        {

            MSG_S14F1 s14f1 = new MSG_S14F1();
            s14f1.arrL_count.Add(modelCode);
            Secslist(s14f1.ToLog());
            s14f1.ToBuffer();
            int SystemByte = oHSMS.Request((StreamFunction)s14f1);
        }

        public string Mappingdata;
        public string Mappingdata2;
        /// <summary>
        /// 收到mapping
        /// </summary>
        private void ON_RCV_S14F2()
        {
            MSG_S14F2 s14f2 = new MSG_S14F2();
            oStrFun.m_BUFFERS.CopyTo(s14f2.m_BUFFERS, 0);
            s14f2.ToBuffer();
            s14f2.m_Length = oStrFun.m_Length;
            s14f2.FromBuffer();
            Secslist(s14f2.ToLog());
            //sp.IfSecs.MapingData = null;    //没拿到数据
            Mappingdata = s14f2.ATTRDATA;  //拿到数据
            Mappingdata2 = s14f2.ATTRDATA2;  //拿到数据
            aEvent_GetMap.Set();
            MSG_S9F1 s9f1 = new MSG_S9F1();
            string str = "Unrecognized Device ID";
            byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(str);
            s9f1.MHEAD = byteArray;
            s9f1.ToBuffer();
            oHSMS.Response((StreamFunction)s9f1);
            IMessage.Logger.Info($"Host->Machine:" + Mappingdata.Trim() + "\r\n" + Mappingdata2.Trim());
        }

        public void eventsend(uint ceid)
        {
            try
            {
                s6f11(ceid);
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"SECS信息处理异常,{ex}");
            }
        }
        public void errorsend(uint alid)
        {
            try
            {
                s5f1(alid);
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"SECS信息处理异常,{ex}");
            }
        }
        public void errorClear(string text)
        {
            try
            {
                S5F1_Clear(text);
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"SECS信息处理异常,{ex}");
            }
        }
        public void errorClear_All()
        {
            try
            {
                for (int k1 = 0; k1 < LsALID.Count - 1; k1++)
                {
                    if (LsALID[k1].M_Ceed == "T")
                    {
                        MSG_S5F1 s5f1 = new MSG_S5F1();
                        s5f1.ALCD = (byte)(0); //128 第8bit
                        s5f1.ALID = (uint)Int32.Parse(LsALID[k1].ID);
                        //Alarmcode_lbl.Text = "0"; //清Alarm
                        s5f1.ALTX = LsALID[k1].Discription;
                        s5f1.ToBuffer();
                        Secslist(s5f1.ToLog());
                        int SystemByte = oHSMS.Request((StreamFunction)s5f1);
                    }
                }
                curALID.Clear();
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"SECS信息处理异常,{ex}");
            }
            
        }
        /// <summary>
        /// ALID CEID  ECID REPID SVID VID
        /// </summary>
        public object OpenID(string name)
        {
            try
            {
                string configPath = GetSecsConfigDirectory();
                string fileName = $"{name}.xml";
                string filePath = Path.Combine(configPath, fileName);

                if (!File.Exists(filePath))
                {
                    IMessage.Logger.Info($"SECS配置文件不存在: {filePath}");
                    return null;
                }

                switch (name)
                {
                    case "ALID":
                        List<ALID> alid = XmlSerializeHelper.DESerializer<List<ALID>>(filePath);
                        return alid;
                    case "CEID":
                        List<CEID> ceid = XmlSerializeHelper.DESerializer<List<CEID>>(filePath);
                        return ceid;
                    case "ECID":
                        List<ECID> ecid = XmlSerializeHelper.DESerializer<List<ECID>>(filePath);
                        return ecid;
                    case "REPID":
                        List<REPID> repid = XmlSerializeHelper.DESerializer<List<REPID>>(filePath);
                        return repid;
                    case "SVID":
                        List<SVID> svid = XmlSerializeHelper.DESerializer<List<SVID>>(filePath);
                        return svid;
                    case "VID":
                        List<VID> vid = XmlSerializeHelper.DESerializer<List<VID>>(filePath);
                        return vid;
                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"SECS获取xml异常:{ex}");
                return null;
            }
        }
        /// <summary>
        /// ALID CEID  ECID REPID SVID VID  ALL
        /// </summary>
        public void SaveID(string name)
        {
            try
            {
                // 使用路径组合方法避免拼接错误
                string configDir = GetSecsConfigDirectory();
                string filePathBase = Path.Combine(configDir, name);

                switch (name.ToUpper())
                {
                    case "ALID":
                        SaveALID(configDir);
                        return;

                    case "CEID":
                        SaveConfiguration(LsCEID, $"{filePathBase}.xml");
                        return;

                    case "ECID":
                        SaveConfiguration(LsECID, $"{filePathBase}.xml");
                        return;

                    case "REPID":
                        SaveConfiguration(LsREPID, $"{filePathBase}.xml");
                        return;

                    case "SVID":
                        SaveConfiguration(LsSVID, $"{filePathBase}.xml");
                        return;

                    case "VID":
                        SaveConfiguration(LsVID, $"{filePathBase}.xml");
                        return;

                    case "ALL":
                        SaveAllConfigurations(configDir);
                        return;

                    default:
                        // 记录未知配置类型
                        IMessage.Logger.Warn($"未知的配置类型: {name}");
                        return;
                }
            }
            catch (Exception ex)
            {
                // 统一异常处理
                HandleSaveError(name, ex);
            }
        }

        // === 辅助方法 ===

        /// <summary>
        /// 获取SECS配置文件的基础目录，确保目录存在
        /// </summary>
        private static string GetSecsConfigDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string configDir = Path.Combine(baseDir, "Config", "SECS");

            // 确保目录存在
            try
            {
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                    IMessage.Logger.Info($"创建SECS配置目录: {configDir}");
                }
                return configDir;
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"创建目录失败: {configDir}, {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 保存ALID配置为CSV文件
        /// </summary>
        private void SaveALID(string configDir)
        {
            string csvPath = Path.Combine(configDir, "报警代码.csv");

            try
            {
                var csv = new CSVoperater();

                // 安全创建空文件
                csv.CreateNewFile(csvPath);

                // 准备数据
                csv.AddData(new string[] { "ALID", "注释", "ALCD", "T/F", "备注" }, csvPath);

                // 批量添加数据行
                foreach (var item in LsALID)
                {
                    csv.AddData(new string[] {
                    item.ID,
                    item.Discription ?? "",  // 处理空值
                    item.ALCD ?? "N/A",
                    item.M_Ceed,
                    item.Remark ?? ""
                    }, csvPath);
                }

                IMessage.Logger.Info($"成功保存 {LsALID.Count} 条报警代码到: {csvPath}");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"保存报警代码失败: {ex.Message}");

                // 清理无效文件
                try { if (File.Exists(csvPath)) File.Delete(csvPath); }
                catch (Exception delEx)
                {
                    IMessage.Logger.Error($"清理无效文件失败: {delEx.Message}");
                }
            }
        }

        /// <summary>
        /// 保存XML格式的配置
        /// </summary>
        private void SaveConfiguration<T>(List<T> configList, string filePath)
        {
            if (configList == null || configList.Count == 0)
            {
                IMessage.Logger.Warn($"未保存配置：列表为空({Path.GetFileName(filePath)})");
                return;
            }

            try
            {
                XmlSerializeHelper.XmlSerialize(configList, filePath);
                IMessage.Logger.Info($"配置已保存: {filePath}");
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"保存配置失败: {filePath}, {ex.Message}");
            }
        }

        /// <summary>
        /// 保存所有配置类型
        /// </summary>
        private void SaveAllConfigurations(string configDir)
        {
            // 保存CSV格式的ALID
            SaveALID(configDir);

            // 保存XML格式的其他配置
            SaveConfiguration(LsCEID, Path.Combine(configDir, "CEID.xml"));
            SaveConfiguration(LsECID, Path.Combine(configDir, "ECID.xml"));
            SaveConfiguration(LsREPID, Path.Combine(configDir, "REPID.xml"));
            SaveConfiguration(LsSVID, Path.Combine(configDir, "SVID.xml"));
            SaveConfiguration(LsVID, Path.Combine(configDir, "VID.xml"));
        }

        /// <summary>
        /// 统一的错误处理方法
        /// </summary>
        private void HandleSaveError(string configName, Exception ex)
        {
            IMessage.Logger.Error($"保存SECS配置失败({configName}): {ex.Message}");
        }

        public bool SendRecipeChangedMessage(string recipeName)
        {
            try
            {
                // SECS S2F33: Recipe Change Notification
                //var message = new SECSMessage("S2F33")
                //    .AddItem(recipeName);

                //bool success = SendMessage(message);

                //if (success)
                //    VerifyHostAcknowledge();
                bool success = true;
                return success;
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"SECS SendRecipeChangedMessage failed: {ex.Message}");
                return false;
            }
        }

        private void On_RCV_Unknown(String StreamFunction)
        {
            int Stream = 0;
            int Function = 0;
            Stream = int.Parse(StreamFunction.Substring(1, 3)); //Stream   ID
            Function = int.Parse(StreamFunction.Substring(5, 3)); //Function ID

            if (Stream == 3 || Stream == 4 || Stream == 8 || Stream == 10)
            {
                MSG_S9F3 s9f3 = new MSG_S9F3();
                string str = "Unrecognized Stream";
                byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(str);
                s9f3.MHEAD = byteArray;
                s9f3.ToBuffer();
                Setlog(s9f3.ToLog());
                oHSMS.Response((StreamFunction)s9f3);
                Secslist(s9f3.ToLog());
            }
            else
            {
                switch (Stream)
                {
                    case 2:
                        if (Function == 25 || Function == 26 || Function == 39 || Function == 40 || Function == 41 || Function == 42
                            || Function == 43 || Function == 44 || Function == 45 || Function == 46 || Function == 47 || Function == 48
                            || Function == 49 || Function == 50)
                        {
                            MSG_S9F5 s9f5 = new MSG_S9F5();
                            string str = "Unrecognized Function";
                            byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(str);
                            s9f5.MHEAD = byteArray;
                            s9f5.ToBuffer();
                            Setlog(s9f5.ToLog());
                            oHSMS.Response((StreamFunction)s9f5);
                            Secslist(s9f5.ToLog());
                        }
                        break;

                    case 6:
                        if (Function == 1 || Function == 2 || Function == 3 || Function == 4 || Function == 5 || Function == 6
                           || Function == 13 || Function == 14 || Function == 17 || Function == 18 || Function == 19 || Function == 20
                           || Function == 21 || Function == 22 || Function == 23 || Function == 24)
                        {
                            MSG_S9F5 s9f5 = new MSG_S9F5();
                            string str = "Unrecognized Function";
                            byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(str);
                            s9f5.MHEAD = byteArray;
                            s9f5.ToBuffer();
                            Setlog(s9f5.ToLog());
                            oHSMS.Response((StreamFunction)s9f5);
                            Secslist(s9f5.ToLog());
                        }
                        break;

                    case 7:
                        if (Function == 1 || Function == 2 || Function == 33 || Function == 34)
                        {
                            MSG_S9F5 s9f5 = new MSG_S9F5();
                            string str = "Unrecognized Function";
                            byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(str);
                            s9f5.MHEAD = byteArray;
                            s9f5.ToBuffer();
                            Setlog(s9f5.ToLog());
                            oHSMS.Response((StreamFunction)s9f5);
                            Secslist(s9f5.ToLog());
                        }
                        break;
                }
            }
            //}

        }

        //入站通过板边码获取Maping
        public bool GetMaping(string modelCode, out short[,] mapSts, out string[,] arrBarCode)
        {
            try
            {
                aEvent_GetMap.Reset();
                ON_RCV_S14F1(modelCode);
                if (!aEvent_GetMap.WaitOne(30000))//有延迟回复
                {
                    mapSts = null;
                    arrBarCode = null;
                    IMessage.Logger.Warn($"Machine->Host:" + "S14F1 host返回超时");
                    return false;
                }
                IMessage.Logger.Info($"Host->Machine:" + Mappingdata.Trim());
                mapSts = null;
                arrBarCode = null;
                if (GetMapingSts(Mappingdata.Trim(), Mappingdata2.Trim(), out mapSts, out arrBarCode))
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                mapSts = null;
                arrBarCode = null;
                IMessage.Logger.Error($"SECS获取mapping异常:{ex}");
                return false;
            }
        }
        //上传出站绑定的二维码数组
        public bool OutStationBind(string barCode, string[,] unitCode)
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                XmlNode header = xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null);
                xmlDoc.AppendChild(header);
                XmlElement xm = xmlDoc.CreateElement("UnitCode");
                XmlElement xm1 = xmlDoc.CreateElement("Data");
                XmlNode xm2 = xmlDoc.CreateElement("CarrierID");
                xm2.InnerText = barCode;
                xm1.AppendChild(xm2);
                XmlNode xm3 = xmlDoc.CreateElement("Row");
                xm3.InnerText = unitCode.GetLength(1).ToString();
                xm1.AppendChild(xm3);
                XmlNode xm4 = xmlDoc.CreateElement("Column");
                xm4.InnerText = unitCode.GetLength(0).ToString();
                xm1.AppendChild(xm4);
                Dictionary<string, string> dic = new Dictionary<string, string>();

                //unitCode转换map位置
                int row = Convert.ToInt32(xm3.InnerText);
                int col = Convert.ToInt32(xm4.InnerText);
                string[,] tMap = new string[row, col];
                int a = 0, b = 0;
                for (int i = 0; i < col; i++)//原数组是21行8列
                {
                    for (int j = 0; j < row; j++)
                    {
                        tMap[j, i] = unitCode[i, j];//转换后8行 21列
                    }
                }
                //转换后的数组
                for (int i = 0; i < tMap.GetLength(0); i++)//8行
                {
                    for (int j = 0; j < tMap.GetLength(1); j++)//21列
                    {
                        string node = "R" + (i + 1).ToString() + "C" + (j + 1).ToString();
                        string intext = tMap[i, j];
                        XmlNode xmcode = xmlDoc.CreateElement(node);
                        if (intext != "null")
                        {
                            xmcode.InnerText = intext;
                            xm1.AppendChild(xmcode);
                        }
                    }
                }
                xm.AppendChild(xm1);
                xmlDoc.AppendChild(xm);
                string path = AppDomain.CurrentDomain.BaseDirectory + _codepath + barCode + ".xml";
                //创建当天的文件夹
                DateTime dateTime = DateTime.Now;
                string strData = dateTime.ToString("yyyy_MM_dd");
                string strTemp = _codepath + strData;
                if (!Directory.Exists(strTemp))
                {
                    Directory.CreateDirectory(strTemp);
                }
                path = strTemp + "\\" + barCode + ".xml";
                xmlDoc.Save(path);
                _secGemService.OutUnitCode = xmlDoc.InnerXml;
                IMessage.Logger.Error($"上抛过站内容:{_secGemService.OutUnitCode}");
                eventsend(5);
                return aEvent_OutStation.WaitOne(3000);
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"SECS信息处理异常,{ex}");
                return false;
            }
        }
        //本地保存出站绑定的二维码数组
        public bool OutStationXml(string barCode, string[,] unitCode)
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                XmlNode header = xmlDoc.CreateXmlDeclaration("1.0", "UTF-8", null);
                xmlDoc.AppendChild(header);
                XmlElement xm = xmlDoc.CreateElement("UnitCode");
                XmlElement xm1 = xmlDoc.CreateElement("Data");
                XmlNode xm2 = xmlDoc.CreateElement("CarrierID");
                xm2.InnerText = barCode;
                xm1.AppendChild(xm2);
                XmlNode xm3 = xmlDoc.CreateElement("Row");
                xm3.InnerText = unitCode.GetLength(1).ToString();
                xm1.AppendChild(xm3);
                XmlNode xm4 = xmlDoc.CreateElement("Column");
                xm4.InnerText = unitCode.GetLength(0).ToString();
                xm1.AppendChild(xm4);
                Dictionary<string, string> dic = new Dictionary<string, string>();

                //unitCode转换map位置
                int row = Convert.ToInt32(xm3.InnerText);
                int col = Convert.ToInt32(xm4.InnerText);
                string[,] tMap = new string[row, col];
                int a = 0, b = 0;
                for (int i = 0; i < col; i++)//原数组是21行8列
                {
                    for (int j = 0; j < row; j++)
                    {
                        tMap[j, i] = unitCode[i, j];//转换后8行 21列
                    }
                }
                //转换后的数组
                for (int i = 0; i < tMap.GetLength(0); i++)//8行
                {
                    for (int j = 0; j < tMap.GetLength(1); j++)//21列
                    {
                        string node = "R" + (i + 1).ToString() + "C" + (j + 1).ToString();
                        string intext = tMap[i, j];
                        XmlNode xmcode = xmlDoc.CreateElement(node);
                        xmcode.InnerText = intext;
                        xm1.AppendChild(xmcode);
                    }
                }
                xm.AppendChild(xm1);
                xmlDoc.AppendChild(xm);

                DateTime dateTime = DateTime.Now;
                string strData = dateTime.ToString("yyyy_MM_dd");
                //创建当天的文件夹
                string strTemp = _codepath + strData;
                if (!Directory.Exists(strTemp))
                {
                    Directory.CreateDirectory(strTemp);
                }
                string path = strTemp + "\\" + barCode + ".xml";
                xmlDoc.Save(path);
                return true;
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"SECS信息处理异常,{ex}");
                return false;
            }
        }
        //上传的单个产品SN
        public bool OutStationCode(string unitCode, uint ceid)
        {
            try
            {
                _secGemService.OutUnitCode = unitCode;
                aEvent_OutStation.Reset();
                eventsend(ceid);//4=上传单个sn 5=上传出站码
                return aEvent_OutStation.WaitOne(3000);
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"SECS信息处理异常,{ex}");
                return false;
            }

        }
        /// <summary>
        /// 远程获取mapping
        /// </summary>
        public bool GetMapingSts(string mapdata,string sndata,out short[,] map,out string[,] sn )
        {
            map = null;
            sn = null;
            List<string> mapData = new List<string>();
            List<string> snData = new List<string>();
            XmlDocument xmlD = new XmlDocument();
            //string xmlPath = AppDomain.CurrentDomain.BaseDirectory+ "MINGSEAL.xml";
            //xmlD.Load(xmlPath);
            //data = xmlD.InnerXml;
            try
            {
                xmlD.LoadXml(mapdata);
                XmlNode rootNode = xmlD.DocumentElement;
                foreach (XmlNode xmlNode in rootNode.ChildNodes)   //解析xml文件
                {
                    if (xmlNode.Name == "SubstrateMap")
                    {
                        foreach (XmlNode cNode1 in xmlNode.ChildNodes)
                        {
                            if (cNode1.Name == "BinCodeMap")
                            {
                                foreach (XmlNode cNode2 in cNode1.ChildNodes)
                                {
                                    if (cNode2.Name == "BinCode")
                                    {
                                        mapData.Add(cNode2.InnerText);
                                    }
                                }
                            }
                        }
                    }
                }
                if (mapData.Count == 0) 
                    return false;
                int len = mapData[0].Length / 4;
                short[,] nMap = new short[mapData.Count, len];

                for (int i = 0; i < nMap.GetLength(0); i++)
                {
                    for (int j = 0; j < nMap.GetLength(1); j++)
                    {
                        if (mapData[i].Length < 4)
                        {
                            IMessage.Logger.Info($"SECS Mapping解析错误异常:Length<4");
                            return false;
                        }
                        string unitBin = mapData[mapData.Count - 1 - i].Substring(mapData[mapData.Count - 1 - i].Length - 4 * (j + 1), 4);
                        if (unitBin == "006F") nMap[i, j] = 1;
                        else if (unitBin == "006E") nMap[i, j] = 2;
                        //else if (unitBin == "0009") nMap[i, j] = 2;
                        else nMap[i, j] = 0;
                    }
                }
                short[,] tMap = new short[len, mapData.Count];
                for (int i = 0; i < nMap.GetLength(0); i++)
                {
                    for (int j = 0; j < nMap.GetLength(1); j++)
                    {
                        tMap[j, i] = nMap[i, j];
                    }
                }

                map = tMap;

                //  code
                //string[] data1 = sndata.Split(';');
                //for (int i = 0; i < data1.Length; i++)
                //{
                //    string[] data2 = data1[i].Split(',');
                //    snData.Add(data2[0]);
                //}
                //if (snData.Count != (nMap.GetLength(0) * nMap.GetLength(1))) 
                //    return;
                //string[,] arrsn = new string[nMap.GetLength(0), nMap.GetLength(1)];
                //for (int i = 0; i < nMap.GetLength(0); i++)
                //{
                //    for (int k = 0; k < nMap.GetLength(1); k++)
                //    {
                //        arrsn[i, k] = snData[i * nMap.GetLength(1) + k];
                //    }
                //}
                //string[,] tsn = new string[nMap.GetLength(1), nMap.GetLength(0)];
                //for (int i = 0; i < arrsn.GetLength(0); i++)
                //{
                //    for (int j = 0; j < arrsn.GetLength(1); j++)
                //    {
                //        tsn[j, i] = arrsn[i, j];
                //    }
                //}

                //sn = tsn;
                return true;
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"SECS Mapping解析错误异常:{ex}");
                return false;
            }
        }
       
        /// <summary>
        /// 本地xml读取map
        /// </summary>
        public bool GetMappingSts(string mapdata, string sndata, out short[,] map, out string[,] sn)
        {
            map = null;
            sn = null;
            List<string> mapData = new List<string>();
            List<string> snData = new List<string>();
            XmlDocument xmlD = new XmlDocument();
            string goodBin = "";
            string xmlPath = AppDomain.CurrentDomain.BaseDirectory + "MINGSEAL.xml";
            //xmlD.Load(xmlPath);
            //data = xmlD.InnerXml;
            try
            {
                xmlD.LoadXml(mapdata);
                XmlNode rootNode = xmlD.DocumentElement;
                foreach (XmlNode xmlNode in rootNode.ChildNodes)   //解析xml文件
                {
                    if (xmlNode.Name == "SubstrateMap")
                    {
                        foreach (XmlNode cNode1 in xmlNode.ChildNodes)
                        {
                            if (cNode1.Name == "BinCodeMap")
                            {
                                foreach (XmlNode cNode2 in cNode1.ChildNodes)
                                {
                                    if (cNode2.Name == "BinCode")
                                    {
                                        mapData.Add(cNode2.InnerText);
                                    }
                                }
                            }
                        }
                    }
                }
                if (mapData.Count == 0) 
                    return false;
                int len = mapData[0].Length / 4;
                short[,] nMap = new short[mapData.Count, len];  //4行10列

                for (int i = 0; i < nMap.GetLength(0); i++)
                {
                    for (int j = 0; j < nMap.GetLength(1); j++)
                    {
                        if (mapData[i].Length < 4)
                        {
                            //Msg.Show("Maping解析错误", enMsgType.ERROR);
                            return false;
                        }
                        string unitBin = mapData[mapData.Count - 1 - i].Substring(mapData[mapData.Count - 1 - i].Length - 4 * (j + 1), 4);
                        if (unitBin == "006F") nMap[i, j] = 1;
                        else if (unitBin == "006E") nMap[i, j] = 2;
                        //else if (unitBin == "0009") nMap[i, j] = 2;
                        else nMap[i, j] = 0;
                    }
                }
                short[,] tMap = new short[len, mapData.Count];
                for (int i = 0; i < nMap.GetLength(0); i++)//4
                {
                    for (int j = 0; j < nMap.GetLength(1); j++)//10
                    {
                        tMap[j, i] = nMap[i, j];
                    }
                }

                map = tMap;
                //  code
                //string[] data1 = sndata.Split(';');
                //for (int i = 0; i < data1.Length; i++)
                //{
                //    string[] data2 = data1[i].Split(',');
                //    snData.Add(data2[0]);
                //}
                //if (snData.Count != (nMap.GetLength(0) * nMap.GetLength(1))) 
                //    return;
                //string[,] arrsn = new string[nMap.GetLength(0), nMap.GetLength(1)];
                //for (int i = 0; i < nMap.GetLength(0); i++)
                //{
                //    for (int k = 0; k < nMap.GetLength(1); k++)
                //    {
                //        arrsn[i, k] = snData[i * nMap.GetLength(1) + k];
                //    }
                //}
                //string[,] tsn = new string[nMap.GetLength(1), nMap.GetLength(0)];
                //for (int i = 0; i < arrsn.GetLength(0); i++)
                //{
                //    for (int j = 0; j < arrsn.GetLength(1); j++)
                //    {
                //        tsn[j, i] = arrsn[i, j];
                //    }
                //}

                //sn = tsn;
                return true;
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"SECS Mapping解析错误异常:{ex}");
                return false;
            }
            finally
            {
            }
        }

        public event Action<bool> OnStatusChangeEvent;

    }
}

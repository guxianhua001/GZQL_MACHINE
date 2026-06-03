using MotionControl.Interfaces;
using MotionControl.Services;
using System;
using System.Threading;

namespace MotionControl.Card
{
    public class LeisaiMotionCard : MotionCardBase
    {
        private readonly object _lockObj = new();
        private int _cardIndex;          // 用户选择的卡序号
        private ushort _cardId;          // 初始化后获得的真实卡号
        public override int CardId => (int)_cardId;
        private bool _initialized = false;

        // IO 数据缓存
        private int[] DI_Data = new int[256];
        private int[] DO_Data = new int[256];
        //private int[] DI_Data_2 = new int[256];
        //private int[] DO_Data_2 = new int[256];

        // 默认使用第 0 号卡
        public LeisaiMotionCard(int cardIndex = 0)
        {
            _cardIndex = cardIndex;
            _cardId = (ushort)cardIndex;
        }

        #region 基础生命周期
        public override int CheckEtherCatStatus()
        {
            lock (_lockObj)
            {
                try
                {
                    ushort err = 0;
                    LTDMC.nmc_get_errcode(_cardId, 2, ref err);
                    return err;
                }
                catch { return -1; }
            }
        }
        public override int Initialize()
        {
            lock (_lockObj)
            {
                if (_initialized) return 0;
                try
                {
                    LTDMC.dmc_board_close();
                    int num = LTDMC.dmc_board_init();
                    if (num < 0 || num > 8) return -1;

                    ushort _num = 0;
                    ushort[] ids = new ushort[8];
                    uint[] types = new uint[8];
                    short res = LTDMC.dmc_get_CardInfList(ref _num, types, ids);
                    if (res != 0) return -1;

                    if (_cardIndex < 0 || _cardIndex >= _num) return -2;

                    _cardId = ids[_cardIndex];   // 用序号取真实ID
                    _initialized = true;
                    return res;
                }
                catch { return -1; }
            }
        }
        public override int Close()
        {
            lock (_lockObj)
            {
                try { return LTDMC.dmc_board_close(); }
                catch { return -1; }
            }
        }

        /// <summary>
        /// 总线卡软件复位：dmc_soft_reset → close → 等待约15s → board_init（与旧项目一致）
        /// </summary>
        public override int SoftReset()
        {
            lock (_lockObj)
            {
                try
                {
                    _initialized = false;

                    int res = LTDMC.dmc_soft_reset(_cardId);
                    if (res != 0) return res;

                    LTDMC.dmc_board_close();

                    // 总线卡软件复位耗时约 15s
                    for (int i = 0; i < 15; i++)
                        Thread.Sleep(1000);

                    int initNum = LTDMC.dmc_board_init();
                    if (initNum < 0 || initNum > 8) return -1;

                    ushort cardNum = 0;
                    ushort[] ids = new ushort[8];
                    uint[] types = new uint[8];
                    short listRes = LTDMC.dmc_get_CardInfList(ref cardNum, types, ids);
                    if (listRes != 0) return listRes;

                    if (_cardIndex < 0 || _cardIndex >= cardNum) return -2;

                    _cardId = ids[_cardIndex];
                    _initialized = true;

                    ushort usErr = 0;
                    LTDMC.nmc_get_errcode(_cardId, 2, ref usErr);
                    return usErr;
                }
                catch { return -1; }
            }
        }

        public override int LoadConfig(string configPath)
        {
            lock (_lockObj)
            {
                try { return LTDMC.dmc_download_configfile(_cardId, configPath); }
                catch { return -1; }
            }
        }
        #endregion

        #region 轴控制
        public override int SetServo(int axisId, bool enable)
        {
            lock (_lockObj)
            {
                try
                {
                    return enable
                        ? LTDMC.nmc_set_axis_enable(_cardId, (ushort)axisId)
                        : LTDMC.nmc_set_axis_disable(_cardId, (ushort)axisId);
                }
                catch { return -1; }
            }
        }

        public override int MoveAbs(int axisId, double position, double velocity)
        {
            lock (_lockObj)
            {
                try
                {
                    double minvel = 0, maxvel = 0, acc = 0, dec = 0, stopvel = 0;
                    ushort s_mode = 0;
                    double s_time = 0.1;

                    LTDMC.dmc_get_profile_unit(_cardId, (ushort)axisId,
                        ref minvel, ref maxvel, ref acc, ref dec, ref stopvel);
                    LTDMC.dmc_set_profile_unit(_cardId, (ushort)axisId, minvel, velocity, acc, dec, stopvel);
                    LTDMC.dmc_get_s_profile(_cardId, (ushort)axisId, s_mode, ref s_time);
                    LTDMC.dmc_set_s_profile(_cardId, (ushort)axisId, 0, s_time);

                    return LTDMC.dmc_pmove_unit(_cardId, (ushort)axisId, position, 1);
                }
                catch { return -1; }
            }
        }

        public override int MoveRel(int axisId, double distance, double velocity)
        {
            lock (_lockObj)
            {
                try
                {
                    double minvel = 0, maxvel = 0, acc = 0, dec = 0, stopvel = 0;
                    LTDMC.dmc_get_profile(_cardId, (ushort)axisId,
                        ref minvel, ref maxvel, ref acc, ref dec, ref stopvel);
                    LTDMC.dmc_set_profile(_cardId, (ushort)axisId, minvel, velocity, 0.1, 0.1, stopvel);
                    LTDMC.dmc_set_s_profile(_cardId, (ushort)axisId, 0, 0.01);
                    LTDMC.dmc_set_dec_stop_time(_cardId, (ushort)axisId, 0.1);

                    return LTDMC.dmc_pmove_unit(_cardId, (ushort)axisId, distance, 0);
                }
                catch { return -1; }
            }
        }

        public override int GoHome(int axisId)
        {
            lock (_lockObj)
            {
                try { return LTDMC.nmc_home_move(_cardId, (ushort)axisId); }
                catch { return -1; }
            }
        }

        public override int SetHomeMode(int axisId, int mode, double minVel, double maxVel)
        {
            lock (_lockObj)
            {
                try
                {
                    return LTDMC.nmc_set_home_profile(_cardId, (ushort)axisId,
                        (ushort)mode, minVel, maxVel, 0.1, 0.1, 0);
                }
                catch { return -1; }
            }
        }

        public override int Stop(int axisId)
        {
            lock (_lockObj)
            {
                try { return LTDMC.dmc_stop(_cardId, (ushort)axisId, 0); }
                catch { return -1; }
            }
        }

        public override int EStop(int axisId)
        {
            lock (_lockObj)
            {
                try { return LTDMC.dmc_stop(_cardId, (ushort)axisId, 1); }
                catch { return -1; }
            }
        }
        #endregion

        #region 插补与 JOG
        public override int MoveLineAbs(int coordId, int[] axisIds, double[] positions, double velocity)
        {
            lock (_lockObj)
            {
                try
                {
                    double minvel = 0, maxvel = 0, acc = 0.1, dec = 0.1, stopvel = 0.1;
                    ushort[] axesNum = new ushort[axisIds.Length];
                    for (int i = 0; i < axisIds.Length; i++)
                        axesNum[i] = (ushort)axisIds[i];

                    LTDMC.dmc_set_vector_profile_unit(_cardId, (ushort)coordId,
                        minvel, velocity, acc, dec, stopvel);
                    LTDMC.dmc_set_vector_s_profile(_cardId, (ushort)coordId, 0, 0.1);

                    double[] pos = new double[axisIds.Length];
                    Array.Copy(positions, pos, axisIds.Length);

                    return LTDMC.dmc_line_unit(_cardId, (ushort)coordId,
                        (ushort)axisIds.Length, axesNum, pos, 1);
                }
                catch { return -1; }
            }
        }

        /// <summary>连续点动：先 dmc_set_profile_unit 写入速度，再 dmc_vmove</summary>
        public override int MoveJog(int axisId, int direction, double velocity)
        {
            lock (_lockObj)
            {
                try
                {
                    double minvel = 0, maxvel = 0, acc = 0, dec = 0, stopvel = 0;
                    ushort s_mode = 0;
                    double s_time = 0.1;

                    LTDMC.dmc_get_profile_unit(_cardId, (ushort)axisId,
                        ref minvel, ref maxvel, ref acc, ref dec, ref stopvel);
                    LTDMC.dmc_set_profile_unit(_cardId, (ushort)axisId, minvel, velocity, acc, dec, stopvel);
                    LTDMC.dmc_get_s_profile(_cardId, (ushort)axisId, s_mode, ref s_time);
                    LTDMC.dmc_set_s_profile(_cardId, (ushort)axisId, 0, s_time);

                    return LTDMC.dmc_vmove(_cardId, (ushort)axisId, (ushort)direction);
                }
                catch { return -1; }
            }
        }
        #endregion

        #region 状态与 IO
        public override double GetPosition(int axisId)
        {
            lock (_lockObj)
            {
                double pos = 0;
                try { LTDMC.dmc_get_position_unit(_cardId, (ushort)axisId, ref pos); }
                catch { /* ignore */ }
                return pos;
            }
        }

        public override int GetMotionIO(int axisId, ref int status)
        {
            lock (_lockObj)
            {
                try
                {
                    uint raw = LTDMC.dmc_axis_io_status(_cardId, (ushort)axisId);
                    int sts = (int)raw;
                    int result = 0;

                    if (MotionConvert.BitEnable(sts, 1 << 0)) MotionConvert.SetBits(ref result, Leisai_Define.MIO_ALM);
                    if (MotionConvert.BitEnable(sts, 1 << 1)) MotionConvert.SetBits(ref result, Leisai_Define.MIO_PEL);
                    if (MotionConvert.BitEnable(sts, 1 << 2)) MotionConvert.SetBits(ref result, Leisai_Define.MIO_MEL);
                    if (MotionConvert.BitEnable(sts, 1 << 3)) MotionConvert.SetBits(ref result, Leisai_Define.MIO_EMG);
                    if (MotionConvert.BitEnable(sts, 1 << 4)) MotionConvert.SetBits(ref result, Leisai_Define.MIO_ORG);
                    if (MotionConvert.BitEnable(sts, 1 << 6)) MotionConvert.SetBits(ref result, Leisai_Define.MIO_SPEL);
                    if (MotionConvert.BitEnable(sts, 1 << 7)) MotionConvert.SetBits(ref result, Leisai_Define.MIO_SMEL);
                    // 伺服使能/急停有效：走 IO 位比 EtherCAT 状态机响应更快，供 UI 指示灯使用
                    if (MotionConvert.BitEnable(sts, 1 << 12)) MotionConvert.SetBits(ref result, Leisai_Define.MIO_SVON);
                    if (MotionConvert.BitEnable(sts, 1 << 13)) MotionConvert.SetBits(ref result, Leisai_Define.MIO_ASTP);

                    status = result;
                    return 0;
                }
                catch { return -1; }
            }
        }

        /// <summary>
        /// 读取轴运动状态字（dmc_get_stop_reason）
        /// ASTP 等读 m_MotionSts；伺服使能 IsSVON 改走 GetEtherCatSts
        /// </summary>
        public override int GetMotionSts(int axisId, ref int status)
        {
            lock (_lockObj)
            {
                try
                {
                    int res = 0;
                    LTDMC.dmc_get_stop_reason(_cardId, (ushort)axisId, ref res);

                    int sts = (int)res;
                    int rSts = 0;

                    // 停止原因 → MTS 掩码（与旧项目 GetMotionSts 完全一致）
                    if (sts == 0)
                        MotionConvert.SetBits(ref rSts, Leisai_Define.MTS_MDN);
                    if (sts == 1)
                        MotionConvert.SetBits(ref rSts, Leisai_Define.MTS_ALM);
                    if (sts == 15)
                        MotionConvert.SetBits(ref rSts, Leisai_Define.MTS_OTHER);
                    if (sts == 4)
                        MotionConvert.SetBits(ref rSts, Leisai_Define.MTS_EMG);

                    status = rSts;
                    return 0;
                }
                catch { return -1; }
            }
        }

        /// <summary>
        /// 读取 EtherCAT 轴状态机（nmc_get_axis_state_machine）
        /// IsSVON：GetEtherCatSts 后 sts==4 表示伺服使能
        /// </summary>
        public override int GetEtherCatSts(int axisId, ref int status)
        {
            lock (_lockObj)
            {
                try
                {
                    ushort res = 0;
                    LTDMC.nmc_get_axis_state_machine(_cardId, (ushort)axisId, ref res);
                    status = res;
                    return 0;
                }
                catch { return -1; }
            }
        }

        public override int ClearAlarm(int axisId)
        {
            lock (_lockObj)
            {
                try
                {
                    LTDMC.nmc_clear_axis_errcode(_cardId, (ushort)axisId);
                    return LTDMC.dmc_clear_stop_reason(_cardId, (ushort)axisId);
                }
                catch { return -1; }
            }
        }

        /// <summary>指令位置与编码器位置清零（与旧项目一致：先 position 再 encoder）</summary>
        public override int ClearPosition(int axisId)
        {
            lock (_lockObj)
            {
                try
                {
                    LTDMC.dmc_set_position_unit(_cardId, (ushort)axisId, 0);
                    return LTDMC.dmc_set_encoder_unit(_cardId, (ushort)axisId, 0);
                }
                catch { return -1; }
            }
        }

        public override int SetDo(int port, int value)
        {
            lock (_lockObj)
            {
                try
                {
                    int sts = (value == 0) ? 1 : 0;
                    return LTDMC.dmc_write_outbit(_cardId, (ushort)port, (ushort)sts);
                }
                catch { return -1; }
            }
        }

        public override int GetDi(int port, ref int value)
        {
            lock (_lockObj)
            {
                try
                {
                    short raw = LTDMC.dmc_read_inbit(_cardId, (ushort)port);
                    value = (raw == 0) ? 1 : 0;
                    return value;
                }
                catch { return -1; }
            }
        }
        public override int GetDo(int port, ref int value)
        {
            lock (_lockObj)
            {
                try
                {
                    short sts = LTDMC.dmc_read_outbit(_cardId, (ushort)port);
                    value = sts;      // 返回硬件原始状态（0或1）
                    return 0;
                }
                catch { return -1; }
            }
        }
        public override int CheckDone(int axisId)
        {
            lock (_lockObj)
            {
                try
                {
                    int done = LTDMC.dmc_check_done(_cardId, (ushort)axisId);
                    int encOk = LTDMC.dmc_check_success_encoder(_cardId, (ushort)axisId);
                    return (done == 1 && encOk == 1) ? 1 : 0;
                }
                catch { return -1; }
            }
        }

        public override int CheckHomeDone(int axisId)
        {
            lock (_lockObj)
            {
                try
                {
                    ushort state = 2;
                    LTDMC.dmc_get_home_result(_cardId, (ushort)axisId, ref state);
                    return state;
                }
                catch { return -1; }
            }
        }
        public override int CheckCoordDone(int coordId)
        {
            lock (_lockObj)
            {
                try
                {
                    // 返回 0 表示运动中，返回 1 表示运动完成
                    return LTDMC.dmc_check_done_multicoor(_cardId, (ushort)coordId);
                }
                catch { return -1; } // 异常返回 -1
            }
        }
        #endregion

        public void Update()
        {
            lock (_lockObj)
            {
                try
                {
                    ushort totalIn = 0, totalOut = 0;
                    LTDMC.nmc_get_total_ionum(_cardId, ref totalIn, ref totalOut);
                    for (ushort i = 0; i < totalIn + 7; i++)
                    {
                        short value = LTDMC.dmc_read_inbit(_cardId, (ushort)(i + 8));
                        DI_Data[i] = (value == 0) ? 1 : 0;
                    }
                    for (ushort i = 0; i < totalOut + 7; i++)
                    {
                        short value = LTDMC.dmc_read_outbit(_cardId, (ushort)(i + 8));
                        DO_Data[i] = (value == 0) ? 1 : 0;
                    }
                }
                catch { /* ignore */ }
            }
        }

        public double GetEncPos(int axisId)
        {
            lock (_lockObj)
            {
                double pos = 0;
                try { LTDMC.dmc_get_encoder_unit(_cardId, (ushort)axisId, ref pos); }
                catch { }
                return pos;
            }
        }

        /// <summary>设置指令位置与编码器反馈为同一值（非清零场景可指定任意位置）</summary>
        public int SetPosition(int axisId, double position)
        {
            lock (_lockObj)
            {
                try
                {
                    LTDMC.dmc_set_position_unit(_cardId, (ushort)axisId, position);
                    return LTDMC.dmc_set_encoder_unit(_cardId, (ushort)axisId, position);
                }
                catch { return -1; }
            }
        }

        /// <summary>队列比较模式（多点位置比较）</summary>
        public override int SetTriggerPosition(int nAxisID, int myhcmp, int mycmp_source,
                                      int mycmp_logic, int mytime, double[] adPositionArray)
        {
            /// <param name="lsPoint">比较点位</param>
            /// <param name="hcmp">比较器号，取值范围：0~5（对应硬件 OUT2~OUT7 端口）</param>
            /// <param name="encNum">  辅助编码器通道号
            /// <param name="cmp_logic">  触发电平
            /// <param name="ptime">脉冲宽度 微秒
            lock (_lockObj)
            {
                int rtn = 0;
                ushort cmp_mode = 4;
                LTDMC.dmc_hcmp_set_mode(_cardId, (ushort)myhcmp, cmp_mode);
                LTDMC.dmc_hcmp_set_config(_cardId, (ushort)myhcmp, (ushort)nAxisID,
                                          (ushort)mycmp_source, (ushort)mycmp_logic, mytime);
                LTDMC.dmc_hcmp_clear_points(_cardId, (ushort)myhcmp);

                foreach (var position in adPositionArray)
                {
                    rtn = LTDMC.dmc_hcmp_add_point(_cardId, (ushort)myhcmp, (int)position);
                }
                return rtn;
            }
        }

        public override int Write_rxpdo(int portNum, int address, int dataLen, int value)
        {
            lock (_lockObj)
            {
                try
                {
                    return LTDMC.nmc_write_rxpdo_extra(_cardId, (ushort)portNum,
                                                       (ushort)address, (ushort)dataLen, value);
                }
                catch { return -1; }
            }
        }

        public override int ClearEncoder(int channel)
        {
            lock (_lockObj)
            {
                int rtn = LTDMC.dmc_set_extra_encoder(_cardId, (ushort)channel, 0);
                return rtn;
            }
        }

        #region 连续插补

        public override int SetVectorProfileUnit(int coordId, double startVel, double maxVel, double acc, double dec, double endVel)
        {
            lock (_lockObj) { return LTDMC.dmc_set_vector_profile_unit(_cardId, (ushort)coordId, startVel, maxVel, acc, dec, endVel); }
        }

        public override int ContiSetLookaheadMode(int coordId, int mode, int fifoSize, int reserved1, int reserved2)
        {
            lock (_lockObj) { return LTDMC.dmc_conti_set_lookahead_mode(_cardId, (ushort)coordId, (ushort)mode, fifoSize, (double)reserved1, (double)reserved2); }
        }

        public override int SetVectorSProfile(int coordId, int reserved, double sPara)
        {
            lock (_lockObj) { return LTDMC.dmc_set_vector_s_profile(_cardId, (ushort)coordId, (ushort)reserved, sPara); }
        }

        public override int SetArcLimit(int coordId, int reserved1, int reserved2, int reserved3)
        {
            lock (_lockObj) { return LTDMC.dmc_set_arc_limit(_cardId, (ushort)coordId, (ushort)reserved1, (double)reserved2, (double)reserved3); }
        }

        public override int ContiOpenList(int coordId, int axisCount, int[] axisIds)
        {
            lock (_lockObj)
            {
                ushort[] axs = new ushort[axisCount];
                for (int i = 0; i < axisCount; i++) axs[i] = (ushort)axisIds[i];
                return LTDMC.dmc_conti_open_list(_cardId, (ushort)coordId, (ushort)axisCount, axs);
            }
        }

        public override int ContiLineUnit(int coordId, int axisCount, int[] axisIds, double[] targetPos, ushort posiMode, int mark)
        {
            lock (_lockObj)
            {
                ushort[] axs = new ushort[axisCount];
                for (int i = 0; i < axisCount; i++) axs[i] = (ushort)axisIds[i];
                return LTDMC.dmc_conti_line_unit(_cardId, (ushort)coordId, (ushort)axisCount, axs, targetPos, posiMode, mark);
            }
        }

        public override int ContiStartList(int coordId)
        {
            lock (_lockObj) { return LTDMC.dmc_conti_start_list(_cardId, (ushort)coordId); }
        }

        public override int ContiCloseList(int coordId)
        {
            lock (_lockObj) { return LTDMC.dmc_conti_close_list(_cardId, (ushort)coordId); }
        }

        public override int ContiPauseList(int coordId)
        {
            lock (_lockObj) { return LTDMC.dmc_conti_pause_list(_cardId, (ushort)coordId); }
        }

        public override int CheckCoordMotionDone(int coordId)
        {
            lock (_lockObj) { return LTDMC.dmc_check_done_multicoor(_cardId, (ushort)coordId); }
        }

        #endregion

        #region 轴参数设置

        public override int SetPulseEquivalent(int axisId, double pulsePerUnit)
        {
            lock (_lockObj) { return LTDMC.dmc_set_equiv(_cardId, (ushort)axisId, pulsePerUnit); }
        }

        public override int SetEmergencyStopMode(int axisId, bool enabled, int logicLevel)
        {
            lock (_lockObj)
            {
                return LTDMC.dmc_set_emg_mode(_cardId, (ushort)axisId,
                    (ushort)(enabled ? 1 : 0), (ushort)(logicLevel == 1 ? 1 : 0));
            }
        }

        public override int SetAxisIOMap(int axisId, int ioType, int mapIoType, int mapIoIndex, double filterTime)
        {
            lock (_lockObj)
            {
                return LTDMC.dmc_set_axis_io_map(_cardId, (ushort)axisId,
                    (ushort)ioType, (ushort)mapIoType, (ushort)mapIoIndex, filterTime);
            }
        }

        public override int SetHomeProfile(int axisId, int mode, double lowSpeed, double highSpeed, double accTime, double decTime, double offset)
        {
            lock (_lockObj)
            {
                return LTDMC.nmc_set_home_profile(_cardId, (ushort)axisId,
                    (ushort)mode, lowSpeed, highSpeed, accTime, decTime, offset);
            }
        }

        public override int SetProfileUnit(int axisId, double startVel, double maxVel, double accTime, double decTime, double stopVel)
        {
            lock (_lockObj)
            {
                return LTDMC.dmc_set_profile_unit(_cardId, (ushort)axisId,
                    startVel, maxVel, accTime, decTime, stopVel);
            }
        }

        public override int SetSProfile(int axisId, int reserved, double sPara)
        {
            lock (_lockObj) { return LTDMC.dmc_set_s_profile(_cardId, (ushort)axisId, (ushort)reserved, sPara); }
        }

        public override int SetDecStopTime(int axisId, double decStopTime)
        {
            lock (_lockObj) { return LTDMC.dmc_set_dec_stop_time(_cardId, (ushort)axisId, (ushort)decStopTime); }
        }

        #endregion

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

    }
}

/*----------------------------------------------------------------
* 命名空间: SmarterMotion.Framework.Device.Card
*
* 类 名： XCommandCardLeisai5000
* 功 能： N/A
* 唯一标识：007c5b99-174b-4052-83fe-9b144006e553
* 
* 变更日期：2023/8/22 22:47:13
* 作者：szb
* 公司：CYG
*----------------------------------------------------------------*/


using SmarterMotion.Framework.Plc;
using System;
using System.IO;
using System.Linq;

namespace SmarterMotion
{
    public class XCommandCardLeisai3000 : XCommandCard
    {
        private object obj = new object();
        public XCommandCardLeisai3000()
        {
            CurrentCard = CardStyle.Leisai;
        }

        public override int Initial()
        {
            lock (obj)
            {
                try
                {
                    LTDMC.dmc_board_close();
                    int num = LTDMC.dmc_board_init();//获取板卡数量
                    if (num < 0 || num > 8)
                    {
                        return -1;
                    }
                    ushort _num = 0;
                    ushort[] cardids = new ushort[8];
                    uint[] cardtypes = new uint[8];
                    short res = LTDMC.dmc_get_CardInfList(ref _num, cardtypes, cardids);
                    if (res != 0)
                    {
                        return -1;
                    }
                    CardID = cardids[0];
                    uint TotalAxises = 0;
                    LTDMC.nmc_get_total_axes(CardID, ref TotalAxises);//轴数

                    return res;
                }
                catch (Exception e)
                {
                    return -1;

                }

            }
        }
        public override int SoftReset(ushort cardnum)
        {
            lock (obj)
            {
                try
                {
                    DateTime dt_start = DateTime.Now;
                    int res = LTDMC.dmc_soft_reset(cardnum);//复位总线错误
                    if (res != 0)
                    {
                        //InfoShow.MessageManager.gOnly.Message($"dmc_soft_reset == " + res.ToString(), MessageType.Alarm);
                        return res;
                    }
                    while (true)
                    {
                        if ((DateTime.Now - dt_start).TotalMilliseconds >= 10000.0)//超时退出循环
                        {
                            //InfoShow.MessageManager.gOnly.Message($"总线复位失败， 请检查硬件!", MessageType.Alarm);
                            return res;
                        }
                        ushort usErr = 0;
                        res = LTDMC.nmc_get_errcode(cardnum, 2, ref  usErr);
                        if (res == 0)
                        {
                            if (usErr == 0)//总线复位正常完成
                            {
                                break;
                            }
                        }
                        else
                        {
                            //InfoShow.MessageManager.gOnly.Message($"nmc_get_errcode == " + res.ToString(), MessageType.Alarm);
                        }
                        //Application.DoEvents();
                    }
                    return res;
                }
                catch (Exception ex)
                {
                    //NLogService.Error(ex);
                    return -1;
                }

            }
        }
        public override int LoadParam(ushort cardnum, string configFn)
        {
            lock (obj)
            {
                try
                {
                    //string fileName = Path.GetFileName(configFn);//.ini的绝对路径
                    return LTDMC.dmc_download_configfile(cardnum, configFn);     //下载配置文件
                }
                catch (Exception ex)
                {
                    //NLogService.Error(ex);
                    return -1;

                }

            }
        }

        public override int CheckEtherCatStatus(ushort cardnum)
        {
            lock (obj)
            {
                try
                {
                    ushort err = 0;
                    int res = LTDMC.nmc_get_errcode(cardnum, 2, ref err);//获取总线状态 0代表正常
                    return err;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }
        public override int Close()
        {
            lock (obj)
            {
                try
                {
                    return LTDMC.dmc_board_close();
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }

        public override int Update(int actCardId)
        {
            lock (obj)
            {
                try
                {

                    uint diData = 0;
                    uint doData = 0;

                    //总线方式必须每个IO口都要读一次，只能用dmc_read_inbit读单个口
                    ushort totalIn = 0;
                    ushort totalOut = 0;
                    int ionum = LTDMC.nmc_get_total_ionum((ushort)actCardId, ref totalIn, ref totalOut);
                    for (ushort i = 0; i < totalIn + 7; i++)
                    {
                        short value = LTDMC.dmc_read_inbit((ushort)actCardId, (ushort)(i + 8));
                        if (actCardId == 0)
                        {
                            if (value == 0)
                                DI_Data[i] = 1;
                            else
                                DI_Data[i] = 0;
                        }
                        else
                        {
                            if (value == 0)
                                DI_Data_2[i] = 1;
                            else
                                DI_Data_2[i] = 0;
                        }
                    }
                    for (ushort i = 0; i < totalOut + 7; i++)
                    {
                        short value = LTDMC.dmc_read_outbit((ushort)actCardId, (ushort)(i + 8));
                        if (actCardId == 0)
                        {
                            if (value == 0)
                                DO_Data[i] = 1;
                            else
                                DO_Data[i] = 0;
                        }
                        else
                        {
                            if (value == 0)
                                DO_Data_2[i] = 1;
                            else
                                DO_Data_2[i] = 0;
                        }
                    }
                }
                catch (Exception)
                {
                    return -1;
                }

                return 0;
            }
        }

        public override int SetDo(int actCardId, int channel, int index, int sts)
        {
            lock (obj)
            {
                try
                {
                    sts = sts == 0 ? 1 : 0;
                    int res = 0;
                    res = LTDMC.dmc_write_outbit((ushort)actCardId, (ushort)channel, (ushort)sts);
                    return res;
                }
                catch (Exception)
                {
                    return -1;

                }
            }
        }
        public override int GetDi(int actCardId, int channel, int index, ref int sts)
        {
            lock (obj)
            {

                //sts = (DI_Data[channel-8] >> index) & 1;
                if (actCardId == 0)
                {
                    if (DI_Data[channel - 8] == 1)
                    {
                        sts = 1;
                    }
                    else
                    {
                        sts = 0;
                    }
                }
                else
                {
                    if (DI_Data_2[channel - 8] == 1)
                    {
                        sts = 1;
                    }
                    else
                    {
                        sts = 0;
                    }
                }
                return sts;

            }
        }
        public override int ReadDi(int actCardId, int channel)
        {
            lock (obj)
            {
                try
                {
                    short value = 0;
                    value = LTDMC.dmc_read_inbit((ushort)actCardId, (ushort)channel);
                    return value;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }
        public override int GetDo(int actCardId, int channel, int index, ref int sts)
        {
            lock (obj)
            {

                //sts = (DO_Data[channel] >> index) & 1;
                if (actCardId == 0)
                {
                    if (DO_Data[channel - 8] == 1)
                    {
                        sts = 1;
                    }
                    else
                    {
                        sts = 0;
                    }
                }
                else
                {
                    if (DO_Data_2[channel - 8] == 1)
                    {
                        sts = 1;
                    }
                    else
                    {
                        sts = 0;
                    }
                }
                return sts;
            }
        }
        public override int SetServo(int actCardId, int axisId, bool on)
        {
            lock (obj)
            {
                try
                {
                    int i = (on) ? 0 : 1;
                    var res = 0;
                    if (on)
                    {
                        res = LTDMC.nmc_set_axis_enable((ushort)actCardId, (ushort)axisId);
                    }
                    else
                    {
                        res = LTDMC.nmc_set_axis_disable((ushort)actCardId, (ushort)axisId);
                    }

                    return res;
                }
                catch (Exception e)
                {
                    return -1;
                }

            }
        }
        public override int GoHome(int actCardId, int axisId)
        {
            lock (obj)
            {
                try
                {
                    var res = LTDMC.nmc_home_move((ushort)actCardId, (ushort)axisId);

                    return res;
                }
                catch (Exception)
                {
                    return -1;

                }

            }
        }
        public override int SetHomeMode(int actCardId, int axisId, int mode, double dMinVel, double dMaxVel)
        {
            lock (obj)
            {
                try
                {
                    int ret = 0;

                    ret = LTDMC.nmc_set_home_profile((ushort)actCardId, (ushort)axisId, (ushort)mode, dMinVel, dMaxVel, 0.1, 0.1, 0);//设置回零参数

                    return ret;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }
        public override int SetPosition(int acrCardId, int axisId, double position)
        {
            lock (obj)
            {
                try
                {
                    var res = LTDMC.dmc_set_position_unit((ushort)acrCardId, (ushort)axisId, position);
                    res = LTDMC.dmc_set_encoder_unit((ushort)acrCardId, (ushort)axisId, position);
                    return res;
                }
                catch (Exception)
                {

                    return -1;
                }

            }
        }
        public override int MoveAbs(int actCardId, int axisId, double position, double vel)
        {
            lock (obj)
            {
                try
                {
                    int res = 0;
                    double minvel = 0, maxvel = 0, acc = 0, dec = 0, stopvel = 0;
                    ushort s_mode = 0;
                    double s_time = 0.1, stop_time = 0.1;

                    LTDMC.dmc_get_profile_unit((ushort)actCardId, (ushort)axisId, ref minvel, ref maxvel, ref acc, ref dec,ref stopvel);

                    LTDMC.dmc_set_profile_unit((ushort)actCardId, (ushort)axisId, minvel, vel, acc, dec, stopvel);

                    LTDMC.dmc_get_s_profile((ushort)actCardId, (ushort)axisId, s_mode, ref s_time);

                    LTDMC.dmc_set_s_profile((ushort)actCardId, (ushort)axisId, 0, s_time);//设置S段速度参数

                    //LTDMC.dmc_get_dec_stop_time((ushort)actCardId, (ushort)axisId, ref stop_time);

                    //LTDMC.dmc_set_dec_stop_time((ushort)actCardId, (ushort)axisId, stop_time); //设置减速停止时间

                    res = LTDMC.dmc_pmove_unit((ushort)actCardId, (ushort)axisId, position, 1);

                    return res;
                }
                catch (Exception)
                {
                    return -1;

                }

            }
        }
        /// <summary>
        /// XY 轴直线插补
        /// </summary>
        /// <param name="actCardId">卡号</param>
        /// <param name="coordId">坐标系号</param>
        /// <param name="axes">加速度</param>
        /// <param name="positions"></param>
        /// <param name="vel">速度</param>
        public override int MoveLineAbs(int actCardId, int coordId, int[] axes, double[] positions, double vel)
        {
            lock (obj)
            {
                try
                {
                    int res = 0;
                    actCardId = 0;
                    double minvel = 0, maxvel = 0, acc = 0.1, dec = 0.1, stopvel = 0.1;
                    ushort[] axesNum = new ushort[2];
                    axesNum[0] = (ushort)axes[0];
                    axesNum[1] = (ushort)axes[1];
                    //LTDMC.dmc_get_vector_profile_unit((ushort)actCardId, (ushort)coordId, ref minvel, ref maxvel, ref acc, ref dec, ref stopvel);
                    LTDMC.dmc_set_vector_profile_unit((ushort)actCardId, (ushort)coordId, minvel, vel, acc, dec, stopvel); //设置插补运动速度曲线
                    //设置插补运动速度曲线的平滑时间
                    LTDMC.dmc_set_vector_s_profile(0, 0, 0, 0.1);
                    double[] pos = new double[2];
                    pos[0] = positions[0];
                    pos[1] = positions[1];
                    LTDMC.dmc_line_unit((ushort)actCardId, (ushort)coordId, 2, axesNum, pos, 1);
                    return res;
                }
                catch (Exception)
                {
                    return -1;

                }

            }
        }
        public override int MoveRel(int actCardId, int axisId, double distance, double vel)
        {
            lock (obj)
            {
                try
                {
                    double minvel = 0, maxvel = 0, acc = 0, dec = 0, stopvel = 0;

                    LTDMC.dmc_get_profile((ushort)actCardId, (ushort)axisId, ref minvel, ref maxvel, ref acc, ref dec,
                        ref stopvel);

                    LTDMC.dmc_set_profile((ushort)actCardId, (ushort)axisId, minvel, vel, 0.1, 0.1, stopvel);

                    LTDMC.dmc_set_s_profile((ushort)actCardId, (ushort)axisId, 0, 0.01);//设置S段速度参数

                    LTDMC.dmc_set_dec_stop_time((ushort)actCardId, (ushort)axisId, 0.1); //设置减速停止时间

                    var res = LTDMC.dmc_pmove_unit((ushort)actCardId, (ushort)axisId, distance, 0);
                    return res;
                }
                catch (Exception)
                {
                    return -1;

                }

            }
        }
        public override int Stop(int actCardId, int axisId)
        {
            lock (obj)
            {
                try
                {
                    var res = LTDMC.dmc_stop((ushort)actCardId, (ushort)axisId, 0);
                    return res;
                }
                catch (Exception)
                {

                    return -1;
                }

            }
        }
        public override int EStop(int actCardId, int axisId)
        {
            lock (obj)
            {
                try
                {
                    var res = LTDMC.dmc_stop((ushort)actCardId, (ushort)axisId, 1);
                    return res;
                }
                catch (Exception)
                {

                    return -1;
                }

            }
        }
        public override int GetMotionIo(int actCardId, int axisId, ref int sts)
        {
            lock (obj)
            {
                int _rSts;
                uint res = 1;
                try
                {
                    res = LTDMC.dmc_axis_io_status((ushort)actCardId, (ushort)axisId);//读取指定轴有关运动信号的状态
                                                                                      //读取60FD
                    //int statusword = 0;
                    //LTDMC.nmc_get_node_od((ushort)actCardId, (ushort)2, (ushort)1009, (ushort)24829, (ushort)0, (ushort)32, ref statusword);
                    //// 分解每一位
                    //bool[] bitStatus = new bool[32];
                    //for (int i = 0; i < 32; i++)
                    //{
                    //    bitStatus[i] = (statusword & (1u << i)) != 0; // ((statusword >> i) & 0x1) == 0x1;
                    //}
                }
                catch (Exception)
                {
                    return -1;
                }

                int _sts = (int)res;

                _rSts = 0;
                if (XConvert.BitEnable(_sts, 0x01 << 0))
                {
                    XConvert.SetBits(ref _rSts, Xleisai_Define.MIO_ALM);
                }
                if (XConvert.BitEnable(_sts, 0x01 << 1))
                {
                    XConvert.SetBits(ref _rSts, Xleisai_Define.MIO_PEL);
                }
                if (XConvert.BitEnable(_sts, 0x01 << 2))
                {
                    XConvert.SetBits(ref _rSts, Xleisai_Define.MIO_MEL);
                }
                if (XConvert.BitEnable(_sts, 0x01 << 3))
                {
                    XConvert.SetBits(ref _rSts, Xleisai_Define.MIO_EMG);
                }
                if (XConvert.BitEnable(_sts, 0x01 << 4))
                {
                    XConvert.SetBits(ref _rSts, Xleisai_Define.MIO_ORG);
                }
                if (XConvert.BitEnable(_sts, 0x01 << 6))
                {
                    XConvert.SetBits(ref _rSts, Xleisai_Define.MIO_SPEL);
                }
                if (XConvert.BitEnable(_sts, 0x01 << 7))
                {
                    XConvert.SetBits(ref _rSts, Xleisai_Define.MIO_SMEL);
                }
                //if (XConvert.BitEnable(_sts, 0x01 << 8))
                //{
                //    XConvert.SetBits(ref _rSts, Xleisai_Define.MIO_INP);
                //}
                sts = _rSts;
                return 0;
            }
        }
        public override int GetMotionSts(int actCardId, int axisId, ref int sts)
        {
            lock (obj)
            {
                int _rSts = 0;
                int res = 0;
                short tt = 0;
                try
                {
                    LTDMC.dmc_get_stop_reason((ushort)actCardId, (ushort)axisId, ref res);//读取轴停止原因
                                                                                          //tt = LTDMC.dmc_read_sevon_pin((ushort)actCardId, (ushort)axisId);
                }
                catch (Exception)
                {
                    return -1;
                }

                int _sts = (int)res;

                _rSts = 0;
                if (_sts == 0)
                {
                    XConvert.SetBits(ref _rSts, Xleisai_Define.MTS_MDN);
                }
                if (_sts == 1)
                {
                    XConvert.SetBits(ref _rSts, Xleisai_Define.MTS_ALM);
                }
                if (_sts == 15)
                {
                    XConvert.SetBits(ref _rSts, Xleisai_Define.MTS_OTHER);
                }
                if (_sts == 4)
                {
                    XConvert.SetBits(ref _rSts, Xleisai_Define.MTS_EMG);
                }

                sts = _rSts;
                return 0;
            }

        }
        public override int GetMotionPos(int actCardId, int axisId, ref double pos)
        {
            lock (obj)
            {
                try
                {
                    LTDMC.dmc_get_position_unit((ushort)actCardId, (ushort)axisId, ref pos);
                    //-----------------雷赛驱动器有些没有接反馈，只能读命令位置-------
                    //pos = LTDMC.dmc_get_encoder((ushort)actCardId, (ushort)axisId);
                    return 0;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }
        public override int GetCommandPos(int actCardId, int axisId, ref double pos)
        {
            lock (obj)
            {
                try
                {
                    LTDMC.dmc_get_position_unit((ushort)actCardId, (ushort)axisId, ref pos);
                    return 0;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }
        public override int GetEtherCatSts(int actCardId, int axisId, ref int sts)
        {
            lock (obj)
            {
                try
                {
                    ushort res = 0;
                    LTDMC.nmc_get_axis_state_machine((ushort)actCardId, (ushort)axisId, ref res);
                    sts = res;
                    return res;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }
        public override int MoveJog(int actCardId, int axisId, int dir)
        {
            lock (obj)
            {
                try
                {
                    LTDMC.dmc_vmove((ushort)actCardId, (ushort)axisId, (ushort)dir);
                    return 0;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }
        public override int MoveJog(int actCardId, int axisId, double vel, int dir)
        {
            lock (obj)
            {
                try
                {
                    double minvel = 0, maxvel = 0, acc = 0, dec = 0, stopvel = 0;
                    //LTDMC.dmc_set_equiv(_CardID, axis, dEquiv);  //设置脉冲当量
                    LTDMC.dmc_get_profile_unit((ushort)actCardId, (ushort)axisId, ref minvel, ref maxvel, ref acc, ref dec,
                        ref stopvel);
                    LTDMC.dmc_set_profile_unit((ushort)actCardId, (ushort)axisId, minvel, maxvel, acc, dec, vel);
                    LTDMC.dmc_set_s_profile((ushort)actCardId, (ushort)axisId, 0, 0.01);//设置S段速度参数
                    LTDMC.dmc_vmove((ushort)actCardId, (ushort)axisId, (ushort)dir);
                    return 0;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }
        public override int SetAxisAccAndDec(int actCardId, int axisId, double acc, double dec)
        {
            lock (obj)
            {
                try
                {
                    int ret = 0;
                    double minvel = 0, maxvel = 0, acc1 = 0, dec1 = 0, stopvel = 0;

                    LTDMC.dmc_get_profile_unit((ushort)actCardId, (ushort)axisId, ref minvel, ref maxvel, ref acc1, ref dec1, ref stopvel);

                    ret = LTDMC.dmc_set_profile_unit((ushort)actCardId, (ushort)axisId, minvel, maxvel, acc, dec, stopvel);//设置速度参数

                    LTDMC.dmc_set_s_profile((ushort)actCardId, (ushort)axisId, 0, 0.01);//设置S段速度参数 0.01

                    LTDMC.dmc_set_dec_stop_time((ushort)actCardId, (ushort)axisId, dec); //设置减速停止时间 0.1
                    return ret;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }
        public override int SetAxisAccAndDec(int actCardId, int axisId, double dMinVel, double Vel, double acc, double dec, double dStopVel,double sPara)
        {
            lock (obj)
            {
                try
                {
                    int ret = 0;

                    ret = LTDMC.dmc_set_profile_unit((ushort)actCardId, (ushort)axisId, dMinVel, Vel, acc, dec, dStopVel);//设置速度参数

                    LTDMC.dmc_set_s_profile((ushort)actCardId, (ushort)axisId, 0, sPara);//设置S段速度参数 0.01

                    //LTDMC.dmc_set_dec_stop_time((ushort)actCardId, (ushort)axisId, dStopVel); //设置减速停止时间 0.1
                    return ret;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }
        public override int SetAxisJogVel(int actCardId, int axisId, double vel)
        {
            lock (obj)
            {
                try
                {
                    int ret = 0;
                    double minvel = 0, maxvel = 0, stopvel = 0, acc = 0, dec = 0;
                    ret = LTDMC.dmc_get_profile_unit((ushort)actCardId, (ushort)axisId, ref minvel, ref maxvel, ref acc, ref dec,
                        ref stopvel);
                    LTDMC.dmc_set_profile_unit((ushort)actCardId, (ushort)axisId, minvel, vel, acc, dec, stopvel);
                    LTDMC.dmc_set_s_profile((ushort)actCardId, (ushort)axisId, 0, 0.01);//设置S段速度参数
                    return ret;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }
        public override int SetJogParam(int actCardId, int axisId, double acc, double dec, double vel)
        {
            lock (obj)
            {
                try
                {
                    int ret = 0;
                    double minvel = 0, maxvel = 0, stopvel = 0;
                    //LTDMC.dmc_set_equiv(_CardID, axis, dEquiv);  //设置脉冲当量
                    ret = LTDMC.dmc_get_profile_unit((ushort)actCardId, (ushort)axisId, ref minvel, ref maxvel, ref acc, ref dec,
                        ref stopvel);
                    //LTDMC.dmc_set_equiv(actCardId, axisId, 100); //设置脉冲当量为 100pulse/unit
                    LTDMC.dmc_set_profile_unit((ushort)actCardId, (ushort)axisId, minvel, vel, acc, dec, stopvel);
                    LTDMC.dmc_set_s_profile((ushort)actCardId, (ushort)axisId, 0, 0.01);//设置S段速度参数
                    return ret;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }
        //在线变速
        public override int ChangeAxisSpeed(int actCardId, int axisId,double newVel, double taccdcc)
        {
            lock (obj)
            {
                try
                {
                    int ret = 0;
                    //LTDMC.dmc_set_profile_unit((ushort)actCardId, (ushort)axisId, MyMin_Vel, MyMax_Vel, MyTacc, MyTdec,MyStop_Vel);
                    //LTDMC.dmc_pmove_unit((ushort)actCardId, (ushort)axisId, MyDist, Myposi_mode);//执行点位运动
                    ret = LTDMC.dmc_change_speed_unit((ushort)actCardId, (ushort)axisId, newVel, taccdcc);//在线变速，速度变为 unit/s
                    return ret;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }
        //在线变位
        public override int ChangeAxisTargetPosn(int actCardId, int axisId,double newPos)
        {
            lock (obj)
            {
                try
                {
                    int ret = 0;
                    //LTDMC.dmc_set_profile_unit((ushort)actCardId, (ushort)axisId, MyMin_Vel, MyMax_Vel, MyTacc, MyTdec,MyStop_Vel);
                    //LTDMC.dmc_pmove_unit((ushort)actCardId, (ushort)axisId, MyDist, Myposi_mode);//执行点位运动
                    ret = LTDMC.dmc_reset_target_position_unit((ushort)actCardId, (ushort)axisId, newPos);//在线变位,绝对位置
                    return ret;
                }
                catch (Exception)
                {
                    return -1;
                }
            }
        }
        /// <summary>
        /// 1表示停止，0表示运动中
        /// </summary>
        /// <param name="actCardId"></param>
        /// <param name="axisId"></param>
        /// <returns></returns>
        public override int CheckMoveDone(ushort actCardId, ushort axisId)
        {
            try
            {
                int ret1 = 0;
                int ret2 = 0;
                int ret = 0;
                ret1 = LTDMC.dmc_check_done(actCardId, axisId);
                ret2 = LTDMC.dmc_check_success_encoder(actCardId, axisId);
                if (ret1 == 1 && ret2 == 1)
                    ret = 1;
                else
                    ret = 0;
                return ret;
            }
            catch (Exception)
            {
                return -1;
            }

        }

        public override int CheckMoveDone(ushort actCardId, ushort axisId, bool bWait = true)
        {
            try
            {
                int ret = 0;
                if (bWait)
                {
                    while (LTDMC.dmc_check_done(actCardId, axisId) == 0)
                    //判断轴运动状态，等待运动完成
                    {
                        //Application.DoEvents();
                    }
                }
                if (LTDMC.dmc_check_success_encoder(actCardId, axisId) == 0)//检测编码器到位状态
                {
                    ret = 0;
                    //MessageBox.Show("编码器不到位!");
                }
                else
                {
                    ret = 1;
                    //MessageBox.Show("编码器到位!");
                }
                return ret;
            }
            catch (Exception)
            {
                return -1;
            }
        }
        /// <summary>
        /// 回零完成
        /// </summary>
        /// <param name="actCardId"></param>
        /// <param name="axisId"></param>
        /// <returns>state=1回零正常完成</returns>
        public override int CheckHomeDone(ushort actCardId, ushort axisId)
        {
            try
            {
                short ret = 0;
                ushort state = 2;
                ret = LTDMC.dmc_get_home_result(actCardId, axisId, ref state);
                return state;
            }
            catch (Exception)
            {
                return -1;
            }

        }
        public override int ClearALM(int actCardId, int axisId)
        {
            try
            {
                LTDMC.nmc_clear_axis_errcode((ushort)actCardId, (ushort)axisId);
                return LTDMC.dmc_clear_stop_reason((ushort)actCardId, (ushort)axisId);
            }
            catch (Exception)
            {
                return -1;
            }
        }
        public override int ClearPosition(int actCardId, int axisId)
        {
            try
            {
                LTDMC.dmc_set_position_unit((ushort)actCardId, (ushort)axisId, 0);
                return LTDMC.dmc_set_encoder_unit((ushort)actCardId, (ushort)axisId, 0);
            }
            catch (Exception)
            {

                return -1;
            }
        }
        public override int Write_rxpdo(int actCardId, int portNum, int address, int dataLen, int value)
        {
            try
            {
                return LTDMC.nmc_write_rxpdo_extra((ushort)actCardId, (ushort)portNum, (ushort)address, (ushort)dataLen, value);
            }
            catch (Exception)
            {

                return -1;
            }
        }

        public override int ClearEncoder(int actCardId, int channel)
        {
            int rtn = LTDMC.dmc_set_extra_encoder((ushort)actCardId, (ushort)channel, 0);//清除辅助编码器0的值
            if (0 != rtn)
            {
                return rtn;
            }
            return 0;
        }
        /// <summary>
        /// 获取轴编码器反馈位置
        /// </summary>
        /// <returns></returns>
        public override double GetEncPos(int actCardId, int axisId)  //编码器反馈位置
        {
            double pos = 0;
            LTDMC.dmc_get_encoder_unit((ushort)actCardId, (ushort)axisId, ref pos);
            return pos;
        }
        public override int SetExtraEncoder(int actCardId, int encNum, int pos)
        {
            int rtn = LTDMC.dmc_set_extra_encoder((ushort)actCardId, (ushort)encNum, pos);//辅助编码器计数值，单位：pulse
            if (rtn != 0)
            {
                //Show("dmc_set_encoder_unit", rtn, "设置辅助编码器的值");
            }
            return rtn;
        }
        /// <summary>
        /// 队列比较模式
        /// 多点位置比较
        /// 输出低电平有效
        /// </summary>
        /// <param name="lsPoint">比较点位</param>
        /// <param name="hcmp">比较器号，取值范围：0~5（对应硬件 OUT2~OUT7 端口）</param>
        /// <param name="encNum">  辅助编码器通道号
        /// <param name="cmp_logic">  触发电平
        /// <param name="ptime">脉冲宽度 微秒
        /// <returns></returns>
		public override int SetTriggerPosition(int nAxisID, int myhcmp, int mycmp_source, int mycmp_logic, int mytime, double[] adPositionArray)
        {
            int rtn = 0;
            ushort myCardNo = 0;
            ushort mycmp_mode = 4;
            LTDMC.dmc_hcmp_set_mode(myCardNo, (ushort)myhcmp, mycmp_mode);//队列比较模式
            LTDMC.dmc_hcmp_set_config(myCardNo, (ushort)myhcmp, (ushort)nAxisID, (ushort)mycmp_source, (ushort)mycmp_logic, mytime);
            LTDMC.dmc_hcmp_clear_points(myCardNo, (ushort)myhcmp);

            foreach (var position in adPositionArray)
            {
                rtn = LTDMC.dmc_hcmp_add_point(myCardNo, (ushort)myhcmp, (int)position);
            }
            if (rtn != 0)
            {
                //Show("dmc_set_encoder_unit", rtn, "设置辅助编码器的值");
            }
            return rtn;
        }

        public override int SetElmoDriveHomeSearch(int actCardId, int axisId, int mode, double dMinVel, double dMaxVel, double acc, double dec)
        {
            try
            {
                int ret = 0;
                short[] err = new short[3];
                err[0] = LTDMC.nmc_set_home_profile((ushort)actCardId, (ushort)axisId, (ushort)mode, dMinVel, dMaxVel, acc, dec, 0); //36
                err[1] = LTDMC.nmc_set_node_od((ushort)actCardId, 2, (ushort)(1001 + axisId), 0x6098, 0, 8, -3);
                //err[2] = LTDMC.nmc_home_move((ushort)actCardId, (ushort)axisId);
                return err[1];
            }
            catch (Exception)
            {
                return -1;
            }
        }

    }
}

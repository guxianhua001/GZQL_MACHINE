
/*----------------------------------------------------------------
* 命名空间: SmarterMotion.Framework.Device
*
* 类 名： XACSAxis
* 功 能： N/A
* 唯一标识：06ccd1a2-a0e2-4487-ae9b-025cd9d49cee
* 
* 变更日期：2023/8/22 16:22:04
* 作者：szb
* 公司：CYG
*----------------------------------------------------------------*/

using Core.Abstraction;
using System;

namespace SmarterMotion
{
    /// <summary>
    /// 以色列ACS运动控制卡
    /// </summary>
    public class XACSAxis : XObject, IAxis
    {
        private int actAxisId;
        private double lead;
        private XCard card;
        private string name;
        private int m_MotionIO;
        private int m_MotionSts;
        private double m_MotionPos;
        private double m_CommandPos;
        private bool isHomeOk;
        private bool hasEStoped = true;
        private bool m_HasServoOff = false;
        private bool m_ServoStsLast = false;
        private bool m_Feedback = true;

        public XACSAxis(int actAxisId, double lead, XCard card, string name)
        {
            this.actAxisId = actAxisId;
            this.lead = lead;
            this.card = card;
            this.name = name;
        }

        public int ActId
        {
            get { return this.actAxisId; }
        }

        public int SetId { get; set; }

        public int CardId { get; set; }

        public int TaskId { get; set; }

        public bool IsFeedback
        {
            get { return this.m_Feedback; }
            set { this.m_Feedback = value; }
        }
        public XAxisDirection AxisDirection { get; set; }

        public AxisStyle CurrentAxis
        {
            get { return AxisStyle.ACS_Axis; }
        }
        public int SetServo(bool on)
        {
            return card.SetServo(actAxisId, on);
        }
        public int GoHome()
        {
            return card.GoHome(actAxisId);
        }
        public int SetAxisJogParam(double acc, double dec, double vel)
        {
            return 0;
        }
        public int SetStopDec(double dec)
        {
            return 0;
        }

        public int SetPosition(int position)
        {
            return 0;
        }

        public int CleanALM()
        {
            return 0;
        }
        public int MoveAbs(double position, double vel)
        {
            //return card.MoveAbs(actAxisId, XConvert.MM2PULS(position, lead), XConvert.MM2PULS(vel, lead));
            return card.MoveAbs(actAxisId, position, vel);
        }
        public int MoveRel(double distance, double vel)
        {
            //return card.MoveRel(actAxisId, XConvert.MM2PULS(distance, lead), XConvert.MM2PULS(vel, lead));
            return card.MoveRel(actAxisId, distance, vel);
        }
        public int MoveJog(double vel, int dir)
        {
            //return card.MoveRel(actAxisId, XConvert.MM2PULS(distance, lead), XConvert.MM2PULS(vel, lead));
            return card.MoveJog(actAxisId, vel, dir);
        }
        public int Stop()
        {
            return card.Stop(actAxisId);
        }
        public int EStop()
        {
            return card.EStop(actAxisId);
        }
        public int Update()
        {
            lock (this)
            {
                if (!card.IsConnect)
                    return 0;
                int sts = 0;
                double sts1 = 0.0;
                card.GetMotionIo(actAxisId, ref sts);
                m_MotionIO = sts;
                card.GetMotionSts(actAxisId, ref sts);
                m_MotionSts = sts;
                card.GetMotionPos(actAxisId, ref sts1);
                m_MotionPos = sts1;
                card.GetCommandPos(actAxisId, ref sts1);
                m_CommandPos = sts1;

                if (IsSVON == false && m_ServoStsLast == true)
                {
                    m_HasServoOff = true;
                }
                m_ServoStsLast = IsSVON;

                return 0;
            }
        }
        public int SetHome(bool b)
        {
            lock (this)
            {
                isHomeOk = b;
                m_HasServoOff = false;
                return 0;
            }
        }
        public string Name
        {
            get
            {
                return name;
            }
        }
        public bool IsALM
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionIO, XACS_Define.FAULT_DRIVE);
                }
            }
        }
        public bool IsPEL
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionIO, XACS_Define.FAULT_RL);//故意取反
                }
            }
        }
        public bool IsMEL
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionIO, XACS_Define.FAULT_LL);//故意取反
                }
            }
        }
        public bool IsORG
        {
            get
            {
                lock (this)
                {
                    return false;
                }
            }
        }
        public bool IsEMG
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionIO, XACS_Define.FAULT_DRIVE);
                }
            }
        }
        public bool IsSVON
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionSts, XACS_Define.MST_ENABLE);
                }
            }
        }
        public bool HasSVONOFF
        {
            get
            {
                lock (this)
                {
                    return m_HasServoOff;
                }
            }
        }

        public bool IsHomeD
        {
            get
            {
                lock (this)
                {
                    return !XConvert.BitEnable(m_MotionSts, XACS_Define.MST_MOVE);//故意取反
                }
            }
        }
        public bool IsMDN
        {
            get
            {
                lock (this)
                {
                    return !XConvert.BitEnable(m_MotionSts, XACS_Define.MST_MOVE);//故意取反
                }
            }
        }
        public bool IsHMV
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionSts, XACS_Define.MST_MOVE);
                }
            }
        }
        public bool IsASTP
        {
            get
            {
                lock (this)
                {
                    //bool ishmv = IsHMV;
                    int result = 0;
                    ////card.ReadBufferIntVariable("homeFinished", actAxisId, out result);
                    if (result == 0)
                    {
                        return true;
                    }
                    return false;
                }
            }
        }
        public bool IsHomeOk
        {
            get
            {
                lock (this)
                {
                    return isHomeOk;
                }
            }
        }
        public bool HasEStoped
        {
            get
            {
                lock (this)
                {
                    return hasEStoped;
                }
            }
            set
            {
                lock (this)
                {
                    hasEStoped = value;
                }
            }
        }
        public double POS
        {
            get
            {
                lock (this)
                {
                    //return XConvert.PULS2MM(m_MotionPos, lead);
                    return m_MotionPos;
                }
            }
        }
        public double CommandPOS
        {
            get
            {
                lock (this)
                {
                    //return XConvert.PULS2MM(m_CommandPos, lead);
                    return m_CommandPos;
                }
            }
        }
        public int PULS
        {
            get
            {
                lock (this)
                {
                    int puls = (int)m_MotionPos;
                    return puls * 10000;
                }
            }
        }

        public double JogVelocityPercent { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public double MotionSpeedRatio { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public int SetAxisAccAndDec(double acc, double dec)
        {
            return card.SetAxisAccAndDec(actAxisId, acc, dec);
        }

        public int SetJerk(double jerkvalue)
        {
            return card.SetAxisJerk(actAxisId, jerkvalue);
        }

        public int SetPosition(double position)
        {
            throw new NotImplementedException();
        }

        public int SetHomeMode(int mode)
        {
            throw new NotImplementedException();
        }

        public int MoveJog(double vel)
        {
            throw new NotImplementedException();
        }

        public bool CheckHomeDone()
        {
            throw new NotImplementedException();
        }

        public int ClearPosition()
        {
            throw new NotImplementedException();
        }

        public int MoveJog(int dir)
        {
            throw new NotImplementedException();
        }

        public int CheckMoveDone()
        {
            throw new NotImplementedException();
        }

        int IAxis.CheckHomeDone()
        {
            throw new NotImplementedException();
        }

        public int SetAxisJogVel(double vel)
        {
            throw new NotImplementedException();
        }

        public int CheckMoveDone(bool bWait)
        {
            throw new NotImplementedException();
        }

        public int CheckMoveDone(int axisId, bool bWait)
        {
            throw new NotImplementedException();
        }

        public int SetAxisAccAndDec(double dMinVel, double dMaxVel, double acc, double dec, double dStopVel)
        {
            throw new NotImplementedException();
        }

        public int SetAxisAccAndDec(double dMinVel, double dVel, double acc, double dec, double dStopVel, double sPara)
        {
            throw new NotImplementedException();
        }
    }
}

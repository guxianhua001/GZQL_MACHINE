
/*----------------------------------------------------------------
* 命名空间: SmarterMotion.Framework.Device
*
* 类 名： XADLINKAxis
* 功 能： N/A
* 唯一标识：25669b99-448e-401c-a12f-488b29cd4426
* 
* 变更日期：2023/8/22 16:32:16
* 作者：szb
* 公司：CYG
*----------------------------------------------------------------*/

using Core.Abstraction;
using System;

namespace SmarterMotion
{
    /// <summary>
    /// 凌华轴
    /// </summary>
    public class XADLINKAxis : XObject, IAxis
    {
        private int actAxisId;
        private double lead;
        private XCard card;
        private string name;
        private int m_MotionIO;
        private int m_MotionSts;
        private int m_MotionPos;
        private int m_CommandPos;
        private bool isHomeOk;
        private bool hasEStoped = true;
        private bool m_HasServoOff = false;
        private bool m_ServoStsLast = false;
        private bool m_Feedback = true;

        public XADLINKAxis(int actAxisId, double lead, XCard card, string name)
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

        public XAxisDirection AxisDirection { get; set; }

        public AxisStyle CurrentAxis
        {
            get { return AxisStyle.ADLINK_Axis; }
        }
        public bool IsFeedback
        {
            get { return this.m_Feedback; }
            set { this.m_Feedback = value; }
        }

        public int SetServo(bool on)
        {
            return card.SetServo(actAxisId, on);
        }
        public int GoHome()
        {
            return card.GoHome(actAxisId);
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
            return card.MoveAbs(actAxisId, XConvert.MM2PULS(position, lead), XConvert.MM2PULS(vel, lead));
        }
        public int MoveJog(double isStart)
        {
            return card.MoveJog(actAxisId, (int)isStart);
        }

        public int MoveRel(double distance, double vel)
        {
            return card.MoveRel(actAxisId, XConvert.MM2PULS(distance, lead), XConvert.MM2PULS(vel, lead));
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
                int sts = 0;
                card.GetMotionIo(actAxisId, ref sts);
                m_MotionIO = sts;
                card.GetMotionSts(actAxisId, ref sts);
                m_MotionSts = sts;
                card.GetMotionPos(actAxisId, ref sts);
                m_MotionPos = sts;
                card.GetCommandPos(actAxisId, ref sts);
                m_CommandPos = sts;

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
                    return XConvert.BitEnable(m_MotionIO, XAPS_Define.MIO_ALM);
                }
            }
        }
        public bool IsPEL
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionIO, XAPS_Define.MIO_PEL);
                }
            }
        }
        public bool IsMEL
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionIO, XAPS_Define.MIO_MEL);
                }
            }
        }
        public bool IsORG
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionIO, XAPS_Define.MIO_ORG);
                }
            }
        }
        public bool IsEMG
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionIO, XAPS_Define.MIO_EMG);
                }
            }
        }
        public bool IsSVON
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionIO, XAPS_Define.MIO_SVON);
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
                    return XConvert.BitEnable(m_MotionSts, XAPS_Define.MTS_MDN);
                }
            }
        }

        public bool IsMDN
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionSts, XAPS_Define.MTS_MDN);
                }
            }
        }
        public bool IsHMV
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionSts, XAPS_Define.MTS_HMV);
                }
            }
        }
        public bool IsASTP
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionSts, XAPS_Define.MTS_ASTP);
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
                    return XConvert.PULS2MM(m_MotionPos, lead);
                }
            }
        }
        public double CommandPOS
        {
            get
            {
                lock (this)
                {
                    return XConvert.PULS2MM(m_CommandPos, lead);
                }
            }
        }
        public int PULS
        {
            get
            {
                lock (this)
                {
                    return m_MotionPos;
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
            return 0;
        }
        public int SetStopDec(double dec)
        {
            return card.SetStopDec(actAxisId, lead, dec);
        }
        public int SetAxisJogParam(double acc, double dec, double vel)
        {
            return card.SetJogParam(actAxisId, acc, dec, vel);
        }

        //public int APS_SetBacklashEn(int on)
        //{
        //    return card.APS_SetBacklashEnable(actAxisId, on);
        //}

        public int SetPosition(double position)
        {
            throw new NotImplementedException();
        }

        public int SetHomeMode(int mode)
        {
            throw new NotImplementedException();
        }

        public int MoveJog(double vel, int dir)
        {
            throw new NotImplementedException();
        }

        public int CheckHomeDone()
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

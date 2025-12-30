

using Core.Abstraction;

namespace SmarterMotion
{
    public class XleisaiAxis_EtherCAT : XObject, IAxis
    {
        private int actAxisId;
        private double lead;
        private XCard card;
        private string name;
        private int m_MotionIO;
        private int m_MotionSts;
        private double m_MotionPos;
        private double m_CommandPos;
        private double m_MotionVel;
        private bool isHomeOk;
        private bool hasEStoped = true;
        private bool m_HasServoOff = false;
        private bool m_ServoStsLast = false;
        private bool m_Feedback = true;
        private bool checkDone = true;
        private bool IsHomeDone = true;
        private double[] m_Value = new double[8];

        public XleisaiAxis_EtherCAT(int actAxisId, XCard card, string name)
        {
            this.actAxisId = actAxisId;
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

        public int AxisAdr { get; set; }

        public XAxisDirection AxisDirection { get; set; }

        public AxisStyle CurrentAxis
        {
            get { return AxisStyle.Leisai_Axis; }
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

        public int SetPosition(double position)
        {
            return card.SetPosition(actAxisId, position, lead);
        }

        public int CleanALM()
        {
            return card.ClearALM(actAxisId);
        }

        public int MoveAbs(double position, double vel)
        {
            return card.MoveAbs(actAxisId, position, vel);
        }
        public int MoveJog(double dir)
        {
            return card.MoveJog(actAxisId, (int)dir);
        }

        public int MoveRel(double distance, double vel)
        {
            return card.MoveRel(actAxisId, distance, vel);
        }
        public int Stop()
        {
            return card.Stop(actAxisId);
        }
        public int EStop()
        {
            return card.EStop(actAxisId);
        }
        //循环刷新
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
                double vel = 0;
                //card.GetMotionVel(actAxisId, ref vel);
                //m_MotionVel = vel;

                if (IsSVON == false && m_ServoStsLast == true)
                {
                    m_HasServoOff = true;
                }
                m_ServoStsLast = IsSVON;

                if (card.CheckMoveDone((ushort)actAxisId) == 1)
                {
                    checkDone = true;
                }
                else
                {
                    checkDone = false;
                }
                if (card.CheckHomeDone((ushort)actAxisId) == 1)
                {
                    IsHomeDone = true;
                }
                else
                {
                    IsHomeDone = false;
                }
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
                    return XConvert.BitEnable(m_MotionIO, Xleisai_Define.MIO_ALM);
                }
            }
        }
        public bool IsPEL
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionIO, Xleisai_Define.MIO_PEL);
                }
            }
        }
        public bool IsMEL
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionIO, Xleisai_Define.MIO_MEL);
                }
            }
        }
        public bool IsORG
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionIO, Xleisai_Define.MIO_ORG);
                }
            }
        }
        public bool IsEMG
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionIO, Xleisai_Define.MIO_EMG);
                }
            }
        }
        public bool IsSVON
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionSts, Xleisai_Define.MTS_SVON);
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

        public bool IsArrived { get; }

        public bool IsHomeD
        {
            get
            {
                lock (this)
                {
                    return IsHomeDone;

                }
            }
        }

        public bool IsMDN
        {
            get
            {
                lock (this)
                {
                    return checkDone;
                    //return XConvert.BitEnable(m_MotionSts, Xleisai_Define.MTS_MDN);
                }
            }
        }
        public bool IsHMV
        {
            get
            {
                lock (this)
                {
                    return XConvert.BitEnable(m_MotionSts, Xleisai_Define.MTS_ALM);
                }
            }
        }
        public bool IsASTP
        {
            get
            {
                lock (this)
                {

                    return XConvert.BitEnable(m_MotionSts, Xleisai_Define.MTS_OTHER);
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
                    return 0;
                }
            }
        }

        public double VEL
        {
            get
            {
                lock (this)
                {
                    return m_MotionVel;
                }
            }
        }

        XAxisDirection IAxis.AxisDirection { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public double JogVelocityPercent { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public double MotionSpeedRatio { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public int SetAxisAccAndDec(double acc, double dec)
        {
            return card.SetAxisAccAndDec(actAxisId, acc, dec);
        }

        //不用的方法
        public int SetJerk(double jerkvalue)
        {
            return 0;
        }
        public int SetStopDec(double dec)
        {
            return 0;
        }

        public int SetAxisJogParam(double acc, double dec, double vel)
        {
            return card.SetJogParam(actAxisId, acc, dec, vel);
        }

        //public int APS_SetBacklashEn(int on)
        //{
        //    return card.APS_SetBacklashEnable(actAxisId, on);
        //}

        public int SetVelocity(double velocity)
        {
            throw new System.NotImplementedException();
        }

        public int MoveJog(int isStart)
        {
            return card.MoveJog(actAxisId, isStart);
        }

        public int ResetServo(int axisId)
        {
            throw new System.NotImplementedException();
        }

        public int SetEStop(bool on)
        {
            throw new System.NotImplementedException();
        }

        public int SetPosition(int position)
        {
            throw new System.NotImplementedException();
        }

        public int SetHomeMode(int mode)
        {
            throw new System.NotImplementedException();
        }

        public int MoveJog(double vel, int dir)
        {
            throw new System.NotImplementedException();
        }

        public bool CheckHomeDone()
        {
            throw new System.NotImplementedException();
        }

        public int ClearPosition()
        {
            throw new System.NotImplementedException();
        }

        public int CheckMoveDone()
        {
            throw new System.NotImplementedException();
        }

        int IAxis.CheckHomeDone()
        {
            throw new System.NotImplementedException();
        }

        public int SetAxisJogVel(double vel)
        {
            throw new System.NotImplementedException();
        }

        public int CheckMoveDone(bool bWait)
        {
            throw new System.NotImplementedException();
        }

        public int CheckMoveDone(int axisId, bool bWait)
        {
            throw new System.NotImplementedException();
        }

        public int SetAxisAccAndDec(double dMinVel, double dMaxVel, double acc, double dec, double dStopVel)
        {
            throw new System.NotImplementedException();
        }

        public int SetAxisAccAndDec(double dMinVel, double dVel, double acc, double dec, double dStopVel, double sPara)
        {
            throw new System.NotImplementedException();
        }
    }

}


using Core.Abstraction;

namespace SmarterMotion
{
    /// <summary>
    /// 雷赛轴
    /// </summary>
    public class XleisaiAxis : XObject, IAxis
    {
        private int actAxisId;
        private double lead;
        private int puls;
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
        private bool checkDone = true;
        //private bool IsHomeDone = true;
        private double[] m_Value = new double[8];
        private double m_JogVelocityPercent = 0;
        private double m_MotionSpeedRatio = 0;
        public XleisaiAxis(int actAxisId, double lead, XCard card, string name)
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

        public int SetPosition(int position)
        {
            return card.SetPosition(actAxisId, position);
        }

        public int CleanALM()
        {
            return card.ClearALM(actAxisId);
        }

        public int MoveAbs(double position, double vel)
        {
            //return card.MoveAbs(actAxisId, XConvert.MM2PULS(position, lead), XConvert.MM2PULS(vel, lead));
            return card.MoveAbs(actAxisId, position, vel);
        }
        public int MoveJog(int dir)
        {
            return card.MoveJog(actAxisId, dir);
        }

        public int MoveRel(double distance, double vel)
        {
            //return card.MoveRel(actAxisId, XConvert.MM2PULS(distance, lead), XConvert.MM2PULS(vel, lead));
            return card.MoveRel(actAxisId, distance, vel);
        }
        public int MoveLineAbs(int coordId, int[] axisId, double[] pos, double vel)
        {
            return -1;// card.MoveLineAbs(coordId, axisId, pos, vel);
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

                //card.GetMotionPos(actAxisId, ref m_MotionPos);
                //card.GetCommandPos(actAxisId, ref m_CommandPos);

                if (IsSVON == false && m_ServoStsLast == true)
                {
                    m_HasServoOff = true;
                }
                m_ServoStsLast = IsSVON;

                //if (card.CheckMoveDone((ushort)actAxisId) == 1)
                //{
                //    checkDone = true;
                //}
                //else
                //{
                //    checkDone = false;
                //}
                //if (card.CheckHomeDone((ushort)actAxisId) == 1)
                //{
                //    isHomeOk = true;
                //}
                //else
                //{
                //    isHomeOk = false;
                //}
                return 0;
            }
        }
        public int SetHome(bool b)
        {
            lock (this)
            {
                //isHomeOk = b;
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
                    int sts = 0;
                    card.GetEtherCatSts(actAxisId, ref sts);
                    if (sts == 4)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                    //return XConvert.BitEnable(sts, Xleisai_Define.ENABLE);
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

        //public bool IsHomeD
        //{
        //    get
        //    {
        //        lock (this)
        //        {
        //            return IsHomeDone;

        //        }
        //    }
        //}

        public bool IsMDN
        {
            get
            {
                lock (this)
                {
                    return CheckMoveDone() == 1 ? true : false;
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
                    return CheckHomeDone() == 1 ? true : false;
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
                    card.GetMotionPos(actAxisId, ref m_MotionPos);
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
                    card.GetCommandPos(actAxisId, ref m_CommandPos);
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
                    return puls;
                }
            }
        }


        public double JogVelocityPercent
        {
            get { return m_JogVelocityPercent; }
            set { m_JogVelocityPercent = value; }
        }

        public double MotionSpeedRatio
        {
            get { return m_MotionSpeedRatio; }
            set { m_MotionSpeedRatio = value; }
        }

        public int GetADinput(ushort channel, ref double Value)
        {
            return card.GetADinput(channel, ref Value);
        }

        public int SetAxisAccAndDec(double acc, double dec)
        {
            return card.SetAxisAccAndDec(actAxisId, acc, dec);
        }
        public int SetAxisAccAndDec(double dMinVel, double dMaxVel, double acc, double dec, double dStopVel)
        {
            return card.SetAxisAccAndDec(actAxisId, dMinVel, dMaxVel, acc, dec, dStopVel);
        }
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

        public int ClearPosition()
        {
            return card.ClearPosition(actAxisId);
        }
        public int CheckMoveDone()
        {
            return card.CheckMoveDone((ushort)actAxisId);//返回1是轴停止状态
        }
        public int SetHomeMode(int mode)
        {
            throw new System.NotImplementedException();
        }

        public int MoveJog(double vel, int dir)
        {
            return card.MoveJog(actAxisId, vel, dir);
        }

        public int CheckHomeDone()
        {
            return card.CheckHomeDone((ushort)actAxisId);
        }

        public int SetPosition(double position)
        {
            throw new System.NotImplementedException();
        }

        public int SetAxisJogVel(double vel)
        {
            return card.SetAxisJogVel(actAxisId, vel);
        }

        public int CheckMoveDone(int axisId, bool bWait)
        {
          return  card.CheckMoveDone((ushort)axisId, bWait);//返回1是轴停止状态
        }

        public int SetAxisAccAndDec(double dMinVel, double dVel, double acc, double dec, double dStopVel, double sPara)
        {
            return card.SetAxisAccAndDec(actAxisId, dMinVel, dVel, acc, dec, dStopVel, sPara);
        }
    }

}

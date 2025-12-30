

namespace Core.Abstraction
{
    /// <summary>
    /// 轴信息
    /// </summary>
    public interface IAxis
    {
        string Name { get; }
        bool IsALM { get; }
        bool IsPEL { get; }
        bool IsMEL { get; }
        bool IsORG { get; }
        bool IsEMG { get; }
        bool IsSVON { get; }
        bool HasSVONOFF { get; }
        bool IsMDN { get; }

        //bool IsHomeD { get; }
        bool IsHMV { get; }
        bool IsASTP { get; }
        bool IsHomeOk { get; }
        bool HasEStoped { get; set; }
        double POS { get;  }
        double CommandPOS { get; }
        int PULS { get; }

        //////////////////////////////////////////////////////
        int ActId { get; }
        int SetId { get; set; }
        int CardId { get; set; }
        int TaskId { get; set; }
        bool IsFeedback
        { get; set; }
        int SetServo(bool on);
        int GoHome();
        int SetPosition(int position);
        int SetPosition(double position);
        int CleanALM();
        int MoveAbs(double position, double vel);
        int MoveRel(double distance, double vel);
        int MoveJog(int dir);
        int MoveJog(double vel, int dir);
        int Stop();
        int EStop();
        int Update();
        int SetHome(bool b);
        int SetHomeMode(int mode);
        int SetAxisAccAndDec(double acc, double dec);
        int SetAxisAccAndDec(double dMinVel, double dMaxVel, double acc, double dec, double dStopVel);
        int SetAxisAccAndDec(double dMinVel, double dVel, double acc, double dec, double dStopVel,double sPara);
        int SetJerk(double jerkvalue);
        double MotionSpeedRatio { get; set; }
        double JogVelocityPercent { get; set; }
        XAxisDirection AxisDirection { get; set; }
        AxisStyle CurrentAxis { get; }
        int SetAxisJogVel(double vel);
        int SetAxisJogParam(double acc, double dec, double vel);
        int SetStopDec(double dec);
        int ClearPosition();
        int CheckMoveDone();
        int CheckMoveDone(int axisId, bool bWait);
        int CheckHomeDone();
    }
}
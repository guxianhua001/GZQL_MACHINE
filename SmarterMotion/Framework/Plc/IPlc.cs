
namespace SmarterMotion.Framework.Plc
{

    /// <summary>
    /// plc通讯控制接口
    /// </summary>
    public interface IPlc
    {
        int AxisId { get; }

        string AxisName { get; set; }

        int AxisAdr { get; set; }


        int MotorOn(int axisId);

        int MotorOff(int axisId);

        int GoHome(int axisId);

        int ClearALM(int axisId);

        int Stop(int axisId);

        int MoveStop();

        int MoveAbs(int axisId, double position, double vel);

        int MoveJogP(int axisId, double vel);

        int MoveJogN(int axisId, double vel);

        int SetAxisVel(double vel);

        int SetAxisAccAndDec(double acc, double dec);

        int SetAxisPos(double pos);

        bool CheckMoveDone(int axisId);

        bool CheckHomeDone(int axisId);

        double GetMotionPos(int axisAdr);

        double GetMotionVel(int axisAdr);

        int GetMotionStatus(int axisAdr);

    }

}


namespace MotionControl.Interfaces
{
    public interface IMotionCard
    {
        int CardId { get; }
        int CheckEtherCatStatus();
        int Initialize();
        int Close();
        int SoftReset();
        int LoadConfig(string configPath);

        int SetServo(int axisId, bool enable);
        int MoveAbs(int axisId, double position, double velocity);
        int MoveRel(int axisId, double distance, double velocity);
        int GoHome(int axisId);
        int SetHomeMode(int axisId, int mode, double minVel, double maxVel);

        int Stop(int axisId);
        int EStop(int axisId);

        double GetPosition(int axisId);
        int GetMotionIO(int axisId, ref int status);
        /// <summary>读取轴运动状态字（dmc_get_stop_reason，与 GetMotionIO 分离）</summary>
        int GetMotionSts(int axisId, ref int status);
        /// <summary>读取 EtherCAT 轴状态机（nmc_get_axis_state_machine，IsSVON 判断 sts==4）</summary>
        int GetEtherCatSts(int axisId, ref int status);
        int ClearAlarm(int axisId);

        int SetDo(int port, int value);
        int GetDi(int port, ref int value);
        int GetDo(int port, ref int value);
        int MoveLineAbs(int coordId, int[] axisIds, double[] positions, double velocity);
        int MoveJog(int axisId, int direction);

        int CheckDone(int axisId);
        int CheckHomeDone(int axisId);
        int CheckCoordDone(int coordId);

        // 连续插补
        int SetVectorProfileUnit(int coordId, double startVel, double maxVel, double acc, double dec, double endVel);
        int ContiSetLookaheadMode(int coordId, int mode, int fifoSize, int reserved1, int reserved2);
        int SetVectorSProfile(int coordId, int reserved, double sPara);
        int SetArcLimit(int coordId, int reserved1, int reserved2, int reserved3);
        int ContiOpenList(int coordId, int axisCount, int[] axisIds);
        int ContiLineUnit(int coordId, int axisCount, int[] axisIds, double[] targetPos, ushort posiMode, int mark);
        int ContiStartList(int coordId);
        int ContiCloseList(int coordId);
        int ContiPauseList(int coordId);
        int CheckCoordMotionDone(int coordId);

        #region 轴参数设置（用于 AxisSetting 参数配置）

        int SetPulseEquivalent(int axisId, double pulsePerUnit);

        int SetEmergencyStopMode(int axisId, bool enabled, int logicLevel);

        int SetAxisIOMap(int axisId, int ioType, int mapIoType, int mapIoIndex, double filterTime);

        int SetHomeProfile(int axisId, int mode, double lowSpeed, double highSpeed, double accTime, double decTime, double offset);

        int SetProfileUnit(int axisId, double startVel, double maxVel, double accTime, double decTime, double stopVel);

        int SetSProfile(int axisId, int reserved, double sPara);

        int SetDecStopTime(int axisId, double decStopTime);

        #endregion

        #region 轴参数读取（用于 AxisSetting 从卡读取参数）

        int GetPulseEquivalent(int axisId, ref double pulsePerUnit);

        int GetEmergencyStopMode(int axisId, ref bool enabled, ref int logicLevel);

        int GetAxisIOMap(int axisId, int ioType, ref int mapIoType, ref int mapIoIndex, ref double filterTime);

        int GetHomeProfile(int axisId, ref int mode, ref double lowSpeed, ref double highSpeed, ref double accTime, ref double decTime, ref double offset);

        int GetProfileUnit(int axisId, ref double startVel, ref double maxVel, ref double accTime, ref double decTime, ref double stopVel);

        int GetSProfile(int axisId, int reserved, ref double sPara);

        int GetDecStopTime(int axisId, ref double decStopTime);

        int GetVectorProfileUnit(int coordId, ref double startVel, ref double maxVel, ref double accTime, ref double decTime, ref double endVel);

        int GetVectorSProfile(int coordId, int reserved, ref double sPara);

        #endregion
    }
}
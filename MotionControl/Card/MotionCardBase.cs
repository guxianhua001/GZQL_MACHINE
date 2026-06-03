using MotionControl.Interfaces;

namespace MotionControl.Card
{
    public abstract class MotionCardBase : IMotionCard
    {
        public abstract int CardId { get; }
        public abstract int CheckEtherCatStatus();
        public abstract int Initialize();
        public abstract int Close();
        public abstract int SoftReset();
        public abstract int LoadConfig(string configPath);
        public abstract int SetServo(int axisId, bool enable);
        public abstract int MoveAbs(int axisId, double position, double velocity);
        public abstract int MoveRel(int axisId, double distance, double velocity);
        public abstract int GoHome(int axisId);
        public abstract int SetHomeMode(int axisId, int mode, double minVel, double maxVel);
        public abstract int Stop(int axisId);
        public abstract int EStop(int axisId);
        public abstract double GetPosition(int axisId);
        public abstract int GetMotionIO(int axisId, ref int status);
        public abstract int GetMotionSts(int axisId, ref int status);
        public abstract int GetEtherCatSts(int axisId, ref int status);
        public abstract int ClearAlarm(int axisId);
        public abstract int ClearPosition(int axisId);
        public abstract int SetDo(int port, int value);
        public abstract int GetDi(int port, ref int value);
        public abstract int GetDo(int port, ref int value);
        public abstract int MoveLineAbs(int coordId, int[] axisIds, double[] positions, double velocity);
        public abstract int MoveJog(int axisId, int direction, double speed);
        public abstract int CheckDone(int axisId);
        public abstract int CheckHomeDone(int axisId);
        public abstract int CheckCoordDone(int coordId);

        public abstract int SetTriggerPosition(int nAxisID, int myhcmp, int mycmp_source, int mycmp_logic, int mytime, double[] adPositionArray);
        public abstract int ClearEncoder(int channel);
        public abstract int Write_rxpdo(int portNum, int address, int dataLen, int value);

        // 连续插补
        public abstract int SetVectorProfileUnit(int coordId, double startVel, double maxVel, double acc, double dec, double endVel);
        public abstract int ContiSetLookaheadMode(int coordId, int mode, int fifoSize, int reserved1, int reserved2);
        public abstract int SetVectorSProfile(int coordId, int reserved, double sPara);
        public abstract int SetArcLimit(int coordId, int reserved1, int reserved2, int reserved3);
        public abstract int ContiOpenList(int coordId, int axisCount, int[] axisIds);
        public abstract int ContiLineUnit(int coordId, int axisCount, int[] axisIds, double[] targetPos, ushort posiMode, int mark);
        public abstract int ContiStartList(int coordId);
        public abstract int ContiCloseList(int coordId);
        public abstract int ContiPauseList(int coordId);
        public abstract int CheckCoordMotionDone(int coordId);

        // 轴参数设置（用于 AxisSetting 参数配置）
        public abstract int SetPulseEquivalent(int axisId, double pulsePerUnit);
        public abstract int SetEmergencyStopMode(int axisId, bool enabled, int logicLevel);
        public abstract int SetAxisIOMap(int axisId, int ioType, int mapIoType, int mapIoIndex, double filterTime);
        public abstract int SetHomeProfile(int axisId, int mode, double lowSpeed, double highSpeed, double accTime, double decTime, double offset);
        public abstract int SetProfileUnit(int axisId, double startVel, double maxVel, double accTime, double decTime, double stopVel);
        public abstract int SetSProfile(int axisId, int reserved, double sPara);
        public abstract int SetDecStopTime(int axisId, double decStopTime);

        // 轴参数读取（用于 AxisSetting 从卡读取参数）
        public abstract int GetPulseEquivalent(int axisId, ref double pulsePerUnit);
        public abstract int GetEmergencyStopMode(int axisId, ref bool enabled, ref int logicLevel);
        public abstract int GetAxisIOMap(int axisId, int ioType, ref int mapIoType, ref int mapIoIndex, ref double filterTime);
        public abstract int GetHomeProfile(int axisId, ref int mode, ref double lowSpeed, ref double highSpeed, ref double accTime, ref double decTime, ref double offset);
        public abstract int GetProfileUnit(int axisId, ref double startVel, ref double maxVel, ref double accTime, ref double decTime, ref double stopVel);
        public abstract int GetSProfile(int axisId, int reserved, ref double sPara);
        public abstract int GetDecStopTime(int axisId, ref double decStopTime);
        public abstract int GetVectorProfileUnit(int coordId, ref double startVel, ref double maxVel, ref double accTime, ref double decTime, ref double endVel);
        public abstract int GetVectorSProfile(int coordId, int reserved, ref double sPara);
    }
}
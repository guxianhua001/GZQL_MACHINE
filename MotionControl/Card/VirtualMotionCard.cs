using MotionControl.Services;

namespace MotionControl.Card
{
    /// <summary>
    /// 无硬件时的虚拟运动卡，所有操作返回成功（0）并提供默认值
    /// 模拟 DI/DO 状态变化，支持 IO 显示面板的读写测试
    /// DI 默认值为 0（低电平），IO面板显示直观：0=OFF/灰色，1=ON/绿色
    /// 安全信号极性由 SystemStateService.IsSignalActive 统一处理
    /// </summary>
    public class VirtualMotionCard : MotionCardBase
    {
        private int _virtualCardId;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, int> _doStates = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, int> _diStates = new();
        /// <summary>虚拟轴伺服使能状态（供 GetMotionSts 返回 MTS_SVON）</summary>
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, bool> _servoEnabled = new();

        public VirtualMotionCard(int cardId = -1)
        {
            _virtualCardId = cardId;
        }

        public override int CardId => _virtualCardId;
        public override int Initialize() => 0;
        public override int Close() => 0;
        public override int SoftReset() => 0;
        public override int LoadConfig(string configPath) => 0;
        public override int SetServo(int axisId, bool enable)
        {
            _servoEnabled[axisId] = enable;
            return 0;
        }
        public override int MoveAbs(int axisId, double position, double velocity) => 0;
        public override int MoveRel(int axisId, double distance, double velocity) => 0;
        public override int GoHome(int axisId) => 0;
        public override int SetHomeMode(int axisId, int mode, double minVel, double maxVel) => 0;
        public override int Stop(int axisId) => 0;
        public override int EStop(int axisId) => 0;
        public override double GetPosition(int axisId) => 0.0;
        public override int GetMotionIO(int axisId, ref int status) { status = 0; return 0; }
        public override int GetMotionSts(int axisId, ref int status)
        {
            status = Leisai_Define.MTS_MDN;
            if (_servoEnabled.GetValueOrDefault(axisId, false))
                status |= Leisai_Define.MTS_SVON;
            return 0;
        }
        public override int ClearAlarm(int axisId) => 0;
        public override int SetDo(int port, int value)
        {
            _doStates[port] = value;
            return 0;
        }
        public override int GetDi(int port, ref int value)
        {
            value = _diStates.GetValueOrDefault(port, 0);
            return 0;
        }
        public override int GetDo(int port, ref int value)
        {
            value = _doStates.GetValueOrDefault(port, 0);
            return 0;
        }
        public override int MoveLineAbs(int coordId, int[] axisIds, double[] positions, double velocity) => 0;
        public override int MoveJog(int axisId, int direction) => 0;
        public override int CheckDone(int axisId) => 1; // 始终认为运动完成
        public override int CheckHomeDone(int axisId) => 1;
        public override int CheckEtherCatStatus() => 0; // 总线正常
        public override int Write_rxpdo(int portNum, int address, int dataLen, int value) => 0;
        public override int ClearEncoder(int channel) => 0;
        public override int SetTriggerPosition(int nAxisID, int myhcmp, int mycmp_source, int mycmp_logic, int mytime, double[] adPositionArray) => 0;

        public override int CheckCoordDone(int coordId) => 1;

        // 连续插补（虚拟卡全部返回成功）
        public override int SetVectorProfileUnit(int coordId, double startVel, double maxVel, double acc, double dec, double endVel) => 0;
        public override int ContiSetLookaheadMode(int coordId, int mode, int fifoSize, int reserved1, int reserved2) => 0;
        public override int SetVectorSProfile(int coordId, int reserved, double sPara) => 0;
        public override int SetArcLimit(int coordId, int reserved1, int reserved2, int reserved3) => 0;
        public override int ContiOpenList(int coordId, int axisCount, int[] axisIds) => 0;
        public override int ContiLineUnit(int coordId, int axisCount, int[] axisIds, double[] targetPos, ushort posiMode, int mark) => 0;
        public override int ContiStartList(int coordId) => 0;
        public override int ContiCloseList(int coordId) => 0;
        public override int ContiPauseList(int coordId) => 0;
        public override int CheckCoordMotionDone(int coordId) => 1;

        // 轴参数设置（虚拟卡全部返回成功）
        public override int SetPulseEquivalent(int axisId, double pulsePerUnit) => 0;
        public override int SetEmergencyStopMode(int axisId, bool enabled, int logicLevel) => 0;
        public override int SetAxisIOMap(int axisId, int ioType, int mapIoType, int mapIoIndex, double filterTime) => 0;
        public override int SetHomeProfile(int axisId, int mode, double lowSpeed, double highSpeed, double accTime, double decTime, double offset) => 0;
        public override int SetProfileUnit(int axisId, double startVel, double maxVel, double accTime, double decTime, double stopVel) => 0;
        public override int SetSProfile(int axisId, int reserved, double sPara) => 0;
        public override int SetDecStopTime(int axisId, double decStopTime) => 0;

        // 轴参数读取（虚拟卡返回默认值）
        public override int GetPulseEquivalent(int axisId, ref double pulsePerUnit) { pulsePerUnit = 1.0; return 0; }
        public override int GetEmergencyStopMode(int axisId, ref bool enabled, ref int logicLevel) { enabled = true; logicLevel = 0; return 0; }
        public override int GetAxisIOMap(int axisId, int ioType, ref int mapIoType, ref int mapIoIndex, ref double filterTime) { mapIoType = 6; mapIoIndex = 0; filterTime = 0; return 0; }
        public override int GetHomeProfile(int axisId, ref int mode, ref double lowSpeed, ref double highSpeed, ref double accTime, ref double decTime, ref double offset) { mode = 1; lowSpeed = 0.5; highSpeed = 5.0; accTime = 0.1; decTime = 0.1; offset = 0; return 0; }
        public override int GetProfileUnit(int axisId, ref double startVel, ref double maxVel, ref double accTime, ref double decTime, ref double stopVel) { startVel = 0; maxVel = 10.0; accTime = 0.1; decTime = 0.1; stopVel = 0.1; return 0; }
        public override int GetSProfile(int axisId, int reserved, ref double sPara) { sPara = 0.1; return 0; }
        public override int GetDecStopTime(int axisId, ref double decStopTime) { decStopTime = 0.1; return 0; }
        public override int GetVectorProfileUnit(int coordId, ref double startVel, ref double maxVel, ref double accTime, ref double decTime, ref double endVel) { startVel = 5.0; maxVel = 50.0; accTime = 0.1; decTime = 0.1; endVel = 5.0; return 0; }
        public override int GetVectorSProfile(int coordId, int reserved, ref double sPara) { sPara = 0.1; return 0; }

    }
}
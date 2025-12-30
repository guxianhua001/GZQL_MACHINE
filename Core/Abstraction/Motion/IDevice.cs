
namespace Core.Abstraction
{
    // 基础设备接口
    public interface IDevice
    {
        int Id { get; }
        string Name { get; }
        int TaskId { get; set; }
    }

    // 数字输入接口
    public interface IDigitalInput : IDevice
    {
        bool Status { get; }
        bool RisingEdge { get; }
        bool FallingEdge { get; }
        int Update();
    }

    // 数字输出接口
    public interface IDigitalOutput : IDevice
    {
        bool Status { get; }
        int SetState(bool state);
        int Update();
    }

    // 工站接口
    public interface IStation : IDevice
    {
        int StationId { get; }
        StationState State { get; }
        void Start(object runMode);
        void Stop();
        void Reset();
        void Pause();
        void Continue();
    }

    // 事件接口
    public interface IEvent
    {
        EventType EventType { get; }
        IDevice Sender { get; }
        int CurrentTaskId { get; }
        object EventArgs { get; }
    }

    // 枚举定义
    public enum StationState
    {
        None, Estop, Alarm, Stop, WaitReset, Resetting,
        WaitRun, Running, Pause, Clear, Tip
    }

    public enum EventType
    {
        Signal, SetServo, Move, MoveStop, SetDo, WaitDi, Timeout,
        Reset, Estop, Alarm, StopMustReset, WaitReset, Start,
        Pause, Continue, DiContinue, Log, ResetBuzz, Buzz
    }
    public enum XAxisDirection
    {
        Left_Right,
        Front_Back,
        Up_Down,
        Rotate,
        Rotate_antiClock,
        Right_Left,
        Down_Up,
        Back_Front,
    }
    public enum AxisStyle
    {
        ADLINK_Axis,
        ACS_Axis,
        Leisai_Axis,
    }
    public enum XStationState
    {
        NONE,
        ESTOP,
        ALARM,
        STOP,
        WAITRESET,
        RESETING,
        WAITRUN,
        RUNNING,
        PAUSE,
        CLEAR,
        TIP
    }
    public enum XSysAlarmId
    {
        ESTOP = 1,
        DOOR_OPEN = 2,
        CURTAIN_ACT = 3,
        AXIS_SERVON_FAIL = 4,
        AXIS_ASTP = 5,
        AXIS_ALM = 6,
        AXIS_PEL = 7,
        AXIS_MEL = 8,
        AXIS_POSERROR = 9,
        WAITDI_TIMEOUT = 10,
        CARD_INIT_FAIL = 11,
        CARD_LOAD_PARAM_FAIL = 12,
        CARD_REST_FAIL = 13,
        CARD_ERROR_FUNGETBACK = 14,
        CAMERA_OPEN_FAIL = 15,
        CAMERA_PROGRAM_ERROR = 16,
        MACHINE = 17,
        SCAN_CAMERA_OPEN_FAIL = 18,
        CONFIG_ERROR = 19,
        SENSOR_ALM = 20
    }

    public enum XAlarmLevel
    {
        ONLYLOG,//0
        TIP,    //1
        PAUSE,  //2
        STOP    //3
    }
    public enum XAlarmColor
    {
        Red,
        Yellow,
        Green
    }
    public enum AlarmCategory
    {
        VISION,
        LASER,
        TRAY,
        PLC,
        SYSTEM,
        DI,
        DO,
        Robort,
        Machine,
        CONFIG
    }
    public enum XEventID
    {
        SIGNAL,

        SETSERVO,
        MOVE,
        MOVESTOP,
        SETDO,
        WAITDI,
        TIMEOUT,
        RST,
        ESTOP,
        ALARM,
        STOPMUSTRESET,
        RESET,
        WAITRESET,
        START,
        PAUSE,
        CONTINUE,
        DICONTINUE,
        LOG,
        RESETBUZZ,
        BUZZ

    }
}
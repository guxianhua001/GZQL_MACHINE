

using Core.Abstraction;
using Interfaces;

namespace SmarterMotion
{
    public class XEvent : XObject, IEvent
    {
        private XObject _sender;
        private XEventHandler _target;
        private XEventArgs _eventArgs;
        private XEventID _eventID;
        private int _currenttaskID;

        public XEvent()
            : this(null, 0, null, null, -2)
        {

        }

        public XEvent(XEventHandler target, XEventID eventID, XEventArgs eventArgs = null, XObject sender = null, int currenttaskid = -2)
        {
            this._target = target;
            this._eventID = eventID;
            this._eventArgs = eventArgs;
            this._sender = sender;
            this._currenttaskID = currenttaskid;
        }

        public XObject Sender
        {
            get { return _sender; }
            set { _sender = value; }
        }

        public XEventHandler EventHandler
        {
            get { return _target; }
            set { _target = value; }
        }

        public XEventID EventID
        {
            get { return _eventID; }
            set { _eventID = value; }
        }

        public XEventArgs EventArgs
        {
            get { return _eventArgs; }
            set { _eventArgs = value; }
        }
        public int CurrentTaskID
        {
            get { return _currenttaskID; }
            set { _currenttaskID = value; }
        }

        public int CurrentTaskId => _currenttaskID;

        public EventType EventType => MapToEventType(_eventID);

        IDevice IEvent.Sender => _sender as IDevice;

        object IEvent.EventArgs => _eventArgs;

        public int Execute()
        {
            if (_target == null)
            {
                return -1;
            }
            _target.HandleEvent(this);
            return 0;
        }
        // 映射 XEventID 到 EventType
        private EventType MapToEventType(XEventID xEventId)
        {
            return xEventId switch
            {
                XEventID.SIGNAL => EventType.Signal,
                XEventID.SETSERVO => EventType.SetServo,
                XEventID.MOVE => EventType.Move,
                XEventID.MOVESTOP => EventType.MoveStop,
                XEventID.SETDO => EventType.SetDo,
                XEventID.WAITDI => EventType.WaitDi,
                XEventID.TIMEOUT => EventType.Timeout,
                XEventID.RST => EventType.Reset,
                XEventID.ESTOP => EventType.Estop,
                XEventID.ALARM => EventType.Alarm,
                XEventID.STOPMUSTRESET => EventType.StopMustReset,
                XEventID.WAITRESET => EventType.WaitReset,
                XEventID.START => EventType.Start,
                XEventID.PAUSE => EventType.Pause,
                XEventID.CONTINUE => EventType.Continue,
                XEventID.DICONTINUE => EventType.DiContinue,
                XEventID.LOG => EventType.Log,
                XEventID.RESETBUZZ => EventType.ResetBuzz,
                XEventID.BUZZ => EventType.Buzz,
                _ => EventType.Signal
            };
        }
    }

   
}
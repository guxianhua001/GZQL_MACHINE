using Core.Abstraction;
using Interfaces;

namespace SmarterMotion
{
    public abstract class XEventHandler : XObject
    {
        //使用post将任务提交给一个事件队列 事件队列中的任务将被一一执行
        public void PostEvent(XEventHandler observer, XEventID xEventID, bool highPriorityFlag = false, int currenttaskid = -2) //-2代表各个station中所有任务
        {
            XController.Instance.EventServer.PostEvent(observer, xEventID, null, null, highPriorityFlag, currenttaskid);
        }

        public void PostEventEStop(XStation xStation)
        {
            XEventArgs e = new XEventArgs();
            e.StationId = xStation.StationId;
            e.AlarmLevel = (int)XAlarmLevel.STOP;
            XController.Instance.EventServer.PostEvent(xStation, XEventID.ESTOP, e, null, true);
            //XController.Instance.AlarmEventServer.PostEvent(XAlarmReporter.Instance, XEventID.ESTOP, e, null, true);
        }

        public void PostEvent(XStation xStation, XAlarmLevel alarmLevel, XSysAlarmId sysAlarmId, int intValue = 0, bool flag = true, int currenttaskid = -2, string append = "")
        {
            XEventArgs e = new XEventArgs();
            e.AlarmLevel = (int)alarmLevel;
            e.AlarmId = (int)sysAlarmId;
            e.IntValue = intValue;
            e.StringValue = append;
            if (xStation == null) return;
            e.StationId = xStation.StationId;
            if (xStation.StationId == -1)
                return;
            e.BoolValue = flag;
            if (xStation != null)
                XController.Instance.EventServer.PostEvent(xStation, XEventID.ALARM, e, null, true, currenttaskid);
            XController.Instance.AlarmEventServer.PostEvent(XAlarmReporter.Instance, XEventID.ALARM, e, null, true, currenttaskid);
        }

        public void PostEvent(XStation xStation, XAlarmLevel alarmLevel, XAlarmEventArgs args, string append = "", int currenttaskid = -2)
        {
            if (!string.IsNullOrEmpty(append))
                args.Description += ":" + append;
            if (xStation == null)
                return;
            XEventArgs e = new XEventArgs();
            e.IntValue = args.IntValue;
            e.AlarmLevel = (int)alarmLevel;
            e.AlarmEventArgs = args;
            e.StationId = xStation.StationId;
            e.AlarmId = 0;
            XController.Instance.EventServer.PostEvent(xStation, XEventID.ALARM, e, null, true, currenttaskid);
            XController.Instance.AlarmEventServer.PostEvent(XAlarmReporter.Instance, XEventID.ALARM, e, null, true, currenttaskid);
        }

        public void NotifyStations(XAlarmLevel alarmLevel, int alarmCode, string append = "", int intValue = 0)
        {
            XEventArgs e = new XEventArgs();
            e.AlarmLevel = (int)alarmLevel;
            e.AlarmId = alarmCode;
            e.StringValue = append;
            e.IntValue = intValue;
            foreach (XStation station in XStationManager.Instance.Stations.Values)
            {
                XController.Instance.EventServer.PostEvent(station, XEventID.ALARM, e, null, true);
            }
            XController.Instance.AlarmEventServer.PostEvent(XAlarmReporter.Instance, XEventID.ALARM, e, null, true);
        }

        public virtual int HandleEvent(XEvent xEvent)
        {
            return -1;
        }
    }


}

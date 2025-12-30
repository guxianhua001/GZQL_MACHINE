using Core.Abstraction;
using Interfaces;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace SmarterMotion
{
    public class XEventServer : XEventServerBase
    {

        private ConcurrentQueue<XEvent> queue_XEvent_Alarm;
        private ConcurrentQueue<XEvent> queue_XEvent_Signal;
        private ConcurrentQueue<XEvent> queue_XEvent_Priority;
        private ConcurrentQueue<XEvent> queue_XEvent;
        public XEventServer()
        {
            InitEventPool(300);
            queue_XEvent_Alarm = new ConcurrentQueue<XEvent>();
            queue_XEvent_Signal = new ConcurrentQueue<XEvent>();
            queue_XEvent_Priority = new ConcurrentQueue<XEvent>();
            queue_XEvent = new ConcurrentQueue<XEvent>();
        }
        //Post各种事件
        public override void PostEvent(XEventHandler target, XEventID eventID, XEventArgs eventArgs = null, XObject sender = null, bool isPriority = false, int currenttaskid = -2)
        {
            XEvent xEvent = CreateEvent(target, eventID, eventArgs, sender, currenttaskid);
            switch (xEvent.EventID)
            {
                case XEventID.SIGNAL:
                    queue_XEvent_Signal.Enqueue(xEvent);
                    break;
                case XEventID.ESTOP:
                    queue_XEvent_Alarm.Enqueue(xEvent);
                    break;
                case XEventID.ALARM:
                    queue_XEvent_Alarm.Enqueue(xEvent);
                    break;
                default:

                    if (isPriority)
                    {
                        queue_XEvent_Priority.Enqueue(xEvent);
                    }
                    else
                    {
                        queue_XEvent.Enqueue(xEvent);
                    }
                    break;
            }
        }
        //任务分发事件 执行HandleEvent()
        protected override void DispatchEvent()
        {
            XEvent xEvent = null;
            while (queue_XEvent_Signal.Count > 0)
            {
                if (queue_XEvent_Signal.TryDequeue(out xEvent))
                {
                    xEvent.Execute();
                }
            }

            while (queue_XEvent_Alarm.Count > 0)
            {
                if (queue_XEvent_Alarm.TryDequeue(out xEvent))
                {
                    xEvent.Execute();
                }
            }

            while (queue_XEvent_Priority.Count > 0)
            {
                if (queue_XEvent_Priority.TryDequeue(out xEvent))
                {
                    xEvent.Execute();
                }
            }

            if (queue_XEvent.Count > 0)
            {
                if (queue_XEvent.TryDequeue(out xEvent))
                {
                    xEvent.Execute();
                }
            }

            if (xEvent != null)
            {
                Store(xEvent);
            }
        }

        //循环读取轴和io状态和调度事件
        protected override void ProcessEventQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                //Update();
                NotifyObservers(XEventID.SIGNAL);
                DispatchEvent();
                Thread.Sleep(3);
            }
        }
        private void Update()
        {
            foreach (KeyValuePair<int, IAxis> kvp in XDevice.Instance.AxisMap)
            {
                kvp.Value.Update();   //XleisaiAxis.Update();
            }
            foreach (KeyValuePair<int, XCard> kvp in XDevice.Instance.CardMap)
            {
                kvp.Value.Update();   //XCard.Update() => XCommandCardLeisai.Update();
            }
            foreach (KeyValuePair<int, XDi> kvp in XDevice.Instance.DiMap)
            {
                kvp.Value.Update();   // XDi.Update() => XCommandCardLeisai.GetDi();
            }
            foreach (KeyValuePair<int, XDo> kvp in XDevice.Instance.DoMap)
            {
                kvp.Value.Update();   // XDo.Update() => XCommandCardLeisai.GetDo();
            }
        }
    }
}

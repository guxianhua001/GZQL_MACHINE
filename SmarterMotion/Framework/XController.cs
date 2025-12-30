
using Core.Abstraction;

namespace SmarterMotion
{
    public class EmptyTaskParameters : TaskParametersBase
    {
        // 空参数类，无需任何实现
        public override string Identifier => "";
    }
    public sealed class XController : XTaskBase<EmptyTaskParameters>
    {
        private XEventServer eventServer = new XEventServer();
        private XAlarmEventServer alarmEventServer = new XAlarmEventServer();
        private readonly static XController instance = new XController();

        // 调用基类构造函数（默认ID=0，名称="Controller"）
        private XController() : base(-2, "Controller") { }
        public static XController Instance => instance;
        // 抽象方法实现（控制器不需要实际功能）
        protected override void ExecuteHoming() { }
        protected override void InitProcessVar() { }
        protected override void OnErrorOccurred() { }
        public XEventServer EventServer => eventServer;
        public XAlarmEventServer AlarmEventServer => alarmEventServer;
        public void Start()
        {
            eventServer.Start();
            alarmEventServer.Start();
        }
        public void Stop()
        {
            eventServer.Stop();
            alarmEventServer.Stop();
            foreach (var task in XTaskManager.Instance.Tasks.Values)
            {
                task.Cancel();
            }
        }

        public int _MoveHome(int axidId)
        {
            return MoveHome(axidId);
        }

        public int _MoveAbs(int axisId, double pos, double vel, bool checkLmt = true)
        {
            return MoveAbs(axisId, pos, vel, checkLmt);
        }

        public int _MoveAbs(int[] axisId, double[] pos, double vel, bool checkLmt = true)
        {
            return MoveAbs(axisId, pos, vel, checkLmt);
        }

        public int _MovePosition(XPosition position, double vel)
        {
            return MovePosition(position, vel);
        }

        public int _MoveRel(int axisId, double distance, double vel)
        {
            return MoveRel(axisId, distance, vel);
        }
        public int _MoveJog(int axisId, int isStart)
        {
            return MoveJog(axisId, isStart);
        }

        //public int _setPosition(ushort axisId, int position)
        //{
        //    return SetPosition(axisId, position);
        //}

        public int _CleanALM(ushort axisId)
        {
            return CleanALM((int)axisId);
        }

        public int _MoveStop()
        {
            return MoveStop();
        }

        public bool _WaitMoveDone()
        {
            return WaitMoveDone();
        }

    }


}

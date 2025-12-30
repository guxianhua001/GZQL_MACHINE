using Core.Abstraction;

namespace SmarterMotion
{
    public abstract class XCtrlEventHandler : XEventHandler
    {
        public XCtrlEventHandler()
        {
            XController.Instance.EventServer.RegisterForNotification(this, XEventID.SIGNAL);
        }
    }
}

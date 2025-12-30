using Core.Abstraction;

namespace SmarterMotion
{
    public abstract class XMachineEventHandler : XEventHandler
    {
        public XMachineEventHandler()
        {
            XController.Instance.EventServer.RegisterForNotification(this, XEventID.SIGNAL);
        }
    }
}



namespace HSMS
{
    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string StatusText { get; set; }
    }
}

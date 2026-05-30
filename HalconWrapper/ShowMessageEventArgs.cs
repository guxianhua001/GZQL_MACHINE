

namespace HalconWrapper
{
    public enum MessageType
    {
        MouseMessage,
        ShowOk,
        ShowNg
    }
    public class ShowMessageEventArgs
    {
        public readonly MessageType MessageType;
        public readonly string message;
        public ShowMessageEventArgs(MessageType messageType)
        {
            this.MessageType = messageType;
        }
        public ShowMessageEventArgs(string message)
        {
            this.MessageType = MessageType.MouseMessage;
            this.message = message;
        }
    }
}

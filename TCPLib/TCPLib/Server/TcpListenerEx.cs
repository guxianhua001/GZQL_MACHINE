
/*----------------------------------------------------------------
* 命名空间: TCPLib.TCPLib.Server
*
* 类 名： TcpListenerEx
* 功 能： N/A
* 唯一标识：ae9146ad-0e32-4fea-ab4e-5ae6617a39ce
* 
* 变更日期：2023/8/22 23:27:49
* 作者：szb
* 公司：CYG
*----------------------------------------------------------------*/

using System.Net;
using System.Net.Sockets;

namespace TCPLib.Server
{
    public class TcpListenerEx : TcpListener
    {
        public TcpListenerEx(IPEndPoint localEP) : base(localEP)
        {
        }

        public TcpListenerEx(IPAddress localaddr, int port) : base(localaddr, port)
        {
        }

        public bool Active =>
            base.Active;
    }
}

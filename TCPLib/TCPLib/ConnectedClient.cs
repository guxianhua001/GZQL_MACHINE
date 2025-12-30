
/*----------------------------------------------------------------
* 命名空间: TCPLib.TCPLib
*
* 类 名： ConnectedClient
* 功 能： N/A
* 唯一标识：392d78f4-ec0a-4ebc-8cd9-99f065af9d76
* 
* 变更日期：2023/8/22 23:25:54
* 作者：szb
* 公司：CYG
*----------------------------------------------------------------*/

using System.Net;
using System.Net.Sockets;

namespace TCPLib
{
    public class ConnectedClient
    {
        public IPAddress ServerIP { get; internal set; }

        public TcpClient Client { get; internal set; }
    }
}

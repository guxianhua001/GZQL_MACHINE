#region << 版 本 注 释 >>
/*----------------------------------------------------------------
 * 版权所有 (c) 2024 China  保留所有权利。
 * CLR版本：4.0.30319.42000
 * 机器名称：USER-20240611LB
 * 命名空间：TCPLib.TCPHelper
 * 唯一标识：c08d90b2-4d7f-4583-93f4-239901a436f4
 * 文件名：NetEventArgs
 * 
 * 创建者：szb
 * 创建时间：2024/8/12 15:28:41
 * 版本：V1.0.0
 * 描述：
 *
 * ----------------------------------------------------------------
 * 修改人：
 * 时间：
 * 修改说明：
 *
 * 版本：V1.0.1
 *----------------------------------------------------------------*/
#endregion << 版 本 注 释 >>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TCPLib.TCPHelper
{
    /// <summary> 
    /// 网络通讯事件模型委托 
    /// </summary> 
    public delegate void NetEvent(object sender, NetEventArgs e);

    /// <summary> 
    /// 服务器程序的事件参数,包含了激发该事件的会话对象 
    /// </summary> 
    public class NetEventArgs : EventArgs
    {

        #region 字段

        /// <summary> 
        /// 客户端与服务器之间的会话 
        /// </summary> 
        private Session _client;
        public SocketError ErrorCode { get; set; }


        #endregion

        #region 构造函数
        /// <summary> 
        /// 构造函数 
        /// </summary>
        /// <param name="client">客户端会话</param> 
        public NetEventArgs(Session client)
        {
            if (null == client)
            {
                //throw (new ArgumentNullException());
            }
            _client = client;
            // 若Session有效，初始化RemoteIP/Port
            if (_client != null)
            {
                RemoteIP = _client.RemoteIP;
                RemotePort = _client.RemotePort;
            }

        }

        #endregion

        #region 属性

        /// <summary> 
        /// 获得激发该事件的会话对象 
        /// </summary> 
        public Session Client
        {
            get
            {
                return _client;
            }
        }
        public string FailedIP { get; set; } = "0.0.0.0";
        public int FailedPort { get; set; }
        public string RemoteIP { get; private set; }
        public int RemotePort { get; private set; }

        #endregion

    }
}

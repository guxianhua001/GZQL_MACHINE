
/*----------------------------------------------------------------
* 命名空间: TCPLib.TCPLib
*
* 类 名： TCPManager
* 功 能： N/A
* 唯一标识：28c803a2-c26a-4ec2-9755-eb4c66bb7dfe
* 
* 变更日期：2023/8/22 23:27:32
* 作者：szb
* 公司：CYG
*----------------------------------------------------------------*/

using System;
using System.Collections.Generic;

namespace TCPLib
{

    public class TCPManager
    {
        public static readonly TCPManager Instance = new TCPManager();
        private object objServer = new object();
        private Dictionary<int, Client> m_ServerId = new Dictionary<int, Client>();
        private Dictionary<int, int> m_TcpIdDic = new Dictionary<int, int>();

        public void BindTcp(int cameraId, int serverId, string IpAdress = null, int Port = -1)
        {
            if (!this.m_ServerId.ContainsKey(serverId))
            {
                this.m_ServerId.Add(serverId, new Client(IpAdress, Port));
            }
            this.m_TcpIdDic.Add(cameraId, serverId);
        }

        public Client GetClient(int serverId) =>
            this.m_ServerId[serverId];

        public Tuple<bool, string[]> GetData(int cameraId, int timeout)
        {
            Tuple<bool, string[]> tuple;
            if (!this.m_ServerId[this.m_TcpIdDic[cameraId]].ReceiveDoneEvent.WaitOne(timeout))
            {
                tuple = new Tuple<bool, string[]>(false, null);
            }
            else
            {
                char[] separator = new char[] { ',' };
                tuple = new Tuple<bool, string[]>(true, this.m_ServerId[this.m_TcpIdDic[cameraId]].RetStr.Replace("\r\n", "").Replace("\0", "").Trim().Split(separator));
            }
            return tuple;
        }

        public Tuple<bool, string[]> TriggerAndGetData(int cameraId, string cmd, int timeout = -1)
        {
            object objServer = this.objServer;
            lock (objServer)
            {
                if (!this.m_ServerId[this.m_TcpIdDic[cameraId]].ConnectState)
                {
                    this.m_ServerId[this.m_TcpIdDic[cameraId]].Connect();
                }
                this.m_ServerId[this.m_TcpIdDic[cameraId]].Write(cmd);
                return this.GetData(cameraId, timeout);
            }
        }
    }
}

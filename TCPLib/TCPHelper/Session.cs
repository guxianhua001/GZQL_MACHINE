#region << 版 本 注 释 >>
/*----------------------------------------------------------------
 * 版权所有 (c) 2024 China  保留所有权利。
 * CLR版本：4.0.30319.42000
 * 机器名称：USER-20240611LB
 * 命名空间：TCPLib.TCPHelper
 * 唯一标识：308bbe53-321b-4b25-99ad-23cacec38e34
 * 文件名：Session
 * 
 * 创建者：szb
 * 创建时间：2024/8/12 15:28:56
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
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TCPLib.TCPHelper
{
    /// <summary> 
    /// 客户端与服务器之间的会话类 
    /// 
    /// 说明: 
    /// 会话类包含远程通讯端的状态,这些状态包括Socket,报文内容, 
    /// 客户端退出的类型(正常关闭,强制退出两种类型) 
    /// </summary> 
    public class Session : ICloneable
    {
        #region 字段

        /// <summary>
        /// 会话ID 
        /// </summary>
        private SessionId _id;

        /// <summary>
        /// 客户端发送到服务器的报文 
        /// 注意:在有些情况下报文可能只是报文的片断而不完整 
        /// </summary> 
        private byte[] _datagram;

        /// <summary> 
        /// 客户端的Socket 
        /// </summary> 
        private Socket _cliSock;

        /// <summary> 
        /// 客户端的退出类型 
        /// </summary> 
        private ExitType _exitType;

        /// <summary> 
        /// 退出类型枚举 
        /// </summary> 
        public enum ExitType
        {
            NormalExit,
            ExceptionExit
        };

        #endregion

        #region 属性

        /// <summary> 
        /// 返回会话的ID 
        /// </summary> 
        public SessionId ID
        {
            get
            {
                return _id;
            }
        }

        /// <summary> 
        /// 存取会话的报文 
        /// </summary> 
        public byte[] Datagram
        {
            get
            {
                return _datagram;
            }
            set
            {
                _datagram = value;
            }
        }

        /// <summary> 
        /// 获得与客户端会话关联的Socket对象 
        /// </summary> 
        public Socket ClientSocket
        {
            get
            {
                return _cliSock;
            }
        }

        /// <summary> 
        /// 存取客户端的退出方式 
        /// </summary> 
        public ExitType TypeOfExit
        {
            get
            {
                return _exitType;
            }

            set
            {
                _exitType = value;
            }
        }

        public string RemoteIP { get; internal set; }
        public int RemotePort { get; internal set; }

        #endregion

        /// <summary> 
        /// 构造函数 
        /// </summary> 
        /// <param name="cliSock">会话使用的Socket连接</param> 
        public Session(Socket cliSock)
        {
            Debug.Assert(cliSock != null);
            _cliSock = cliSock;
            _id = new SessionId((int)cliSock.Handle);
        }

        #region 方法

        /// <summary> 
        /// 使用Socket对象的Handle值作为HashCode,它具有良好的线性特征. 
        /// </summary> 
        /// <returns></returns> 
        public override int GetHashCode()
        {
            return (int)_cliSock.Handle;
        }

        /// <summary> 
        /// 返回两个Session是否代表同一个客户端 
        /// </summary> 
        /// <param name="obj"></param> 
        /// <returns></returns> 
        public override bool Equals(object obj)
        {
            Session rightObj = (Session)obj;
            return (int)_cliSock.Handle == (int)rightObj.ClientSocket.Handle;
        }

        /// <summary> 
        /// 重载ToString()方法,返回Session对象的特征 
        /// </summary> 
        /// <returns></returns> 
        public override string ToString()
        {
            string result = string.Format("Session:{0},IP:{1}", _id, _cliSock.RemoteEndPoint.ToString());

            return result;
        }

        /// <summary> 
        /// 关闭会话 
        /// </summary> 
        public void Close()
        {
            Debug.Assert(_cliSock != null);
            //关闭数据的接受和发送 
            _cliSock.Shutdown(SocketShutdown.Both);
            //清理资源 
            _cliSock.Close();
        }

        #endregion

        #region ICloneable 成员

        object ICloneable.Clone()  //这个的用法很有特点：定义是ICloneable.Clone()，实际使用是由Session的对象按public类型函数直接调用
        {
            Session newSession = new Session(_cliSock);
            newSession.Datagram = _datagram;
            newSession.TypeOfExit = _exitType;
            return newSession;
        }

        #endregion
    }
}

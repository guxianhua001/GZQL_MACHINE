
/*----------------------------------------------------------------
* 命名空间: TCPLib.TCPLib
*
* 类 名： Client
* 功 能： N/A
* 唯一标识：94a2ed93-4a3b-4079-b605-ee578455c6c9
* 
* 变更日期：2023/8/22 23:25:31
* 作者：szb
* 公司：CYG
*----------------------------------------------------------------*/

using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TCPLib
{
    public class Client : IDisposable
    {
        private Socket m_socket;
        private byte[] buffer = new byte[0x32000];
        private string m_IpAdress;
        private int m_Port;
        private AutoResetEvent _timeoutObject = new AutoResetEvent(false);
        public AutoResetEvent ReceiveDoneEvent = new AutoResetEvent(false);
        private bool disposedValue = false;

        public Client(string IpAdress, int Port)
        {
            m_IpAdress = IpAdress;
            m_Port = Port;
            RetStr = null;
            ConnectState = false;
        }

        public void AsyncCallback(IAsyncResult ar)
        {
            try
            {
                if (m_socket.EndReceive(ar) > 0)
                {
                    string cmd = Encoding.ASCII.GetString(buffer).Replace("\0", "").Trim();
                    RetStr = cmd;
                    ReceiveDoneEvent.Set();
                    TcpClientsend(cmd, "Receive");
                    buffer = new byte[0x32000];
                    m_socket.BeginReceive(buffer, 0, 0x32000, SocketFlags.None, new AsyncCallback(AsyncCallback), this);
                }
            }
            catch (Exception exception)
            {
                m_socket.Disconnect(true);
                m_socket = null;
                ConnectState = false;
                RetStr = null;
                ReceiveDoneEvent.Set();
                TcpClientsend(exception.ToString(), "ReceiveError");
                System.Windows.MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show(string.Format("接收=>{0} 通讯命令失败:\r\n" + exception.ToString(), m_IpAdress), "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void CallBackConnect(IAsyncResult asyncresult)
        {
            _timeoutObject.Set();
        }

        public Client Connect()
        {
            try
            {
                m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _timeoutObject = new AutoResetEvent(false);
                m_socket.BeginConnect(m_IpAdress, m_Port, new AsyncCallback(CallBackConnect), m_socket);
                if (!_timeoutObject.WaitOne(0x7d0))
                {
                    throw new Exception();
                }
                m_socket.BeginReceive(buffer, 0, 0x32000, SocketFlags.None, new AsyncCallback(AsyncCallback), this);
                ConnectState = true;
            }
            catch (Exception exception)
            {
                ConnectState = false;
                m_socket.Dispose();
                m_socket = null;
                TcpClientsend(exception.ToString(), "ConnectError");
                return null;
            }
            return this;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                }
                if (m_socket != null)
                {
                    try
                    {
                        m_socket.Close();
                    }
                    catch
                    {
                    }
                    m_socket = null;
                }
                disposedValue = true;
            }
        }

        private void TcpClientsend(string cmd, string strType)
        {
            try
            {
                string path = @"D:\Record\TcpClientReceive\" + DateTime.Today.ToString("yyyyMMdd") + "_TcpClient.csv";
                if (!File.Exists(path))
                {
                    CsvServer.Instance.WriteLine(path, "DataTime,Property,TcpClientReceive");
                }
                cmd = cmd.Replace("\r\n", "");
                cmd = cmd.Replace("\r", "").Trim();
                cmd = cmd.Replace("\n", "").Trim();
                cmd = cmd.Replace("\"", "").Trim();
                string[] textArray1 = new string[] { DateTime.Now.ToString("HH:mm:ss:fff"), ",", strType, ",", cmd };
                CsvServer.Instance.WriteLine(path, string.Concat(textArray1));
            }
            catch (Exception)
            {
            }
        }

        public void Write(string data)
        {
            if (!ReferenceEquals(data, null))
            {
                ReceiveDoneEvent.Reset();
                Write(Encoding.UTF8.GetBytes(data));
                TcpClientsend(data, "Send");
            }
        }

        private void Write(byte[] data)
        {
            try
            {
                if (ReferenceEquals(m_socket, null))
                {
                    throw new Exception("Cannot send data to a null TcpClient (check to see if Connect was called)");
                }
                m_socket.Send(data, data.Length, SocketFlags.None);
            }
            catch (Exception exception)
            {
                TcpClientsend(exception.ToString(), "SendError");
                System.Windows.MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show(string.Format("发送=>{0} 通讯命令失败:\r\n" + exception.ToString(), m_IpAdress), "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public string RetStr { get; set; }

        public bool ConnectState { get; set; }
    }
}

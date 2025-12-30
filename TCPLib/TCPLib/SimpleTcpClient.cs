
/*----------------------------------------------------------------
* 命名空间: TCPLib.TCPLib
*
* 类 名： SimpleTcpClient
* 功 能： N/A
* 唯一标识：7e03a5bd-b8e8-46b0-a4d8-8bc8b2f849dc
* 
* 变更日期：2023/8/22 23:27:18
* 作者：szb
* 公司：CYG
*----------------------------------------------------------------*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TCPLib
{
    public class SimpleTcpClient : IDisposable
    {
        private Socket socket;
        private byte[] buffer;
        private string IPADDRESS;
        private int IPPORT;
        private Thread _rxThread;
        private List<byte> _queuedMsg;
        public string IpAddress;
        public int Iport;
        private Ping pingSender;
        private PingOptions options;
        private AutoResetEvent _timeoutObject;
        private TCPLib.Message mReply;
        private bool disposedValue;

        public event EventHandler<TCPLib.Message> DataReceived;

        public event EventHandler<TCPLib.Message> DelimiterDataReceived;

        public SimpleTcpClient()
        {
            this.buffer = new byte[0x32000];
            this._rxThread = null;
            this._queuedMsg = new List<byte>();
            this.pingSender = new Ping();
            this.options = new PingOptions();
            this.mReply = null;
            this.disposedValue = false;
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.StringEncoder = Encoding.UTF8;
            this.ReadLoopIntervalMs = 5;
            this.Delimiter = 0x13;
            this.DataReceived += new EventHandler<TCPLib.Message>(this.SimpleTcpClient_DataReceived);
        }

        public SimpleTcpClient(string ipaddress, int ipport) : this()
        {
            this.IPADDRESS = ipaddress;
            this.IPPORT = ipport;
        }

        public void AsyncCallback(IAsyncResult ar)
        {
            try
            {
                if (this.socket.EndReceive(ar) > 0)
                {
                    string s = Encoding.ASCII.GetString(this.buffer);
                    if (this.DataReceived != null)
                    {
                        int index = s.IndexOf("\0");
                        if (index > 0)
                        {
                            s = s.Substring(0, index);
                        }
                        TCPLib.Message e = new TCPLib.Message(Encoding.ASCII.GetBytes(s), this.socket, this.StringEncoder, this.Delimiter, this.AutoTrimStrings);
                        this.mReply = e;
                        this.DataReceived(this, e);
                        this.buffer = new byte[0x32000];
                        this.socket.BeginReceive(this.buffer, 0, 0x32000, SocketFlags.None, new System.AsyncCallback(this.AsyncCallback), this);
                    }
                }
            }
            catch
            {
                if (this.IpAddress != "169.254.0.10")
                {
                    this.Disconnect();
                    this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    this.StartRxThread();
                }
            }
        }

        private void CallBackConnect(IAsyncResult asyncresult)
        {
            this._timeoutObject.Set();
        }

        public SimpleTcpClient Connect()
        {
            try
            {
                if (!this.Connected)
                {
                    if (string.IsNullOrEmpty(this.IPADDRESS))
                    {
                        throw new ArgumentNullException("hostNameOrIpAddress");
                    }
                    this.Iport = this.IPPORT;
                    this.IpAddress = this.IPADDRESS;
                    this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    this._timeoutObject.Reset();
                    this.socket.BeginConnect(this.IPADDRESS, this.IPPORT, new System.AsyncCallback(this.CallBackConnect), this.socket);
                    if (!this._timeoutObject.WaitOne(0x7d0, false))
                    {
                        throw new Exception();
                    }
                    this.socket.BeginReceive(this.buffer, 0, 0x32000, SocketFlags.None, new System.AsyncCallback(this.AsyncCallback), this);
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                this._timeoutObject = new AutoResetEvent(false);
                return null;
            }
            return this;
        }

        public SimpleTcpClient Connect(string hostNameOrIpAddress, int port)
        {
            try
            {
                if (string.IsNullOrEmpty(hostNameOrIpAddress))
                {
                    throw new ArgumentNullException("hostNameOrIpAddress");
                }
                this.Iport = port;
                this.IpAddress = hostNameOrIpAddress;
                this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                this._timeoutObject = new AutoResetEvent(false);
                this.socket.BeginConnect(hostNameOrIpAddress, port, new System.AsyncCallback(this.CallBackConnect), this.socket);
                if (!this._timeoutObject.WaitOne(0x1388))
                {
                    throw new Exception();
                }
                this.socket.BeginReceive(this.buffer, 0, 0x32000, SocketFlags.None, new System.AsyncCallback(this.AsyncCallback), this);
            }
            catch
            {
                this.StartRxThread();
                return null;
            }
            return this;
        }

        public SimpleTcpClient Disconnect()
        {
            try
            {
                if (this._rxThread != null)
                {
                    this._rxThread.Abort();
                    this._rxThread = null;
                }
                if (!ReferenceEquals(this.socket, null))
                {
                    this.socket.Disconnect(true);
                }
                else
                {
                    return this;
                }
            }
            catch (Exception)
            {
            }
            return this;
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                }
                this.QueueStop = true;
                if (this.socket != null)
                {
                    try
                    {
                        this.socket.Close();
                    }
                    catch
                    {
                    }
                    this.socket = null;
                }
                this.disposedValue = true;
            }
        }

        public bool GetReply(out string result, int timeout)
        {
            result = "";
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (true)
            {
                bool flag2;
                if (stopwatch.ElapsedMilliseconds > timeout)
                {
                    flag2 = false;
                }
                else
                {
                    if (ReferenceEquals(this.mReply, null))
                    {
                        Thread.Sleep(10);
                        continue;
                    }
                    result = this.mReply.MessageString;
                    flag2 = true;
                }
                return flag2;
            }
        }

        private void ListenerLoop(object state)
        {
            while (!this.QueueStop)
            {
                try
                {
                    this.RunLoopStep();
                }
                catch
                {
                }
                Thread.Sleep(50);
            }
        }

        private void NotifyDelimiterMessageRx(Socket client, byte[] msg)
        {
            if (this.DelimiterDataReceived != null)
            {
                TCPLib.Message e = new TCPLib.Message(msg, client, this.StringEncoder, this.Delimiter, this.AutoTrimStrings);
                this.DelimiterDataReceived(this, e);
            }
        }

        private void NotifyEndTransmissionRx(Socket client, byte[] msg)
        {
            if (this.DataReceived != null)
            {
                TCPLib.Message e = new TCPLib.Message(msg, client, this.StringEncoder, this.Delimiter, this.AutoTrimStrings);
                this.DataReceived(this, e);
            }
        }

        public void ReConnect()
        {
            try
            {
                this.socket.Connect(this.IpAddress, this.Iport);
                this.socket.BeginReceive(this.buffer, 0, 0x32000, SocketFlags.None, new System.AsyncCallback(this.AsyncCallback), this);
                if (this.socket.Connected)
                {
                    this.StopRxThread();
                }
            }
            catch (Exception)
            {
            }
        }

        private void RunLoopStep()
        {
            if (((this.socket == null) || !this.socket.Connected) && (this.IpAddress != "169.254.0.10"))
            {
                this.ReConnect();
            }
        }

        public void Send(string cmd)
        {
            this.mReply = null;
            this.Write(cmd);
        }

        private void SimpleTcpClient_DataReceived(object sender, TCPLib.Message message)
        {
            this.mReply = message;
            try
            {
                string path = @"E:\Record\TcpClientReceive\" + DateTime.Today.ToString("yyyyMMdd") + "_TcpClient.csv";
                if (!File.Exists(path))
                {
                    CsvServer.Instance.WriteLine(path, "DataTime,SendOrReceive,TcpClientReceive");
                }
                string str2 = Encoding.ASCII.GetString(message.Data).Replace("\r\n", "").Replace("\r", "");
                CsvServer.Instance.WriteLine(path, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fff") + ",Receive," + str2);
            }
            catch (Exception)
            {
            }
        }

        private void StartRxThread()
        {
            if (this._rxThread == null)
            {
                this._rxThread = new Thread(new ParameterizedThreadStart(this.ListenerLoop));
                this._rxThread.IsBackground = true;
                this._rxThread.Start();
            }
        }

        private void StopRxThread()
        {
            try
            {
                if (this._rxThread == null)
                {
                    this._rxThread.Abort();
                    this._rxThread = null;
                }
            }
            catch (Exception)
            {
            }
        }

        private void TcpClientsend(string cmd)
        {
            try
            {
                string path = @"E:\Record\TcpClientReceive\" + DateTime.Today.ToString("yyyyMMdd") + "_TcpClient.csv";
                if (!File.Exists(path))
                {
                    CsvServer.Instance.WriteLine(path, "DataTime,SendOrReceive,TcpClientReceive");
                }
                cmd = cmd.Replace("\r\n", "");
                cmd = cmd.Replace("\r", "");
                CsvServer.Instance.WriteLine(path, DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:fff") + ",Send," + cmd);
            }
            catch (Exception)
            {
            }
        }

        public void Write(byte[] data)
        {
            try
            {
                if (ReferenceEquals(this.socket, null))
                {
                    throw new Exception("Cannot send data to a null TcpClient (check to see if Connect was called)");
                }
                this.socket.Send(data, data.Length, SocketFlags.None);
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show($"发送=>{this.IpAddress} 通讯命令失败,请检查通讯!", "提示", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Asterisk);
            }
        }

        public void Write(string data)
        {
            try
            {
                if (!ReferenceEquals(data, null))
                {
                    this.Write(this.StringEncoder.GetBytes(data));
                    this.TcpClientsend(data);
                }
            }
            catch (Exception)
            {
            }
        }

        public bool WriteAndGetReply(string cmd, out string result, int timeout)
        {
            result = "";
            this.mReply = null;
            this.Write(cmd);
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (true)
            {
                bool flag2;
                if (stopwatch.ElapsedMilliseconds > timeout)
                {
                    flag2 = false;
                }
                else
                {
                    if (ReferenceEquals(this.mReply, null))
                    {
                        Thread.Sleep(10);
                        continue;
                    }
                    result = this.mReply.MessageString;
                    flag2 = true;
                }
                return flag2;
            }
        }

        public void WriteLine(string data)
        {
            try
            {
                if (!string.IsNullOrEmpty(data))
                {
                    if (data.LastOrDefault<char>() == this.Delimiter)
                    {
                        this.Write(data);
                    }
                    else
                    {
                        byte[] bytes = new byte[] { this.Delimiter };
                        this.Write(data + this.StringEncoder.GetString(bytes));
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public byte Delimiter { get; set; }

        public Encoding StringEncoder { get; set; }

        internal bool QueueStop { get; set; }

        internal int ReadLoopIntervalMs { get; set; }

        public bool AutoTrimStrings { get; set; }

        public bool Connected =>
            (this.socket != null) && this.socket.Connected;

        public Socket SocketClient =>
            this.socket;
    }
}


/*----------------------------------------------------------------
* 命名空间: TCPLib.TCPLib
*
* 类 名： Message
* 功 能： N/A
* 唯一标识：16e87cc0-2a78-4d66-b4b3-dc3741ce707e
* 
* 变更日期：2023/8/22 23:26:40
* 作者：szb
* 公司：CYG
*----------------------------------------------------------------*/

using System;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace TCPLib
{
    public class Message : EventArgs
    {
        private Socket _Scoket;
        private Encoding _encoder;
        private byte _writeLineDelimiter;
        private bool _autoTrim;

        internal Message(byte[] data, Socket tcpClient, Encoding stringEncoder, byte lineDelimiter)
        {
            this._encoder = null;
            this._autoTrim = false;
            this.Data = data;
            this._Scoket = tcpClient;
            this._encoder = stringEncoder;
            this._writeLineDelimiter = lineDelimiter;
        }

        internal Message(byte[] data, Socket tcpClient, Encoding stringEncoder, byte lineDelimiter, bool autoTrim)
        {
            this._encoder = null;
            this._autoTrim = false;
            this.Data = data;
            this._Scoket = tcpClient;
            this._encoder = stringEncoder;
            this._writeLineDelimiter = lineDelimiter;
            this._autoTrim = autoTrim;
        }

        public void Reply(byte[] data)
        {
            this._Scoket.Send(data);
        }

        public void Reply(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                this.Reply(this._encoder.GetBytes(data));
            }
        }

        public void ReplyLine(string data)
        {
            if (!string.IsNullOrEmpty(data))
            {
                if (data.LastOrDefault<char>() == this._writeLineDelimiter)
                {
                    this.Reply(data);
                }
                else
                {
                    byte[] bytes = new byte[] { this._writeLineDelimiter };
                    this.Reply(data + this._encoder.GetString(bytes));
                }
            }
        }

        public byte[] Data { get; private set; }

        public string MessageString =>
            !this._autoTrim ? this._encoder.GetString(this.Data) : this._encoder.GetString(this.Data).Trim();
    }
}

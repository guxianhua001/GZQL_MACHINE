
/*----------------------------------------------------------------
* 命名空间: TCPLib.TCPLib
*
* 类 名： CsvServer
* 功 能： N/A
* 唯一标识：c91673b0-c0d7-4c6f-9b58-d9979430a334
* 
* 变更日期：2023/8/22 23:26:22
* 作者：szb
* 公司：CYG
*----------------------------------------------------------------*/

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace TCPLib
{
    public class CsvServer
    {
        private Thread _thread;
        private ConcurrentQueue<CsvInfo> queue = new ConcurrentQueue<CsvInfo>();
        private static readonly CsvServer instance = new CsvServer();
        public object obj = new object();

        private CsvServer()
        {
            this.Start();
        }

        private void Kill()
        {
            foreach (Process process in Process.GetProcesses())
            {
                if (process.ProcessName.ToUpper() == "ET")
                {
                    process.CloseMainWindow();
                    process.WaitForExit();
                }
            }
        }

        private void ProcessEventQueue()
        {
            while (true)
            {
                while (true)
                {
                    if (this.queue.Count > 0)
                    {
                        CsvInfo info;
                        this.queue.TryDequeue(out info);
                        try
                        {
                            object obj2 = this.obj;
                            lock (obj2)
                            {
                                this.Kill();
                                StreamWriter writer = File.AppendText(info.Path);
                                writer.WriteLine(info.Line);
                                writer.Dispose();
                            }
                        }
                        catch
                        {
                        }
                    }
                    break;
                }
                Thread.Sleep(20);
            }
        }

        public void Start()
        {
            this.Stop();
            this._thread = new Thread(new ThreadStart(this.ProcessEventQueue));
            this._thread.IsBackground = true;
            this._thread.Start();
        }

        public void Stop()
        {
            if (this._thread != null)
            {
                this._thread.Abort();
            }
        }

        public void WriteLine(string path, string line)
        {
            CsvInfo item = new CsvInfo
            {
                Path = path,
                Line = line
            };
            this.queue.Enqueue(item);
        }

        public static CsvServer Instance =>
            instance;
    }
}

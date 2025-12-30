using HandyControl.Expression.Shapes;
using Interfaces;
using Prism.Commands;
using Prism.Mvvm;
using SmarterMotion;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Framework.ViewModels
{
    public class DebugThresholdViewModel : BindableBase
    {
        // 阈值属性
        private double _threshold;
        public double ThresholdValue
        {
            get => _threshold;
            set
            {
                if (value >= 0 && value <= 1000) // 示例校验范围
                {
                    SetProperty(ref _threshold, value);
                }
            }
        }
        // 方向选择（使用两个独立属性允许双向选择）
        private bool _isForward = true;
        public bool IsForward
        {
            get => _isForward;
            set
            {
                _isForward = value;
                if (value) IsReverse = false; // 强制单向选择
                SetProperty(ref _isForward, value);
            }
        }
        private bool _isReverse;
        public bool IsReverse
        {
            get => _isReverse;
            set
            {
                _isReverse = value;
                if (value) IsForward = false;
                SetProperty(ref _isReverse, value);
            }
        }
        private int _slaveno = 1;
        public int Slaveno
        {
            get => _slaveno;
            set
            {
                _slaveno = value;
                SetProperty(ref _slaveno, value);
            }
        }
        //通道
        private int _calno;
        public int Calno
        {
            get => _calno;
            set
            {
                _calno = value;
                SetProperty(ref _calno, value);
            }
        }
        private int _pollingFrequency = 1000; // 默认 1000ms
        public int PollingFrequency
        {
            get => _pollingFrequency;
            set => SetProperty(ref _pollingFrequency, value);
        }

        // 命令定义
        public DelegateCommand SetThresholdCommand { get; private set; }
        public DelegateCommand ReadBufferCommand { get; private set; }
        public DelegateCommand SetFrequencyCommand { get; private set; }
        public DelegateCommand MoveCommand { get; private set; }
        public DelegateCommand StopCommand { get; private set; }

        private readonly IDeviceService _deviceService;
        private readonly int _slaveno1 = 1; // 从站号
        private readonly int _slaveno2 = 2; // 从站号

        public DebugThresholdViewModel(IDeviceService deviceService)
        {
            _deviceService = deviceService;
            SetThresholdCommand = new DelegateCommand(() =>
                SetThresholdMode()
            );
            //读取缓存
            ReadBufferCommand = new DelegateCommand(() =>
                ReadBuffer()
            );
            SetFrequencyCommand = new DelegateCommand(() =>
                SetFrequency()
            );
            MoveCommand = new DelegateCommand(() =>
                MoveToPosition()
            );
            StopCommand = new DelegateCommand(() =>
                StopMotion()
            );
        }
        private Thread _monitorThread;
        private volatile bool _shouldMonitor;
        private readonly object _lock = new object();
        private void SetFrequency()
        {
            int calnum = 0;
            _deviceService.SetFrequency(_slaveno, _pollingFrequency);
            _deviceService.ResetFifo(_slaveno, _calno);
        }
        public void SetThresholdMode()
        {
            //阈值设定
            int data = (int)((double)_threshold * 32767 / 10);
            int dir = 0;
            if (_isForward)
                dir = 0;
            else
                dir = -1;
            _deviceService.ResetFifo(_slaveno, 0);
            _deviceService.ResetFifo(_slaveno, 1);
            _deviceService.SetThresholdMode(_slaveno, _calno, data, dir);
           
        }
        /// <summary>
        /// 获取阈值输出信号状态 0:已触发 1:未触发
        /// </summary>
        public bool GetThresholdOut()
        {
            var statusword = LTDMC.dmc_read_inbit(0, 1);//常开信号
            return statusword == 0;
        }
        private void MonitorThreadProc()
        {
            ushort actCardId = 0;
            const int STATUS_BIT_TO_WATCH = 19; // 需要监控的状态位
            const int POLLING_INTERVAL_MS = 1; // 轮询间隔(ms)
            Stopwatch timeoutWatch = Stopwatch.StartNew();
            const int TIMEOUT_MS = 35000; // 超时时间5秒
            try
            {
                while (_shouldMonitor)//&& timeoutWatch.ElapsedMilliseconds < TIMEOUT_MS
                {
                    // 读取状态字
                    //var sw = Stopwatch.StartNew();
                    var statusword = LTDMC.dmc_read_inbit(0, 1);//常开信号
                    //sw.Stop();
                    //IMessage.Logger.Info($"[性能统计] dmc_read_inbit 读取耗时: {sw.ElapsedMilliseconds:F3}ms");
                    // 检查第19位
                    if (statusword == 0)
                    {
                        var res = LTDMC.dmc_stop((ushort)actCardId, (ushort)7, 1);
                        //Thread.Sleep(50);  
                        double actualPosition = 0;
                        LTDMC.dmc_get_position_unit(0, (ushort)7, ref actualPosition);
                        IMessage.Logger.Warn($"轴位置: {actualPosition}");
                        //res = LTDMC.dmc_pmove_unit((ushort)actCardId, (ushort)7, actualPosition + 0.5, 1);
                        ReadBuffer();
                        break;
                    }
                    //Thread.Sleep(POLLING_INTERVAL_MS);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"监控线程异常: {ex.Message}");
            }
            finally
            {
                timeoutWatch.Stop();
            }
        }

        public void ReadBuffer()
        {
            int calnum = 0;
            int count = 0;
            int[] a0 = new int[20000];
            int[] p0 = new int[20000];
            double[] d0 = new double[20000];

            while (true)
            {
                count = _deviceService.GetAcqFIFOCnt(_slaveno, _calno);
                if (count > 6000)
                {
                    break;
                }
            }
            _deviceService.GetSyncCollectionData(_slaveno, ref calnum, _calno, ref a0[0], ref p0[0]);
            for (int i = 0; i < calnum; i++)
            {
                //数据拼接
                d0[i] = (double)(short)a0[i] / 32767 * 10;
            }
            //数据展示
            //for (int i = 0; i < calnum; i++)
            //{
            //    TorqueChart1.UpdateSeries(d0[i], 0);
            //}
            int index = 1;
            if (index > 0)
            {
                ExportToCsv(d0, calnum, "C:\\Users\\Administrator\\Desktop\\力矩数据.csv");
            }
        }
        public void ExportToCsv(double[] a0, int calnum, string filePath)
        {
            // 创建CSV内容
            var csvContent = new StringBuilder();
            csvContent.AppendLine("序号,转换值(N)"); // 表头
            for (int i = 0; i < calnum; i++)
            {
                csvContent.AppendLine($"{i + 1},{a0[i]:F3}");
            }
            // 写入文件（自动处理文件编码和换行符）
            File.WriteAllText(filePath, csvContent.ToString(), Encoding.UTF8);
        }

        private void MoveToPosition()
        {
            // 启动监控线程
            lock (_lock)
            {
                if (_monitorThread != null && _monitorThread.IsAlive)
                {
                    _shouldMonitor = false;
                    _monitorThread.Join();
                }
                _shouldMonitor = true;
                _monitorThread = new Thread(() => MonitorThreadProc())
                {
                    Name = "ThresholdMonitorThread",
                    Priority = ThreadPriority.AboveNormal // 提高监控线程优先级
                };
                _monitorThread.Start();
            }
            Thread.Sleep(200);
            _deviceService.ResetFifo(_slaveno, _calno);
            _deviceService.StartAcquisition(_slaveno, _calno);
            double position = -3;
            double minvel = 0, maxvel = 0, acc = 0, dec = 0, stopvel = 0.3;
            //LTDMC.dmc_get_profile_unit((ushort)0, (ushort)7, ref minvel, ref maxvel, ref acc, ref dec,
            //           ref stopvel);
            LTDMC.dmc_set_profile_unit((ushort)0, (ushort)7, minvel, 3, 0.1, 0.15, stopvel);
            LTDMC.dmc_set_s_profile((ushort)0, (ushort)7, 0, 0.1);//设置S段速度参数
            LTDMC.dmc_pmove_unit((ushort)0, (ushort)7, position, 0);
        }
        private void StopMotion()
        {
            LTDMC.dmc_stop((ushort)0, (ushort)7, 1);
            _shouldMonitor = false;
        }
    }
}

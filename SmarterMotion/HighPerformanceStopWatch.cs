using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace SmarterMotion
{
    public class HighPerformanceStopWatch
    {
        #region private members

        private long frequency = 0;

        private long elapsedTime = 0;

        private long baseTime = 0;

        #endregion


        # region windows API

        /// <summary>
        /// 获取时间的精度
        /// </summary>
        /// <param name="PerformanceFrequency"></param>
        /// <returns></returns>
        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32")]
        static private extern bool QueryPerformanceFrequency(ref long PerformanceFrequency);
        /// <summary>
        /// 获取时间计数
        /// </summary>
        /// <param name="PerformanceCount"></param>
        /// <returns></returns>
        [SuppressUnmanagedCodeSecurity]
        [DllImport("kernel32")]

        static private extern bool QueryPerformanceCounter(ref long PerformanceCount);

        #endregion

        #region constructors
        public HighPerformanceStopWatch()
        {
            if (!QueryPerformanceFrequency(ref frequency))
                /*throw new ApplicationException("Timer: Performance Frequency Unavailable")*/
                ;
            Reset();
        }
        #endregion

        #region public methods
        public void Start()
        {
            QueryPerformanceCounter(ref baseTime);
        }
        public double SlicedTime
        {
            get
            {
                long counter = 0;
                QueryPerformanceCounter(ref counter);
                return (double)(counter - baseTime) * 1000.0 / frequency; //转为ms
            }
        }

        /// <summary>
        /// 重置时间相关计数器
        /// </summary>
        public void Reset()
        {
            long time = 0;
            QueryPerformanceCounter(ref time);
            baseTime = time;
            elapsedTime = 0;
        }
        /// <summary>
        /// 获取当前与最近一次 reset 时间差
        /// </summary>
        /// <returns></returns>
        public double GetTime()
        {
            long time = 0;
            QueryPerformanceCounter(ref time);
            return (double)(time - baseTime) / (double)frequency;
        }

        /// <summary>
        /// 获取当前系统的时间 ticks 数
        /// </summary>
        /// <returns></returns>
        public double GetAbsoluteTime()
        {
            long time = 0;
            QueryPerformanceCounter(ref time);
            return (double)time / (double)frequency;
        }

        /// <summary>
        /// 获取此次与上次调用此方法的两次时间差
        /// </summary>
        /// <returns></returns>
        public double GetElapsedTime()
        {
            long time = 0;
            QueryPerformanceCounter(ref time);
            double absoluteTime = (double)(time - elapsedTime) / (double)frequency;
            elapsedTime = time;
            return absoluteTime;

        }

        //判断一个方法是否在多少秒内重复进入
        //HighPerformanceStopWatch t = new HighPerformanceStopWatch();
        //double t1 = t.GetElapsedTime();
        //double ms = Math.Round((t1 - temp), 3);
        //double dif = Math.Round((t1 - temp) * 1000000);  //秒->微秒
        //temp = t1;
        //if (dif< 1200)  //100us
        //{
        //  return;
        //}

        #endregion

    }
}

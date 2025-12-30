using lctdevice;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static Interfaces.LctDeviceService;

namespace Interfaces
{
    public interface IDeviceService
    {
        int InitializeEcat();
        int GetEncoderData(int slaveNo, int channel);
        short GetAnalogInput(int slaveNo, int channel);
        int GetAcqFIFOCnt(int slaveNo, int channelIndex);
        void GetFifoData(
            int slaveNo,
            ref short ain0,
            ref short ain1,
            ref short ain2,
            ref short ain3,
            ref int encoder,
            ref uint timeStamp);
        void ResetFifo(int slaveNo, int channelIndex);
        void SetFrequency(int slaveNo, int frequency);
        void SetThresholdMode(int slaveNo, int channel, int data, int dir);
        int GetSyncCollectionData(
        int slaveNo,
        ref int calnum,
        int calNo,
        ref int a0,
        ref int p0);
        void StartAcquisition(int slaveNo, int channel);
        void StopAcquisition(int slaveNo, int channel);
        void SetForceThreshold(int SlaveNo, int CalNo, bool isForward, double threshold);
        ForceDataPackage ReadForceData(int SlaveNo, ref int calnum, int CalNo, double tolerance = 0.2);

        double[] ReadBuffer(int _slaveno, int _calno, int FIFOCount = 6000);
    }

    public class LctDeviceService : IDeviceService
    {
        public int InitializeEcat()
        {
            int slaveno = 0;
            lctdevice.MiniEcatLib.Mb_InitEcat(ref slaveno, 0);
            if (slaveno == 0)
            {
                return -1;
            }
            lctdevice.MiniEcatLib.Mb_C2A4_InitAnalogCal(slaveno);
            return 0;

        }
        public int GetEncoderData(int slaveNo, int channel)
        {
            int enc = 0;
            lctdevice.MiniEcatLib.Mb_C2A4_GetEncoderData(slaveNo, channel, ref enc);
            return enc;
        }

        public short GetAnalogInput(int slaveNo, int channel)
        {
            short value = 0;
            lctdevice.MiniEcatLib.Mb_C2A4_GetAnalogInput(slaveNo, channel, ref value);
            return value;
        }

        public int GetAcqFIFOCnt(int _slaveNo, int _channelIndex)
        {
            int fifoCount = 0;
            lctdevice.MiniEcatLib.Mb_C2A4A_GetAcqFIFOCnt(_slaveNo, _channelIndex, ref fifoCount);
            return fifoCount;
        }

        public void GetFifoData(
        int slaveNo,
        ref short ain0,
        ref short ain1,
        ref short ain2,
        ref short ain3,
        ref int encoder,
        ref uint timeStamp)
        {
            MiniEcatLib.Mb_C2A4_GetFifoData(
                slaveNo,
                ref ain0,
                ref ain1,
                ref ain2,
                ref ain3,
                ref encoder,
                ref timeStamp);
        }
        public void SetThresholdMode(int slaveNo, int channel, int data, int dir)
        {
            // 参数有效性验证
            if (dir != 0 && dir != -1)
                throw new ArgumentException("方向参数必须为0（正向）或-1（反向）", nameof(dir));
            // 调用硬件SDK接口
            MiniEcatLib.Mb_C2A4_SetThresholdMode(
                slaveNo,    // 从站号
                channel,    // 通道号
                data,       // 阈值数据
                dir         // 方向参数
            );
        }
       
        public int GetSyncCollectionData(
        int slaveNo,
        ref int calnum,
        int channel,
        ref int a0,
        ref int p0)
        {
            return MiniEcatLib.Mb_C2A4A_GetAcqFIFOData(
                slaveNo,
                ref calnum,
                channel,
                ref a0,
                ref p0);
        }
        public void StartAcquisition(int slaveNo, int channelIndex)
        {
            MiniEcatLib.Mb_C2A4A_StartADAndEncChnAcq(slaveNo, channelIndex);
        }
        public void StopAcquisition(int slaveNo, int channelIndex)
        {
            MiniEcatLib.Mb_C2A4A_StopADAndEncChnAcq(slaveNo, channelIndex);
        }
        public void ResetFifo(int slaveNo, int channelIndex)
        {
            MiniEcatLib.Mb_C2A4A_ResetFifo(slaveNo, channelIndex);
        }
        #region 力控阈值功能增强
        public void SetFrequency(int slaveNo, int frequency)
        {
            MiniEcatLib.Mb_C2A4_SetTimerMode(slaveNo, frequency);
        }
        /// <summary>
        /// 力控阈值设定（带单位转换）
        /// </summary>
        /// <param name="isForward">true:正向触发 false:反向触发</param>
        /// <param name="threshold">阈值物理量（单位：牛顿）</param>
        public void SetForceThreshold(int SlaveNo, int CalNo, bool isForward, double threshold)
        {
            // 物理量转数字量（10N对应32767）
            int data = (int)((double)threshold * 32767 / 10);
            int dir = isForward ? 0 : -1;
            SetThresholdMode(SlaveNo, CalNo, data, dir);
        }

        #endregion

        #region 高速数据采集功能
        public enum AnalogChannelGroup
        {
            GroupA0_A1 = 0,
            GroupA2_A3 = 1
        }

        public class ForceDataPackage
        {
            public double[] AnalogData { get; set; }
            public int[] EncoderData { get; set; }
            public double AverageForceInRange { get; set; }
            public int ValidDataCount { get; set; }
            public double MaxForceValue { get; set; }
            public double MinForceValue { get; set; }
        }
        /// <summary>
        /// 读取缓冲区数据并计算有效力值
        /// </summary>
        /// <param name="tolerance">有效范围容忍值（默认±0.2N）</param>
        public ForceDataPackage ReadForceData(int SlaveNo, ref int calnum, int CalNo, double tolerance = 0.2)
        {
            var package = new ForceDataPackage();
            calnum = 0;

            // 初始化缓冲区
            int[] a0 = new int[20000], a1 = new int[20000], a2 = new int[20000],
                  a3 = new int[20000], p0 = new int[20000], p1 = new int[20000];
            // 获取有效数据量
            int count = GetAcqFIFOCnt(SlaveNo, CalNo);
            if (count < 1000) return package;
            // 读取同步采集数据
            GetSyncCollectionData(SlaveNo, ref calnum, CalNo,
                ref a0[0],
                ref p0[0]);
            // 根据选择的通道组处理数据
            package.AnalogData = ProcessAnalogData(a0, calnum);
            package.EncoderData = p0.Take(calnum).ToArray();
            package.ValidDataCount = calnum;
            // 计算统计数据
            if (calnum > 0)
            {
                package.MaxForceValue = package.AnalogData.Max();
                package.MinForceValue = package.AnalogData.Min();

                var validData = package.AnalogData
                    .Where(v => Math.Abs(v - package.MaxForceValue) <= tolerance)
                    .ToArray();
                package.AverageForceInRange = validData.Any() ? validData.Average() : 0;
            }

            return package;

        }
        private double[] ProcessAnalogData(int[] rawData, int count)
        {
            double[] result = new double[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = (double)(short)rawData[i] / 32767 * 10; // 转换为物理量
            }
            return result;
        }

        public double[] ReadBuffer(int _slaveno, int _calno, int FIFOCount = 6000)
        {
            int calnum = 0;
            int count = 0;
            int[] a0 = new int[20000];
            int[] p0 = new int[20000];
            double[] d0 = new double[20000];

            while (true)
            {
                count = GetAcqFIFOCnt(_slaveno, _calno);
                if (count > FIFOCount)
                {
                    break;
                }
            }
            GetSyncCollectionData(_slaveno, ref calnum, _calno, ref a0[0], ref p0[0]);
            for (int i = 0; i < calnum; i++)
            {
                //数据拼接
                d0[i] = (double)(short)a0[i] / 32767 * 10;
            }
            return d0;
        }
        #endregion

        #region 异常处理类
        public class DeviceOperationException : Exception
        {
            public DeviceOperationException(string message, Exception inner)
                : base(message, inner) { }
        }
        #endregion


    }
}

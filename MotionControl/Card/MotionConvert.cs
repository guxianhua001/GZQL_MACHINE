
using System;

namespace MotionControl.Card
{
    /// <summary>
    /// 通用转换与位操作工具
    /// </summary>
    public static class MotionConvert
    {
        #region 位操作

        /// <summary>检查指定位是否置位</summary>
        public static bool BitEnable(int word, int bits)
        {
            return (word & bits) != 0;
        }

        /// <summary>设置指定位</summary>
        public static void SetBits(ref int word, int bits)
        {
            word |= bits;
        }

        /// <summary>清除指定位</summary>
        public static void ClrBits(ref int word, int bits)
        {
            word &= ~bits;
        }

        /// <summary>取反低16位（针对原项目习惯）</summary>
        public static int ConvertValue(int sts)
        {
            return sts ^ 0xFFFF;
        }

        #endregion

        #region 数值转换

        /// <summary>毫米转脉冲（默认螺距 lead mm，10000 脉冲/转）</summary>
        public static int MM2PULS(double mm, double lead)
        {
            return System.Convert.ToInt32(mm * 10000 / lead);  
        }

        /// <summary>脉冲转毫米</summary>
        public static double PULS2MM(int puls, double lead)
        {
            return puls * lead / 10000;
        }

        #endregion

        #region 字符串与数组互转

        public static int[] Str2IntG(string str, char c)
        {
            if (string.IsNullOrEmpty(str)) return new int[0];
            return str.Split(c).Select(int.Parse).ToArray();
        }

        public static double[] Str2DoubleG(string str, char c)
        {
            if (string.IsNullOrEmpty(str)) return new double[0];
            return str.Split(c).Select(double.Parse).ToArray();
        }

        public static string IntG2Str(int[] data, string separator)
        {
            return data == null ? "" : string.Join(separator, data);
        }

        public static string DoubleG2Str(double[] data, string separator)
        {
            return data == null ? "" : string.Join(separator, data.Select(d => d.ToString("F4")));
        }

        public static string[] Str2StrG(string str, char c)
        {
            return str?.Split(c) ?? new string[0];
        }

        #endregion
    }
}

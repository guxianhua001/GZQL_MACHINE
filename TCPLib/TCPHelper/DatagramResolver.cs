#region << 版 本 注 释 >>
/*----------------------------------------------------------------
 * 版权所有 (c) 2024 China  保留所有权利。
 * CLR版本：4.0.30319.42000
 * 机器名称：USER-20240611LB
 * 命名空间：TCPLib.TCPHelper
 * 唯一标识：2cbd2ee4-f1f7-4106-831f-e6daf4851d6c
 * 文件名：DatagramResolver
 * 
 * 创建者：szb
 * 创建时间：2024/8/12 15:28:19
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCPLib.TCPHelper
{
    /// <summary> 
    /// 数据报文分析器,通过分析接收到的原始数据,得到完整的数据报文. 
    /// 继承该类可以实现自己的报文解析方法. 
    /// 当前没有设定报文的解析方法
    /// </summary> 
    public class DatagramResolver
    {
        /// <summary> 
        /// 报文结束标记 
        /// </summary> 
        private string endTag;

        /// <summary> 
        /// 返回结束标记 
        /// </summary> 
        public string EndTag
        {
            get
            {
                return endTag;
            }
        }

        /// <summary> 
        /// 受保护的默认构造函数,提供给继承类使用 
        /// </summary> 
        protected DatagramResolver()
        {

        }

        /// <summary> 
        /// 构造函数 
        /// </summary> 
        /// <param name="endTag">报文结束标记</param> 
        public DatagramResolver(string endTag)
        {
            if (endTag == null)
            {
                throw (new ArgumentNullException("结束标记不能为null"));
            }

            if (endTag == "")
            {
                throw (new ArgumentException("结束标记符号不能为空字符串"));
            }

            this.endTag = endTag;
        }

        /// <summary> 
        /// 解析报文 
        /// </summary> 
        /// <param name="rawDatagram">原始数据,返回未使用的报文片断, 
        /// 该片断会保存在Session的Datagram对象中</param> 
        /// <returns>报文数组,原始数据可能包含多个报文</returns> 
        public virtual string[] Resolve(ref string rawDatagram)
        {
            ArrayList datagrams = new ArrayList();

            //末尾标记位置索引 
            int tagIndex = -1;

            while (true)
            {
                tagIndex = rawDatagram.IndexOf(endTag, tagIndex + 1);

                if (tagIndex == -1)
                {
                    break;
                }
                else
                {
                    //按照末尾标记把字符串分为左右两个部分 
                    string newDatagram = rawDatagram.Substring(
                        0, tagIndex + endTag.Length);

                    datagrams.Add(newDatagram);

                    if (tagIndex + endTag.Length >= rawDatagram.Length)
                    {
                        rawDatagram = "";

                        break;
                    }
                    rawDatagram = rawDatagram.Substring(tagIndex + endTag.Length, rawDatagram.Length - newDatagram.Length);

                    //从开始位置开始查找 
                    tagIndex = 0;
                }
            }

            string[] results = new string[datagrams.Count];

            datagrams.CopyTo(results);

            return results;
        }
    }
}

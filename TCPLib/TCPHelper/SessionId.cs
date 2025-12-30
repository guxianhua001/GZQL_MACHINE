#region << 版 本 注 释 >>
/*----------------------------------------------------------------
 * 版权所有 (c) 2024 China  保留所有权利。
 * CLR版本：4.0.30319.42000
 * 机器名称：USER-20240611LB
 * 命名空间：TCPLib.TCPHelper
 * 唯一标识：798ccc4d-cbeb-4532-a9a9-d40b18c5e25a
 * 文件名：SessionId
 * 
 * 创建者：szb
 * 创建时间：2024/8/12 15:29:16
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TCPLib.TCPHelper
{
    /// <summary> 
    /// 唯一的标志一个Session,辅助Session对象在Hash表中完成特定功能 
    /// </summary> 
    public class SessionId
    {
        /// <summary> 
        /// 与Session对象的Socket对象的Handle值相同,必须用这个值来初始化它 
        /// </summary> 
        private int _id;

        /// <summary> 
        /// 返回ID值 
        /// </summary> 
        public int ID
        {
            get
            {
                return _id;
            }
        }

        /// <summary> 
        /// 构造函数 
        /// </summary> 
        /// <param name="id">Socket的Handle值</param> 
        public SessionId(int id)
        {
            _id = id;
        }

        /// <summary> 
        /// 重载.为了符合Hashtable键值特征 
        /// </summary> 
        /// <param name="obj"></param> 
        /// <returns></returns> 
        public override bool Equals(object obj)   //Equals(object obj)是类Object(所有类的基类)的Virtual类型函数。
        {
            if (obj != null)
            {
                SessionId right = (SessionId)obj;
                return _id == right._id;
            }
            else if (this == null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary> 
        /// 重载.为了符合Hashtable键值特征 
        /// </summary> 
        /// <returns></returns> 
        public override int GetHashCode()
        {
            return _id;
        }

        /// <summary> 
        /// 重载,为了方便显示输出 
        /// </summary> 
        /// <returns></returns> 
        public override string ToString()
        {
            return _id.ToString();
        }
    }
}

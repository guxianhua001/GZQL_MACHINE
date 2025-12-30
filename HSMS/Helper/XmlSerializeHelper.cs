using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace HSMS
{
    public class XmlSerializeHelper
    {
        #region 使用演示
        //aaa Example = new aaa();
        //Example.Name = "张三";
        //Example.Age = 20;
        string strxml;
        //反序列化演示
        //aaa Example = XmlSerializeHelper.DESerializer<aaa>(strxml); 
        //序列化演示
        //string strxml = XmlSerializeHelper.XmlSerialize<aaa>(Example);
        #endregion

        /// <summary>
        /// 实体类转换成XML
        /// </summary>
        /// <typeparam name="T">类名</typeparam>
        /// <param name="obj">T类名的实例</param>
        /// <returns></returns>
        public static string XmlSerialize<T>(T obj)
        {
            using (StringWriter sw = new StringWriter())
            {
                Type t = obj.GetType();
                XmlSerializer serializer = new XmlSerializer(obj.GetType());
                serializer.Serialize(sw, obj);
                sw.Close();
                return sw.ToString();
            }
        }

        public static void XmlSerialize<T>(T obj, string filePath)
        {
            try
            {
                using (FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    new XmlSerializer(obj.GetType()).Serialize((Stream)fileStream, (object)obj);
                    fileStream.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// XML转换成实体类-方法1
        /// </summary>
        /// <typeparam name="T">对应的类</typeparam>
        /// <param name="strXML">XML字符串</param>
        /// <returns></returns>
        public static T DESerializer<T>(string strXML) where T : class
        {
            try
            {
                using (FileStream fileStream = new FileStream(strXML, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    return new XmlSerializer(typeof(T)).Deserialize((Stream)fileStream) as T;
            }
            catch (Exception ex)
            {
                return default(T);
            }
        }

        /// <summary>
        /// XML转换成实体类-方法2
        /// </summary>
        /// <param name="xmlStr"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public static object DeserializeFromXml(string xmlStr, Type type)
        {
            try
            {
                using (StringReader sr = new StringReader(xmlStr))
                {
                    XmlSerializer xs = new XmlSerializer(type);
                    return xs.Deserialize(sr);
                }
            }
            catch (Exception ex)
            {
                throw (ex);
            }
        }

    }
}

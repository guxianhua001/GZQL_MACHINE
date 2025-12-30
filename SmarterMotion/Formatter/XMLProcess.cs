

namespace SmarterMotion
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Xml;

    public class XMLProcess : XObject
    {
        #region 构造函数
        public XMLProcess()
        { }

        public XMLProcess(string xmlFilePath)
        {
            this._XMLPath = xmlFilePath;
        }
        #endregion

        #region 公有属性
        private string _XMLPath;
        public string XMLPath
        {
            get { return this._XMLPath; }
        }
        #endregion

        #region 私有方法
        /// <summary>
        /// 导入XML文件
        /// </summary>
        /// <param name="XMLPath">XML文件路径</param>
        private XmlDocument XMLLoad()
        {
            string XMLFile = XMLPath;
            XmlDocument xmldoc = new XmlDocument();
            try
            {
                string filename = AppDomain.CurrentDomain.BaseDirectory.ToString() + XMLFile;
                if (File.Exists(filename)) xmldoc.Load(filename);
            }
            catch (Exception e)
            { }
            return xmldoc;
        }

        /// <summary>
        /// 导入XML文件
        /// </summary>
        /// <param name="XMLPath">XML文件路径</param>
        private static XmlDocument XMLLoad(string strPath)
        {
            XmlDocument xmldoc = new XmlDocument();
            try
            {
                string filename = AppDomain.CurrentDomain.BaseDirectory.ToString() + strPath;
                if (File.Exists(filename)) xmldoc.Load(filename);
            }
            catch (Exception e)
            { }
            return xmldoc;
        }

        #endregion

        private static object locker = new object();

        public static void Insert(string xmlPath, string nodeName)
        {
            try
            {
                lock (locker)
                {
                    string path = xmlPath;// AppDomain.CurrentDomain.BaseDirectory.ToString() + path;
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlNode documentElement = document.DocumentElement;
                        foreach (XmlNode node2 in documentElement.ChildNodes)
                        {
                            if (node2.Name == nodeName)
                            {
                                return;
                            }
                        }
                        XmlElement newChild = document.CreateElement(nodeName);
                        documentElement.AppendChild(newChild);
                        document.Save(path);
                    }
                    else
                    {
                        //MessageBox.Show("配置文件不存在");
                    }
                }
            }
            catch (Exception ex)
            {
                //CommonModules.Services.NLogService.Error($"{ex.Message}");
            }
        }

        public static void Insert(string xmlPath, string nodeName, string newKey, string newValue)
        {
            try
            {
                lock (locker)
                {
                    string path = xmlPath;
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlElement documentElement = document.DocumentElement;
                        XmlNode node = documentElement.SelectSingleNode(nodeName);
                        foreach (XmlElement element2 in documentElement.ChildNodes)
                        {
                            if (element2.Name == nodeName)
                            {
                                foreach (XmlNode node2 in element2)
                                {
                                    if (node2.Name == newKey)
                                    {
                                        node2.InnerText = newValue;
                                        document.Save(path);
                                        return;
                                    }
                                }
                            }
                        }
                        XmlElement newChild = document.CreateElement(newKey);
                        newChild.InnerText = newValue;
                        node.AppendChild(newChild);
                        document.Save(path);
                    }
                    else
                    {
                        //MessageBox.Show("配置文件不存在");
                    }
                }
            }
            catch (Exception ex)
            {
                //CommonModules.Services.NLogService.Error($"{ex.Message}");
            }
        }

        public static void Insert(string xmlPath, string nodeName, string newKey, string attributes, string newValue)
        {
            try
            {
                lock (locker)
                {
                    string path = xmlPath;
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlElement documentElement = document.DocumentElement;
                        XmlNode node = documentElement.SelectSingleNode(nodeName);
                        foreach (XmlElement element2 in documentElement.ChildNodes)
                        {
                            if (element2.Name == nodeName)
                            {
                                foreach (XmlNode node2 in element2)
                                {
                                    if (node2.Name == newKey)
                                    {
                                        bool flag = false;
                                        foreach (XmlAttribute attribute in node2.Attributes)
                                        {
                                            if (attribute.Name == attributes)
                                            {
                                                attribute.Value = newValue;
                                                flag = true;
                                            }
                                        }
                                        if (!flag)
                                        {
                                            XmlAttribute attribute2 = document.CreateAttribute(attributes);
                                            attribute2.Value = newValue;
                                            node2.Attributes.Append(attribute2);
                                        }
                                        document.Save(path);
                                        return;
                                    }
                                }
                            }
                        }
                        XmlElement newChild = document.CreateElement(newKey);
                        node.AppendChild(newChild);
                        XmlAttribute attribute3 = document.CreateAttribute(attributes);
                        attribute3.InnerText = newValue;
                        foreach (XmlElement element2 in documentElement.ChildNodes)
                        {
                            if (element2.Name == nodeName)
                            {
                                element2[newKey].Attributes.Append(attribute3);
                            }
                        }
                        document.Save(path);
                    }
                    else
                    {
                        //MessageBox.Show("配置文件不存在");
                    }
                }
            }
            catch (Exception ex)
            {
                //CommonModules.Services.NLogService.Error($"{ex.Message}");
            }
        }

        public static void createXML(string xmlPath, string parent)
        {
            try
            {
                lock (locker)
                {
                    string path = xmlPath;
                    XmlDocument document = new XmlDocument();
                    if (!File.Exists(path))
                    {
                        using (new FileStream(path, FileMode.Create, FileAccess.Write))
                        {
                        }
                        document.AppendChild(document.CreateXmlDeclaration("1.0", "UTF-8", null));
                        XmlElement newChild = document.CreateElement(parent);  //"configuration"
                        document.AppendChild(newChild);
                        document.Save(path);
                    }
                }
            }
            catch (Exception ex)
            {
                //CommonModules.Services.NLogService.Error($"{ex.Message}");
            }
        }

        public static void Delete(string xmlPath, string nodeName)
        {
            try
            {
                string path = xmlPath;
                lock (locker)
                {
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlNode documentElement = document.DocumentElement;
                        foreach (XmlNode node2 in documentElement.ChildNodes)
                        {
                            if (node2.Name == nodeName)
                            {
                                documentElement.RemoveChild(node2);
                                document.Save(path);
                                return;
                            }
                        }
                    }
                    else
                    {
                        //MessageBox.Show("配置文件不存在");
                    }
                }
            }
            catch (Exception ex)
            {
                //CommonModules.Services.NLogService.Error($"{ex.Message}");
            }
        }

        public static void Delete(string xmlPath, string nodeName, string key)
        {
            try
            {
                string path = xmlPath;
                lock (locker)
                {
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlNode documentElement = document.DocumentElement;
                        foreach (XmlNode node2 in documentElement.ChildNodes)
                        {
                            if (node2.Name == nodeName)
                            {
                                foreach (XmlNode node3 in node2)
                                {
                                    if (node3.Name == key)
                                    {
                                        node2.RemoveChild(node3);
                                        document.Save(path);
                                        return;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        //MessageBox.Show("配置文件不存在");
                    }
                }
            }
            catch (Exception ex)
            {
                //CommonModules.Services.NLogService.Error($"{ex.Message}");
            }
        }

        public static void Delete(string xmlPath, string nodeName, string key, string attributes)
        {
            try
            {
                string path = xmlPath;
                lock (locker)
                {
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlNode documentElement = document.DocumentElement;
                        foreach (XmlNode node2 in documentElement.ChildNodes)
                        {
                            if (node2.Name == nodeName)
                            {
                                foreach (XmlNode node3 in node2)
                                {
                                    if (node3.Name == key)
                                    {
                                        foreach (XmlAttribute attribute in node3.Attributes)
                                        {
                                            if (attribute.Name == attributes)
                                            {
                                                node3.Attributes.Remove(attribute);
                                                document.Save(path);
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        //MessageBox.Show("配置文件不存在");
                    }
                }
            }
            catch (Exception ex)
            {
                //CommonModules.Services.NLogService.Error($"{ex.Message}");
            }
        }

        private static List<string> GetAllConfigInNode(string xmlPath, string nodeName)
        {
            List<string> list = new List<string>();
            try
            {
                lock (locker)
                {
                    string path = xmlPath;
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlNode documentElement = document.DocumentElement;
                        foreach (XmlNode node2 in documentElement.ChildNodes)
                        {
                            if (node2.Name == nodeName)
                            {
                                foreach (XmlNode node3 in node2)
                                {
                                    list.Add(node3.Name);
                                }
                            }
                        }
                        return list;
                    }
                    //MessageBox.Show("配置文件不存在");
                    return list;
                }
            }
            catch (Exception ex)
            {
                //CommonModules.Services.NLogService.Error($"{ex.Message}");
            }
            return list;
        }

        public static List<string> GetAllKey(string xmlPath)
        {
            List<string> list = new List<string>();
            try
            {
                lock (locker)
                {
                    string path = xmlPath;
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlNode documentElement = document.DocumentElement;
                        foreach (XmlNode node2 in documentElement.ChildNodes)
                        {
                            list.Add(node2.Name);
                        }
                        return list;
                    }
                    //MessageBox.Show("配置文件不存在");
                    return list;
                }
            }
            catch (Exception ex)
            {
                //CommonModules.Services.NLogService.Error($"{ex.Message}");
            }
            return list;
        }

        public static List<string> GetAllKey(string xmlPath, string nodeName)
        {
            List<string> list = new List<string>();
            try
            {
                lock (locker)
                {
                    string path = xmlPath;
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlNode documentElement = document.DocumentElement;
                        foreach (XmlNode node2 in documentElement.ChildNodes)
                        {
                            if (node2.Name == nodeName)
                            {
                                foreach (XmlNode node3 in node2)
                                {
                                    list.Add(node3.Name);
                                }
                            }
                        }
                        return list;
                    }
                    //MessageBox.Show("配置文件不存在");
                    return list;
                }
            }
            catch (Exception ex)
            {
                //CommonModules.Services.NLogService.Error($"{ex.Message}");
            }
            return list;
        }

        public static string Get(string xmlPath, string nodeName, string key)
        {
            try
            {
                lock (locker)
                {
                    string path = xmlPath;
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlNode documentElement = document.DocumentElement;
                        foreach (XmlNode node2 in documentElement.ChildNodes)
                        {
                            if (node2.Name == nodeName)
                            {
                                foreach (XmlNode node3 in node2)
                                {
                                    if (node3.Name == key)
                                    {
                                        return node3.InnerText;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        //MessageBox.Show("配置文件不存在");
                    }
                    //MessageBox.Show("配置文件读取错误,请检查是否有误，并重启软件！nodeName:" + nodeName + ",Key:" + key);
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                //CommonModules.Services.NLogService.Error($"{ex.Message}");
                return string.Empty;
            }
        }

        public static string Get(string xmlPath, string nodeName, string key, string Attributes)
        {
            try
            {
                lock (locker)
                {
                    string path = xmlPath;
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlNode documentElement = document.DocumentElement;
                        foreach (XmlNode node2 in documentElement.ChildNodes)
                        {
                            if (node2.Name == nodeName)
                            {
                                foreach (XmlNode node3 in node2)
                                {
                                    if (node3.Name == key)
                                    {
                                        foreach (XmlAttribute attribute in node3.Attributes)
                                        {
                                            if (attribute.Name == Attributes)
                                            {
                                                return node3.Attributes[Attributes].Value;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        //MessageBox.Show("配置文件不存在");
                    }
                    //MessageBox.Show("配置文件读取错误,请检查是否有误，并重启软件！nodeName:" + nodeName + ",Key:" + key);
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                //CommonModules.Services.NLogService.Error($"{ex.Message}");
                return string.Empty;
            }
        }

        public static List<string> GetAttributes(string xmlPath, string nodeName, string key)
        {
            List<string> list = new List<string>();
            try
            {
                lock (locker)
                {
                    string path = xmlPath;
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlNode documentElement = document.DocumentElement;
                        foreach (XmlNode node2 in documentElement.ChildNodes)
                        {
                            if (node2.Name == nodeName)
                            {
                                foreach (XmlNode node3 in node2)
                                {
                                    if (node3.Name == key)
                                    {
                                        if (node3.Attributes.Count > 0)
                                        {
                                            foreach (XmlAttribute attribute in node3.Attributes)
                                            {
                                                list.Add(attribute.Name);
                                            }
                                        }
                                        return list;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        //MessageBox.Show("配置文件不存在");
                    }
                }
            }
            catch (Exception exception)
            {
                throw exception;
            }
            //MessageBox.Show("配置文件读取错误,请检查是否有误，并重启软件！nodeName:" + nodeName + ",Key:" + key);
            return null;
        }

        public static List<string> GetNodeName(string xmlPath, string nodeName)
        {
            List<string> list = new List<string>();
            try
            {
                lock (locker)
                {
                    List<string> allKey = GetAllKey(xmlPath, nodeName);
                    if (allKey.Count == 0)
                    {
                        list.Add("Key,Value");
                        return list;
                    }
                    List<string> appConfigAttributes = GetAttributes(xmlPath, nodeName, allKey[0]);
                    if (appConfigAttributes.Count > 0)
                    {
                        string str = "";
                        int num = 0;
                        while (num < appConfigAttributes.Count)
                        {
                            if (num == (appConfigAttributes.Count - 1))
                            {
                                str = str + appConfigAttributes[num];
                            }
                            else
                            {
                                str = str + appConfigAttributes[num] + ",";
                            }
                            num++;
                        }
                        list.Add("Key," + str);
                        foreach (string str2 in allKey)
                        {
                            List<string> list4 = GetAttributes(xmlPath, nodeName, str2);
                            string str3 = "";
                            for (num = 0; num < list4.Count; num++)
                            {
                                if (num == (list4.Count - 1))
                                {
                                    str3 = str3 + Get(nodeName, str2, list4[num]);
                                }
                                else
                                {
                                    str3 = str3 + Get(nodeName, str2, list4[num]) + ",";
                                }
                            }
                            list.Add(str2 + "," + str3);
                        }
                        return list;
                    }
                    list.Add("Key,Value");
                    foreach (string str2 in allKey)
                    {
                        list.Add(str2 + "," + Get(xmlPath, nodeName, str2));
                    }
                }
            }
            catch (Exception exception)
            {
                throw exception;
            }
            return list;
        }

        private static List<string> GetNodeName(string xmlPath)
        {
            List<string> list = new List<string>();
            try
            {
                lock (locker)
                {
                    string path = xmlPath;
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlNode documentElement = document.DocumentElement;
                        foreach (XmlNode node2 in documentElement.ChildNodes)
                        {
                            list.Add(node2.Name);
                        }
                        return list;
                    }
                    //MessageBox.Show("配置文件不存在！");
                    return list;
                }
            }
            catch (Exception exception)
            {
                throw exception;
            }
            return list;
        }

        public static bool IsExistNode(string xmlPath, string nodeName)
        {
            try
            {
                lock (locker)
                {
                    List<string> allKey = GetAllKey(xmlPath);
                    foreach (string str in allKey)
                    {
                        if (str == nodeName)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                throw exception;
            }
            return false;
        }

        public static bool IsExistKey(string xmlPath, string nodeName, string Key)
        {
            bool flag = false;
            try
            {
                lock (locker)
                {
                    string path = xmlPath;
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlNode documentElement = document.DocumentElement;
                        foreach (XmlNode node2 in documentElement.ChildNodes)
                        {
                            if (node2.Name == nodeName)
                            {
                                foreach (XmlNode node3 in node2)
                                {
                                    if (node3.Name == Key)
                                    {
                                        flag = true;
                                    }
                                }
                            }
                        }
                    }
                    return flag;
                }
            }
            catch (Exception exception)
            {
                //MessageBox.Show(exception.ToString());
            }
            return flag;
        }

        public static bool IsExistXML(string xmlPath)
        {
            try
            {
                lock (locker)
                {
                    string path = xmlPath;
                    XmlDocument document = new XmlDocument();
                    if (!File.Exists(path))
                    {
                        return false;
                    }
                    return true;
                }
            }
            catch (Exception exception)
            {
                //MessageBox.Show(exception.ToString());
            }
            return false;
        }

        public static bool IsExistXML(string xmlPath, bool isAppend)
        {
            bool flag = false;
            try
            {
                lock (locker)
                {
                    string path = xmlPath;
                    XmlDocument document = new XmlDocument();
                    if (!File.Exists(path))
                    {
                        flag = false;
                        if (!isAppend)
                        {
                            return false;
                        }
                        using (new FileStream(path, FileMode.Create, FileAccess.Write))
                        {
                        }
                        document.AppendChild(document.CreateXmlDeclaration("1.0", "UTF-8", null));
                        XmlElement newChild = document.CreateElement("configuration");
                        document.AppendChild(newChild);
                        document.Save(path);
                        return flag;
                    }
                    flag = true;
                }
            }
            catch (Exception exception)
            {
                throw exception;
            }
            return flag;
        }

        public static void Update(string xmlPath, string nodeName, string Key, string newValue)
        {
            try
            {
                lock (locker)
                {
                    string path = xmlPath;
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlNode documentElement = document.DocumentElement;
                        foreach (XmlNode node2 in documentElement.ChildNodes)
                        {
                            if (node2.Name == nodeName)
                            {
                                foreach (XmlNode node3 in node2)
                                {
                                    if (node3.Name == Key)
                                    {
                                        node3.InnerText = newValue;
                                    }
                                }
                            }
                        }
                        document.Save(path);
                    }
                    else
                    {
                        //MessageBox.Show("配置文件不存在");
                    }
                }
            }
            catch (Exception ex)
            {
                //CommonModules.Services.NLogService.Error($"{ex.Message}");
            }
        }

        public static void Update(string xmlPath, string nodeName, string Key, string Attributes, string newValue)
        {
            try
            {
                lock (locker)
                {
                    string path = xmlPath;
                    XmlDocument document = new XmlDocument();
                    if (File.Exists(path))
                    {
                        document.Load(path);
                        XmlNode documentElement = document.DocumentElement;
                        foreach (XmlNode node2 in documentElement.ChildNodes)
                        {
                            if (node2.Name == nodeName)
                            {
                                foreach (XmlNode node3 in node2)
                                {
                                    if (node3.Name == Key)
                                    {
                                        foreach (XmlAttribute attribute in node3.Attributes)
                                        {
                                            if (attribute.Name == Attributes)
                                            {
                                                attribute.InnerText = newValue;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        document.Save(path);
                    }
                    else
                    {
                        //MessageBox.Show("配置文件不存在");
                    }
                }
            }
            catch (Exception ex)
            {
                //CommonModules.Services.NLogService.Error($"{ex.Message}");
            }
        }
    }
}


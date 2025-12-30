using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml;

namespace SmarterMotion
{
    public class XXml : XObject
    {
        private static object locker = new object();
        public static int NewElement(string path, string urlParent, string nodeName, string innerText)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlNode parent = doc.SelectSingleNode(urlParent);
                XmlElement child = doc.CreateElement(nodeName);
                child.InnerText = innerText;
                parent.AppendChild(child);
                doc.Save(path);
                return 0;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex, "");
                return -1;
            }
        }

        public static int NewElement(string path, string urlParent, string[] nodeName, string[] innerText)
        {
            try
            {
                XmlDocument dom = new XmlDocument();
                dom.Load(path);
                XmlNode parent = dom.SelectSingleNode(urlParent);
                for (int i = 0; i < nodeName.Length; i++)
                {
                    XmlElement child = dom.CreateElement(nodeName[i]);
                    child.InnerText = innerText[i];
                    parent.AppendChild(child);
                }
                dom.Save(path);
                return 0;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex, "");
                return -1;
            }
        }

        public static int NewElement(string path, string nodeName, string Key, string Attributes, string newValue)
        {
            try
            {
                string text = path;
                XmlDocument xmlDocument = new XmlDocument();
                if (File.Exists(text))
                {
                    xmlDocument.Load(text);
                    XmlNode documentElement = xmlDocument.DocumentElement;
                    foreach (XmlNode childNode in documentElement.ChildNodes)
                    {
                        if (!(childNode.Name == nodeName))
                        {
                            continue;
                        }

                        foreach (XmlNode item in childNode)
                        {
                            if (!(item.Name == Key))
                            {
                                continue;
                            }

                            foreach (XmlAttribute attribute in item.Attributes)
                            {
                                if (attribute.Name == Attributes)
                                {
                                    attribute.InnerText = newValue;
                                }
                            }
                        }
                    }
                    xmlDocument.Save(text);
                    return 0;
                }
                else
                {
                    return -1;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex, "");
                return -1;
            }
        }

        public static bool FindChildInParent(string path, string urlParent, string child)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlNode parent = doc.SelectSingleNode(urlParent);
                XmlNodeList children = parent.ChildNodes;
                foreach (XmlNode node in children)
                {
                    if (node.Name == child)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex, "");
                return false;
            }
        }

        public static int ReadNode(string path, string urlParent, out string[] nodeName)
        {
            nodeName = null;
            try
            {
                XmlDocument dom = new XmlDocument();
                dom.Load(path);
                XmlNode parent = dom.SelectSingleNode(urlParent);
                XmlNodeList children = parent.ChildNodes;
                int count = children.Count;
                nodeName = new string[count];
                for (int i = 0; i < count; i++)
                {
                    nodeName[i] = children[i].Name;
                }
                return 0;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex, "");
                return -1;
            }
        }

        public static int ReadNodeAndInnerText(string path, string urlParent, out string[] nodeName, out string[] innerText)
        {
            nodeName = null;
            innerText = null;
            try
            {
                XmlDocument dom = new XmlDocument();
                dom.Load(path);
                XmlNode parent = dom.SelectSingleNode(urlParent);
                if (parent == null)
                {
                    return -1;
                }
                XmlNodeList children = parent.ChildNodes;
                int count = children.Count;
                nodeName = new string[count];
                innerText = new string[count];
                for (int i = 0; i < count; i++)
                {
                    nodeName[i] = children[i].Name;
                    innerText[i] = children[i].InnerText;
                }
                return 0;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex, "");
                return -1;
            }
        }

        public static string ReadAttributesAndInnerText(string path, string nodeName, string key, string Attributes)
        {
            try
            {
                lock (locker)
                {
                    string text = path;
                    XmlDocument xmlDocument = new XmlDocument();
                    if (File.Exists(text))
                    {
                        xmlDocument.Load(text);
                        XmlNode documentElement = xmlDocument.DocumentElement;
                        foreach (XmlNode childNode in documentElement.ChildNodes)
                        {
                            if (!(childNode.Name == nodeName))
                            {
                                continue;
                            }

                            foreach (XmlNode item in childNode)
                            {
                                if (!(item.Name == key))
                                {
                                    continue;
                                }

                                foreach (XmlAttribute attribute in item.Attributes)
                                {
                                    if (attribute.Name == Attributes)
                                    {
                                        return item.Attributes[Attributes].Value;
                                    }
                                }

                                return "";
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
                Trace.WriteLine(ex, "");
            }
            return null;
        }
        public static void UpdateAttributesInnerText(string path, string nodeName, string Key, string Attributes, string newValue)
        {
            try
            {
                lock (locker)
                {
                    string text = path;
                    XmlDocument xmlDocument = new XmlDocument();
                    if (File.Exists(text))
                    {
                        xmlDocument.Load(text);
                        XmlNode documentElement = xmlDocument.DocumentElement;
                        foreach (XmlNode childNode in documentElement.ChildNodes)
                        {
                            if (!(childNode.Name == nodeName))
                            {
                                continue;
                            }

                            foreach (XmlNode item in childNode)
                            {
                                if (!(item.Name == Key))
                                {
                                    continue;
                                }

                                foreach (XmlAttribute attribute in item.Attributes)
                                {
                                    if (attribute.Name == Attributes)
                                    {
                                        attribute.InnerText = newValue;
                                    }
                                }
                            }
                        }

                        xmlDocument.Save(text);
                    }
                    else
                    {
                        ////MessageBox.Show("配置文件不存在");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex, "");
            }
        }

        public static int UpdateInnerText(string path, string urlNode, string innerText)
        {
            try
            {
                XmlDocument dom = new XmlDocument();
                dom.Load(path);
                XmlNode node = dom.SelectSingleNode(urlNode);
                node.InnerText = innerText;
                dom.Save(path);
                return 0;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex, "");
                return -1;
            }
        }

        public static int UpdateNode(string path, string urlParent, string[] nodeName, string[] innerText)
        {
            try
            {
                XmlDocument dom = new XmlDocument();
                dom.Load(path);
                XmlNode parent = dom.SelectSingleNode(urlParent);
                parent.RemoveAll();
                for (int i = 0; i < nodeName.Length; i++)
                {
                    XmlElement xe = dom.CreateElement(nodeName[i]);
                    xe.InnerText = innerText[i];
                    parent.AppendChild(xe);
                }
                dom.Save(path);
                return 0;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex, "");
                return -1;
            }
        }

        public static int UpdateNode(string path, string urlParent, List<string> nodeName, List<string> innerText)
        {
            try
            {
                XmlDocument dom = new XmlDocument();
                dom.Load(path);
                XmlNode parent = dom.SelectSingleNode(urlParent);
                parent.RemoveAll();
                for (int i = 0; i < nodeName.Count; i++)
                {
                    XmlElement xe = dom.CreateElement(nodeName[i]);
                    xe.InnerText = innerText[i];
                    parent.AppendChild(xe);
                }
                dom.Save(path);
                return 0;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex, "");
                return -1;
            }
        }

        public static int DeleteElement(string path, string urlParent, string nodeName)
        {
            try
            {
                XmlDocument dom = new XmlDocument();
                dom.Load(path);
                XmlNode parent = dom.SelectSingleNode(urlParent);
                XmlNodeList children = parent.ChildNodes;
                foreach (XmlElement child in children)
                {
                    if (child.Name == nodeName)
                    {
                        parent.RemoveChild(child);
                    }
                }
                dom.Save(path);
                return 0;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex, "");
                return -1;
            }
        }

        public static int DeleteNodeAndInnerText(string path, string urlParent, string nodeName)
        {
            try
            {
                XmlDocument dom = new XmlDocument();
                dom.Load(path);
                XmlNode parent = dom.SelectSingleNode(urlParent);
                XmlNodeList children = parent.ChildNodes;
                foreach (XmlElement child in children)
                {
                    if (child.Name == nodeName)
                    {
                        child.RemoveAll();
                        parent.RemoveChild(child);
                    }
                }
                dom.Save(path);
                return 0;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex, "");
                return -1;
            }
        }

        public static int DeleteInnerText(string path, string urlParent, string nodeName)
        {
            try
            {
                XmlDocument dom = new XmlDocument();
                dom.Load(path);
                XmlNode parent = dom.SelectSingleNode(urlParent);
                XmlNodeList children = parent.ChildNodes;
                foreach (XmlElement child in children)
                {
                    if (child.Name == nodeName)
                    {
                        child.RemoveAll();
                    }
                }
                dom.Save(path);
                return 0;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex, "");
                return -1;
            }
        }
        public static void CreateXML(string xmlPath, string parent)
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
    }
}

using System;
using System.IO;
using System.Xml.Serialization;



namespace SmarterMotion
{

    public enum loginType
    {
        Op,
        Admin,
        Engineer,
        Tech,
        IPQC,
        None
    }


    [Serializable]
    public class LoginCtr
    {

        public static loginType loginTp = loginType.None;

        public static string Xml;

        public LoginCtr()
        {


        }

        //public string Xml
        //{

        //    get { return "D:\\Config\\Setting\\Login.xml"; }
        //}

        public string AdminUser { get; set; }
        public string AdminPwd { get; set; }

        public string EngineerUser { get; set; }
        public string EngineerPwd { get; set; }

        public string TecUser { get; set; }
        public string TecPwd { get; set; }

        public string opUser { get; set; }
        public string opPwd { get; set; }

        public string IPQCUser { get; set; }
        public string IPQCPwd { get; set; }

        public string BuildDate { get; set; }
        public string HWVision { get; set; }
        public string SWUpdate { get; set; }



        public bool Save()
        {
            try
            {
                if (!File.Exists(Xml))
                {
                    AdminUser = "Admin";
                    AdminPwd = "admin";

                    EngineerUser = "Engineer";
                    EngineerPwd = "engineer";

                    TecUser = "Tech";
                    TecPwd = "tech";

                    opUser = "OP";
                    opPwd = "op";

                    IPQCUser = "IPQC";
                    IPQCPwd = "ipqc";

                    HWVision = "VJ1.0.1";
                    BuildDate = "2022.01.05";
                    SWUpdate = "2022.06.06";

                }
                XmlSerializer xs = new XmlSerializer(typeof(LoginCtr));
                Stream stream = new FileStream(Xml, FileMode.Create, FileAccess.Write, FileShare.Read);
                xs.Serialize(stream, this);
                stream.Close();
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public LoginCtr Load()
        {
            try
            {
                if (File.Exists(Xml) == false)
                {
                    Save();
                }
                XmlSerializer xs = new XmlSerializer(typeof(LoginCtr));
                Stream stream = new FileStream(Xml, FileMode.Open, FileAccess.Read, FileShare.Read);
                var ret = xs.Deserialize(stream) as LoginCtr;
                stream.Close();
                if (ret.IPQCPwd == null)
                {
                    File.Delete(Xml);
                    Load();
                }
                return ret;
            }
            catch
            {
                //MessageBox.Show("加载文件失败：" + Xml);
                return this;
            }
        }
    }
}




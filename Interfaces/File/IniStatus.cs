using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    public class IniStatus
    {
        public static IniStatus Instance = new IniStatus();
        string appStartupPath = System.IO.Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
        private string curProduct;
        private string lastProduct;

        #region 事件
        public bool HasChanged { get; set; }

        public event EventHandler<EventArgs> StatusChangeEvent;
        private void OnEvent()
        {
            if (StatusChangeEvent != null)
                StatusChangeEvent(this, null);
        }
        #endregion

        /// <summary>
        /// 当前产品
        /// </summary>
        public string CurProduct
        {
            get
            {
                return curProduct;
            }
            set
            {
                if (value != curProduct)
                {
                    curProduct = value;
                    HasChanged = true;
                    OnEvent();
                }
            }
        }
        /// <summary>
        /// 上个产品
        /// </summary>
        public string LastProduct
        {
            get
            {
                return lastProduct;
            }
            set
            {
                if (value != lastProduct)
                {
                    lastProduct = value;
                }
            }
        }

        public void ReadINI()
        {
            string filePath = appStartupPath + "\\" + "config.ini";
            if (System.IO.File.Exists(filePath))
            {
                CurProduct = INIOperation.GetValue(filePath, "setting", "CurProduct", null);
                LastProduct = INIOperation.GetValue(filePath, "setting", "LastProduct", null);
            }
        }

        public void SaveINI()
        {
            string filePath = appStartupPath + "\\" + "config.ini";
            INIOperation.WriteValue(filePath, "setting", "CurProduct", CurProduct);
            INIOperation.WriteValue(filePath, "setting", "LastProduct", LastProduct);
        }


    }
}

using Core.Services;
using Interfaces;
using Prism.Mvvm;
using SmarterMotion;
using Stations;
using System;
using System.IO;
using System.Linq;

namespace ModuleCore.Models
{
    public class RecipeManagerModel : BindableBase
    {
        private readonly string baseDir = Path.Combine(
                                AppDomain.CurrentDomain.BaseDirectory);
        TaskInstanceManager _taskManager;
        AppConfig _appConfig;
        public RecipeManagerModel(TaskInstanceManager taskManager, AppConfig appConfig)
        {
            _taskManager = taskManager;
            _appConfig = appConfig;
        }

        /// <summary>
        /// 配置所有Task参数
        /// </summary>
        public void SetupTParams()
        {
            // TODO: 配置所有Task参数
            var allTasks = _taskManager.GetAllTasks().ToList();
            foreach (var task in allTasks)
            {
                switch (task)
                {
                    //case Task1 t1:
                    //    break;
                    //case Task2 t2:

                    //    break;
                    //case Task3 t3:

                    //    break;
                    //case Task4 t4:
                    default:
                        break;
                }
            }
        }
        /// <summary>
        /// 切换Task配方
        /// </summary>
        public void ChangeTRecipe()
        {
            // TODO: 切换配方
            var allTasks = _taskManager.GetAllTasks().ToList();
            foreach (var task in allTasks)
            {
                switch (task)
                {
                    //case Task1 t1:
                    //    var rt1 = new Task1Parameters();
                    //    rt1.Product = _appConfig.Name;
                    //    rt1.LastProduct = _appConfig.LastRecipeName;
                    //    rt1 = (Task1Parameters)rt1.LoadSettings();
                    //    t1.Parameters = (Task1Parameters)rt1;
                    //    break;
                    //case Task2 t2:
                    //    var rt2 = new Task2Parameters();
                    //    rt2.Product = _appConfig.Name;
                    //    rt2.LastProduct = _appConfig.LastRecipeName;
                    //    rt2 = (Task2Parameters)rt2.LoadSettings();
                    //    t2.Parameters = (Task2Parameters)rt2;
                    //    break;
                    //case Task3 t3:
                    //    var rt3 = new Task3Parameters();
                    //    rt3.Product = _appConfig.Name;
                    //    rt3.LastProduct = _appConfig.LastRecipeName;
                    //    rt3 = (Task3Parameters)rt3.LoadSettings();
                    //    t3.Parameters = (Task3Parameters)rt3;
                    //    break;
                    //case Task4 t4:
                    //    var rt4 = new Task4Parameters();
                    //    rt4.Product = _appConfig.Name;
                    //    rt4.LastProduct = _appConfig.LastRecipeName;
                    //    rt4 = (Task4Parameters)rt4.LoadSettings();
                    //    t4.Parameters = (Task4Parameters)rt4;
                    //    break;
                    //case Task5 t5:
                    //    var rt5 = new Task5Parameters();
                    //    rt5.Product = _appConfig.Name;
                    //    rt5.LastProduct = _appConfig.LastRecipeName;
                    //    rt5 = (Task5Parameters)rt5.LoadSettings();
                    //    t5.Parameters = (Task5Parameters)rt5;
                    //    break;
                    //case Task6 t6:
                    //    var rt6 = new Task6Parameters();
                    //    rt6.Product = _appConfig.Name;
                    //    rt6.LastProduct = _appConfig.LastRecipeName;
                    //    rt6 = (Task6Parameters)rt6.LoadSettings();
                    //    t6.Parameters = (Task6Parameters)rt6;
                    //    break;
                    default:
                        break;
                }
            }
        }


        /// <summary>
        /// 重新配置Position参数
        /// </summary>
        public void ResetBindPositionParams()
        {
            // TODO: 重新配置Position参数
            for (int i = 0; i < XTaskManager.Instance.Tasks.Count; i++)
            {
                XPositionManager.Instance.UnBindPositionTable(i + 1);
            }
            XPositionManager.Instance.SetPositionXmlPathAndRoot(baseDir + @"Config\Position\",
               _appConfig.Name + " Position.xml", _appConfig.Name + " PositionName.xml", "XSetting", baseDir + @"Config\BackUp\");

            for (int i = 0; i < XTaskManager.Instance.Tasks.Count; i++)
            {
                XPositionManager.Instance.BindPositionTableByTaskId(i + 1);
                XPositionManager.Instance.LoadPositionSet();
            }
        }

        #region 参数文件复制和删除
        ///<summary>
        ///删除Task参数xml文件
        ///</summary>
        public void DeleteTaskParameterFile(string sourceFilePath)
        {
            try
            {
                string sourceDirectory = baseDir + @"Config\MotionParameter\" + sourceFilePath;
                if (Directory.Exists(sourceDirectory))
                {
                    Directory.Delete(sourceDirectory, true);
                }
            }
            catch (Exception ex)
            {
       
            }
        }
        /// <summary>
        /// 复制Task参数xml文件到目标路径
        /// </summary>
        public void CopyTaskParameterFileToDestPath(string oldName, string newProductName)
        {
            try
            {
                string sourceDirectory = baseDir + @"Config\MotionParameter\" + oldName;
                string destDirectory = baseDir + @"Config\MotionParameter\";
                CopyFileHelper.CopyAndRenameProductConfig(destDirectory, oldName, newProductName);
            }
            catch (Exception ex)
            {
              
            }
        }
        /// <summary>
        /// 删除Position参数xml文件
        /// </summary>
        public void DeletePositionFile(string sourceFileName)
        {
            try
            {
                string destPath = Path.Combine(baseDir, "Config", "Position");
                // 安全过滤非法字符
                var safeFileName = Path.GetFileName(sourceFileName);
                // 获取匹配文件（不区分大小写）
                var files = Directory.GetFiles(destPath, $"*{safeFileName}*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    System.IO.File.Delete(file);
                }
            }
            catch (Exception ex)
            {
               
            }
        }
        /// <summary>
        /// 复制位置参数xml文件
        /// </summary>
        public void CopyPositionFileToDestPath(string oldName, string newProductName)
        {
            try
            {
                string sourceXml = baseDir + @"Config\Position\" + oldName + " " + "Position.xml";
                string sourceXmlName = baseDir + @"Config\Position\" + oldName + " " + "PositionName.xml";

                string destPath = baseDir + @"Config\Position";
                string destXmlFile = newProductName + " " + "Position.xml";
                string destXmlNameFile = newProductName + " " + "PositionName.xml";

                if (!File.Exists(destPath + "\\" + destXmlFile))
                {
                    //直接复制Position.xml
                    CopyFileHelper.CopyConfigFile(sourceXml, destPath, destXmlFile);
                }

                if (!File.Exists(destPath + "\\" + destXmlNameFile))
                {
                    //直接复制PositionName.xml
                    CopyFileHelper.CopyConfigFile(sourceXmlName, destPath, destXmlNameFile);
                }
            }
            catch (Exception ex)
            {

            }
        }
        #endregion

    }
}

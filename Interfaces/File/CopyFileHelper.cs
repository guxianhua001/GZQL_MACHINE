using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Interfaces
{
    public static class CopyFileHelper
    {
        public static void CopyAndRenameProductConfig(string targetDirectory,string sourceProductName,string newProductName)
        {
            // 验证输入
            if (string.IsNullOrWhiteSpace(newProductName))
                throw new ArgumentException("产品名称不能为空");

            // 清理非法字符
            newProductName = SanitizeFileName(newProductName);

            // 构建完整路径
            var sourcePath = Path.Combine(targetDirectory, sourceProductName);
            var destPath = Path.Combine(targetDirectory, newProductName);

            try
            {
                // 确保源目录存在
                if (!Directory.Exists(sourcePath))
                    throw new DirectoryNotFoundException($"源目录不存在: {sourcePath}");

                // 创建目标目录
                Directory.CreateDirectory(destPath);

                // 获取所有需要处理的文件
                var files = Directory.GetFiles(sourcePath, "*.xml");

                foreach (var file in files)
                {
                    ProcessFile(sourceProductName, file, destPath, newProductName);
                }

                Console.WriteLine($"成功复制并重命名{files.Length}个配置文件");
            }
            catch (Exception ex)
            {
                throw new ApplicationException("配置文件复制失败，请检查日志", ex);
            }
        }

        private static void ProcessFile(string sourceProductName,string sourceFile, string destPath, string newName)
        {
            var fileName = Path.GetFileName(sourceFile);

            // 使用正则表达式匹配文件名模式
            var pattern = $@"^{Regex.Escape(sourceProductName)}\s+(Task\d+Parameters?)\.xml$";
            var match = Regex.Match(fileName, pattern, RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                Console.WriteLine($"跳过非标准文件: {fileName}");
                return;
            }

            // 构建新文件名
            var taskPart = match.Groups[1].Value;
            var newFileName = $"{newName} {taskPart}.xml";
            var destFile = Path.Combine(destPath, newFileName);

            try
            {
                // 复制文件并保留原始创建时间
                File.Copy(sourceFile, destFile, overwrite: false);
                File.SetCreationTime(destFile, File.GetCreationTime(sourceFile));

                Console.WriteLine($"已创建: {newFileName}");
            }
            catch (IOException ex) when (ex.Message.Contains("already exists"))
            {
                //LogWarning($"文件已存在，跳过覆盖: {destFile}");
            }
        }

        private static string SanitizeFileName(string name)
        {
            // 移除非法字符
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", name.Split(invalidChars));
        }

        /// <summary>
        /// 复制文件到新的文件夹并重命名
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="destinationPath"></param>
        /// <param name="destinationFileName"></param>
        /// <returns></returns>
        public static bool CopyConfigFile(string sourceFile, string destinationPath, string destinationFileName)
        {
            FileInfo tempFileInfo;
            FileInfo tempBakFileInfo;
            DirectoryInfo tempDirectoryInfo;

            tempFileInfo = new FileInfo(sourceFile);
            tempDirectoryInfo = new DirectoryInfo(destinationPath);
            tempBakFileInfo = new FileInfo(destinationPath + "\\" + destinationFileName);
            try
            {
                tempFileInfo.CopyTo(destinationPath + "\\" + destinationFileName);
            }
            catch (Exception ex)
            {
                return false;
            }

            return true;
        }

    }
}

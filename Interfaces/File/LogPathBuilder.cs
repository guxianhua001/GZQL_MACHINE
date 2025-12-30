using Interfaces.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    public static class LogPathBuilder
    {
        public enum RecordType
        {
            Torque,
            Dial
        }
        public static string BuildTorqueRecordPath(
            string moduleName,
            string baseName,
            int index,
            string carrierBarcode,
            string defaultDataPath = null)
        {
            defaultDataPath ??= DeviceConfigService.CurrentDataSavePath;
            return BuildRecordPath(RecordType.Torque, moduleName, baseName, index, carrierBarcode, defaultDataPath);
        }
        public static string BuildDialRecordPath(
            string moduleName,
            string baseName,
            string carrierBarcode,
            string defaultDataPath = null)
        {
            defaultDataPath ??= DeviceConfigService.CurrentDataSavePath;
            return BuildRecordPath(RecordType.Dial, moduleName, baseName, pointIndex: 0, carrierBarcode, defaultDataPath);
        }
        private static string BuildRecordPath(
            RecordType recordType,
            string moduleName,
            string baseName,
            int pointIndex,
            string carrierBarcode,
            string defaultDataPath = null)
        {
            // 1. 日期目录
            string dateStr = DateTime.Now.ToString("yyyyMMdd");
            string timeStr = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // 2. 安全处理空值
            carrierBarcode = string.IsNullOrWhiteSpace(carrierBarcode)
                ? "Unknown"
                : SanitizeFileName(carrierBarcode);

            baseName = string.IsNullOrWhiteSpace(baseName)
                ? "Unknown"
                : SanitizeFileName(baseName);
            // 3. 根据记录类型构建路径
            string fileName;
            string pathTemplate;
            switch (recordType)
            {
                case RecordType.Torque:
                    fileName = $"TorquePositionRecords_{moduleName}_{pointIndex}_{timeStr}_{baseName}.csv";
                    pathTemplate = Path.Combine(
                        defaultDataPath,
                        "TorquePositionRecords",
                        dateStr,
                        carrierBarcode,     // 托盘码目录
                        baseName,           // 物料码目录
                        $"Index_{pointIndex}"); // 点位索引目录
                    break;
                // 构建格式：根目录\年\月\日\工位_时间_托盘码_产品码.csv
                case RecordType.Dial:
                    fileName = $"DialRecords_{moduleName}_{timeStr}_{baseName}.csv";
                    pathTemplate = Path.Combine(
                        defaultDataPath,
                        "DialRecords",
                        dateStr,
                        carrierBarcode);    // 托盘码目录
                    break;

                default:
                    throw new ArgumentException("无效的记录类型");
            }
            return Path.Combine(pathTemplate, fileName);
        }
        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Unknown";
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Concat(name.Split(invalidChars));
        }

        public static string CreateDirectoryForFile(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(dir);
            return filePath;
        }
    }

}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Interfaces.Services
{
    public class LogDialRecordService
    {
        public static void LogDialRecord(PointViewModel point)
        {
            try
            {
                var log = new StringBuilder()
                .AppendLine($"【拨针记录】序号：{point.Index}")
                .AppendLine($"操作时间：{point.OperationTime:yyyy-MM-dd HH:mm:ss.fff}")
                .AppendLine("负向拨针：")
                .AppendLine($"  寻针位置：{point.NegativeRecord.SearchPosition:F3}mm")
                .AppendLine($"  接触力：{point.NegativeRecord.HomeDialForce:F2}N")
                .AppendLine($"  位移量：{point.NegativeRecord.HomeDisplacement:F3}mm")
                .AppendLine($"  拨针力：{point.NegativeRecord.DialForce:F2}N")
                .AppendLine($"  位移量：{point.NegativeRecord.DialDisplacement:F3}mm")
                .AppendLine("正向拨针：")
                .AppendLine($"  寻针位置：{point.PositiveRecord.SearchPosition:F3}mm")
                .AppendLine($"  接触力：{point.PositiveRecord.HomeDialForce:F2}N")
                .AppendLine($"  位移量：{point.PositiveRecord.HomeDisplacement:F3}mm")
                .AppendLine($"  拨针力：{point.PositiveRecord.DialForce:F2}N")
                .AppendLine($"  位移量：{point.PositiveRecord.DialDisplacement:F3}mm")
                .AppendLine($"  拨针高度：{point.NegativeRecord.DialHeight:F3}mm")
                .AppendLine($"  拨针次数：{point.NegativeRecord.DialCount}")
                .AppendLine($"总体结果：{point.IsOk switch
                {
                    true => "成功",
                    false => "失败",
                    _ => "未检测" // 处理null的情况
                }}");

                IMessage.Logger.Info(log.ToString());
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error(ex, "拨针记录日志写入失败");
                return;
            }
        }
        public static void ExportDialRecords(
             ObservableCollection<PointViewModel> pinPoints,
             string moduleName,
             string baseName,
             string carrierBarcode)
        {
            if (pinPoints == null || !pinPoints.Any())
            {
                IMessage.Logger.Warn("导出拨针记录失败：点列表为空");
                return;
            }
            try
            {
                // 1. 确定文件夹分类 (OK/NG)
                var resultFolder = pinPoints.All(p => (bool)p.IsOk) ? "OK" : "NG";
                IMessage.Logger.Info($"检测到 {pinPoints.Count} 个点位，状态: {(resultFolder == "OK" ? "全部通过" : "存在失败")}");

                // 2. 获取基础路径并添加结果状态文件夹
                string originalPath = LogPathBuilder.BuildDialRecordPath(
                    moduleName,
                    baseName,
                    carrierBarcode);

                // 在文件路径中插入结果状态文件夹
                string filePath = InsertResultFolderToPath(originalPath, resultFolder);

                // 3. 创建所需目录
                LogPathBuilder.CreateDirectoryForFile(filePath);

                // 4. 构建CSV内容
                var csv = new StringBuilder();
                // 添加表头
                csv.AppendLine("序号,操作时间,方向,寻针位置(mm),接触力(N),寻针位移量(mm),寻针目标位置(mm),寻针实际位置(mm),拨针力(N),位移量(mm),目标位置(mm),实际位置(mm),拨针高度(mm),拨针次数,结果");
                // 5. 添加所有点位的记录
                foreach (var point in pinPoints)
                {
                    AddRecordToCsv(csv, point, point.NegativeRecord, "负向");
                    AddRecordToCsv(csv, point, point.PositiveRecord, "正向");
                }
                // 6. 写入文件
                File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

                IMessage.Logger.Info($"拨针记录导出成功: {filePath}");
                // 7. 添加额外的成功标记文件 (可选)
                if (resultFolder == "OK")
                {
                    try
                    {
                        string flagFile = Path.Combine(Path.GetDirectoryName(filePath), "SUCCESS.flg");
                        File.WriteAllText(flagFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - All points passed");
                        IMessage.Logger.Info($"生成成功标记文件: {flagFile}");
                    }
                    catch (Exception ex)
                    {
                        IMessage.Logger.Warn($"生成成功标记文件失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error(ex, "拨针记录导出失败");
                return;
            }
        }
        /// <summary>
        /// 在文件路径中插入结果状态文件夹
        /// </summary>
        private static string InsertResultFolderToPath(string originalPath, string resultFolder)
        {
            // 示例原始路径: E:\Logs\2024\05\19\T1_abc123.csv
            // 处理后路径: E:\Logs\2024\05\19\OK\T1_abc123.csv

            try
            {
                var directory = Path.GetDirectoryName(originalPath);
                var fileName = Path.GetFileName(originalPath);

                // 添加结果状态文件夹
                return Path.Combine(directory, resultFolder, fileName);
            }
            catch (Exception ex)
            {
                IMessage.Logger.Warn($"路径处理失败: {ex.Message}. 使用原始路径: {originalPath}");
                return originalPath;
            }
        }
        private static void AddRecordToCsv(StringBuilder csv, PointViewModel point, DialRecord record, string direction)
        {
            csv.AppendLine(string.Join(",",
                point.Index,
                point.OperationTime.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                direction,
                record.SearchPosition.ToString("F3"),
                record.HomeDialForce.ToString("F2"),
                record.HomeDisplacement.ToString("F3"),
                record.HomeTargetPosition.ToString("F3"),
                record.HomeActualPosition.ToString("F3"),
                record.DialForce.ToString("F2"),
                record.DialDisplacement.ToString("F3"),
                record.TargetPosition.ToString("F3"),
                record.ActualPosition.ToString("F3"),
                record.DialHeight.ToString("F3"),
                record.DialCount,
                record.IsSuccess ? "成功" : "失败"
            ));
        }

        public static void ExportToCsv(double[] a0, int calnum, string filePath)
        {
            // 创建CSV内容
            var csvContent = new StringBuilder();
            csvContent.AppendLine("序号,转换值(N)"); // 表头
            for (int i = 0; i < calnum; i++)
            {
                csvContent.AppendLine($"{i + 1},{a0[i]:F3}");
            }
            // 写入文件（自动处理文件编码和换行符）
            File.WriteAllText(filePath, csvContent.ToString(), Encoding.UTF8);
        }
    }
}

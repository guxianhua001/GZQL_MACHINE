using Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HSMS
{
    [Serializable]
    public class CSVoperater : IDisposable
    {
        private const string DEFAULT_HEADER_FILE = "header.csv";
        private Encoding FILE_ENCODING = Encoding.UTF8;
        private bool _disposed = false;

        public string FolderPath { get; set; }
        public string FilePath { get; set; }
        public string HeadPath { get; set; } = Path.Combine(Environment.CurrentDirectory, DEFAULT_HEADER_FILE);
        public CSVoperater(string headPath, string filePath)
        {
            HeadPath = headPath;
            FilePath = filePath;
        }
        public CSVoperater(string filePath) : this(Path.Combine(Environment.CurrentDirectory, DEFAULT_HEADER_FILE), filePath)
        {
        }
        public CSVoperater() : this(null, null)
        {
        }

        /// <summary>
        /// 添加行数据到CSV文件
        /// </summary>
        public void AddRow(params string[] rowData)
        {
            if (string.IsNullOrEmpty(FilePath))
                throw new InvalidOperationException("文件路径未初始化");

            WriteCsvRecord(FilePath, rowData);
        }
        /// <summary>
        /// 添加表头到CSV文件
        /// </summary>
        public void AddHeader(params string[] headers)
        {
            if (string.IsNullOrEmpty(FilePath))
                throw new InvalidOperationException("文件路径未初始化");

            // 确保文件存在（如果不存在则创建）
            InitializeCsvFile();

            // 写入表头
            WriteCsvRecord(FilePath, headers);
        }
        /// <summary>
        /// 向指定路径添加行数据
        /// </summary>
        public void AddRowTo(string filePath, params string[] rowData)
        {
            WriteCsvRecord(filePath, rowData);
        }
        /// <summary>
        /// 向指定路径添加表头
        /// </summary>
        public void AddHeaderTo(string filePath, params string[] headers)
        {
            EnsureFileExists(filePath);
            WriteCsvRecord(filePath, headers);
        }
        /// <summary>
        /// 添加单行数据到CSV文件
        /// </summary>
        public void AddData(string[] recordData, string filePath)
        {
            if (recordData == null) throw new ArgumentNullException(nameof(recordData));
            WriteCsvRecord(filePath, recordData);
        }
        /// <summary>
        /// 添加多行数据到CSV文件
        /// </summary>
        public void AddData(IEnumerable<string[]> allData, string filePath)
        {
            if (allData == null) throw new ArgumentNullException(nameof(allData));

            try
            {
                EnsureFileExists(filePath);

                using (var writer = new StreamWriter(filePath, true, Encoding.UTF8))
                {
                    foreach (var record in allData)
                    {
                        writer.WriteLine(string.Join(",", record.Select(EscapeCsvField)));
                    }
                }
            }
            catch (Exception ex)
            {
                HandleFileError($"批量写入CSV数据失败: {filePath}", ex);
            }
        }
        /// <summary>
        /// 写入CSV记录行
        /// </summary>
        private void WriteCsvRecord(string filePath, string[] recordData)
        {
            try
            {
                EnsureFileExists(filePath);

                using (var writer = new StreamWriter(filePath, true, FILE_ENCODING))
                {
                    writer.WriteLine(string.Join(",", recordData.Select(EscapeCsvField)));
                }
            }
            catch (Exception ex)
            {
                HandleFileError($"写入CSV记录失败: {filePath}", ex);
            }
        }

        /// <summary>
        /// 转义CSV字段中的特殊字符
        /// </summary>
        private static string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "\"\"";

            return field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r")
                ? $"\"{field.Replace("\"", "\"\"")}\""
                : field;
        }
        // ===== 文件操作优化 =====

        /// <summary>
        /// 安全创建目标文件
        /// </summary>
        public void CreateNewFile(string filePath)
        {
            try
            {
                // 确保目录存在
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // 如果文件已存在，则删除
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // 创建新文件
                File.Create(filePath).Close();
            }
            catch (Exception ex)
            {
                throw new IOException($"无法创建文件: {filePath}", ex);
            }
        }
        /// <summary>
        /// 确保CSV文件存在
        /// </summary>
        private void InitializeCsvFile()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            try
            {
                if (File.Exists(FilePath)) return;

                File.Create(FilePath).Close();
                IMessage.Logger?.Info($"创建CSV文件: {FilePath}");
            }
            catch (Exception ex)
            {
                HandleFileError("初始化CSV文件失败", ex);
            }
        }

        /// <summary>
        /// 确保指定文件存在
        /// </summary>
        private void EnsureFileExists(string filePath)
        {
            try
            {
                if (File.Exists(filePath)) return;

                // 创建目录结构
                var dir = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // 创建空文件
                using (File.Create(filePath)) { }
            }
            catch (Exception ex)
            {
                HandleFileError($"创建文件失败: {filePath}", ex);
            }
        }

        public bool IsFileOpen()
        {
            return !string.IsNullOrEmpty(FilePath) && IsFileOpen(FilePath);
        }
        public bool IsFileOpen(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                return true;
            }
            catch (Exception ex)
            {
                HandleFileError($"检查文件状态失败: {path}", ex);
                return true;
            }
        }
        public string[][] ReadCsvFile()
        {
            return !string.IsNullOrEmpty(FilePath)
                ? ReadCsvFile(FilePath)
                : throw new InvalidOperationException("文件路径未初始化");
        }
        public string[][] ReadCsvFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    HandleFileError("CSV文件不存在", null);
                    return Array.Empty<string[]>();
                }
                var lines = new List<string[]>();
                using (var reader = new StreamReader(path, Encoding.UTF8))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine()?.TrimEnd(',');
                        if (!string.IsNullOrEmpty(line))
                        {
                            lines.Add(ParseCsvLine(line));
                        }
                    }
                }
                return lines.ToArray();
            }
            catch (Exception ex)
            {
                HandleFileError($"读取CSV文件失败: {path}", ex);
                return Array.Empty<string[]>();
            }
        }

        private static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++; // 跳过下一个引号
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            fields.Add(currentField.ToString());
            return fields.ToArray();
        }

        public void AddData(double[] data)
        {
            if (string.IsNullOrEmpty(FilePath))
                throw new InvalidOperationException("文件路径未初始化");

            using (var writer = new StreamWriter(FilePath, true, Encoding.UTF8))
            {
                foreach (var value in data)
                {
                    writer.WriteLine($"{value},");
                }
                for (int i = 0; i < 3; i++)
                {
                    writer.WriteLine();
                }
            }
        }

        /// <summary>
        /// 添加行数据到指定CSV文件
        /// </summary>
        public void AddRow(string[] rowData, string filePath)
        {
            if (rowData == null) throw new ArgumentNullException(nameof(rowData));
            WriteCsvRecord(filePath, rowData);
        }
        /// <summary>
        /// 添加表头到指定CSV文件
        /// </summary>
        public void AddHeader(string[] headers, string filePath)
        {
            if (headers == null) throw new ArgumentNullException(nameof(headers));

            // 确保文件存在
            EnsureFileExists(filePath);

            // 检查是否已经存在表头
            var existingData = ReadCsvFile(filePath);
            if (existingData.Length > 0)
            {
                HandleFileError($"文件 [{filePath}] 已存在表头", null);
                return;
            }

            // 写入表头
            WriteCsvRecord(filePath, headers);
        }

        // ===== 错误处理统一 =====
        private void HandleFileError(string message, Exception ex)
        {
            Debug.WriteLine($"{message}: {ex?.Message}");
            IMessage.Logger?.Error($"{message}{(ex != null ? $": {ex.Message}" : "")}");
        }
        // ===== IDisposable 实现 =====
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // 释放托管资源
            }

            // 尝试关闭可能被Excel占用的文件
            TryReleaseFileLock();

            _disposed = true;
        }
        private void TryReleaseFileLock()
        {
            if (!string.IsNullOrEmpty(FilePath) && IsFileOpen(FilePath))
            {
                try
                {
                    foreach (var process in Process.GetProcessesByName("EXCEL"))
                    {
                        process.Kill();
                    }
                }
                catch (Exception ex)
                {
                    HandleFileError("关闭Excel进程失败", ex);
                }
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        ~CSVoperater()
        {
            Dispose(false);
        }
    }
}


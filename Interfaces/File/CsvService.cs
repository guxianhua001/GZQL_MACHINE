using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    // ICsvService.cs
    public interface ICsvParserService
    {
        List<DialRecord> ParseCsv(string filePath);

        List<TorquePositionRecord> ParseTorqueData(string filePath);
    }
    // CsvService.cs
    public class CsvService : ICsvParserService
    {
        public List<DialRecord> ParseCsv(string filePath)
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<DialRecord>().ToList();
        }
        public List<TorquePositionRecord> ParseTorqueData(string filePath)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,           // 明确指定有标题行
                MissingFieldFound = null,         // 忽略缺失字段
                HeaderValidated = null,           // 跳过标题验证
                PrepareHeaderForMatch = args => args.Header.ToLower()
            };

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);

            csv.Context.RegisterClassMap<TorquePositionRecordMap>();  // 注册自定义映射

            // 添加日期类型转换器
            csv.Context.TypeConverterOptionsCache.GetOptions<DateTime>().Formats
                = new[] { "HH:mm:ss.fff" };

            return csv.GetRecords<TorquePositionRecord>().ToList();
        }
    }

}

using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

    // 添加CsvHelper的类映射配置
    public sealed class TorquePositionRecordMap : ClassMap<TorquePositionRecord>
    {
        public TorquePositionRecordMap()
        {
            Map(m => m.Timestamp)
                .Index(0)
                .TypeConverterOption.Format("HH:mm:ss.fff")
                .TypeConverterOption.DateTimeStyles(DateTimeStyles.AssumeUniversal);

            Map(m => m.Torque).Index(1);
            Map(m => m.Position).Index(2);
        }
    }

    public class TorquePositionRecord
    {
        public DateTime Timestamp { get; set; }
        public double Torque { get; set; }
        public double Position { get; set; }
    }

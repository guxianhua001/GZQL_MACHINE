using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    public class PersistentAlarm
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }   // 主键

        [Required]
        [Column(TypeName = "datetime2(4)")] // 精确到100纳秒
        public DateTime Timestamp { get; set; }

        [Range(1, 9999)]
        public int StationId { get; set; }

        [StringLength(20)]
        public string Code { get; set; }

        public AlarmLevel Level { get; set; } = AlarmLevel.Normal;

        [MaxLength(50)]
        public string Category { get; set; }

        [MaxLength(200)]
        public string Description { get; set; }

        // ISO 8601格式化存储
        public string OriginalRawTime { get; set; }

        // 故障持续时间（可选）
        public TimeSpan? Duration { get; set; }
    }

    public enum AlarmLevel
    {
        Normal = 1,
        Severe = 2,
        Critical = 3
    }

}

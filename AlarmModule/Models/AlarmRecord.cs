using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AlarmModule.Models
{
    /// <summary>
    /// 报警记录实体：包含完整的报警生命周期数据
    /// 支持工业4级分类、确认/复位流程、阈值记录、防抖抑制
    /// </summary>
    [Table("AlarmRecords")]
    public class AlarmRecord
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required]
        public DateTime AlarmTime { get; set; } = DateTime.Now;

        [Required]
        public AlarmLevel AlarmLevel { get; set; } = AlarmLevel.General;

        [Required]
        [StringLength(50)]
        public string AlarmCode { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string AlarmSource { get; set; } = string.Empty;

        [Required]
        public AlarmType AlarmType { get; set; } = AlarmType.HardwareFault;

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public double? TriggerValue { get; set; }

        public double? ThresholdValue { get; set; }

        [Required]
        public AlarmStatus Status { get; set; } = AlarmStatus.Unconfirmed;

        [StringLength(50)]
        public string? ConfirmedBy { get; set; }

        public DateTime? ConfirmedTime { get; set; }

        [StringLength(50)]
        public string? ResetBy { get; set; }

        public DateTime? ResetTime { get; set; }

        [StringLength(1000)]
        public string? ProcessingNotes { get; set; }

        public DateTime? SuppressedUntil { get; set; }
    }
}

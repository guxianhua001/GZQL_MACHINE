using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace AlarmModule.Models
{
    /// <summary>
    /// 报警阈值配置：允许用户在不修改代码的情况下设置报警阈值
    /// </summary>
    [Table("AlarmThresholdConfigs")]
    public class AlarmThresholdConfig : INotifyPropertyChanged
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string AlarmCode { get; set; } = string.Empty;

        [StringLength(100)]
        public string? AlarmSource { get; set; }

        public double ThresholdValue { get; set; }

        public AlarmLevel AlarmLevel { get; set; } = AlarmLevel.General;

        public AlarmType AlarmType { get; set; } = AlarmType.ParameterOutOfLimit;

        /// <summary>
        /// 防抖时间窗口（秒）：相同Code+Source在此时间内不重复触发
        /// </summary>
        public int SuppressionWindowSeconds { get; set; } = 60;

        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// UI选中状态（不映射到数据库）：用于批量操作的复选框绑定
        /// </summary>
        [NotMapped]
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }
        private bool _isSelected;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleCore.Views
{
    // ThresholdWarningEvent.cs (Prism 事件)
    public class ThresholdWarningNotification : BindableBase
    {
        public string NeedleId { get; set; }

        private int _currentUsage;
        public int CurrentUsage
        {
            get => _currentUsage;
            set => SetProperty(ref _currentUsage, value);
        }

        private int _maxUsage;
        public int MaxUsage
        {
            get => _maxUsage;
            set => SetProperty(ref _maxUsage, value);
        }

        public DateTime TriggerTime { get; set; } = DateTime.Now;

        private double _usagePercentage;
        public double UsagePercentage
        {
            get => _usagePercentage;
            set
            {
                _usagePercentage = (double)CurrentUsage / MaxUsage;
                SetProperty(ref _usagePercentage, value);
            }
        }

        private bool _isBlocked;
        public bool IsBlocked
        {
            get => _isBlocked;
            set
            {
                if (SetProperty(ref _isBlocked, value))
                {
                    // 关键点：属性变化时触发通知
                    RaisePropertyChanged(nameof(LevelText));
                    RaisePropertyChanged(nameof(FormattedMessage));
                }
            }
        }

        public string LevelText => IsBlocked ? "阻断状态" : "预警状态";

        public string FormattedMessage =>
            IsBlocked
                ? $"针头{NeedleId} 已达到最大使用次数! 请立即更换!"
                : $"针头{NeedleId} 使用次数接近最大值 (已使用{CurrentUsage}/{MaxUsage}次)";
    }


    public class ThresholdWarningEvent : PubSubEvent<ThresholdWarningNotification> { }


}

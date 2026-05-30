using Prism.Mvvm;

namespace Core.Models
{
    /// <summary>
    /// 步骤报警配置，定义该步骤异常时的报警行为
    /// 每个步骤可独立配置是否启用报警、报警代码、报警等级
    /// 放在 Core 层以便 MotionControl 和 StationTasks 都能引用
    /// </summary>
    public class StepAlarmConfig : BindableBase
    {
        private bool _isEnabled;
        private string _alarmCode = "";
        private int _alarmLevel = 3;

        /// <summary> 是否启用步骤报警（默认关闭） </summary>
        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
        /// <summary> 报警代码（如 SCAN_TIMEOUT、VISION_FAIL），为空时自动生成 </summary>
        public string AlarmCode { get => _alarmCode; set => SetProperty(ref _alarmCode, value); }
        /// <summary>
        /// 报警等级：1=紧急(Emergency) 2=严重(Serious) 3=一般(General) 4=提示(Prompt)
        /// 默认3=一般
        /// </summary>
        public int AlarmLevel { get => _alarmLevel; set => SetProperty(ref _alarmLevel, value); }
    }
}

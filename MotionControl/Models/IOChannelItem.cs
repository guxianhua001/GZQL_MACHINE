using Prism.Mvvm;
using System.Windows.Media;

namespace MotionControl.Models
{
    /// <summary>
    /// DI（数字输入）通道视图项
    /// </summary>
    public class DiChannelItem : BindableBase
    {
        private readonly int _logicalId;
        private bool _isActive;

        /// <summary>逻辑ID（用于 ReadDi 调用）</summary>
        public int LogicalId => _logicalId;

        /// <summary>物理端口号</summary>
        public int Port { get; set; }

        /// <summary>IO点名称</summary>
        public string Name { get; set; }

        /// <summary>是否激活（高电平有效）</summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (SetProperty(ref _isActive, value))
                    RaisePropertyChanged(nameof(StatusColor));
            }
        }

        /// <summary>状态颜色（绿色=激活，灰色=未激活），使用静态单例避免重复创建</summary>
        public Brush StatusColor => IsActive
            ? ActiveBrush : InactiveBrush;

        private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(0, 255, 0));
        private static readonly SolidColorBrush InactiveBrush = new(Color.FromRgb(169, 169, 169));

        public DiChannelItem(int logicalId, int port, string name)
        {
            _logicalId = logicalId;
            Port = port;
            Name = name;
        }
    }

    /// <summary>
    /// DO（数字输出通道视图项（支持切换操作）
    /// 三色灯/蜂鸣器通道激活时显示对应颜色（红/绿/橙/黄），普通通道显示绿色
    /// </summary>
    public class DoChannelItem : BindableBase
    {
        private readonly int _logicalId;
        private bool _isActive;

        /// <summary>逻辑ID（用于 WriteDo 调用）</summary>
        public int LogicalId => _logicalId;

        /// <summary>物理端口号</summary>
        public int Port { get; set; }

        /// <summary>IO点名称</summary>
        public string Name { get; set; }

        /// <summary>
        /// 灯光类型标识（来自hwcfg.xml TowerLights配置）
        /// "Red"/"Green"/"Orange"/"Buzzer" 为三色灯/蜂鸣器通道，null为普通DO
        /// </summary>
        public string LightType { get; set; }

        /// <summary>当前输出状态</summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (SetProperty(ref _isActive, value))
                    RaisePropertyChanged(nameof(StatusColor));
            }
        }

        /// <summary>
        /// 状态颜色：激活时按LightType显示对应颜色，未激活显示灰色
        /// 三色灯通道与状态面板StationStateView保持一致
        /// </summary>
        public Brush StatusColor => IsActive ? GetActiveBrush() : InactiveBrush;

        private Brush GetActiveBrush() => LightType switch
        {
            "Red" => RedActiveBrush,
            "Green" => GreenActiveBrush,
            "Orange" => OrangeActiveBrush,
            "Buzzer" => BuzzerActiveBrush,
            _ => DefaultActiveBrush
        };

        private static readonly SolidColorBrush RedActiveBrush = new(Color.FromRgb(244, 67, 54));
        private static readonly SolidColorBrush GreenActiveBrush = new(Color.FromRgb(76, 175, 80));
        private static readonly SolidColorBrush OrangeActiveBrush = new(Color.FromRgb(255, 152, 0));
        private static readonly SolidColorBrush BuzzerActiveBrush = new(Color.FromRgb(255, 152, 0));
        private static readonly SolidColorBrush DefaultActiveBrush = new(Color.FromRgb(0, 255, 0));
        private static readonly SolidColorBrush InactiveBrush = new(Color.FromRgb(100, 100, 100));

        public DoChannelItem(int logicalId, int port, string name, string lightType = null)
        {
            _logicalId = logicalId;
            Port = port;
            Name = name;
            LightType = lightType;
        }
    }
}

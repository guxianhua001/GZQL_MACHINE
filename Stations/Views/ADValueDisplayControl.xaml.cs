using Core.Abstraction;
using SmarterMotion;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Stations.Views
{
    /// <summary>
    /// ADValueDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class ADValueDisplayControl : UserControl, INotifyPropertyChanged
    {
        private ObservableCollection<ChannelForceViewModel> _channelForces;
        public ObservableCollection<ChannelForceViewModel> ChannelForces
        {
            get => _channelForces;
            set
            {
                _channelForces = value;
                OnPropertyChanged(nameof(ChannelForces));
            }
        }

        private Timer _refreshTimer;
        private readonly IADValueConverter _adConverter;
        private readonly Random _random = new Random();

        public ADValueDisplayControl()
        {
            InitializeComponent();
            DataContext = this;

            // 这里应该通过依赖注入获取，暂时创建默认实例
            _adConverter = new DefaultADValueConverter();

            InitializeChannels();
        }

        /// <summary>
        /// 使用指定的AD值转换器初始化
        /// </summary>
        public ADValueDisplayControl(IADValueConverter adConverter)
        {
            _adConverter = adConverter ?? throw new ArgumentNullException(nameof(adConverter));
            InitializeComponent();
            DataContext = this;
            InitializeChannels();
        }

        private void InitializeChannels()
        {
            ChannelForces = new ObservableCollection<ChannelForceViewModel>();

            // 初始化9个通道 (0-8)
            for (int i = 0; i < 9; i++)
            {
                string range = i <= 3 || i >= 6 ? "±50N" : "±5N";
                double minValue = i <= 3 || i >= 6 ? -50 : -5;
                double maxValue = i <= 3 || i >= 6 ? 50 : 5;

                ChannelForces.Add(new ChannelForceViewModel
                {
                    Channel = i,
                    ChannelName = $"通道 {i} ({range})",
                    ForceValue = 0,
                    DisplayValue = "0.00 N",
                    MinValue = minValue,
                    MaxValue = maxValue
                });
            }
        }

        /// <summary>
        /// 开始实时刷新
        /// </summary>
        public void StartRealTimeRefresh()
        {
            StopRealTimeRefresh(); // 确保先停止之前的定时器

            _refreshTimer = new Timer(100); // 100ms刷新间隔
            _refreshTimer.Elapsed += (s, e) => RefreshADValues();
            _refreshTimer.AutoReset = true;
            _refreshTimer.Start();
        }

        /// <summary>
        /// 停止实时刷新
        /// </summary>
        public void StopRealTimeRefresh()
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _refreshTimer = null;
        }

        /// <summary>
        /// 刷新AD值（这里模拟数据，实际应该从硬件读取）
        /// </summary>
        private void RefreshADValues()
        {
            // 使用WPF的Dispatcher
            Dispatcher.BeginInvoke(new Action(() =>
            {
                for (int i = 0; i < ChannelForces.Count; i++)
                {
                    //// 模拟AD值（实际应该从硬件接口获取）
                    //double simulatedADValue = _random.Next(-32000, 32000);
                    double simulatedADValue = 0;
                    LTDMC.dmc_get_ad_input(0, (ushort)i, ref simulatedADValue);
                    // 计算力值
                    double force = _adConverter.Convert(i, simulatedADValue);

                    ChannelForces[i].ForceValue = force;
                    ChannelForces[i].DisplayValue = $"{force:F2} N";
                    ChannelForces[i].ADValue = simulatedADValue;
                }
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// 更新指定通道的AD值（从硬件读取时调用）
        /// </summary>
        public void UpdateChannelValue(int channel, double adValue)
        {
            if (channel >= 0 && channel < ChannelForces.Count)
            {
                double force = _adConverter.Convert(channel, adValue);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ChannelForces[channel].ForceValue = force;
                    ChannelForces[channel].DisplayValue = $"{force:F2} N";
                    ChannelForces[channel].ADValue = adValue;
                }), DispatcherPriority.Background);
            }
        }

        /// <summary>
        /// 批量更新通道值
        /// </summary>
        public void UpdateChannelValues(System.Collections.Generic.Dictionary<int, double> channelValues)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var kvp in channelValues)
                {
                    if (kvp.Key >= 0 && kvp.Key < ChannelForces.Count)
                    {
                        double force = _adConverter.Convert(kvp.Key, kvp.Value);
                        ChannelForces[kvp.Key].ForceValue = force;
                        ChannelForces[kvp.Key].DisplayValue = $"{force:F2} N";
                        ChannelForces[kvp.Key].ADValue = kvp.Value;
                    }
                }
            }), DispatcherPriority.Background);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ChannelForceViewModel : INotifyPropertyChanged
    {
        public int Channel { get; set; }
        public string ChannelName { get; set; }
        public double MinValue { get; set; }
        public double MaxValue { get; set; }

        private double _forceValue;
        public double ForceValue
        {
            get => _forceValue;
            set
            {
                _forceValue = value;
                OnPropertyChanged(nameof(ForceValue));
            }
        }

        private string _displayValue;
        public string DisplayValue
        {
            get => _displayValue;
            set
            {
                _displayValue = value;
                OnPropertyChanged(nameof(DisplayValue));
            }
        }

        public double ADValue { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 默认的AD值转换器实现
    /// </summary>
    internal class DefaultADValueConverter : IADValueConverter
    {
        public double Convert(int channel, double adValue)
        {
            double maxVoltage = 32767;
            double minVoltage = -32767;
            double maxForce, minForce;

            // 根据通道设置不同的量程
            if (channel >= 0 && channel <= 2)
            {
                // 0-2通道: -50N 到 50N
                maxForce = 50.0;
                minForce = -50.0;
            }
            else if (channel >= 3 && channel <= 5)
            {
                // 3-5通道: -5N 到 5N
                maxForce = 5.0;
                minForce = -5.0;
            }
            else if (channel >= 6 && channel <= 8)
            {
                // 6-8通道: -50N 到 50N
                maxForce = 50.0;
                minForce = -50.0;
            }
            else
            {
                throw new ArgumentException($"不支持的通道号: {channel}");
            }

            // 计算力值
            double force = ((adValue - minVoltage) / (maxVoltage - minVoltage)) * (maxForce - minForce) + minForce;
            return force;
        }

        public System.Collections.Generic.Dictionary<int, double> ConvertBatch(System.Collections.Generic.Dictionary<int, double> channelADValues)
        {
            var results = new System.Collections.Generic.Dictionary<int, double>();
            foreach (var kvp in channelADValues)
            {
                results[kvp.Key] = Convert(kvp.Key, kvp.Value);
            }
            return results;
        }

        public ADChannelConfig GetChannelConfig(int channel)
        {
            throw new NotImplementedException();
        }

        public void UpdateChannelConfig(ADChannelConfig config)
        {
            throw new NotImplementedException();
        }

        public System.Collections.Generic.IReadOnlyDictionary<int, ADChannelConfig> GetAllChannelConfigs()
        {
            throw new NotImplementedException();
        }
    }
}
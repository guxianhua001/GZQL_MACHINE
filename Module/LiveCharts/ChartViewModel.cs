
using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using Prism.Mvvm;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Framework.ViewModels
{
    public class ChartViewModel : BindableBase
    {
        private readonly Dispatcher _dispatcher;
        private const int MaxPoints = 200;
        private const double MinYValue = -6.5; // Y轴最小值为-1.5
        private const double MaxYValue = 6.5;  // Y轴最大值为1.5
        private static readonly Brush[] ChannelBrushes =
        {
            Brushes.DodgerBlue,
            Brushes.OrangeRed,
            Brushes.LimeGreen,
            Brushes.Gold
        };

        public ChartViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            InitializeChart();
        }

        public SeriesCollection Series { get; private set; }
        public Func<double, string> XFormatter { get; set; }
        public Func<double, string> YFormatter { get; set; }
        private double _axisMin = MinYValue;
        private double _axisMax = MaxYValue;
        // 添加Y轴范围属性
        public double AxisMin
        {
            get => _axisMin;
            set => SetProperty(ref _axisMin, value);
        }
        public double AxisMax
        {
            get => _axisMax;
            set => SetProperty(ref _axisMax, value);
        }
        private void InitializeChart()
        {
            // 初始化四个通道
            Series = new SeriesCollection();
            for (int i = 0; i < 2; i++)
            {
                Series.Add(new LineSeries
                {
                    Title = $"通道 {i + 1}",
                    Values = new ChartValues<ObservableValue>(),
                    Stroke = ChannelBrushes[i],
                    Fill = Brushes.Transparent,
                    LineSmoothness = 0.8,
                    PointGeometry = null
                });
            }

            // 配置坐标映射
            var mapper = LiveCharts.Configurations.Mappers.Xy<ObservableValue>()
                .X((v, index) => index)
                .Y(v => v.Value);

            Series.Configuration = mapper;

            XFormatter = value => $"{value:F1}s";
            YFormatter = value => $"{value:F3} N";
        }

        // 修改ChartViewModel.UpdateSeries方法
        public void UpdateSeries(double value, int channelIndex)
        {
            if (Application.Current == null || Application.Current.Dispatcher == null)
                return;
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                // 异步更新
                Application.Current.Dispatcher.BeginInvoke((Action)(() =>
                    UpdateSeries(value, channelIndex)),
                    DispatcherPriority.Background);
                return;
            }

            // UI线程直接操作
            if (channelIndex < 0 || channelIndex >= Series.Count) return;

            var series = Series[channelIndex] as LineSeries;
            series?.Values.Add(new ObservableValue(value));

            // 限制数据量 _maxPoints
            while (series?.Values.Count > MaxPoints)
            {
                series.Values.RemoveAt(0);
            }
        }
    }
}

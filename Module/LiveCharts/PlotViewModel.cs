using Interfaces;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using LiveCharts;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using System.Windows;
using System.Text.RegularExpressions;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using static System.Net.WebRequestMethods;
using LiveCharts.Configurations;


namespace Framework.ViewModels
{
    public class PlotViewModel : BindableBase
    {

        // 配置时间轴映射规则（核心修改）
        private static readonly CartesianMapper<DateTimePoint> TimeMapper = Mappers.Xy<DateTimePoint>()
            .X(point => point.DateTime.Ticks)
            .Y(point => point.Value);

        private SeriesCollection _seriesCollection;
        public SeriesCollection SeriesCollection
        {
            get => _seriesCollection;
            set => SetProperty(ref _seriesCollection, value);
        }

        // 添加进度条属性
        private double _progress;
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }
        public Func<double, string> XFormatter { get; set; }
        public Func<double, string> YTorqueFormatter { get; set; }
        public Func<double, string> YPositionFormatter { get; set; }

        private DelegateCommand _ImportCsvCommand;
        public DelegateCommand ImportCsvCommand =>
                _ImportCsvCommand ??= new DelegateCommand(ImportCsv);

        ICsvParserService _parser;
        public PlotViewModel(ICsvParserService parser)
        {
            _parser = parser;
            // 初始化映射规则
            var mapper = Mappers.Xy<DateTimePoint>()
                .X(p => p.DateTime.Ticks)
                .Y(p => p.Value);

            SeriesCollection = new SeriesCollection(mapper);
        }

        private async void ImportCsv()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "CSV文件|*.csv",
                DefaultExt = ".csv"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var rawData = await Task.Run(() => _parser.ParseTorqueData(dialog.FileName));
                var sampledData = rawData.Where((_, i) => i % 5 == 0).ToList();
                // 在数据显示前添加验证
                if (sampledData.Any(d =>
                    double.IsNaN(d.Torque) || double.IsNaN(d.Position)))
                {
                    MessageBox.Show("存在无效数值");
                    return;
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 添加数据有效性检查
                    var validTorquePoints = sampledData
                        .Where(d => d.Timestamp != default && !double.IsNaN(d.Torque))
                        .Select(d => new DateTimePoint(d.Timestamp, d.Torque))
                        .ToList();

                    var validPositionPoints = sampledData
                        .Where(d => d.Timestamp != default && !double.IsNaN(d.Position))
                        .Select(d => new DateTimePoint(d.Timestamp, d.Position))
                        .ToList();

                    // 配置全局时间映射规则
                    SeriesCollection = new SeriesCollection(TimeMapper)
                {
                   new LineSeries
                    {
                          Title = "扭矩 (Nm)",
                          Values = new ChartValues<DateTimePoint>(validTorquePoints
                        ), // 注意此处闭合括号
                        ScalesYAt = 0,
                        LineSmoothness = 0,
                        PointGeometry = null
                   },
                    new LineSeries
                    {
                        Title = "位置 (mm)",
                        Values = new ChartValues<DateTimePoint>(validPositionPoints
                        ),
                        ScalesYAt = 1,
                        LineSmoothness = 0,
                        StrokeDashArray = new DoubleCollection { 2 }
                    }
                };
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"错误: {ex.Message}");
            }
        }
    }
}


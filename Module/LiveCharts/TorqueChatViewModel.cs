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


namespace Framework
{
    // MainViewModel.cs
    public class TorqueChatViewModel : BindableBase
    {
        private readonly ICsvParserService _csvService;
        private SeriesCollection _seriesCollection;
        public SeriesCollection SeriesCollection
        {
            get => _seriesCollection;
            set => SetProperty(ref _seriesCollection, value);
        }
        // 新增坐标轴集合
        public AxesCollection XAxes { get; } = new AxesCollection();
        public AxesCollection YAxes { get; } = new AxesCollection();
        public ObservableCollection<PointViewModel> Points { get; } = new ObservableCollection<PointViewModel>();
        public DelegateCommand ImportCsvCommand { get; }

        public TorqueChatViewModel(ICsvParserService csvService)
        {
            _csvService = csvService;
            ImportCsvCommand = new DelegateCommand(ImportCsv);
            InitializeChart();
        }

        private void InitializeChart()
        {
            SeriesCollection = new SeriesCollection();

            // 配置X轴
            XAxes.Add(new Axis
            {
                Title = "位移量 (mm)",
                LabelFormatter = value => value.ToString("N2") + " mm",
                Position = AxisPosition.LeftBottom
            });

            // 配置Y轴
            YAxes.Add(new Axis
            {
                Title = "拨入力 (N)",
                LabelFormatter = value => value.ToString("N2") + " N",
                Position = AxisPosition.RightTop
            });
        }

        private void ImportCsv()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "CSV文件|*.csv";

            bool? result = dialog.ShowDialog();

            if (result == true)
            {
                try
                {
                    ProcessCsvData(dialog.FileName);
                    UpdateChart();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入CSV文件时发生错误: {ex.Message}");
                }
            }
        }

        private void ProcessCsvData(string filePath)
        {
            // 使用UTF-8编码读取文件，并忽略BOM（兼容不同编辑器）
            using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            csv.Context.RegisterClassMap<DialRecordMap>();

            var records = csv.GetRecords<DialRecord>()
                .Where(r => r.IsSuccess)
                .GroupBy(r => r.Sequence)
                .Where(g => g.Count() == 2)
                .ToList();

            Points.Clear();
            foreach (var group in records)
            {
                var pointVM = new PointViewModel { Index = group.Key };

                foreach (var record in group)
                {
                    var signedDisplacement = record.Direction == "负向" ?
                        -record.HomeDisplacement :
                        record.HomeDisplacement;

                    var dialRecord = new DialRecord
                    {
                        SearchPosition = record.SearchPosition,
                        HomeDialForce = record.HomeDialForce,
                        //DialForce = record.DialForce,
                        HomeDisplacement = signedDisplacement,
                        HomeTargetPosition = record.HomeTargetPosition,
                        HomeActualPosition = record.HomeActualPosition,
                        IsSuccess = record.IsSuccess
                    };

                    if (record.Direction == "负向")
                    {
                        pointVM.NegativeRecord = dialRecord;
                        pointVM.X = signedDisplacement;
                    }
                    else
                    {
                        pointVM.PositiveRecord = dialRecord;
                        pointVM.Y = signedDisplacement;
                    }
                }
                Points.Add(pointVM);
            }
        }

        private void UpdateChart()
        {
            SeriesCollection.Clear();

            // 创建负向散点系列
            var negativeScatter = new ScatterSeries
            {
                Title = "负向操作",
                Values = new ChartValues<ObservablePoint>(),
                PointGeometry = DefaultGeometries.Triangle,
                Fill = System.Windows.Media.Brushes.Red,
                StrokeThickness = 2,
                MinPointShapeDiameter = 8,
                MaxPointShapeDiameter = 8
            };

            // 创建正向散点系列
            var positiveScatter = new ScatterSeries
            {
                Title = "正向操作",
                Values = new ChartValues<ObservablePoint>(),
                PointGeometry = DefaultGeometries.Circle,
                Fill = System.Windows.Media.Brushes.Green,
                StrokeThickness = 2,
                MinPointShapeDiameter = 8,
                MaxPointShapeDiameter = 8
            };

            // 创建连接线系列
            var lineSeries = new LineSeries
            {
                Title = "滞回曲线",
                Values = new ChartValues<ObservablePoint>(),
                Stroke = System.Windows.Media.Brushes.Blue,
                StrokeDashArray = new System.Windows.Media.DoubleCollection { 2 },
                Fill = System.Windows.Media.Brushes.Transparent,
                PointGeometrySize = 0
            };

            foreach (var point in Points)
            {
                if (point.NegativeRecord != null)
                {
                    negativeScatter.Values.Add(new ObservablePoint(
                        point.NegativeRecord.HomeDisplacement,
                        point.NegativeRecord.HomeDialForce
                    ));
                }

                if (point.PositiveRecord != null)
                {
                    positiveScatter.Values.Add(new ObservablePoint(
                        point.PositiveRecord.HomeDisplacement,
                        point.PositiveRecord.HomeDialForce
                    ));
                }

                // 添加连接线
                if (point.NegativeRecord != null && point.PositiveRecord != null)
                {
                    lineSeries.Values.Add(new ObservablePoint(
                        point.NegativeRecord.HomeDisplacement,
                        point.NegativeRecord.HomeDialForce
                    ));
                    lineSeries.Values.Add(new ObservablePoint(
                        point.PositiveRecord.HomeDisplacement,
                        point.PositiveRecord.HomeDialForce
                    ));
                }
            }

            SeriesCollection.Add(negativeScatter);
            SeriesCollection.Add(positiveScatter);
            SeriesCollection.Add(lineSeries);
        }

        // CSV字段映射配置
        public sealed class DialRecordMap : ClassMap<DialRecord>
        {
            public DialRecordMap()
            {
                Map(m => m.Sequence).Name("序号");
                Map(m => m.OperationTime).Name("操作时间");
                Map(m => m.Direction).Name("方向");
                Map(m => m.SearchPosition).Name("寻针位置(mm)");
                Map(m => m.HomeDialForce).Name("接触力(N)");
                Map(m => m.HomeDisplacement).Name("寻针位移量(mm)");
                Map(m => m.HomeTargetPosition).Name("寻针目标位置(mm)");
                Map(m => m.HomeActualPosition).Name("寻针实际位置(mm)");
                Map(m => m.DialForce).Name("拨针力(N)");
                Map(m => m.DialDisplacement).Name("位移量(mm)");
                Map(m => m.TargetPosition).Name("目标位置(mm)");
                Map(m => m.ActualPosition).Name("实际位置(mm)");
                Map(m => m.IsSuccess).Convert(row => row.Row.GetField("结果") == "成功");
            }
        }
    }
}

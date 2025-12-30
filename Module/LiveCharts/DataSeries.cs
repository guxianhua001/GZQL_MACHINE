using LiveCharts.Defaults;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework
{
    // 修改DataSeries类以存储带时间戳的数据点
    public class DataSeries : BindableBase
    {
        public class DataPoint
        {
            public DateTime Time { get; set; }
            public double Value { get; set; }
        }

        private readonly object _syncLock = new();
        private DateTime _startTime;

        public DateTime StartTime
        {
            get => _startTime;
            private set => SetProperty(ref _startTime, value);
        }

        public string Title { get; set; }
        public ObservableCollection<DataPoint> HistoricalData { get; } = new();
        public ObservableCollection<DataPoint> DisplayData { get; } = new();
        public int MaxDisplayPoints { get; set; } = 100;

        public void StartRecording()
        {
            StartTime = DateTime.Now;
            HistoricalData.Clear();
            DisplayData.Clear();
        }
        private readonly List<DataPoint> _displayData = new List<DataPoint>();
        private bool _hasNewData;

        public bool HasNewData => _hasNewData;
        public void AddValue(double value)
        {
            lock (_syncLock)
            {
                var point = new DataPoint
                {
                    Time = DateTime.Now,
                    Value = value
                };

                HistoricalData.Add(point);
                DisplayData.Add(point);

                _hasNewData = true;
            }
        }
        public IEnumerable<ObservablePoint> GetDisplayPoints(int maxPoints)
        {
            lock (_syncLock)
            {
                var points = _displayData
                    .Select(p => new ObservablePoint(
                        (p.Time - StartTime).TotalSeconds,
                        p.Value))
                    .ToList();

                // 保持数据量
                if (points.Count > maxPoints)
                {
                    points = points.Skip(points.Count - maxPoints).ToList();
                }

                return points;
            }
        }

        public void ResetNewDataFlag() => _hasNewData = false;
        // 新增插值方法
        public IEnumerable<DataPoint> GetInterpolatedPoints()
        {
            if (DisplayData.Count < 2) return DisplayData;

            // 线性插值示例
            var interpolated = new List<DataPoint>();
            for (int i = 1; i < DisplayData.Count; i++)
            {
                var prev = DisplayData[i - 1];
                var current = DisplayData[i];

                // 每两点间插入一个中间点
                interpolated.Add(prev);
                interpolated.Add(new DataPoint
                {
                    Time = prev.Time.AddMilliseconds(50),
                    Value = (prev.Value + current.Value) / 2
                });
            }
            return interpolated.Take(MaxDisplayPoints);
        }
    }
}

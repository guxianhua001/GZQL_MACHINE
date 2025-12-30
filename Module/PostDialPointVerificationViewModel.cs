// PostDialPointVerificationViewModel.cs
using Interfaces;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Framework.ViewModels
{
    public class DialPointData : BindableBase
    {
        private int _index;
        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        public int PointId { get; set; } // 添加点位的原始ID用于映射

        private bool? _isOk;
        public bool? IsOk
        {
            get => _isOk;
            set
            {
                SetProperty(ref _isOk, value);
                State = value switch
                {
                    true => "OK",
                    false => "NG",
                    null => "未操作"
                };
                RaisePropertyChanged(nameof(State));
            }
        }

        public string State { get; private set; } = "未操作";

        private double _xDeviation;
        public double XDeviation
        {
            get => _xDeviation;
            set
            {
                SetProperty(ref _xDeviation, value);
                RaisePropertyChanged(nameof(TotalDeviation));
            }
        }

        private double _yDeviation;
        public double YDeviation
        {
            get => _yDeviation;
            set
            {
                SetProperty(ref _yDeviation, value);
                RaisePropertyChanged(nameof(TotalDeviation));
            }
        }

        public double TotalDeviation => Math.Sqrt(XDeviation * XDeviation + YDeviation * YDeviation);
    }

    public class PostDialPointVerificationViewModel : BindableBase
    {
        private readonly ITaskWithPoints _boundTask;
        private readonly string _visionResult;
        // 二维码绑定属性
        public string TaskMaterialQRCode => _boundTask.MaterialQRCode;
        public PinMapViewModel MapViewModel { get; }
        public ObservableCollection<DialPointData> Points { get; } = new();

        public DelegateCommand ConfirmCommand { get; set; }
        public DelegateCommand ExportCommand { get; set; }

        public PostDialPointVerificationViewModel(ITaskWithPoints task, string visionResult)
        {
            _boundTask = task ?? throw new ArgumentNullException(nameof(task));
            _visionResult = visionResult ?? throw new ArgumentNullException(nameof(visionResult));

            MapViewModel = new PinMapViewModel(task);
            ParseVisionResult();
            AddMissingPoints();

            ConfirmCommand = new DelegateCommand(OnConfirm);
            ExportCommand = new DelegateCommand(OnExport);
        }

        private void OnConfirm()
        {
            // 确认操作逻辑
        }

        private void OnExport()
        {
            // 导出Excel逻辑
        }

        private void ParseVisionResult()
        {
            try
            {
                // 示例: "T300,1,0.305,0.302,2,0.386,0.303,..."
                var parts = _visionResult.Split(',');

                // 跳过第一个标识 "T300"
                int startIndex = 1;
                int pointIndex = 0;

                // 每3个值为一组：状态码、X偏差、Y偏差
                for (int i = startIndex; i + 2 < parts.Length; i += 3)
                {
                    // 解析状态码 (1=OK, 2=NG)
                    if (!int.TryParse(parts[i], out int statusCode))
                        continue;

                    // 解析X偏差
                    if (!double.TryParse(parts[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double xDeviation))
                        continue;

                    // 解析Y偏差
                    if (!double.TryParse(parts[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out double yDeviation))
                        continue;

                    // 创建数据点
                    var pointData = new DialPointData
                    {
                        PointId = pointIndex + 1,
                        Index = pointIndex,
                        XDeviation = xDeviation,
                        YDeviation = yDeviation,
                        IsOk = statusCode == 1 ? true :
                               (statusCode == 2 ? false : null)
                    };

                    Points.Add(pointData);
                    pointIndex++;
                }
            }
            catch (Exception ex)
            {
                // 日志处理异常
                System.Diagnostics.Debug.WriteLine($"解析视觉结果时出错: {ex.Message}");
            }
        }

        private void AddMissingPoints()
        {
            int totalPoints = _boundTask.PinPoints.Count;

            // 检查是否有遗漏的点位
            for (int pointIndex = Points.Count; pointIndex < totalPoints; pointIndex++)
            {
                Points.Add(new DialPointData
                {
                    PointId = pointIndex + 1,
                    Index = pointIndex,
                    IsOk = null,
                    XDeviation = double.NaN,
                    YDeviation = double.NaN
                });
            }
        }
    }
}

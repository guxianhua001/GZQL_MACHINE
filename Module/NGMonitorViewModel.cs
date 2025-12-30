using Prism.Mvvm;
using SmarterMotion;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZXing.Common;
using ZXing;
using System.Timers;
using Stations;
using System.Windows;

namespace Framework.ViewModels
{
    // ViewModel层
    public class NGMonitorViewModel : BindableBase
    {
        private readonly Timer _refreshTimer;

        public class NGPoint : BindableBase
        {
            private int _count;
            private string _qrCode;
            private bool _isActive;
            private bool _isFlashing;

            public int Count
            {
                get => _count;
                set => SetProperty(ref _count, value, OnCountChanged);
            }

            public string QRCode
            {
                get => _qrCode;
                set => SetProperty(ref _qrCode, value);
            }

            public bool IsActive
            {
                get => _isActive;
                set => SetProperty(ref _isActive, value);
            }

            public bool IsFlashing
            {
                get => _isFlashing;
                set => SetProperty(ref _isFlashing, value);
            }

            private void OnCountChanged()
            {
                IsActive = Count >= 1;
                IsFlashing = Count >= 2;
            }
        }

        // NG位集合
        public ObservableCollection<NGPoint> NG1Points { get; } = new();
        public ObservableCollection<NGPoint> NG2Points { get; } = new();
        public ObservableCollection<NGPoint> NG3Points { get; } = new();

        //private Task2 _task2;

        public NGMonitorViewModel(TaskInstanceManager taskManager)
        {
            InitializePoints();

            //_task2 = taskManager.GetTask<Task2>();

            _refreshTimer = new Timer(1000);
            _refreshTimer.Elapsed += (s, e) => RefreshData();
            _refreshTimer.Start();
        }

        private void InitializePoints()
        {
            for (int i = 0; i < 2; i++)
            {
                NG1Points.Add(new NGPoint());
                NG2Points.Add(new NGPoint());
                NG3Points.Add(new NGPoint());
            }
        }

        private void RefreshData()
        {
            // 获取实时数据
        }

        private void UpdateNGPoints(ObservableCollection<NGPoint> points, int count,string qrCode = null)
        {
            if (Application.Current == null) return;
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    points[0].Count = count >= 1 ? 1 : 0;
                    points[0].QRCode = count >= 1 ? qrCode : null;

                    points[1].Count = count >= 2 ? 1 : 0;
                    points[1].QRCode = count >= 2 ? qrCode : null;

                    // 处理闪烁状态
                    foreach (var p in points)
                    {
                        p.IsFlashing = count >= 2;
                    }
                });
            }
            catch{  }
        }

        //private string GenerateQR(string content)
        //{
        //    var writer = new BarcodeWriter
        //    {
        //        Format = BarcodeFormat.QR_CODE,
        //        Options = new EncodingOptions { Height = 80, Width = 80 }
        //    };
        //    return writer.Write(content).ToBitmapImage();
        //}
    }

}

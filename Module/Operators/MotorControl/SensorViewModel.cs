using Prism.Ioc;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using SmarterMotion;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Framework.ViewModels
{
    public class SensorViewModel : BindableBase
    {
        private XDi _sensor;
        private bool _sensorStatus;
        private string _sensorName;
        private bool _sensorEnable;
        private DispatcherTimer _refreshTimer;
        private void SetupRefreshTimer()
        {
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _refreshTimer.Tick += (s, e) => UpdateSensorProperties();
            _refreshTimer.Start();
        }

        // 传感器实例由外部动态注入
        public XDi Sensor
        {
            get => _sensor;
            set
            {
                if (_sensor != value)
                {
                    // 解绑旧传感器的监听
                    if (_sensor != null)
                        _sensor.PropertyChanged -= OnSensorPropertyChanged;

                    if (SetProperty(ref _sensor, value))
                    {
                        // 绑定新传感器的监听
                        if (_sensor != null)
                            _sensor.PropertyChanged += OnSensorPropertyChanged;

                        UpdateSensorProperties();
                    }
                }
            }
        }
        private void OnSensorPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 当 XDi 的 Name 属性变化时，更新 SensorStatus
            //if (e.PropertyName == nameof(XDi.Name))
            //{
            //    SensorName = Sensor?.Name ?? "未配置";
            //}
            if (e.PropertyName == nameof(XDi.STS))
            {
                SensorStatus = Sensor.STS;
            }
        }

        // 对外暴露的绑定属性
        public bool SensorStatus
        {
            get => _sensorStatus;
            set => SetProperty(ref _sensorStatus, value);
        }

        public string SensorName
        {
            get => _sensorName;
            set => SetProperty(ref _sensorName, value);
        }
        public bool SensorEnable
        {
            get => _sensorEnable;
            set => SetProperty(ref _sensorEnable, value);
        }
        public SensorViewModel()
        {
            SetupRefreshTimer();
        }
        private void UpdateSensorProperties()
        {
            if (Sensor != null)
            {
                SensorName = _sensor?.Name ?? "未配置";
                SensorStatus = _sensor?.STS ?? false;
                SensorEnable = _sensor != null;
            }
            else
            {
                SensorName = "未配置";
                SensorStatus = false;
                SensorEnable = false;
            }
        }
    }
}

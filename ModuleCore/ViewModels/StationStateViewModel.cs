using Prism.Mvvm;
using Prism.Regions;
using SmarterMotion;
using System.Diagnostics;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Prism.Ioc;
using Core.Abstraction;

namespace ModuleCore.ViewModels
{
    public class StationStateViewModel : BindableBase
    {
        public StationStateViewModel(IContainerExtension container,IRegionManager regionManager)
        {

        }
        private int _stationId;
        public int StationId
        {
            get => _stationId;
            set
            {
                if (SetProperty(ref _stationId, value))
                {
                    // StationId 变化时重新初始化
                    InitStationManager();
                }
            }
        }
        private XStation _station;
        private string _stateText = ">>> 等待复位";
        private Brush _statusColor = Brushes.White;

        public string StateText
        {
            get => _stateText;
            set => SetProperty(ref _stateText, value);
        }

        public Brush StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }
        private SolidColorBrush StatusGreen { get; set; }
        private SolidColorBrush StatusRed { get; set; }

        // 与 XStationManager 交互的逻辑...
        private void InitStationManager()
        {
            if (XStationManager.Instance != null)
            {
                _station = XStationManager.Instance.FindStationById(_stationId);
                if (_station != null)
                {
                    GetStatusBrush();
                    SubscribeEvents();
                }
            }
        }
        /// <summary>
        /// 订阅事件，更新状态文本和颜色。
        /// </summary>
        private void SubscribeEvents()
        {
            _station.RedLightON += () => UpdateColor(StatusRed);
            _station.GreenLightON += () => UpdateColor(StatusGreen);
            _station.OrangeLightON += () => UpdateColor(Brushes.Orange);
            _station.AllLightsOFF += () => UpdateColor(Brushes.White);
            _station.OnStationStateChanged += UpdateStateText;
        }
        private void GetStatusBrush()
        {
            if (Application.Current == null) return;
            var colorGreen = (Color)ColorConverter.ConvertFromString("#3AB54A");
            var colorRed = (Color)ColorConverter.ConvertFromString("#C82506");
            StatusGreen = new SolidColorBrush(colorGreen);
            StatusRed = new SolidColorBrush(colorRed);
        }

        private void UpdateColor(Brush color)
        {
            if (color == null) color = Brushes.Magenta;
            if (Application.Current == null) return;

            var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            if (dispatcher != null)
            {
                try
                {
                    dispatcher.Invoke(() =>
                    {
                        // 强制清除可能的样式缓存
                        StatusColor = Brushes.Transparent;
                        StatusColor = color;
                    });
                }
                catch {  }
               
            }
        }
        private void UpdateStateText(XStationState state)
        {
            var text = state switch
            {
                XStationState.ESTOP => "急停按下>>>",
                XStationState.ALARM => "发现报警>>>等待复位",
                XStationState.PAUSE => "暂停中>>>等待运行",
                XStationState.RESETING => "复位中>>>",
                XStationState.RUNNING => "运行中>>>",
                XStationState.STOP => "停止>>>等待复位",
                XStationState.WAITRESET => ">>>等待复位",
                XStationState.CLEAR => "运行中>>>正在清料",
                XStationState.TIP => "运行中>>>发现报警",
                XStationState.WAITRUN => ">>>等待运行"
            };
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                try
                {
                    dispatcher.Invoke(() =>
                    {
                        StateText = text;
                    });
                }
                catch { }
            }
        }
    }
}

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
        private readonly ILocalizationService _localizationService;
        public StationStateViewModel(
            IContainerExtension container,
            IRegionManager regionManager,
            ILocalizationService localizationService)
        {
            _localizationService = localizationService;
            // 订阅语言变更事件
            _localizationService.LanguageChanged += OnLanguageChanged;
        }
        private void OnLanguageChanged(object sender, EventArgs e)
        {
            // 语言变化时更新当前状态文本
            UpdateCurrentStateText();
        }

        private void UpdateCurrentStateText()
        {
            if (_station?.State != null)
            {
                UpdateStateText(_station.State);
            }
        }
        private string GetLocalizedStateText(XStationState state)
        {
            //string[] testKeys = { "ESTOP", "ALARM", "RUNNING", "PAUSE", "RESETING" };
            //foreach (var key in testKeys)
            //{                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 
            //    var result = _localizationService.GetResource(key);
            //    Debug.WriteLine($"  {key}: {result}");
            //}

            // 使用枚举值作为资源键
            string resourceKey = state.ToString();

            // 从本地化服务获取字符串
            return _localizationService.GetResource(resourceKey);
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
            // 使用本地化服务获取文本
            var text = GetLocalizedStateText(state);

            // 或者使用默认映射作为后备方案
            if (string.IsNullOrEmpty(text))
            {
                text = state switch
                {
                    XStationState.ESTOP => ">>> 等待复位",
                    XStationState.ALARM => "发现报警>>>等待复位",
                    XStationState.PAUSE => "暂停中>>>等待运行",
                    XStationState.RESETING => "复位中>>>",
                    XStationState.RUNNING => "运行中>>>",
                    XStationState.STOP => "停止>>>等待复位",
                    XStationState.WAITRESET => ">>>等待复位",
                    XStationState.CLEAR => "运行中>>>正在清料",
                    XStationState.TIP => "运行中>>>发现报警",
                    XStationState.WAITRUN => ">>>等待运行",
                    _ => state.ToString()
                };
            }

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
        // 清理资源
        public void Cleanup()
        {
            if (_localizationService != null)
            {
                _localizationService.LanguageChanged -= OnLanguageChanged;
            }
            UnsubscribeEvents();
        }
        private void UnsubscribeEvents()
        {
            if (_station != null)
            {
                _station.RedLightON -= () => UpdateColor(StatusRed);
                _station.GreenLightON -= () => UpdateColor(StatusGreen);
                _station.OrangeLightON -= () => UpdateColor(Brushes.Orange);
                _station.AllLightsOFF -= () => UpdateColor(Brushes.White);
                _station.OnStationStateChanged -= UpdateStateText;
            }
        }
    }
}

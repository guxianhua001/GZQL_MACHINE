using Core.Abstraction;
using MotionControl.Events;
using MotionControl.Models;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Windows;
using System.Windows.Media;

namespace MotionControl.ViewModels
{
    public class StationStateViewModel : BindableBase, IDisposable
    {
        private readonly IEventAggregator _ea;
        private readonly ILocalizationService _localization;
        private SubscriptionToken _stateToken;

        private string _stateText;
        private Brush _statusColor = Brushes.Orange;
        private bool _buzzerActive;
        private Brush _buzzerColor = Brushes.Gray;

        private bool _isBlinkingRed;
        private bool _isBlinkingGreen;
        private bool _isBlinkingOrange;
        private static readonly Brush RedOnColor = new SolidColorBrush(Color.FromRgb(0xC8, 0x25, 0x06));
        private static readonly Brush RedOffColor = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        private static readonly Brush GreenOnColor = new SolidColorBrush(Color.FromRgb(0x3A, 0xB5, 0x4A));
        private static readonly Brush GreenOffColor = new SolidColorBrush(Color.FromRgb(0x1B, 0x5E, 0x20));
        private static readonly Brush OrangeOnColor = new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00));
        private static readonly Brush OrangeOffColor = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));

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

        public bool BuzzerActive
        {
            get => _buzzerActive;
            set => SetProperty(ref _buzzerActive, value);
        }

        public Brush BuzzerColor
        {
            get => _buzzerColor;
            set => SetProperty(ref _buzzerColor, value);
        }

        public StationStateViewModel(IEventAggregator ea, ILocalizationService localization)
        {
            _ea = ea;
            _localization = localization;
            _stateText = _localization.GetResource("StateDesc_WaitReset");
            _stateToken = _ea.GetEvent<StationStateChangedEvent>().Subscribe(OnStateChanged);
            _localization.LanguageChanged += OnLanguageChanged;
        }

        private void OnStateChanged(StationStatePayload payload)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                StateText = payload.Description;

                _isBlinkingRed = payload.State == StationState.STOP
                              || payload.State == StationState.WAITRESET
                              || payload.State == StationState.ESTOP
                              || payload.State == StationState.ALARM
                              || payload.State == StationState.TIP;
                _isBlinkingGreen = payload.State == StationState.PAUSE;
                _isBlinkingOrange = payload.State == StationState.WAITRUN;

                if (_isBlinkingRed)
                    StatusColor = payload.RedLight ? RedOnColor : RedOffColor;
                else if (_isBlinkingGreen)
                    StatusColor = payload.GreenLight ? GreenOnColor : GreenOffColor;
                else if (_isBlinkingOrange)
                    StatusColor = payload.OrangeLight ? OrangeOnColor : OrangeOffColor;
                else if (payload.RedLight)
                    StatusColor = RedOnColor;
                else if (payload.GreenLight)
                    StatusColor = GreenOnColor;
                else if (payload.OrangeLight)
                    StatusColor = OrangeOnColor;
                else
                    StatusColor = Brushes.White;

                BuzzerActive = payload.Buzzer;
                BuzzerColor = payload.Buzzer ? Brushes.Red : Brushes.Gray;
            });
        }

        private void OnLanguageChanged(object sender, Core.Abstraction.LanguageChangedEventArgs e)
        {
        }

        public void Dispose()
        {
            _localization.LanguageChanged -= OnLanguageChanged;
            _stateToken?.Dispose();
        }
    }
}

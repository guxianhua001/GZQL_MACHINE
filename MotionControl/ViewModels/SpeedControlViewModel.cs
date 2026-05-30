using MotionControl.Interfaces;
using Prism.Commands;
using Prism.Mvvm;
using System;

namespace MotionControl.ViewModels
{
    public class SpeedControlViewModel : BindableBase, IDisposable
    {
        private readonly ISpeedOverrideService _speedOverride;

        private double _pendingPercent;
        public double PendingPercent
        {
            get => _pendingPercent;
            set
            {
                var clamped = Math.Clamp(Math.Round(value), 1, 100);
                if (SetProperty(ref _pendingPercent, clamped))
                    UpdatePendingState();
            }
        }

        private double _currentPercent;
        public double CurrentPercent
        {
            get => _currentPercent;
            set => SetProperty(ref _currentPercent, value);
        }

        private bool _isPending;
        public bool IsPending
        {
            get => _isPending;
            set => SetProperty(ref _isPending, value);
        }

        public DelegateCommand ConfirmCommand { get; }

        public SpeedControlViewModel(ISpeedOverrideService speedOverride)
        {
            _speedOverride = speedOverride;
            _pendingPercent = _speedOverride.SpeedPercent;
            _currentPercent = _speedOverride.SpeedPercent;
            _isPending = false;
            _speedOverride.SpeedChanged += OnSpeedChanged;
            ConfirmCommand = new DelegateCommand(ExecuteConfirm, CanConfirm)
                .ObservesProperty(() => IsPending);
        }

        private void OnSpeedChanged(double newPercent)
        {
            CurrentPercent = newPercent;
            UpdatePendingState();
        }

        private void UpdatePendingState()
        {
            IsPending = Math.Abs(_pendingPercent - _speedOverride.SpeedPercent) > 0.5;
        }

        private bool CanConfirm() => IsPending;

        private void ExecuteConfirm()
        {
            _speedOverride.SpeedPercent = PendingPercent;
        }

        public void Dispose()
        {
            _speedOverride.SpeedChanged -= OnSpeedChanged;
        }
    }
}

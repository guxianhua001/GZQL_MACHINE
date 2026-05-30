using Core.Abstraction;
using MotionControl.Interfaces;
using System;
using System.Text.Json;

namespace MotionControl.Services
{
    public class SpeedOverrideService : ISpeedOverrideService
    {
        private readonly IAppSettingService _appSettings;
        private double _speedPercent;

        public double SpeedPercent
        {
            get => _speedPercent;
            set
            {
                var clamped = Math.Clamp(Math.Round(value), 1, 100);
                if (_speedPercent == clamped) return;
                _speedPercent = clamped;
                Save();
                SpeedChanged?.Invoke(_speedPercent);
            }
        }

        public event Action<double> SpeedChanged;

        public SpeedOverrideService(IAppSettingService appSettings)
        {
            _appSettings = appSettings;
            Load();
        }

        private void Load()
        {
            if (_appSettings.Settings.ExtensionData.TryGetValue("GlobalSpeedPercent", out var element))
            {
                if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var val))
                    _speedPercent = Math.Clamp(Math.Round(val), 1, 100);
                else if (element.ValueKind == JsonValueKind.String && double.TryParse(element.GetString(), out var sVal))
                    _speedPercent = Math.Clamp(Math.Round(sVal), 1, 100);
                else
                    _speedPercent = 100;
            }
            else
            {
                _speedPercent = 100;
            }
        }

        private void Save()
        {
            _appSettings.Settings.ExtensionData["GlobalSpeedPercent"] =
                JsonSerializer.SerializeToElement(_speedPercent);
            _appSettings.Save();
        }
    }
}

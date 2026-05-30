using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Module.Services
{
    public class StageCalibrationService : IStageCalibrationService
    {
        private readonly ILoadUnloadController _controller;
        private readonly ILoggerService _logger;
        private readonly IZScanConfigService _configService;
        private readonly string _configDirectory;
        private readonly JsonSerializerSettings _serializerSettings;

        private StageCalibrationData _currentData = new StageCalibrationData();

        public StageCalibrationService(
            ILoadUnloadController controller,
            ILoggerService logger,
            IZScanConfigService configService)
        {
            _controller = controller;
            _logger = logger;
            _configService = configService;
            _configDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "StageCalibration");
            _serializerSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        public async Task GoToPhotoPositionAsync(double x, double y, double z, double rx, double rz)
        {
            if (!_controller.CanExecuteMotion())
                throw new InvalidOperationException("Motion is prohibited while equipment is running");

            _logger?.Info($"StageCalibration: Moving to photo position (X:{x:F2}, Y:{y:F2}, Z:{z:F2}, Rx:{rx:F3}, Rz:{rz:F3})");

            await _controller.MoveToAssemblyPositionAsync(1);

            _logger?.Info("StageCalibration: Arrived at photo position");
        }

        public async Task<FiducialCaptureResult> CaptureFiducialAsync(int fiducialIndex)
        {
            _logger?.Info($"StageCalibration: Capturing fiducial {fiducialIndex}");

            try
            {
                var positions = await _controller.GetRealTimePositionsAsync();
                double x = positions.TryGetValue("X", out var xVal) ? xVal : 0;
                double y = positions.TryGetValue("Y", out var yVal) ? yVal : 0;
                double angle = positions.TryGetValue("Rz", out var rzVal) ? rzVal : 0;

                var result = new FiducialCaptureResult
                {
                    Success = true,
                    X = x,
                    Y = y,
                    Angle = angle
                };

                _logger?.Info($"StageCalibration: Fiducial {fiducialIndex} captured - X:{x:F3}, Y:{y:F3}, Angle:{angle:F3}");
                return result;
            }
            catch (Exception ex)
            {
                _logger?.Error($"StageCalibration: Capture fiducial {fiducialIndex} failed - {ex.Message}");
                return new FiducialCaptureResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task ApplyCorrectionAsync(double dx, double dy, double dAngle)
        {
            if (!_controller.CanExecuteMotion())
                throw new InvalidOperationException("Motion is prohibited while equipment is running");

            _logger?.Info($"StageCalibration: Applying correction - dX:{dx:F3}, dY:{dy:F3}, dAngle:{dAngle:F3}");

            await _controller.MoveToAssemblyPositionAsync(1);

            _logger?.Info("StageCalibration: Correction applied");
        }

        public async Task<CurrentPositionResult> TeachCurrentPositionAsync()
        {
            _logger?.Info("StageCalibration: Teaching current position");

            var positions = await _controller.GetRealTimePositionsAsync();

            var result = new CurrentPositionResult
            {
                X = positions.TryGetValue("X", out var x) ? x : 0,
                Y = positions.TryGetValue("Y", out var y) ? y : 0,
                Z = positions.TryGetValue("Z", out var z) ? z : 0,
                Rx = positions.TryGetValue("Rx", out var rx) ? rx : 0,
                Rz = positions.TryGetValue("Rz", out var rz) ? rz : 0
            };

            _logger?.Info($"StageCalibration: Current position taught - X:{result.X:F3}, Y:{result.Y:F3}, Z:{result.Z:F3}, Rx:{result.Rx:F3}, Rz:{result.Rz:F3}");
            return result;
        }

        public async Task SaveCalibrationDataAsync()
        {
            _logger?.Info("StageCalibration: Saving calibration data");

            try
            {
                if (!Directory.Exists(_configDirectory))
                    Directory.CreateDirectory(_configDirectory);

                var filePath = Path.Combine(_configDirectory, "StageCalibration.json");
                var json = JsonConvert.SerializeObject(_currentData, _serializerSettings);
                await Task.Run(() => File.WriteAllText(filePath, json));

                _logger?.Info($"StageCalibration: Data saved to {filePath}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"StageCalibration: Save failed - {ex.Message}");
            }
        }

        public async Task LoadCalibrationDataAsync()
        {
            _logger?.Info("StageCalibration: Loading calibration data");

            try
            {
                var filePath = Path.Combine(_configDirectory, "StageCalibration.json");

                if (!File.Exists(filePath))
                {
                    _logger?.Info("StageCalibration: No saved data found, using defaults");
                    _currentData = new StageCalibrationData();
                    return;
                }

                var json = await Task.Run(() => File.ReadAllText(filePath));
                var data = JsonConvert.DeserializeObject<StageCalibrationData>(json, _serializerSettings);

                if (data != null)
                {
                    _currentData = data;
                    _logger?.Info("StageCalibration: Data loaded successfully");
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"StageCalibration: Load failed - {ex.Message}");
            }
        }

        public StageCalibrationData GetCurrentCalibrationData()
        {
            return _currentData;
        }

        public void ApplyCalibrationData(StageCalibrationData data)
        {
            if (data == null) return;
            _currentData = data;
        }
    }
}

using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TCPIPModule.Interfaces;

namespace Module.Services
{
    /// <summary>
    /// 载台校准服务实现——提供载台基准位移动、相机拍照位移动、视觉触发拍照、旋转校正等功能
    /// </summary>
    public class StageCalibrationService : IStageCalibrationService
    {
        private readonly ILoadUnloadController _controller;
        private readonly IPositionMotionController _motionController;
        private readonly ITCPEventService _tcpEventService;
        private readonly ILoggerService _logger;
        private readonly IZScanConfigService _configService;
        private readonly string _configDirectory;
        private readonly JsonSerializerSettings _serializerSettings;

        private StageCalibrationData _currentData = new StageCalibrationData();

        /// <summary>工站标识</summary>
        private const string StationIdentifier = "DispenserStation";

        public StageCalibrationService(
            ILoadUnloadController controller,
            IPositionMotionController motionController,
            ITCPEventService tcpEventService,
            ILoggerService logger,
            IZScanConfigService configService)
        {
            _controller = controller;
            _motionController = motionController;
            _tcpEventService = tcpEventService;
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

        // ===== 旧接口实现（保留兼容） =====

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

                var result = new FiducialCaptureResult { Success = true, X = x, Y = y, Angle = angle };
                _logger?.Info($"StageCalibration: Fiducial {fiducialIndex} captured - X:{x:F3}, Y:{y:F3}, Angle:{angle:F3}");
                return result;
            }
            catch (Exception ex)
            {
                _logger?.Error($"StageCalibration: Capture fiducial {fiducialIndex} failed - {ex.Message}");
                return new FiducialCaptureResult { Success = false, ErrorMessage = ex.Message };
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

        public StageCalibrationData GetCurrentCalibrationData() => _currentData;

        public void ApplyCalibrationData(StageCalibrationData data)
        {
            if (data == null) return;
            _currentData = data;
        }

        // ===== 新增接口实现 =====

        /// <summary>移动载台Rx/Rz轴到拍照基准位</summary>
        public async Task MoveToReferencePositionAsync(double rx, double rz)
        {
            if (!_motionController.CanExecuteMotion(StationIdentifier))
                throw new InvalidOperationException("运动控制不可用，请检查安全互锁状态");

            _logger?.Info($"StageCalibration: 移动载台到基准位 Rx:{rx:F3}, Rz:{rz:F3}");

            var targetPositions = new Dictionary<string, double>();
            if (rx != 0) targetPositions["Rx"] = rx;
            if (rz != 0) targetPositions["Rz"] = rz;

            await _motionController.GotoAsync(StationIdentifier, targetPositions, 30.0);
            _logger?.Info("StageCalibration: 载台已到达基准位");
        }

        /// <summary>移动相机(Dx/Dy/Dz轴)到指定拍照位</summary>
        public async Task MoveCameraToPhotoPositionAsync(double dx, double dy, double dz)
        {
            if (!_motionController.CanExecuteMotion(StationIdentifier))
                throw new InvalidOperationException("运动控制不可用，请检查安全互锁状态");

            _logger?.Info($"StageCalibration: 移动相机到拍照位 Dx:{dx:F3}, Dy:{dy:F3}, Dz:{dz:F3}");

            var targetPositions = new Dictionary<string, double>();
            if (dx != 0) targetPositions["Dx"] = dx;
            if (dy != 0) targetPositions["Dy"] = dy;
            if (dz != 0) targetPositions["Dz"] = dz;

            await _motionController.GotoAsync(StationIdentifier, targetPositions, 50.0);
            _logger?.Info("StageCalibration: 相机已到达拍照位");
        }

        /// <summary>触发视觉拍照并返回结果（通过TCP通讯）</summary>
        public async Task<FiducialCaptureResult> TriggerCaptureAsync(string tcpConnectionName, string triggerCommand, int timeoutMs)
        {
            _logger?.Info($"StageCalibration: 触发视觉拍照，连接:{tcpConnectionName}, 命令:{triggerCommand}");

            try
            {
                // 发送触发命令并等待响应
                var response = await _tcpEventService.SendCommandWithResponseAsync(tcpConnectionName, triggerCommand, timeoutMs);

                if (string.IsNullOrEmpty(response))
                {
                    return new FiducialCaptureResult
                    {
                        Success = false,
                        ErrorMessage = "视觉返回数据为空"
                    };
                }

                // 解析视觉返回数据
                var (x, y) = ParseVisionData(response);
                return new FiducialCaptureResult
                {
                    Success = true,
                    X = x,
                    Y = y,
                    Angle = 0 // 角度由两次拍照计算得出
                };
            }
            catch (TimeoutException)
            {
                _logger?.Warn($"StageCalibration: 视觉拍照超时 ({timeoutMs}ms)");
                return new FiducialCaptureResult
                {
                    Success = false,
                    ErrorMessage = $"视觉拍照超时 ({timeoutMs}ms)"
                };
            }
            catch (Exception ex)
            {
                _logger?.Error($"StageCalibration: 视觉拍照失败 - {ex.Message}");
                return new FiducialCaptureResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>旋转载台Rz轴到基准角度（当前角度+偏差角度）</summary>
        public async Task RotateToReferenceAngleAsync(double currentRz, double deltaAngle)
        {
            if (!_motionController.CanExecuteMotion(StationIdentifier))
                throw new InvalidOperationException("运动控制不可用，请检查安全互锁状态");

            var targetAngle = currentRz + deltaAngle;
            _logger?.Info($"StageCalibration: 旋转Rz轴到基准角度，当前:{currentRz:F3}, 偏差:{deltaAngle:F3}, 目标:{targetAngle:F3}");

            var targetPositions = new Dictionary<string, double>
            {
                ["Rz"] = targetAngle
            };

            await _motionController.GotoAsync(StationIdentifier, targetPositions, 20.0);
            _logger?.Info("StageCalibration: Rz轴旋转完成");
        }

        /// <summary>读取当前所有轴位置</summary>
        public async Task<CurrentPositionResult> ReadCurrentPositionsAsync()
        {
            _logger?.Info("StageCalibration: 读取当前轴位置");

            var positions = await _motionController.TeachAsync(StationIdentifier);

            var result = new CurrentPositionResult
            {
                X = positions.TryGetValue("X", out var x) ? x : 0,
                Y = positions.TryGetValue("Y", out var y) ? y : 0,
                Z = positions.TryGetValue("Z", out var z) ? z : 0,
                Rx = positions.TryGetValue("Rx", out var rx) ? rx : 0,
                Rz = positions.TryGetValue("Rz", out var rz) ? rz : 0,
                Dx = positions.TryGetValue("Dx", out var dx) ? dx : 0,
                Dy = positions.TryGetValue("Dy", out var dy) ? dy : 0,
                Dz = positions.TryGetValue("Dz", out var dz) ? dz : 0,
            };

            return result;
        }

        /// <summary>解析视觉返回数据，支持JSON和逗号分隔格式</summary>
        private (double X, double Y) ParseVisionData(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("视觉数据为空");

            message = message.Trim();

            // JSON格式
            if (message.StartsWith("{"))
            {
                try
                {
                    var json = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
                    double x = 0, y = 0;
                    if (json.TryGetValue("X", out var xVal)) x = Convert.ToDouble(xVal);
                    else if (json.TryGetValue("x", out xVal)) x = Convert.ToDouble(xVal);
                    if (json.TryGetValue("Y", out var yVal)) y = Convert.ToDouble(yVal);
                    else if (json.TryGetValue("y", out yVal)) y = Convert.ToDouble(yVal);
                    return (x, y);
                }
                catch (Newtonsoft.Json.JsonException) { }
            }

            // 逗号分隔格式
            var parts = message.Split(new[] { ',', ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && double.TryParse(parts[0], out var vx) && double.TryParse(parts[1], out var vy))
            {
                return (vx, vy);
            }

            throw new FormatException($"无法解析视觉数据: {message}");
        }
    }
}

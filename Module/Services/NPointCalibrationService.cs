using Core.Abstraction;
using Core.Models;
using Core.Services;
using Core.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCPIPModule.Interfaces;

namespace Module.Services
{
    /// <summary>
    /// N点标定服务实现——提供自动标定流程、单点示教/移动、TCP视觉数据接收、仿射计算
    /// 自动标定流程：移动到点位 -> 示教机械坐标 -> 触发视觉 -> 等待数据 -> 填充 -> 延时 -> 下一点
    /// </summary>
    public class NPointCalibrationService : INPointCalibrationService
    {
        private readonly IPositionMotionController _motionController;
        private readonly ITCPEventService _tcpEventService;
        private readonly ILoggerService _logger;

        private const string StationIdentifier = "DispenserStation";

        /// <summary>自动标定取消令牌源</summary>
        private CancellationTokenSource? _autoCalibCts;

        /// <summary>TCP数据接收事件处理器引用，用于取消订阅</summary>
        private Action<string, string>? _cameraDataHandler;

        /// <summary>当前等待视觉数据的TaskCompletionSource</summary>
        private TaskCompletionSource<(double X, double Y)>? _visionDataTcs;

        /// <summary>当前订阅的连接名称</summary>
        private string? _subscribedConnectionName;

        /// <summary>是否正在自动标定中</summary>
        public bool IsAutoCalibrating => _autoCalibCts != null && !_autoCalibCts.IsCancellationRequested;

        /// <summary>单点标定完成事件</summary>
        public event Action<int, NPointCalibrationPoint>? PointCalibrated;

        /// <summary>视觉数据到达事件</summary>
        public event Action<NPointCalibrationPoint>? VisionDataReceived;

        /// <summary>全部标定完成事件</summary>
        public event Action<AffineCalibrationResult>? CalibrationCompleted;

        /// <summary>标定错误事件</summary>
        public event Action<string>? CalibrationError;

        public NPointCalibrationService(
            IPositionMotionController motionController,
            ITCPEventService tcpEventService,
            ILoggerService logger)
        {
            _motionController = motionController;
            _tcpEventService = tcpEventService;
            _logger = logger;
        }

        /// <summary>
        /// 启动自动标定流程
        /// 依次移动到各点位 -> 示教机械坐标 -> 触发视觉 -> 等待数据 -> 填充 -> 延时 -> 下一点
        /// </summary>
        public async Task StartAutoCalibrationAsync(
            IList<NPointCalibrationPoint> points,
            int delayMs,
            bool enableVisionData,
            string tcpConnectionName,
            string triggerCommand,
            CancellationToken ct)
        {
            if (IsAutoCalibrating)
                throw new InvalidOperationException("自动标定正在进行中");

            _autoCalibCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            try
            {
                // 订阅视觉数据
                if (enableVisionData && !string.IsNullOrEmpty(tcpConnectionName))
                {
                    SubscribeVisionData(tcpConnectionName);
                }

                for (int i = 0; i < points.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var point = points[i];

                    // 1. 示教当前位置（读取机械坐标）
                    var teachResult = await TeachPointAsync(i);
                    point.MachineX = teachResult.MachineX;
                    point.MachineY = teachResult.MachineY;

                    // 2. 触发视觉并等待数据
                    if (enableVisionData && !string.IsNullOrEmpty(tcpConnectionName))
                    {
                        // 发送触发命令
                        if (!string.IsNullOrEmpty(triggerCommand))
                        {
                            try
                            {
                                await _tcpEventService.SendCommandAsync(tcpConnectionName, triggerCommand);
                            }
                            catch (Exception ex)
                            {
                                _logger.Warn($"N点标定: 发送触发命令失败 - {ex.Message}");
                            }
                        }

                        // 等待视觉数据返回
                        _visionDataTcs = new TaskCompletionSource<(double X, double Y)>();
                        try
                        {
                            var timeoutTask = Task.Delay(5000, ct);
                            var completedTask = await Task.WhenAny(_visionDataTcs.Task, timeoutTask);

                            if (completedTask == _visionDataTcs.Task)
                            {
                                var visionData = await _visionDataTcs.Task;
                                point.VisionX = visionData.X;
                                point.VisionY = visionData.Y;
                            }
                            else
                            {
                                _logger.Warn($"N点标定: 点位 {point.Name} 等待视觉数据超时");
                            }
                        }
                        finally
                        {
                            _visionDataTcs = null;
                        }
                    }

                    // 3. 标记已标定
                    point.IsCalibrated = true;
                    PointCalibrated?.Invoke(i, point);

                    // 4. 延时
                    if (delayMs > 0 && i < points.Count - 1)
                    {
                        await Task.Delay(delayMs, ct);
                    }
                }

                // 5. 计算仿射标定结果
                var calibratedPoints = points.Where(p => p.IsCalibrated).ToList();
                if (calibratedPoints.Count >= 3)
                {
                    var result = ComputeCalibration(calibratedPoints);
                    CalibrationCompleted?.Invoke(result);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info("N点标定: 自动标定已取消");
                CalibrationError?.Invoke("自动标定已取消");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "N点标定: 自动标定异常");
                CalibrationError?.Invoke($"自动标定异常: {ex.Message}");
            }
            finally
            {
                UnsubscribeVisionData();
                _autoCalibCts?.Dispose();
                _autoCalibCts = null;
            }
        }

        /// <summary>停止自动标定</summary>
        public void StopAutoCalibration()
        {
            _autoCalibCts?.Cancel();
        }

        /// <summary>示教指定点位的机械坐标（读取当前轴位置）</summary>
        public async Task<NPointCalibrationPoint> TeachPointAsync(int pointIndex)
        {
            if (!_motionController.CanExecuteMotion(StationIdentifier))
                throw new InvalidOperationException("运动控制不可用，请检查安全互锁状态");

            var positions = await _motionController.TeachAsync(StationIdentifier);

            var point = new NPointCalibrationPoint
            {
                Index = pointIndex,
                MachineX = positions.TryGetValue("X", out var x) ? x : 0,
                MachineY = positions.TryGetValue("Y", out var y) ? y : 0,
            };

            return point;
        }

        /// <summary>移动到指定点位的机械坐标</summary>
        public async Task MoveToPointAsync(NPointCalibrationPoint point)
        {
            if (!_motionController.CanExecuteMotion(StationIdentifier))
                throw new InvalidOperationException("运动控制不可用，请检查安全互锁状态");

            var targetPositions = new Dictionary<string, double>();
            if (point.MachineX != 0) targetPositions["X"] = point.MachineX;
            if (point.MachineY != 0) targetPositions["Y"] = point.MachineY;

            await _motionController.GotoAsync(StationIdentifier, targetPositions, 50.0);
        }

        /// <summary>订阅TCP视觉数据</summary>
        public void SubscribeVisionData(string connectionName)
        {
            UnsubscribeVisionData();

            _subscribedConnectionName = connectionName;
            _cameraDataHandler = (cameraName, message) =>
            {
                // 仅处理匹配连接名的数据
                if (cameraName != connectionName) return;

                try
                {
                    var (x, y) = ParseVisionData(message);
                    _visionDataTcs?.TrySetResult((x, y));

                    // 通知ViewModel视觉数据到达
                    VisionDataReceived?.Invoke(new NPointCalibrationPoint
                    {
                        VisionX = x,
                        VisionY = y
                    });
                }
                catch (Exception ex)
                {
                    _logger.Warn($"N点标定: 解析视觉数据失败 - {ex.Message}, 原始数据: {message}");
                }
            };

            _tcpEventService.CameraMessageReceived += _cameraDataHandler;
            _logger.Info($"N点标定: 已订阅TCP视觉数据，连接名: {connectionName}");
        }

        /// <summary>取消订阅TCP视觉数据</summary>
        public void UnsubscribeVisionData()
        {
            if (_cameraDataHandler != null)
            {
                _tcpEventService.CameraMessageReceived -= _cameraDataHandler;
                _cameraDataHandler = null;
                _subscribedConnectionName = null;
                _logger.Info("N点标定: 已取消订阅TCP视觉数据");
            }
        }

        /// <summary>计算仿射标定结果（N点最小二乘法，>=3点）</summary>
        public AffineCalibrationResult ComputeCalibration(IList<NPointCalibrationPoint> points)
        {
            var calibratedPoints = points.Where(p => p.IsCalibrated).ToList();
            if (calibratedPoints.Count < 3)
                throw new ArgumentException("标定至少需要3个已标定的点");

            // 视觉坐标作为CAD坐标（输入），机械坐标作为输出
            var cadPoints = calibratedPoints.Select(p => (p.VisionX, p.VisionY)).ToList();
            var machinePoints = calibratedPoints.Select(p => (p.MachineX, p.MachineY)).ToList();

            return AffineCalibrationService.Solve(cadPoints, machinePoints);
        }

        /// <summary>
        /// 解析视觉返回数据，支持JSON和逗号分隔两种格式
        /// JSON: {"X": 123.45, "Y": 67.89}
        /// 逗号分隔: "123.45,67.89"
        /// </summary>
        private (double X, double Y) ParseVisionData(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("视觉数据为空");

            message = message.Trim();

            // 尝试JSON格式解析
            if (message.StartsWith("{"))
            {
                try
                {
                    var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
                    double x = 0, y = 0;

                    // 尝试多种键名：X/x/PosX/px, Y/y/PosY/py
                    if (json.TryGetValue("X", out var xVal)) x = Convert.ToDouble(xVal);
                    else if (json.TryGetValue("x", out xVal)) x = Convert.ToDouble(xVal);
                    else if (json.TryGetValue("PosX", out xVal)) x = Convert.ToDouble(xVal);
                    else if (json.TryGetValue("px", out xVal)) x = Convert.ToDouble(xVal);

                    if (json.TryGetValue("Y", out var yVal)) y = Convert.ToDouble(yVal);
                    else if (json.TryGetValue("y", out yVal)) y = Convert.ToDouble(yVal);
                    else if (json.TryGetValue("PosY", out yVal)) y = Convert.ToDouble(yVal);
                    else if (json.TryGetValue("py", out yVal)) y = Convert.ToDouble(yVal);

                    return (x, y);
                }
                catch (JsonException)
                {
                    // JSON解析失败，尝试逗号分隔
                }
            }

            // 尝试逗号分隔格式
            var parts = message.Split(new[] { ',', ' ', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && double.TryParse(parts[0], out var vx) && double.TryParse(parts[1], out var vy))
            {
                return (vx, vy);
            }

            throw new FormatException($"无法解析视觉数据: {message}");
        }
    }
}

using Core.Abstraction;
using Core.Models;
using Core.Services;
using Core.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCPIPModule.Interfaces;

namespace Module.Services
{
    /// <summary>
    /// 双龙门标定服务实现——提供双龙门独立仿射标定、公共基准点采集、跨龙门Y基准对齐功能
    /// 机构特点：龙门1(Dx+Dy独立) + 龙门2(X2+共用Y) + 双上相机(Cam1/Cam2)
    /// 自动标定流程：移动到点位 → 发送触发命令 → 读取当前位置 → 等待视觉数据 → 填充 → 标记已标定 → 延时 → 下一点 → 全部完成后计算仿射
    /// 线程安全：使用 lock 保护共享资源（CTS/TCS/事件处理器）
    /// 急停支持：CancellationTokenSource.CreateLinkedTokenSource(ct) 关联外部取消令牌
    /// </summary>
    public class DualGantryCalibrationService : IDualGantryCalibrationService
    {
        private readonly IPositionMotionController _motionController;
        private readonly ITCPEventService _tcpEventService;
        private readonly ILoggerService _logger;

        /// <summary>线程同步锁，保护共享资源（CTS/TCS/事件处理器/连接名）</summary>
        private readonly object _lock = new object();

        // ===== 龙门1独立资源 =====
        /// <summary>龙门1自动标定取消令牌源</summary>
        private CancellationTokenSource? _gantry1Cts;
        /// <summary>龙门1视觉数据等待TaskCompletionSource</summary>
        private TaskCompletionSource<(double X, double Y)>? _gantry1VisionTcs;
        /// <summary>龙门1 TCP数据接收事件处理器引用，用于取消订阅</summary>
        private Action<string, string>? _gantry1Handler;
        /// <summary>龙门1当前订阅的连接名称</summary>
        private string? _gantry1ConnectionName;

        // ===== 龙门2独立资源 =====
        /// <summary>龙门2自动标定取消令牌源</summary>
        private CancellationTokenSource? _gantry2Cts;
        /// <summary>龙门2视觉数据等待TaskCompletionSource</summary>
        private TaskCompletionSource<(double X, double Y)>? _gantry2VisionTcs;
        /// <summary>龙门2 TCP数据接收事件处理器引用，用于取消订阅</summary>
        private Action<string, string>? _gantry2Handler;
        /// <summary>龙门2当前订阅的连接名称</summary>
        private string? _gantry2ConnectionName;

        /// <summary>是否正在自动标定中（龙门1或龙门2任一在标定即为true）</summary>
        public bool IsAutoCalibrating
        {
            get
            {
                lock (_lock)
                {
                    bool g1 = _gantry1Cts != null && !_gantry1Cts.IsCancellationRequested;
                    bool g2 = _gantry2Cts != null && !_gantry2Cts.IsCancellationRequested;
                    return g1 || g2;
                }
            }
        }

        /// <summary>单点标定完成事件：(龙门编号, 点序号, 标定点数据)</summary>
        public event Action<int, int, DualGantryCalibrationPoint>? PointCalibrated;

        /// <summary>视觉数据到达事件：(龙门编号, 视觉X, 视觉Y)</summary>
        public event Action<int, double, double>? VisionDataReceived;

        /// <summary>单龙门标定完成事件：(龙门编号, 仿射结果)</summary>
        public event Action<int, AffineCalibrationResult>? GantryCalibrationCompleted;

        /// <summary>公共基准点采集完成事件</summary>
        public event Action<CommonReferencePoint>? CommonReferenceCaptured;

        /// <summary>跨龙门对齐完成事件</summary>
        public event Action<GantryTransform>? GantryTransformComputed;

        /// <summary>最近一次计算的跨龙门变换参数缓存（供其他模块查询）</summary>
        private GantryTransform? _cachedGantryTransform;

        /// <summary>标定错误事件：(龙门编号, 错误信息) gantryId=0 表示通用错误</summary>
        public event Action<int, string>? CalibrationError;

        /// <summary>
        /// 构造函数——注入运动控制器、TCP事件服务、日志服务
        /// </summary>
        public DualGantryCalibrationService(
            IPositionMotionController motionController,
            ITCPEventService tcpEventService,
            ILoggerService logger)
        {
            _motionController = motionController;
            _tcpEventService = tcpEventService;
            _logger = logger;
        }

        /// <summary>
        /// 启动指定龙门的自动标定流程
        /// 流程：移动到点位 → 发送触发命令 → 读取当前位置 → 等待视觉数据 → 填充 → 标记已标定 → 延时 → 下一点 → 全部完成后计算仿射
        /// </summary>
        public async Task StartAutoCalibrationAsync(
            int gantryId,
            IList<DualGantryCalibrationPoint> points,
            DualGantryCalibrationConfig config,
            CancellationToken ct)
        {
            ValidateGantryId(gantryId);

            // 检查该龙门是否已在标定中
            lock (_lock)
            {
                if (gantryId == 1 && _gantry1Cts != null && !_gantry1Cts.IsCancellationRequested)
                    throw new InvalidOperationException("龙门1自动标定正在进行中");
                if (gantryId == 2 && _gantry2Cts != null && !_gantry2Cts.IsCancellationRequested)
                    throw new InvalidOperationException("龙门2自动标定正在进行中");
            }

            // 创建关联取消令牌（支持外部急停取消）
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var (axisX, axisY, tcpConnection, triggerCommand) = GetGantryConfig(gantryId, config);

            // 设置该龙门的CTS
            lock (_lock)
            {
                if (gantryId == 1) _gantry1Cts = linkedCts;
                else _gantry2Cts = linkedCts;
            }

            try
            {
                _logger.Info($"双龙门标定: 启动龙门{gantryId}自动标定，共 {points.Count} 个点位");

                // 订阅视觉数据
                if (config.EnableVisionData && !string.IsNullOrEmpty(tcpConnection))
                {
                    SubscribeVisionData(gantryId, tcpConnection);
                }

                for (int i = 0; i < points.Count; i++)
                {
                    linkedCts.Token.ThrowIfCancellationRequested();
                    var point = points[i];

                    // 1. 移动到预定义机械点位（如果已设置机械坐标）
                    if (point.MachineX != 0 || point.MachineY != 0)
                    {
                        await MoveToPointAsync(gantryId, point, config);
                    }

                    // 2. 拍照：发送触发命令
                    if (config.EnableVisionData && !string.IsNullOrEmpty(tcpConnection) && !string.IsNullOrEmpty(triggerCommand))
                    {
                        try
                        {
                            await _tcpEventService.SendCommandAsync(tcpConnection, triggerCommand);
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn($"双龙门标定: 龙门{gantryId}发送触发命令失败 - {ex.Message}");
                        }
                    }

                    // 3. 读取当前位置（移动到位后重新读取实际位置）
                    var teachResult = await TeachPointAsync(gantryId, config);
                    point.MachineX = teachResult.MachineX;
                    point.MachineY = teachResult.MachineY;

                    // 4. 等待视觉数据返回
                    if (config.EnableVisionData && !string.IsNullOrEmpty(tcpConnection))
                    {
                        var tcs = new TaskCompletionSource<(double X, double Y)>();
                        lock (_lock)
                        {
                            if (gantryId == 1) _gantry1VisionTcs = tcs;
                            else _gantry2VisionTcs = tcs;
                        }

                        try
                        {
                            var timeoutTask = Task.Delay(5000, linkedCts.Token);
                            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                            if (completedTask == tcs.Task)
                            {
                                var visionData = await tcs.Task;
                                point.VisionX = visionData.X;
                                point.VisionY = visionData.Y;
                            }
                            else
                            {
                                _logger.Warn($"双龙门标定: 龙门{gantryId}点位 {point.Name} 等待视觉数据超时");
                            }
                        }
                        finally
                        {
                            lock (_lock)
                            {
                                if (gantryId == 1) _gantry1VisionTcs = null;
                                else _gantry2VisionTcs = null;
                            }
                        }
                    }

                    // 5. 标记已标定
                    point.IsCalibrated = true;
                    PointCalibrated?.Invoke(gantryId, i, point);

                    // 6. 延时
                    if (config.AutoCalibDelayMs > 0 && i < points.Count - 1)
                    {
                        await Task.Delay(config.AutoCalibDelayMs, linkedCts.Token);
                    }
                }

                // 7. 计算仿射标定结果
                var calibratedPoints = points.Where(p => p.IsCalibrated).ToList();
                if (calibratedPoints.Count >= 3)
                {
                    var result = ComputeCalibration(calibratedPoints);
                    GantryCalibrationCompleted?.Invoke(gantryId, result);
                    _logger.Info($"双龙门标定: 龙门{gantryId}标定完成，RMS={result.RmsError}mm，点数={result.PointCount}");
                }
                else
                {
                    _logger.Warn($"双龙门标定: 龙门{gantryId}已标定点数不足3个，无法计算仿射");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"双龙门标定: 龙门{gantryId}自动标定已取消");
                CalibrationError?.Invoke(gantryId, "自动标定已取消");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"双龙门标定: 龙门{gantryId}自动标定异常");
                CalibrationError?.Invoke(gantryId, $"自动标定异常: {ex.Message}");
            }
            finally
            {
                // 清理该龙门的CTS
                lock (_lock)
                {
                    if (gantryId == 1)
                    {
                        _gantry1Cts?.Dispose();
                        _gantry1Cts = null;
                    }
                    else
                    {
                        _gantry2Cts?.Dispose();
                        _gantry2Cts = null;
                    }
                }
            }
        }

        /// <summary>停止自动标定（取消所有龙门的标定流程）</summary>
        public void StopAutoCalibration()
        {
            lock (_lock)
            {
                _gantry1Cts?.Cancel();
                _gantry2Cts?.Cancel();
            }
            _logger.Info("双龙门标定: 已停止所有自动标定流程");
        }

        /// <summary>
        /// 示教指定龙门的单点机械坐标（读取当前轴位置）
        /// 龙门1: 读取 Gantry1AxisX + Gantry1AxisY
        /// 龙门2: 读取 Gantry2AxisX + CommonAxisY
        /// </summary>
        public async Task<DualGantryCalibrationPoint> TeachPointAsync(int gantryId, DualGantryCalibrationConfig config)
        {
            ValidateGantryId(gantryId);
            var (axisX, axisY, _, _) = GetGantryConfig(gantryId, config);

            if (!_motionController.CanExecuteMotion(config.StationIdentifier))
                throw new InvalidOperationException("运动控制不可用，请检查安全互锁状态");

            var positions = await _motionController.TeachAsync(config.StationIdentifier);

            var point = new DualGantryCalibrationPoint
            {
                MachineX = positions.TryGetValue(axisX, out var x) ? x : 0,
                MachineY = positions.TryGetValue(axisY, out var y) ? y : 0,
                IsTaught = true
            };

            return point;
        }

        /// <summary>
        /// 移动到指定龙门的单点机械坐标
        /// 龙门1: 移动 Gantry1AxisX + Gantry1AxisY
        /// 龙门2: 移动 Gantry2AxisX + CommonAxisY
        /// </summary>
        public async Task MoveToPointAsync(int gantryId, DualGantryCalibrationPoint point, DualGantryCalibrationConfig config)
        {
            ValidateGantryId(gantryId);
            var (axisX, axisY, _, _) = GetGantryConfig(gantryId, config);

            if (!_motionController.CanExecuteMotion(config.StationIdentifier))
                throw new InvalidOperationException("运动控制不可用，请检查安全互锁状态");

            var targetPositions = new Dictionary<string, double>
            {
                [axisX] = point.MachineX,
                [axisY] = point.MachineY
            };

            await _motionController.GotoAsync(config.StationIdentifier, targetPositions, 50.0);
        }

        /// <summary>
        /// 订阅指定龙门的TCP视觉数据
        /// 龙门1: 订阅 Gantry1TcpConnection
        /// 龙门2: 订阅 Gantry2TcpConnection
        /// </summary>
        public void SubscribeVisionData(int gantryId, string connectionName)
        {
            ValidateGantryId(gantryId);

            // 先取消该龙门的旧订阅
            UnsubscribeVisionData(gantryId);

            if (string.IsNullOrEmpty(connectionName))
            {
                _logger.Warn($"双龙门标定: 龙门{gantryId}订阅连接名为空，跳过订阅");
                return;
            }

            // 创建事件处理器（捕获gantryId和connectionName）
            Action<string, string> handler = (cameraName, message) =>
            {
                // 仅处理匹配连接名的数据
                if (cameraName != connectionName) return;

                try
                {
                    var (x, y) = ParseVisionData(message);

                    // 设置对应龙门的视觉数据TCS
                    lock (_lock)
                    {
                        if (gantryId == 1)
                            _gantry1VisionTcs?.TrySetResult((x, y));
                        else
                            _gantry2VisionTcs?.TrySetResult((x, y));
                    }

                    // 通知视觉数据到达
                    VisionDataReceived?.Invoke(gantryId, x, y);
                }
                catch (Exception ex)
                {
                    _logger.Warn($"双龙门标定: 龙门{gantryId}解析视觉数据失败 - {ex.Message}, 原始数据: {message}");
                }
            };

            lock (_lock)
            {
                if (gantryId == 1)
                {
                    _gantry1Handler = handler;
                    _gantry1ConnectionName = connectionName;
                }
                else
                {
                    _gantry2Handler = handler;
                    _gantry2ConnectionName = connectionName;
                }
            }

            _tcpEventService.CameraMessageReceived += handler;
            _logger.Info($"双龙门标定: 龙门{gantryId}已订阅TCP视觉数据，连接名: {connectionName}");
        }

        /// <summary>取消订阅指定龙门的TCP视觉数据</summary>
        public void UnsubscribeVisionData(int gantryId)
        {
            ValidateGantryId(gantryId);

            Action<string, string>? handler;
            string? connectionName;

            lock (_lock)
            {
                if (gantryId == 1)
                {
                    handler = _gantry1Handler;
                    connectionName = _gantry1ConnectionName;
                    _gantry1Handler = null;
                    _gantry1ConnectionName = null;
                }
                else
                {
                    handler = _gantry2Handler;
                    connectionName = _gantry2ConnectionName;
                    _gantry2Handler = null;
                    _gantry2ConnectionName = null;
                }
            }

            if (handler != null)
            {
                _tcpEventService.CameraMessageReceived -= handler;
                _logger.Info($"双龙门标定: 龙门{gantryId}已取消订阅TCP视觉数据，连接名: {connectionName}");
            }
        }

        /// <summary>取消所有TCP订阅（龙门1和龙门2）</summary>
        public void UnsubscribeAllVisionData()
        {
            UnsubscribeVisionData(1);
            UnsubscribeVisionData(2);
        }

        /// <summary>
        /// 计算指定龙门的仿射标定结果（N点最小二乘法，>=3点）
        /// 视觉坐标作为CAD坐标（输入），机械坐标作为输出
        /// </summary>
        public AffineCalibrationResult ComputeCalibration(IList<DualGantryCalibrationPoint> points)
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
        /// 采集Cam1公共基准数据（Y轴在Cam1视野内时调用）
        /// 流程：读取当前共用Y位置 → 触发Cam1拍照 → 等待视觉数据 → 返回采集结果
        /// </summary>
        public async Task<(double CommonY1, double VisionX, double VisionY)> CaptureReferenceGantry1Async(
            DualGantryCalibrationConfig config, CancellationToken ct)
        {
            // 1. 确保龙门1视觉订阅已激活
            lock (_lock)
            {
                if (_gantry1Handler == null && !string.IsNullOrEmpty(config.Gantry1TcpConnection))
                    SubscribeVisionData(1, config.Gantry1TcpConnection);
            }

            // 2. 读取当前共用Y轴位置（Cam1视野内的Y坐标）
            if (!_motionController.CanExecuteMotion(config.StationIdentifier))
                throw new InvalidOperationException("运动控制不可用，请检查安全互锁状态");

            var positions = await _motionController.TeachAsync(config.StationIdentifier);
            double commonY1 = positions.TryGetValue(config.CommonAxisY, out var yVal) ? yVal : 0;

            // 3. 设置龙门1视觉数据等待
            var tcs = new TaskCompletionSource<(double X, double Y)>();
            lock (_lock)
            {
                _gantry1VisionTcs = tcs;
            }

            try
            {
                _logger.Info($"双龙门标定: 采集Cam1公共基准，CommonY1={commonY1}");

                // 4. 触发Cam1拍照
                if (!string.IsNullOrEmpty(config.Gantry1TcpConnection) && !string.IsNullOrEmpty(config.Gantry1TriggerCommand))
                {
                    try
                    {
                        await _tcpEventService.SendCommandAsync(config.Gantry1TcpConnection, config.Gantry1TriggerCommand);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"双龙门标定: 触发Cam1失败 - {ex.Message}");
                    }
                }

                // 5. 等待视觉数据返回（超时5秒）
                var timeoutTask = Task.Delay(5000, ct);
                var completed = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completed == tcs.Task)
                {
                    var visionData = await tcs.Task;
                    _logger.Info($"双龙门标定: Cam1公共基准采集完成，视觉=({visionData.X},{visionData.Y})");
                    return (commonY1, visionData.X, visionData.Y);
                }
                else
                {
                    _logger.Warn("双龙门标定: 等待Cam1视觉数据超时");
                    return (commonY1, 0, 0);
                }
            }
            finally
            {
                lock (_lock)
                {
                    _gantry1VisionTcs = null;
                }
            }
        }

        /// <summary>
        /// 采集Cam2公共基准数据（Y轴在Cam2视野内时调用）
        /// 流程：读取当前共用Y位置 → 触发Cam2拍照 → 等待视觉数据 → 返回采集结果
        /// </summary>
        public async Task<(double CommonY2, double VisionX, double VisionY)> CaptureReferenceGantry2Async(
            DualGantryCalibrationConfig config, CancellationToken ct)
        {
            // 1. 确保龙门2视觉订阅已激活
            lock (_lock)
            {
                if (_gantry2Handler == null && !string.IsNullOrEmpty(config.Gantry2TcpConnection))
                    SubscribeVisionData(2, config.Gantry2TcpConnection);
            }

            // 2. 读取当前共用Y轴位置（Cam2视野内的Y坐标）
            if (!_motionController.CanExecuteMotion(config.StationIdentifier))
                throw new InvalidOperationException("运动控制不可用，请检查安全互锁状态");

            var positions = await _motionController.TeachAsync(config.StationIdentifier);
            double commonY2 = positions.TryGetValue(config.CommonAxisY, out var yVal) ? yVal : 0;

            // 3. 设置龙门2视觉数据等待
            var tcs = new TaskCompletionSource<(double X, double Y)>();
            lock (_lock)
            {
                _gantry2VisionTcs = tcs;
            }

            try
            {
                _logger.Info($"双龙门标定: 采集Cam2公共基准，CommonY2={commonY2}");

                // 4. 触发Cam2拍照
                if (!string.IsNullOrEmpty(config.Gantry2TcpConnection) && !string.IsNullOrEmpty(config.Gantry2TriggerCommand))
                {
                    try
                    {
                        await _tcpEventService.SendCommandAsync(config.Gantry2TcpConnection, config.Gantry2TriggerCommand);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"双龙门标定: 触发Cam2失败 - {ex.Message}");
                    }
                }

                // 5. 等待视觉数据返回（超时5秒）
                var timeoutTask = Task.Delay(5000, ct);
                var completed = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completed == tcs.Task)
                {
                    var visionData = await tcs.Task;
                    _logger.Info($"双龙门标定: Cam2公共基准采集完成，视觉=({visionData.X},{visionData.Y})");
                    return (commonY2, visionData.X, visionData.Y);
                }
                else
                {
                    _logger.Warn("双龙门标定: 等待Cam2视觉数据超时");
                    return (commonY2, 0, 0);
                }
            }
            finally
            {
                lock (_lock)
                {
                    _gantry2VisionTcs = null;
                }
            }
        }

        /// <summary>
        /// 跨龙门Y基准对齐计算（基于公共基准点计算变换参数）
        /// 算法：
        /// 1. 对每个公共基准点，通过龙门1仿射结果将Cam1视觉坐标转为龙门1机械坐标
        /// 2. 通过龙门2仿射结果将Cam2视觉坐标转为龙门2机械坐标
        /// 3. 使用最小二乘法拟合刚体变换参数 OffsetX/OffsetY/RotationDeg
        /// 4. 计算残差 Residual，设置 IsAligned = true
        /// </summary>
        public GantryTransform ComputeGantryTransform(
            IList<CommonReferencePoint> referencePoints,
            AffineCalibrationResult gantry1Result,
            AffineCalibrationResult gantry2Result)
        {
            if (referencePoints == null || referencePoints.Count < 2)
                throw new ArgumentException("跨龙门对齐至少需要2个公共基准点");
            if (gantry1Result == null)
                throw new ArgumentNullException(nameof(gantry1Result), "龙门1仿射标定结果不能为空");
            if (gantry2Result == null)
                throw new ArgumentNullException(nameof(gantry2Result), "龙门2仿射标定结果不能为空");

            // 过滤已采集的公共基准点
            var validPoints = referencePoints.Where(p => p.IsCaptured).ToList();
            if (validPoints.Count < 2)
                throw new ArgumentException("跨龙门对齐至少需要2个已采集的公共基准点");

            int n = validPoints.Count;

            // 对每个公共基准点，将视觉坐标转为机械坐标
            // 龙门1: (Gantry1VisionX, Gantry1VisionY) → (mx1, my1) via gantry1Result
            //   特征点绝对位置 = (mx1, CommonY1 + my1)，其中 my1 为龙门1标定的Dy值
            // 龙门2: (Gantry2VisionX, Gantry2VisionY) → (mx2, my2) via gantry2Result
            //   特征点绝对位置 = (mx2, my2)，其中 my2 为龙门2标定的共用Y值
            var gantry1Machine = new List<(double Mx, double My)>();
            var gantry2Machine = new List<(double Mx, double My)>();

            for (int i = 0; i < n; i++)
            {
                var p = validPoints[i];
                var (mx1, my1) = AffineCalibrationService.Transform(gantry1Result, p.Gantry1VisionX, p.Gantry1VisionY);
                var (mx2, my2) = AffineCalibrationService.Transform(gantry2Result, p.Gantry2VisionX, p.Gantry2VisionY);
                // 龙门1绝对Y = CommonY1 + my1（共用Y + 龙门1标定的Dy偏移）
                gantry1Machine.Add((mx1, p.CommonY1 + my1));
                // 龙门2绝对Y = my2（龙门2标定的共用Y值即为绝对Y）
                gantry2Machine.Add((mx2, my2));
            }

            // 最小二乘法拟合刚体变换：龙门1机械坐标 → 龙门2机械坐标
            // 模型: X2 = OffsetX + a·X1 - b·Y1,  Y2 = OffsetY + b·X1 + a·Y1
            // 其中 a = scale·cos(θ), b = scale·sin(θ)
            // 4个未知数: [OffsetX, OffsetY, a, b]
            // 构建 4×4 正规方程: (A^T·A)·x = A^T·b

            double sX1 = 0, sY1 = 0, sX1X1Y1 = 0;  // ΣX1, ΣY1, Σ(X1²+Y1²)
            double sX2 = 0, sY2 = 0;                 // ΣX2, ΣY2
            double sX1X2Y1Y2 = 0;                     // Σ(X1·X2 + Y1·Y2)
            double sX1Y2Y1X2 = 0;                     // Σ(X1·Y2 - Y1·X2)

            for (int i = 0; i < n; i++)
            {
                double x1 = gantry1Machine[i].Mx;
                double y1 = gantry1Machine[i].My;
                double x2 = gantry2Machine[i].Mx;
                double y2 = gantry2Machine[i].My;

                sX1 += x1;
                sY1 += y1;
                sX1X1Y1 += x1 * x1 + y1 * y1;
                sX2 += x2;
                sY2 += y2;
                sX1X2Y1Y2 += x1 * x2 + y1 * y2;
                sX1Y2Y1X2 += x1 * y2 - y1 * x2;
            }

            // 正规方程矩阵 (4×4):
            // [ 2N,  0,   sX1, -sY1  ] [OffsetX]   [sX2]
            // [ 0,   2N,  sY1,  sX1  ] [OffsetY] = [sY2]
            // [ sX1, sY1, sX1X1Y1, 0 ] [a]         [sX1X2Y1Y2]
            // [-sY1, sX1, 0,  sX1X1Y1] [b]         [sX1Y2Y1X2]
            double[,] ATA = new double[4, 4]
            {
                { 2 * n,    0,        sX1,      -sY1       },
                { 0,        2 * n,    sY1,       sX1       },
                { sX1,      sY1,      sX1X1Y1,   0         },
                { -sY1,     sX1,      0,         sX1X1Y1   }
            };

            double[] ATb = { sX2, sY2, sX1X2Y1Y2, sX1Y2Y1X2 };

            // 求解 4×4 线性方程组
            double[] sol = SolveLinear4x4(ATA, ATb);

            double offsetX = sol[0];
            double offsetY = sol[1];
            double a = sol[2];  // a = scale·cos(θ)
            double b = sol[3];  // b = scale·sin(θ)

            // 提取缩放因子和旋转角度
            double scale = Math.Sqrt(a * a + b * b);
            double rotationRad = Math.Atan2(b, a);
            double rotationDeg = rotationRad * 180.0 / Math.PI;

            // 计算残差（每个点的预测值与实际值的欧氏距离的均方根）
            double sumResidualSq = 0;
            for (int i = 0; i < n; i++)
            {
                double x1 = gantry1Machine[i].Mx;
                double y1 = gantry1Machine[i].My;
                double x2 = gantry2Machine[i].Mx;
                double y2 = gantry2Machine[i].My;

                // 预测的龙门2机械坐标
                double predX2 = offsetX + a * x1 - b * y1;
                double predY2 = offsetY + b * x1 + a * y1;

                double resX = predX2 - x2;
                double resY = predY2 - y2;
                sumResidualSq += resX * resX + resY * resY;
            }

            double residual = Math.Sqrt(sumResidualSq / n);

            var transform = new GantryTransform
            {
                OffsetX = Math.Round(offsetX, 6),
                OffsetY = Math.Round(offsetY, 6),
                RotationDeg = Math.Round(rotationDeg, 6),
                Scale = Math.Round(scale, 6),
                Residual = Math.Round(residual, 6),
                IsAligned = true
            };

            GantryTransformComputed?.Invoke(transform);
            _cachedGantryTransform = transform;
            _logger.Info($"双龙门标定: 跨龙门对齐完成，Offset=({transform.OffsetX},{transform.OffsetY})，旋转={transform.RotationDeg}°，残差={transform.Residual}mm");

            return transform;
        }

        /// <summary>
        /// 获取当前跨龙门变换参数（供其他模块如CadAlignment夹爪定位使用）
        /// </summary>
        /// <returns>已对齐的变换参数；未标定返回null</returns>
        public GantryTransform? GetGantryTransform()
        {
            return _cachedGantryTransform;
        }

        // ==================== 私有辅助方法 ====================

        /// <summary>
        /// 验证龙门编号合法性
        /// </summary>
        /// <param name="gantryId">龙门编号</param>
        private void ValidateGantryId(int gantryId)
        {
            if (gantryId != 1 && gantryId != 2)
                throw new ArgumentException($"龙门编号必须为1或2，当前: {gantryId}", nameof(gantryId));
        }

        /// <summary>
        /// 获取指定龙门的轴名、TCP连接名、触发命令配置
        /// 龙门1: Gantry1AxisX(Dx) + Gantry1AxisY(Dy)，TCP用Gantry1TcpConnection
        /// 龙门2: Gantry2AxisX(X2) + CommonAxisY(共用Y)，TCP用Gantry2TcpConnection
        /// </summary>
        private (string axisX, string axisY, string tcpConnection, string triggerCommand) GetGantryConfig(
            int gantryId, DualGantryCalibrationConfig config)
        {
            if (gantryId == 1)
            {
                return (config.Gantry1AxisX, config.Gantry1AxisY, config.Gantry1TcpConnection, config.Gantry1TriggerCommand);
            }
            else
            {
                // 龙门2的Y轴使用共用下层Y轴
                return (config.Gantry2AxisX, config.CommonAxisY, config.Gantry2TcpConnection, config.Gantry2TriggerCommand);
            }
        }

        /// <summary>
        /// 解析视觉返回数据，支持JSON和逗号分隔两种格式
        /// JSON: {"X": 123.45, "Y": 67.89}
        /// 逗号分隔: "123.45,67.89"
        /// </summary>
        /// <param name="message">原始视觉数据字符串</param>
        /// <returns>解析后的视觉坐标 (X, Y)</returns>
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

        /// <summary>
        /// 求解4×4线性方程组 M·x = b（使用高斯消元法，含部分主元选取）
        /// 用于跨龙门对齐的最小二乘正规方程求解
        /// </summary>
        /// <param name="M">4×4系数矩阵</param>
        /// <param name="b">4×1右端向量</param>
        /// <returns>解向量 x (4×1)</returns>
        private static double[] SolveLinear4x4(double[,] M, double[] b)
        {
            // 构建增广矩阵 [M|b] (4×5)
            double[,] aug = new double[4, 5];
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                    aug[i, j] = M[i, j];
                aug[i, 4] = b[i];
            }

            // 前向消元（部分主元选取，提高数值稳定性）
            for (int col = 0; col < 4; col++)
            {
                // 选取当前列绝对值最大的行作为主元
                int maxRow = col;
                for (int row = col + 1; row < 4; row++)
                {
                    if (Math.Abs(aug[row, col]) > Math.Abs(aug[maxRow, col]))
                        maxRow = row;
                }

                // 交换行
                if (maxRow != col)
                {
                    for (int j = 0; j < 5; j++)
                    {
                        double tmp = aug[col, j];
                        aug[col, j] = aug[maxRow, j];
                        aug[maxRow, j] = tmp;
                    }
                }

                // 检查主元是否接近零（奇异矩阵）
                if (Math.Abs(aug[col, col]) < 1e-12)
                    throw new InvalidOperationException("跨龙门对齐计算失败: 矩阵奇异，请检查公共基准点是否充分分散");

                // 消去下方行
                for (int row = col + 1; row < 4; row++)
                {
                    double factor = aug[row, col] / aug[col, col];
                    for (int j = col; j < 5; j++)
                        aug[row, j] -= factor * aug[col, j];
                }
            }

            // 回代求解
            double[] x = new double[4];
            for (int i = 3; i >= 0; i--)
            {
                double sum = aug[i, 4];
                for (int j = i + 1; j < 4; j++)
                    sum -= aug[i, j] * x[j];
                x[i] = sum / aug[i, i];
            }

            return x;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Abstraction;
using Core.Models;
using System.IO;
using SmarterMotion;
using System.Threading;

namespace Stations
{
    public partial class DispenserStation
    {
        private DmcMotionService _motionService;
        /// <summary>
        /// 执行曲线轮廓点胶
        /// </summary>
        public async Task<bool> ExtractPathAndDispensingAsync(
            int groupIndex,int photoIndex,
            double pathSegmentCount = 20,
            double pathMoveSpeed = 0.5,
            double pathDispensingTime = 1000,
            double axisStartX = 0,
            double axisStartY = 0,
            double axisOffsetX = 0,
            double axisOffsetY = 0,
            Action<string> updateStatus = null,
            Action<string> addLog = null)
        {
            try
            {
                updateStatus?.Invoke("曲线轮廓点胶运行中");
                addLog?.Invoke($"开始曲线轮廓点胶 - 组{groupIndex}");

                // 1. 移动到拍照位置
                updateStatus?.Invoke("移动到拍照位置...");
                bool moveSuccess = false;

                moveSuccess = await MoveToCurvedPathPhotoPosAsync(groupIndex);

                if (!moveSuccess)
                {
                    throw new Exception($"移动到曲线轮廓拍照位置失败");
                }

                // 2. 拍照并获取结果
                updateStatus?.Invoke("拍照中...");
                addLog?.Invoke("开始曲线轮廓拍照...");

                var visionResult = await CaptureCurvedPathOffsetAsync(groupIndex, photoIndex, addLog);
                if (!visionResult.Success || visionResult.Points.Count < 3)
                {
                    throw new Exception($"获取曲线轮廓点失败，点数: {visionResult.Points.Count}");
                }

                // 3. 解析三个控制点 (与相机中心的距离)
                var startPoint = visionResult.Points[0];
                var middlePoint = visionResult.Points[1];
                var endPoint = visionResult.Points[2];

                addLog?.Invoke($"解析控制点成功:");
                addLog?.Invoke($"  起点: X={startPoint.X:F3}, Y={startPoint.Y:F3}");
                addLog?.Invoke($"  中点: X={middlePoint.X:F3}, Y={middlePoint.Y:F3}");
                addLog?.Invoke($"  终点: X={endPoint.X:F3}, Y={endPoint.Y:F3}");

                // 4. 计算针头实际位置
                updateStatus?.Invoke("计算针头位置...");

                // 获取相机中心位置（拍照位置）
                string positionName = $"CurvedPath{groupIndex}点胶拍照位";
                double cameraCenterX = GetPosition(DispX.ActId, positionName);
                double cameraCenterY = GetPosition(DispY_1.ActId, positionName);

                // 获取相机与针头的固定间距和补偿参数
                var needleCalibrationParams = await GetNeedleCalibrationParametersAsync(addLog);

                // 计算三个控制点的针头实际位置
                var needleStartPoint = CalculateNeedlePositionForCurvedPath(
                    cameraCenterX, cameraCenterY, endPoint, needleCalibrationParams, visionResult, addLog);

                var needleMiddlePoint = CalculateNeedlePositionForCurvedPath(
                    cameraCenterX, cameraCenterY, middlePoint, needleCalibrationParams, visionResult, addLog);

                var needleEndPoint = CalculateNeedlePositionForCurvedPath(
                    cameraCenterX, cameraCenterY, startPoint, needleCalibrationParams, visionResult,addLog);

                // 5. 生成并执行曲线轨迹
                updateStatus?.Invoke("生成轨迹并执行...");

                bool result = await ExecuteCurvedPathAsync(
                    needleStartPoint.X, needleStartPoint.Y,
                    needleMiddlePoint.X, needleMiddlePoint.Y,
                    needleEndPoint.X, needleEndPoint.Y,
                    (int)pathSegmentCount,
                    pathMoveSpeed,
                    pathDispensingTime,
                    axisStartX,
                    axisStartY,
                    axisOffsetX,
                    axisOffsetY,
                    addLog);

                if (result)
                {
                    updateStatus?.Invoke("完成");
                    addLog?.Invoke("曲线轮廓点胶完成");
                }

                if (dispensingPathIndex >= 2)
                {
                    dispensingPathIndex = 0;
                    _currentDispensingState = DispensingState.MoveToWaitPosition;
                }
                else
                {
                    dispensingPathIndex++;
                    _currentDispensingState = DispensingState.ExtractPathAndDispensing;
                }
                return result;
            }
            catch (Exception ex)
            {
                updateStatus?.Invoke("错误");
                addLog?.Invoke($"曲线轮廓点胶异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 移动到曲线路径1拍照位置
        /// </summary>
        public async Task<bool> MoveToCurvedPathPhotoPosAsync(int groupIndex)
        {
            try
            {
                _logger.Info($"移动到CurvedPath{groupIndex}拍照位置...");

                _currentPhotoGroup = groupIndex;

                await MoveToCurvedPath1PhotoPosition();

                _logger.Info($"已移动到CurvedPath{groupIndex}拍照位置");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"移动到CurvedPath{groupIndex}拍照位置异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 移动到曲线路径2拍照位置
        /// </summary>
        public async Task<bool> MoveToCurvedPath2PhotoPosAsync(int index)
        {
            try
            {
                _logger.Info($"移动到CurvedPath{index}-2拍照位置...");

                _currentPhotoGroup = index;

                await MoveToCurvedPath2PhotoPosition();

                _logger.Info($"已移动到CurvedPath{index}-2拍照位置");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"移动到CurvedPath{index}-2拍照位置异常: {ex.Message}");
                return false;
            }
        }

        private async Task MoveToCurvedPath1PhotoPosition()
        {
            string positionName = $"CurvedPath{_currentPhotoGroup}点胶拍照位";
            _logger.Info($"【曲线点胶流程】移动到{positionName}");

            IAxis[] axes = new[] { DispX, DispY_1, PlatY };
            var velocities = new[] {
                _axisConfigService.GetAxisSpeed(0, DispX.ActId),
                _axisConfigService.GetAxisSpeed(0, DispY_1.ActId),
                _axisConfigService.GetAxisSpeed(0, PlatY.ActId)
            };
            var positions = new[] {
                GetPosition(DispX.ActId, positionName),
                GetPosition(DispY_1.ActId, positionName),
                _loadingStation.GetPosition(PlatY.ActId, positionName)
            };

            if (!MoveMultiAxisToPosition(axes, positions, velocities))
            {
                throw new InvalidOperationException($"移动到{positionName}失败");
            }

            if (!MoveAxisToPosition(DispZ3, positionName, _axisConfigService.GetAxisSpeed(0, DispZ3.ActId)))
            {
                throw new InvalidOperationException($"移动到{positionName}失败");
            }

            _currentDispensingState = DispensingState.FirstCleanGlue;
            UpdateStepStatus($"移动到{positionName}", true);
        }

        private async Task MoveToCurvedPath2PhotoPosition()
        {
            string positionName = $"CurvedPath{_currentPhotoGroup}_2点胶拍照位";
            _logger.Info($"【曲线点胶流程】移动到{positionName}");

            IAxis[] axes = new[] { DispX, DispY_1 };
            var velocities = new[] {
                _axisConfigService.GetAxisSpeed(0, DispX.ActId),
                _axisConfigService.GetAxisSpeed(0, DispY_1.ActId)
            };
            var positions = new[] {
                GetPosition(DispX.ActId, positionName),
                GetPosition(DispY_1.ActId, positionName)
            };

            if (!MoveMultiAxisToPosition(axes, positions, velocities))
            {
                throw new InvalidOperationException($"移动到{positionName}失败");
            }

            if (!MoveAxisToPosition(DispZ3, positionName, _axisConfigService.GetAxisSpeed(0, DispZ3.ActId)))
            {
                throw new InvalidOperationException($"移动到{positionName}失败");
            }

            _currentDispensingState = DispensingState.FirstCleanGlue;
            UpdateStepStatus($"移动到{positionName}", true);
        }

        /// <summary>
        /// 拍照获取曲线路径偏移位置
        /// </summary>
        private async Task<VisionResult> CaptureCurvedPathOffsetAsync(int groupIndex, int photoIndex, Action<string> addLog = null)
        {
            try
            {
                addLog?.Invoke($"触发曲线轮廓拍照...");

                string cameraName = "DispensingCamera";
                string photoCommand = $"T{50 + photoIndex}"; // T51 或 T52

                var photoResult = await _cameraController.TakePhotoAsync(cameraName, photoCommand);

                // 等待视觉系统处理完成
                var visionResult = await WaitForVisionSystemCurvedPathPhotoComplete(cameraName);

                addLog?.Invoke($"获取到曲线轮廓视觉数据，点数: {visionResult.Points.Count}");
                return visionResult;
            }
            catch (Exception ex)
            {
                addLog?.Invoke($"曲线轮廓拍照异常: {ex.Message}");
                return new VisionResult() { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// 等待视觉系统曲线路径拍照完成
        /// </summary>
        private async Task<VisionResult> WaitForVisionSystemCurvedPathPhotoComplete(string cameraName, int timeout = 30000)
        {
            try
            {
                _logger.Info($"等待{cameraName}视觉系统曲线路径拍照完成");

                // 使用视觉数据服务等待视觉数据
                var visionData = await _visionDataService.WaitForVisionDataAsync(cameraName, timeout);

                if (visionData.Contains("SUCCESS"))
                {
                    _logger.Info($"{cameraName}视觉数据接收完成");

                    // 解析视觉数据
                    var visionResult = ParseVisionDataFromRawDataNewFormat(visionData);

                    if (visionResult.Success)
                    {
                        _logger.Info($"曲线路径视觉数据解析成功，点数: {visionResult.Points.Count}");
                        return visionResult;
                    }
                    else
                    {
                        _logger.Error($"曲线路径视觉数据解析失败: {visionData}");
                        return new VisionResult { Success = false, Message = "视觉数据解析失败" };
                    }
                }
                else
                {
                    string errorMsg = visionData ?? "未知错误";
                    _logger.Error($"{cameraName}视觉系统曲线路径拍照失败: {errorMsg}");
                    return new VisionResult { Success = false, Message = errorMsg };
                }
            }
            catch (TimeoutException)
            {
                _logger.Error($"等待{cameraName}视觉数据超时");
                return new VisionResult { Success = false, Message = "等待视觉数据超时" };
            }
            catch (Exception ex)
            {
                _logger.Error($"等待视觉系统响应异常: {ex.Message}");
                return new VisionResult { Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// 计算曲线路径的针头位置
        /// </summary>
        private (double X, double Y) CalculateNeedlePositionForCurvedPath(
            double cameraCenterX,
            double cameraCenterY,
            PointResult pointOffset,
            NeedleCalibrationParameters parameters,
            VisionResult visionResult,
            Action<string> addLog = null)
        {
            try
            {
                // 计算公式：针头坐标 = 相机中心位置 + 视觉偏移 + 相机与针头固定间距 + 针头补偿
                double calculatedOffsetX = visionResult.CenterX - pointOffset.X; // 相机中心 - 当前点
                double calculatedOffsetY = visionResult.CenterY - pointOffset.Y; 

                double needleX = calculatedOffsetX    // 距离相机中心的X偏移量
                    + parameters.CameraCenterX        // -49.003
                    + parameters.CalibrationDeltaX    // 118.980
                    + parameters.CompensationX;   

                double needleY = calculatedOffsetY    // 距离相机中心的Y偏移量
                    + parameters.CameraCenterY        // 187.642
                    + parameters.CalibrationDeltaY    // 23.008
                    + parameters.CompensationY;

                addLog?.Invoke($"计算针头位置:");
                addLog?.Invoke($"  相机中心: X={cameraCenterX:F3}, Y={cameraCenterY:F3}");
                addLog?.Invoke($"  视觉偏移: X={pointOffset.X:F3}, Y={pointOffset.Y:F3}");
                addLog?.Invoke($"  相机-针头间距: ΔX={parameters.CalibrationDeltaX:F3}, ΔY={parameters.CalibrationDeltaY:F3}");
                addLog?.Invoke($"  针头补偿: X={parameters.CompensationX:F3}, Y={parameters.CompensationY:F3}");
                addLog?.Invoke($"  最终针头位置: X={needleX:F3}, Y={needleY:F3}");

                return (needleX, needleY);
            }
            catch (Exception ex)
            {
                addLog?.Invoke($"计算针头位置异常: {ex.Message}");
                return (0, 0);
            }
        }

        /// <summary>
        /// 获取针头校准参数
        /// </summary>
        private async Task<NeedleCalibrationParameters> GetNeedleCalibrationParametersAsync(Action<string> addLog = null)
        {
            try
            {
                string customDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "Calibration");
                var parameters = _parameterStorage?.Load<NeedleCalibrationParameters>(
                    "NeedleCalibration",
                    customDirectory
                );

                if (parameters == null)
                {
                    addLog?.Invoke("警告: 使用默认针头校准参数");
                    parameters = new NeedleCalibrationParameters
                    {
                        CalibrationDeltaX = 5.0,
                        CalibrationDeltaY = 5.0,
                        CompensationX = 0.1,
                        CompensationY = 0.1
                    };
                }

                return parameters;
            }
            catch (Exception ex)
            {
                addLog?.Invoke($"加载针头校准参数异常: {ex.Message}");
                return new NeedleCalibrationParameters
                {
                    CalibrationDeltaX = 5.0,
                    CalibrationDeltaY = 5.0,
                    CompensationX = 0.1,
                    CompensationY = 0.1
                };
            }
        }

        /// <summary>
        /// 执行曲线路径点胶
        /// </summary>
        private async Task<bool> ExecuteCurvedPathAsync(
            double startX, double startY,
            double midX, double midY,
            double endX, double endY,
            int segmentCount,
            double moveSpeed,
            double dispensingTime,
            double axisStartX,
            double axisStartY,
            double axisOffsetX,
            double axisOffsetY,
            Action<string> addLog = null)
        {
            try
            {
                addLog?.Invoke($"开始执行曲线路径点胶: 起点({startX:F3},{startY:F3}), 中点({midX:F3},{midY:F3}), 终点({endX:F3},{endY:F3})");

                // 1. 创建二次贝塞尔曲线轨迹
                addLog?.Invoke("生成二次贝塞尔曲线轨迹点...");
                var bezierPoints = GenerateQuadraticBezierPoints(startX, startY, midX, midY, endX, endY, segmentCount);

                addLog?.Invoke($"轨迹生成完成，共 {bezierPoints.Count} 个点");

                // 移动到起点
                await MoveToContinuousTrajectoryStart(bezierPoints[0].X, bezierPoints[0].Y);

                // 下降到点胶高度
                await MoveToDispseningHeightAsync();

                // 初始化连续插补
                _motionService.InitializeContinuousInterpolation();

                // 计算针头轨迹点
                for (int i = 0; i < bezierPoints.Count; i++)
                {
                    _motionService.AddLineSegment(bezierPoints[i].X, bezierPoints[i].Y, 1, i);
                    await Task.Delay(10);
                }

                // 执行连续插补
                _motionService.ExecuteContinuousInterpolation();

                // 点胶开始
                await _motionService.ControlDispensing(20);

                // 等待运动完成
                TimeSpan timeout = TimeSpan.FromSeconds(60 * 5);  //5个60s
                await _motionService.WaitForMotionCompletionAsync(timeout);

                await Z2MoveToSafeHeightAsync();

                await _motionService.StopDispensing(20);

                addLog?.Invoke("曲线路径点胶执行完成");
                return true;
            }
            catch (Exception ex)
            {
                addLog?.Invoke($"执行曲线路径失败: {ex.Message}");

                // 紧急停止点胶
                StopDispensing();

                // 尝试返回到安全位置
                try
                {
                    await ReturnToSafePositionAsync();
                }
                catch { }

                return false;
            }
        }
        /// <summary>
        /// 生成二次贝塞尔曲线点
        /// </summary>
        private List<(double X, double Y)> GenerateQuadraticBezierPoints(
            double startX, double startY,
            double midX, double midY,
            double endX, double endY,
            int segmentCount)
        {
            var points = new List<(double X, double Y)>();

            for (int i = 0; i <= segmentCount; i++)
            {
                double t = (double)i / segmentCount;

                // 二次贝塞尔曲线公式: B(t) = (1-t)² * P0 + 2*(1-t)*t * P1 + t² * P2
                double x = Math.Pow(1 - t, 2) * startX + 2 * (1 - t) * t * midX + Math.Pow(t, 2) * endX;
                double y = Math.Pow(1 - t, 2) * startY + 2 * (1 - t) * t * midY + Math.Pow(t, 2) * endY;

                points.Add((x, y));
            }

            return points;
        }
        /// <summary>
        /// 转换到轴坐标系统
        /// </summary>
        private List<(double X, double Y)> ConvertToAxisCoordinates(
            List<(double X, double Y)> points,
            double axisStartX,
            double axisStartY,
            double axisOffsetX,
            double axisOffsetY,
            Action<string> addLog)
        {
            var convertedPoints = new List<(double X, double Y)>();

            foreach (var point in points)
            {
                double convertedX = point.X + axisStartX + axisOffsetX;
                double convertedY = point.Y + axisStartY + axisOffsetY;

                convertedPoints.Add((convertedX, convertedY));
            }

            addLog?.Invoke($"坐标转换完成: 起点偏移({axisStartX},{axisStartY}), 轴偏移({axisOffsetX},{axisOffsetY})");

            return convertedPoints;
        }
        /// <summary>
        /// 下降到点胶高度
        /// </summary>
        private async Task<bool> MoveToDispseningHeightAsync()
        {
            _logger.Info("下降到点胶高度...");

            return await MoveToDispensingHeightAsync(needleTipZ);
        }
        /// <summary>
        /// 点胶完成
        /// <summary>
        private async Task<bool> Z2MoveToSafeHeightAsync()
        {
            _logger.Info("抬起到安全高度...");

            return await ReturnToSafePositionAsync();
        }

        
    }
}

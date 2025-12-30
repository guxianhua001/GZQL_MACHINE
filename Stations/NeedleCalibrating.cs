using Core.Models;
using HalconDotNet;
using SmarterMotion;
using Stations.Service;
using Stations.Services;
using Stations.TaskParameters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Stations
{
    // 在 DispenserStation.cs 中添加针头校准相关功能

    public partial class DispenserStation
    {
        // 针头校准相关字段
        private bool _isNeedleCalibrating = false;
        private CancellationTokenSource _needleCalibrationCTS;
        private XDi _needleSensor; // 针头接触传感器

        // 添加针头校准服务
        private readonly NeedleCalibrationService _needleCalibrationService;

        // 针头校准相关属性和方法
        public NeedleCalibrationParams NeedleCalibrationParams => _needleCalibrationService.CurrentParameters;

        // 针头校准事件
        public event Action<string, double> NeedleCalibrationStatusUpdated;
        public event Action<NeedleCalibrationParams> NeedleCalibrationCompleted;

        /// <summary>
        /// 执行针头校准流程
        /// </summary>
        public async Task<bool> ExecuteNeedleCalibrationAsync(NeedleCalibrationParams parameters)
        {
            if (_isNeedleCalibrating)
            {
                _logger.Warn("针头校准已在执行中");
                return false;
            }
            // 使用 Task.Run 将整个同步流程包装为异步
            return await Task.Run(async () =>
            {
                try
                {
                    _isNeedleCalibrating = true;
                    _needleCalibrationCTS = new CancellationTokenSource();

                    UpdateCalibrationStatus("开始针头校准", 0);

                    // 1. 移动到安全高度
                    UpdateCalibrationStatus("抬升到安全高度", 10);
                    if (!await MoveToSafeHeightAsync().ConfigureAwait(false))
                    {
                        UpdateCalibrationStatus("移动到安全高度失败", 0);
                        return false;
                    }

                    // 2. 搜索中心点XY
                    UpdateCalibrationStatus("搜索中心点XY", 20);
                    PointF centerPoint = await SearchCenterPointAsync(parameters).ConfigureAwait(false);
                    if (centerPoint == null)
                    {
                        UpdateCalibrationStatus("搜索中心点失败", 0);
                        return false;
                    }

                    // 3. 搜索针尖高度
                    UpdateCalibrationStatus("搜索针尖高度", 60);
                    double needleHeight = await SearchNeedleHeightAsync(centerPoint, parameters).ConfigureAwait(false);
                    if (double.IsNaN(needleHeight))
                    {
                        UpdateCalibrationStatus("搜索针尖高度失败", 0);
                        return false;
                    }

                    // 4. 计算补偿值
                    UpdateCalibrationStatus("计算补偿值", 90);
                    await CalculateCompensationAsync(centerPoint, needleHeight, parameters).ConfigureAwait(false);

                    UpdateCalibrationStatus("针头校准完成", 100);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    UpdateCalibrationStatus("校准已取消", 0);
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "针头校准异常");
                    UpdateCalibrationStatus($"校准异常: {ex.Message}", 0);
                    return false;
                }
                finally
                {
                    _isNeedleCalibrating = false;
                    _needleCalibrationCTS?.Dispose();
                }
            });
        }

        /// <summary>
        /// 搜索中心点XY
        /// </summary>
        private async Task<PointF> SearchCenterPointAsync(NeedleCalibrationParams parameters)
        {
            var searchPoints = new[]
            {
                parameters.SearchPoint1,
                parameters.SearchPoint2,
                parameters.SearchPoint3,
                parameters.SearchPoint4
            };

            var xEdgePoints = new List<PointF>();
            var yEdgePoints = new List<PointF>();

            // 移动到起始点后 下降到寻针高度
            await MoveToXYPositionAsync(searchPoints[0], 30);
            if (!await MoveToSearchNeedledHeightAsync())
                return null;

            // 前两个点进行X方向搜索
            for (int i = 0; i < 2; i++)
            {
                var point = searchPoints[i];
                UpdateCalibrationStatus($"在点{i + 1}进行X方向搜索", 20 + i * 10);

                var xEdge = await SearchEdgeInDirectionAsync(point, Direction.XPositive, Direction.X, parameters);
                if (xEdge != null)
                    xEdgePoints.Add(xEdge);
                else
                    return null;
            }

            // 后两个点进行Y方向搜索
            for (int i = 2; i < 4; i++)
            {
                var point = searchPoints[i];
                UpdateCalibrationStatus($"在点{i + 1}进行Y方向搜索", 40 + (i - 2) * 10);

                var yEdge = await SearchEdgeInDirectionAsync(point, Direction.XPositive, Direction.Y, parameters);
                if (yEdge != null)
                    yEdgePoints.Add(yEdge);
                else
                    return null;
            }

            if (xEdgePoints.Count < 2 || yEdgePoints.Count < 2)
            {
                _logger.Error($"有效边缘点不足: X方向={xEdgePoints.Count}, Y方向={yEdgePoints.Count}");
                return null;
            }

            // 使用Halcon方法计算中心点
            var centerPoint = CalculateCenterPointWithHalcon(xEdgePoints, yEdgePoints);

            if (centerPoint != null)
            {
                _logger.Info($"Halcon计算得到中心点: X={centerPoint.X:F3}, Y={centerPoint.Y:F3}");
                _logger.Info($"X方向搜索点: [{xEdgePoints[0].X:F3}, {xEdgePoints[0].Y:F3}], [{xEdgePoints[1].X:F3}, {xEdgePoints[1].Y:F3}]");
                _logger.Info($"Y方向搜索点: [{yEdgePoints[0].X:F3}, {yEdgePoints[0].Y:F3}], [{yEdgePoints[1].X:F3}, {yEdgePoints[1].Y:F3}]");

                return centerPoint;
            }
            else
            {
                _logger.Error("Halcon直线交点计算失败，使用平均值作为备选");
                // 备选方案：使用平均值
                float centerX = xEdgePoints.Average(p => p.X);
                float centerY = yEdgePoints.Average(p => p.Y);
                return new PointF(centerX, centerY);
            }
        }
        /// <summary>
        /// 使用Halcon方法计算两条直线的交点
        /// </summary>
        private PointF? CalculateCenterPointWithHalcon(List<PointF> xPoints, List<PointF> yPoints)
        {
            try
            {
                // 创建Halcon对象
                HObject xLine, yLine;
                HTuple intersectionRow, intersectionColumn;
                HTuple isOverlapping;

                // 生成X方向的直线
                HOperatorSet.GenContourPolygonXld(out xLine,
                    new HTuple(xPoints[0].Y, xPoints[1].Y),  // Row (Y坐标)
                    new HTuple(xPoints[0].X, xPoints[1].X)   // Column (X坐标)
                );

                // 生成Y方向的直线  
                HOperatorSet.GenContourPolygonXld(out yLine,
                    new HTuple(yPoints[0].Y, yPoints[1].Y),  // Row (Y坐标)
                    new HTuple(yPoints[0].X, yPoints[1].X)   // Column (X坐标)
                );

                // 计算两条直线的交点
                HOperatorSet.IntersectionLines(
                    new HTuple(xPoints[0].Y),
                    new HTuple(xPoints[0].X),  // Line1 Row
                    new HTuple(xPoints[1].Y),
                    new HTuple(xPoints[1].X),  // Line1 Column  
                    new HTuple(yPoints[0].Y),
                    new HTuple(yPoints[0].X),  // Line2 Row
                    new HTuple(yPoints[1].Y),
                    new HTuple(yPoints[1].X),  // Line2 Column
                    out intersectionRow,       // 交点Row (Y)
                    out intersectionColumn,    // 交点Column (X)
                    out isOverlapping          // 是否重叠
                );

                // 检查交点是否有效
                if (intersectionRow.TupleLength() > 0 && intersectionColumn.TupleLength() > 0 &&
                    !isOverlapping.TupleEqual(1))
                {
                    float centerX = (float)intersectionColumn.D;
                    float centerY = (float)intersectionRow.D;

                    _logger.Info($"Halcon交点计算成功: X={centerX:F3}, Y={centerY:F3}");
                    return new PointF(centerX, centerY);
                }
                else
                {
                    _logger.Warn("Halcon计算交点失败，直线可能平行或重叠");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Halcon直线交点计算异常: {ex.Message}");
                return null;
            }
        }
        /// <summary>
        /// 在指定方向搜索边缘
        /// </summary>
        private async Task<PointF> SearchEdgeInDirectionAsync(PointF startPoint, Direction direction, Direction sensorDirection, NeedleCalibrationParams parameters)
        {
            try
            {
                // 移动到起始点
                await MoveToXYPositionAsync(new PointF((float)(startPoint.X - parameters.SearchRange), startPoint.Y),
                    parameters.SearchSpeed).ConfigureAwait(false);

                // 正向搜索
                double forwardEdge = await SearchSingleEdgeAsync(direction, sensorDirection, parameters.SearchRange * 2, parameters.FineSearchSpeed).ConfigureAwait(false);
                if (double.IsNaN(forwardEdge))
                    return null;

                //await CheckMoveDoneAsync(0, (ushort)GetAxisIdForDirection(direction)).ConfigureAwait(false);
                WaitMoveDone();

                // 反向搜索
                double backwardEdge = await SearchSingleEdgeAsync(GetOppositeDirection(direction), sensorDirection, parameters.SearchRange * 2, parameters.FineSearchSpeed).ConfigureAwait(false);
                if (double.IsNaN(backwardEdge))
                  return null;

                //await CheckMoveDoneAsync(0, (ushort)GetAxisIdForDirection(direction)).ConfigureAwait(false);
                WaitMoveDone();

                // 计算中心位置
                double center = (forwardEdge + backwardEdge) / 2;

                var result = new PointF(startPoint.X, startPoint.Y);

                if (direction == Direction.XPositive || direction == Direction.XNegative)
                    result.X = (float)center;
                else
                    result.Y = (float)center;

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"{direction}方向边缘搜索失败");
                return null;
            }
        }

        /// <summary>
        /// 搜索单个边缘
        /// </summary>
        private async Task<double> SearchSingleEdgeAsync(Direction direction, Direction sensorDirection, double searchRange, double speed)
        {
            return await Task.Run(async () =>
            {
                int axisId = GetAxisIdForDirection(direction);
                double startPos = GetAxisCurrentPosition(axisId);
                double searchDistance = direction == Direction.XPositive || direction == Direction.YPositive ?
                                      searchRange : -searchRange;

                _logger.Info($"开始{direction}方向边缘搜索: 起始位置={startPos:F3}, 搜索距离={searchDistance:F3}, 速度={speed:F1}");

                // 开始搜索移动
                MoveRel(axisId, searchDistance, speed);

                // 使用异步等待方法
                var edgePos = await WaitForSensorTriggerAsync(direction, sensorDirection, axisId).ConfigureAwait(false);

                if (!double.IsNaN(edgePos))
                {
                    _logger.Info($"{direction}方向边缘位置: {edgePos:F3}");
                }
                else
                {
                    _logger.Warn($"{direction}方向边缘搜索超时");
                }

                return edgePos;
            }).ConfigureAwait(false);
        }
        /// <summary>
        /// 异步等待传感器触发
        /// </summary>
        private async Task<double> WaitForSensorTriggerAsync(Direction direction, Direction sensorDirection, int axisId)
        {
            try
            {
                var timeoutMs = 60000; // 60秒超时
                var checkIntervalMs = 20; // 检查间隔增加到20ms，减少检查频率

                // 使用 TaskCompletionSource 更优雅地处理等待
                var tcs = new TaskCompletionSource<double>();
                var cancellationToken = _needleCalibrationCTS.Token;

                // 使用专门的线程或线程池任务来检查传感器
                var sensorTask = Task.Run(async () =>
                {
                    DateTime startTime = DateTime.Now;

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMs)
                        {
                            tcs.SetResult(double.NaN);
                            break;
                        }

                        if (CheckNeedleSensor(sensorDirection))
                        {
                            var currentPos = GetAxisCurrentPosition(axisId);
                            tcs.SetResult(currentPos);
                            break;
                        }

                        // 使用延迟而不是繁忙等待
                        await Task.Delay(checkIntervalMs, cancellationToken).ConfigureAwait(false);
                    }
                }, cancellationToken);

                return await tcs.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"{direction}方向搜索被取消");
                return double.NaN;
            }
        }
        /// <summary>
        /// 搜索针尖高度  
        /// </summary>
        private async Task<double> SearchNeedleHeightAsync(PointF centerPoint, NeedleCalibrationParams parameters)
        {
            try
            {
                // 移动到中心点
                await MoveToXYPositionAsync(new PointF(centerPoint.X, centerPoint.Y), parameters.SearchSpeed);

                double totalHeight = 0;
                int successCount = 0;

                for (int i = 0; i < parameters.ZSearchCount; i++)
                {
                    UpdateCalibrationStatus($"第 {i + 1}/{parameters.ZSearchCount} 次高度搜索", 60 + i * 10);

                    double height = await SearchSingleNeedleHeightAsync(parameters);
                    if (!double.IsNaN(height))
                    {
                        totalHeight += height;
                        successCount++;
                    }

                    // 短暂抬升进行下一次搜索
                    //if (i < parameters.ZSearchCount - 1)
                    //{
                    //    await MoveZAxisRelativeAsync(-0.5, parameters.SearchSpeed); // 抬升5mm
                    //}
                    WaitMoveDone();
                }

                if (successCount == 0)
                    return double.NaN;

                double averageHeight = totalHeight / successCount;
                _logger.Info($"针尖高度搜索结果: 平均高度={averageHeight:F3}mm, 成功次数={successCount}");

                return averageHeight;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "搜索针尖高度异常");
                return double.NaN;
            }
        }
        /// <summary>
        /// 检查Z轴针头传感器信号
        /// </summary>
        private bool CheckZNeedleSensor()
        {
            try
            {
                var sensor1 = LTDMC.dmc_read_inbit(0, 37);
                var sensor2 = LTDMC.dmc_read_inbit(0, 38);
                if (sensor1 != null && sensor2 != null)
                {
                    bool sensorState = sensor1 == 0 && sensor2 == 0;
                    _logger.Debug($"Z方向传感器状态: {sensorState}");
                    return sensorState;
                }
                else
                {
                    _logger.Warn("未找到Z方向传感器 channel 37");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "读取Z方向传感器状态失败");
                return false;
            }
        }
        /// <summary>
        /// 单次搜索针尖高度
        /// </summary>
        private async Task<double> SearchSingleNeedleHeightAsync(NeedleCalibrationParams parameters)
        {
            int zAxisId = DispZ2.ActId; // 使用Z2轴进行高度搜索
            double startHeight = GetAxisCurrentPosition(zAxisId);
            double searchDistance = -5; 

            _logger.Info($"开始Z方向高度搜索: 起始高度={startHeight:F3}, 搜索距离={searchDistance:F3}, 速度={parameters.FineSearchSpeed:F1}");
            // 向上抬5mm
            MoveRel(zAxisId, searchDistance, parameters.SearchSpeed);

            await Task.Delay(100);

            WaitMoveDone();

            // 开始向下搜索移动
            MoveRel(zAxisId, -searchDistance, parameters.FineSearchSpeed);

            // 等待传感器信号
            DateTime startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalSeconds < 60)
            {
                if (_needleCalibrationCTS.Token.IsCancellationRequested)
                    throw new OperationCanceledException();

                if (CheckZNeedleSensor())
                {
                    double needleHeight = GetAxisCurrentPosition(zAxisId);
                    _logger.Info($"针尖高度: {needleHeight:F3}");
                    return needleHeight;
                }

                await Task.Delay(10);
            }

            _logger.Warn("针尖高度搜索超时");
            return double.NaN;
        }

        /// <summary>
        /// 计算补偿值
        /// </summary>
        private async Task CalculateCompensationAsync(PointF measuredPoint, double measuredHeight, NeedleCalibrationParams parameters)
        {
            // 计算偏差
            float deltaX = measuredPoint.X - parameters.ReferenceXYZ.X;
            float deltaY = measuredPoint.Y - parameters.ReferenceXYZ.Y;
            float deltaZ = (float)measuredHeight - parameters.ReferenceXYZ.Z;

            // 更新当前值
            parameters.CurrentXYZ = new PointF(measuredPoint.X, measuredPoint.Y, (float)measuredHeight);

            // 计算补偿值（取反）
            parameters.CompensationXYZ = new PointF(-deltaX, -deltaY, -deltaZ);

            _logger.Info($"针头校准结果 - 偏差: ΔX={deltaX:F3}, ΔY={deltaY:F3}, ΔZ={deltaZ:F3}");
            _logger.Info($"补偿值: X={parameters.CompensationXYZ.X:F3}, Y={parameters.CompensationXYZ.Y:F3}, Z={parameters.CompensationXYZ.Z:F3}");

            // 保存参数到服务
            //await _needleCalibrationService.SaveParametersAsync().ConfigureAwait(false);

            // 触发校准完成事件
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                NeedleCalibrationCompleted?.Invoke(parameters);
            });

            // 移动到安全高度
            await MoveToSafeHeightAsync();
        }

        /// <summary>
        /// 停止针头校准
        /// </summary>
        public void StopNeedleCalibration()
        {
            _needleCalibrationCTS?.Cancel();
            _isNeedleCalibrating = false;
        }

        /// <summary>
        /// 检查针头传感器信号
        /// </summary>
        private bool CheckNeedleSensor(Direction direction)
        {
            try
            {
                // 假设X方向传感器连接到DI 37，Y方向传感器连接到DI 38
                int sensorDiId = (direction == Direction.X || direction == Direction.XPositive || direction == Direction.XNegative)
                    ? 38 : 37;

                var sensor = LTDMC.dmc_read_inbit(0, (ushort)sensorDiId); // 0是有信号
                if (sensor != null)
                {
                    bool sensorState = sensor == 0;
                    _logger.Debug($"{direction}方向传感器状态: {sensorState}");
                    return sensorState;
                }
                else
                {
                    _logger.Warn($"未找到传感器 DI{sensorDiId}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "读取传感器状态失败");
                return false;
            }
        }

        /// <summary>
        /// 更新校准状态
        /// </summary>
        private void UpdateCalibrationStatus(string status, double progress)
        {
            NeedleCalibrationStatusUpdated?.Invoke(status, progress);
            _logger.Info($"[针头校准] {status}");
        }
        /// <summary>
        /// 应用针头补偿值
        /// </summary>
        public void ApplyNeedleCompensation(PointF compensationXYZ)
        {
            try
            {
                //var compensation = NeedleCalibrationParams.CompensationXYZ;
                var compensation = compensationXYZ;
                NeedleCalibrationParams.CompensationXYZ = compensation;
                _logger.Info($"应用针头补偿值: X={compensation.X:F3}, Y={compensation.Y:F3}, Z={compensation.Z:F3}");

                UpdateCalibrationStatus("补偿值已应用", 0);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "应用针头补偿值失败");
                throw;
            }
        }
        /// <summary>
        /// 重置针头补偿值
        /// </summary>
        public void ResetNeedleCompensation()
        {
            //NeedleCalibrationParams.CompensationXYZ = new PointF(0, 0, 0);
            //NeedleCalibrationParams.CurrentXYZ = new PointF(0, 0, 0);
            _logger.Info("针头补偿值已重置");
        }
        // 辅助枚举和方法
        private enum Direction { XPositive, XNegative, YPositive, YNegative, X, Y }

        private Direction GetOppositeDirection(Direction direction)
        {
            return direction switch
            {
                Direction.XPositive => Direction.XNegative,
                Direction.XNegative => Direction.XPositive,
                Direction.YPositive => Direction.YNegative,
                Direction.YNegative => Direction.YPositive,
                _ => direction
            };
        }

        private int GetAxisIdForDirection(Direction direction)
        {
            return direction switch
            {
                Direction.XPositive or Direction.XNegative or Direction.X => DispX.ActId,
                Direction.YPositive or Direction.YNegative or Direction.Y => DispY_1.ActId,
                _ => DispX.ActId
            };
        }
        /// <summary>
        /// 异步检查运动是否完成
        /// </summary>
        private async Task<int> CheckMoveDoneAsync(ushort actCardId, ushort axisId, int pollInterval = 10)
        {
            try
            {
                while (true)
                {
                    if (_needleCalibrationCTS?.Token.IsCancellationRequested == true)
                        return -2;

                    if (LTDMC.dmc_check_done(actCardId, axisId) != 0)
                    {
                        return LTDMC.dmc_check_success_encoder(actCardId, axisId) == 0 ? 0 : 1;
                    }

                    await Task.Delay(pollInterval, _needleCalibrationCTS?.Token ?? CancellationToken.None);
                }
            }
            catch (OperationCanceledException)
            {
                return -2;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "检查运动状态异常");
                return -1;
            }
        }

        /// <summary>
        /// 优化的运动完成等待
        /// </summary>
        private async Task<bool> WaitMoveDoneOptimizedAsync(int axisId, int timeoutMs = 5000)
        {
            try
            {
                var startTime = DateTime.Now;

                while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMs)
                {
                    if (LTDMC.dmc_check_done(0, (ushort)axisId) != 0)
                    {
                        var success = LTDMC.dmc_check_success_encoder(0, (ushort)axisId) == 0;
                        return success;
                    }

                    await Task.Delay(10).ConfigureAwait(false);
                }

                _logger.Warn($"轴{axisId}运动等待超时");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"等待轴{axisId}运动完成异常");
                return false;
            }
        }
    }
}

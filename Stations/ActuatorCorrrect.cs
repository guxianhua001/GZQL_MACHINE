using Core.Models;
using Stations.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stations
{
    public partial class AssemblyStation
    {
        /// <summary>
        /// 纠正Actuator的X方向偏差
        /// </summary>
        public async Task<bool> CorrectActuatorXAsync(
            int groupIndex = 1, int actuatorIndex = 1,
            CancellationToken cancellationToken = default,
            IProgress<(int progress, string status)> progressCallback = null)
        {
            try
            {
                _logger.Info($"【组装工站】开始Actuator X方向纠正流程");

                int maxRetries = _recipeService.Parameters?.ActuatorCorrectionMaxRetries ?? 3;
                double tolerance = _recipeService.Parameters?.ActuatorXTolerance ?? 0.02; // 单位：mm
                double firstHeight = _recipeService.Parameters?.ActuatorFirstPhotoHeight ?? 10.0;
                double secondHeight = _recipeService.Parameters?.ActuatorSecondPhotoHeight ?? 15.0;

                // 记录初始位置
                double initialX = GetAxisPosition(DispX.ActId);
                double initialY = GetAxisPosition(DispY_1.ActId);
                double initialZ = GetAxisPosition(DispZ3.ActId);

                for (int retry = 0; retry < maxRetries; retry++)
                {
                    progressCallback?.Report((retry * 30, $"第{retry + 1}/{maxRetries}次纠正循环开始"));

                    // 步骤1: 移动到Actuator拍照位置
                    progressCallback?.Report((retry * 30 + 5, "移动相机到Actuator上方..."));
                    _logger.Info($"步骤1: 移动到Actuator拍照位置，第{retry + 1}次尝试");

                    if (!await MoveToActuatorPhotoPositionAsync(firstHeight, groupIndex, actuatorIndex))
                    {
                        _logger.Error("移动到Actuator拍照位置失败");
                        progressCallback?.Report((retry * 30 + 5, "移动相机到Actuator位置失败"));
                        return false;
                    }

                    // 等待稳定
                    await Task.Delay(200, cancellationToken);

                    // 步骤2: 第一次拍照
                    progressCallback?.Report((retry * 30 + 10, "进行第一次拍照..."));
                    _logger.Info($"步骤2: 第一次拍照，高度{firstHeight}mm");

                    string cameraName = "DispensingCamera";
                    string photoCommand = $"ACTUATOR{groupIndex}_{1}";

                    var firstPhotoResult = await _cameraController.TakePhotoAsync(cameraName, photoCommand);
                    if (!firstPhotoResult)
                    {
                        _logger.Error("Actuator第一次拍照失败");
                        progressCallback?.Report((retry * 30 + 10, "第一次拍照失败"));
                        return false;
                    }

                    // 等待第一次视觉结果
                    progressCallback?.Report((retry * 30 + 15, "等待第一次视觉结果..."));
                    var firstVisionResult = await WaitForVisionSystemPhotoComplete(cameraName);

                    if (!firstVisionResult.Success)
                    {
                        _logger.Error($"第一次视觉数据获取失败: {firstVisionResult.Message}");
                        progressCallback?.Report((retry * 30 + 15, $"第一次视觉数据失败: {firstVisionResult.Message}"));
                        return false;
                    }

                    // 步骤3: 调整高度，进行第二次拍照
                    progressCallback?.Report((retry * 30 + 20, $"调整高度到{secondHeight}mm进行第二次拍照..."));
                    _logger.Info($"步骤3: 调整高度到{secondHeight}mm");

                    if (!await MoveDispZ3ToHeightAsync(secondHeight, cancellationToken))
                    {
                        _logger.Error($"调整DispZ3高度到{secondHeight}mm失败");
                        progressCallback?.Report((retry * 30 + 20, "调整高度失败"));
                        return false;
                    }

                    // 等待稳定
                    await Task.Delay(200, cancellationToken);

                    // 第二次拍照
                    progressCallback?.Report((retry * 30 + 25, "进行第二次拍照..."));
                    _logger.Info($"步骤3: 第二次拍照，高度{secondHeight}mm");
                    photoCommand = $"ACTUATOR{groupIndex}_{2}";
                    var secondPhotoResult = await _cameraController.TakePhotoAsync(cameraName, photoCommand);
                    if (!secondPhotoResult)
                    {
                        _logger.Error("Actuator第二次拍照失败");
                        progressCallback?.Report((retry * 30 + 25, "第二次拍照失败"));
                        return false;
                    }

                    // 等待第二次视觉结果
                    progressCallback?.Report((retry * 30 + 30, "等待第二次视觉结果..."));
                    var secondVisionResult = await WaitForVisionSystemPhotoComplete(cameraName);

                    if (!secondVisionResult.Success)
                    {
                        _logger.Error($"第二次视觉数据获取失败: {secondVisionResult.Message}");
                        progressCallback?.Report((retry * 30 + 30, $"第二次视觉数据失败: {secondVisionResult.Message}"));
                        return false;
                    }

                    // 步骤4: 计算补偿值并调整DispX轴
                    progressCallback?.Report((retry * 30 + 35, "计算X方向补偿值..."));
                    _logger.Info($"步骤4: 计算X方向补偿值");

                    double standardSpacing = GetStandardSpacing();
                    double compensation = CalculateActuatorXCompensationSimple(firstVisionResult, secondVisionResult, standardSpacing);
                    _logger.Info($"计算得到X方向补偿值: {compensation:F3}mm");

                    // 检查是否在容差范围内
                    if (Math.Abs(compensation) <= tolerance)
                    {
                        _logger.Info($"Actuator X方向偏差在允许范围内(±{tolerance:F3}mm)，纠正完成");
                        progressCallback?.Report((100, $"纠正完成，X方向偏差: {compensation:F3}mm"));

                        // 步骤5: 恢复初始位置
                        progressCallback?.Report((100, "恢复初始位置..."));
                        await ReturnToInitialPositionAsync(initialX, initialY, initialZ, cancellationToken);

                        return true;
                    }

                    // 调整AsmX轴
                    progressCallback?.Report((retry * 30 + 40, $"调整AsmX轴: {compensation:F3}mm..."));
                    _logger.Info($"步骤5: 调整AsmX轴 {compensation:F3}mm");

                    if (!await AdjustDispXAxisAsync(compensation, cancellationToken))
                    {
                        _logger.Error("调整DispX轴失败");
                        progressCallback?.Report((retry * 30 + 40, "调整X轴失败"));
                        return false;
                    }

                    progressCallback?.Report((retry * 30 + 45, $"第{retry + 1}次调整完成，等待下一轮验证"));

                    // 如果是最后一次循环但还没达到容差，则继续拍照验证但不调整
                    if (retry == maxRetries - 1)
                    {
                        _logger.Warn($"达到最大重试次数{maxRetries}次，X方向偏差仍为{compensation:F3}mm，超出容差{Math.Abs(compensation) - tolerance:F3}mm");
                        progressCallback?.Report((100, $"达到最大重试次数，偏差: {compensation:F3}mm"));

                        // 恢复初始位置
                        await ReturnToInitialPositionAsync(initialX, initialY, initialZ, cancellationToken);

                        return false;
                    }
                    // 更新补偿值
                    _compensationService.UpdateCompensation(actuatorIndex, CompensationType.Actuator,
                           new CompensationData
                           {
                               CompensationX = compensation, 
                               Source = "DispenserStation"
                           });
                    return true;
                }

                return false;
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"Actuator X方向纠正被用户取消");
                MoveStop();
                progressCallback?.Report((0, "操作已取消"));
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Actuator X方向纠正流程异常");
                progressCallback?.Report((0, $"纠正异常: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// 计算Actuator X方向补偿值
        /// </summary>
        private double CalculateActuatorXCompensation(VisionResult firstResult, VisionResult secondResult, double firstHeight, double secondHeight)
        {
            // 简单实现：取两次测量的平均值
            // 实际应用中可能需要更复杂的算法，比如根据高度差进行三角计算

            double offsetX1 = firstResult.OffsetX;
            double offsetX2 = secondResult.OffsetX;

            // 这里可以添加更复杂的计算逻辑
            // 例如：假设相机有一定角度，通过两个高度下的偏移可以计算出真实偏差
            // double angle = Math.Atan2(offsetX2 - offsetX1, secondHeight - firstHeight);
            // double compensation = offsetX1 + firstHeight * Math.Tan(angle);

            // 当前简单实现：取平均值
            double compensation = (offsetX1 + offsetX2) / 2.0;

            _logger.Info($"计算补偿值: 高度{firstHeight}mm偏移{offsetX1:F3}mm, 高度{secondHeight}mm偏移{offsetX2:F3}mm, 计算得{compensation:F3}mm");

            return compensation;
        }
        
        /// <summary>
        /// 计算Actuator X方向补偿值（简单算法：保持X2距离X1的标准间距）
        /// </summary>
        private double CalculateActuatorXCompensationSimple(
            VisionResult firstResult,
            VisionResult secondResult,
            double standardSpacing)
        {
            try
            {
                double offsetX1 = firstResult.OffsetX;
                double offsetX2 = secondResult.OffsetX;

                _logger.Info($"简单计算补偿值: X1偏移={offsetX1:F3}mm, X2偏移={offsetX2:F3}mm, 标准间距={standardSpacing:F3}mm");

                // 1. 计算当前X2距离X1的实际间距
                double currentSpacing = offsetX2 - offsetX1;

                _logger.Info($"当前X2-X1间距={currentSpacing:F3}mm");

                // 2. 计算需要调整的补偿值
                double compensation = standardSpacing - currentSpacing;

                _logger.Info($"计算补偿值: C = {standardSpacing:F3} - {currentSpacing:F3} = {compensation:F3}mm");

                // 3. 验证调整后效果
                double adjustedX1 = offsetX1;  // X1保持不变
                double adjustedX2 = offsetX2 + compensation;
                double adjustedSpacing = adjustedX2 - adjustedX1;

                _logger.Info($"调整后: X1'={adjustedX1:F3}mm, X2'={adjustedX2:F3}mm, 间距={adjustedSpacing:F3}mm");

                return compensation;
            }
            catch (Exception ex)
            {
                _logger.Error($"计算补偿值时发生异常: {ex.Message}");
                // 异常情况下退回简单平均值
                return (firstResult.OffsetX + secondResult.OffsetX) / 2.0;
            }
        }

        /// <summary>
        /// 计算标准间距
        /// </summary>
        private double GetStandardSpacing()
        {
            // 从配方获取标准间距，如果没有配置则使用默认值
            return _recipeService.Parameters?.ActuatorStandardSpacing ?? 1.0; // 默认1mm
        }
        /// <summary>
        /// 移动到Actuator拍照位置
        /// </summary>
        private async Task<bool> MoveToActuatorPhotoPositionAsync(double zHeight,int groupIndex, int actuatorIndex)
        {
            try
            {
                _logger.Info($"移动到Actuator拍照位置，Z轴高度: {zHeight}mm");

                // 移动X轴和Y轴到拍照位置
                await _dispenserStation.MoveToActuatorPhotoPositionAsync(groupIndex, 1, zHeight);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"移动到Actuator拍照位置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 调整DispZ3轴高度
        /// </summary>
        private async Task<bool> MoveDispZ3ToHeightAsync(double targetHeight, CancellationToken cancellationToken = default)
        {
            try
            {
                double speed = 10.0;
                _logger.Info($"移动DispZ3轴到高度{targetHeight:F2}mm，速度{speed:F2}");

                MoveAbs(DispZ3.ActId, targetHeight, speed);

                if (WaitMoveDone())
                {
                    await Task.Delay(100, cancellationToken); // 等待稳定
                    return true;
                }
                else
                {
                    _logger.Error("DispZ3轴移动超时");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info("DispZ3轴移动被取消");
                MoveStop();
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"移动DispZ3轴失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 调整DispX轴位置
        /// </summary>
        private async Task<bool> AdjustDispXAxisAsync(double compensation, CancellationToken cancellationToken = default)
        {
            try
            {
                double currentX = GetAxisPosition(AsmX.ActId);
                double targetX = currentX + compensation;

                _logger.Info($"调整DispX轴: 当前位置{currentX:F3}mm，补偿{compensation:F3}mm，目标位置{targetX:F3}mm");

                double speed = 1.5; // 调整速度较慢，保证精度
                MoveAbs(AsmX.ActId, targetX, speed);

                if (WaitMoveDone())
                {
                    double newX = GetAxisPosition(AsmX.ActId);
                    _logger.Info($"AsmX轴调整完成，新位置{newX:F3}mm");

                    await Task.Delay(200, cancellationToken); // 等待稳定
                    return true;
                }
                else
                {
                    _logger.Error("AsmX轴移动超时");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info("AsmX轴移动被取消");
                MoveStop();
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"调整AsmX轴失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 恢复到初始位置
        /// </summary>
        private async Task<bool> ReturnToInitialPositionAsync(double initialX, double initialY, double initialZ, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Info("恢复轴到初始位置");

                // 1. 抬升Z轴
                double safeZ = 0;// _recipeService.Parameters?.SafeZHeight ?? 0.0;
                MoveAbs(DispZ3.ActId, safeZ, 10.0);
                if (!WaitMoveDone())
                {
                    _logger.Error("抬升Z轴失败");
                    return false;
                }

                // 2. 移动X和Y轴到初始位置
                var axes = new[] { DispX, DispY_1 };
                var velocities = new[] { 20.0, 20.0 };
                var positions = new[] { initialX, initialY };

                if (!MoveMultiAxisToPosition(axes, positions, velocities))
                {
                    throw new InvalidOperationException($"移动到initialX,initialY失败");
                }

                // 3. 移动Y-1轴到初始位置
                if (!MoveDispYAxisStandbyPos())
                {
                    _logger.Error("移动Y-1轴到初始位置失败");
                    return false;
                }

                await Task.Delay(200, cancellationToken);
                _logger.Info("所有轴已恢复到初始位置");

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"恢复初始位置失败: {ex.Message}");
                return false;
            }
        }
        private bool MoveDispYAxisStandbyPos()
        {
            return _dispenserStation.MoveDispYAxisStandbyPos();
        }
    }
}
using Recipe;
using Stations.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stations
{ 
    public partial class DispenserStation
    {
        /// <summary>
        /// 纠正Tab位置
        /// </summary>
        /// <param name="tabIndex">序号，默认为1</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="progressCallback">进度回调</param>
        /// <returns>Tab位置纠正是否成功</returns>
        public async Task<bool> CalculateTabCompensationAsync(
            int tabIndex = 1,
            CancellationToken cancellationToken = default,
            IProgress<(int progress, string status)> progressCallback = null)
        {
            try
            {
                _logger.Info($"【点胶工站】开始Tab{tabIndex}的位置纠正流程");

                // 步骤1: 移动相机到Tab拍照位置
                progressCallback?.Report((0, $"移动到Tab{tabIndex}的拍照位置..."));
                _logger.Info($"步骤1: 移动到Tab{tabIndex}的拍照位置");

                if (!await MoveToTabPhotoPositionAsync(tabIndex))
                {
                    _logger.Error($"移动到Tab{tabIndex}的拍照位置失败");
                    progressCallback?.Report((0, $"移动到Tab拍照位置失败"));
                    return false;
                }
                progressCallback?.Report((20, $"已到达Tab{tabIndex}的拍照位置"));

                // 步骤2: 触发Tab拍照
                progressCallback?.Report((25, "触发Tab拍照..."));
                _logger.Info($"步骤2: 触发Tab{tabIndex}拍照");

                string cameraName = "DispensingCamera";
                string photoCommand = $"Tab{tabIndex}";

                var photoResult = await _cameraController.TakePhotoAsync(cameraName, photoCommand);
                if (!photoResult)
                {
                    _logger.Error($"Slot{tabIndex}的Tab拍照失败");
                    progressCallback?.Report((25, $"Tab拍照失败"));
                    return false;
                }

                // 步骤3: 等待相机返回结果
                progressCallback?.Report((30, "等待视觉系统处理照片..."));
                var firstVisionResult = await WaitForVisionSystemPhotoComplete(cameraName);

                if (!firstVisionResult.Success)
                {
                    _logger.Error($"Slot{tabIndex}的Tab视觉数据获取失败");
                    progressCallback?.Report((30, $"视觉数据获取失败: {firstVisionResult.Message}"));
                    return false;
                }

                // 解析第一次拍照的偏移量
                progressCallback?.Report((40, "解析偏移补偿值..."));
                _logger.Info($"步骤3: 解析Tab{tabIndex}的偏移量");

                double offsetX = firstVisionResult.OffsetX;
                double offsetY = firstVisionResult.OffsetY;
                _logger.Info($"获取到Tab偏移量: X={offsetX:F3}mm, Y={offsetY:F3}mm");

                // 检查偏移量是否在允许范围内
                double maxOffsetX = GetMaxTabOffsetX();
                double maxOffsetY = GetMaxTabOffsetY();

                if (Math.Abs(offsetX) > 6 || Math.Abs(offsetY) > 6)  // maxOffsetX  maxOffsetY
                {
                    _logger.Warn($"Tab偏移量超出允许范围(X上限{maxOffsetX:F3}mm, Y上限{maxOffsetY:F3}mm)，需要调整");
                }
                else
                {
                    _logger.Info($"Tab偏移量在允许范围内，无需调整");
                    progressCallback?.Report((100, $"Tab位置已在允许范围内"));
                    //return true;
                }

                // 步骤4: 根据XY补偿值，移动X轴和Y轴
                progressCallback?.Report((50, $"应用位置补偿: X={-offsetX:F3}mm, Y={-offsetY:F3}mm..."));
                _logger.Info($"步骤4: 根据偏移量调整位置");

                //    // 更新补偿值
                _compensationService.UpdateCompensation(tabIndex, CompensationType.Tab,
                        new CompensationData
                        {
                            CompensationX = offsetX,
                            CompensationY = offsetY,
                            Source = "DispenserStation"
                        });
                _logger.Info($"Tab位置纠正成功，当前偏移: X={offsetX:F3}mm, Y={offsetY:F3}mm");
                //if (!await ApplyTabPositionCompensationAsync(offsetX, offsetY, cancellationToken))
                //{
                //    _logger.Error("应用Tab位置补偿失败");
                //    progressCallback?.Report((50, "位置补偿失败"));
                //    return false;
                //}
                progressCallback?.Report((100, "位置补偿完成"));
                return true;
                //// 步骤5: 重新拍照确认位置
                //progressCallback?.Report((75, "重新拍照确认位置..."));
                //_logger.Info("步骤5: 重新拍照确认Tab位置");

                //// 如果移动了位置，需要确保相机仍然在正确的位置
                //if (!await MoveToTabPhotoPositionAsync(tabIndex))
                //{
                //    _logger.Error($"重新移动到Tab拍照位置失败");
                //    progressCallback?.Report((75, $"重新移动到拍照位置失败"));
                //    return false;
                //}

                // 触发第二次拍照
                //var secondPhotoResult = await _cameraController.TakePhotoAsync(cameraName, photoCommand);
                //if (!secondPhotoResult)
                //{
                //    _logger.Error($"Tab第二次拍照失败");
                //    progressCallback?.Report((75, "第二次拍照失败"));
                //    return false;
                //}

                //// 步骤6: 等待相机返回结果确认是否到位
                //progressCallback?.Report((80, "等待视觉系统处理确认照片..."));
                //var secondVisionResult = await WaitForVisionSystemPhotoComplete(cameraName);

                //    if (!secondVisionResult.Success)
                //    {
                //    _logger.Error($"Tab第二次视觉数据获取失败");
                //    progressCallback?.Report((80, "第二次视觉数据获取失败"));
                //    return false;
                //    }

                //// 解析第二次的偏移量
                //double secondOffsetX = secondVisionResult.OffsetX;
                //double secondOffsetY = secondVisionResult.OffsetY;
                //progressCallback?.Report((90, "验证纠正结果..."));

                //// 检查纠正后的偏移量是否在允许范围内
                //if (Math.Abs(secondOffsetX) <= maxOffsetX && Math.Abs(secondOffsetY) <= maxOffsetY)  // maxOffsetX   maxOffsetY
                //{
                //    _logger.Info($"Tab位置纠正成功，当前偏移: X={secondOffsetX:F3}mm, Y={secondOffsetY:F3}mm");
                //    progressCallback?.Report((100, $"Tab位置纠正完成，当前偏移: X={secondOffsetX:F3}mm, Y={secondOffsetY:F3}mm"));

                //    // 更新补偿值
                //    _compensationService.UpdateCompensation(tabIndex, CompensationType.Tab,
                //            new CompensationData
                //            {
                //                OffsetX = offsetX,
                //                OffsetY = offsetY,
                //                Source = "DispenserStation"
                //            });

                //    return true;
                //}
                //else
                //{
                //    _logger.Warn($"Tab位置纠正后仍超出允许范围，当前偏移: X={secondOffsetX:F3}mm, Y={secondOffsetY:F3}mm");
                //    progressCallback?.Report((100, $"位置纠正后偏移仍较大: X={secondOffsetX:F3}mm, Y={secondOffsetY:F3}mm"));

                //    bool needSecondAdjustment = await CheckIfNeedSecondAdjustment(secondOffsetX, secondOffsetY);

                //    if (needSecondAdjustment)
                //    {
                //        _logger.Info("进行第二次位置调整");
                //        return await ApplySecondAdjustmentAsync(tabIndex, secondOffsetX, secondOffsetY, cancellationToken, progressCallback);
                //    }

                //    return false;
                //}
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"Tab位置纠正被用户取消");
                MoveStop();
                progressCallback?.Report((0, "操作已取消"));
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Tab位置纠正流程异常");
                progressCallback?.Report((0, $"位置纠正异常: {ex.Message}"));
                return false;
            }
        }
        /// <summary>
        /// 应用Tab位置补偿
        /// </summary>
        private async Task<bool> ApplyTabPositionCompensationAsync(double offsetX, double offsetY, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Info($"应用Tab位置补偿: offsetX={offsetX:F3}mm, offsetY={offsetY:F3}mm");

                // 如果偏移量很小，可以直接忽略
                double minCompensation = GetMinTabCompensationThreshold();
                if (Math.Abs(offsetX) < minCompensation && Math.Abs(offsetY) < minCompensation)
                {
                    _logger.Info($"偏移量小于最小补偿阈值({minCompensation:F3}mm)，忽略补偿");
                    return true;
                }

                // 获取当前位置
                double currentX = GetAxisPosition(DispX.ActId);
                double currentY = GetAxisPosition(DispY_1.ActId);

                // 计算目标位置（注意：偏移量是相对于当前位置的，所以是减去偏移量）
                double targetX = currentX - offsetX;  // 补偿方向可能需要根据坐标系调整
                double targetY = currentY - offsetY;

                // 检查目标位置是否在安全范围内
                if (!IsPositionSafe(targetX, targetY))
                {
                    _logger.Error($"目标位置超出安全范围: X={targetX:F3}, Y={targetY:F3}");
                    return false;
                }

                _logger.Info($"应用补偿: X轴 {currentX:F3} -> {targetX:F3}, Y轴 {currentY:F3} -> {targetY:F3}");

                // 使用多轴同步运动进行补偿
                var axes = new[] { DispX, DispY_1 };
                var positions = new[] { targetX, targetY };
                var speeds = new[] {
                        _axisConfigService.GetAxisSpeed(0, DispX.ActId) * 0.5, // 补偿时使用较慢速度
                        _axisConfigService.GetAxisSpeed(0, DispY_1.ActId) * 0.5
                    };

                if (MoveMultiAxisToPosition(axes, positions, speeds))
                {
                    _logger.Info("位置补偿完成");

                    // 等待稳定
                    await Task.Delay(200, cancellationToken);

                    return true;
                }
                else
                {
                    _logger.Error("位置补偿启动失败");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"位置补偿被取消");
                MoveStop();
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"应用位置补偿失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查是否需要第二次调整
        /// </summary>
        private async Task<bool> CheckIfNeedSecondAdjustment(double offsetX, double offsetY)
        {
            // 根据偏移量和系统精度决定是否需要二次调整
            double maxAllowableError = GetMaxAllowableTabError();
            double currentError = Math.Sqrt(offsetX * offsetX + offsetY * offsetY);

            return currentError > maxAllowableError;
        }

        /// <summary>
        /// 应用第二次调整
        /// </summary>
        private async Task<bool> ApplySecondAdjustmentAsync(int slotIndex, double offsetX, double offsetY,
            CancellationToken cancellationToken, IProgress<(int progress, string status)> progressCallback)
        {
            try
            {
                _logger.Info($"进行Tab位置第二次调整: offsetX={offsetX:F3}mm, offsetY={offsetY:F3}mm");
                progressCallback?.Report((0, "进行第二次位置调整..."));

                // 应用第二次补偿
                if (!await ApplyTabPositionCompensationAsync(offsetX, offsetY, cancellationToken))
                {
                    progressCallback?.Report((0, "第二次调整失败"));
                    return false;
                }

                // 重新拍照验证
                progressCallback?.Report((50, "第二次调整后重新拍照验证..."));

                string cameraName = "SideCamera";
                string photoCommand = $"TAKE_TAB_PHOTO:Slot{slotIndex}";

                var photoResult = await _cameraController.TakePhotoAsync(cameraName, photoCommand);
                if (!photoResult)
                {
                    progressCallback?.Report((50, "第二次调整后拍照失败"));
                    return false;
                }

                var visionResult = await WaitForVisionSystemPhotoComplete(cameraName);
                if (!visionResult.Success)
                {
                    progressCallback?.Report((50, "第二次调整后视觉数据获取失败"));
                    return false;
                }

                // 检查最终结果
                double finalOffsetX = visionResult.OffsetX;
                double finalOffsetY = visionResult.OffsetY;
                double maxOffsetX = GetMaxTabOffsetX();
                double maxOffsetY = GetMaxTabOffsetY();

                if (Math.Abs(finalOffsetX) <= maxOffsetX && Math.Abs(finalOffsetY) <= maxOffsetY)
                {
                    _logger.Info($"第二次调整成功，最终偏移: X={finalOffsetX:F3}mm, Y={finalOffsetY:F3}mm");
                    progressCallback?.Report((100, $"第二次调整成功，最终偏移: X={finalOffsetX:F3}mm, Y={finalOffsetY:F3}mm"));
                    return true;
                }
                else
                {
                    _logger.Error($"第二次调整后仍然超出允许范围: X={finalOffsetX:F3}mm, Y={finalOffsetY:F3}mm");
                    progressCallback?.Report((100, $"第二次调整后仍然超出范围"));
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"第二次调整异常: {ex.Message}");
                progressCallback?.Report((0, $"第二次调整异常: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// 获取Tab允许的最大X偏移
        /// </summary>
        private double GetMaxTabOffsetX()
        {
            // 从配方或配置获取
            return _recipeService.Parameters?.TabMaxOffsetX ?? 0.5; // 默认0.5mm
        }

        /// <summary>
        /// 获取Tab允许的最大Y偏移
        /// </summary>
        private double GetMaxTabOffsetY()
        {
            // 从配方或配置获取
            return _recipeService.Parameters?.TabMaxOffsetY ?? 0.5; // 默认0.5mm
        }

        /// <summary>
        /// 获取最小补偿阈值
        /// </summary>
        private double GetMinTabCompensationThreshold()
        {
            // 小于此值的偏移量忽略不计
            return _recipeService.Parameters?.MinTabCompensationThreshold ?? 0.05; // 默认0.05mm
        }

        /// <summary>
        /// 获取最大允许的Tab误差
        /// </summary>
        private double GetMaxAllowableTabError()
        {
            // 用于决定是否需要二次调整的阈值
            return _recipeService.Parameters?.MaxAllowableTabError ?? 0.2; // 默认0.2mm
        }

        /// <summary>
        /// 检查位置是否安全
        /// </summary>
        private bool IsPositionSafe(double x, double y)
        {
            return true;
        }
    }
}
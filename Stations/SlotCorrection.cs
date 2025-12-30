using Framework.Services;
using MaterialDesignThemes.Wpf;
using Stations.Services;
using System;
using System.Threading.Tasks;

namespace Stations
{
    /// <summary>
    /// Slot for Correcting the Assembly Station
    /// </summary>
    public partial class AssemblyStation
    {
        double stripperX = 0;
        double stripperZ = 0;
        /// <summary>
        /// 执行Solt拨正角度动作
        /// </summary>
        public async Task<bool> AlignSlotAngleAsync()
        {
            try
            {
                _logger.Info("开始执行Slot拨正角度动作");

                // 步骤1: 移动到侧相机拍照位触发拍照
                _logger.Info("步骤1: 移动到侧相机拍照位");
                if (!await MoveToSideCameraForSlotCorrection())
                {
                    _currentAssemblyState = AssemblyState.Error;
                    _logger.Error("移动到侧相机拍照位失败");
                    return false;
                }

                // 等待视觉系统完成拍照
                var waitTask1 = WaitForVisionSystemPhotoComplete();

                // 触发侧相机拍照
                var firstPhotoSuccess = await TakePhotoAsync("SideCamera", "Slot");
                if (!firstPhotoSuccess)
                {
                    _logger.Error($"第一次侧相机拍照失败");
                    return false;
                }
                var firstPhotoResult = await waitTask1;
                if (!firstPhotoResult.Success)
                {
                    var result = DialogService.ShowBlockingDialog(
                                                   title: "⚠️警告",
                                                   message: "【组装工站】Slot第1次拍照获取结果,超时" + "\r\n",
                                                   yesButtonText: "重试",
                                                   noButtonText: "继续",
                                                   extraButtonText: "",
                                                   showExtraButton: false,
                                                   showYesButton: true);
                    return false;
                }

                // 步骤2: 解析获取到Slot的纠正角度
                _logger.Info("步骤2: 解析Slot纠正角度");
                double correctionAngle = firstPhotoResult.OffsetU;
                _logger.Info($"获取到Slot纠正角度: {correctionAngle:F3}度");

                // 如果角度在允许误差范围内，则不需要调整
                double tolerance = _recipeService.Parameters.SlotAngleTolerance;
                if (Math.Abs(correctionAngle) <= tolerance)
                {
                    stripperX = firstPhotoResult.OffsetX;
                    stripperZ = firstPhotoResult.OffsetY;
                    _currentAssemblyState = AssemblyState.PerformPickSlot;
                    _logger.Info($"Slot角度在允许误差范围内({tolerance}F3度)，无需调整");
                    return true;
                }

                // 步骤3: 旋转U轴纠正角度
                _logger.Info($"步骤3: 旋转U轴纠正角度 {correctionAngle:F3}度");
                if (!await RotateUAxisForCorrection(correctionAngle))
                {
                    _logger.Error("旋转U轴纠正角度失败");
                    return false;
                }

                // 步骤4: 到侧相机拍照位重新拍照
                _logger.Info("步骤4: 重新移动到侧相机拍照位");
                if (!await MoveToSideCameraForSlotCorrection())
                {
                    _logger.Error("重新移动到侧相机拍照位失败");
                    return false;
                }

                // 步骤5: 获取offsetX offsetY 纠正量
                var waitTask2 = WaitForVisionSystemPhotoComplete();

                // 触发第二次侧相机拍照
                var secondPhotoSuccess = await TakePhotoAsync("SideCamera", "Slot");
                if (!secondPhotoSuccess)
                {
                    _logger.Error($"第二次侧相机拍照失败");
                    return false;
                }

                var secondPhotoResult = await waitTask2;

                if (!secondPhotoResult.Success)
                {
                    var result = DialogService.ShowBlockingDialog(
                                                   title: "⚠️警告",
                                                   message: "【组装工站】Slot第2次拍照获取结果,超时" + "\r\n",
                                                   yesButtonText: "重试",
                                                   noButtonText: "继续",
                                                   extraButtonText: "",
                                                   showExtraButton: false,
                                                   showYesButton: true);
                    return false;
                }

                _logger.Info("步骤5: 解析offsetX offsetY纠正量");
                _logger.Info($"获取到偏移纠正量: offsetX={secondPhotoResult.OffsetX:F3}mm, offsetY={secondPhotoResult.OffsetY:F3}mm");


                // 检查偏移量是否在允许范围内(-0.5°到 +0.5°)
                double offsetXTolerance = _recipeService.Parameters.SlotCenterXMaxOffset;
                double offsetYTolerance = _recipeService.Parameters.SlotCenterYMaxOffset;
                if (Math.Abs(secondPhotoResult.OffsetX) > offsetXTolerance || Math.Abs(secondPhotoResult.OffsetY) > offsetYTolerance)
                {
                    _logger.Warn($"Slot偏移量超出允许范围(X上限{offsetXTolerance:F3},Y上限{offsetYTolerance:F3}mm)，可能需要进一步调整");

                    var result = DialogService.ShowBlockingDialog(
                                                   title: "⚠️警告",
                                                   message: "【组装工站】Slot偏移量超出允许范围(X上限{offsetXTolerance:F3},Y上限{offsetYTolerance:F3}mm" + "\r\n",
                                                   yesButtonText: "重试",
                                                   noButtonText: "继续",
                                                   extraButtonText: "",
                                                   showExtraButton: false,
                                                   showYesButton: true,
                                                   showNoButton: true,
                                                   icon: PackIconKind.ClockAlert
                                                 );
                    return false;
                }
                else
                {
                    stripperX = secondPhotoResult.OffsetX + 2.0;  //加了补偿
                    stripperZ = secondPhotoResult.OffsetY;
                    _logger.Info("偏移量在允许范围内，调整完成");
                }
                _currentAssemblyState = AssemblyState.PerformPickSlot;
                _logger.Info("Slot拨正角度动作执行完成");
                return true;
            }
            catch (Exception ex)
            {
                _currentAssemblyState = AssemblyState.Error;
                _logger.Error($"执行拨片动作失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 移动到侧相机拍照位进行Slot角度校正
        /// </summary>
        private async Task<bool> MoveToSideCameraForSlotCorrection()
        {
            try
            {
                _logger.Info("移动轴到侧相机Slot校正拍照位");

                // 移动到侧相机拍照位
                if (!MoveZAxisStandbyPos())
                {
                    _logger.Error("Z轴回到待机位失败");
                    return false;
                }

                // X轴到侧相机Slot校正拍照位
                if (!MoveAxisToPosition(AsmX, "侧相机拍照位", _axisConfigService.GetAxisSpeed(0, AsmX.ActId)))
                {
                    _logger.Error("X轴移动到侧相机Slot校正拍照位失败");
                    return false;
                }

                // Z轴到侧相机Slot校正拍照位
                if (!MoveAxisToPosition(AsmZ, "侧相机拍照位", _axisConfigService.GetAxisSpeed(0, AsmZ.ActId)))
                {
                    _logger.Error("Z轴移动到侧相机Slot校正拍照位失败");
                    return false;
                }

                // 侧相机Y轴到Slot校正拍照位
                if (!MoveAxisToPosition(AsmCamY, "侧相机拍照位", _axisConfigService.GetAxisSpeed(0, AsmCamY.ActId)))
                {
                    _logger.Error("侧相机Y轴移动到Slot校正拍照位失败");
                    return false;
                }

                // 等待稳定
                await Task.Delay(300);

                _logger.Info("已移动到侧相机Slot校正拍照位");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"移动到侧相机Slot校正拍照位失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 旋转U轴进行角度校正
        /// </summary>
        private async Task<bool> RotateUAxisForCorrection(double correctionAngle)
        {
            try
            {
                _logger.Info($"开始旋转U轴进行角度校正: {correctionAngle:F3}度");

                // 获取U轴当前位置
                double currentAngle = GetAxisPosition(AsmU.ActId);
                _logger.Info($"U轴当前角度: {currentAngle:F3}度");

                // 计算目标角度
                double targetAngle = currentAngle + correctionAngle;

                // 检查角度范围限制
                double minAngle = _recipeService.Parameters.UAxisMinAngle;
                double maxAngle = _recipeService.Parameters.UAxisMaxAngle;

                if (correctionAngle < minAngle || correctionAngle > maxAngle)
                {
                    _logger.Error($"目标角度{targetAngle:F3}度超出允许范围[{minAngle:F3}, {maxAngle:F3}]");
                    return false;
                }

                // 旋转U轴到目标角度
                double speed = _recipeService.Parameters.UAxisCorrectionSpeed;
                _logger.Info($"旋转U轴到目标角度: {targetAngle:F3}度，速度: {speed:F2}");

                MoveAbs(AsmU.ActId, targetAngle, speed);

                // 等待运动完成
                if (WaitMoveDone())
                {
                    _logger.Info($"U轴旋转完成，当前角度: {GetAxisPosition(AsmU.ActId):F3}度");

                    // 等待稳定
                    await Task.Delay(200);

                    return true;
                }
                else
                {
                    _logger.Error("U轴旋转超时");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"旋转U轴失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 应用位置补偿
        /// </summary>
        private async Task<bool> ApplyPositionCompensation(double offsetX, double offsetY)
        {
            try
            {
                _logger.Info($"应用位置补偿: offsetX={offsetX:F3}mm, offsetY={offsetY:F3}mm");

                // 如果偏移量很小，可以直接忽略
                double minCompensation = 0.5; // _recipeService.Parameters.MinCompensationThreshold;
                if (Math.Abs(offsetX) < minCompensation && Math.Abs(offsetY) < minCompensation)
                {
                    _logger.Info($"偏移量小于最小补偿阈值({minCompensation:F3}mm)，忽略补偿");
                    return true;
                }

                // 根据偏移量调整位置（这里可能需要调整多个轴）
                // 例如：调整X轴和Z轴的位置
                double currentX = GetAxisPosition(AsmX.ActId);
                double currentZ = GetAxisPosition(AsmZ.ActId);

                double targetX = currentX + offsetX;
                double targetZ = currentZ + offsetY; // 注意：offsetY可能对应Z轴方向

                _logger.Info($"应用补偿: X轴 {currentX:F3} -> {targetX:F3}, Z轴 {currentZ:F3} -> {targetZ:F3}");

                // 使用多轴同步运动进行补偿
                var axes = new[] { AsmX, AsmZ };
                var positions = new[] { targetX, targetZ };
                var speeds = new[] {
                    _axisConfigService.GetAxisSpeed(0, AsmX.ActId),
                    _axisConfigService.GetAxisSpeed(0, AsmZ.ActId)
                };

                if (MoveMultiAxisToPosition(axes, positions, speeds))
                {
                    _logger.Info("位置补偿完成");
                    return true;
                }
                else
                {
                    _logger.Error("位置补偿失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"应用位置补偿失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 移动去拨片位置
        /// </summary>
        private async Task<bool> MoveToStripperPositionAsync()
        {
            try
            {
                _logger.Info("移动到拨片位置");

                // 拨片位置的X轴和Z轴的补偿

                bool success = await Task.Run(()=> MoveAxesToSlotPosition(stripperX, stripperZ));
                if (success)
                {
                    _logger.Info("已移动到拨片位置");
                    return true;
                }
                else
                {
                    _logger.Error("移动到拨片位置失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"移动到拨片位置失败: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// 执行拨片动作
        /// </summary>
        private async Task<bool> PerformStripperSlotAsync()
        {
            try
            {
                _logger.Info("执行拨片动作");

                // 拨片的动作逻辑
                bool moveSuccess = await Task.Run(() => MoveToStripperPositionAsync());

                await Task.Delay(50); 

                if (!moveSuccess)
                {
                    _logger.Error("拨片动作失败");
                    throw new InvalidOperationException("拨片动作失败");
                }

                bool stripperSuccess = await ExecuteStripperSlotAction();
                if (!stripperSuccess)
                {
                    _logger.Error("拨片动作失败");
                    throw new InvalidOperationException("拨片动作失败");
                }
                _currentAssemblyState = AssemblyState.CheckStripperSlotPhoto;
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"执行拨片动作失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 拨片后检查拨片位置
        /// </summary>
        private async Task<bool> CheckStripperSlotPhotoAsync()
        {
            try
            {
                _logger.Info("检查拨片位置");

                // 检查拨片的动作逻辑
                var result = await PerformSideCameraRecheckAsync();

                if (result.success)
                {
                    _currentAssemblyState = AssemblyState.MoveToBottomCameraPhotoPosition;
                    _logger.Info("拨片位置正确");
                    return true;
                }
                else
                {
                    _logger.Error("拨片位置不正确");
                    throw new InvalidOperationException("拨片位置不正确");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"检查拨片位置失败: {ex.Message}");
                return false;
            }
        }
    }
}

using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stations
{
    public partial class DispenserStation
    {
        /// <summary>
        /// Corrects the pillar angle.
        /// </summary>
        /// <param name="pillarIndex">1表示Pillar1，2表示Pillar2</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="progressCallback">进度回调</param>
        /// <returns>角度纠正是否成功</returns>
        public async Task<bool> CorrectPillarAngleAsync(
            int pillarIndex = 1,
            CancellationToken cancellationToken = default,
            IProgress<(int progress, string status)> progressCallback = null)
        {
            try
            {
                _logger.Info($"【点胶工站】开始Pillar{pillarIndex}角度纠正流程");

                // 步骤1: 移动到Pillar拍照位置
                progressCallback?.Report((0, $"移动到Pillar{pillarIndex}拍照位置..."));
                _logger.Info($"步骤1: 移动到Pillar{pillarIndex}拍照位置");

                if (!await MoveToPillar1PhotoPositionAsync(pillarIndex))
                {
                    _logger.Error($"移动到Pillar{pillarIndex}拍照位置失败");
                    progressCallback?.Report((0, $"移动到Pillar{pillarIndex}拍照位置失败"));
                    return false;
                }
                progressCallback?.Report((20, $"已到达Pillar{pillarIndex}拍照位置"));

                // 步骤2: 触发拍照
                progressCallback?.Report((25, "触发Pillar拍照..."));
                _logger.Info($"步骤2: 触发Pillar{pillarIndex}拍照");

                string cameraName = "DispensingCamera";
                string photoCommand = $"Pillar{pillarIndex}_1";

                var photoResult = await _cameraController.TakePhotoAsync(cameraName, photoCommand); 
                if (!photoResult)
                {
                    _logger.Error($"Pillar{pillarIndex}拍照失败");
                    progressCallback?.Report((25, $"Pillar{pillarIndex}拍照失败"));
                    return false;
                }

                // 等待视觉系统完成拍照
                progressCallback?.Report((30, "等待视觉系统处理照片..."));
                var visionResult = await WaitForVisionSystemPhotoComplete(cameraName);

                if (!visionResult.Success)
                {
                    _logger.Error($"Pillar{pillarIndex}视觉数据获取失败");
                    progressCallback?.Report((30, $"视觉数据获取失败: {visionResult.Message}"));
                    return false;
                }

                // 步骤3: 解析获取到Pillar的纠正角度
                progressCallback?.Report((40, "解析纠正角度..."));
                _logger.Info("步骤3: 解析Pillar纠正角度");

                double correctionAngle = ParseCorrectionAngle(visionResult);
                _logger.Info($"获取到Pillar{pillarIndex}纠正角度: {correctionAngle:F3}度");

                // 如果角度在允许误差范围内，则不需要调整
                double baseAngle = GetPillarAngleBase(pillarIndex);
                double tolerance = GetPillarAngleTolerance(pillarIndex);
                if ((Math.Abs(correctionAngle) - baseAngle ) <= tolerance)
                {
                    _logger.Info($"Pillar{pillarIndex}角度在允许误差范围内({tolerance:F3}度)，无需调整");
                    progressCallback?.Report((100, $"Pillar{pillarIndex}角度已在允许范围内"));
                    return true;
                }

                // 步骤4: 旋转U轴纠正角度
                progressCallback?.Report((50, $"旋转U轴纠正角度 {correctionAngle:F3}度..."));
                _logger.Info($"步骤4: 旋转U轴纠正角度 {correctionAngle:F3}度");

                if (!await RotateUAxisForCorrection(pillarIndex, correctionAngle, cancellationToken))
                {
                    _logger.Error("旋转U轴纠正角度失败");
                    progressCallback?.Report((50, "U轴旋转失败"));
                    return false;
                }
                progressCallback?.Report((70, "角度纠正完成"));

                // 步骤5: 重新拍照确认角度是否校正正确
                progressCallback?.Report((75, "重新拍照确认角度..."));
                _logger.Info("步骤5: 重新拍照确认角度");

                // 重新移动到拍照位置
                if (!await MoveToPillar1PhotoPositionAsync(pillarIndex))
                {
                    _logger.Error($"重新移动到Pillar{pillarIndex}拍照位置失败");
                    progressCallback?.Report((75, $"重新移动到拍照位置失败"));
                    return false;
                }

                // 触发第二次拍照
                var secondPhotoResult = await _cameraController.TakePhotoAsync(cameraName, photoCommand);
                if (!secondPhotoResult)
                {
                    _logger.Error($"Pillar{pillarIndex}第二次拍照失败");
                    progressCallback?.Report((75, "第二次拍照失败"));
                    return false;
                }

                // 等待第二次视觉数据
                progressCallback?.Report((80, "等待视觉系统处理确认照片..."));
                var secondVisionResult = await WaitForVisionSystemPhotoComplete(cameraName);

                if (!secondVisionResult.Success)
                {
                    _logger.Error($"Pillar{pillarIndex}第二次视觉数据获取失败");
                    progressCallback?.Report((80, "第二次视觉数据获取失败"));
                    return false;
                }

                // 解析第二次的纠正角度
                double secondCorrectionAngle = ParseCorrectionAngle(secondVisionResult);
                progressCallback?.Report((90, "验证纠正结果..."));

                // 检查纠正后的角度是否在允许范围内
                if (Math.Abs(secondCorrectionAngle) <= 0.35)  //tolerance
                {
                    _logger.Info($"Pillar{pillarIndex}角度纠正成功，当前误差: {secondCorrectionAngle:F3}度");
                    progressCallback?.Report((100, $"Pillar{pillarIndex}角度纠正完成，当前误差: {secondCorrectionAngle:F3}度"));
                    return true;
                }
                else
                {
                    _logger.Warn($"Pillar{pillarIndex}角度纠正后仍超出允许范围，当前误差: {secondCorrectionAngle:F3}度，允许范围: ±{tolerance:F3}度");
                    progressCallback?.Report((100, $"角度纠正后误差较大: {secondCorrectionAngle:F3}度"));
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"Pillar{pillarIndex}角度纠正被用户取消");
                MoveStop();
                progressCallback?.Report((0, "操作已取消"));
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Pillar{pillarIndex}角度纠正流程异常");
                progressCallback?.Report((0, $"角度纠正异常: {ex.Message}"));
                return false;
            }
        }

        /// <summary>
        /// 解析纠正角度
        /// </summary>
        private double ParseCorrectionAngle(VisionResult visionResult)
        {
            // 假设OffsetU就是角度偏移量
            // 根据实际情况调整
            return visionResult.OffsetU;
        }
        /// <summary>
        /// 获取Pillar角度基准角度
        /// </summary>
        private double GetPillarAngleBase(int pillarIndex)
        {
            // 从配方或配置获取角度容差
            // 示例：默认-0.3度
            return _recipeService.Parameters?.PillarBaseAngle ?? -0.3;
        }
        /// <summary>
        /// 获取Pillar角度允许的误差范围
        /// </summary>
        private double GetPillarAngleTolerance(int pillarIndex)
        {
            // 从配方或配置获取角度容差
            // 示例：默认0.5度
            return _recipeService.Parameters?.PillarAngleTolerance ?? 0.5;
        }

        /// <summary>
        /// 旋转U轴进行角度校正
        /// </summary>
        private async Task<bool> RotateUAxisForCorrection(int pillarIndex, double correctionAngle, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.Info($"开始旋转U轴进行Pillar{pillarIndex}角度校正: {correctionAngle:F3}度");

                // 获取U轴当前位置
                double currentAngle = GetAxisPosition(PlatR.ActId);
                _logger.Info($"U轴当前角度: {currentAngle:F3}度");

                // 计算目标角度
                double targetAngle = currentAngle + correctionAngle + _recipeService.Parameters ?.PillarBaseAngleCompensation ?? 0.0;

                // 检查角度范围限制
                //double minAngle = _recipeService.Parameters?.UAxisMinAngle ?? -180.0;
                //double maxAngle = _recipeService.Parameters?.UAxisMaxAngle ?? 180.0;

                //if (targetAngle < minAngle || targetAngle > maxAngle)
                //{
                //    _logger.Error($"目标角度{targetAngle:F3}度超出允许范围[{minAngle:F3}, {maxAngle:F3}]");
                //    return false;
                //}

                // 旋转U轴到目标角度
                double speed = 10.0;  // _recipeService.Parameters?.UAxisCorrectionSpeed ?? 10.0;
                _logger.Info($"旋转U轴到目标角度: {targetAngle:F3}度，速度: {speed:F2}");

                MoveAbs(PlatR.ActId, targetAngle, speed);

                // 等待运动完成
                if (WaitMoveDone())
                {
                    double newAngle = GetAxisPosition(PlatR.ActId);
                    _logger.Info($"U轴旋转完成，当前角度: {newAngle:F3}度");

                    // 等待稳定
                    await Task.Delay(200, cancellationToken);

                    return true;
                }
                else
                {
                    _logger.Error("U轴旋转超时");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"U轴旋转被取消");
                MoveStop();
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"旋转U轴失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 等待视觉系统拍照完成
        /// </summary>
        private async Task<VisionResult> WaitForVisionSystemPhotoComplete(string cameraName, int timeout = 30000)
        {
            try
            {
                _logger.Info($"等待{cameraName}视觉系统拍照完成");

                // 使用视觉数据服务等待视觉数据
                var visionData = await _visionDataService.WaitForVisionDataAsync(cameraName, timeout);

                if (visionData.Contains("SUCCESS"))
                {
                    _logger.Info($"{cameraName}视觉数据接收完成");

                    // 解析视觉数据
                    var visionResult = ParseVisionDataFromRawData(visionData);

                    if (visionResult.Success)
                    {
                        _logger.Info($"视觉数据解析成功: {visionResult}");
                        return visionResult;
                    }
                    else
                    {
                        _logger.Error($"视觉数据解析失败: {visionData}");
                        return new VisionResult { Success = false, Message = "视觉数据解析失败" };
                    }
                }
                else
                {
                    string errorMsg = visionData ?? "未知错误";
                    _logger.Error($"{cameraName}视觉系统拍照失败: {errorMsg}");
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
        /// 从原始数据解析视觉结果
        /// </summary>
        private VisionResult ParseVisionDataFromRawData(string rawData)
        {
            var result = new VisionResult
            {
                RawData = rawData,
                Success = false
            };

            try
            {
                // 解析格式: "Camera=SideCamera;VISION_RESULT:SUCCESS:offsetX=1.234,offsetY=2.345,offsetU=0.123,offsetH=3.456"
                string data = rawData;

                // 解析相机名称
                int cameraIndex = data.IndexOf("Camera=");
                int endIndex = data.IndexOf(";", cameraIndex);
                if (cameraIndex >= 0 && endIndex > cameraIndex)
                {
                    result.Camera = data.Substring(cameraIndex + 7, endIndex - cameraIndex - 7);
                }

                // 检查是否成功
                result.Success = data.Contains("VISION_RESULT:SUCCESS:");

                if (result.Success)
                {
                    // 查找偏移量开始位置
                    int offsetStart = data.IndexOf("offsetX=");
                    if (offsetStart >= 0)
                    {
                        string offsetData = data.Substring(offsetStart);

                        // 使用正则表达式提取所有数字
                        var matches = System.Text.RegularExpressions.Regex.Matches(offsetData, @"[-+]?[0-9]*\.?\d+");

                        if (matches.Count >= 4)
                        {
                            result.OffsetX = double.Parse(matches[0].Value);
                            result.OffsetY = double.Parse(matches[1].Value);
                            result.OffsetU = double.Parse(matches[2].Value);
                            result.OffsetH = double.Parse(matches[3].Value);
                        }
                        else if (matches.Count == 3)
                        {
                            result.OffsetX = double.Parse(matches[0].Value);
                            result.OffsetY = double.Parse(matches[1].Value);
                            result.OffsetU = double.Parse(matches[2].Value);
                        }
                        else if (matches.Count == 2)
                        {
                            result.OffsetX = double.Parse(matches[0].Value);
                            result.OffsetY = double.Parse(matches[1].Value);
                        }
                    }
                }
                else
                {
                    // 尝试提取错误信息
                    int errorIndex = data.IndexOf("ERROR:");
                    if (errorIndex >= 0)
                    {
                        result.Message = data.Substring(errorIndex + 6);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"解析视觉数据时发生异常: {ex.Message}");
                result.Message = $"解析视觉数据异常: {ex.Message}";
                return result;
            }
        }

    }
}

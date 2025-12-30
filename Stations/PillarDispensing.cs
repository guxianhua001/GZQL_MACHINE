using Core.Abstraction;
using Core.Models;
using Framework.Services;
using MaterialDesignThemes.Wpf;
using Stations.TaskParameters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stations
{

    public partial class DispenserStation
    {
        /// <summary>
        /// 执行Pillar点胶
        /// </summary>
        public async Task<bool> DispensePillarAsync(int pillarIndex, int selectedIndex,
            double pillarDispensingHeight, double pillarHeightDeltaZ,
            double pillarDispensingTime, bool autoDescendForDispensing,
            double calibrationDeltaX, double calibrationDeltaY,
            double compensationX, double compensationY,
            Action<string> updateStatus = null, Action<string> addLog = null)
        {
            try
            {
                updateStatus?.Invoke("运行中");

                // 1. 移动相机到Pillar拍照位置
                updateStatus?.Invoke("移动到拍照位置...");
                bool moveSuccess = false;

                moveSuccess = await ReturnToSafePositionAsync();

                if (!moveSuccess)
                {
                    throw new Exception($"移动到Pillar{pillarIndex}拍照位置失败");
                }

                if (pillarIndex == 1)
                {
                    moveSuccess = await MoveToPillar1PhotoPosAsync(selectedIndex);
                }
                else
                {
                    moveSuccess = await MoveToPillar2PhotoPosAsync(selectedIndex);
                }

                if (!moveSuccess)
                {
                    throw new Exception($"移动到Pillar{pillarIndex}拍照位置失败");
                }

                // 2. 拍照并获取偏移位置
                updateStatus?.Invoke("拍照中...");
                addLog?.Invoke($"开始Pillar{pillarIndex}拍照...");

                var pillarOffset = await CapturePillarOffsetAsync(pillarIndex, selectedIndex, addLog);
                if (!pillarOffset.Success)
                {
                    throw new Exception($"获取Pillar{pillarIndex}偏移位置失败");
                }

                // △根据pillarOffset的point数量决定点胶的次数 // 
                if (pillarOffset.Points.Count == 0)
                {
                    throw new Exception($"Pillar{pillarIndex}视觉偏移点数为0");
                }
                for (int i = 0; i < pillarOffset.Points.Count; i++)
                {
                    addLog?.Invoke($"Pillar{pillarIndex}视觉偏移: ΔX={pillarOffset.Points[i].X:F3}, ΔY={pillarOffset.Points[i].Y:F3}");
                    // 3. 计算针头实际位置
                    var needlePosition = await CalculateNeedlePositionAsync(
                        pillarOffset,
                        pillarIndex,
                        selectedIndex,
                        calibrationDeltaX,
                        calibrationDeltaY,
                        compensationX,
                        compensationY,
                        i,
                        addLog); 

                    addLog?.Invoke($"计算针头位置: X={needlePosition.X:F3}, Y={needlePosition.Y:F3}, Z={needlePosition.Z:F3}");

                    // 4. 移动到针头位置
                    updateStatus?.Invoke("移动到点胶位置...");
                    bool moveToNeedleSuccess = await MoveToTargetPositionAsync(
                        needlePosition.X,
                        needlePosition.Y,
                        5.0); // 移动速度

                    if (!moveToNeedleSuccess)
                    {
                        throw new Exception("移动到针头位置失败");
                    }

                    // 5. 下降点胶
                    if (autoDescendForDispensing)
                    {
                        updateStatus?.Invoke("点胶中...");

                        // 计算点胶高度
                        double dispensingHeight = CalculateDispensingHeight(
                            pillarIndex, selectedIndex, pillarDispensingHeight, pillarHeightDeltaZ, addLog);
                        addLog?.Invoke($"点胶高度: {dispensingHeight:F3}mm");

                        // 下降到点胶高度
                        dispensingHeight = pillarDispensingHeight;// pillarDispensingHeight;
                        if (dispensingHeight == 0.0)
                        {
                            throw new Exception("点胶高度为0，请检查参数");
                        }
                        if (!await MoveToDispensingHeightAsync(dispensingHeight))
                        {
                            throw new Exception("下降到点胶高度失败");
                        }

                        // 执行点胶 7s  pillarDispensingTime
                        pillarDispensingTime = 7000;
                        await ExecutePillarDispensingAsync(pillarDispensingTime, addLog);

                        // 抬升到安全高度
                        if (!await ReturnToSafePositionAsync())
                        {
                            addLog?.Invoke("抬升到安全高度失败，继续流程") ;
                        }
                    }
                    else
                    {
                        addLog?.Invoke("自动下降点胶已禁用，等待手动操作");
                        updateStatus?.Invoke("等待手动点胶");
                    }
                }
                updateStatus?.Invoke("完成");
                addLog?.Invoke($"Pillar{pillarIndex}点胶流程完成");
                return true;
            }
            catch (Exception ex)
            {
                updateStatus?.Invoke("错误");
                addLog?.Invoke($"Pillar点胶异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 拍照获取Pillar偏移位置
        /// </summary>
        private async Task<VisionResult> CapturePillarOffsetAsync(int pillarIndex, int groupIndex, Action<string> addLog = null)
        {
            try
            {
                addLog?.Invoke($"开始处理Pillar{pillarIndex} (组{groupIndex})...");

                string cameraName = "DispensingCamera";

                int retryCount = 0;
                const int maxRetries = 3;

                while (retryCount < maxRetries)
                {
                    // 1. 先启动等待视觉系统完成的任务
                    addLog?.Invoke($"启动视觉系统等待任务...");
                    var waitTask = WaitForVisionSystemPillarPhotoComplete(cameraName);

                    // 2. 触发拍照
                    string photoCommand = pillarIndex == 1
                        ? $"Pillar{groupIndex}_1"
                        : $"Pillar{groupIndex}_2";

                    addLog?.Invoke($"触发Pillar{pillarIndex}拍照，指令: {photoCommand}");
                    var photoResult = await _cameraController.TakePhotoAsync(cameraName, photoCommand);

                    if (!photoResult)
                    {
                        addLog?.Invoke($"Pillar{pillarIndex}拍照失败");

                        // 拍照失败，直接重试
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            addLog?.Invoke($"拍照失败，第{retryCount}次重试...");
                            await Task.Delay(500); // 短暂延时
                            continue;
                        }

                        return new VisionResult()
                        {
                            Success = false,
                            Message = $"Pillar{pillarIndex}拍照失败，已重试{maxRetries}次"
                        };
                    }

                    // 3. 等待之前启动的视觉系统任务完成
                    addLog?.Invoke($"等待视觉系统处理完成...");
                    var visionResult = await waitTask;

                    if (!visionResult.Success)
                    {
                        addLog?.Invoke($"视觉处理失败: {visionResult.Message}");

                        // 询问用户是否重试
                        var dialogResult = DialogService.ShowBlockingDialog(
                            title: "⚠️警告",
                            message: $"【点胶工站】Pillar{pillarIndex} (组{groupIndex})视觉处理失败: {visionResult.Message}\r\n是否重试？",
                            yesButtonText: "重试",
                            noButtonText: "继续",
                            extraButtonText: "",
                            showExtraButton: false,
                            showYesButton: true
                        );

                        if ((int)dialogResult == 0) // 用户选择"重试"
                        {
                            retryCount++;
                            if (retryCount < maxRetries)
                            {
                                addLog?.Invoke($"用户选择重试，第{retryCount}次重试...");
                                await Task.Delay(500); // 短暂延时
                                continue;
                            }
                            else
                            {
                                return new VisionResult()
                                {
                                    Success = false,
                                    Message = $"Pillar{pillarIndex}视觉处理失败，已重试{maxRetries}次"
                                };
                            }
                        }
                        else // 用户选择"继续"
                        {
                            addLog?.Invoke($"用户选择继续，跳过此Pillar");
                            return new VisionResult()
                            {
                                Success = false,
                                Message = $"Pillar{pillarIndex}视觉处理失败，用户选择跳过"
                            };
                        }
                    }

                    // 4. 视觉处理成功，返回结果
                    addLog?.Invoke($"Pillar{pillarIndex}视觉处理完成，获取到偏移数据");
                    return visionResult;
                }

                // 达到最大重试次数
                addLog?.Invoke($"Pillar{pillarIndex}处理失败，已达到最大重试次数{maxRetries}");
                return new VisionResult()
                {
                    Success = false,
                    Message = $"Pillar{pillarIndex}处理失败，已达到最大重试次数{maxRetries}"
                };
            }
            catch (Exception ex)
            {
                addLog?.Invoke($"获取Pillar{pillarIndex}偏移异常: {ex.Message}");
                return new VisionResult()
                {
                    Success = false,
                    Message = $"获取Pillar{pillarIndex}偏移异常: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 计算针头实际位置
        /// </summary>
        private async Task<(double X, double Y, double Z)> 
            CalculateNeedlePositionAsync(
            VisionResult pillarOffset,
            int pillarIndex,
            int groupIndex,
            double cameraNeedleDeltaX,
            double cameraNeedleDeltaY,
            double needleDeviationX,
            double needleDeviationY,
            int selectedIndex,
            Action<string> addLog = null)
        {
            try
            {
                // 1. 从针头校准参数加载针头校准信息
                var calibrationParams = await EnsureCalibrationParamsLoadedAsync();

                if (calibrationParams == null)
                {
                    addLog?.Invoke("错误：无法加载针头校准参数");
                    return (0,0,0);
                }

                // 2. 获取针头补偿值(针头校准参数)
                double compensationX = calibrationParams.CompensationXYZ.X;
                double compensationY = calibrationParams.CompensationXYZ.Y;
                double compensationZ = calibrationParams.CompensationXYZ.Z;

                // 3. 获取相机中心位置（获取拍照位置）
                string positionName = $"Pillar{groupIndex}_{pillarIndex}点胶拍照位";
                double cameraActualX = GetPosition(DispX.ActId, positionName);
                double cameraActualY = GetPosition(DispY_1.ActId, positionName);
                double cameraActualZ = GetPosition(DispZ3.ActId, positionName);

                // 4. 计算视觉偏移
                double visualOffsetX = 0;
                double visualOffsetY = 0;

                if (pillarOffset.Success)
                {
                    visualOffsetX = pillarOffset.Points[selectedIndex].X;   // 距离相机中心偏移量X
                    visualOffsetY = pillarOffset.Points[selectedIndex].Y;   // 距离相机中心偏移量Y
                }

                // 5. 获取相机与针头的固定间距(从针头标定参数中获取)
                string _customDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                       "Config",
                                       "Calibration");
                var parameters = _parameterStorage?.Load<NeedleCalibrationParameters>(
                    "NeedleCalibration",
                    _customDirectory  // 自定义目录
                );
                double calibrationDeltaX = parameters.CalibrationDeltaX;
                double calibrationDeltaY = parameters.CalibrationDeltaY;
                double calibrationDeltaZ = 0; 

                //double cameraCenterX = parameters.CameraCenterX;
                //double cameraCenterY = parameters.CameraCenterY;

                // 相机的移动量
                //double cameraMoveX = cameraActualX - cameraCenterX;
                //double cameraMoveY = cameraActualY - cameraCenterY;

                // 6. 获取针头基准高度
                double needleBaseZ = parameters.NeedleTipZ;

                needleDeviationX = parameters.CompensationX; // 针头标定的手动补偿X
                needleDeviationY = parameters.CompensationY; // 针头标定的手动补偿Y

                // 7. 计算最终位置
                double finalX = cameraActualX
                              + calibrationDeltaX
                              + visualOffsetX
                              + needleDeviationX   // 手动补偿
                              + compensationX;     // 校针器补偿
                double finalY = cameraActualY
                              + calibrationDeltaY
                              + visualOffsetY
                              + needleDeviationY     // 手动补偿
                              + compensationY;       // 校针器补偿
                double finalZ = needleBaseZ
                              + calibrationDeltaZ
                              + compensationZ;

                // 8. 记录详细的计算过程
                addLog?.Invoke($"针头位置计算:");
                addLog?.Invoke($"  校准参数状态: 已加载");
                addLog?.Invoke($"  相机位置: X={cameraActualX:F3}, Y={cameraActualY:F3}, Z={cameraActualZ:F3}");
                addLog?.Invoke($"  相机-针头间距: ΔX={calibrationDeltaX:F3}, ΔY={calibrationDeltaY:F3}, ΔZ={calibrationDeltaZ:F3}");
                addLog?.Invoke($"  视觉偏移: X={visualOffsetX:F3}, Y={visualOffsetY:F3}");
                addLog?.Invoke($"  针头偏差: X={needleDeviationX:F3}, Y={needleDeviationY:F3}");
                addLog?.Invoke($"  补偿值: X={compensationX:F3}, Y={compensationY:F3}, Z={compensationZ:F3}");
                addLog?.Invoke($"  针头基准高度: {needleBaseZ:F3}");
                addLog?.Invoke($"  最终位置: X={finalX:F3}, Y={finalY:F3}, Z={finalZ:F3}");

                return (finalX, finalY, finalZ);
            }
            catch (Exception ex)
            {
                addLog?.Invoke($"位置计算异常: {ex.Message}");
                _logger.Error(ex, "计算针头位置失败");
                return (0, 0, 0);
            }
        }

        /// <summary>
        /// 异步确保校准参数已加载
        /// </summary>
        private async Task<NeedleCalibrationParams> EnsureCalibrationParamsLoadedAsync()
        {
            NeedleCalibrationParams currentParams = null;
            try
            {
                _logger.Info("校准参数为空，开始异步加载...");

                await _needleCalibrationService.LoadParametersAsync(CurrentRecipeName);
                currentParams = NeedleCalibrationParams;

                if (currentParams != null)
                {
                    _logger.Info($"校准参数加载成功: {CurrentRecipeName}");
                }
                else
                {
                    _logger.Warn("校准参数加载失败，使用默认参数");
                    currentParams = new NeedleCalibrationParams();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "加载校准参数失败");
                currentParams = new NeedleCalibrationParams();
            }

            return currentParams;
        }

        /// <summary>
        /// 计算点胶高度
        /// </summary>
        private double CalculateDispensingHeight(int pillarIndex, int groupIndex,
            double baseHeight, double heightDelta, Action<string> addLog = null)
        {
            try
            {
                // 计算最终高度
                double finalHeight = baseHeight + heightDelta;

                // 确保高度在安全范围内
                finalHeight = Math.Max(0.1, Math.Min(finalHeight, 40.0)); // 限制在0.1-50mm之间

                return finalHeight;
            }
            catch (Exception ex)
            {
                addLog?.Invoke($"高度计算异常: {ex.Message}");
                return 10.0; // 默认高度
            }
        }

        /// <summary>
        /// 执行点胶动作
        /// </summary>
        private async Task ExecutePillarDispensingAsync(double dispensingTime, Action<string> addLog = null)
        {
            try
            {
                addLog?.Invoke($"开始点胶，时间: {dispensingTime}ms");

                // 打开胶阀
                await TriggerDispensingAsync();

                // 等待点胶时间
                await Task.Delay((int)dispensingTime);

                // 关闭胶阀
                StopDispensing();

                addLog?.Invoke("点胶完成");
            }
            catch (Exception ex)
            {
                addLog?.Invoke($"点胶异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 触发点胶动作
        /// </summary>
        public async Task TriggerDispensingAsync()
        {
            try
            {
                _logger.Debug("触发点胶...");

                m_ShotGlueSolenoid.SetDo(1);
                await Task.Delay(10); // 使用异步延迟

                _logger.Debug("点胶完成");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "点胶触发异常");
                throw;
            }
        }

        /// <summary>
        /// 停止Pillar点胶
        /// </summary>
        public void StopPillarDispensing()
        {
            try
            {
                // 停止所有运动
                StopAllMotion();

                // 关闭胶阀
                // StopDispensing();
            }
            catch (Exception ex)
            {
                // 记录日志
            }
        }

        private void StopDispensing()
        {
            m_ShotGlueSolenoid.SetDo(0);
        }

        /// <summary>
        /// 移动到Pillar1拍照位置
        /// </summary>
        public async Task<bool> MoveToPillar1PhotoPosAsync(int index)
        {
            try
            {
                _logger.Info($"移动到Pillar{index}-1拍照位置...");

                _currentPhotoGroup = index;

                await MoveToPillar1PhotoPosition();

                _logger.Info($"已移动到Pillar{index}-1拍照位置");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"移动到Pillar{index}-1拍照位置异常: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// 移动到Pillar2拍照位置
        /// </summary>
        public async Task<bool> MoveToPillar2PhotoPosAsync(int index)
        {
            try
            {
                _logger.Info($"移动到Pillar{index}-2拍照位置...");

                _currentPhotoGroup = index;

                await MoveToPillar2PhotoPosition();

                _logger.Info($"已移动到Pillar{index}-2拍照位置");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"移动到Pillar{index}-2拍照位置异常: {ex.Message}");
                return false;
            }
        }
        private async Task MoveToPillar1PhotoPosition()
        {
            string positionName = $"Pillar{_currentPhotoGroup}_1点胶拍照位";
            _logger.Info($"【点胶流程】移动到{positionName}");

            IAxis[] axes = new[] { DispX, DispY_1 };
            var velocities = new[] { _axisConfigService.GetAxisSpeed(0, DispX.ActId), _axisConfigService.GetAxisSpeed(0, DispY_1.ActId) };
            var positions = new[] { GetPosition(DispX.ActId, positionName), GetPosition(DispY_1.ActId, positionName) };

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
        private async Task MoveToPillar2PhotoPosition()
        {
            string positionName = $"Pillar{_currentPhotoGroup}_2点胶拍照位";
            _logger.Info($"【点胶流程】移动到{positionName}");

            IAxis[] axes = new[] { DispX, DispY_1 };
            var velocities = new[] { _axisConfigService.GetAxisSpeed(0, DispX.ActId), _axisConfigService.GetAxisSpeed(0, DispY_1.ActId) };
            var positions = new[] { GetPosition(DispX.ActId, positionName), GetPosition(DispY_1.ActId, positionName) };

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
        /// 等待视觉系统拍照完成
        /// </summary>
        private async Task<VisionResult> WaitForVisionSystemPillarPhotoComplete(string cameraName, int timeout = 30000)
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
                    var visionResult = ParseVisionDataFromRawDataNewFormat(visionData);

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
        /// 从原始数据解析视觉结果（支持新格式）
        /// </summary>
        private VisionResult ParseVisionDataFromRawDataNewFormat(string rawData)
        {
            var result = new VisionResult
            {
                RawData = rawData,
                Success = false,
                Camera = "",
                CenterX = 0,
                CenterY = 0,
                OffsetX = 0,
                OffsetY = 0,
                Points = new List<PointResult>()
            };

            try
            {
                // 新格式:
                // "Camera=DispensingCamera;VISION_RESULT:SUCCESS:centerX=-6.653,centerY=594.332,point1X=-1.912,point1Y=581.519,point2X=-10.394,point2Y=581.082,point3X=-19.047,point3Y=582.844"

                if (string.IsNullOrWhiteSpace(rawData))
                {
                    result.Message = "原始数据为空";
                    return result;
                }

                // 1. 解析相机名称
                int cameraStartIndex = rawData.IndexOf("Camera=", StringComparison.OrdinalIgnoreCase);
                if (cameraStartIndex >= 0)
                {
                    int cameraEndIndex = rawData.IndexOf(";", cameraStartIndex);
                    if (cameraEndIndex > cameraStartIndex)
                    {
                        result.Camera = rawData.Substring(cameraStartIndex + 7, cameraEndIndex - cameraStartIndex - 7).Trim();
                    }
                }

                // 2. 检查是否成功
                int resultIndex = rawData.IndexOf("VISION_RESULT:", StringComparison.OrdinalIgnoreCase);
                if (resultIndex < 0)
                {
                    result.Message = "未找到VISION_RESULT标识";
                    return result;
                }

                string resultPart = rawData.Substring(resultIndex);
                string[] resultSegments = resultPart.Split(':');

                if (resultSegments.Length < 2)
                {
                    result.Message = "结果格式不正确";
                    return result;
                }

                // 检查结果状态
                string status = resultSegments[1].ToUpper();
                result.Success = status == "SUCCESS";

                if (!result.Success)
                {
                    result.Message = $"视觉检测失败: {status}";
                    return result;
                }

                // 3. 如果有详细数据（第三部分及以后）
                if (resultSegments.Length >= 3)
                {
                    // 合并所有数据段（从第三个开始）
                    string dataSegment = string.Join(":", resultSegments, 2, resultSegments.Length - 2);

                    // 4. 解析点数据
                    ParsePointsFromDataSegment(dataSegment, ref result);

                }
                else
                {
                    result.Message = "无数据内容";
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

        /// <summary>
        /// 从数据段中解析点数据
        /// </summary>
        private void ParsePointsFromDataSegment(string dataSegment, ref VisionResult result)
        {
            try
            {
                // 数据段格式: 
                // "centerX=-6.653,centerY=594.332,point1X=-1.912,point1Y=581.519,point2X=-10.394,point2Y=581.082,point3X=-19.047,point3Y=582.844"

                // 按逗号分割
                string[] pairs = dataSegment.Split(',');

                Dictionary<string, double> values = new Dictionary<string, double>();

                foreach (string pair in pairs)
                {
                    // 按等号分割键值对
                    string[] keyValue = pair.Split('=');
                    if (keyValue.Length == 2)
                    {
                        string key = keyValue[0].Trim().ToLower();

                        if (double.TryParse(keyValue[1].Trim(), out double value))
                        {
                            values[key] = value;
                        }
                        else
                        {
                            _logger.Warn($"无法解析数值: {keyValue[1]}");
                        }
                    }
                }

                // 1. 解析相机中心坐标
                if (values.ContainsKey("centerx"))
                {
                    result.CenterX = values["centerx"];
                }

                if (values.ContainsKey("centery"))
                {
                    result.CenterY = values["centery"];
                }

                // 2. 查找所有点
                int pointIndex = 1;
                while (true)
                {
                    string pointXKey = $"point{pointIndex}x";
                    string pointYKey = $"point{pointIndex}y";

                    if (values.ContainsKey(pointXKey) && values.ContainsKey(pointYKey))
                    {
                        var pointResult = new PointResult
                        {
                            PointIndex = pointIndex,
                            X = values[pointXKey],
                            Y = values[pointYKey]
                        };

                        result.Points.Add(pointResult);

                        // 如果是第一个点，也将其作为OffsetX和OffsetY（兼容旧代码）
                        if (pointIndex == 1)
                        {
                            result.OffsetX = pointResult.X;
                            result.OffsetY = pointResult.Y;
                        }

                        pointIndex++;
                    }
                    else
                    {
                        break;
                    }
                }

                // 3. 如果有特殊参数，尝试解析（如角度、高度等）
                foreach (var kvp in values)
                {
                    string key = kvp.Key;

                    // 检查是否是特殊参数
                    if (key.Contains("angle") || key.Contains("theta") || key.Contains("u"))
                    {
                        result.OffsetU = kvp.Value;
                    }
                    else if (key.Contains("height") || key.Contains("z") || key.Contains("h"))
                    {
                        result.OffsetH = kvp.Value;
                    }
                    else if (key.Contains("offsetx") && !key.StartsWith("point"))
                    {
                        result.OffsetX = kvp.Value;
                    }
                    else if (key.Contains("offsety") && !key.StartsWith("point"))
                    {
                        result.OffsetY = kvp.Value;
                    }
                }

                if (result.Points.Count == 0)
                {
                    // 如果没有找到标准point格式，尝试查找其他可能的偏移量
                    if (values.TryGetValue("offsetx", out double offsetX))
                        result.OffsetX = offsetX;
                    if (values.TryGetValue("offsety", out double offsetY))
                        result.OffsetY = offsetY;
                    if (values.TryGetValue("offsetu", out double offsetU))
                        result.OffsetU = offsetU;
                    if (values.TryGetValue("offseth", out double offsetH))
                        result.OffsetH = offsetH;
                }

                // 更新消息，包含相机中心坐标信息
                if (Math.Abs(result.CenterX) > 0.001 || Math.Abs(result.CenterY) > 0.001)
                {
                    result.Message = $"成功解析到相机中心({result.CenterX:F3}, {result.CenterY:F3})和 {result.Points.Count} 个点";
                }
                else
                {
                    result.Message = $"成功解析到 {result.Points.Count} 个点";
                }
            }
            catch (Exception ex)
            {
                result.Message = $"解析点数据失败: {ex.Message}";
                _logger.Error($"解析点数据失败: {ex.Message}");
            }
        }

    }
}

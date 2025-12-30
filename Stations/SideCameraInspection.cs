using Core.Models;
using NLog;
using Stations.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stations
{
    public partial class AssemblyStation
    {
        /// <summary>
        /// 侧相机复检 XY的偏移量 给到补偿服务里
        /// </summary>
        public async Task<(bool success,
           double offsetX, double offsetY, double offsetU, double offsetH,
           double offsetX2, double offsetY2, double offsetU2, double offsetH2)>
PerformSideCameraRecheckAsync()
        {
            try
            {
                if (!MoveUAxisStandbyPos())
                {
                    _logger?.Error("移动到U轴待机位置失败");
                    return (false, 0, 0, 0, 0, 0, 0, 0, 0);
                }

                // 1.移动相机到侧相机拍照位置
                bool moveSuccess = await MoveAxesToSideCameraPhotoAsync();
                if (!moveSuccess)
                {
                    _logger?.Error("移动到侧相机拍照位置失败");
                    return (false, 0, 0, 0, 0, 0, 0, 0, 0);
                }

                _logger?.Info("已移动到侧相机拍照位置");

                // 2.触发拍照
                string cameraName = "SideCamera";
                string photoCommand = $"Slot";
                bool photoTriggered = await _cameraController.TakePhotoAsync(cameraName, photoCommand);
                if (!photoTriggered)
                {
                    _logger?.Error("触发侧相机拍照失败");
                    return (false, 0, 0, 0, 0, 0, 0, 0, 0);
                }

                _logger?.Info("侧相机拍照已触发");

                // 3.等待拍照结果
                var inspectionResult = await WaitForVisionSystemPhotoComplete(cameraName);
                if (!inspectionResult.Success)
                {
                    _logger?.Error("等待拍照结果超时或无结果");
                    return (false, 0, 0, 0, 0, 0, 0, 0, 0);
                }

                // 4.解析完整的视觉数据
                //var offsets = ParseVisionSlotData("");

                _logger?.Info($"计算得到偏移量: ΔX={inspectionResult.OffsetX2:F3}mm, ΔY={inspectionResult.OffsetY2:F3}mm");
                _logger?.Info($"完整视觉数据: offsetX={inspectionResult.OffsetX:F3}, offsetY={inspectionResult.OffsetX:F3}, offsetH={inspectionResult.OffsetH:F3}");
                _logger?.Info($"offsetX2={inspectionResult.OffsetX2:F3}, offsetY2={inspectionResult.OffsetY2:F3}, offsetH2={inspectionResult.OffsetH:F3}");

                // 5.将偏移量给到补偿服务里
                bool compensationApplied = ApplyCompensationToService(inspectionResult.OffsetX2, inspectionResult.OffsetY2);

                if (compensationApplied)
                {
                    _logger?.Info($"偏移量已应用到补偿服务: ΔX={inspectionResult.OffsetX2:F3}mm, ΔY={inspectionResult.OffsetY2:F3}mm");
                    return (true,
                            inspectionResult.OffsetX, inspectionResult.OffsetY, inspectionResult.OffsetU, inspectionResult.OffsetH,
                            inspectionResult.OffsetX2, inspectionResult.OffsetY2, inspectionResult.OffsetU, inspectionResult.OffsetH);
                }
                else
                {
                    _logger?.Error("应用偏移量到补偿服务失败");
                    return (false, 0, 0, 0, 0, 0, 0, 0, 0);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"侧相机复检异常: {ex.Message}");
                return (false, 0, 0, 0, 0, 0, 0, 0, 0);
            }
        }

        // 解析视觉数据字符串的方法
        private (double offsetX, double offsetY, double offsetU, double offsetH,
                double offsetX2, double offsetY2, double offsetU2, double offsetH2)
            ParseVisionSlotData(string data)
        {
            double offsetX = 0.0, offsetY = 0.0, offsetU = 0.0, offsetH = 0.0;
            double offsetX2 = 0.0, offsetY2 = 0.0, offsetU2 = 0.0, offsetH2 = 0.0;

            try
            {
                // 检查是否包含第二组数据
                bool hasSecondGroup = data.Contains("offsetX2");

                // 直接解析整个字符串
                var parts = data.Split(';');

                foreach (var part in parts)
                {
                    // 查找视觉结果部分
                    if (part.Contains("VISION_RESULT:SUCCESS:"))
                    {
                        // 提取偏移数据部分
                        var offsetData = part.Replace("VISION_RESULT:SUCCESS:", "");

                        // 分割成键值对
                        var keyValuePairs = offsetData.Split(',');

                        foreach (var pair in keyValuePairs)
                        {
                            var keyValue = pair.Split('=');
                            if (keyValue.Length == 2)
                            {
                                var key = keyValue[0].Trim();
                                var value = keyValue[1].Trim();

                                // 处理空值情况（如offsetH2可能为空）
                                if (string.IsNullOrEmpty(value))
                                {
                                    continue;
                                }

                                if (double.TryParse(value, out double doubleValue))
                                {
                                    // 解析所有可能的键
                                    switch (key)
                                    {
                                        case "offsetX":
                                            offsetX = doubleValue;
                                            break;
                                        case "offsetY":
                                            offsetY = doubleValue;
                                            break;
                                        case "offsetU":
                                            offsetU = doubleValue;
                                            break;
                                        case "offsetH":
                                            offsetH = doubleValue;
                                            break;
                                        case "offsetX2":
                                            offsetX2 = doubleValue;
                                            break;
                                        case "offsetY2":
                                            offsetY2 = doubleValue;
                                            break;
                                        case "offsetU2":
                                            offsetU2 = doubleValue;
                                            break;
                                        case "offsetH2":
                                            offsetH2 = doubleValue;
                                            break;
                                    }
                                }
                                else
                                {
                                    _logger?.Warn($"无法解析数值: {key}={value}");
                                }
                            }
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"解析视觉数据失败: {ex.Message}");
            }

            return (offsetX, offsetY, offsetU, offsetH, offsetX2, offsetY2, offsetU2, offsetH2);
        }

        #region 私有方法

        /// <summary>
        /// 移动轴到侧相机拍照位置
        /// </summary>
        private async Task<bool> MoveAxesToSideCameraPhotoAsync()
        {
            try
            {
                _logger.Info("移动到侧相机拍照位");

                // Z轴回到待机位
                if (!MoveZAxisStandbyPos())
                {
                    throw new InvalidOperationException("Z轴回到待机位失败");
                }
                // X轴到侧相机拍照位
                if (!MoveXAxisSideCameraPhotoPos())
                {
                    throw new InvalidOperationException("X轴回到待机位失败");
                }
                // Z轴到侧相机拍照位
                if (!MoveZAxisSideCameraPhotoPos())
                {
                    throw new InvalidOperationException("Z轴回到待机位失败");
                }
                // Y轴到侧相机拍照位
                if (!MoveCamYAxisSideCameraPhotoPos())
                {
                    throw new InvalidOperationException("侧相机Y轴回到待机位失败");
                }
                await Task.Delay(100);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"移动到侧相机拍照位置异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 计算XY偏移量
        /// </summary>
        private (double offsetX, double offsetY) CalculateXYOffset(VisionResult result)
        {
            // 偏移量
            double offsetX = result.OffsetX2;
            double offsetY = result.OffsetY2;

            return (offsetX, offsetY);
        }

        /// <summary>
        /// 将偏移量应用到补偿服务
        /// </summary>
        private bool ApplyCompensationToService(double offsetX, double offsetY)
        {
            try
            {
                // 更新补偿值
                _compensationService.UpdateCompensation(1, CompensationType.Slot,
                       new CompensationData
                       {
                           CompensationX = offsetX,
                           CompensationY = offsetY,
                           Source = "DispenserStation"
                       });

                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"应用补偿到服务异常: {ex.Message}");
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
        #endregion
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
                            result.OffsetX2 = double.Parse(matches[5].Value);
                            result.OffsetY2 = double.Parse(matches[7].Value);
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

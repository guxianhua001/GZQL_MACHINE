using Core.Abstraction;
using Core.Models;
using Recipe;
using Stations;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Threading;
using System.Threading.Tasks;

namespace Stations
{
    public partial class DispenserStation
    {
        /// <summary>
        /// Post-Assembly In-Process Quality Control (IPQC) Inspection
        /// 组装后在线质量检测
        /// </summary>

        #region IPQC检测流程
        /// <summary>
        /// 执行IPQC检测流程
        /// </summary>
        public async Task<bool> ExecuteIPQCInspection(
          int moduleNumber = 1,
          CancellationToken cancellationToken = default,
          IProgress<(int progress, string status)> progressCallback = null)
        {
            try
            {
                _logger.Info($"开始{moduleNumber}号模块IPQC检测");

                double firstHeight = 31.006; //_recipeService.Parameters?.IPQCFirstPhotoZHeight ?? 35;
                double secondHeight = 28.899; //_recipeService.Parameters?.IPQCSecondPhotoZHeight ?? 28;

                // 步骤1: 移动相机到指定位置
                if (!await MoveCameraToIPQCPosition(moduleNumber, firstHeight))
                    return false;

                // 步骤2: 第一次拍照（较低高度）
                var result1 = await TakeIPQCPhoto(moduleNumber, IPQCPhotoType.FirstHeight);
                if (!result1.Success)
                {
                    _logger.Error($"第一次拍照失败: {result1.Message}");
                    await MoveCameraToSafePosition();
                    return true;
                }

                // 步骤3: 等待相机返回结果并解析
                var visionResult1 = ParseIPQCResult(result1.RawData, moduleNumber, 1);
                if (visionResult1 == null)
                {
                    _logger.Error("第一次拍照结果解析失败");
                    return false;
                }

                // 步骤4: 移动Z轴到第二次拍照高度
                if (!await MoveToSecondPhotoHeight(moduleNumber))
                    return false;

                // 步骤5: 第二次拍照（较高高度）
                var result2 = await TakeIPQCPhoto(moduleNumber, IPQCPhotoType.SecondHeight);
                if (!result2.Success)
                {
                    _logger.Error($"第二次拍照失败: {result2.Message}");
                    return false;
                }

                // 步骤6: 等待相机返回结果并解析
                var visionResult2 = ParseIPQCResult(result2.RawData, moduleNumber, 2);
                if (visionResult2 == null)
                {
                    _logger.Error("第二次拍照结果解析失败");
                    return false;
                }

                // 步骤7: 综合判断XY间距是否合格
                bool inspectionPassed = EvaluateIPQCResult(visionResult1, visionResult2);

                // 步骤8: 记录检测结果
                await RecordIPQCResult(moduleNumber, visionResult1, visionResult2, inspectionPassed);

                // 步骤9: 根据检测结果采取相应措施
                if (!inspectionPassed)
                {
                    await HandleIPQCFailure(moduleNumber, visionResult1, visionResult2);
                }

                // 步骤10: 移动相机回安全位置
                await MoveCameraToSafePosition();

                _logger.Info($"IPQC检测完成，结果: {(inspectionPassed ? "合格" : "不合格")}");
                return inspectionPassed;
            }
            catch (Exception ex)
            {
                _logger.Error($"IPQC检测异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 移动相机到IPQC检测位置
        /// </summary>
        private async Task<bool> MoveCameraToIPQCPosition(int moduleNumber, double zHeight)
        {
            try
            {
                _logger.Info($"移动相机到{moduleNumber}号模块IPQC检测位置");

                // 获取IPQC检测位置
                string ipqcPosition = $"Actuator_{moduleNumber}拍照位";

                var axes = new[]
                {
                    DispX,  // 相机X轴
                    DispY_1,  // 相机Y轴
                };

                double[] baseVelocities = new[]
                {
                    _axisConfigService.GetAxisSpeed(0, DispX.ActId),
                    _axisConfigService.GetAxisSpeed(0, DispY_1.ActId),
                };

                // 移动到第一个拍照高度位置
                if (!MoveMultiAxisToPosition(axes, ipqcPosition, baseVelocities))
                {
                    _logger.Error($"移动相机到IPQC位置失败");
                    return false;
                }

                _logger.Info($"移动DispZ3到高度{zHeight:F2}mm");

                double speedZ = 10.0;
                MoveAbs(DispZ3.ActId, zHeight, speedZ);

                WaitMoveDone();

                // 等待运动稳定
                await Task.Delay(200);

                _logger.Info($"相机已移动到IPQC检测位置: {ipqcPosition}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"移动相机到IPQC位置异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 执行IPQC拍照
        /// </summary>
        private async Task<VisionResult> TakeIPQCPhoto(int moduleNumber, IPQCPhotoType photoType)
        {
            try
            {
                string photoName = photoType == IPQCPhotoType.FirstHeight ? "第一次拍照" : "第二次拍照";
                _logger.Info($"执行{moduleNumber}号模块{photoName}");

                // 发送拍照命令给相机
                string cameraCommand = photoType == IPQCPhotoType.FirstHeight ? "IPQC_FIRST" : "IPQC_SECOND";
                string cameraName = "DispensingCamera";

                var photoResult = await _cameraController.TakePhotoAsync(cameraName, cameraCommand);
                if (!photoResult)
                {
                    _logger.Error($"Module{moduleNumber}的IPQC拍照失败");
                    return new VisionResult
                    {
                        Success = false,
                        Message = $"视觉数据解析失败"
                    };
                }

                // 等待视觉系统完成
                var visionData = await _visionDataService.WaitForVisionDataAsync(cameraName, 10000); // 10秒超时

                if (string.IsNullOrEmpty(visionData))
                {
                    return new VisionResult
                    {
                        Success = false,
                        Message = $"相机响应超时: {photoName}"
                    };
                }

                // 解析视觉数据
                var result = ParseIPQCVisionData(visionData);
                if (result.Success == false)
                {
                    return new VisionResult
                    {
                        Success = false,
                        Message = $"视觉数据解析失败: {visionData}"
                    };
                }

                //result.PhotoType = photoType;
                //result.ModuleNumber = moduleNumber;
                //_logger.Info($"{photoName}完成，特征点数量: {result.FeaturePoints?.Count ?? 0}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"{photoType}拍照异常: {ex.Message}");
                return new VisionResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// 移动到第二次拍照高度
        /// </summary>
        private async Task<bool> MoveToSecondPhotoHeight(int moduleNumber)
        {
            try
            {
                _logger.Info($"移动到第二次拍照高度");

                // 获取第二次拍照高度（比第一次高一定距离）
                string secondHeightPosition = $"IPQC第二次拍照高度{moduleNumber}";

                // 只移动Z轴到新高度
                double secondHeight = _recipeService.Parameters?.IPQCSecondPhotoZHeight ?? 28;
                if (secondHeight == -1)
                {
                    // 如果未配置，使用默认偏移（比如比第一次高2mm）
                    double firstHeight = GetAxisPosition(DispZ3.ActId);
                    secondHeight = firstHeight + 2.0; // 第二次拍照高度比第一次高2mm
                }

                double velocity = _axisConfigService.GetAxisSpeed(0, DispZ3.ActId);

                // 移动到第二次拍照高度
                MoveAbs(DispZ3.ActId, secondHeight, velocity);

                if (WaitMoveDone())
                {
                    _logger.Info($"已移动到第二次拍照高度: {secondHeight:F3}");
                    await Task.Delay(300); // 等待稳定
                    return true;
                }
                else
                {
                    _logger.Warn($"移动到第二次拍照高度超时");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"移动到第二次拍照高度异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 解析IPQC检测结果
        /// </summary>
        private IPQCResult ParseIPQCResult(string visionData, int moduleNumber, int photoSequence)
        {
            try
            {
                _logger.Debug($"解析IPQC结果，模块: {moduleNumber}, 序号: {photoSequence}, 数据: {visionData}");

                // 假设视觉系统返回的格式为：
                // "FEATURE_COUNT=2;POINT1_X=123.45;POINT1_Y=67.89;POINT2_X=234.56;POINT2_Y=78.90;QUALITY=95"

                var result = new IPQCResult
                {
                    ModuleNumber = moduleNumber,
                    PhotoSequence = photoSequence,
                    RawData = visionData
                };

                // 解析特征点数据
                result.FeaturePoints = ParseFeaturePoints(visionData);

                // 解析质量指标
                ParseQualityIndicators(visionData, result);

                // 记录时间戳
                result.Timestamp = DateTime.Now;

                _logger.Info($"IPQC结果解析完成，特征点: {result.FeaturePoints?.Count ?? 0}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"解析IPQC结果异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析特征点数据
        /// </summary>
        private List<FeaturePoint> ParseFeaturePoints(string visionData)
        {
            var points = new List<FeaturePoint>();

            try
            {
                // 使用正则表达式匹配特征点数据
                var pointPattern = @"POINT(\d+)_X=([\d.]+);POINT\1_Y=([\d.]+)";
                var matches = System.Text.RegularExpressions.Regex.Matches(visionData, pointPattern);

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count >= 4)
                    {
                        int pointIndex = int.Parse(match.Groups[1].Value);
                        double x = double.Parse(match.Groups[2].Value);
                        double y = double.Parse(match.Groups[3].Value);

                        points.Add(new FeaturePoint
                        {
                            Index = pointIndex,
                            PixelX = x,
                            PixelY = y
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"解析特征点异常: {ex.Message}");
            }

            return points;
        }

        /// <summary>
        /// 解析质量指标
        /// </summary>
        private void ParseQualityIndicators(string visionData, IPQCResult result)
        {
            try
            {
                // 提取质量分数
                var qualityMatch = System.Text.RegularExpressions.Regex.Match(visionData, @"QUALITY=([\d.]+)");
                if (qualityMatch.Success)
                {
                    result.QualityScore = double.Parse(qualityMatch.Groups[1].Value);
                }

                // 提取对比度
                var contrastMatch = System.Text.RegularExpressions.Regex.Match(visionData, @"CONTRAST=([\d.]+)");
                if (contrastMatch.Success)
                {
                    result.Contrast = double.Parse(contrastMatch.Groups[1].Value);
                }

                // 提取清晰度
                var sharpnessMatch = System.Text.RegularExpressions.Regex.Match(visionData, @"SHARPNESS=([\d.]+)");
                if (sharpnessMatch.Success)
                {
                    result.Sharpness = double.Parse(sharpnessMatch.Groups[1].Value);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"解析质量指标异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 评估IPQC检测结果是否合格
        /// </summary>
        private bool EvaluateIPQCResult(IPQCResult result1, IPQCResult result2)
        {
            try
            {
                _logger.Info($"评估IPQC检测结果");

                // 检查是否有足够特征点
                if (result1.FeaturePoints == null || result2.FeaturePoints == null ||
                    result1.FeaturePoints.Count < 2 || result2.FeaturePoints.Count < 2)
                {
                    _logger.Error("特征点数量不足");
                    return false;
                }

                // 计算第一次拍照的特征点间距
                var point1_1 = result1.FeaturePoints[0];
                var point1_2 = result1.FeaturePoints[1];
                double distance1 = CalculateDistance(point1_1, point1_2);

                // 计算第二次拍照的特征点间距
                var point2_1 = result2.FeaturePoints[0];
                var point2_2 = result2.FeaturePoints[1];
                double distance2 = CalculateDistance(point2_1, point2_2);

                // 记录间距值
                result1.FeatureDistance = distance1;
                result2.FeatureDistance = distance2;

                // 计算间距变化（像素单位）
                double distanceChange = Math.Abs(distance2 - distance1);
                double relativeChange = distanceChange / Math.Max(distance1, distance2);

                // 获取允许的公差
                double tolerance = _recipeService.Parameters.IPQCTolerance;
                double relativeTolerance = _recipeService.Parameters.IPQCRelativeTolerance;

                _logger.Info($"间距计算结果: 第一次={distance1:F3}px, 第二次={distance2:F3}px, " +
                           $"变化={distanceChange:F3}px, 相对变化={relativeChange:P2}");

                // 评估标准：
                // 1. 绝对变化在公差范围内
                // 2. 相对变化在百分比公差范围内
                // 3. 质量分数达标
                bool absolutePass = distanceChange <= tolerance;
                bool relativePass = relativeChange <= relativeTolerance;
                bool qualityPass = result1.QualityScore >= 80 && result2.QualityScore >= 80; // 质量分数阈值

                bool overallPass = absolutePass && relativePass && qualityPass;

                if (!overallPass)
                {
                    _logger.Warn($"IPQC检测不合格: " +
                               $"绝对变化{(absolutePass ? "合格" : $"不合格({distanceChange:F3}>{tolerance:F3})")}, " +
                               $"相对变化{(relativePass ? "合格" : $"不合格({relativeChange:P2}>{relativeTolerance:P2})")}, " +
                               $"质量{(qualityPass ? "合格" : "不合格")}");
                }
                else
                {
                    _logger.Info($"IPQC检测合格");
                }

                return overallPass;
            }
            catch (Exception ex)
            {
                _logger.Error($"评估IPQC结果异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 计算两点之间的距离
        /// </summary>
        private double CalculateDistance(FeaturePoint point1, FeaturePoint point2)
        {
            double dx = point2.PixelX - point1.PixelX;
            double dy = point2.PixelY - point1.PixelY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 记录IPQC检测结果
        /// </summary>
        private async Task RecordIPQCResult(int moduleNumber, IPQCResult result1, IPQCResult result2, bool passed)
        {
            try
            {
                var record = new IPQCRecord
                {
                    ModuleNumber = moduleNumber,
                    InspectionTime = DateTime.Now,
                    FirstPhotoResult = result1,
                    SecondPhotoResult = result2,
                    Passed = passed,
                    Operator = Environment.UserName,
                    StationId = this.Station.Id
                };

                // 保存到数据库或文件
                //await _dataService.SaveIPQCRecordAsync(record);
                await Task.Delay(100); 

                // 触发结果更新事件
                OnIPQCResultRecorded?.Invoke(this, new IPQCResultEventArgs
                {
                    ModuleNumber = moduleNumber,
                    Passed = passed,
                    Distance1 = result1.FeatureDistance,
                    Distance2 = result2.FeatureDistance,
                    Change = Math.Abs(result2.FeatureDistance - result1.FeatureDistance)
                });

                _logger.Info($"IPQC检测结果已记录，模块: {moduleNumber}, 结果: {(passed ? "合格" : "不合格")}");
            }
            catch (Exception ex)
            {
                _logger.Error($"记录IPQC结果异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理IPQC检测失败
        /// </summary>
        private async Task HandleIPQCFailure(int moduleNumber, IPQCResult result1, IPQCResult result2)
        {
            try
            {
                _logger.Warn($"处理{moduleNumber}号模块IPQC检测失败");

                // 1. 发送报警
                ReportAlarm(XAlarmLevel.PAUSE,
                           (int)MachineAlarmCode.IPQC检测不合格,
                           (int)XSysAlarmId.MACHINE,
                           AlarmCategory.VISION.ToString(),
                           $"模块{moduleNumber} IPQC检测不合格");

                // 2. 暂停流程（可选）
                // this.Station.SetState(XStationState.PAUSED);

                // 3. 通知操作员
                // await ShowIPQCFailureDialog(moduleNumber, result1, result2);

                // 4. 记录失败详情
                //await RecordIPQCFailureDetail(moduleNumber, result1, result2);
                await Task.Delay(100);
                // 5. 根据配置决定是否继续或停止
                if (_recipeService.Parameters.StopOnIPQCFailure)
                {
                    _logger.Info($"根据配置，IPQC失败时停止流程");
                    // 停止后续流程
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"处理IPQC失败异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 移动相机到安全位置
        /// </summary>
        private async Task MoveCameraToSafePosition()
        {
            try
            {
                _logger.Info("移动相机到安全位置");

                var axes = new[]
                {
                    DispX,
                    DispY_1,
                };

                string safePosition = "待机位";

                if (MoveMultiAxisToPosition(axes, safePosition,
                    _axisConfigService.GetAxisSpeed(0, DispX.ActId)))
                {
                    double speedZ = 10.0;
                    double height = GetPosition(DispZ3.ActId, safePosition);
                    MoveAbs(DispZ3.ActId, height, speedZ);
                    WaitMoveDone();
                    await Task.Delay(300);
                    _logger.Info("相机已移动到安全位置");
                }
                else
                {
                    _logger.Warn("移动相机到安全位置失败");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"移动相机到安全位置异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 解析IPQC视觉数据（从相机响应）
        /// </summary>
        private VisionResult ParseIPQCVisionData(string response)
        {
            try
            {
                // 示例响应格式: "SUCCESS:FEATURE_COUNT=2;POINT1_X=123.45;POINT1_Y=67.89;POINT2_X=234.56;POINT2_Y=78.90;QUALITY=95"
                if (response.StartsWith("SUCCESS:"))
                {
                    return new VisionResult
                    {
                        Success = true,
                        RawData = response.Substring("SUCCESS:".Length)
                    };
                }
                else if (response.StartsWith("ERROR:"))
                {
                    return new VisionResult
                    {
                        Success = false,
                        Message = response.Substring("ERROR:".Length)
                    };
                }

                return new VisionResult { Success = false, Message = "响应格式错误" };
            }
            catch (Exception ex)
            {
                _logger.Error($"解析IPQC视觉数据异常: {ex.Message}");
                return new VisionResult { Success = false, Message = ex.Message };
            }
        }

        #endregion

        #region 辅助模型

        /// <summary>
        /// IPQC拍照类型
        /// </summary>
        public enum IPQCPhotoType
        {
            FirstHeight,   // 第一次拍照（较低高度）
            SecondHeight   // 第二次拍照（较高高度）
        }

        /// <summary>
        /// IPQC检测结果
        /// </summary>
        public class IPQCResult
        {
            public int ModuleNumber { get; set; }
            public int PhotoSequence { get; set; } // 1: 第一次拍照, 2: 第二次拍照
            public IPQCPhotoType PhotoType { get; set; }
            public List<FeaturePoint> FeaturePoints { get; set; }
            public double FeatureDistance { get; set; } // 特征点间距（像素）
            public double QualityScore { get; set; } // 质量分数（0-100）
            public double Contrast { get; set; } // 对比度
            public double Sharpness { get; set; } // 清晰度
            public string RawData { get; set; }
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// 特征点
        /// </summary>
        public class FeaturePoint
        {
            public int Index { get; set; }
            public double PixelX { get; set; }
            public double PixelY { get; set; }
            public double Confidence { get; set; } = 1.0;
        }

        /// <summary>
        /// IPQC检测记录
        /// </summary>
        public class IPQCRecord
        {
            public int ModuleNumber { get; set; }
            public DateTime InspectionTime { get; set; }
            public IPQCResult FirstPhotoResult { get; set; }
            public IPQCResult SecondPhotoResult { get; set; }
            public bool Passed { get; set; }
            public string Operator { get; set; }
            public int StationId { get; set; }
        }

        /// <summary>
        /// IPQC结果事件参数
        /// </summary>
        public class IPQCResultEventArgs : EventArgs
        {
            public int ModuleNumber { get; set; }
            public bool Passed { get; set; }
            public double Distance1 { get; set; }
            public double Distance2 { get; set; }
            public double Change { get; set; }
        }

        #endregion

        #region 事件定义

        /// <summary>
        /// IPQC结果记录事件
        /// </summary>
        public event EventHandler<IPQCResultEventArgs> OnIPQCResultRecorded;

        #endregion
    }

}
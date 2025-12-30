// CameraEventProcessor.cs
using Core.Abstraction;
using Core.Services;
using Core.Utilities;
using Stations.Services;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Stations.Services
{
    /// <summary>
    /// 相机事件处理器 - 处理所有TCP相机事件并分发视觉数据
    /// </summary>
    public interface ICameraEventProcessor
    {
        void Initialize(ITCPEventService tcpEventService, IVisionDataService visionDataService);
        void Start();
        void Stop();
    }

    public class CameraEventProcessor : ICameraEventProcessor, ICameraController
    {
        private readonly ILoggerService _logger;
        private ITCPEventService _tcpEventService;
        private IVisionDataService _visionDataService;
        private bool _isInitialized = false;

        // 相机类型常量
        private const string PICKUP_CAMERA = "PickupCamera";
        private const string SIDE_CAMERA = "SideCamera";
        private const string BOTTOM_CAMERA = "BottomCamera";
        private const string DISPENSING_CAMERA = "DispensingCamera";
        private const string ASSEMBLY_CAMERA = "AssemblyCamera";

        public CameraEventProcessor(ILoggerService logger)
        {
            _logger = logger;
        }

        // 在 Initialize 方法中注入依赖
        public void Initialize(ITCPEventService tcpEventService, IVisionDataService visionDataService)
        {
            _tcpEventService = tcpEventService ?? throw new ArgumentNullException(nameof(tcpEventService));
            _visionDataService = visionDataService ?? throw new ArgumentNullException(nameof(visionDataService));

            // 订阅TCP事件
            SubscribeToTcpEvents();

            _isInitialized = true;
            _logger.Info("相机事件处理器初始化完成");
        }

        public void Start()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("相机事件处理器未初始化");
            }

            _logger.Info("相机事件处理器已启动");
        }

        public void Stop()
        {
            if (_isInitialized && _tcpEventService != null)
            {
                // 取消订阅TCP事件
                UnsubscribeFromTcpEvents();
                _logger.Info("相机事件处理器已停止");
            }
        }

        private void SubscribeToTcpEvents()
        {
            _tcpEventService.CameraMessageReceived += OnCameraMessageReceived;
            _tcpEventService.CameraCommandCompleted += OnCameraCommandCompleted;
            _tcpEventService.ClientConnected += OnCameraClientConnected;
            _tcpEventService.ClientDisconnected += OnCameraClientDisconnected;
            _logger.Info("已订阅TCP事件");
        }

        private void UnsubscribeFromTcpEvents()
        {
            _tcpEventService.CameraMessageReceived -= OnCameraMessageReceived;
            _tcpEventService.CameraCommandCompleted -= OnCameraCommandCompleted;
            _tcpEventService.ClientConnected -= OnCameraClientConnected;
            _tcpEventService.ClientDisconnected -= OnCameraClientDisconnected;
            _logger.Info("已取消订阅TCP事件");
        }

        private void OnCameraMessageReceived(string cameraName, string message)
        {
            try
            {
                _logger.Info($"收到相机消息: {cameraName} - {message}");

                // 处理不同类型的相机消息
                ProcessCameraMessage(cameraName, message);
            }
            catch (Exception ex)
            {
                _logger.Error($"处理相机消息失败: {ex.Message}");
            }
        }

        private void OnCameraCommandCompleted(string cameraName, bool success)
        {
            _logger.Info($"相机命令完成: {cameraName} - {(success ? "成功" : "失败")}");

            // 这里可以记录命令执行状态，或者触发其他事件
        }

        private void OnCameraClientConnected(string clientName, string ip, int port)
        {
            _logger.Info($"相机客户端连接: {clientName} ({ip}:{port})");
        }

        private void OnCameraClientDisconnected(string clientName, string ip, int port)
        {
            _logger.Warn($"相机客户端断开: {clientName} ({ip}:{port})");
        }

        private void ProcessCameraMessage(string clientName, string message)
        {
            try
            {
                _logger.Info($"收到相机消息: 客户端={clientName}, 消息={message}");

                // 解析相机名称（假设消息格式为: CameraName:VISION_RESULT:...）
                string cameraName = ExtractCameraNameFromMessage(clientName, message);

                // 根据消息前缀处理不同类型的相机消息
                if (message.StartsWith("VISION_RESULT:") || message.Contains("VISION_RESULT:"))
                {
                    ProcessPhotoResult(cameraName, ExtractResultData(message));
                }
                else if (message.StartsWith("PHOTO_RESULT:") || message.Contains("PHOTO_RESULT:"))
                {
                    ProcessPhotoResult(cameraName, ExtractResultData(message));
                }
                // ... 其他处理
            }
            catch (Exception ex)
            {
                _logger.Error($"处理相机消息异常: {ex.Message}");
            }
        }

        // 从消息中提取相机名称
        private string ExtractCameraNameFromMessage(string clientName, string message)
        {
            try
            {
                // 方法1: 消息中包含相机名称（例如: "Camera=DispensingCamera;VISION_RESULT:..."）
                if (message.Contains("Camera="))
                {
                    var cameraPart = message.Split(';')[0];
                    if (cameraPart.StartsWith("Camera="))
                    {
                        return cameraPart.Substring("Camera=".Length);
                    }
                }
                return "Camera";
            }
            catch
            {
                return clientName; // 失败时返回客户端名称
            }
        }

        // 从消息中提取结果数据
        private string ExtractResultData(string message)
        {
            // 如果消息包含相机名称前缀，去掉它
            if (message.Contains(";"))
            {
                var parts = message.Split(';');
                if (parts.Length > 1)
                {
                    // 返回第二部分（结果数据）
                    return parts[1];
                }
            }
            return message;
        }

        private void ProcessPhotoResult(string cameraName, string message)
        {
            // 不解析数据，直接转发原始消息
            // 提取完整的拍照结果数据
            string resultData = GetFullResultData(cameraName, message);

            // 发布原始拍照数据到分发服务，各工站自己解析
            _visionDataService.PublishVisionData(cameraName, resultData);

            _logger.Info($"拍照结果已分发（原始格式）: {cameraName} - {message}");
        }

        /// <summary>
        /// 获取完整的结果数据（包含相机名称信息）
        /// </summary>
        private string GetFullResultData(string cameraName, string message)
        {
            try
            {
                // 如果消息中已包含相机名称，直接返回
                if (message.Contains("Camera="))
                {
                    return message;
                }

                // 否则添加相机名称前缀
                return $"Camera={cameraName};{message}";
            }
            catch (Exception ex)
            {
                _logger.Error($"构建完整结果数据失败: {ex.Message}");
                return message;
            }
        }

        private VisionData ParseVisionResult(string resultData)
        {
            var visionData = new VisionData();

            try
            {
                // 解析格式: "SUCCESS:X=1.234,Y=2.345,Z=3.456,Angle=0.12,Confidence=0.98"
                // 或 "ERROR:错误信息"
                if (resultData.StartsWith("SUCCESS:"))
                {
                    var dataStr = resultData.Substring("SUCCESS:".Length);
                    visionData.Success = true;
                    visionData.RawData = dataStr;

                    // 解析键值对
                    var pattern = @"([A-Za-z]+)=([+-]?\d*\.?\d+)";
                    var matches = Regex.Matches(dataStr, pattern);

                    foreach (Match match in matches)
                    {
                        var key = match.Groups[1].Value.ToUpper();
                        var value = match.Groups[2].Value;

                        switch (key)
                        {
                            case "OFFSETX":
                                visionData.OffsetX = double.Parse(value);
                                break;
                            case "OFFSETY":
                                visionData.OffsetY = double.Parse(value);
                                break;
                            case "OFFSETZ":
                                visionData.OffsetZ = double.Parse(value);
                                break;
                            case "ANGLE":
                                visionData.Angle = double.Parse(value);
                                break;
                            case "CONFIDENCE":
                                visionData.Confidence = double.Parse(value);
                                break;
                        }
                    }
                }
                else if (resultData.StartsWith("ERROR:"))
                {
                    visionData.Success = false;
                    visionData.ErrorMessage = resultData.Substring("ERROR:".Length);
                }
            }
            catch (Exception ex)
            {
                visionData.Success = false;
                visionData.ErrorMessage = $"解析视觉结果失败: {ex.Message}";
                _logger.Error($"解析视觉结果失败: {ex.Message}, 原始数据: {resultData}");
            }

            return visionData;
        }

        private VisionData ParsePhotoResult(string resultData)
        {
            // 拍照结果的简化解析
            var visionData = new VisionData();

            try
            {
                if (resultData.StartsWith("SUCCESS:"))
                {
                    var dataStr = resultData.Substring("SUCCESS:".Length);
                    visionData.Success = true;
                    visionData.RawData = dataStr;

                    // 简单解析偏移量
                    var parts = dataStr.Split(',');
                    foreach (var part in parts)
                    {
                        var trimmed = part.Trim();
                        if (trimmed.StartsWith("offsetX="))
                            visionData.OffsetX = double.Parse(trimmed.Substring("offsetX=".Length));
                        else if (trimmed.StartsWith("offsetY="))
                            visionData.OffsetY = double.Parse(trimmed.Substring("offsetY=".Length));
                        else if (trimmed.StartsWith("offsetZ="))
                            visionData.OffsetZ = double.Parse(trimmed.Substring("offsetZ=".Length));
                    }
                }
                else if (resultData.StartsWith("ERROR:"))
                {
                    visionData.Success = false;
                    visionData.ErrorMessage = resultData.Substring("ERROR:".Length);
                }
            }
            catch (Exception ex)
            {
                visionData.Success = false;
                visionData.ErrorMessage = $"解析拍照结果失败: {ex.Message}";
            }

            return visionData;
        }

        private void ProcessCameraError(string cameraName, string message)
        {
            var error = message.Substring("ERROR:".Length);
            _logger.Error($"相机错误: {cameraName} - {error}");

            // 创建错误视觉数据
            var errorData = new VisionData
            {
                Success = false,
                ErrorMessage = error
            };

            // 发布错误数据
            //_visionDataService.PublishVisionData(cameraName, errorData);
        }

        // ================================================
        // ICameraController 接口实现
        // ================================================

        public async Task<bool> SendCommandAsync(string cameraName, string command, int timeout = 5000)
        {
            try
            {
                _logger.Info($"向{cameraName}发送命令: {command}");

                // 通过TCP事件服务发送命令
                bool success = await _tcpEventService.SendCommandAsync(cameraName, command, timeout);

                if (success)
                {
                    _logger.Info($"{cameraName}命令发送成功");
                }
                else
                {
                    _logger.Error($"{cameraName}命令发送失败");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Error($"发送命令异常: {ex.Message}");
                return false;
            }
        }

        public async Task<string> SendCommandWithResponseAsync(string cameraName, string command, int timeout = 5000)
        {
            try
            {
                _logger.Info($"向{cameraName}发送命令并等待响应: {command}");

                // 通过TCP事件服务发送命令并等待响应
                string response = await _tcpEventService.SendCommandWithResponseAsync(cameraName, command, timeout);

                if (!string.IsNullOrEmpty(response))
                {
                    _logger.Info($"{cameraName}响应: {response}");

                    // 解析响应并发布视觉数据
                    ProcessCameraResponse(cameraName, response);

                    return response;
                }
                else
                {
                    _logger.Error($"{cameraName}无响应");
                    return "ERROR: No response";
                }
            }
            catch (TimeoutException)
            {
                _logger.Error($"{cameraName}响应超时");
                return "ERROR: Timeout";
            }
            catch (Exception ex)
            {
                _logger.Error($"发送命令等待响应异常: {ex.Message}");
                return $"ERROR: {ex.Message}";
            }
        }

        public async Task<bool> TakePhotoAsync(string cameraName, string command, int timeout = 5000)
        {
            try
            {
                // 如果命令为空，使用默认拍照命令
                if (string.IsNullOrEmpty(command))
                {
                    command = "TAKE_PHOTO";
                }

                _logger.Info($"向{cameraName}触发拍照: {command}");

                // 发送拍照命令
                bool success = await SendCommandAsync(cameraName, command, timeout);

                if (success)
                {
                    _logger.Info($"{cameraName}拍照命令发送成功");
                }
                else
                {
                    _logger.Error($"{cameraName}拍照命令发送失败");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Error($"触发拍照异常: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> TakeGroupPhotoAsync(int groupNumber, int positionIndex, int timeout = 10000)
        {
            try
            {
                string positionName = GetPositionName(groupNumber, positionIndex);
                string cameraName = GetCameraByPosition(groupNumber, positionIndex);

                _logger.Info($"触发组拍照: {positionName}, 相机: {cameraName}");

                // 构建拍照命令
                string command = BuildPhotoCommand(groupNumber, positionIndex);

                // 发送命令
                bool success = await SendCommandAsync(cameraName, command, timeout);

                if (success)
                {
                    _logger.Info($"{positionName}拍照命令发送成功");

                    // 等待视觉数据
                    try
                    {
                        var visionData = await _visionDataService.WaitForVisionDataAsync(cameraName, timeout);

                        if (visionData.Contains("Success"))
                        {
                            _logger.Info($"{positionName}拍照成功: {visionData}");
                            return true;
                        }
                        else
                        {
                            _logger.Error($"{positionName}拍照失败: {visionData}");
                            return false;
                        }
                    }
                    catch (TimeoutException)
                    {
                        _logger.Error($"等待{positionName}视觉数据超时");
                        return false;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"触发组拍照异常: {ex.Message}");
                return false;
            }
        }

        // ================================================
        // 内部辅助方法
        // ================================================

        private void ProcessCameraResponse(string cameraName, string response)
        {
            try
            {
                if (response.StartsWith("PHOTO_RESULT:"))
                {
                    var resultData = response.Substring("PHOTO_RESULT:".Length);
                    _visionDataService.PublishVisionData(cameraName, resultData);
                }
                else if (response.StartsWith("VISION_RESULT:"))
                {
                    var resultData = response.Substring("VISION_RESULT:".Length);
                    _visionDataService.PublishVisionData(cameraName, resultData);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"处理相机响应失败: {ex.Message}");
            }
        }

        private string GetPositionName(int groupNumber, int positionIndex)
        {
            if (positionIndex == 1) // Tab拍照
            {
                return $"第{groupNumber}组Tab拍照";
            }
            else // Pillar拍照
            {
                string pillarType = positionIndex == 2 ? "Pillar1" : "Pillar2";
                return $"第{groupNumber}组{pillarType}拍照";
            }
        }

        private string GetCameraByPosition(int groupNumber, int positionIndex)
        {
            // 根据位置选择相机
            if (positionIndex == 1) // Tab拍照 - 使用侧相机
            {
                return SIDE_CAMERA;
            }
            else // Pillar拍照 - 使用底部相机
            {
                return BOTTOM_CAMERA;
            }
        }

        private string BuildPhotoCommand(int groupNumber, int positionIndex)
        {
            if (positionIndex == 1) // Tab拍照
            {
                return $"TAKE_TAB_PHOTO:Group{groupNumber}";
            }
            else // Pillar拍照
            {
                string pillarType = positionIndex == 2 ? "Pillar1" : "Pillar2";
                return $"TAKE_PILLAR_PHOTO:Group{groupNumber}:{pillarType}";
            }
        }

        // ================================================
        // 向后兼容的旧方法（可以保留或标记为过时）
        // ================================================

        [Obsolete("请使用 SendCommandAsync 方法代替")]
        public async Task<bool> SendPhotoCommandAsync(string cameraName, string command, int timeout = 5000)
        {
            return await SendCommandAsync(cameraName, command, timeout);
        }
    }
}
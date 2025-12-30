// VisionDataService.cs
using Core.Services;
using Core.Utilities;
using Stations.Services;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Stations.Services
{
    /// <summary>
    /// 视觉数据服务 - 统一接收和分发视觉数据
    /// </summary>
    public interface IVisionDataService
    {
        event EventHandler<VisionDataEventArgs> VisionDataReceived;
        Task<string> WaitForVisionDataAsync(string cameraName, int timeoutMilliseconds);
        void PublishVisionData(string cameraName, string data);
        void RegisterStation(string stationId, string cameraName, Action<string> callback);
        void UnregisterStation(string stationId, string cameraName);
    }

    /// <summary>
    /// 专门负责相机控制的接口，例如发送命令、拍照等操作。
    /// </summary>
    public interface ICameraController
    {
        Task<bool> SendCommandAsync(string cameraName, string command, int timeout = 5000);
        Task<string> SendCommandWithResponseAsync(string cameraName, string command, int timeout = 5000);
        Task<bool> TakePhotoAsync(string cameraName, string command, int timeout = 5000);
        Task<bool> TakeGroupPhotoAsync(int groupNumber, int positionIndex, int timeout = 10000);
    }

    public class VisionDataService : IVisionDataService
    {
        private readonly ILoggerService _logger;
        private readonly ICameraEventProcessor _cameraEventProcessor;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingRequests = new();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Action<string>>> _stationCallbacks = new();

        public event EventHandler<VisionDataEventArgs> VisionDataReceived;

        public VisionDataService(ILoggerService logger, ICameraEventProcessor cameraEventProcessor)
        {
            _logger = logger;
            _cameraEventProcessor = cameraEventProcessor;
        }

        public async Task<string> WaitForVisionDataAsync(string cameraName, int timeoutMilliseconds)
        {
            var tcs = new TaskCompletionSource<string>();

            if (!_pendingRequests.TryAdd(cameraName, tcs))
            {
                throw new InvalidOperationException($"已经在等待来自 {cameraName} 的视觉数据");
            }

            try
            {
                return await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMilliseconds));
            }
            catch (TimeoutException)
            {
                 _logger.Warn($"等待视觉数据超时: {cameraName}, 超时时间: {timeoutMilliseconds}ms");
                throw;
            }
            finally
            {
                _pendingRequests.TryRemove(cameraName, out _);
            }
        }

        public void PublishVisionData(string cameraName, string data)
        {
            _logger.Info($"发布视觉数据: {cameraName}, 成功: data:{data}");

            // 触发全局事件
            VisionDataReceived?.Invoke(this, new VisionDataEventArgs
            {
                CameraName = cameraName,
                Data = data
            });

            // 完成等待的请求
            if (_pendingRequests.TryGetValue(cameraName, out var tcs))
            {
                tcs.TrySetResult(data);
            }

            // 调用注册的回调 
            if (_stationCallbacks.TryGetValue(cameraName, out var callbacks))
            {
                foreach (var callback in callbacks.Values)
                {
                    try
                    {
                        callback?.Invoke(data);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"调用工站回调失败: {ex.Message}");
                    }
                }
            }
        }

        public void RegisterStation(string stationId, string cameraName, Action<string> callback)
        {
            var cameraCallbacks = _stationCallbacks.GetOrAdd(cameraName, _ => new ConcurrentDictionary<string, Action<string>>());
            cameraCallbacks[stationId] = callback;
            _logger.Info($"工站注册视觉数据回调: {stationId} -> {cameraName}");
        }

        public void UnregisterStation(string stationId, string cameraName)
        {
            if (_stationCallbacks.TryGetValue(cameraName, out var callbacks))
            {
                callbacks.TryRemove(stationId, out _);
                _logger.Info($"工站注销视觉数据回调: {stationId} -> {cameraName}");
            }
        }

    }

    public class VisionDataEventArgs : EventArgs
    {
        public string CameraName { get; set; }
        public string Data { get; set; }
    }

    public class VisionData
    {
        public bool Success { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double OffsetZ { get; set; }
        public double Angle { get; set; } // 可选：角度补偿
        public double Confidence { get; set; } // 可选：置信度
        public string RawData { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public override string ToString()
        {
            return Success
                ? $"X:{OffsetX:F3}, Y:{OffsetY:F3}, Z:{OffsetZ:F3}, Angle:{Angle:F2}°, Confidence:{Confidence:P0}"
                : $"Error: {ErrorMessage}";
        }
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Abstraction;
using MotionControl.Exceptions;
using MotionControl.Interfaces;
using TCPIPModule.Interfaces;
using Core.Utilities;

namespace StationTasks.Services
{
    public class VisionCaptureResult
    {
        public string RawResponse { get; set; }
        public Dictionary<string, double> ParsedData { get; set; } = new Dictionary<string, double>();
        public List<(double X, double Y)> MachinePoints { get; set; } = new List<(double X, double Y)>();
    }

    public class VisionCaptureService
    {
        private readonly IMotionService _motionService;
        private readonly IPositionProvider _positionProvider;
        private readonly ITCPEventService _tcpEventService;
        private readonly IVisionDataParser _visionDataParser;
        private readonly ILoggerService _logger;
        /// <summary> 本地化服务，用于日志多语言支持 </summary>
        private readonly ILocalizationService _localization;

        public VisionCaptureService(IMotionService motionService, IPositionProvider positionProvider,
            ITCPEventService tcpEventService, IVisionDataParser visionDataParser, ILoggerService logger,
            ILocalizationService localization)
        {
            _motionService = motionService;
            _positionProvider = positionProvider;
            _tcpEventService = tcpEventService;
            _visionDataParser = visionDataParser;
            _logger = logger;
            _localization = localization;
        }

        public async Task<VisionCaptureResult> ExecuteCaptureAsync(
            string connectionName, string triggerCommand, int timeout,
            CancellationToken token)
        {
            _logger.Info(string.Format(_localization.GetResourceOrDefault("VisCapSvc_Log_SendTriggerCommand", "[VisionCapture] 发送触发命令: {0} → {1}"), triggerCommand, connectionName));
            string rawData;
            try
            {
                rawData = await _tcpEventService.SendCommandWithResponseAsync(connectionName, triggerCommand, timeout);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new RecoverableException(
                    $"视觉系统响应超时（{timeout}ms）: {ex.Message}",
                    "请检查视觉系统连接是否正常，或增加超时时间。可选择重试、暂停或停止。");
            }

            if (string.IsNullOrEmpty(rawData))
            {
                throw new RecoverableException(
                    $"视觉系统返回空数据（超时{timeout}ms）",
                    "请检查视觉系统是否正常工作。可选择重试、暂停或停止。");
            }

            var parsedData = _visionDataParser.Parse(rawData);
            _logger.Info(string.Format(_localization.GetResourceOrDefault("VisCapSvc_Log_ParseCompleted", "[VisionCapture] 数据解析完成，共 {0} 个键值对, 原始数据: {1}"), parsedData.Count, rawData));

            return new VisionCaptureResult
            {
                RawResponse = rawData,
                ParsedData = parsedData,
                MachinePoints = new List<(double X, double Y)>()
            };
        }
    }
}

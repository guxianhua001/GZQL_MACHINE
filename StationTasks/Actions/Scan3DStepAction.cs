using Core.Models;
using Core.Utilities;
using MotionControl.Exceptions;
using MotionControl.Interfaces;
using Prism.Events;
using Recipe.Events;
using Recipe.Interfaces;
using StationTasks.Models;
using StationTasks.Services;
using StationTasks.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCPIPModule.Interfaces;

namespace StationTasks.Actions
{
    /// <summary>
    /// SCAN步骤动作：3D相机扫描工作流
    /// 按9步顺序编排：Z抬升→Y起始→X起始→Z下降→IO触发(异步复位)→X终点+TCP实时接收→Z安全→X待机→Y待机
    /// X轴移动期间通过TCP/IP实时接收3D相机回传数据并解析
    /// </summary>
    public class Scan3DStepAction : IProcessStepAction
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly ILoggerService _logger;
        private readonly ITCPEventService _tcpEventService;
        private readonly IMotionService _motionService;
        private readonly IVisionDataParser _defaultParser;
        private readonly ScriptVisionDataParser _scriptParser;
        private readonly IEventAggregator _eventAggregator;

        public StepType SupportedStepType => StepType.SCAN;

        public Scan3DStepAction(
            IRecipePoolService recipePoolService,
            ILoggerService logger,
            ITCPEventService tcpEventService,
            IMotionService motionService,
            IVisionDataParser defaultParser,
            ScriptVisionDataParser scriptParser,
            IEventAggregator eventAggregator)
        {
            _recipePoolService = recipePoolService;
            _logger = logger;
            _tcpEventService = tcpEventService;
            _motionService = motionService;
            _defaultParser = defaultParser;
            _scriptParser = scriptParser;
            _eventAggregator = eventAggregator;
        }

        /// <summary>
        /// 执行3D相机扫描步骤：读取ScanDetail配置，按9步工作流顺序执行
        /// </summary>
        public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            var detail = step.ScanDetail;
            if (detail == null)
            {
                _logger.Warn($"SCAN 步骤 [{step.Seq}] 没有 ScanDetail 配置，跳过执行");
                return;
            }

            string moveLabel = $"[{step.Seq}] SCAN 3D相机扫描";
            TaskState? overrideState = task.State;
            task.PublishStepStatus(moveLabel, overrideState);

            try
            {
                double speed = detail.MoveSpeed > 0 ? detail.MoveSpeed : 10.0;

                // 步骤1：Z轴抬升至初始位置
                _logger.Info($"SCAN [{step.Seq}] 步骤1: Z轴抬升至 {detail.ZInitPosition}");
                await task.ExecuteMoveAsync(detail.ZAxisId, detail.ZInitPosition, speed);

                // 步骤2：Y轴移动至起始点
                _logger.Info($"SCAN [{step.Seq}] 步骤2: Y轴移动至 {detail.YStartPosition}");
                await task.ExecuteMoveAsync(detail.YAxisId, detail.YStartPosition, speed);

                // 步骤3：X轴移动至起始点
                _logger.Info($"SCAN [{step.Seq}] 步骤3: X轴移动至 {detail.XStartPosition}");
                await task.ExecuteMoveAsync(detail.XAxisId, detail.XStartPosition, speed);

                // 步骤4：Z轴下降至拍照高度
                _logger.Info($"SCAN [{step.Seq}] 步骤4: Z轴下降至 {detail.ZPhotoPosition}");
                await task.ExecuteMoveAsync(detail.ZAxisId, detail.ZPhotoPosition, speed);

                // 步骤5：触发IO拍照信号（异步自动复位，不阻塞后续流程）
                _logger.Info($"SCAN [{step.Seq}] 步骤5: 触发IO[{detail.TriggerIoPort}]拍照信号，复位延时{detail.IoResetDelayMs}ms");
                TriggerIoWithAutoReset(detail.TriggerIoPort, detail.IoResetDelayMs);

                // 步骤6：X轴移动至终点，同时通过TCP实时接收3D相机数据
                _logger.Info($"SCAN [{step.Seq}] 步骤6: X轴移动至 {detail.XEndPosition}，同时接收TCP数据");
                string rawData = await MoveAndReceiveDataAsync(task, detail, speed, token);

                // 步骤7：Z轴抬升至安全高度
                _logger.Info($"SCAN [{step.Seq}] 步骤7: Z轴抬升至 {detail.ZSafePosition}");
                await task.ExecuteMoveAsync(detail.ZAxisId, detail.ZSafePosition, speed);

                // 步骤8：X轴返回待机位置
                _logger.Info($"SCAN [{step.Seq}] 步骤8: X轴返回 {detail.XStandbyPosition}");
                await task.ExecuteMoveAsync(detail.XAxisId, detail.XStandbyPosition, speed);

                // 步骤9：Y轴返回待机位置
                _logger.Info($"SCAN [{step.Seq}] 步骤9: Y轴返回 {detail.YStandbyPosition}");
                await task.ExecuteMoveAsync(detail.YAxisId, detail.YStandbyPosition, speed);

                // 解析数据并映射全局变量
                if (!string.IsNullOrEmpty(rawData))
                {
                    var parsedData = ParseRawData(rawData, detail);
                    _logger.Info($"SCAN [{step.Seq}] 解析结果: {string.Join(", ", parsedData.Select(kv => $"{kv.Key}={kv.Value:F3}"))}");
                    await MapToGlobalVariablesAsync(parsedData, detail.VariableMappings);
                }
                else
                {
                    _logger.Warn($"SCAN [{step.Seq}] 未收到3D相机数据");
                }
            }
            finally
            {
                task.CompleteStepStatus(overrideState);
            }
        }

        /// <summary>
        /// 触发IO信号并异步自动复位：写入高电平后，延时指定毫秒后自动写入低电平
        /// 复位操作为fire-and-forget，不阻塞工作流后续步骤
        /// </summary>
        private void TriggerIoWithAutoReset(int ioPort, int resetDelayMs)
        {
            _motionService.WriteDo(ioPort, true);
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(resetDelayMs);
                    _motionService.WriteDo(ioPort, false);
                    _logger.Info($"IO[{ioPort}] 已自动复位（延时{resetDelayMs}ms）");
                }
                catch (Exception ex)
                {
                    _logger.Error($"IO[{ioPort}] 自动复位失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// X轴移动至终点的同时，通过TCP实时接收3D相机回传数据
        /// 订阅CameraMessageReceived事件，在移动过程中收集数据，移动完成后等待剩余数据
        /// </summary>
        private async Task<string> MoveAndReceiveDataAsync(
            StationTaskBase task, ScanDetail detail, double speed, CancellationToken token)
        {
            if (detail.CommunicationType != "TCPIP" || string.IsNullOrEmpty(detail.ConnectionName))
            {
                _logger.Warn("SCAN 通讯方式未配置或非TCPIP模式，仅执行移动");
                await task.ExecuteMoveAsync(detail.XAxisId, detail.XEndPosition, speed);
                return string.Empty;
            }

            var receivedData = new List<string>();
            var dataReceivedTcs = new TaskCompletionSource<bool>();
            bool dataReceived = false;

            Action<string, string> handler = (cameraName, message) =>
            {
                if (cameraName == detail.ConnectionName)
                {
                    try
                    {
                        receivedData.Add(message);
                        dataReceived = true;
                        _logger.Info($"SCAN 实时收到TCP数据: {message}");
                        dataReceivedTcs.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"SCAN TCP数据处理失败: {ex.Message}");
                    }
                }
            };

            _tcpEventService.CameraMessageReceived += handler;

            try
            {
                await task.ExecuteMoveAsync(detail.XAxisId, detail.XEndPosition, speed);

                if (!dataReceived)
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    cts.CancelAfter(detail.ResponseTimeout);
                    try
                    {
                        await Task.WhenAny(dataReceivedTcs.Task, Task.Delay(Timeout.Infinite, cts.Token));
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    {
                        _logger.Warn($"SCAN 等待TCP数据超时（{detail.ResponseTimeout}ms）");
                    }
                }

                if (!dataReceived)
                {
                    throw new RecoverableException(
                        message: $"SCAN 步骤未收到3D相机数据（超时{detail.ResponseTimeout}ms）",
                        suggestedAction: "请检查3D相机TCP连接是否正常、IO触发信号是否正确，复位后重试。"
                    );
                }

                return string.Join("", receivedData);
            }
            finally
            {
                _tcpEventService.CameraMessageReceived -= handler;
            }
        }

        /// <summary>
        /// 解析原始数据：有自定义脚本时使用脚本解析器，否则使用Camera3DDataParser
        /// </summary>
        private Dictionary<string, double> ParseRawData(string rawData, ScanDetail detail)
        {
            if (string.IsNullOrEmpty(rawData))
                return new Dictionary<string, double>();

            try
            {
                if (!string.IsNullOrEmpty(detail.ParseScript))
                {
                    _scriptParser.Script = detail.ParseScript;
                    return _scriptParser.Parse(rawData);
                }

                var parser = new Camera3DDataParser(_logger, detail.TabCount);
                return parser.Parse(rawData);
            }
            catch (RecoverableException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"SCAN 数据解析失败: {ex.Message}");
                throw new RecoverableException(
                    message: $"SCAN 数据解析失败: {ex.Message}",
                    suggestedAction: "请检查数据解析脚本或3D相机数据格式是否正确。"
                );
            }
        }

        /// <summary>
        /// 将解析结果映射到全局变量并持久化
        /// 同时写入原始实测值和补偿后值（实测值+固定补偿）
        /// </summary>
        private async Task MapToGlobalVariablesAsync(
            Dictionary<string, double> parsedData, ObservableCollection<VariableMapping> mappings)
        {
            if (mappings == null || mappings.Count == 0)
            {
                _logger.Info("SCAN 未配置变量映射，跳过全局变量写入");
                return;
            }

            var poolId = _recipePoolService.CurrentPoolId;
            if (string.IsNullOrEmpty(poolId)) return;

            var globalVars = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
            bool changed = false;

            foreach (var mapping in mappings)
            {
                if (string.IsNullOrEmpty(mapping.SourceKey))
                    continue;

                if (!parsedData.TryGetValue(mapping.SourceKey, out double value))
                {
                    _logger.Warn($"SCAN 映射跳过: 解析结果中不存在键 '{mapping.SourceKey}'");
                    continue;
                }

                // 1. 原始实测值 → 全局变量
                if (!string.IsNullOrEmpty(mapping.GlobalVariableName))
                {
                    var targetVar = globalVars.FirstOrDefault(v => v.Name == mapping.GlobalVariableName);
                    if (targetVar != null)
                    {
                        targetVar.Value = value.ToString("F6");
                        _logger.Info($"SCAN 映射: {mapping.SourceKey}={value:F3} → '{mapping.GlobalVariableName}'(原始值)");
                        changed = true;
                    }
                    else
                    {
                        _logger.Warn($"SCAN 映射跳过: 全局变量 '{mapping.GlobalVariableName}' 不存在");
                    }
                }

                // 2. 偏差值（实测值 - 基准Z值）→ 全局变量
                if (!string.IsNullOrEmpty(mapping.CompensatedGlobalVariableName))
                {
                    // 偏差 = 实测值 - 基准Z值
                    double deviation = value - mapping.BaseZValue;
                    var compTargetVar = globalVars.FirstOrDefault(v => v.Name == mapping.CompensatedGlobalVariableName);
                    if (compTargetVar != null)
                    {
                        compTargetVar.Value = deviation.ToString("F6");
                        _logger.Info($"SCAN 映射: {mapping.SourceKey}({value:F3}-{mapping.BaseZValue:F3})={deviation:F3} → '{mapping.CompensatedGlobalVariableName}'(偏差)");
                        changed = true;
                    }
                    else
                    {
                        _logger.Warn($"SCAN 映射跳过: 偏差全局变量 '{mapping.CompensatedGlobalVariableName}' 不存在");
                    }
                }
            }

            if (changed)
            {
                await _recipePoolService.SaveGlobalVariablesAsync(poolId, globalVars);
                _logger.Info("SCAN 全局变量已保存");

                // 通知全局变量窗口重新加载最新数据
                _eventAggregator.GetEvent<GlobalVariablesChangedEvent>().Publish(poolId);
            }
        }
    }
}

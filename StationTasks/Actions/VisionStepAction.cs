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
    /// VISION步骤动作：通过TCPIP发送触发命令→接收返回数据→解析数据→映射全局变量
    /// 支持超时处理，超时时抛出RecoverableException供上层处理
    /// </summary>
    public class VisionStepAction : IProcessStepAction
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly ILoggerService _logger;
        private readonly ITCPEventService _tcpEventService;
        private readonly IVisionDataParser _defaultParser;
        private readonly ScriptVisionDataParser _scriptParser;
        private readonly IEventAggregator _eventAggregator;

        public StepType SupportedStepType => StepType.VISION;

        public VisionStepAction(
            IRecipePoolService recipePoolService,
            ILoggerService logger,
            ITCPEventService tcpEventService,
            IVisionDataParser defaultParser,
            ScriptVisionDataParser scriptParser,
            IEventAggregator eventAggregator)
        {
            _recipePoolService = recipePoolService;
            _logger = logger;
            _tcpEventService = tcpEventService;
            _defaultParser = defaultParser;
            _scriptParser = scriptParser;
            _eventAggregator = eventAggregator;
        }

        public async Task ExecuteAsync(ProcessStep step, StationTaskBase task, CancellationToken token)
        {
            var detail = step.VisionDetail;
            if (detail == null)
            {
                _logger.Warn($"VISION 步骤 [{step.Seq}] 没有 VisionDetail 配置，跳过执行");
                return;
            }

            string moveLabel = $"[{step.Seq}] VISION → {detail.ConnectionName}";
            TaskState? overrideState = task.State;
            task.PublishStepStatus(moveLabel, overrideState);

            try
            {
                // 阶段1：发送触发命令并等待响应
                string rawData = await SendTriggerCommandAsync(detail, step, token);

                // 阶段2：解析返回数据
                var parsedData = ParseRawData(rawData, detail.ParseScript);
                _logger.Info($"VISION 步骤 [{step.Seq}] 解析结果: {string.Join(", ", parsedData.Select(kv => $"{kv.Key}={kv.Value:F3}"))}");

                // 阶段3：映射全局变量
                await MapToGlobalVariablesAsync(parsedData, detail.VariableMappings);
            }
            finally
            {
                task.CompleteStepStatus(overrideState);
            }
        }

        /// <summary>
        /// 通过TCPIP发送触发命令并等待响应
        /// 超时后自动重试：重新发送拍照命令再等待返回信号
        /// </summary>
        private async Task<string> SendTriggerCommandAsync(VisionDetail detail, ProcessStep step, CancellationToken token)
        {
            if (string.IsNullOrEmpty(detail.TriggerCommand))
            {
                _logger.Warn($"VISION 步骤 [{step.Seq}] 未配置触发命令");
                return string.Empty;
            }

            if (detail.CommunicationType != "TCPIP" || string.IsNullOrEmpty(detail.ConnectionName))
            {
                _logger.Warn($"VISION 步骤 [{step.Seq}] 通讯方式未配置或非TCPIP模式");
                return string.Empty;
            }

            const int maxRetries = 3;
            int attempt = 0;

            while (attempt < maxRetries)
            {
                attempt++;
                token.ThrowIfCancellationRequested();

                _logger.Info($"VISION 步骤 [{step.Seq}] 发送触发命令（第{attempt}次）: '{detail.TriggerCommand}' → {detail.ConnectionName}, 超时={detail.ResponseTimeout}ms");

                try
                {
                    string response = await _tcpEventService.SendCommandWithResponseAsync(
                        detail.ConnectionName,
                        detail.TriggerCommand,
                        detail.ResponseTimeout);

                    _logger.Info($"VISION 步骤 [{step.Seq}] 收到响应: {response}");
                    return response ?? string.Empty;
                }
                catch (TimeoutException ex)
                {
                    _logger.Warn($"VISION 步骤 [{step.Seq}] 第{attempt}次等待响应超时（{detail.ResponseTimeout}ms）: {ex.Message}");

                    if (attempt < maxRetries)
                    {
                        _logger.Info($"VISION 步骤 [{step.Seq}] 将重新发送拍照命令并等待返回信号...");
                        continue;
                    }

                    // 重试次数用尽，抛出可恢复异常
                    _logger.Error($"VISION 步骤 [{step.Seq}] 已重试{maxRetries}次，均超时");
                    throw new RecoverableException(
                        message: $"VISION 步骤 [{step.Seq}] 等待响应超时，已重试{maxRetries}次（每次{detail.ResponseTimeout}ms）",
                        suggestedAction: "请检查视觉系统连接是否正常，或增加超时时间。可选择重试、暂停或停止。"
                    );
                }
                catch (Exception ex)
                {
                    _logger.Error($"VISION 步骤 [{step.Seq}] 发送命令失败: {ex.Message}");
                    throw new RecoverableException(
                        message: $"VISION 步骤 [{step.Seq}] 发送命令失败: {ex.Message}",
                        suggestedAction: "请检查TCPIP连接配置是否正确，视觉系统是否在线。"
                    );
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 解析原始数据：有自定义脚本时使用脚本解析器，否则使用默认解析器
        /// </summary>
        private Dictionary<string, double> ParseRawData(string rawData, string parseScript)
        {
            if (string.IsNullOrEmpty(rawData))
            {
                _logger.Warn("VISION 收到空数据，无法解析");
                return new Dictionary<string, double>();
            }

            try
            {
                if (string.IsNullOrEmpty(parseScript))
                    return _defaultParser.Parse(rawData);

                _scriptParser.Script = parseScript;
                return _scriptParser.Parse(rawData);
            }
            catch (Exception ex)
            {
                _logger.Error($"VISION 数据解析失败: {ex.Message}");
                throw new RecoverableException(
                    message: $"VISION 数据解析失败: {ex.Message}",
                    suggestedAction: "请检查数据解析脚本是否正确，或使用默认解析器。"
                );
            }
        }

        /// <summary>
        /// 将解析结果映射到全局变量并持久化
        /// </summary>
        private async Task MapToGlobalVariablesAsync(Dictionary<string, double> parsedData, ObservableCollection<VariableMapping> mappings)
        {
            if (mappings == null || mappings.Count == 0)
            {
                _logger.Info("VISION 未配置变量映射，跳过全局变量写入");
                return;
            }

            var poolId = _recipePoolService.CurrentPoolName;
            if (string.IsNullOrEmpty(poolId)) return;

            var globalVars = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
            bool changed = false;

            foreach (var mapping in mappings)
            {
                if (string.IsNullOrEmpty(mapping.SourceKey) || string.IsNullOrEmpty(mapping.GlobalVariableName))
                    continue;

                if (!parsedData.TryGetValue(mapping.SourceKey, out double value))
                {
                    _logger.Warn($"VISION 映射跳过: 解析结果中不存在键 '{mapping.SourceKey}'");
                    continue;
                }

                var targetVar = globalVars.FirstOrDefault(v => v.Name == mapping.GlobalVariableName);
                if (targetVar != null)
                {
                    targetVar.Value = value.ToString("F6");
                    _logger.Info($"VISION 映射: {mapping.SourceKey}={value:F3} → 全局变量 '{mapping.GlobalVariableName}'");
                    changed = true;
                }
                else
                {
                    _logger.Warn($"VISION 映射跳过: 全局变量 '{mapping.GlobalVariableName}' 不存在");
                }
            }

            if (changed)
            {
                await _recipePoolService.SaveGlobalVariablesAsync(poolId, globalVars);
                _logger.Info("VISION 全局变量已保存");

                // 通知所有订阅者全局变量已更新，刷新 UI 显示值
                _eventAggregator.GetEvent<GlobalVariablesChangedEvent>().Publish(poolId);
            }
        }
    }
}

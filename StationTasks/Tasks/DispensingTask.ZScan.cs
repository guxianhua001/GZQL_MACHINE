using Core.Abstraction;
using Core.Utilities;
using MotionControl.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StationTasks.Tasks
{
    /// <summary>
    /// DispensingTask partial class — Z-Scan 3D扫描运动序列与返回待机位操作。
    /// 复用 StationTaskBase 的 RunStep 安全保护（暂停/急停/单步/可恢复异常）和 ExecuteManualProcess 机制。
    /// </summary>
    public partial class DispensingTask : IDispensingZScanOperations
    {
        /// <summary>
        /// 执行3D扫描运动序列（纯运动部分，不含数据接收）。
        /// 通过 ExecuteManualProcess 包装，享受 RunStep 暂停/急停/单步安全保护。
        /// </summary>
        /// <param name="dxScanSpeed">Dx轴扫描速度(mm/s)，范围10-60</param>
        public async Task ExecuteZScan3DSequenceAsync(
            string safePosName, string scanStartPosName, string scanEndPosName,
            string standbyPosName, string triggerIOName, double dxScanSpeed,
            Action<string> progressCallback, CancellationToken token)
        {
            await ExecuteManualProcess("Z-Scan 3D", async () =>
            {
                // 位置编辑器保存后强制刷新缓存，避免 Preload 阶段缓存的旧值
                await RefreshPositionsCacheAsync();
                var positions = await LoadPositionsAsync();

                // 解析位置值
                double safeDz1 = GetPositionValueFromMap(positions, safePosName, "Dz₁");
                double safeDz2 = GetPositionValueFromMap(positions, safePosName, "Dz₂");
                double safeDz3 = GetPositionValueFromMap(positions, safePosName, "Dz₃");
                double scanDz = GetPositionValueFromMap(positions, scanStartPosName, "Dz₁");
                double startDx = GetPositionValueFromMap(positions, scanStartPosName, "Dx");
                double startDy = GetPositionValueFromMap(positions, scanStartPosName, "Dy");
                double endDx = GetPositionValueFromMap(positions, scanEndPosName, "Dx");
                double standbyDx = GetPositionValueFromMap(positions, standbyPosName, "Dx");
                double standbyDy = GetPositionValueFromMap(positions, standbyPosName, "Dy");

                int coordId = ResolveCoordId();

                // 获取各轴配置速度（含全局速度比例）
                double dz1Speed = GetAxisConfiguredSpeed(AxisDz1);
                double dz2Speed = GetAxisConfiguredSpeed(AxisDz2);
                double dz3Speed = GetAxisConfiguredSpeed(AxisDz3);
                // Dx+Dy 插补运动使用插补系速度
                double interpSpeed = GetInterpolationSpeed(coordId);

                TaskLogger.Info($"[{TaskName}] ZScan 位置解析: SafeZ(Dz1={safeDz1:F3}, Dz2={safeDz2:F3}, Dz3={safeDz3:F3}), " +
                    $"Start(Dx={startDx:F3}, Dy={startDy:F3}), End(Dx={endDx:F3}), Standby(Dx={standbyDx:F3}, Dy={standbyDy:F3})");
                TaskLogger.Info($"[{TaskName}] ZScan 速度参数: Dx扫描={dxScanSpeed:F1}mm/s, Dz1={dz1Speed:F1}, Dz2={dz2Speed:F1}, Dz3={dz3Speed:F1}, 插补={interpSpeed:F1}");

                // 步骤1：Dz₁/Dz₂/Dz3 抬起到安全高度（多轴同步，统一轮询）
                progressCallback?.Invoke("Raising Z axes...");
                await _motion.MoveAbsMultiAxisAsync(new (int, double, double)[]
                {
                    (AxisDz1, safeDz1, dz1Speed),
                    (AxisDz2, safeDz2, dz2Speed),
                    (AxisDz3, safeDz3, dz3Speed)
                }, CurrentToken);
                await WaitTime(100);

                // 步骤2：Dx+Dy 插补运动到扫描起始位
                progressCallback?.Invoke("Moving to scan start...");
                // Dy轴使用插补系速度
                await _motion.MoveLineAbsAsync(coordId,
                    new[] { AxisDx, AxisDy },
                    new[] { startDx, startDy }, interpSpeed, CurrentToken);
                await WaitTime(100);

                // 步骤2：AxisDz1 单轴运动到扫描高度
                progressCallback?.Invoke("Moving to scan height...");
                await _motion.MoveAbsAsync(AxisDz1, scanDz, dz1Speed, CurrentToken);
                await WaitTime(100);

                // 步骤4：触发3D相机拍照（IO触发）
                progressCallback?.Invoke("Triggering camera...");
                int triggerLogicalId = GetDoLogicalId(triggerIOName);
                if (triggerLogicalId >= 0)
                {
                    WriteDO(triggerLogicalId, true);
                    TaskLogger.Info($"[{TaskName}] IO 触发信号已置位: {triggerIOName} (LogicalId={triggerLogicalId})");
                }
                else
                {
                    TaskLogger.Warn($"[{TaskName}] 未找到 IO 端口 '{triggerIOName}'，跳过相机触发");
                }

                // 步骤5：Dx 运动到扫描结束位（使用 dxScanSpeed）
                progressCallback?.Invoke("Scanning...");
                await _motion.MoveAbsAsync(AxisDx, endDx, dxScanSpeed, CurrentToken);

                // 步骤6：复位触发信号
                if (triggerLogicalId >= 0)
                {
                    WriteDO(triggerLogicalId, false);
                    TaskLogger.Info($"[{TaskName}] IO 触发信号已复位: {triggerIOName}");
                }

                // 步骤7：Dz₁/Dz₂/Dz3 再次抬起到安全高度（多轴同步，统一轮询）
                progressCallback?.Invoke("Raising Z axes...");
                await _motion.MoveAbsMultiAxisAsync(new (int, double, double)[]
                {
                    (AxisDz1, safeDz1, dz1Speed),
                    (AxisDz2, safeDz2, dz2Speed),
                    (AxisDz3, safeDz3, dz3Speed)
                }, CurrentToken);

                // 步骤8：Dx+Dy 插补运动到待机位
                progressCallback?.Invoke("Returning to standby...");
                await _motion.MoveLineAbsAsync(coordId,
                    new[] { AxisDx, AxisDy },
                    new[] { standbyDx, standbyDy }, interpSpeed, CurrentToken);

                progressCallback?.Invoke("Motion sequence completed");
                TaskLogger.Info($"[{TaskName}] Z-Scan 3D扫描运动序列完成");
            });
        }

        /// <summary>
        /// 返回待机位：Dz₁/Dz₂/Dz3 抬起到安全高度 → Dx+Dy 插补到待机位。
        /// 通过 ExecuteManualProcess 包装，享受 RunStep 安全保护。
        /// 各轴运动速度从轴参数配置获取，并使用全局速度比例。
        /// </summary>
        public async Task ReturnToStandbyAsync(
            string safePosName, string standbyPosName,
            Action<string> progressCallback, CancellationToken token)
        {
            await ExecuteManualProcess("Return to Standby", async () =>
            {
                await RefreshPositionsCacheAsync();
                var positions = await LoadPositionsAsync();

                // 解析位置值
                double safeDz1 = GetPositionValueFromMap(positions, safePosName, "Dz₁");
                double safeDz2 = GetPositionValueFromMap(positions, safePosName, "Dz₂");
                double safeDz3 = GetPositionValueFromMap(positions, safePosName, "Dz3");
                double standbyDx = GetPositionValueFromMap(positions, standbyPosName, "Dx");
                double standbyDy = GetPositionValueFromMap(positions, standbyPosName, "Dy");

                int coordId = ResolveCoordId();

                // 各轴使用配置速度（含全局速度比例）
                double dz1Speed = GetAxisConfiguredSpeed(AxisDz1);
                double dz2Speed = GetAxisConfiguredSpeed(AxisDz2);
                double dz3Speed = GetAxisConfiguredSpeed(AxisDz3);
                // Dx+Dy 插补运动使用插补系速度
                double interpSpeed = GetInterpolationSpeed(coordId);

                TaskLogger.Info($"[{TaskName}] ReturnToStandby 速度参数: Dz1={dz1Speed:F1}, Dz2={dz2Speed:F1}, Dz3={dz3Speed:F1}, 插补={interpSpeed:F1}");

                // 步骤1：Dz₁/Dz₂/Dz3 抬起到安全高度（多轴同步，统一轮询）
                progressCallback?.Invoke("Raising Z axes...");
                await _motion.MoveAbsMultiAxisAsync(new (int, double, double)[]
                {
                    (AxisDz1, safeDz1, dz1Speed),
                    (AxisDz2, safeDz2, dz2Speed),
                    (AxisDz3, safeDz3, dz3Speed)
                }, CurrentToken);
                await WaitTime(100);

                // 步骤2：Dx+Dy 插补运动到待机位
                progressCallback?.Invoke("Moving to standby...");
                await _motion.MoveLineAbsAsync(coordId,
                    new[] { AxisDx, AxisDy },
                    new[] { standbyDx, standbyDy }, interpSpeed, CurrentToken);

                progressCallback?.Invoke("Standby");
                TaskLogger.Info($"[{TaskName}] 已返回待机位置");
            });
        }

        #region ZScan 辅助方法

        /// <summary>
        /// 获取轴配置速度（含全局速度比例）。
        /// 从轴参数配置获取 MaxSpeed，乘以全局速度比例 SpeedPercent。
        /// 逻辑：1) 通过 LogicalId 查找 AxisConfig → 获取 CardId/AxisId
        ///       2) 通过 IAxisParameterService.GetAxisSpeed 获取 MaxSpeed
        ///       3) MaxSpeed × (SpeedPercent / 100) = 实际运动速度
        /// </summary>
        /// <param name="logicalAxisId">逻辑轴编号（即 hwcfg.xml 中的 LogicalId）</param>
        /// <returns>含全局速度比例的实际运动速度(mm/s)</returns>
        private double GetAxisConfiguredSpeed(int logicalAxisId)
        {
            var cfg = _motion.GetAxisConfigurations().FirstOrDefault(a => a.LogicalId == logicalAxisId);
            double baseSpeed;
            if (cfg != null)
                baseSpeed = _axisParameterService.GetAxisSpeed(cfg.CardId, cfg.AxisId);
            else
                baseSpeed = 10.0; // 默认速度

            return baseSpeed * (_speedOverrideLocal.SpeedPercent / 100.0);
        }

        /// <summary>
        /// 获取插补系运动速度（含全局速度比例）。
        /// 从 IAxisParameterService.GetInterpolationSpeeds 获取插补速度，乘以全局速度比例。
        /// </summary>
        /// <param name="coordId">插补系编号</param>
        /// <returns>含全局速度比例的实际插补运动速度(mm/s)</returns>
        private double GetInterpolationSpeed(int coordId)
        {
            // 获取 Dx 轴所在控制卡的 CardId
            var dxCfg = _motion.GetAxisConfigurations().FirstOrDefault(a => a.LogicalId == AxisDx);
            int cardId = dxCfg?.CardId ?? 0;

            double baseSpeed = _axisParameterService.GetInterpolationSpeeds(cardId, coordId);
            return baseSpeed * (_speedOverrideLocal.SpeedPercent / 100.0);
        }

        /// <summary>
        /// 从 hwcfg.xml 的 InterpolationSystems 查找含有 Dx 轴的插补系 CoordId。
        /// 用于 Dx+Dy 插补运动。
        /// </summary>
        private int ResolveCoordId()
        {
            var axisConfigs = _motion.GetAxisConfigurations();
            var dxConfig = axisConfigs.FirstOrDefault(a => a.Name == "Dx");
            if (dxConfig == null)
            {
                TaskLogger.Warn($"[{TaskName}] 未找到Dx轴配置，CoordId 回退到 0");
                return 0;
            }

            var systems = _axisParameterService.LoadInterpolationSystems().ToList();
            foreach (var sys in systems)
            {
                foreach (var axisEntry in sys.Axes)
                {
                    var parts = axisEntry.Split('-');
                    if (parts.Length == 2 && int.TryParse(parts[1], out int actAxisId))
                    {
                        if (actAxisId == dxConfig.AxisId)
                        {
                            TaskLogger.Info($"[{TaskName}] Dx(actAxisId={dxConfig.AxisId}) 匹配插补系 CoordId={sys.CoordId}");
                            return sys.CoordId;
                        }
                    }
                }
            }

            TaskLogger.Warn($"[{TaskName}] Dx 不在任何插补系中，CoordId 回退到 0");
            return 0;
        }

        /// <summary>
        /// 从位置字典中获取指定位置名的指定轴的值。
        /// 位置字典 key 格式: {PositionName}.{AxisName}
        /// </summary>
        private double GetPositionValueFromMap(Dictionary<string, double> positions, string positionName, string axisName)
        {
            var key = $"{positionName}.{axisName}";
            if (positions.TryGetValue(key, out double value))
                return value;

            TaskLogger.Warn($"[{TaskName}] 位置查找失败: {key}");
            return 0;
        }

        #endregion
    }
}

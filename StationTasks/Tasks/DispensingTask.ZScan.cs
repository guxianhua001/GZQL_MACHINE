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
        public async Task ExecuteZScan3DSequenceAsync(
            string safePosName, string scanStartPosName, string scanEndPosName,
            string standbyPosName, string triggerIOName, double speed,
            Action<string> progressCallback, CancellationToken token)
        {
            await ExecuteManualProcess("Z-Scan 3D", async () =>
            {
                // 加载位置数据
                var positions = await LoadPositionsAsync();

                // 解析位置值
                double safeDz1 = GetPositionValueFromMap(positions, safePosName, "Dz₁");
                double safeDz2 = GetPositionValueFromMap(positions, safePosName, "Dz₂");
                double safeDz3 = GetPositionValueFromMap(positions, safePosName, "Dz3");
                double scanDz = GetPositionValueFromMap(positions, scanStartPosName, "Dz₁");
                double startDx = GetPositionValueFromMap(positions, scanStartPosName, "Dx");
                double startDy = GetPositionValueFromMap(positions, scanStartPosName, "Dy");
                double endDx = GetPositionValueFromMap(positions, scanEndPosName, "Dx");
                double standbyDx = GetPositionValueFromMap(positions, standbyPosName, "Dx");
                double standbyDy = GetPositionValueFromMap(positions, standbyPosName, "Dy");

                int coordId = ResolveCoordId();

                TaskLogger.Info($"[{TaskName}] ZScan 位置解析: SafeZ(Dz1={safeDz1:F3}, Dz2={safeDz2:F3}, Dz3={safeDz3:F3}), " +
                    $"Start(Dx={startDx:F3}, Dy={startDy:F3}), End(Dx={endDx:F3}), Standby(Dx={standbyDx:F3}, Dy={standbyDy:F3})");

                // 步骤1：Dz₁/Dz₂/Dz3 抬起到安全高度（并行）
                progressCallback?.Invoke("Raising Z axes...");
                await Task.WhenAll(
                    _motion.MoveAbsAsync(AxisDz1, safeDz1, speed, CurrentToken),
                    _motion.MoveAbsAsync(AxisDz2, safeDz2, speed, CurrentToken),
                    _motion.MoveAbsAsync(AxisDz3, safeDz3, speed, CurrentToken)
                );
                await WaitTime(100);

                // 步骤2：Dx+Dy 插补运动到扫描起始位
                progressCallback?.Invoke("Moving to scan start...");
                await _motion.MoveLineAbsAsync(coordId,
                    new[] { AxisDx, AxisDy },
                    new[] { startDx, startDy }, speed, CurrentToken);
                await WaitTime(100);

                // 步骤2：AxisDz1 单轴运动到扫描高度
                progressCallback?.Invoke("Moving to scan height...");
                await _motion.MoveAbsAsync(AxisDz1, scanDz, speed, CurrentToken);
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

                // 步骤5：Dx 运动到扫描结束位
                progressCallback?.Invoke("Scanning...");
                await _motion.MoveAbsAsync(AxisDx, endDx, speed, CurrentToken);

                // 步骤6：复位触发信号
                if (triggerLogicalId >= 0)
                {
                    WriteDO(triggerLogicalId, false);
                    TaskLogger.Info($"[{TaskName}] IO 触发信号已复位: {triggerIOName}");
                }

                // 步骤7：Dz₁/Dz₂/Dz3 再次抬起到安全高度（并行）
                progressCallback?.Invoke("Raising Z axes...");
                await Task.WhenAll(
                    _motion.MoveAbsAsync(AxisDz1, safeDz1, speed, CurrentToken),
                    _motion.MoveAbsAsync(AxisDz2, safeDz2, speed, CurrentToken),
                    _motion.MoveAbsAsync(AxisDz3, safeDz3, speed, CurrentToken)
                );

                // 步骤8：Dx+Dy 插补运动到待机位
                progressCallback?.Invoke("Returning to standby...");
                await _motion.MoveLineAbsAsync(coordId,
                    new[] { AxisDx, AxisDy },
                    new[] { standbyDx, standbyDy }, speed, CurrentToken);

                progressCallback?.Invoke("Motion sequence completed");
                TaskLogger.Info($"[{TaskName}] Z-Scan 3D扫描运动序列完成");
            });
        }

        /// <summary>
        /// 返回待机位：Dz₁/Dz₂/Dz3 抬起到安全高度 → Dx+Dy 插补到待机位。
        /// 通过 ExecuteManualProcess 包装，享受 RunStep 安全保护。
        /// </summary>
        public async Task ReturnToStandbyAsync(
            string safePosName, string standbyPosName, double speed,
            Action<string> progressCallback, CancellationToken token)
        {
            await ExecuteManualProcess("Return to Standby", async () =>
            {
                // 加载位置数据
                var positions = await LoadPositionsAsync();

                // 解析位置值
                double safeDz1 = GetPositionValueFromMap(positions, safePosName, "Dz₁");
                double safeDz2 = GetPositionValueFromMap(positions, safePosName, "Dz₂");
                double safeDz3 = GetPositionValueFromMap(positions, safePosName, "Dz3");
                double standbyDx = GetPositionValueFromMap(positions, standbyPosName, "Dx");
                double standbyDy = GetPositionValueFromMap(positions, standbyPosName, "Dy");

                int coordId = ResolveCoordId();

                // 步骤1：Dz₁/Dz₂/Dz3 抬起到安全高度（并行）
                progressCallback?.Invoke("Raising Z axes...");
                await Task.WhenAll(
                    _motion.MoveAbsAsync(AxisDz1, safeDz1, speed, CurrentToken),
                    _motion.MoveAbsAsync(AxisDz2, safeDz2, speed, CurrentToken),
                    _motion.MoveAbsAsync(AxisDz3, safeDz3, speed, CurrentToken)
                );
                await WaitTime(100);

                // 步骤2：Dx+Dy 插补运动到待机位
                progressCallback?.Invoke("Moving to standby...");
                await _motion.MoveLineAbsAsync(coordId,
                    new[] { AxisDx, AxisDy },
                    new[] { standbyDx, standbyDy }, speed, CurrentToken);

                progressCallback?.Invoke("Standby");
                TaskLogger.Info($"[{TaskName}] 已返回待机位置");
            });
        }

        #region ZScan 辅助方法

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

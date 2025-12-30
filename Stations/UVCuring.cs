using Core.Models;
using Stations.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Stations
{
    public partial class DispenserStation
    {
        /// <summary>
        /// UV固化流程
        /// </summary>
        /// <param name="uvIndex">UV灯序号，默认为1</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="progressCallback">进度回调</param>
        /// <returns>UV固化是否成功</returns>
        public async Task<bool> StartUVCuringAsync(
            int groupIndex = 1, int uvIndex = 1,
            CancellationToken cancellationToken = default,
            IProgress<(int progress, string status)> progressCallback = null)
        {
            try
            {
                _logger.Info($"【点胶工站】开始UV{uvIndex}固化流程");

                progressCallback?.Report((0, "检查UV系统状态..."));

                // 移动到UV固化位置
                progressCallback?.Report((20, $"移动到UV{uvIndex}固化位置..."));
                if (!await MoveToUVCuringPositionAsync(groupIndex, uvIndex, cancellationToken, progressCallback))
                {
                    _logger.Error($"移动到UV{uvIndex}固化位置失败");
                    return false;
                }
                progressCallback?.Report((40, $"已到达UV{uvIndex}固化位置"));

                // 步骤3: 获取固化参数
                progressCallback?.Report((45, "获取固化参数..."));
                var curingParams = GetUVCuringParameters(uvIndex);
                _logger.Info($"UV固化参数: 时间={curingParams.CuringTime}秒");

                // 步骤4: 开启UV灯并开始固化
                progressCallback?.Report((50, $"开启UV灯，开始固化 ({curingParams.CuringTime}秒)..."));
                _logger.Info($"步骤4: 开启UV{uvIndex}灯，固化时间{curingParams.CuringTime}秒");

                await StartUVLightAsync(uvIndex);

                // 步骤5: 等待固化完成（支持进度更新和取消）
                progressCallback?.Report((60, "正在固化中..."));
                if (!await WaitForUVCuringCompleteAsync(curingParams.CuringTime, uvIndex, cancellationToken, progressCallback))
                {
                    _logger.Error($"UV{uvIndex}固化过程中出现异常");
                    // 尝试关闭UV灯
                    await StopUVLight();
                    return false;
                }

                // 步骤6: 关闭UV灯
                progressCallback?.Report((95, "固化完成，关闭UV灯..."));
                _logger.Info($"步骤6: 关闭UV{uvIndex}灯");

                await StopUVLight();

                // 步骤7: 记录固化结果
                progressCallback?.Report((100, "UV固化完成"));
                _logger.Info($"UV{uvIndex}固化流程完成");

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"UV{uvIndex}固化流程被用户取消");
                progressCallback?.Report((0, "操作已取消"));
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"UV{uvIndex}固化流程异常");
                progressCallback?.Report((0, $"固化异常: {ex.Message}"));
                return false;
            }
        }
        /// <summary>
        /// 获取UV固化参数
        /// </summary>
        private UVCuringParameters GetUVCuringParameters(int uvIndex)
        {
            var recipeParams = _recipeService.Parameters;

            return new UVCuringParameters
            {
                CuringTime = recipeParams?.UVFixTime ?? 30.0, // 默认30秒
                //Intensity = recipeParams?.UVIntensity ?? 80.0,   // 默认80%
                //Wavelength = recipeParams?.UVWavelength ?? 365.0 // 默认365nm
            };
        }
        /// <summary>
        /// 移动到UV固化位置
        /// </summary>
        /// <param name="uvIndex">UV灯序号</param>
        /// <returns></returns>
        private async Task<bool> MoveToUVCuringPositionAsync(int groupIndex, int uvIndex, CancellationToken cancellationToken, IProgress<(int progress, string status)> progressCallback)
        {
            string positionName = $"UV照射{groupIndex}_{uvIndex}";
            var axes = new[] { DispX, DispY_1 };
            var velocities = new[] { _axisConfigService.GetAxisSpeed(0, DispX.ActId), _axisConfigService.GetAxisSpeed(0, DispY_1.ActId) };
            var positions = new[] { GetPosition(DispX.ActId, positionName), GetPosition(DispY_1.ActId, positionName) };

            if (!MoveMultiAxisToPosition(axes, positions, velocities))
            {
                throw new InvalidOperationException($"移动到{positionName}失败");
            }

            // 2. 移动Z轴到拍照高度
            double zHeight = GetPosition(DispZ1.ActId, positionName);
            _logger.Info($"移动DispZ3到高度{zHeight:F2}mm");

            double speedZ = 5.0;
            MoveAbs(DispZ1.ActId, zHeight, speedZ);

            if (!WaitMoveDone())
            {
                _logger.Error("移动Z3轴到拍照位置失败");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 等待UV固化完成
        /// </summary>
        private async Task<bool> WaitForUVCuringCompleteAsync(double curingTimeSeconds, int uvIndex,
            CancellationToken cancellationToken, IProgress<(int progress, string status)> progressCallback)
        {
            try
            {
                _logger.Info($"开始UV固化等待: {curingTimeSeconds}秒");

                int totalMilliseconds = (int)(curingTimeSeconds * 1000);
                int updateInterval = 1000; // 每秒更新一次进度
                int elapsed = 0;

                while (elapsed < totalMilliseconds)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.Info($"UV固化等待被取消");
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    // 更新进度
                    int progress = 60 + (int)((elapsed / (double)totalMilliseconds) * 30); // 60%到90%
                    int remainingSeconds = (totalMilliseconds - elapsed) / 1000;

                    progressCallback?.Report((progress, $"固化中... 剩余{remainingSeconds}秒"));

                    // 等待下一个更新间隔
                    int waitTime = Math.Min(updateInterval, totalMilliseconds - elapsed);
                    await Task.Delay(waitTime, cancellationToken);
                    elapsed += waitTime;
                }

                _logger.Info($"UV固化等待完成");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"UV固化等待被取消");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"UV固化等待异常");
                return false;
            }
        }

        private async Task StartUVLightAsync(int uvIndex = 1)
        {
            if (TypedParameters.UVFixTime <= 0) return;

            if (uvIndex == 1)
            {
                m_UVLight1.SetDo(1);  // 开灯
            }
            else if (uvIndex == 2)
            {
                m_UVLight2.SetDo(1);  // 开灯
            }
        }
    }

    /// <summary>
    /// UV固化参数类
    /// </summary>
    public class UVCuringParameters
    {
        public double CuringTime { get; set; }     // 固化时间（秒）
        public double Intensity { get; set; }      // 强度（%）
        public double Wavelength { get; set; }     // 波长（nm）
    }

    /// <summary>
    /// UV固化历史记录类
    /// </summary>
    public class UVCuringHistory
    {
        public int UVIndex { get; set; }
        public DateTime StartTime { get; set; }
        public double CuringTime { get; set; }
        public double Intensity { get; set; }
        public double Wavelength { get; set; }
        public bool Success { get; set; }
        public string StationName { get; set; }
    }
}
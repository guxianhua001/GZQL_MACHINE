using System;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Abstraction
{
    /// <summary>
    /// 点胶工站 Z-Scan 3D扫描操作接口，供 ZScanDetailViewModel 调用。
    /// 实现类（DispensingTask.ZScan.cs）复用 StationTaskBase 的 RunStep 安全保护。
    /// </summary>
    public interface IDispensingZScanOperations
    {
        /// <summary>
        /// 执行3D扫描运动序列（纯运动部分，不含数据接收）：
        /// 1. Dz₁/Dz₂/Dz3 抬起到安全高度
        /// 2. Dx+Dy 插补到扫描起始位
        /// 3. IO 触发3D相机
        /// 4. Dx 运动到扫描结束位
        /// 5. 复位触发信号
        /// 6. Dz₁/Dz₂/Dz3 再次抬起
        /// 7. Dx+Dy 插补到待机位
        /// </summary>
        /// <param name="safePosName">安全位置名（位置编辑器中的 SafePosition）</param>
        /// <param name="scanStartPosName">扫描起始位置名</param>
        /// <param name="scanEndPosName">扫描结束位置名</param>
        /// <param name="standbyPosName">待机位置名</param>
        /// <param name="triggerIOName">3D相机触发 IO 端口名</param>
        /// <param name="dxScanSpeed">Dx轴扫描速度(mm/s)，范围10-60，由 ViewModel ScanSpeed 传入</param>
        /// <param name="progressCallback">步骤状态回调（供 ViewModel 更新 StatusText）</param>
        /// <param name="token">取消令牌</param>
        Task ExecuteZScan3DSequenceAsync(
            string safePosName, string scanStartPosName, string scanEndPosName,
            string standbyPosName, string triggerIOName, double dxScanSpeed,
            Action<string> progressCallback, CancellationToken token);

        /// <summary>
        /// 返回待机位：Dz₁/Dz₂/Dz3 抬起到安全高度 → Dx+Dy 插补到待机位
        /// 各轴运动速度从轴参数配置获取，并使用全局速度比例
        /// </summary>
        /// <param name="safePosName">安全位置名</param>
        /// <param name="standbyPosName">待机位置名</param>
        /// <param name="progressCallback">步骤状态回调</param>
        /// <param name="token">取消令牌</param>
        Task ReturnToStandbyAsync(
            string safePosName, string standbyPosName,
            Action<string> progressCallback, CancellationToken token);
    }
}

using Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Module.Services
{
    /// <summary>
    /// 点胶执行服务接口 — 将 CAD 轨迹转换为实际运动控制指令
    /// </summary>
    public interface IDispenseExecuteService
    {
        /// <summary>
        /// 空跑仿真：沿轨迹逐段移动，可选是否下降到工作高度
        /// </summary>
        /// <param name="segments">轨迹段集合</param>
        /// <param name="descendToWorkHeight">是否下降到工作高度</param>
        /// <param name="needleIndex">针头索引（0=针头1/Dz₂, 1=针头2/Dz₃）</param>
        /// <param name="token">取消令牌</param>
        /// <param name="pauseEvent">暂停事件</param>
        /// <param name="zCorrectionEnabled">是否启用 Z 向校准（3 轴 XYZ 连续插补，跟随 CAD 表面 Z 轮廓）</param>
        Task DryRunAsync(IEnumerable<DispenseSegment> segments, bool descendToWorkHeight = false, int needleIndex = 0, CancellationToken token = default, ManualResetEventSlim? pauseEvent = null, bool zCorrectionEnabled = false);

        /// <summary>
        /// 执行完整走胶路径：按段顺序执行出胶+插补运动
        /// </summary>
        /// <param name="segments">轨迹段集合</param>
        /// <param name="site">站点标识</param>
        /// <param name="needleIndex">针头索引（0=针头1/Dz₂, 1=针头2/Dz₃）</param>
        /// <param name="token">取消令牌</param>
        /// <param name="pauseEvent">暂停事件</param>
        /// <param name="zCorrectionEnabled">是否启用 Z 向校准（3 轴 XYZ 连续插补，跟随 CAD 表面 Z 轮廓）</param>
        Task ExecutePathAsync(IEnumerable<DispenseSegment> segments, string site, int needleIndex = 0, CancellationToken token = default, ManualResetEventSlim? pauseEvent = null, bool zCorrectionEnabled = false);

        /// <summary>
        /// 执行单点点胶：定点下降→开胶→延时→关胶→上升
        /// </summary>
        /// <param name="point">目标点</param>
        /// <param name="processParams">Step3EditParamsPanel 单点模式工艺参数</param>
        /// <param name="needleIndex">针头索引（0=针头1/Dz1, 1=针头2/Dz2）</param>
        /// <param name="token">取消令牌</param>
        Task ExecuteSinglePointAsync(CadPoint point, DotProcessParams processParams, int needleIndex = 0, CancellationToken token = default);

        /// <summary>
        /// 单点模式执行线条走胶：逐点下降→开胶→出胶→关胶→抬升→循环
        /// 工艺流程：单点→Z抬升→移动至接近高度→减速到示教高度+偏移(同步检测开胶距离)→
        /// 执行点胶(起点延时)→点胶完成(收胶延时)→抬升至安全高度→循环→结束后Z抬升至待机位
        /// </summary>
        /// <param name="segments">轨迹段集合</param>
        /// <param name="processParams">单点模式工艺参数（复用点涂A参数体系）</param>
        /// <param name="needleIndex">针头索引（0=针头1/Dz1, 1=针头2/Dz2）</param>
        /// <param name="token">取消令牌</param>
        /// <param name="dryRun">空跑模式：true=不下降到工作高度不出胶，false=正常走胶</param>
        Task ExecuteSinglePointLineAsync(IEnumerable<DispenseSegment> segments, DotProcessParams processParams, int needleIndex = 0, CancellationToken token = default, bool dryRun = false, ManualResetEventSlim? pauseEvent = null);

        /// <summary> 进度变更事件：(状态描述, 当前段索引, 总段数) </summary>
        event Action<string, int, int>? ProgressChanged;

        /// <summary> 状态变更事件："Running" | "Paused" | "Completed" | "Error" | "Ready" </summary>
        event Action<string>? StatusChanged;

        /// <summary> 是否正在运行中 </summary>
        bool IsRunning { get; }
    }
}

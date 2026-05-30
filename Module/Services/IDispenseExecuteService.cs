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
        /// <summary> 空跑仿真：沿轨迹逐段移动，可选是否下降到工作高度 </summary>
        Task DryRunAsync(IEnumerable<DispenseSegment> segments, bool descendToWorkHeight = false, CancellationToken token = default);

        /// <summary> 执行完整走胶路径：按段顺序执行出胶+插补运动 </summary>
        Task ExecutePathAsync(IEnumerable<DispenseSegment> segments, string site, CancellationToken token = default);

        /// <summary> 执行单点点胶：定点下降→开胶→延时→关胶→上升 </summary>
        Task ExecuteSinglePointAsync(CadPoint point, CancellationToken token = default);

        /// <summary>
        /// 单点模式执行线条走胶：逐点下降→开胶→出胶→关胶→抬升→循环
        /// 工艺流程：单点→Z抬升→移动至接近高度→减速到示教高度+偏移(同步检测开胶距离)→
        /// 执行点胶(起点延时)→点胶完成(收胶延时)→抬升至安全高度→循环→结束后Z抬升至待机位
        /// </summary>
        /// <param name="segments">轨迹段集合</param>
        /// <param name="processParams">单点模式工艺参数（复用点涂A参数体系）</param>
        /// <param name="standbyHeight">待机高度mm（循环结束后Z轴抬升目标）</param>
        /// <param name="token">取消令牌</param>
        Task ExecuteSinglePointLineAsync(IEnumerable<DispenseSegment> segments, DotProcessParams processParams, double standbyHeight, CancellationToken token = default);

        /// <summary> 进度变更事件：(状态描述, 当前段索引, 总段数) </summary>
        event Action<string, int, int>? ProgressChanged;

        /// <summary> 状态变更事件："Running" | "Paused" | "Completed" | "Error" | "Ready" </summary>
        event Action<string>? StatusChanged;

        /// <summary> 是否正在运行中 </summary>
        bool IsRunning { get; }
    }
}

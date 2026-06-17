using Core.Models;
using Module.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Module.Services
{
    /// <summary>
    /// 点胶单点执行服务接口——点涂模式下的空跑、真实点胶和示教操作
    /// 支持双针头：needleIndex=0 使用针头1/Dz₂轴，needleIndex=1 使用针头2/Dz₃轴
    /// </summary>
    public interface IDotDispenseService
    {
        /// <summary>空跑试运行：按工艺流程运动但不出胶，Z轴保持在安全高度</summary>
        /// <param name="needleIndex">针头索引（0=针头1/Dz₂, 1=针头2/Dz₃）</param>
        Task DryRunAsync(IEnumerable<DotPoint> points, DotProcessParams processParams, int needleIndex = 0, CancellationToken token = default);

        /// <summary>真实点胶执行：按行业标准流程逐点点胶</summary>
        /// <param name="needleIndex">针头索引（0=针头1/Dz₂, 1=针头2/Dz₃）</param>
        Task ExecuteDotDispenseAsync(IEnumerable<DotPoint> points, DotProcessParams processParams, int needleIndex = 0, CancellationToken token = default);

        /// <summary>示教单点：读取当前运动轴位置填入点位坐标</summary>
        /// <param name="needleIndex">针头索引（0=针头1/Dz₂, 1=针头2/Dz₃），决定Dz2/Dz3字段写入</param>
        Task TeachPointAsync(DotPoint point, int needleIndex = 0, CancellationToken token = default);

        /// <summary>安全停止：停止所有相关轴运动并关胶，等待轴完全停止</summary>
        Task StopAsync();

        /// <summary>进度变更事件 (statusText, currentPointIndex, totalPoints)</summary>
        event Action<string, int, int> ProgressChanged;

        /// <summary>状态变更事件 ("Running"/"Completed"/"Canceled"/"Error")</summary>
        event Action<string> StatusChanged;

        /// <summary>是否正在执行</summary>
        bool IsRunning { get; }
    }
}

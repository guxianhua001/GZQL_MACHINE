using System.Threading.Tasks;

namespace StationTasks.Services
{
    /// <summary>
    /// 整机初始化服务接口：协调上下料、点胶、组装三工站的初始化序列。
    /// 初始化动作不写在工站主任务里，由本服务统一编排。
    /// </summary>
    public interface IMachineInitializationService
    {
        /// <summary> 是否正在执行初始化 </summary>
        bool IsInitializing { get; }

        /// <summary>
        /// 执行整机初始化序列：
        /// 1. 所有Z轴先归零并回到待机位（Z, Dz₁, Dz₂, Dz₃）
        /// 2. 并行：上下料Y/Rz/Rx回零、点胶Dx/Dy回零+待机、组装Cy/Ey回零+待机
        /// 3. 等待点胶工站回零完成
        /// 4. 组装X/Ry回零+待机
        /// 5. 设置站状态为等待运行（WAITRUN）
        /// </summary>
        /// <returns>true=初始化成功，false=初始化失败或被取消</returns>
        Task<bool> InitializeMachineAsync();
    }
}

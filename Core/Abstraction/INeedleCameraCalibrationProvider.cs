namespace Core.Abstraction
{
    /// <summary>
    /// 针头相机标定数据提供者——按系统号读取“相机中心↔针尖”固定偏移（即针头偏移量）。
    /// 偏移定义为 针尖坐标 - 相机中心坐标，用于将相机中心坐标换算为实际针头点胶坐标。
    /// 系统号约定：系统1 对应针头1(Dz₂)，系统2 对应针头2(Dz₃)。
    /// </summary>
    public interface INeedleCameraCalibrationProvider
    {
        /// <summary>
        /// 异步获取指定系统的相机-针头固定偏移（针尖-相机中心），无标定时返回 (0,0)。
        /// </summary>
        /// <param name="systemNumber">系统号（1=针头1，2=针头2）</param>
        System.Threading.Tasks.Task<(double OffsetX, double OffsetY)> GetCameraNeedleOffsetAsync(int systemNumber);

        /// <summary>
        /// 同步获取指定系统的相机-针头固定偏移（供运动执行线程使用），无标定时返回 (0,0)。
        /// </summary>
        /// <param name="systemNumber">系统号（1=针头1，2=针头2）</param>
        (double OffsetX, double OffsetY) GetCameraNeedleOffset(int systemNumber);
    }
}

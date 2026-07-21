namespace Core.Models
{
    /// <summary>
    /// 位置编辑器 Step5/Step6 面板操作选项——随轨迹 JSON 一并持久化
    /// </summary>
    public class CadPointPanelOptions
    {
        #region Step5 仿真/空跑

        /// <summary>UI 仿真模式（不实际运动）</summary>
        public bool IsSimMode { get; set; } = true;

        /// <summary>真实空跑模式（运动但不出胶）</summary>
        public bool IsRealDryRunMode { get; set; }

        /// <summary>空跑时是否下降到工作高度</summary>
        public bool DescendInDryRun { get; set; }

        #endregion

        #region Step6 执行

        /// <summary>目标线段 ID（Step6 单条模式 ComboBox 选中项）</summary>
        public string SelectedSegmentId { get; set; } = string.Empty;

        /// <summary>Step6 是否执行全部已启用线段（默认 true，与实机空跑一致）</summary>
        public bool IsExecuteAllEnabledSegments { get; set; } = true;

        /// <summary>线条点胶模式（单点 / 连续插补）</summary>
        public LineDispenseMode LineDispenseMode { get; set; } = LineDispenseMode.ContinuousInterpolation;

        /// <summary>是否启用 Z 高度校正</summary>
        public bool ZCorrectionEnabled { get; set; } = true;

        #endregion
    }
}

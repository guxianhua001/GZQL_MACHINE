namespace Core.Models
{
    /// <summary>
    /// 线条点胶操作模式——决定参数显示和执行方式
    /// </summary>
    public enum LineDispenseMode
    {
        /// <summary>单点模式：逐点点胶，复用点涂(A)工艺参数体系</summary>
        SinglePoint,
        /// <summary>连续插补模式：连续插补走胶，使用线段工艺参数</summary>
        ContinuousInterpolation
    }
}

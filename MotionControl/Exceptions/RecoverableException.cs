using System;
namespace MotionControl.Exceptions 
{
    /// <summary>
    /// 可恢复异常：表示工艺流程中遇到可预期的阻碍（如超时、传感器未触发），
    /// 设备不需要急停，只需暂停等待人工干预后即可继续运行。
    /// </summary>
    public class RecoverableException : Exception
    {
        /// <summary>
        /// 给操作员的排故建议（例如："请检查气缸是否卡滞，确认后点击恢复"）
        /// </summary>
        public string SuggestedAction { get; }
        public RecoverableException(string message, string suggestedAction = "请检查设备状态后点击恢复运行")
            : base(message)
        {
            SuggestedAction = suggestedAction;
        }
        public RecoverableException(string message, Exception innerException, string suggestedAction = "")
            : base(message, innerException)
        {
            SuggestedAction = suggestedAction;
        }
    }
}
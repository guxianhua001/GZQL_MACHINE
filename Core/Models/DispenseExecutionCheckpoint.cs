using Newtonsoft.Json;

namespace Core.Models
{
    /// <summary>
    /// DISPENSE 步骤运行时检查点（仅内存态，不序列化到工艺文件）
    /// 用于弧线模式在连续插补中断后恢复时跳过当前段，避免重复走胶
    /// </summary>
    public class DispenseExecutionCheckpoint
    {
        /// <summary>暂停发生在连续插补阶段时，恢复后跳过当前弧线段</summary>
        [JsonIgnore]
        public bool SkipCurrentArcSegment { get; set; }
    }
}

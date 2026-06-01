namespace Core.Constants
{
    /// <summary>
    /// 针头对针校准模块 — 补偿链接默认全局变量名。
    /// 无 JSON 配置时作为 UI 默认链接目标；用户可在界面更改为其他 Double 全局变量。
    /// 链接关系仅持久化在 JSON（CompensationX/Y/ZLinkedVar），不在全局变量池重复存储。
    /// </summary>
    public static class NeedleAlignerGlobalVariableNames
    {
        /// <summary>X 补偿默认链接的全局变量名（Double）</summary>
        public const string DefaultCompXLinkedVar = "NeedleAligner_CompX_LinkedVar";

        /// <summary>Y 补偿默认链接的全局变量名（Double）</summary>
        public const string DefaultCompYLinkedVar = "NeedleAligner_CompY_LinkedVar";

        /// <summary>Z 补偿默认链接的全局变量名（Double）</summary>
        public const string DefaultCompZLinkedVar = "NeedleAligner_CompZ_LinkedVar";

        /// <summary>已废弃：旧版重复写入的默认补偿变量（无 LinkedVar 后缀）</summary>
        public const string LegacyCompXKey = "NeedleAligner_CompX";

        /// <summary>已废弃：旧版重复写入的默认补偿变量</summary>
        public const string LegacyCompYKey = "NeedleAligner_CompY";

        /// <summary>已废弃：旧版重复写入的默认补偿变量</summary>
        public const string LegacyCompZKey = "NeedleAligner_CompZ";
    }
}

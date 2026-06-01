namespace Core.Constants
{
    /// <summary>
    /// 针头相机校准模块 — 补偿链接默认全局变量名（按系统区分）。
    /// 无 JSON 配置时作为 UI 默认链接目标；用户可在界面更改为其他 Double 全局变量。
    /// 链接关系仅持久化在 JSON（CompXLinkedVar / CompYLinkedVar），不在全局变量池重复存储。
    /// </summary>
    public static class NeedleCameraGlobalVariableNames
    {
        /// <summary>获取指定系统 X 补偿默认链接的全局变量名</summary>
        public static string GetDefaultCompXLinkedVar(int systemNumber) =>
            $"NeedleCamera_System{systemNumber}_CompX_LinkedVar";

        /// <summary>获取指定系统 Y 补偿默认链接的全局变量名</summary>
        public static string GetDefaultCompYLinkedVar(int systemNumber) =>
            $"NeedleCamera_System{systemNumber}_CompY_LinkedVar";

        /// <summary>已废弃：旧版在全局变量池中用 String 类型重复存储链接关系的键名</summary>
        public const string LegacyCompXLinkMetadataKey = "NeedleCamera_CompX_LinkedVar";

        /// <summary>已废弃：旧版在全局变量池中用 String 类型重复存储链接关系的键名</summary>
        public const string LegacyCompYLinkMetadataKey = "NeedleCamera_CompY_LinkedVar";
    }
}

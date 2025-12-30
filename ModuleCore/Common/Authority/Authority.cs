namespace ModuleCore.Common.Authority
{
    /// <summary>
    /// 权限
    /// </summary>
    public enum Authority
    {
        Guest = 0,      // 只能查看
        Operator = 1,   // 基础操作（移动、选择）
        Technician = 2,   // 配置修改（添加、编辑点位）
        Administrator = 3 // 全部权限（包括删除、系统配置）
    }
}
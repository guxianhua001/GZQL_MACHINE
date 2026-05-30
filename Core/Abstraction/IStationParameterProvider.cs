namespace Core.Abstraction
{
    /// <summary>
    /// 工站参数提供者接口，用于获取工站的配方参数信息
    /// </summary>
    public interface IStationParameterProvider
    {
        /// <summary>工站唯一标识符</summary>
        string StationIdentifier { get; }

        /// <summary>当前配方池名称</summary>
        string CurrentPoolName { get; }

        /// <summary>当前配方名称</summary>
        string CurrentRecipeName { get; }

        /// <summary>当前参数对象</summary>
        object CurrentParameters { get; }

        /// <summary>是否存在未保存的更改</summary>
        bool HasUnsavedChanges { get; }
    }
}

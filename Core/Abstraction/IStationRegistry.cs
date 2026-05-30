namespace Core.Abstraction
{
    /// <summary>
    /// 工站注册表接口，工站创建时自注册，消费者按需查询
    /// 解决模块加载时序导致的IEnumerable注入为空问题
    /// </summary>
    public interface IStationRegistry
    {
        /// <summary>注册一个工站</summary>
        void Register(IStationParameterProvider station);

        /// <summary>注销一个工站</summary>
        void Unregister(IStationParameterProvider station);

        /// <summary>获取所有已注册的工站</summary>
        IReadOnlyList<IStationParameterProvider> GetAllStations();

        /// <summary>根据工站标识符获取工站</summary>
        IStationParameterProvider GetStation(string stationIdentifier);
    }
}
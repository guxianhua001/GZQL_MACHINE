using Core.Abstraction;
using Core.Events;
using Prism.Events;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Core.Services
{
    /// <summary>
    /// 工站注册表实现，线程安全的活集合
    /// 工站创建时自注册，消费者按需查询，不受模块加载时序影响
    /// </summary>
    public class StationRegistry : IStationRegistry
    {
        private readonly ConcurrentDictionary<string, IStationParameterProvider> _stations = new();
        private readonly IEventAggregator _ea;

        public StationRegistry(IEventAggregator ea)
        {
            _ea = ea;
        }

        public void Register(IStationParameterProvider station)
        {
            if (station == null || string.IsNullOrEmpty(station.StationIdentifier)) return;
            _stations[station.StationIdentifier] = station;
            _ea.GetEvent<StationRegisteredEvent>().Publish(station);
        }

        public void Unregister(IStationParameterProvider station)
        {
            if (station == null) return;
            if (_stations.TryRemove(station.StationIdentifier, out _))
            {
                _ea.GetEvent<StationUnregisteredEvent>().Publish(station);
            }
        }

        public IReadOnlyList<IStationParameterProvider> GetAllStations()
            => _stations.Values.ToList();

        public IStationParameterProvider GetStation(string stationIdentifier)
            => _stations.TryGetValue(stationIdentifier, out var station) ? station : null;
    }
}

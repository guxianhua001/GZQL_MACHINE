using System.Collections.Generic;

namespace Recipe.Models
{
    public class ParameterStagingArea
    {
        private readonly Dictionary<string, object> _stagedParameters = new Dictionary<string, object>();
        private readonly HashSet<string> _dirtyStations = new HashSet<string>();
        private readonly object _lock = new object();

        public void Stage(string stationIdentifier, object parameters)
        {
            lock (_lock)
            {
                _stagedParameters[stationIdentifier] = parameters;
                _dirtyStations.Add(stationIdentifier);
            }
        }

        public bool IsDirty(string stationIdentifier)
        {
            lock (_lock)
            {
                return _dirtyStations.Contains(stationIdentifier);
            }
        }

        public bool HasAnyDirty()
        {
            lock (_lock)
            {
                return _dirtyStations.Count > 0;
            }
        }

        public Dictionary<string, object> GetDirtyParameters()
        {
            lock (_lock)
            {
                var result = new Dictionary<string, object>();
                foreach (var stationId in _dirtyStations)
                {
                    if (_stagedParameters.TryGetValue(stationId, out var parameters))
                    {
                        result[stationId] = parameters;
                    }
                }
                return result;
            }
        }

        public void ClearDirty()
        {
            lock (_lock)
            {
                _dirtyStations.Clear();
                _stagedParameters.Clear(); // 同步清理暂存区，避免旧数据残留
            }
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                _stagedParameters.Clear();
                _dirtyStations.Clear();
            }
        }

        public object GetStagedParameters(string stationIdentifier)
        {
            lock (_lock)
            {
                return _stagedParameters.TryGetValue(stationIdentifier, out var parameters) ? parameters : null;
            }
        }
    }
}

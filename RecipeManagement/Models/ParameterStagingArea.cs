using System.Collections.Generic;

namespace Recipe.Models
{
    public class ParameterStagingArea
    {
        private readonly Dictionary<string, object> _stagedParameters = new Dictionary<string, object>();
        private readonly HashSet<string> _dirtyStations = new HashSet<string>();
        private readonly object _lock = new object();

        /// <summary>位置编辑器暂存时标记为 true，提交时完整替换 Positions（含删除）</summary>
        private readonly Dictionary<string, bool> _replacePositionsFlags = new Dictionary<string, bool>();

        public void Stage(string stationIdentifier, object parameters, bool replacePositions = false)
        {
            lock (_lock)
            {
                _stagedParameters[stationIdentifier] = parameters;
                _dirtyStations.Add(stationIdentifier);
                if (replacePositions)
                    _replacePositionsFlags[stationIdentifier] = true;
                else
                    _replacePositionsFlags.Remove(stationIdentifier);
            }
        }

        /// <summary>该工站暂存数据是否应完整替换 Positions 节点</summary>
        public bool ShouldReplacePositions(string stationIdentifier)
        {
            lock (_lock)
            {
                return _replacePositionsFlags.TryGetValue(stationIdentifier, out var flag) && flag;
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
                _replacePositionsFlags.Clear();
            }
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                _stagedParameters.Clear();
                _dirtyStations.Clear();
                _replacePositionsFlags.Clear();
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

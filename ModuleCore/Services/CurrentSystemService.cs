using Interfaces.SharedInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModuleCore.Services
{
    public class CurrentSystemService : ICurrentSystemService
    {
        private readonly Dictionary<int, IGantrySyncService> _systems;
        private int _currentSystemId = 1;

        public CurrentSystemService(IEnumerable<IGantrySyncService> systems)
        {
            _systems = systems.ToDictionary(s => s.SystemId);
            // 设置默认系统
            _currentSystemId = _systems.Keys.First();
        }

        public IGantrySyncService CurrentSystem => _systems.TryGetValue(_currentSystemId, out var system)
            ? system
            : null;

        public event EventHandler SystemChanged;

        public void SelectSystem(int systemId)
        {
            if (_systems.ContainsKey(systemId))
            {
                _currentSystemId = systemId;
                SystemChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}

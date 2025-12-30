
using System;
using System.Collections.Generic;
using System.Linq;

namespace Interfaces
{
    public class EquipmentStatusHub : IEquipmentStateSource
    {
        private readonly List<IEquipmentStateSource> _sources = new List<IEquipmentStateSource>();
        private EquipmentState _currentState;
        private readonly object _lock = new object();

        public EquipmentState CurrentState
        {
            get => _currentState;
            private set
            {
                lock (_lock)
                {
                    if (_currentState != value)
                    {
                        var old = _currentState;
                        _currentState = value;
                        StateChanged?.Invoke(this, new EquipmentStateChangedArgs(value, old));
                    }
                }
            }
        }

        public event EventHandler<EquipmentStateChangedArgs> StateChanged;

        public void RegisterSource(IEquipmentStateSource source)
        {
            if (source == null) return;

            lock (_lock)
            {
                if (!_sources.Contains(source))
                {
                    source.StateChanged += OnSourceStateChanged;
                    _sources.Add(source);
                    UpdateAggregateState();
                }
            }
        }

        public void UnregisterSource(IEquipmentStateSource source)
        {
            lock (_lock)
            {
                if (_sources.Remove(source))
                {
                    source.StateChanged -= OnSourceStateChanged;
                    UpdateAggregateState();
                }
            }
        }

        private void OnSourceStateChanged(object sender, EquipmentStateChangedArgs e)
        {
            UpdateAggregateState();
        }

        private void UpdateAggregateState()
        {
            // 状态
            var newState = _sources.Any(s => s.CurrentState == EquipmentState.Alarm) ? EquipmentState.Alarm :
                           _sources.Any(s => s.CurrentState == EquipmentState.Running) ? EquipmentState.Running :
                           _sources.Any(s => s.CurrentState == EquipmentState.Paused) ? EquipmentState.Paused :
                           EquipmentState.Idle;

            CurrentState = newState;
        }
    }
}


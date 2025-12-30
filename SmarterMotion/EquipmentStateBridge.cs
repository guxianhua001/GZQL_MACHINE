
using Core.Abstraction;
using Interfaces;
using System;


namespace SmarterMotion
{
    public class EquipmentStateBridge : IEquipmentStateSource, IDisposable
    {
        private readonly XStationManager _stationManager;
        private XStation _station;
        private EquipmentState _currentState;

        public EquipmentState CurrentState => _currentState;
        public event EventHandler<EquipmentStateChangedArgs> StateChanged;
        public EquipmentStateBridge(XStationManager stationManager,int stationId)
        {
            _stationManager = stationManager ?? throw new ArgumentNullException(nameof(stationManager));
            _station = _stationManager.FindStationById(stationId);
            _station.OnStationStateChanged += OnStationStateChanged;
            UpdateState(_station.State);
        }
        private void OnStationStateChanged(XStationState newState)
        {
            UpdateState(newState);
        }

        private void UpdateState(XStationState hardwareState)
        {
            var newState = hardwareState switch
            {
                XStationState.ALARM or XStationState.ESTOP => EquipmentState.Alarm,
                XStationState.RUNNING => EquipmentState.Running,
                XStationState.PAUSE => EquipmentState.Paused,
                _ => EquipmentState.Idle
            };
            if (_currentState != newState)
            {
                var old = _currentState;
                _currentState = newState;
                StateChanged?.Invoke(this, new EquipmentStateChangedArgs(newState, old));
            }
        }
        public void Dispose()
        {
            if (_station != null)
            {
                _station.OnStationStateChanged -= OnStationStateChanged;
            }
        }
    }

}

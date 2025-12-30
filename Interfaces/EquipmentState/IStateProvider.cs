
namespace Interfaces
{
    using System;
    using System.ComponentModel;

    public class EquipmentStateChangedEventArgs : EventArgs
    {
        public EquipmentState NewState { get; }
        public EquipmentState PreviousState { get; }

        public EquipmentStateChangedEventArgs(EquipmentState newState, EquipmentState previousState)
        {
            NewState = newState;
            PreviousState = previousState;
        }
    }

    public interface IStateProvider
    {
        EquipmentState CurrentState { get; }
        event EventHandler<EquipmentStateChangedEventArgs> StateChanged;
    }
}

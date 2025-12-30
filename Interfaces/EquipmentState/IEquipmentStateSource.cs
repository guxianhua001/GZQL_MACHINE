
using System;

namespace Interfaces
{
    public interface IEquipmentStateSource
    {
        EquipmentState CurrentState { get; }
        event EventHandler<EquipmentStateChangedArgs> StateChanged;
    }

    public class EquipmentStateChangedArgs : EventArgs
    {
        public EquipmentState NewState { get; }
        public EquipmentState OldState { get; }
        public DateTime ChangeTime { get; }

        public EquipmentStateChangedArgs(EquipmentState newState, EquipmentState oldState)
        {
            NewState = newState;
            OldState = oldState;
            ChangeTime = DateTime.UtcNow;
        }
    }
}


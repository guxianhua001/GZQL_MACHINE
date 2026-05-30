using System;

namespace MotionControl.Interfaces
{
    public interface IMotionCardFactory
    {
        IMotionCard? GetCard(int index);
        IMotionCard? GetDefaultCard();
        int CardCount { get; }
    }

}

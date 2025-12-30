using System;

namespace Interfaces.SharedInterfaces
{
    public interface IControlSystemConfig
    {
        int SystemId { get; }
        ushort UpperXAxis { get; }
        ushort UpperYAxis { get; }
        ushort LowerXAxis { get; }
        ushort LowerYAxis { get; }
    }

}

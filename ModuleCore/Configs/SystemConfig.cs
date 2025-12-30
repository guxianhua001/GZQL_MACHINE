using Interfaces.SharedInterfaces;

namespace ModuleCore.Configs
{
    public class System1Config : IControlSystemConfig
    {
        public int SystemId => 1;
        public ushort UpperXAxis => 0;
        public ushort UpperYAxis => 10;
        public ushort LowerXAxis => 4;
        public ushort LowerYAxis => 12;
    }
    public class System2Config : IControlSystemConfig
    {
        public int SystemId => 2;
        public ushort UpperXAxis => 2;
        public ushort UpperYAxis => 14;
        public ushort LowerXAxis => 6;
        public ushort LowerYAxis => 16;
    }
}

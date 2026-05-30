using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StationTasks.Interfaces
{
    public interface IStationSignalProvider
    {
        bool IsSignalSet(string signalName);
        void SetSignal(string signalName, bool value);
        event Action<string, bool> SignalChanged;
    }
}

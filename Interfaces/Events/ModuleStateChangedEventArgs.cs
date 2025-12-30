using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces.Events
{
    // ModuleCore.Common.Events
    public class ModuleStateChangedEventArgs : EventArgs
    {
        public int ModuleId { get; set; }   // 1-4对应四个模块
        public bool NewState { get; set; }
    }

    public class ModuleStateChangedEvent : PubSubEvent<ModuleStateChangedEventArgs> { }
}

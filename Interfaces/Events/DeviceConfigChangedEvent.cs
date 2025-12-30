using Interfaces.Services;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces.Events
{
    // 配置更改事件类
    public class DeviceConfigChangedEvent : PubSubEvent<DeviceConfig>
    {
    }
}

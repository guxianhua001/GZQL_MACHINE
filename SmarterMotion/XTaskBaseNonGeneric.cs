using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmarterMotion
{
    // 非泛型任务基类，让特殊类如XController可以继承
    public abstract class XTaskBaseNonGeneric : XTaskEventHandler
    {
        public int TaskId { get; set; }
        public string Name { get; set; }
        public int Step { get; set; }
        public int LastStep { get; set; }
        public int StationId { get; set; }

        // 添加所有任务基类中的通用方法和属性
        // ...

        public virtual void Start(object runMode) { }
        public virtual void Reset() { }
        public virtual void Cancel() { }
        public virtual void Pause() { }
        public virtual void Continue() { }

        // 添加XController需要的方法
        public int _MoveHome(int axisId) => 0;
        public int _MoveAbs(int axisId, double pos, double vel, bool checkLmt = true) => 0;
        // 其他移动方法...
    }
}

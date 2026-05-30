using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MotionControl.Interfaces
{
    public interface IHasStep
    {
        void StepNext();
        void EnableSingleStep();
        void DisableSingleStep();
    }
}

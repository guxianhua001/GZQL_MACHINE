using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmarterMotion
{
    public interface ICommonParameter
    {
        int Priority { get; set; }
        string ConfigVersion { get; set; }
        void LoadParamters();

        int SaveParamters();

        bool SaveBackUp();
    }
}

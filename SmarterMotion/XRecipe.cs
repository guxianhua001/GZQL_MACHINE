using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmarterMotion
{
    /// <summary>
    /// 临时策略
    /// </summary>
    public class XRecipe
    {
        private static XRecipe instance;
        public static XRecipe Instance
        {
            get
            {
                if (instance == null)
                    instance = new XRecipe();
                return instance;
            }
            set => instance = value;
        }

        public string Name { get; set; }
        public string LastName { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Recipe.Interfaces
{
    public interface IRecipeServiceFactory
    {
        IRecipeService Create(string stationIdentifier, string stationName);
    }
}

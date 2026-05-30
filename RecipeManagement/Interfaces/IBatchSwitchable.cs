using Recipe.Models;
using System.Threading.Tasks;

namespace Recipe.Interfaces
{
    public interface IBatchSwitchable
    {
        Task SwitchToRecipeAsync(string newRecipeName, BatchSwitchContext batchContext);
    }
}

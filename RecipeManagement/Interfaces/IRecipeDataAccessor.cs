using System.Threading.Tasks;
using Core.Abstraction;

namespace Recipe.Interfaces
{
    public interface IRecipeDataAccessor<TParameters> where TParameters : TaskParametersBase
    {
        TParameters Params { get; }
        string CurrentRecipeName { get; }
        string CurrentPoolName { get; }
        bool HasUnsavedChanges { get; }
        Task SaveAsync();
        Task SwitchRecipeAsync(string newRecipeName);
        Task EditParametersAsync();
    }
}

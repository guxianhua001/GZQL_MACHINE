

using Prism.Events;
using System.Collections.Generic;

namespace Interfaces
{
    public interface IRecipeManagerService
    {
        void UpdateRecipeParameters(string recipeName, List<RecipeParameter> parameters);
        void UpdateRecipe(Recipe recipe);
        bool SwitchRecipe(string recipeName);
        Recipe GetRecipeByName(string name);
    }
}

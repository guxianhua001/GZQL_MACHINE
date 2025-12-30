// Interfaces/Events/RecipeSelectionEvent.cs
using Prism.Events;

namespace Interfaces.Events
{
    public class RecipeSelectionEvent : PubSubEvent<RecipeSelectionParams> { }

    public class RecipeSelectionParams
    {
        public string RecipeName { get; set; }
    }
}

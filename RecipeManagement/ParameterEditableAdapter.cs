using Core.Abstraction;
using Recipe.Interfaces;

namespace Recipe
{
    /// <summary>
    /// 适配器类，用于将RecipeService适配为IParameterEditable接口
    /// </summary>
    public class ParameterEditableAdapter<TParameters> : IParameterEditable
        where TParameters : TaskParametersBase, new()
    {
        private readonly RecipeService<TParameters> _recipeService;

        public ParameterEditableAdapter(RecipeService<TParameters> recipeService)
        {
            _recipeService = recipeService;
        }

        public string EditTitle => $"{_recipeService.StationName} - 参数编辑";
        public string Identifier => _recipeService.StationIdentifier;
        public object Parameters => _recipeService.Parameters;
    }
}
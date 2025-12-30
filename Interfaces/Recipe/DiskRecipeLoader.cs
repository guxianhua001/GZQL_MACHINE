using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Interfaces
{
    public class DiskRecipeLoader : BindableBase,IRecipeLoaderService
    {
        // 实现 INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        public List<string> AvailableRecipes => new ();

        public string CurrentRecipeName => throw new NotImplementedException();

        public IReadOnlyList<string> RecipeNames => throw new NotImplementedException();

        private readonly RecipePool _recipePool;
        public DiskRecipeLoader(RecipePool recipePool)
        {
            _recipePool = recipePool;
        }
        /// <summary>
        /// 加载配方
        /// </summary>
        public bool LoadRecipe(string recipeName, string recipePath = "")
        {
            try
            {
                if (_recipePool.Recipes == null)
                    return false;

                var recipe = _recipePool.Recipes.FirstOrDefault(r =>
                    r.Name.Equals(recipeName, StringComparison.OrdinalIgnoreCase));

                if (recipe == null)
                {
                    IMessage.Logger.Error($"配方未找到: {recipeName}");
                    return false;
                }

                _recipePool.CurrentRecipe = recipe;

                IMessage.Logger.Info($"成功加载配方: {recipeName}");
                return true;
            }
            catch (Exception ex)
            {
                IMessage.Logger.Error($"加载配方失败: {ex.Message}");
                return false;
            }
        }

        public void InitializeRecipes()
        {
            throw new NotImplementedException();
        }

        public void NotifyRecipeChanged()
        {
            RaisePropertyChanged(nameof(CurrentRecipeName));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentRecipeName)));
        }

    }

}


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    /// <summary>
    /// 配方加载服务接口
    /// </summary>
    public interface IRecipeLoaderService : INotifyPropertyChanged // 继承接口
    {
        bool LoadRecipe(string recipeName, string recipePath = null);
        void InitializeRecipes();
        string CurrentRecipeName { get; }
        IReadOnlyList<string> RecipeNames { get; }
    }

}

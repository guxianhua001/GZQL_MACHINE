using Newtonsoft.Json;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Interfaces
{
    /// <summary>
    /// 配方池，用于存储和管理配方数据。
    /// </summary>
    public class RecipePool : BindableBase
    {
        private ObservableCollection<Recipe> _recipes = new ObservableCollection<Recipe>();
        public ObservableCollection<Recipe> Recipes => _recipes;

        private Recipe _currentRecipe;
        public Recipe CurrentRecipe
        {
            get => _currentRecipe;
            set
            {
                if (_currentRecipe != value)
                {
                    // ▶️ 移除旧事件的订阅（如果存在）
                    if (_currentRecipe != null)
                    {
                        _currentRecipe.PropertyChanged -= OnCurrentRecipePropertyChanged;
                    }
                    _currentRecipe = value;
                    OnPropertyChanged(nameof(CurrentRecipe));
                    // ▶️ 添加新事件的订阅
                    if (value != null)
                    {
                        _currentRecipe.PropertyChanged += OnCurrentRecipePropertyChanged;
                    }
                }
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void OnCurrentRecipePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // ▶️ 将内部属性的变更通知转发给外部监听器
            OnPropertyChanged(nameof(CurrentRecipe));
        }

    }
}

using Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Recipe.Models
{
    public class RecipePool : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _id = Guid.NewGuid().ToString();
        public string Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        private string _name = "Default";
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private string _description = "Default";
        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        private DateTime _createdTime = DateTime.Now;
        public DateTime CreatedTime
        {
            get => _createdTime;
            set { _createdTime = value; OnPropertyChanged(); }
        }

        private string _currentRecipeName = "Default";
        public string CurrentRecipeName
        {
            get => _currentRecipeName;
            set
            {
                if (_currentRecipeName != value)
                {
                    _currentRecipeName = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _currentRecipePoolName = "Default";
        public string CurrentRecipePoolName
        {
            get => _currentRecipePoolName;
            set { _currentRecipePoolName = value; OnPropertyChanged(); }
        }

        private DateTime _switchTime = DateTime.Now;
        public DateTime SwitchTime
        {
            get => _switchTime;
            set { _switchTime = value; OnPropertyChanged(); }
        }

        private DateTime _modifiedTime = DateTime.Now;
        public DateTime ModifiedTime
        {
            get => _modifiedTime;
            set
            {
                if (_modifiedTime != value)
                {
                    _modifiedTime = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isDefault = true;
        public bool IsDefault
        {
            get => _isDefault;
            set
            {
                if (_isDefault != value)
                {
                    _isDefault = value;
                    OnPropertyChanged();
                }
            }
        }

        private List<RecipeInfo> _recipes = new List<RecipeInfo>();
        public List<RecipeInfo> Recipes
        {
            get => _recipes;
            set { _recipes = value; OnPropertyChanged(); }
        }

        private List<GlobalVariable> _globalVariables = new List<GlobalVariable>();
        public List<GlobalVariable> GlobalVariables
        {
            get => _globalVariables;
            set { _globalVariables = value; OnPropertyChanged(); }
        }

        // 扩展数据字典（键值对存储）
        private Dictionary<string, JsonElement?> _extensionData = new ();

        /// <summary>
        /// 扩展数据字典，键为数据类型标识（如 "WorkOrderData"），值为 JSON 字符串。
        /// </summary>
        public Dictionary<string, JsonElement?> ExtensionData
        {
            get => _extensionData;
            set
            {
                _extensionData = value ?? new ();
                OnPropertyChanged();
            }
        }

        public RecipeInfo GetRecipe(string recipeId)
            => Recipes.FirstOrDefault(r => r.Id == recipeId);

        public void AddRecipe(RecipeInfo recipe)
        {
            var existing = GetRecipe(recipe.Id);
            if (existing != null)
                Recipes.Remove(existing);

            Recipes.Add(recipe);
            OnPropertyChanged(nameof(Recipes));
        }

        public RecipeInfo GetRecipeByName(string recipeName)
        {
            return Recipes?.FirstOrDefault(r => r.Name == recipeName);
        }

        /// <summary>
        /// 设置当前配方信息
        /// </summary>
        public void SetCurrentRecipeInfo(string stationIdentifier, string recipeName, string recipePool)
        {
            // 通过属性 setter 触发通知
            CurrentRecipeName = recipeName;
            CurrentRecipePoolName = recipePool;
            SwitchTime = DateTime.Now;
            //IsDefault = recipeName == "Default";
        }

        /// <summary>
        /// 获取当前配方信息
        /// </summary>
        public CurrentRecipeInfo GetCurrentRecipeInfo()
        {
            return new CurrentRecipeInfo
            {
                RecipeName = CurrentRecipeName,
                RecipePool = CurrentRecipePoolName,
                SwitchTime = SwitchTime,
                IsDefault = IsDefault,
                IsValid = !string.IsNullOrEmpty(CurrentRecipeName)
            };
        }
    }
}
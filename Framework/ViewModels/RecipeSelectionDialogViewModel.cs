// Framework/ViewModels/RecipeSelectionDialogViewModel.cs
using Prism.Mvvm;
using Prism.Commands;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using Core.Utilities;
using Core.Abstraction;

namespace Framework.ViewModels
{
    public class RecipeSelectionDialogViewModel : BindableBase, IDialogAware
    {
        private readonly ILoggerService _logger;
        private readonly ILocalizationService _localization;

        public RecipeSelectionDialogViewModel(ILoggerService logger, ILocalizationService localization)
        {
            _logger = logger;
            _localization = localization;
            SelectCommand = new DelegateCommand<string>(ExecuteSelect, CanSelect);
            CancelCommand = new DelegateCommand(ExecuteCancel);
        }

        #region 属性
        private string _title = "选择配方";
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private string _message;
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        private ObservableCollection<string> _recipes = new ObservableCollection<string>();
        public ObservableCollection<string> Recipes
        {
            get => _recipes;
            set => SetProperty(ref _recipes, value);
        }

        private string _selectedRecipe;
        public string SelectedRecipe
        {
            get => _selectedRecipe;
            set
            {
                if (SetProperty(ref _selectedRecipe, value))
                {
                    SelectCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _currentRecipe;
        public string CurrentRecipe
        {
            get => _currentRecipe;
            set => SetProperty(ref _currentRecipe, value);
        }
        #endregion

        #region 命令
        public DelegateCommand<string> SelectCommand { get; }
        public DelegateCommand CancelCommand { get; }

        private bool CanSelect(string parameter) => !string.IsNullOrEmpty(SelectedRecipe);

        private void ExecuteSelect(string parameter)
        {
            var result = new DialogResult(ButtonResult.OK);
            result.Parameters.Add("SelectedRecipe", SelectedRecipe);
            RequestClose?.Invoke(result);
        }

        private void ExecuteCancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }
        #endregion

        #region IDialogAware 实现
        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed()
        {
            _logger.Info(_localization.GetResourceOrDefault("RSD_Log_DialogClosed", "配方选择对话框已关闭"));
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            // 获取传入参数
            if (parameters.ContainsKey("AvailableRecipes"))
            {
                var recipes = parameters.GetValue<ObservableCollection<string>>("AvailableRecipes");
                Recipes.Clear();
                foreach (var recipe in recipes)
                {
                    Recipes.Add(recipe);
                }
            }

            if (parameters.ContainsKey("CurrentRecipe"))
            {
                CurrentRecipe = parameters.GetValue<string>("CurrentRecipe");
                SelectedRecipe = CurrentRecipe; // 默认选中当前配方
            }

            if (parameters.ContainsKey("Title"))
            {
                Title = parameters.GetValue<string>("Title");
            }

            if (parameters.ContainsKey("Message"))
            {
                Message = parameters.GetValue<string>("Message");
            }

            _logger.Info(string.Format(_localization.GetResourceOrDefault("RSD_Log_DialogOpened", "配方选择对话框已打开，共 {0} 个配方"), Recipes.Count));
        }
        #endregion
    }
}

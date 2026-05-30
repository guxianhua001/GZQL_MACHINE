using Core.Abstraction;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace Framework.Services
{
    public class RecipeDialogService : IRecipeDialogService
    {
        private readonly IDialogService _dialogService;

        public RecipeDialogService(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public async Task<string> ShowRecipeSelectionDialogAsync(List<string> availableRecipes, string title, string message, string stationName = null)
        {
            var tcs = new TaskCompletionSource<string>();

            try
            {
                var dialogParameters = new DialogParameters
                {
                    { "AvailableRecipes", new ObservableCollection<string>(availableRecipes) },
                    { "CurrentRecipe", "" },
                    { "Title", title },
                    { "Message", message },
                    { "StationName", stationName ?? "所有工站" }
                };

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _dialogService.ShowDialog("RecipeSelectionDialog", dialogParameters, result =>
                    {
                        try
                        {
                            var selectedRecipe = result.Parameters.GetValue<string>("SelectedRecipe");
                            tcs.SetResult(string.IsNullOrEmpty(selectedRecipe) ? null : selectedRecipe);
                        }
                        catch (Exception ex)
                        {
                            // 记录日志（此处可注入 ILogger，为简化暂略）
                            tcs.SetResult(null);
                        }
                    });
                });
            }
            catch (Exception)
            {
                tcs.SetResult(null);
            }

            return await tcs.Task;
        }

        public async Task<string> ShowConfirmationDialogAsync(string title, string message, string[] options)
        {
            try
            {
                object result = await DialogService.ShowDialogAsync(
                    title: title,
                    message: message,
                    buttons: options,
                    defaultButtonIndex: 0
                );

                if (result is int index && index >= 0 && index < options.Length)
                {
                    return options[index];
                }

                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task ShowAlertAsync(string title, string message)
        {
            try
            {
                await DialogService.ShowDialogAsync(
                    title: title,
                    message: message,
                    buttons: new[] { "确定" },
                    defaultButtonIndex: 0
                );
            }
            catch (Exception ex)
            {
                // 备用方案：使用 MessageBox
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
        }
    }
}

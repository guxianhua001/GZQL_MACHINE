using Core.Models;
using Recipe.Models;

namespace Recipe.Interfaces
{
    public interface IRecipePoolService
    {
        string CurrentPoolId { get; set; }
        string CurrentPoolName { get; set; }
        string CurrentRecipeName { get; set; }

        Task<List<RecipePool>> GetAllRecipePoolsAsync();
        Task<RecipePool> GetRecipePoolAsync(string poolId);
        Task<RecipePool> CopyRecipePoolAsync(string sourcePoolId, string newPoolId, string newName, string newDescription);
        Task CreateRecipePoolAsync(string poolId, string name);
        Task<bool> DeleteRecipePoolAsync(string poolId);
        Task RenameRecipePoolAsync(string oldPoolId, string newName, string newDescription);
        Task SaveRecipePoolAsync(RecipePool pool);
        Task SwitchToPoolAsync(string poolId, bool saveCurrentPool = true);
        Task UpdateRecipePoolAsync(string poolId, Action<RecipePool> updateAction);

        Task<List<GlobalVariable>> LoadGlobalVariablesAsync(string poolId);
        Task SaveGlobalVariablesAsync(string poolId, IEnumerable<GlobalVariable> variables);

        /// <param name="replacePositions">为 true 时完整替换 Positions（位置编辑器保存/删除位置）</param>
        void StageStationParameters(string stationIdentifier, object parameters, bool replacePositions = false);
        Task<bool> CommitStagedParametersAsync(string poolId, string recipeName);
        bool HasStagedChanges(string stationIdentifier = null);

        Task<bool> SaveStationParametersAsync(string poolId, string recipeName, string stationIdentifier, object internalParameters, bool replacePositions = false);
        Task<bool> SaveAllStationParametersAsync(string poolId, string recipeName);

        Task SwitchAllStationsAsync(string poolName, string poolId, string newRecipeName, bool showAlert = true);

        Task<List<string>> GetAllAvailableRecipesAsync(CancellationToken cancellationToken = default);
        Task<(bool exists, string poolName, string poolId)> RecipeExistsInAnyPoolAsync(string recipeId);
        Task<RecipeInfo> LoadRecipeFromAnyPoolAsync(string recipeName);

        Task<T> GetExtensionDataAsync<T>(string poolId, string key) where T : class, new();
        Task SetExtensionDataAsync<T>(string poolId, string key, T data) where T : class;

        Task<bool> ImportRecipePoolAsync(string filePath);
        Task<bool> ExportRecipePoolAsync(string poolId, string filePath);
    }
}

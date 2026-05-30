using Core.Models;

namespace Core.Abstraction
{
    public interface IZScanConfigService
    {
        ZScanConfigFile Load(string fileName = "ZScanConfig.json");
        void Save(ZScanConfigFile config, string fileName = "ZScanConfig.json");
        string GetConfigPath();
        string SaveWithTimestamp(ZScanConfigFile config);
        ZScanConfigFile LoadLastFromRecipePool();
        void SaveToRecipePool(ZScanConfigFile config, string recipeName);
        string LastSavedFilePath { get; }
        ZScanConfigFile LoadFromFile(string fullPath);
    }
}

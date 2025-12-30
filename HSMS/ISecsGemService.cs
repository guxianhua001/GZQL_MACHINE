using Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HSMS
{
    public interface ISecsGemService
    {
        SECS.SECS Secs { get; }
        bool IsConnect { get; }
        bool IsEnableSecs { get; set; }
        int controlMode { get; set; }
        bool hostToEqpHold { get; set; }
        void InitializeSECS();
        bool Initialize(int port, string deviceId);
        void InitializeDependencies(IRecipeManagerService recipeManagerService);
        event EventHandler<ConnectionStatusChangedEventArgs> ConnectionStatusChanged;
        void SetEnabled(bool enabled);
        void ConnectSECS();
        void CloseSECS();
        void UploadAlarmProcess(int alarmId);
        void ClearAlarm();
        void ClearAllAlarm();
        void OnRecipeListChanged(List<string> recipeNames);
        void SetRecipeInfo(string currentRecipe, List<string> allRecipes);
        bool GetMappingStus(string code, out short[,] mapping, out string[,] barcode);
        string OutUnitCode { get; set; }
        string CurrentRecipe { get; set; }
        List<string> RecipeList { get; set; }
        int UPH { get; set; }
        int TotalCount { get; set; }
        int recipe { get; set; }
        short[,] GetMapping { get; }
        string[,] GetSn { get; }
    }
}

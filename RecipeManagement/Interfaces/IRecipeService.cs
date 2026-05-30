using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Core.Abstraction;
using Recipe.Models;

namespace Recipe.Interfaces
{
    /// <summary>
    /// 配方服务接口（非泛型），所有工站通用
    /// </summary>
    public interface IRecipeService : IDisposable
    {
        /// <summary>工站唯一标识符</summary>
        string StationIdentifier { get; set; }

        /// <summary>工站显示名称</summary>
        string StationName { get; set; }

        /// <summary>当前配方名称</summary>
        string CurrentRecipeName { get; }

        /// <summary>当前配方池名称</summary>
        string CurrentRecipePoolName { get; }

        /// <summary>当前参数对象（类型为 TaskParametersBase 的派生类）</summary>
        object Parameters { get; }

        /// <summary>所有可用配方名称列表</summary>
        List<string> AvailableRecipes { get; }

        /// <summary>初始化任务（用于等待配方服务完成初始加载）</summary>
        Task InitializationTask { get; set; }

        /// <summary>编辑参数命令</summary>
        ICommand EditParametersCommand { get; set;}

        /// <summary>切换配方命令</summary>
        ICommand SwitchRecipeCommand { get; set;}

        /// <summary>加载指定配方的参数（不自动应用硬件）</summary>
        Task LoadRecipeParameters(string poolName, string recipeName);

        /// <summary>保存当前参数到当前配方（本地 + 配方系统）</summary>
        Task SaveCurrentParameters();

        /// <summary>将当前参数保存到指定配方（不改变当前配方）</summary>
        Task SaveParametersToRecipe(string poolName, string recipeName);

        /// <summary>异步切换到指定配方（支持用户确认对话框）</summary>
        Task SwitchRecipeAsync(string newRecipeName);

        /// <summary>直接切换到指定配方（无对话框）</summary>
        Task SwitchToRecipe(string recipeName, string poolId);

        /// <summary>获取当前配方信息（从默认池读取）</summary>
        Task<CurrentRecipeInfo> GetCurrentRecipeInfoAsync();

        /// <summary>参数应用成功事件（参数已保存并应用到硬件后触发）</summary>
        event EventHandler<object> ParametersApplied;

        /// <summary>配方切换成功事件</summary>
        event EventHandler<string> RecipeChanged;

        /// <summary>参数加载完成事件（不触发硬件应用）</summary>
        event EventHandler<object> ParametersLoaded;
    }
}
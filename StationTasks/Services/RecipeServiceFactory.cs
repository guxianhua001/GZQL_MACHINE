using Core.Abstraction;
using Core.Utilities;
using Prism.Events;
using Prism.Services.Dialogs;
using Recipe;
using Recipe.Interfaces;
using Recipe.Models;
using StationTasks.Params;
using System;
using System.Collections.Generic;

namespace StationTasks.Services
{
    /// <summary>
    /// 配方服务工厂，根据工站标识符创建对应的RecipeService实例
    /// </summary>
    public class RecipeServiceFactory : IRecipeServiceFactory
    {
        private readonly ILoggerService _logger;
        private readonly IDialogService _dialogService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IParameterEditor _parameterEditor;
        private readonly IParameterStorage _parameterStorage;
        private readonly IRecipeStorage _recipeStorage;
        private readonly IAppSettingService _appConfig;
        private readonly IRecipePoolService _recipePoolService;
        private readonly IRecipeDialogService _recipeDialogService;

        /// <summary>
        /// 工站标识符与参数类型的映射表
        /// </summary>
        private static readonly Dictionary<string, Type> StationParameterTypeMap = new()
        {
            { "LoadingStation", typeof(LoadingStationParams) },
            { "DispenserStation", typeof(DispenserStationParams) },
            { "AssemblyStation", typeof(AssemblyStationParams) },
        };

        public RecipeServiceFactory(
            ILoggerService logger,
            IDialogService dialogService,
            IEventAggregator eventAggregator,
            IParameterEditor parameterEditor,
            IParameterStorage parameterStorage,
            IRecipeStorage recipeStorage,
            IAppSettingService appConfig,
            IRecipePoolService recipePoolService,
            IRecipeDialogService recipeDialogService)
        {
            _logger = logger;
            _dialogService = dialogService;
            _eventAggregator = eventAggregator;
            _parameterEditor = parameterEditor;
            _parameterStorage = parameterStorage;
            _recipeStorage = recipeStorage;
            _appConfig = appConfig;
            _recipePoolService = recipePoolService;
            _recipeDialogService = recipeDialogService;
        }

        /// <summary>
        /// 根据工站标识符创建对应的RecipeService实例
        /// </summary>
        public IRecipeService Create(string stationIdentifier, string stationName)
        {
            if (!StationParameterTypeMap.TryGetValue(stationIdentifier, out var paramType))
            {
                _logger?.Warn($"未知的工站标识符: {stationIdentifier}，使用默认参数类型");
                paramType = typeof(TaskParametersBase);
            }

            var recipeServiceType = typeof(RecipeService<>).MakeGenericType(paramType);
            var instance = Activator.CreateInstance(recipeServiceType,
                stationIdentifier,
                stationName,
                _logger,
                _dialogService,
                _eventAggregator,
                _parameterEditor,
                _parameterStorage,
                _recipeStorage,
                _appConfig,
                _recipePoolService,
                _recipeDialogService);

            return (IRecipeService)instance;
        }
    }
}

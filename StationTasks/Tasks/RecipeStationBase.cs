using AlarmModule.Interfaces;
using Core.Abstraction;
using Core.Utilities;
using MotionControl.Interfaces;
using Prism.Commands;
using Prism.Events;
using Recipe.Extensions;
using Recipe.Interfaces;
using Recipe.Models;
using StationTasks.Models;
using StationTasks.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace StationTasks.Tasks
{
    public abstract class RecipeStationBase<TParameters> : StationTaskBase, IStationParameterProvider, IRecipeDataAccessor<TParameters>, IBatchSwitchable
        where TParameters : TaskParametersBase, new()
    {
        private readonly IRecipeService _recipeService;
        protected readonly IRecipePoolService _recipePoolService;
        protected  ILoggerService _logger;
        private readonly IStationRegistry _stationRegistry;
        private TParameters _internalParameters = new TParameters();
        private bool _hasUnsavedChanges;

        string IStationParameterProvider.StationIdentifier => StationIdentifierValue;
        string IStationParameterProvider.CurrentPoolName => _recipeService.CurrentRecipePoolName ?? "Default";
        string IStationParameterProvider.CurrentRecipeName => _recipeService.CurrentRecipeName ?? "Default";
        object IStationParameterProvider.CurrentParameters => _recipeService.Parameters;
        bool IStationParameterProvider.HasUnsavedChanges => _hasUnsavedChanges;

        TParameters IRecipeDataAccessor<TParameters>.Params => _recipeService.GetParameters<TParameters>();
        string IRecipeDataAccessor<TParameters>.CurrentRecipeName => _recipeService.CurrentRecipeName ?? "Default";
        string IRecipeDataAccessor<TParameters>.CurrentPoolName => _recipeService.CurrentRecipePoolName ?? "Default";
        bool IRecipeDataAccessor<TParameters>.HasUnsavedChanges => _hasUnsavedChanges;

        public TParameters Params => _recipeService.GetParameters<TParameters>();
        public string CurrentRecipeName => _recipeService.CurrentRecipeName ?? "Default";
        public string CurrentPoolName => _recipeService.CurrentRecipePoolName ?? "Default";

        public ICommand EditParametersCommand { get; }
        public ICommand SwitchRecipeCommand { get; }

        protected RecipeStationBase(
            IMotionService motion,
            IPositionProvider positionProvider,
            IStationInteractionService interaction,
            IEventAggregator ea,
            ILoggerService logger,
            IAlarmService alarmService,
            ISystemStateService systemState,
            IRecipeServiceFactory recipeServiceFactory,
            IRecipePoolService recipePoolService,
            IStationRegistry stationRegistry,
            ISpeedOverrideService speedOverride,
            int taskId,
            string taskName,
            string stationId)
            : base(motion, positionProvider, interaction, ea, logger, alarmService, systemState, stationRegistry, speedOverride, taskId, taskName, stationId)
        {
            _logger = logger;
            _recipePoolService = recipePoolService;
            _stationRegistry = stationRegistry;

            _recipeService = recipeServiceFactory.Create(stationId, taskName);

            EditParametersCommand = new DelegateCommand(async () => await EditParametersAsync());
            SwitchRecipeCommand = new DelegateCommand(async () => await SwitchRecipeAsync(null));

            SubscribeToRecipeEvents();

            // 自注册到工站注册表
            _stationRegistry.Register(this);
            _logger.Info($"[{StationIdentifierValue}] 已注册到工站注册表");

            _ = InitializeRecipeAsync();
        }

        private async Task InitializeRecipeAsync()
        {
            try
            {
                await _recipeService.InitializationTask.ConfigureAwait(false);

                string currentRecipeName = _recipeService.CurrentRecipeName;
                string currentPoolName = _recipeService.CurrentRecipePoolName;

                await _recipeService.LoadRecipeParameters(currentPoolName, currentRecipeName).ConfigureAwait(false);

                _internalParameters = _recipeService.GetParameters<TParameters>();
                _logger.Info($"[{StationIdentifierValue}] 配方参数初始化完成: {currentRecipeName}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifierValue}] 配方参数初始化失败: {ex.Message}");
            }
        }

        private void SubscribeToRecipeEvents()
        {
            _recipeService.ParametersApplied += OnRecipeParametersApplied;
            _recipeService.RecipeChanged += OnRecipeChanged;
            _recipeService.ParametersLoaded += OnParametersLoaded;
        }

        private void OnRecipeParametersApplied(object sender, object parameters)
        {
            if (parameters is TParameters typedParams)
            {
                _internalParameters = typedParams;
                _hasUnsavedChanges = false;
                _logger.Info($"[{StationIdentifierValue}] 配方参数已应用: {CurrentRecipeName}");
            }
        }

        private void OnRecipeChanged(object sender, string newRecipeName)
        {
            _hasUnsavedChanges = false;
            _logger.Info($"[{StationIdentifierValue}] 配方已切换: {newRecipeName}");
        }

        private void OnParametersLoaded(object sender, object parameters)
        {
            if (parameters is TParameters typedParams)
            {
                _internalParameters = typedParams;
                _logger.Info($"[{StationIdentifierValue}] 配方参数已加载: {CurrentRecipeName}");
            }
        }

        public async Task EditParametersAsync()
        {
            _recipeService.EditParametersCommand?.Execute(null);
        }

        public async Task SaveAsync()
        {
            try
            {
                var parameters = _recipeService.GetParameters<TParameters>();
                _recipePoolService.StageStationParameters(StationIdentifierValue, parameters);
                await _recipeService.SaveCurrentParameters().ConfigureAwait(false);
                _hasUnsavedChanges = false;
                _logger.Info($"[{StationIdentifierValue}] 参数已暂存并保存");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifierValue}] 保存参数失败: {ex.Message}");
            }
        }

        public async Task SwitchRecipeAsync(string newRecipeName)
        {
            await _recipeService.SwitchRecipeAsync(newRecipeName).ConfigureAwait(false);
        }

        public async Task SwitchToRecipeAsync(string newRecipeName, BatchSwitchContext batchContext)
        {
            try
            {
                _logger.Info($"[{StationIdentifierValue}] 批量切换到配方: {newRecipeName}");

                await _recipeService.LoadRecipeParameters(batchContext.PoolName, newRecipeName).ConfigureAwait(false);

                _internalParameters = _recipeService.GetParameters<TParameters>();

                _logger.Info($"[{StationIdentifierValue}] 批量切换完成: {newRecipeName}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationIdentifierValue}] 批量切换配方失败: {ex.Message}");
            }
        }

        protected T GetParameterValue<T>(Func<TParameters, T> selector)
        {
            var parameters = _recipeService.GetParameters<TParameters>();
            return selector(parameters);
        }

    }
}

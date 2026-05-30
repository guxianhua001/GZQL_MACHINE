using Core.Abstraction;
using Framework.Dialogs;
using Module.Models;
using StationTasks.Models;
using Module.Services;
using Module.Views;
using Module.Editor;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;
using Prism.Regions;
using Prism.Services.Dialogs;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Core.Models;
using System.Threading.Tasks;
using Core.Utilities;

namespace Module.ViewModels
{
    public class ProcessSequenceEditorViewModel : BindableBase, INavigationAware
    {
        private readonly IProcessSequenceService _sequenceService;
        private readonly IRegionManager _regionManager;
        private readonly IDialogService _dialogService;
        private readonly IFileDialogService _fileDialogService;
        private readonly IRecipePoolService _recipePoolService;
        private readonly IContainerProvider _containerProvider;
        private readonly ILoggerService _logger;
        private readonly Prism.Events.IEventAggregator _ea;
        private readonly ILocalizationService _localization;
        private PropertyChangedEventHandler _propertyChangedHandler;

        public ProcessSequenceEditorViewModel(
            ILoggerService logger,
            IProcessSequenceService sequenceService,
            IRegionManager regionManager,
            IDialogService dialogService,
            IFileDialogService fileDialogService,
            IRecipePoolService recipePoolService,
            IContainerProvider containerProvider,
            Prism.Events.IEventAggregator eventAggregator,
            ILocalizationService localization)
        {
            _logger = logger;
            _sequenceService = sequenceService;
            _regionManager = regionManager;
            _dialogService = dialogService;
            _fileDialogService = fileDialogService;
            _recipePoolService = recipePoolService;
            _containerProvider = containerProvider;
            _ea = eventAggregator;
            _localization = localization;

            Tasks = _sequenceService.Tasks;
            CameraOptions = _sequenceService.CameraOptions;
            PurposeOptions = _sequenceService.PurposeOptions;
            ComponentFeatureOptions = _sequenceService.ComponentFeatureOptions;
            SiteFeatureOptions = _sequenceService.SiteFeatureOptions;
            ValidationResults = new ObservableCollection<ValidationItem>();
            Components = _sequenceService.Components;
            Sites = _sequenceService.Sites;

            // 命令绑定
            AutoGenerateCommand = new DelegateCommand(() =>
            {
                _sequenceService.AutoGenerate();
                ValidateAll();
                SelectedStep = null;
            }).ObservesProperty(() => CurrentTask);

            DeleteStepCommand = new DelegateCommand(() => _sequenceService.DeleteStep(), () => SelectedStep != null)
                .ObservesProperty(() => SelectedStep);
            MoveStepUpCommand = new DelegateCommand(() => _sequenceService.MoveStepUp(), () => SelectedStep != null && SelectedStep.Seq > 1)
                .ObservesProperty(() => SelectedStep);
            MoveStepDownCommand = new DelegateCommand(() => _sequenceService.MoveStepDown(), () => SelectedStep != null && SelectedStep.Seq < CurrentTask?.Steps.Count)
                .ObservesProperty(() => SelectedStep)
                .ObservesProperty(() => CurrentTask);
            NewTaskCommand = new DelegateCommand(() => _sequenceService.AddTask());
            DeleteTaskCommand = new DelegateCommand(() => _sequenceService.DeleteTask(), () => CurrentTask != null && Tasks.Count > 1 && !CurrentTask.IsDefault)
                .ObservesProperty(() => CurrentTask);
            RenameTaskCommand = new DelegateCommand(OnRenameTask, () => CurrentTask != null)
                .ObservesProperty(() => CurrentTask);
            AddStepCommand = new DelegateCommand(OnAddStep);
            SaveToJsonCommand = new DelegateCommand(OnSaveToJson);
            LoadFromJsonCommand = new DelegateCommand(OnLoadFromJson);

            // 任务控制命令
            StartTaskCommand = new DelegateCommand(() => _sequenceService.StartTask(), () => CurrentTask != null && CurrentTask.Status != TaskItem.TaskStatusEnum.Running)
                .ObservesProperty(() => CurrentTask.Status);
            StopTaskCommand = new DelegateCommand(() => _sequenceService.StopTask(), () => CurrentTask != null && CurrentTask.Status != TaskItem.TaskStatusEnum.Stopped)
                .ObservesProperty(() => CurrentTask.Status);
            PauseTaskCommand = new DelegateCommand(() => _sequenceService.PauseTask(), () => CurrentTask != null && CurrentTask.Status == TaskItem.TaskStatusEnum.Running)
                .ObservesProperty(() => CurrentTask.Status);
            ResumeTaskCommand = new DelegateCommand(() => _sequenceService.ResumeTask(), () => CurrentTask != null && CurrentTask.Status == TaskItem.TaskStatusEnum.Paused)
                .ObservesProperty(() => CurrentTask.Status);

            RunSingleStepCommand = new DelegateCommand(async () => await OnRunSingleStepAsync(), () => SelectedStep != null && !_sequenceService.IsExecuting)
                .ObservesProperty(() => SelectedStep);
            
            OpenDashboardCommand = new DelegateCommand<ProcessStep>(OnOpenDashboard);
            InsertDashboardStepCommand = new DelegateCommand(OnInsertDashboardStep);
            OpenBranchConfigCommand = new DelegateCommand<ProcessStep>(OnOpenBranchConfig);
            InsertBranchStepCommand = new DelegateCommand(OnInsertBranchStep);

            // 订阅配方池切换
            if (_recipePoolService is INotifyPropertyChanged inpc)
            {
                _propertyChangedHandler = (s, e) =>
                {
                    if (e.PropertyName == nameof(IRecipePoolService.CurrentPoolName))
                        System.Windows.Application.Current?.Dispatcher.InvokeAsync(async () => await _sequenceService.LoadWorkOrderDataAsync());
                };
                _sequenceService.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IProcessSequenceService.CurrentTask))
                    {
                        RaisePropertyChanged(nameof(CurrentTask));
                        (MoveStepDownCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        (DeleteTaskCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        (StartTaskCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        (StopTaskCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        (PauseTaskCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        (ResumeTaskCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        ValidateAll();
                    }
                    else if (e.PropertyName == nameof(IProcessSequenceService.SelectedStep))
                    {
                        RaisePropertyChanged(nameof(SelectedStep));
                        (MoveStepUpCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        (MoveStepDownCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        (DeleteStepCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    }
                    else if (e.PropertyName == nameof(IProcessSequenceService.CurrentFilePath))
                    {
                        CurrentSequenceFilePath = _sequenceService.CurrentFilePath;
                        CurrentSequenceFileName = !string.IsNullOrEmpty(_sequenceService.CurrentFilePath)
                            ? System.IO.Path.GetFileNameWithoutExtension(_sequenceService.CurrentFilePath)
                            : null;
                    }
                    else if (e.PropertyName == nameof(IProcessSequenceService.SelectedSite))
                    {
                        if (_selectedSite != _sequenceService.SelectedSite)
                        {
                            _selectedSite = _sequenceService.SelectedSite;
                            RaisePropertyChanged(nameof(SelectedSite));
                        }
                    }
                    else if (e.PropertyName == nameof(IProcessSequenceService.SelectedComponent))
                    {
                        if (_selectedComponent != _sequenceService.SelectedComponent)
                        {
                            _selectedComponent = _sequenceService.SelectedComponent;
                            RaisePropertyChanged(nameof(SelectedComponent));
                        }
                    }
                };
                inpc.PropertyChanged += _propertyChangedHandler;
            }
        }

        public Array StepTypeValues => Enum.GetValues(typeof(StepType));
        public ObservableCollection<TaskItem> Tasks { get; }
        public TaskItem CurrentTask
        {
            get => _sequenceService.CurrentTask;
            set
            {
                _sequenceService.CurrentTask = value;
                RaisePropertyChanged();
            }
        }
        public ProcessStep SelectedStep
        {
            get => _sequenceService.SelectedStep;
            set
            {
                _sequenceService.SelectedStep = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(HasAnyStepError));
                RaisePropertyChanged(nameof(SelectedStepErrorMessage));
            }
        }

        /// <summary> 当前是否有任何步骤存在错误（用于错误详情面板的可见性控制） </summary>
        public bool HasAnyStepError => CurrentTask?.Steps.Any(s => s.HasError) == true;

        /// <summary> 当前选中步骤的错误信息（无错误时为空） </summary>
        public string SelectedStepErrorMessage => SelectedStep?.ErrorMessage ?? string.Empty;

        /// <summary> 清除当前步骤的错误状态（操作员确认错误后手动清除） </summary>
        public DelegateCommand ClearStepErrorCommand => new DelegateCommand(() =>
        {
            if (SelectedStep != null)
            {
                SelectedStep.ErrorMessage = null;
                SelectedStep.HasActiveAlarm = false;
                RaisePropertyChanged(nameof(HasAnyStepError));
                RaisePropertyChanged(nameof(SelectedStepErrorMessage));
            }
        });

        /// <summary>
        /// 双击步骤行时：根据步骤类型打开对应的详细编辑弹窗
        /// DASHBOARD → 数据看板 / BRANCH → 条件分支配置 / 其他 → 通用步骤编辑
        /// </summary>
        public void OpenStepDetail()
        {
            if (SelectedStep == null)
            {
                _logger.Info("[OpenStepDetail] SelectedStep为null，跳过");
                return;
            }
            _logger.Info($"[OpenStepDetail] 步骤: Seq={SelectedStep.Seq}, Step={SelectedStep.Step}, CompFeature={SelectedStep.CompFeature}");
            OpenStepDetailForStep(SelectedStep);
        }

        /// <summary>
        /// 打开指定步骤的详细编辑弹窗（双击图标或双击行时调用）
        /// </summary>
        public void OpenStepDetailForStep(ProcessStep step)
        {
            if (step == null) return;

            _logger.Info($"[OpenStepDetailForStep] 步骤类型={step.Step}, Seq={step.Seq}");

            switch (step.Step)
            {
                case StepType.DASHBOARD:
                    _logger.Info("[OpenStepDetailForStep] → 调用 OnOpenDashboard");
                    OnOpenDashboard(step);
                    break;
                case StepType.BRANCH:
                    _logger.Info("[OpenStepDetailForStep] → 调用 OnOpenBranchConfig");
                    OnOpenBranchConfig(step);
                    break;
                default:
                    _logger.Info($"[OpenStepDetailForStep] → 调用 NavigateToDetailView (类型={step.Step})");
                    NavigateToDetailView(step);
                    break;
            }
        }

        private string _currentSequenceFileName;
        public string CurrentSequenceFileName { get => _currentSequenceFileName; set => SetProperty(ref _currentSequenceFileName, value); }
        private string _currentSequenceFilePath;
        public string CurrentSequenceFilePath { get => _currentSequenceFilePath; set => SetProperty(ref _currentSequenceFilePath, value); }

        public ObservableCollection<string> CameraOptions { get; }
        public ObservableCollection<string> PurposeOptions { get; }
        public ObservableCollection<string> ComponentFeatureOptions { get; }
        public ObservableCollection<string> SiteFeatureOptions { get; }

        /// <summary> 最近使用的序列文件列表（代理到 Service） </summary>
        public ObservableCollection<string> RecentFiles => _sequenceService.RecentFiles;

        private string _selectedRecentFile;
        /// <summary> 选中的最近文件，切换时自动加载 </summary>
        public string SelectedRecentFile
        {
            get => _selectedRecentFile;
            set
            {
                if (SetProperty(ref _selectedRecentFile, value) && !string.IsNullOrEmpty(value))
                {
                    if (value != _sequenceService.CurrentFilePath)
                        SwitchToRecentFile(value);
                }
            }
        }

        public ObservableCollection<ValidationItem> ValidationResults { get; private set; }
        public ObservableCollection<Models.Component> Components { get; }
        public ObservableCollection<Site> Sites { get; }

        private Models.Component _selectedComponent;
        public Models.Component SelectedComponent
        {
            get => _selectedComponent;
            set { if (SetProperty(ref _selectedComponent, value)) _sequenceService.SelectedComponent = value; }
        }
        private Site _selectedSite;
        public Site SelectedSite
        {
            get => _selectedSite;
            set { if (SetProperty(ref _selectedSite, value)) _sequenceService.SelectedSite = value; }
        }

        public ICommand AddStepCommand { get; }
        public ICommand DeleteStepCommand { get; }
        public ICommand AutoGenerateCommand { get; }
        public ICommand SaveToJsonCommand { get; }
        public ICommand LoadFromJsonCommand { get; }
        public ICommand MoveStepUpCommand { get; }
        public ICommand MoveStepDownCommand { get; }
        public ICommand NewTaskCommand { get; }
        public ICommand DeleteTaskCommand { get; }
        public ICommand RenameTaskCommand { get; }
        public ICommand StartTaskCommand { get; }
        public ICommand StopTaskCommand { get; }
        public ICommand PauseTaskCommand { get; }
        public ICommand ResumeTaskCommand { get; }

        /// <summary> 单独运行选中的步骤 </summary>
        public ICommand RunSingleStepCommand { get; }
        
        /// <summary> 打开数据看板（编辑器中预览/配置） </summary>
        public ICommand OpenDashboardCommand { get; }

        /// <summary> 在当前选中步骤后插入一个 DASHBOARD 步骤 </summary>
        public ICommand InsertDashboardStepCommand { get; }

        /// <summary> 打开条件分支配置对话框 </summary>
        public ICommand OpenBranchConfigCommand { get; }

        /// <summary> 在当前选中步骤后插入一个带条件分支的步骤 </summary>
        public ICommand InsertBranchStepCommand { get; }

        /// <summary>
        /// 根据步骤类型导航到对应的详细视图
        /// </summary>
        private void NavigateToDetailView(ProcessStep step)
        {
            if (step.Step == StepType.GOTO)
            {
                ShowGotoDetailDialog(step);
            }
            else if (step.Step == StepType.VISION)
            {
                ShowVisionDetailDialog(step);
            }
            else if (step.Step == StepType.SCAN)
            {
                ShowScanDetailDialog(step);
            }
            else if (step.Step == StepType.SEEK)
            {
                ShowSeekDetailDialog(step);
            }
            else if (step.Step == StepType.WAIT)
            {
                ShowWaitDetailDialog(step);
            }
            else if (step.Step == StepType.SCRIPT)
            {
                ShowScriptDetailDialog(step);
            }
            else if (step.Step == StepType.DASHBOARD)
            {
                OnOpenDashboard(step);
            }
            else if (step.Step == StepType.PICK)
            {
                ShowPickDetailDialog(step);
            }
            else if (step.Step == StepType.RELEASE)
            {
                ShowReleaseDetailDialog(step);
            }
            else if (step.Step == StepType.CURE)
            {
                ShowCureDetailDialog(step);
            }
            else if (step.Step == StepType.DISPENSE)
            {
                ShowDispenseDetailDialog(step);
            }
        }

        /// <summary>
        /// 安全打开 DialogHost：若已有对话框打开则先关闭，避免 "DialogHost is already open" 异常
        /// </summary>
        private static async Task<object> ShowDialogSafely(object content, string dialogIdentifier = "MainDialogHost")
        {
            var session = MaterialDesignThemes.Wpf.DialogHost.GetDialogSession(dialogIdentifier);
            if (session != null)
            {
                session.Close();
                await System.Threading.Tasks.Task.Delay(50);
            }
            return await MaterialDesignThemes.Wpf.DialogHost.Show(content, dialogIdentifier);
        }

        /// <summary>
        /// 编辑器对话框关闭后自动保存序列到JSON
        /// 防止编辑的配置在序列重新加载时丢失
        /// </summary>
        private async Task AutoSaveSequenceAsync()
        {
            try
            {
                await _sequenceService.SaveSequenceAsync();
            }
            catch (Exception ex)
            {
                _logger.Warn($"自动保存序列失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 以 MaterialDesign DialogHost 模态弹窗方式展示 GOTO 步骤详细配置
        /// </summary>
        private async void ShowGotoDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<GotoDetailViewModel>();
            var view = new GotoDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowDialogSafely(view);
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 以 MaterialDesign DialogHost 模态弹窗方式展示 VISION 步骤详细配置
        /// </summary>
        private async void ShowVisionDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<VisionDetailViewModel>();
            var view = new VisionDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowDialogSafely(view);
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 以 MaterialDesign DialogHost 模态弹窗方式展示 SCAN 步骤详细配置
        /// </summary>
        private async void ShowScanDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<ScanDetailViewModel>();
            var view = new ScanDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowDialogSafely(view);
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 以 MaterialDesign DialogHost 模态弹窗方式展示 SEEK 步骤详细配置
        /// </summary>
        private async void ShowSeekDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<SeekDetailViewModel>();
            var view = new SeekDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowDialogSafely(view);
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 以 MaterialDesign DialogHost 模态弹窗方式展示 WAIT/DELAY 步骤详细配置
        /// </summary>
        private async void ShowWaitDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<WaitDetailViewModel>();
            var view = new WaitDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowDialogSafely(view);
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 以 MaterialDesign DialogHost 模态弹窗方式展示 SCRIPT 步骤详细配置
        /// </summary>
        private async void ShowScriptDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<ScriptDetailViewModel>();
            var view = new ScriptDetailView();
            view.DataContext = vm;
            vm.Step = step;
            vm.AllSteps = CurrentTask?.Steps;
            await ShowDialogSafely(view);
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 以 MaterialDesign DialogHost 模态弹窗方式展示 PICK 步骤详细配置
        /// </summary>
        private async void ShowPickDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<PickDetailViewModel>();
            var view = new PickDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowDialogSafely(view);
            await AutoSaveSequenceAsync();
        }

        private async void ShowReleaseDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<ReleaseDetailViewModel>();
            var view = new ReleaseDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowDialogSafely(view);
            await AutoSaveSequenceAsync();
        }

        private async void ShowCureDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<CureDetailViewModel>();
            var view = new CureDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowDialogSafely(view);
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 以 MaterialDesign DialogHost 模态弹窗方式展示 DISPENSE 步骤详细配置
        /// </summary>
        private async void ShowDispenseDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<DispenseDetailViewModel>();
            var view = new DispenseDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowDialogSafely(view);
            await AutoSaveSequenceAsync();
        }

        /// <summary> 打开数据看板弹窗（编辑器中预览/配置模式） </summary>
        private async void OnOpenDashboard(ProcessStep step)
        {
            _logger.Info($"[OnOpenDashboard] 开始, step={(step != null ? $"Seq={step.Seq}" : "null")}");

            if (step == null) return;

            // 如果DashboardDetail为null，自动创建默认配置
            if (step.DashboardDetail == null)
            {
                _logger.Info("[OnOpenDashboard] DashboardDetail为null，创建默认配置");
                step.DashboardDetail = new Core.Models.DashboardStepDetail
                {
                    Fields = new List<Core.Models.DashboardField>
                    {
                        new Core.Models.DashboardField { Seq = 1, DisplayName = "H2高度", Formula = "@GV:H2", Format = "F3" },
                        new Core.Models.DashboardField { Seq = 2, DisplayName = "Slot实测高度", Formula = "@GV:Slot实测", Format = "F3" },
                        new Core.Models.DashboardField { Seq = 3, DisplayName = _localization.GetResourceOrDefault("PSE_DialDistance", "拨动距离"), Formula = "@GV:H2 - @GV:Slot实测", Format = "F3" },
                        new Core.Models.DashboardField { Seq = 4, DisplayName = _localization.GetResourceOrDefault("PSE_PressHeight", "下压高度"), Formula = "@GV:H2 - @GV:Slot实测 + 0.27 + @GV:补偿值", Format = "F3" },
                        new Core.Models.DashboardField { Seq = 5, DisplayName = _localization.GetResourceOrDefault("PSE_CanAssemble", "可否组装"), ConditionFormula = "@GV:H2 - @GV:Slot实测 > 0" },
                    }
                };
            }

            try
            {
                _logger.Info("[OnOpenDashboard] 解析 DataDashboardViewModel...");
                var vm = _containerProvider.Resolve<DataDashboardViewModel>();
                _logger.Info("[OnOpenDashboard] 创建 DataDashboardView...");
                var view = new DataDashboardView();
                view.DataContext = vm;

                // 发布事件加载数据（复用运行时的事件机制，编辑模式）
                _logger.Info($"[OnOpenDashboard] 发布 ShowDashboardEvent, Fields数量={step.DashboardDetail.Fields?.Count ?? 0}");
                _ea.GetEvent<StationTasks.Events.ShowDashboardEvent>().Publish(new StationTasks.Events.ShowDashboardPayload
                {
                    Step = step,
                    Fields = new System.Collections.ObjectModel.ObservableCollection<Core.Models.DashboardField>(step.DashboardDetail.Fields),
                    ImagePath = step.DashboardDetail.ImagePath,
                    Annotations = new System.Collections.ObjectModel.ObservableCollection<Core.Models.DashboardAnnotation>(step.DashboardDetail.Annotations),
                    IsExecutionMode = false
                });

                _logger.Info("[OnOpenDashboard] 调用 ShowDialogSafely...");
                await ShowDialogSafely(view);
                _logger.Info($"[OnOpenDashboard] 已打开步骤 [{step.Seq}] 的数据看板");
            }
            catch (Exception ex)
            {
                _logger.Error($"[OnOpenDashboard] ❌ 打开数据看板失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary> 单独运行选中的步骤 </summary>
        private async Task OnRunSingleStepAsync()
        {
            if (SelectedStep == null) return;
            var targetStep = SelectedStep;
            try
            {
                targetStep.IsSingleExecuting = true;
                await _sequenceService.RunSingleStepAsync(targetStep);
            }
            catch (Exception ex)
            {
                _logger.Error($"单独执行步骤 [{targetStep.Seq}] 失败: {ex.Message}");
            }
            finally
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (targetStep != null) targetStep.IsSingleExecuting = false;
                });
            }
        }

        /// <summary> 在当前选中步骤后插入一个 DASHBOARD 步骤，预置默认字段模板 </summary>
        private void OnInsertDashboardStep()
        {
            if (CurrentTask == null) return;

            int nextSeq = CurrentTask.Steps.Count > 0 ? CurrentTask.Steps.Max(s => s.Seq) + 1 : 1;
            var newStep = new ProcessStep
            {
                Seq = nextSeq,
                Step = StepType.DASHBOARD,
                CompFeature = "—",
                SiteFeature = "—",
                DashboardDetail = new Core.Models.DashboardStepDetail
                {
                    Fields = new List<Core.Models.DashboardField>
                    {
                        new Core.Models.DashboardField { Seq = 1, DisplayName = "H2高度", Formula = "@GV:H2", Format = "F3" },
                        new Core.Models.DashboardField { Seq = 2, DisplayName = "Slot实测高度", Formula = "@GV:Slot实测", Format = "F3" },
                        new Core.Models.DashboardField { Seq = 3, DisplayName = _localization.GetResourceOrDefault("PSE_DialDistance", "拨动距离"), Formula = "@GV:H2 - @GV:Slot实测", Format = "F3", ConditionFormula = "@GV:H2 - @GV:Slot实测 > 0" },
                        new Core.Models.DashboardField { Seq = 4, DisplayName = _localization.GetResourceOrDefault("PSE_PressHeight", "下压高度"), Formula = "@GV:H2 - @GV:Slot实测 + 0.27 + @GV:补偿值", Format = "F3" },
                        new Core.Models.DashboardField { Seq = 5, DisplayName = _localization.GetResourceOrDefault("PSE_CanAssemble", "可否组装"), ConditionFormula = "@GV:H2 - @GV:Slot实测 > 0" },
                    }
                }
            };

            CurrentTask.Steps.Add(newStep);
            _logger.Info($"[ProcessSequenceEditor] 已插入 DASHBOARD 步骤 [Seq={nextSeq}]");
        }

        /// <summary> 打开条件分支配置对话框（编辑器中配置模式） </summary>
        private async void OnOpenBranchConfig(ProcessStep step)
        {
            if (step == null) return;

            // 如果BranchConfig为null，自动创建默认配置
            if (step.BranchConfig == null)
            {
                int nextSeq = step.Seq + 2;
                step.BranchConfig = new Core.Models.BranchConfig
                {
                    IsEnabled = true,
                    OutputParameters = new List<Core.Models.BranchOutputParameter>
                    {
                        new Core.Models.BranchOutputParameter { Name = _localization.GetResourceOrDefault("PSE_TestResult", "检测结果"), Value = "false", TargetGlobalVariable = "" }
                    },
                    Conditions = new List<Core.Models.BranchCondition>
                    {
                        new Core.Models.BranchCondition
                        {
                            ConditionExpression = "",
                            TargetStepSeq = nextSeq,
                            Description = _localization.GetResourceOrDefault("PSE_Condition1", "条件1")
                        }
                    },
                    DefaultAction = DefaultBranchAction.SkipTo
                };
            }

            try
            {
                var vm = _containerProvider.Resolve<ConditionBranchViewModel>();
                var view = new ConditionBranchView();
                view.DataContext = vm;
                vm.Step = step;

                bool? result = (bool?)await ShowDialogSafely(view);
                if (result == true)
                {
                    _logger.Info($"[ProcessSequenceEditor] 已更新步骤 [{step.Seq}] 的条件分支配置");
                    await AutoSaveSequenceAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[ProcessSequenceEditor] 打开条件分支配置失败: {ex.Message}");
            }
        }

        /// <summary> 在当前选中步骤后插入一个 BRANCH 条件分支步骤 </summary>
        private void OnInsertBranchStep()
        {
            if (CurrentTask == null) return;

            int nextSeq = CurrentTask.Steps.Count > 0 ? CurrentTask.Steps.Max(s => s.Seq) + 1 : 1;
            var newStep = new ProcessStep
            {
                Seq = nextSeq,
                Step = StepType.BRANCH,
                CompFeature = "—",
                SiteFeature = "—",
                BranchConfig = new Core.Models.BranchConfig
                {
                    IsEnabled = true,
                    OutputParameters = new List<Core.Models.BranchOutputParameter>(),
                    Conditions = new List<Core.Models.BranchCondition>
                    {
                        new Core.Models.BranchCondition
                        {
                            ConditionExpression = "",
                            TargetStepSeq = nextSeq + 2,
                            Description = _localization.GetResourceOrDefault("PSE_Condition1", "条件1")
                        }
                    },
                    DefaultAction = Core.Models.DefaultBranchAction.SkipTo
                }
            };

            CurrentTask.Steps.Add(newStep);
            _logger.Info($"[ProcessSequenceEditor] 已插入 BRANCH 条件分支步骤 [Seq={nextSeq}]");
        }

        private void OnAddStep()
        {
            var parameters = new DialogParameters
            {
                { "componentFeatures", ComponentFeatureOptions.ToList() },
                { "siteFeatures", SiteFeatureOptions.ToList() },
                { "cameraOptions", CameraOptions.ToList() }
            };
            _dialogService.ShowDialog("AddEditStepDialogView", parameters, r =>
            {
                if (r.Result == ButtonResult.OK && r.Parameters.TryGetValue<ProcessStep>("step", out var step))
                    _sequenceService.AddStep(step);
            });
        }

        private async void OnSaveToJson()
        {
            try
            {
                // 使用自动保存：自动创建目录并生成文件名 {stationId}_{timestamp}.json
                await _sequenceService.SaveSequenceAsync();
            }
            catch (Exception) { /* 错误处理 */ }
        }

        /// <summary>
        /// 右键重命名任务：弹出 SimpleInputDialog，校验非空和不重复后更新名称
        /// </summary>
        private void OnRenameTask()
        {
            if (CurrentTask == null) return;
            var parameters = new DialogParameters { { "value", CurrentTask.Name } };
            _dialogService.ShowDialog("SimpleInputDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var newName = result.Parameters.GetValue<string>("value");
                    if (string.IsNullOrWhiteSpace(newName)) return;
                    if (Tasks.Any(t => t != CurrentTask && t.Name == newName.Trim())) return;
                    CurrentTask.Name = newName.Trim();
                    _ = AutoSaveSequenceAsync();
                }
            });
        }

        private async void OnLoadFromJson()
        {
            string path = _fileDialogService.ShowOpenFileDialog();
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                await _sequenceService.LoadSequenceFromPathAsync(path);
                SelectedStep = null;
                ValidateAll();
                CurrentSequenceFileName = System.IO.Path.GetFileNameWithoutExtension(path);
                CurrentSequenceFilePath = path;
                SelectedRecentFile = path;
            }
            catch (Exception) { }
        }

        /// <summary>
        /// 切换到选中的最近文件
        /// </summary>
        private async void SwitchToRecentFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) return;
            try
            {
                await _sequenceService.LoadSequenceFromPathAsync(filePath);
                SelectedStep = null;
                ValidateAll();
                CurrentSequenceFileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                CurrentSequenceFilePath = filePath;
            }
            catch (Exception) { }
        }

        private void ValidateAll()
        {
            ValidationResults = _sequenceService.Validate();
            RaisePropertyChanged(nameof(ValidationResults));
        }

        public async void OnNavigatedTo(NavigationContext navigationContext)
        {
            await _sequenceService.LoadWorkOrderDataAsync();
            if (SelectedComponent == null && Components.Any()) SelectedComponent = Components.FirstOrDefault();
            if (SelectedSite == null && Sites.Any()) SelectedSite = Sites.FirstOrDefault();
            ValidateAll();
        }
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
    }
}
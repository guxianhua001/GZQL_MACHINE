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
        private readonly IBaseDialogService _baseDialogService;
        private PropertyChangedEventHandler _propertyChangedHandler;
        /// <summary> 当前订阅 PropertyChanged 的方法（用于方法级控制命令的 CanExecute 刷新） </summary>
        private ProcessMethod _subscribedMethod;

        public ProcessSequenceEditorViewModel(
            ILoggerService logger,
            IProcessSequenceService sequenceService,
            IRegionManager regionManager,
            IDialogService dialogService,
            IFileDialogService fileDialogService,
            IRecipePoolService recipePoolService,
            IContainerProvider containerProvider,
            Prism.Events.IEventAggregator eventAggregator,
            ILocalizationService localization,
            IBaseDialogService baseDialogService)
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
            _baseDialogService = baseDialogService;

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

            // 方法级控制命令（控制单个方法独立执行）
            // 命令参数为 ProcessMethod，允许从方法详情面板对指定方法发起控制
            StartMethodCommand = new DelegateCommand<ProcessMethod>(method =>
                {
                    _sequenceService.StartMethod(method);
                },
                // Idle/Stopped 均可启动；Stopped 为停止后或重启遗留状态
                method => method != null
                          && (method.Status == TaskItem.TaskStatusEnum.Idle
                              || method.Status == TaskItem.TaskStatusEnum.Stopped)
                          && !_sequenceService.IsExecuting);
            PauseMethodCommand = new DelegateCommand<ProcessMethod>(method =>
                {
                    // 仅当暂停的是当前正在执行的方法时才生效
                    if (_sequenceService.ExecutingMethod == method)
                        _sequenceService.PauseMethod();
                },
                method => method != null && method.Status == TaskItem.TaskStatusEnum.Running);
            ResumeMethodCommand = new DelegateCommand<ProcessMethod>(method =>
                {
                    if (_sequenceService.ExecutingMethod == method)
                        _sequenceService.ResumeMethod();
                },
                method => method != null && method.Status == TaskItem.TaskStatusEnum.Paused);
            StopMethodCommand = new DelegateCommand<ProcessMethod>(method =>
                {
                    _sequenceService.StopMethod(method);
                },
                // Running/Paused/Stopped 均可点 Stop：执行中则取消，遗留 Stopped 则重置为 Idle
                method => method != null && method.Status != TaskItem.TaskStatusEnum.Idle);

            // 单步模式命令
            ToggleSingleStepCommand = new DelegateCommand(OnToggleSingleStep);
            StepNextCommand = new DelegateCommand(() => _sequenceService.StepNext(), () => _sequenceService.IsExecuting && _sequenceService.IsSingleStepMode);

            // 订阅单步模式状态变化以刷新“下一步”按钮可用性和UI绑定
            _sequenceService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IProcessSequenceService.IsSingleStepMode))
                {
                    RaisePropertyChanged(nameof(IsSingleStepMode));
                    (StepNextCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                }
                else if (e.PropertyName == nameof(IProcessSequenceService.IsExecuting))
                {
                    (StepNextCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    // 任务级执行状态变化时，方法级命令的可用性也需刷新
                    // （StartMethod 依赖 !_sequenceService.IsExecuting）
                    RefreshMethodCommands();
                }
            };

            RunSingleStepCommand = new DelegateCommand(async () => await OnRunSingleStepAsync(), () => SelectedStep != null && !_sequenceService.IsExecuting)
                .ObservesProperty(() => SelectedStep);
            
            OpenDashboardCommand = new DelegateCommand<ProcessStep>(OnOpenDashboard);
            InsertDashboardStepCommand = new DelegateCommand(OnInsertDashboardStep);
            OpenBranchConfigCommand = new DelegateCommand<ProcessStep>(OnOpenBranchConfig);
            InsertBranchStepCommand = new DelegateCommand(OnInsertBranchStep);

            // IF 条件块步骤命令
            AddIfStepCommand = new DelegateCommand(OnAddIfStep, () => SelectedMethod != null)
                .ObservesProperty(() => SelectedMethod);
            AddIfSubStepCommand = new DelegateCommand<IfBranchGroup>(OnAddIfSubStep, branch => branch != null);
            OpenIfDetailCommand = new DelegateCommand<ProcessStep>(OnOpenIfDetail);

            // 树形节点操作命令（任务/方法/动作的增删改、复制粘贴、启用禁用、运行模式切换）
            NewMethodCommand = new DelegateCommand(() => _sequenceService.AddMethod(), () => CurrentTask != null)
                .ObservesProperty(() => CurrentTask);
            DeleteMethodCommand = new DelegateCommand(() => _sequenceService.DeleteMethod(),
                () => SelectedMethod != null && CurrentTask?.Methods?.Count > 1)
                .ObservesProperty(() => SelectedMethod);
            RenameMethodCommand = new DelegateCommand(OnRenameMethod, () => SelectedMethod != null)
                .ObservesProperty(() => SelectedMethod);
            CopyNodeCommand = new DelegateCommand(() => _sequenceService.CopyNode(), () => SelectedNode != null)
                .ObservesProperty(() => SelectedNode);
            PasteNodeCommand = new DelegateCommand(() => _sequenceService.PasteNode(), () => SelectedNode != null)
                .ObservesProperty(() => SelectedNode);
            ToggleNodeEnabledCommand = new DelegateCommand(() => _sequenceService.ToggleNodeEnabled(), () => SelectedNode != null)
                .ObservesProperty(() => SelectedNode);
            // 添加注释命令：弹出输入对话框，对当前选中节点（Task/Method/Step）设置注释
            EditCommentCommand = new DelegateCommand(OnEditComment, () => SelectedNode != null)
                .ObservesProperty(() => SelectedNode);
            SetTaskRunModeCommand = new DelegateCommand<TaskRunMode?>(mode =>
                {
                    if (mode.HasValue) _sequenceService.SetTaskRunMode(mode.Value);
                },
                mode => CurrentTask != null);
            AddRunTaskStepCommand = new DelegateCommand(OnAddRunTaskStep, () => SelectedMethod != null)
                .ObservesProperty(() => SelectedMethod);

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
                        (NewMethodCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        (DeleteMethodCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        (SetTaskRunModeCommand as DelegateCommand<TaskRunMode?>)?.RaiseCanExecuteChanged();
                        ValidateAll();
                    }
                    else if (e.PropertyName == nameof(IProcessSequenceService.SelectedStep))
                    {
                        RaisePropertyChanged(nameof(SelectedStep));
                        (MoveStepUpCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        (MoveStepDownCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        (DeleteStepCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    }
                    else if (e.PropertyName == nameof(IProcessSequenceService.SelectedMethod))
                    {
                        // 方法节点选中变化：刷新方法相关命令可用性
                        RaisePropertyChanged(nameof(SelectedMethod));
                        (DeleteMethodCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        (RenameMethodCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        (AddRunTaskStepCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        // 切换订阅到新选中方法的 PropertyChanged，以便方法状态变化时刷新方法级控制命令
                        SubscribeMethodPropertyChanged();
                        RefreshMethodCommands();
                    }
                    else if (e.PropertyName == nameof(IProcessSequenceService.SelectedNode))
                    {
                        // 树节点选中变化：刷新节点相关命令可用性
                        RaisePropertyChanged(nameof(SelectedNode));
                        (CopyNodeCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        (PasteNodeCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                        (ToggleNodeEnabledCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    }
                    else if (e.PropertyName == nameof(IProcessSequenceService.CurrentFilePath))
                    {
                        CurrentSequenceFilePath = _sequenceService.CurrentFilePath;
                        CurrentSequenceFileName = !string.IsNullOrEmpty(_sequenceService.CurrentFilePath)
                            ? System.IO.Path.GetFileName(_sequenceService.CurrentFilePath)
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

        /// <summary> 当前选中的树节点（TaskItem/ProcessMethod/ProcessStep），代理自 Service </summary>
        public object SelectedNode
        {
            get => _sequenceService.SelectedNode;
            set => _sequenceService.SelectedNode = value;
        }

        /// <summary> 当前选中的方法，代理自 Service </summary>
        public ProcessMethod SelectedMethod
        {
            get => _sequenceService.SelectedMethod;
            set => _sequenceService.SelectedMethod = value;
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

        /// <summary> 将步骤移动到指定位置（拖拽排序使用） </summary>
        public void MoveStepTo(ProcessStep step, ProcessMethod targetMethod, int targetIndex)
        {
            _sequenceService.MoveStepTo(step, targetMethod, targetIndex);
        }

        /// <summary> 拖拽排序：拖到目标步骤位置（支持 IF 分支内子步骤） </summary>
        public void MoveStepTo(ProcessStep draggedStep, ProcessStep targetStep)
        {
            _sequenceService.MoveStepTo(draggedStep, targetStep);
        }

        /// <summary> 判断两步骤是否可拖拽排序 </summary>
        public bool CanMoveStepTo(ProcessStep draggedStep, ProcessStep targetStep)
        {
            return _sequenceService.CanMoveStepTo(draggedStep, targetStep);
        }

        /// <summary> 将任务移动到指定位置（拖拽排序使用），转发到 Service.MoveTaskTo </summary>
        public void MoveTaskTo(TaskItem task, int targetIndex)
        {
            _sequenceService.MoveTaskTo(task, targetIndex);
        }

        private string _currentSequenceFileName;
        public string CurrentSequenceFileName { get => _currentSequenceFileName; set => SetProperty(ref _currentSequenceFileName, value); }
        private string _currentSequenceFilePath;
        public string CurrentSequenceFilePath { get => _currentSequenceFilePath; set => SetProperty(ref _currentSequenceFilePath, value); }

        public ObservableCollection<string> CameraOptions { get; }
        public ObservableCollection<string> PurposeOptions { get; }
        public ObservableCollection<string> ComponentFeatureOptions { get; }
        public ObservableCollection<string> SiteFeatureOptions { get; }

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

        /// <summary> 启动指定方法的独立执行（方法级控制） </summary>
        public ICommand StartMethodCommand { get; }
        /// <summary> 暂停指定方法（方法级控制） </summary>
        public ICommand PauseMethodCommand { get; }
        /// <summary> 恢复指定方法（方法级控制） </summary>
        public ICommand ResumeMethodCommand { get; }
        /// <summary> 停止指定方法（方法级控制） </summary>
        public ICommand StopMethodCommand { get; }

        /// <summary> 切换单步模式开关 </summary>
        public ICommand ToggleSingleStepCommand { get; }
        /// <summary> 单步模式下执行下一步 </summary>
        public ICommand StepNextCommand { get; }

        /// <summary> 是否启用单步模式（双向绑定代理到 Service） </summary>
        public bool IsSingleStepMode
        {
            get => _sequenceService.IsSingleStepMode;
            set => _sequenceService.IsSingleStepMode = value;
        }

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

        /// <summary> 添加 IF 条件块步骤（含 Then/Else 分支） </summary>
        public ICommand AddIfStepCommand { get; }

        /// <summary> 在 IF 分支组（Then/Else）下添加子步骤 </summary>
        public ICommand AddIfSubStepCommand { get; }

        /// <summary> 打开 IF 条件表达式配置对话框 </summary>
        public ICommand OpenIfDetailCommand { get; }

        /// <summary> 在当前任务下新建方法 </summary>
        public ICommand NewMethodCommand { get; }

        /// <summary> 删除当前选中的方法 </summary>
        public ICommand DeleteMethodCommand { get; }

        /// <summary> 重命名当前选中的方法 </summary>
        public ICommand RenameMethodCommand { get; }

        /// <summary> 复制当前选中节点到剪贴板 </summary>
        public ICommand CopyNodeCommand { get; }

        /// <summary> 粘贴剪贴板节点到当前选中节点下 </summary>
        public ICommand PasteNodeCommand { get; }

        /// <summary> 切换当前选中节点的启用/禁用状态 </summary>
        public ICommand ToggleNodeEnabledCommand { get; }
        /// <summary> 添加/编辑注释命令（对当前选中节点设置注释） </summary>
        public ICommand EditCommentCommand { get; }

        /// <summary> 设置当前任务的运行模式（Active/Passive） </summary>
        public ICommand SetTaskRunModeCommand { get; }

        /// <summary> 添加调用任务动作（RUNTASK 类型步骤） </summary>
        public ICommand AddRunTaskStepCommand { get; }

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
            else if (step.Step == StepType.RUNTASK)
            {
                // 调用任务步骤：打开 RUNTASK 配置对话框
                ShowRunTaskDetailDialog(step);
            }
            else if (step.Step == StepType.SIGNAL_SEND)
            {
                // 信号发送步骤：打开 SIGNAL_SEND 配置对话框
                ShowSignalSendDetailDialog(step);
            }
            else if (step.Step == StepType.SIGNAL_WAIT)
            {
                // 信号等待步骤：打开 SIGNAL_WAIT 配置对话框
                ShowSignalWaitDetailDialog(step);
            }
            else if (step.Step == StepType.IF)
            {
                // IF 条件块步骤：打开 IF 条件表达式配置对话框
                ShowIfDetailDialog(step);
            }
        }

        /// <summary>
        /// 使用 BaseDialogWindow 弹出步骤详情对话框（替代 DialogHost）
        /// </summary>
        /// <param name="view">UserControl 内容</param>
        /// <param name="titleKey">标题本地化键</param>
        /// <param name="iconKind">标题栏图标 Kind（可选）</param>
        private async Task ShowStepDetailDialog(System.Windows.Controls.UserControl view, string titleKey, string iconKind = null)
        {
            var title = _localization.GetResourceOrDefault(titleKey, titleKey);
            await _baseDialogService.ShowDialog(view, title, iconKind);
        }

        /// <summary>
        /// 安全打开 DialogHost：若已有对话框打开则先关闭，避免 "DialogHost is already open" 异常
        /// 仅用于仍需使用 DialogHost 的场景（如 Dashboard）
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
        /// 以 BaseDialogWindow 弹窗方式展示 GOTO 步骤详细配置
        /// </summary>
        private async void ShowGotoDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<GotoDetailViewModel>();
            var view = new GotoDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowStepDetailDialog(view, "PSE_DialogTitleGoto", "DebugStepOver");
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 以 BaseDialogWindow 弹窗方式展示 VISION 步骤详细配置
        /// </summary>
        private async void ShowVisionDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<VisionDetailViewModel>();
            var view = new VisionDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowStepDetailDialog(view, "PSE_DialogTitleVision", "Eye");
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 以 BaseDialogWindow 弹窗方式展示 SCAN 步骤详细配置
        /// </summary>
        private async void ShowScanDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<ScanDetailViewModel>();
            var view = new ScanDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowStepDetailDialog(view, "PSE_DialogTitleScan", "Camera");
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 以 BaseDialogWindow 弹窗方式展示 SEEK 步骤详细配置
        /// </summary>
        private async void ShowSeekDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<SeekDetailViewModel>();
            var view = new SeekDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowStepDetailDialog(view, "PSE_DialogTitleSeek", "CrosshairsGps");
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 以 BaseDialogWindow 弹窗方式展示 WAIT/DELAY 步骤详细配置
        /// </summary>
        private async void ShowWaitDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<WaitDetailViewModel>();
            var view = new WaitDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowStepDetailDialog(view, "PSE_DialogTitleWait", "TimerOutline");
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 以 BaseDialogWindow 弹窗方式展示 SCRIPT 步骤详细配置
        /// </summary>
        private async void ShowScriptDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<ScriptDetailViewModel>();
            var view = new ScriptDetailView();
            view.DataContext = vm;
            vm.Step = step;
            vm.AllSteps = CurrentTask?.Steps;
            await ShowStepDetailDialog(view, "PSE_DialogTitleScript");
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 以 BaseDialogWindow 弹窗方式展示 PICK 步骤详细配置
        /// </summary>
        private async void ShowPickDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<PickDetailViewModel>();
            var view = new PickDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowStepDetailDialog(view, "PSE_DialogTitlePick");
            await AutoSaveSequenceAsync();
        }

        private async void ShowReleaseDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<ReleaseDetailViewModel>();
            var view = new ReleaseDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowStepDetailDialog(view, "PSE_DialogTitleRelease", "HandBackLeft");
            await AutoSaveSequenceAsync();
        }

        private async void ShowCureDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<CureDetailViewModel>();
            var view = new CureDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowStepDetailDialog(view, "PSE_DialogTitleCure", "Fire");
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 以 BaseDialogWindow 弹窗方式展示 DISPENSE 步骤详细配置
        /// </summary>
        private async void ShowDispenseDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<DispenseDetailViewModel>();
            var view = new DispenseDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowStepDetailDialog(view, "PSE_DialogTitleDispense", "Water");
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
                        new Core.Models.DashboardField { Seq = 1, DisplayName = _localization.GetResourceOrDefault("PSE_H2Height", "H2高度"), Formula = "@GV:H2", Format = "F3" },
                        new Core.Models.DashboardField { Seq = 2, DisplayName = _localization.GetResourceOrDefault("PSE_SlotMeasuredHeight", "Slot实测高度"), Formula = "@GV:Slot实测", Format = "F3" },
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

                _logger.Info($"[OnOpenDashboard] 调用 ShowStepDetailDialog (BaseDialogWindow)...");
                await ShowStepDetailDialog(view, "PSE_DialogTitleDashboard", "MonitorDashboard");
                _logger.Info($"[OnOpenDashboard] 已打开步骤 [{step.Seq}] 的数据看板");
            }
            catch (Exception ex)
            {
                _logger.Error($"[OnOpenDashboard] ❌ 打开数据看板失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 切换订阅到当前 SelectedMethod 的 PropertyChanged 事件。
        /// 当方法的 Status 属性变化时（如 Running→Paused），刷新方法级控制命令的 CanExecute。
        /// 注意：Service 在后台线程改变方法状态，需通过 Dispatcher 切回 UI 线程刷新命令。
        /// </summary>
        private void SubscribeMethodPropertyChanged()
        {
            // 取消订阅旧方法
            if (_subscribedMethod != null)
            {
                _subscribedMethod.PropertyChanged -= OnMethodPropertyChanged;
            }
            // 订阅新方法
            _subscribedMethod = SelectedMethod;
            if (_subscribedMethod != null)
            {
                _subscribedMethod.PropertyChanged += OnMethodPropertyChanged;
            }
        }

        /// <summary> 方法属性变化回调：Status 变化时刷新方法级控制命令 </summary>
        private void OnMethodPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProcessMethod.Status))
            {
                // Service 可能在后台线程改变方法状态，需切到 UI 线程刷新命令
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(RefreshMethodCommands);
            }
        }

        /// <summary> 刷新所有方法级控制命令的 CanExecute（Run/Pause/Resume/Stop） </summary>
        private void RefreshMethodCommands()
        {
            (StartMethodCommand as DelegateCommand<ProcessMethod>)?.RaiseCanExecuteChanged();
            (PauseMethodCommand as DelegateCommand<ProcessMethod>)?.RaiseCanExecuteChanged();
            (ResumeMethodCommand as DelegateCommand<ProcessMethod>)?.RaiseCanExecuteChanged();
            (StopMethodCommand as DelegateCommand<ProcessMethod>)?.RaiseCanExecuteChanged();
        }

        /// <summary> 单独运行选中的步骤 </summary>
        /// <summary> 切换单步模式开关，触发属性变更以刷新UI状态 </summary>
        private void OnToggleSingleStep()
        {
            IsSingleStepMode = !IsSingleStepMode;
            RaisePropertyChanged(nameof(IsSingleStepMode));
        }

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
                        new Core.Models.DashboardField { Seq = 1, DisplayName = _localization.GetResourceOrDefault("PSE_H2Height", "H2高度"), Formula = "@GV:H2", Format = "F3" },
                        new Core.Models.DashboardField { Seq = 2, DisplayName = _localization.GetResourceOrDefault("PSE_SlotMeasuredHeight", "Slot实测高度"), Formula = "@GV:Slot实测", Format = "F3" },
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

                await ShowStepDetailDialog(view, "PSE_DialogTitleBranch", "SourceBranch");
                _logger.Info($"[ProcessSequenceEditor] 已更新步骤 [{step.Seq}] 的条件分支配置");
                await AutoSaveSequenceAsync();
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

        /// <summary>
        /// 添加 IF 条件块步骤：创建带 Then/Else 分支的 IF 步骤并添加到当前方法。
        /// 自动初始化 IfDetail（条件表达式）和 IfBranches（Then/Else 两个分支组）。
        /// </summary>
        private void OnAddIfStep()
        {
            if (CurrentTask?.Steps == null) return;

            int nextSeq = CurrentTask.Steps.Count > 0 ? CurrentTask.Steps.Max(s => s.Seq) + 1 : 1;
            var newStep = new ProcessStep
            {
                Seq = nextSeq,
                Step = StepType.IF,
                CompFeature = "—",
                SiteFeature = "—",
                IfDetail = new IfDetail
                {
                    ConditionExpression = "",
                    Description = _localization.GetResourceOrDefault("IfDetail_DefaultDescription", "条件分支")
                },
                IfBranches = new ObservableCollection<IfBranchGroup>
                {
                    new IfBranchGroup { Header = "Then", Steps = new ObservableCollection<ProcessStep>(), IsExpanded = true },
                    new IfBranchGroup { Header = "Else", Steps = new ObservableCollection<ProcessStep>(), IsExpanded = true }
                },
                IsExpanded = true
            };

            CurrentTask.Steps.Add(newStep);
            _logger.Info($"[ProcessSequenceEditor] 已插入 IF 条件块步骤 [Seq={nextSeq}]");

            // 立即打开配置对话框
            ShowIfDetailDialog(newStep);
        }

        /// <summary>
        /// 在 IF 分支组（Then/Else）下添加子步骤。
        /// 弹出 AddEditStepDialogView 选择步骤类型，添加到指定分支组的 Steps 集合。
        /// </summary>
        private void OnAddIfSubStep(IfBranchGroup branch)
        {
            if (branch == null) return;

            var parameters = new DialogParameters
            {
                { "componentFeatures", ComponentFeatureOptions.ToList() },
                { "siteFeatures", SiteFeatureOptions.ToList() },
                { "cameraOptions", CameraOptions.ToList() }
            };
            _dialogService.ShowDialog("AddEditStepDialogView", parameters, r =>
            {
                if (r.Result == ButtonResult.OK && r.Parameters.TryGetValue<ProcessStep>("step", out var step))
                {
                    // 为子步骤分配序号（基于分支组内现有数量）
                    step.Seq = branch.Steps.Count + 1;
                    branch.Steps.Add(step);
                    _logger.Info($"[ProcessSequenceEditor] 已在 IF {branch.Header} 分支下添加子步骤 [{step.Seq}] {step.Step}");
                    _ = AutoSaveSequenceAsync();
                }
            });
        }

        /// <summary>
        /// 打开 IF 条件表达式配置对话框，关闭后自动保存序列。
        /// </summary>
        private async void ShowIfDetailDialog(ProcessStep step)
        {
            // 确保 IF 步骤已初始化
            if (step.IfDetail == null)
            {
                step.IfDetail = new IfDetail { ConditionExpression = "", Description = "" };
            }
            if (step.IfBranches == null || step.IfBranches.Count < 2)
            {
                step.IfBranches = new ObservableCollection<IfBranchGroup>
                {
                    new IfBranchGroup { Header = "Then", Steps = new ObservableCollection<ProcessStep>(), IsExpanded = true },
                    new IfBranchGroup { Header = "Else", Steps = new ObservableCollection<ProcessStep>(), IsExpanded = true }
                };
            }

            try
            {
                var vm = _containerProvider.Resolve<IfDetailViewModel>();
                var view = new IfDetailView();
                view.DataContext = vm;
                vm.Step = step;

                await ShowStepDetailDialog(view, "PSE_DialogTitleIf", "SourceBranch");
                _logger.Info($"[ProcessSequenceEditor] 已更新 IF 步骤 [{step.Seq}] 的条件配置");
                await AutoSaveSequenceAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"[ProcessSequenceEditor] 打开 IF 配置对话框失败: {ex.Message}");
            }
        }

        /// <summary> 打开 IF 条件表达式配置对话框（命令入口） </summary>
        private void OnOpenIfDetail(ProcessStep step)
        {
            if (step == null) return;
            ShowIfDetailDialog(step);
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

        /// <summary>
        /// 右键重命名方法：弹出 SimpleInputDialog，校验非空后通过 Service 更新名称
        /// </summary>
        private void OnRenameMethod()
        {
            if (SelectedMethod == null) return;
            var parameters = new DialogParameters { { "value", SelectedMethod.Name } };
            _dialogService.ShowDialog("SimpleInputDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var newName = result.Parameters.GetValue<string>("value");
                    if (string.IsNullOrWhiteSpace(newName)) return;
                    _sequenceService.RenameMethod(newName.Trim());
                    _ = AutoSaveSequenceAsync();
                }
            });
        }

        /// <summary>
        /// 右键添加/编辑注释：弹出 SimpleInputDialog，对当前选中节点（Task/Method/Step）设置注释。
        /// 注释允许为空（清空注释）。
        /// </summary>
        private void OnEditComment()
        {
            if (SelectedNode == null) return;
            // 获取当前节点的注释作为对话框初始值
            string currentComment = string.Empty;
            switch (SelectedNode)
            {
                case TaskItem task:
                    currentComment = task.Comment ?? string.Empty;
                    break;
                case ProcessMethod method:
                    currentComment = method.Comment ?? string.Empty;
                    break;
                case ProcessStep step:
                    currentComment = step.Comment ?? string.Empty;
                    break;
            }
            var parameters = new DialogParameters { { "value", currentComment } };
            _dialogService.ShowDialog("SimpleInputDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var comment = result.Parameters.GetValue<string>("value");
                    _sequenceService.EditNodeComment(comment ?? string.Empty);
                    _ = AutoSaveSequenceAsync();
                }
            });
        }

        /// <summary>
        /// 添加调用任务动作（RUNTASK 类型步骤）：创建 RUNTASK 步骤并立即打开配置对话框
        /// </summary>
        private void OnAddRunTaskStep()
        {
            var step = new ProcessStep
            {
                Step = StepType.RUNTASK,
                CompFeature = "—",
                SiteFeature = "—",
                RunTaskDetail = new StationTasks.Models.RunTaskDetail()
            };
            _sequenceService.AddStep(step);
            // 立即打开配置对话框
            ShowRunTaskDetailDialog(step);
        }

        /// <summary>
        /// 弹出调用任务（RUNTASK）配置对话框，关闭后自动保存序列
        /// </summary>
        private async void ShowRunTaskDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<RunTaskDetailViewModel>();
            var view = new RunTaskDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowStepDetailDialog(view, "PSE_DialogTitleRunTask", "CallSplit");
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 弹出信号发送（SIGNAL_SEND）配置对话框，关闭后自动保存序列
        /// </summary>
        private async void ShowSignalSendDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<SignalSendDetailViewModel>();
            var view = new SignalSendDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowStepDetailDialog(view, "PSE_DialogTitleSignalSend", "Send");
            await AutoSaveSequenceAsync();
        }

        /// <summary>
        /// 弹出信号等待（SIGNAL_WAIT）配置对话框，关闭后自动保存序列
        /// </summary>
        private async void ShowSignalWaitDetailDialog(ProcessStep step)
        {
            var vm = _containerProvider.Resolve<SignalWaitDetailViewModel>();
            var view = new SignalWaitDetailView();
            view.DataContext = vm;
            vm.Step = step;
            await ShowStepDetailDialog(view, "PSE_DialogTitleSignalWait", "DownloadLock");
            await AutoSaveSequenceAsync();
        }

        private async void OnLoadFromJson()
        {
            // 默认打开路径：bin\Debug\net9.0-windows7.0\Config\ProcessSequences
            string defaultDir = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Config", "ProcessSequences");
            string path = _fileDialogService.ShowOpenFileDialog(
                filter: "JSON files (*.json)|*.json|All files (*.*)|*.*",
                title: "Load Process Sequence",
                initialDirectory: defaultDir);
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                await _sequenceService.LoadSequenceFromPathAsync(path);
                SelectedStep = null;
                ValidateAll();
                CurrentSequenceFileName = System.IO.Path.GetFileName(path);
                CurrentSequenceFilePath = path;
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
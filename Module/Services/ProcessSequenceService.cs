using AlarmModule.Interfaces;
using AlarmModule.Models;
using Core.Abstraction;
using Core.Utilities;
using Module.Models;
using MotionControl.Interfaces;
using Newtonsoft.Json;
using Prism.Events;
using Prism.Mvvm;
using Recipe.Events;
using Recipe.Interfaces;
using StationTasks.Actions;
using StationTasks.Models;
using StationTasks.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Module.Services
{
    public class ProcessSequenceService : BindableBase, IProcessSequenceService, IRunTaskExecutor
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly IParameterStorage _parameterStorage;
        private readonly IAppSettingService _appSettingService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILoggerService _logger;
        private readonly IStationRegistry _stationRegistry;
        private readonly Prism.Ioc.IContainerProvider _containerProvider;
        private readonly ILocalizationService _localization;
        private readonly IConfigFileRetentionService _configRetentionService;
        private readonly IGripperService _gripperService;
        private readonly IMotionInterlockService _motionInterlock;
        private CancellationTokenSource _executionCts;
        private bool _isExecuting;
        private StationTaskBase _activeStationTask;
        /// <summary> 单步模式开关：启用时每个步骤执行后等待用户确认 </summary>
        private bool _isSingleStepMode;
        /// <summary> 单步模式下的“下一步”等待令牌，用户点击“下一步”时设置结果解除等待 </summary>
        private TaskCompletionSource<bool> _stepNextTcs;

        // 方法级执行状态（与任务级执行共享 _isExecuting 互斥锁，确保工站运动轴安全）
        private bool _isMethodExecuting;
        private ProcessMethod _executingMethod;
        /// <summary> 方法级执行的步骤列表引用（用于停止后重置高亮） </summary>
        private ObservableCollection<ProcessStep> _methodExecutionSteps;

        // 工序序列文件默认保存目录
        private const string ProcessSequenceDirectory = "Config\\ProcessSequences";
        private const string LastPathKey = "LastProcessSequencePath";
        private const string RecentPathsKey = "RecentProcessSequencePaths";
        /// <summary> 配方池 ExtensionData 键：记录当前配方池关联的工序序列文件路径 </summary>
        private const string ProcessSequenceCurrentFileKey = "ProcessSequence_CurrentFile";
        private const int MaxRecentFiles = 10;

        private ObservableCollection<Component> _components = new ObservableCollection<Component>();
        private ObservableCollection<Site> _sites = new ObservableCollection<Site>();
        public ObservableCollection<Component> Components => _components;
        public ObservableCollection<Site> Sites => _sites;
        public event EventHandler WorkOrderDataRefreshed;

        public ProcessSequenceService(IRecipePoolService recipePoolService,
            IParameterStorage parameterStorage,
            IAppSettingService appSettingService,
            IEventAggregator eventAggregator,
            ILoggerService logger,
            IStationRegistry stationRegistry,
            Prism.Ioc.IContainerProvider containerProvider,
            ILocalizationService localization,
            IConfigFileRetentionService configRetentionService,
            IGripperService gripperService,
            IMotionInterlockService motionInterlock)
        {
            _recipePoolService = recipePoolService;
            _parameterStorage = parameterStorage;
            _appSettingService = appSettingService;
            _eventAggregator = eventAggregator;
            _logger = logger;
            _stationRegistry = stationRegistry;
            _containerProvider = containerProvider;
            _localization = localization;
            _configRetentionService = configRetentionService;
            _gripperService = gripperService;
            _motionInterlock = motionInterlock;
            Tasks = new ObservableCollection<TaskItem>();
            RecentFiles = new ObservableCollection<string>();
            CameraOptions = new ObservableCollection<string>();
            PurposeOptions = new ObservableCollection<string>();
            ComponentFeatureOptions = new ObservableCollection<string>();
            SiteFeatureOptions = new ObservableCollection<string>();
            // 创建默认任务
            AddTask(isDefault: true);

            // 订阅配方池切换事件：切换池时从新池 ExtensionData 重新加载工序序列文件（参考 ZScanDetailViewModel 模式）
            _eventAggregator.GetEvent<RecipePoolChangedEvent>().Subscribe(OnRecipePoolChanged, ThreadOption.UIThread);
        }

        /// <summary>配方池切换时从新池 ExtensionData 重新加载工序序列文件</summary>
        private void OnRecipePoolChanged(string poolName)
        {
            _ = AutoLoadLastSequenceAsync();
            _logger.Info(string.Format(_localization.GetResourceOrDefault("PSE_Log_RecipePoolSwitchedReload",
                "[ProcessSequence] 配方池切换，已从新池重新加载工序序列（池={0}）"), poolName));
        }

        // ========== 任务与步骤管理 ==========
        public ObservableCollection<TaskItem> Tasks { get; }
        private TaskItem _currentTask;
        public TaskItem CurrentTask
        {
            get => _currentTask;
            set => SetProperty(ref _currentTask, value);
        }
        private ProcessStep _selectedStep;
        public ProcessStep SelectedStep
        {
            get => _selectedStep;
            set => SetProperty(ref _selectedStep, value);
        }
        public int CurrentStepIndex { get; set; }

        private ProcessMethod _selectedMethod;
        /// <summary> 当前选中的方法节点 </summary>
        public ProcessMethod SelectedMethod
        {
            get => _selectedMethod;
            set => SetProperty(ref _selectedMethod, value);
        }

        private object _selectedNode;
        /// <summary> 当前选中的树节点（TaskItem / ProcessMethod / ProcessStep） </summary>
        public object SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (SetProperty(ref _selectedNode, value))
                {
                    // 根据节点类型同步 SelectedTask/SelectedMethod/SelectedStep
                    if (value is TaskItem task)
                    {
                        CurrentTask = task;
                    }
                    else if (value is ProcessMethod method)
                    {
                        SelectedMethod = method;
                        // 从方法推导所属任务，确保 CurrentTask 同步
                        var parentTask = FindTaskContainingMethod(method);
                        if (parentTask != null) CurrentTask = parentTask;
                    }
                    else if (value is ProcessStep step)
                    {
                        SelectedStep = step;
                        // 从步骤推导所属任务/方法（含 IF 分支内嵌子步骤）
                        var location = LocateStep(step);
                        if (location.HasValue)
                        {
                            CurrentTask = location.Value.Task;
                            SelectedMethod = location.Value.Method;
                        }
                    }
                }
            }
        }

        /// <summary> 查找包含指定方法的父任务 </summary>
        private TaskItem FindTaskContainingMethod(ProcessMethod method)
        {
            if (method == null) return null;
            foreach (var task in Tasks)
            {
                if (task.Methods != null && task.Methods.Contains(method))
                    return task;
            }
            return null;
        }

        /// <summary> 查找包含指定步骤的父任务（含 IF 分支内嵌子步骤） </summary>
        private TaskItem FindTaskContainingStep(ProcessStep step)
        {
            return LocateStep(step)?.Task;
        }

        /// <summary>
        /// 步骤在树中的位置：方法顶层步骤 IfBranch 为 null；IF 分支内子步骤 IfBranch 指向所属 Then/Else 组。
        /// </summary>
        private readonly struct StepLocation
        {
            public StepLocation(TaskItem task, ProcessMethod method, IfBranchGroup ifBranch)
            {
                Task = task;
                Method = method;
                IfBranch = ifBranch;
            }

            public TaskItem Task { get; }
            public ProcessMethod Method { get; }
            /// <summary>非 null 表示该步骤位于 IF 分支组内</summary>
            public IfBranchGroup IfBranch { get; }
        }

        /// <summary> 在任务树中定位步骤（支持 IF 嵌套子步骤） </summary>
        private StepLocation? LocateStep(ProcessStep step)
        {
            if (step == null) return null;
            foreach (var task in Tasks)
            {
                if (task.Methods == null) continue;
                foreach (var method in task.Methods)
                {
                    if (TryLocateStep(step, method.Steps, null, out var branch))
                        return new StepLocation(task, method, branch);
                }
            }
            return null;
        }

        /// <summary> 递归查找步骤，foundBranch 为 null 表示方法顶层步骤 </summary>
        private static bool TryLocateStep(
            ProcessStep target,
            ObservableCollection<ProcessStep> steps,
            IfBranchGroup currentBranch,
            out IfBranchGroup foundBranch)
        {
            foundBranch = null;
            if (steps == null) return false;

            foreach (var step in steps)
            {
                if (ReferenceEquals(step, target))
                {
                    foundBranch = currentBranch;
                    return true;
                }

                if (step.Step != StepType.IF || step.IfBranches == null) continue;

                foreach (var branch in step.IfBranches)
                {
                    if (TryLocateStep(target, branch.Steps, branch, out foundBranch))
                        return true;
                }
            }

            return false;
        }

        private StepLocation? LocateIfBranch(IfBranchGroup targetBranch)
        {
            if (targetBranch == null) return null;
            foreach (var task in Tasks)
            {
                if (task.Methods == null) continue;
                foreach (var method in task.Methods)
                {
                    if (TryLocateIfBranch(targetBranch, method.Steps, out var branch))
                        return new StepLocation(task, method, branch);
                }
            }

            return null;
        }

        private static bool TryLocateIfBranch(
            IfBranchGroup targetBranch,
            ObservableCollection<ProcessStep> steps,
            out IfBranchGroup foundBranch)
        {
            foundBranch = null;
            if (steps == null) return false;

            foreach (var step in steps)
            {
                if (step.Step != StepType.IF || step.IfBranches == null) continue;

                foreach (var branch in step.IfBranches)
                {
                    if (ReferenceEquals(branch, targetBranch))
                    {
                        foundBranch = branch;
                        return true;
                    }

                    if (TryLocateIfBranch(targetBranch, branch.Steps, out foundBranch))
                        return true;
                }
            }

            return false;
        }

        /// <summary> 剪贴板：缓存复制的节点 </summary>
        private object _clipboard;

        private string _currentFilePath;
        /// <summary> 当前加载的序列文件路径 </summary>
        public string CurrentFilePath
        {
            get => _currentFilePath;
            set => SetProperty(ref _currentFilePath, value);
        }

        /// <summary> 最近使用的序列文件路径列表（用于 UI 下拉绑定） </summary>
        public ObservableCollection<string> RecentFiles { get; }

        /// <summary>
        /// 将文件路径记录到 MRU 列表并持久化到 ExtensionData
        /// 规则：已存在则移到头部，超出上限则移除最旧的，文件不存在则从列表移除
        /// </summary>
        public void RecordRecentFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            var list = RecentFiles.ToList();
            list.Remove(filePath);
            list.Insert(0, filePath);
            if (list.Count > MaxRecentFiles)
                list = list.Take(MaxRecentFiles).ToList();

            RecentFiles.Clear();
            foreach (var p in list)
                RecentFiles.Add(p);

            SaveRecentFilesToSettings();
            RecordLastSequencePath(filePath);
            // 同步最新文件路径到配方池（按配方池隔离，避免切换配方后加载错误序列）
            _ = SaveCurrentFileToRecipePoolAsync(filePath);
        }

        /// <summary> 从 ExtensionData 读取 MRU 列表，过滤不存在的文件 </summary>
        public List<string> LoadRecentFilesFromSettings()
        {
            var result = new List<string>();
            try
            {
                if (_appSettingService.Settings.ExtensionData.TryGetValue(RecentPathsKey, out var element)
                    && element.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in element.EnumerateArray())
                    {
                        if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var path = item.GetString();
                            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                                result.Add(path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_LoadRecentFailed", "[ProcessSequence] 读取最近文件列表失败: {0}"),
                    ex.Message));
            }
            return result;
        }

        /// <summary> 将 MRU 列表持久化到 ExtensionData </summary>
        public void SaveRecentFilesToSettings()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(RecentFiles.ToList());
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                _appSettingService.Settings.ExtensionData[RecentPathsKey] = doc.RootElement.Clone();
                _appSettingService.Save();
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_SaveRecentFailed", "[ProcessSequence] 保存最近文件列表失败: {0}"),
                    ex.Message));
            }
        }

        public void AddStep(ProcessStep step)
        {
            if (CurrentTask == null) return;
            // 优先添加到选中的方法，否则添加到第一个方法（兼容旧逻辑）
            var targetMethod = SelectedMethod ?? CurrentTask.Methods?.FirstOrDefault();
            if (targetMethod == null)
            {
                // 没有方法则自动创建一个默认方法
                targetMethod = new ProcessMethod(_localization.GetResourceOrDefault("PSE_DefaultMethodName", "默认方法"));
                CurrentTask.Methods.Add(targetMethod);
            }
            step.Seq = targetMethod.Steps.Count + 1;
            targetMethod.Steps.Add(step);
            CurrentTask.SyncStepsFromMethods();
            // 选中新添加的步骤，使右侧详情面板自动显示
            SelectedMethod = targetMethod;
            SelectedNode = step;
        }

        public void DeleteStep()
        {
            if (SelectedStep == null) return;

            var location = LocateStep(SelectedStep);
            if (!location.HasValue) return;

            var loc = location.Value;
            if (loc.IfBranch != null)
            {
                // IF 分支内子步骤：从 Then/Else 组的 Steps 集合中移除
                loc.IfBranch.Steps.Remove(SelectedStep);
                RenumberIfBranchSteps(loc.IfBranch);
            }
            else
            {
                loc.Method.Steps.Remove(SelectedStep);
                RenumberSteps(loc.Task);
            }

            loc.Task.SyncStepsFromMethods();
            SelectedStep = null;
            SelectedNode = loc.Method;
        }

        /// <summary> 重编号 IF 分支组内子步骤序号 </summary>
        private static void RenumberIfBranchSteps(IfBranchGroup branch)
        {
            if (branch?.Steps == null) return;
            for (int i = 0; i < branch.Steps.Count; i++)
                branch.Steps[i].Seq = i + 1;
        }

        public void MoveStepUp()
        {
            if (SelectedStep == null) return;
            var method = SelectedMethod ?? CurrentTask?.Methods?.FirstOrDefault();
            if (method == null) return;
            int idx = method.Steps.IndexOf(SelectedStep);
            if (idx <= 0) return;
            method.Steps.Move(idx, idx - 1);
            RenumberSteps();
            SelectedStep = method.Steps[idx - 1];
        }

        public void MoveStepDown()
        {
            if (SelectedStep == null) return;
            var method = SelectedMethod ?? CurrentTask?.Methods?.FirstOrDefault();
            if (method == null) return;
            int idx = method.Steps.IndexOf(SelectedStep);
            if (idx >= method.Steps.Count - 1) return;
            method.Steps.Move(idx, idx + 1);
            RenumberSteps();
            SelectedStep = method.Steps[idx + 1];
        }

        /// <summary> 将步骤移动到指定方法的指定位置（拖拽排序使用，方法顶层步骤） </summary>
        public void MoveStepTo(ProcessStep step, ProcessMethod targetMethod, int targetIndex)
        {
            if (step == null || targetMethod == null) return;
            if (targetIndex >= 0 && targetIndex < targetMethod.Steps.Count)
                MoveStepTo(step, targetMethod.Steps[targetIndex]);
        }

        /// <summary>
        /// 拖拽排序：支持方法顶层步骤及 IF 分支内子步骤（同层级内移动）。
        /// </summary>
        public void MoveStepTo(ProcessStep draggedStep, ProcessStep targetStep)
        {
            if (draggedStep == null || targetStep == null || ReferenceEquals(draggedStep, targetStep)) return;

            var dragLoc = LocateStep(draggedStep);
            var targetLoc = LocateStep(targetStep);
            if (!dragLoc.HasValue || !targetLoc.HasValue) return;

            // IF 分支内：同一 Then/Else 组内排序
            if (dragLoc.Value.IfBranch != null && targetLoc.Value.IfBranch == dragLoc.Value.IfBranch)
            {
                var branch = dragLoc.Value.IfBranch;
                int oldIdx = branch.Steps.IndexOf(draggedStep);
                int newIdx = branch.Steps.IndexOf(targetStep);
                if (oldIdx < 0 || newIdx < 0 || oldIdx == newIdx) return;
                branch.Steps.Move(oldIdx, newIdx);
                RenumberIfBranchSteps(branch);
                SelectedStep = draggedStep;
                return;
            }

            // 方法顶层：同方法内排序
            if (dragLoc.Value.IfBranch == null && targetLoc.Value.IfBranch == null
                && dragLoc.Value.Method == targetLoc.Value.Method)
            {
                var method = dragLoc.Value.Method;
                int oldIdx = method.Steps.IndexOf(draggedStep);
                int newIdx = method.Steps.IndexOf(targetStep);
                if (oldIdx < 0 || newIdx < 0 || oldIdx == newIdx) return;
                method.Steps.Move(oldIdx, newIdx);
                RenumberSteps(dragLoc.Value.Task);
                SelectedStep = draggedStep;
                return;
            }

            _logger.Warn(_localization.GetResourceOrDefault("PSE_Log_CrossLevelDragNotSupported", "[ProcessSequence] 不支持跨层级或跨 IF 分支拖拽排序"));
        }

        /// <summary> 判断两个步骤是否可在拖拽中互相排序 </summary>
        public bool CanMoveStepTo(ProcessStep draggedStep, ProcessStep targetStep)
        {
            if (draggedStep == null || targetStep == null || ReferenceEquals(draggedStep, targetStep)) return false;

            var dragLoc = LocateStep(draggedStep);
            var targetLoc = LocateStep(targetStep);
            if (!dragLoc.HasValue || !targetLoc.HasValue) return false;

            if (dragLoc.Value.IfBranch != null)
                return targetLoc.Value.IfBranch == dragLoc.Value.IfBranch;

            return targetLoc.Value.IfBranch == null && dragLoc.Value.Method == targetLoc.Value.Method;
        }

        /// <summary> 将任务移动到指定位置（用于拖拽排序） </summary>
        public void MoveTaskTo(TaskItem task, int targetIndex)
        {
            if (task == null) return;
            int oldIndex = Tasks.IndexOf(task);
            if (oldIndex < 0) return;
            // 计算实际目标索引
            int actualTarget = targetIndex;
            if (targetIndex < 0 || targetIndex >= Tasks.Count)
                actualTarget = Tasks.Count - 1;
            if (oldIndex == actualTarget) return;
            Tasks.Move(oldIndex, actualTarget);
            _logger.Info(string.Format(
                _localization.GetResourceOrDefault("PSE_Log_TaskMoved", "[ProcessSequence] 任务 [{0}] 已从位置 {1} 移动到 {2}"),
                task.Name, oldIndex, actualTarget));
        }

        public void AddTask(bool isDefault = false)
        {
            var newTask = new TaskItem($"Task {Tasks.Count + 1}")
            {
                IsDefault = isDefault,
                Status = TaskItem.TaskStatusEnum.Idle
            };
            // 确保至少有一个默认方法，命名与 AddMethod 保持一致（Method 1）
            if (newTask.Methods.Count == 0)
                newTask.Methods.Add(new ProcessMethod($"{_localization.GetResourceOrDefault("PSE_MethodLabel", "方法")} 1"));
            Tasks.Add(newTask);
            CurrentTask = newTask;
        }

        public void DeleteTask()
        {
            // 不允许删除默认任务
            if (CurrentTask == null || Tasks.Count <= 1) return;
            if (CurrentTask.IsDefault) return;
            int idx = Tasks.IndexOf(CurrentTask);
            Tasks.Remove(CurrentTask);
            // 自动切换到前一个或后一个任务
            CurrentTask = idx >= Tasks.Count ? Tasks.Last() : Tasks[idx];
        }

        public void AutoGenerate()
        {
            if (CurrentTask == null) return;
            CurrentTask.Steps.Clear();
            var steps = new[]
            {
                new ProcessStep { Seq = 1, Step = StepType.GOTO, CompFeature = "—", SiteFeature = "HOME",
                                                SubMoves = new ObservableCollection<SubMove>
                                                {
                                                    new SubMove { SubSeq = "1a", Axis = "Y", PositionName = "Home", HomeMode = 1, HomeMinVel = 5, HomeMaxVel = 20 }
                                                } },
                new ProcessStep { Seq = 2, Step = StepType.PICK, CompFeature = "—", SiteFeature = "RACK_001" },
                new ProcessStep { Seq = 3, Step = StepType.RELEASE, CompFeature = "—", SiteFeature = "TAB_001" },
                new ProcessStep { Seq = 4, Step = StepType.GOTO, CompFeature = "—", SiteFeature = "HOME" }
            };
            foreach (var s in steps) CurrentTask.Steps.Add(s);
        }

        // ========== 树形结构管理（Task → Method → Action） ==========

        /// <summary> 在当前任务下新建方法 </summary>
        public void AddMethod()
        {
            if (CurrentTask == null) return;
            var method = new ProcessMethod($"{_localization.GetResourceOrDefault("PSE_MethodLabel", "方法")} {CurrentTask.Methods.Count + 1}");
            CurrentTask.Methods.Add(method);
            SelectedMethod = method;
            SelectedNode = method;
        }

        /// <summary> 删除当前选中的方法（至少保留一个方法） </summary>
        public void DeleteMethod()
        {
            if (SelectedMethod == null || CurrentTask == null) return;
            if (CurrentTask.Methods.Count <= 1) return;
            CurrentTask.Methods.Remove(SelectedMethod);
            SelectedMethod = CurrentTask.Methods.FirstOrDefault();
            RenumberSteps();
        }

        /// <summary> 重命名当前选中的方法 </summary>
        public void RenameMethod(string newName)
        {
            if (SelectedMethod == null || string.IsNullOrEmpty(newName)) return;
            SelectedMethod.Name = newName;
        }

        /// <summary> 复制当前选中节点到剪贴板（深拷贝） </summary>
        public void CopyNode()
        {
            if (SelectedNode == null) return;
            _clipboard = DeepCopyNode(SelectedNode);
        }

        /// <summary> 粘贴剪贴板节点到当前选中节点下 </summary>
        public void PasteNode()
        {
            if (_clipboard == null || SelectedNode == null) return;
            var copy = DeepCopyNode(_clipboard);
            if (copy is ProcessMethod methodCopy && SelectedNode is TaskItem task)
            {
                methodCopy.Name = methodCopy.Name + "_Copy";
                ResetRuntimeState(methodCopy);
                task.Methods.Add(methodCopy);
                RenumberSteps();
            }
            else if (copy is ProcessStep stepCopy && SelectedNode is ProcessMethod method)
            {
                ResetRuntimeState(stepCopy);
                stepCopy.Seq = method.Steps.Count + 1;
                method.Steps.Add(stepCopy);
                RenumberSteps();
                CurrentTask.SyncStepsFromMethods();
                SelectedStep = stepCopy;
                SelectedNode = stepCopy;
            }
            else if (copy is ProcessStep branchStepCopy && SelectedNode is IfBranchGroup branch)
            {
                ResetRuntimeState(branchStepCopy);
                branch.Steps.Add(branchStepCopy);
                RenumberIfBranchSteps(branch);

                var loc = LocateIfBranch(branch);
                if (loc.HasValue)
                {
                    CurrentTask = loc.Value.Task;
                    SelectedMethod = loc.Value.Method;
                    loc.Value.Task.SyncStepsFromMethods();
                }

                SelectedStep = branchStepCopy;
                SelectedNode = branchStepCopy;
            }
            else if (copy is ProcessStep siblingStepCopy && SelectedNode is ProcessStep targetStep)
            {
                var loc = LocateStep(targetStep);
                if (!loc.HasValue) return;

                ResetRuntimeState(siblingStepCopy);
                if (loc.Value.IfBranch != null)
                {
                    var targetBranch = loc.Value.IfBranch;
                    var targetIndex = targetBranch.Steps.IndexOf(targetStep);
                    targetBranch.Steps.Insert(targetIndex < 0 ? targetBranch.Steps.Count : targetIndex + 1, siblingStepCopy);
                    RenumberIfBranchSteps(targetBranch);
                }
                else
                {
                    var targetMethod = loc.Value.Method;
                    var targetIndex = targetMethod.Steps.IndexOf(targetStep);
                    targetMethod.Steps.Insert(targetIndex < 0 ? targetMethod.Steps.Count : targetIndex + 1, siblingStepCopy);
                    RenumberSteps(loc.Value.Task);
                }

                CurrentTask = loc.Value.Task;
                SelectedMethod = loc.Value.Method;
                loc.Value.Task.SyncStepsFromMethods();
                SelectedStep = siblingStepCopy;
                SelectedNode = siblingStepCopy;
            }
        }

        /// <summary> 切换当前选中节点的启用/禁用状态 </summary>
        public void ToggleNodeEnabled()
        {
            switch (SelectedNode)
            {
                case TaskItem task:
                    task.IsEnabled = !task.IsEnabled;
                    break;
                case ProcessMethod method:
                    method.IsEnabled = !method.IsEnabled;
                    break;
                case ProcessStep step:
                    step.IsEnabled = !step.IsEnabled;
                    break;
            }
        }

        /// <summary> 设置当前选中节点的注释（Task/Method/Step 均支持） </summary>
        public void EditNodeComment(string comment)
        {
            switch (SelectedNode)
            {
                case TaskItem task:
                    task.Comment = comment;
                    break;
                case ProcessMethod method:
                    method.Comment = comment;
                    break;
                case ProcessStep step:
                    step.Comment = comment;
                    break;
            }
        }

        /// <summary> 设置当前任务的运行模式 </summary>
        public void SetTaskRunMode(TaskRunMode mode)
        {
            if (CurrentTask == null) return;
            CurrentTask.RunMode = mode;
        }

        /// <summary> 通过 JSON 序列化深拷贝节点（重置运行时状态） </summary>
        private object DeepCopyNode(object node)
        {
            if (node == null) return null;
            var json = JsonConvert.SerializeObject(node);
            var copy = JsonConvert.DeserializeObject(json, node.GetType());
            // 重置运行时状态
            if (copy is ProcessStep step)
            {
                ResetRuntimeState(step);
            }
            return copy;
        }

        private static void ResetRuntimeState(ProcessMethod method)
        {
            if (method?.Steps == null) return;
            foreach (var step in method.Steps)
                ResetRuntimeState(step);
        }

        private static void ResetRuntimeState(ProcessStep step)
        {
            if (step == null) return;

            step.IsCurrent = false;
            step.IsSingleExecuting = false;
            step.HasActiveAlarm = false;
            step.ErrorMessage = null;
            step.LastElapsedMs = 0;

            if (step.IfBranches == null) return;
            foreach (var branch in step.IfBranches)
            {
                if (branch?.Steps == null) continue;
                foreach (var subStep in branch.Steps)
                    ResetRuntimeState(subStep);
            }
        }

        // ========== 任务控制 ==========
        /// <summary> 校验整机处于 WAITRUN，否则记录日志并拒绝序列执行 </summary>
        private bool EnsureMachineReadyForSequence(string operationName)
        {
            if (_motionInterlock.CanExecuteManualMotion)
                return true;
            _logger.Warn(string.Format(
                _localization.GetResourceOrDefault("PSE_Log_OperationRejected", "[ProcessSequence] {0} 被拒绝: {1}"),
                operationName, _motionInterlock.GetBlockedMessage()));
            return false;
        }

        /// <summary> 指示是否有任务正在执行 </summary>
        public bool IsExecuting => _isExecuting;

        /// <summary> 获取第一个可用的 StationTaskBase 作为序列执行宿主（提供暂停/急停/单步保护等运行时基础设施） </summary>
        private StationTaskBase FindStationTask()
        {
            var firstStation = _stationRegistry.GetAllStations().OfType<StationTaskBase>().FirstOrDefault();
            if (firstStation == null)
                _logger.Warn(_localization.GetResourceOrDefault("PSE_Log_NoStationTaskRegistered", "[ProcessSequence] 未找到任何已注册的工站任务"));
            else
                _logger.Info(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_UseExecutionHost", "[ProcessSequence] 使用执行宿主: {0}"),
                    firstStation.TaskName));
            return firstStation;
        }

        /// <summary> 更新执行锁并通知 UI（方法级 Run 按钮依赖 IsExecuting） </summary>
        private void SetExecutingFlag(bool executing)
        {
            if (_isExecuting == executing) return;
            _isExecuting = executing;
            RaisePropertyChanged(nameof(IsExecuting));
        }

        /// <summary> 启动前确保工站未处于遗留 Running 状态，避免 RunCustomSequenceAsync 拒绝启动 </summary>
        private void EnsureStationReadyForSequence(StationTaskBase stationTask)
        {
            if (stationTask == null) return;
            if (stationTask.State == TaskState.Running)
            {
                _logger.Warn(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_StationRunningForceReset", "[ProcessSequence] 工站 [{0}] 状态遗留 Running，强制复位后启动"),
                    stationTask.TaskName));
                stationTask.StopAsync();
                stationTask.ResetMotionPause();
            }
        }

        /// <summary> 递归清除 DISPENSE 步骤运行时检查点（Stop / 新 Run 时调用） </summary>
        private static void ClearDispenseCheckpoints(IEnumerable<ProcessStep> steps)
        {
            if (steps == null) return;
            foreach (var step in steps)
            {
                step.DispenseDetail?.ClearExecutionCheckpoint();
                if (step.IfBranches == null) continue;
                foreach (var branch in step.IfBranches)
                    ClearDispenseCheckpoints(branch.Steps);
            }
        }

        /// <summary> 清除方法及其 IF 嵌套步骤中的 DISPENSE 检查点 </summary>
        private static void ClearDispenseCheckpoints(ProcessMethod method)
        {
            if (method?.Steps == null) return;
            ClearDispenseCheckpoints(method.Steps);
        }

        /// <summary>
        /// 启动当前任务：选中了特定方法时仅执行该方法的步骤，否则扁平化所有启用方法的步骤执行。
        /// 通过 IStationRegistry 获取目标工站，调用 RunCustomSequenceAsync 执行步骤序列。
        /// </summary>
        public void StartTask()
        {
            if (CurrentTask == null) return;
            if (!EnsureMachineReadyForSequence(_localization.GetResourceOrDefault("PSE_Log_Operation_StartTask", "启动任务")))
                return;
            // 被动任务不可直接启动
            if (CurrentTask.RunMode == TaskRunMode.Passive)
            {
                _logger.Warn(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_PassiveTaskCannotStart", "[ProcessSequence] 任务 {0} 为被动模式，不可直接启动，请通过调用任务动作触发"),
                    CurrentTask.Name));
                return;
            }
            if (_isExecuting)
            {
                _logger.Warn(_localization.GetResourceOrDefault("PSE_Log_AlreadyExecutingRejectStart", "[ProcessSequence] 已有任务正在执行，拒绝启动新任务"));
                return;
            }
            var stationTask = FindStationTask();
            if (stationTask == null) return;

            // 根据选中节点确定执行范围：选中特定方法时仅执行该方法，否则执行所有启用方法
            ObservableCollection<ProcessStep> steps;
            string executionLabel;
            if (SelectedMethod != null)
            {
                if (!SelectedMethod.IsEnabled)
                {
                    _logger.Warn(string.Format(
                        _localization.GetResourceOrDefault("PSE_Log_SelectedMethodDisabled", "[ProcessSequence] 选中的方法 [{0}] 已禁用，无法执行"),
                        SelectedMethod.Name));
                    return;
                }
                steps = FlattenMethodSteps(SelectedMethod);
                executionLabel = $"{CurrentTask.Name} > 方法[{SelectedMethod.Name}]";
            }
            else
            {
                steps = FlattenEnabledSteps(CurrentTask);
                executionLabel = CurrentTask.Name;
            }

            if (steps == null || steps.Count == 0)
            {
                _logger.Warn(_localization.GetResourceOrDefault("PSE_Log_NoExecutableSteps", "[ProcessSequence] 没有可执行步骤，无法启动"));
                return;
            }
            _executionCts = new CancellationTokenSource();
            EnsureStationReadyForSequence(stationTask);
            SetExecutingFlag(true);
            _activeStationTask = stationTask;
            CurrentTask.Status = TaskItem.TaskStatusEnum.Running;
            // 启动时重置所有工站的暂停信号：StopTask 会取消所有工站的 _pauseCts，
            // 跨工站执行时目标工站的 PauseAwareToken 也需要处于未取消状态
            foreach (var station in _stationRegistry.GetAllStations().OfType<StationTaskBase>())
                station.ResetMotionPause();
            // 启动时清除上次运行遗留的报警标记与 DISPENSE 检查点
            foreach (var step in steps)
                step.HasActiveAlarm = false;
            ClearDispenseCheckpoints(steps);
            _logger.Info(string.Format(
                _localization.GetResourceOrDefault("PSE_Log_SequenceStarted", "[ProcessSequence] 启动: {0}，共 {1} 个步骤，目标工站: {2}"),
                executionLabel, steps.Count, stationTask.TaskName));

            // 异步执行步骤序列
            _ = ExecuteSequenceAsync(stationTask, steps, _executionCts.Token);
        }

        /// <summary>
        /// 将任务的启用方法的启用步骤扁平化为执行列表。
        /// 禁用的方法/步骤被排除，RUNTASK 步骤保留（运行时由 IRunTaskExecutor 处理）。
        /// </summary>
        private ObservableCollection<ProcessStep> FlattenEnabledSteps(TaskItem task)
        {
            var list = new ObservableCollection<ProcessStep>();
            if (task?.Methods == null) return list;
            foreach (var method in task.Methods)
            {
                if (!method.IsEnabled) continue;
                foreach (var step in method.Steps)
                {
                    if (!step.IsEnabled) continue;
                    list.Add(step);
                }
            }
            return list;
        }

        /// <summary>
        /// 将指定方法的启用步骤扁平化为执行列表（用于选中特定方法时仅执行该方法）。
        /// </summary>
        private ObservableCollection<ProcessStep> FlattenMethodSteps(ProcessMethod method)
        {
            var list = new ObservableCollection<ProcessStep>();
            if (method == null) return list;
            foreach (var step in method.Steps)
            {
                if (!step.IsEnabled) continue;
                list.Add(step);
            }
            return list;
        }

        /// <summary> 异步执行步骤序列，完成后更新状态 </summary>
        private async Task ExecuteSequenceAsync(StationTaskBase stationTask, ObservableCollection<ProcessStep> steps, CancellationToken token)
        {
            try
            {
                await stationTask.RunCustomSequenceAsync(async (ct) =>
                {
                    var actions = CreateStepActions();
                    var alarmService = (IAlarmService)_containerProvider.Resolve(typeof(IAlarmService));
                    // 获取公式求值器实例，用于条件分支表达式的计算
                    var formulaEvaluator = (IFormulaEvaluator)_containerProvider.Resolve(typeof(IFormulaEvaluator));
                    var executor = new ProcessStepExecutor(stationTask, stationTask.TaskLogger, actions, alarmService, formulaEvaluator, _recipePoolService, this);
                    // 设置调用栈起点：压入当前任务名，用于 RUNTASK 步骤的循环引用检测
                    executor.CallStack = new Stack<string>();
                    executor.CallStack.Push(CurrentTask.Name);

                    // 单步模式：设置门控回调，每步执行后等待用户点击“下一步”
                    if (_isSingleStepMode)
                    {
                        executor.StepGate = async (gateToken) =>
                        {
                            _stepNextTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                            using (gateToken.Register(() => _stepNextTcs.TrySetCanceled()))
                            {
                                await _stepNextTcs.Task;
                            }
                        };
                    }

                    WireExecutorStepSync(executor);

                    await executor.ExecuteAsync(steps, ct);
                }, token);
            }
            catch (InvalidOperationException ex)
            {
                _logger.Error(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_StartFailed", "[ProcessSequence] 启动失败: {0}"),
                    ex.Message));
            }
            finally
            {
                SetExecutingFlag(false);
                _activeStationTask = null;
                _stepNextTcs = null;
                _executionCts?.Dispose();
                _executionCts = null;
                if (CurrentTask != null)
                {
                    CurrentTask.Status = TaskItem.TaskStatusEnum.Idle;
                    // 任务结束后重置步骤高亮到执行列表的第一步
                    ResetStepHighlight(steps);
                }
                _logger.Info(_localization.GetResourceOrDefault("PSE_Log_TaskExecutionEnded", "[ProcessSequence] 任务执行结束"));
            }
        }

        /// <summary> 从DI容器解析所有已注册的步骤动作实现 </summary>
        private List<IProcessStepAction> CreateStepActions()
        {
            return _containerProvider.Resolve(typeof(IEnumerable<IProcessStepAction>)) as List<IProcessStepAction>
                ?? ((IEnumerable<IProcessStepAction>)_containerProvider.Resolve(typeof(IEnumerable<IProcessStepAction>))).ToList();
        }

        /// <summary> 停止当前任务，遍历所有工站停止运动中的轴（安全关键） </summary>
        public void StopTask()
        {
            if (CurrentTask == null) return;
            if (_isExecuting)
            {
                _executionCts?.Cancel();
                // 解除单步模式等待，避免执行线程永久阻塞
                _stepNextTcs?.TrySetCanceled();
                // 遍历所有工站调用 StopAsync（无State守卫）：停止所有轴 + 取消 _cts/_pauseCts
                foreach (var station in _stationRegistry.GetAllStations().OfType<StationTaskBase>())
                    station.StopAsync();
            }
            CurrentTask.Status = TaskItem.TaskStatusEnum.Stopped;
            ResetStepHighlight();
            _logger.Info(_localization.GetResourceOrDefault("PSE_Log_TaskStopped", "[ProcessSequence] 任务已停止"));
        }

        /// <summary> 暂停当前任务，遍历所有工站停止运动轴并取消暂停令牌（安全关键） </summary>
        public void PauseTask()
        {
            if (!_isExecuting || CurrentTask == null) return;
            if (CurrentTask.Status != TaskItem.TaskStatusEnum.Running) return;
            // 暂停主工站（State: Running → Paused + CancelMotionPause）
            _activeStationTask?.PauseAsync();
            // 遍历所有工站调用 CancelMotionPause（无State守卫）
            // 确保跨工站运动轴的 _pauseCts 被取消，WaitForDone 立即感知暂停信号
            foreach (var station in _stationRegistry.GetAllStations().OfType<StationTaskBase>())
                station.CancelMotionPause();
            CurrentTask.Status = TaskItem.TaskStatusEnum.Paused;
            _logger.Info(_localization.GetResourceOrDefault("PSE_Log_TaskPaused", "[ProcessSequence] 任务已暂停"));
        }

        /// <summary> 恢复当前任务，遍历所有工站重置暂停令牌（跨工站轴恢复通过 RunStep 重试自动完成） </summary>
        public void ResumeTask()
        {
            if (!_isExecuting || CurrentTask == null) return;
            if (CurrentTask.Status != TaskItem.TaskStatusEnum.Paused) return;
            // 恢复主工站（State: Paused → Running + 重建 _pauseCts + 解除暂停阻塞）
            _activeStationTask?.ResumeAsync();
            // 遍历所有工站调用 ResetMotionPause（无State守卫）
            // 确保跨工站工站的 _pauseCts 被重建，ExecuteMoveAsync 重试时 PauseAwareToken 有效
            foreach (var station in _stationRegistry.GetAllStations().OfType<StationTaskBase>())
                station.ResetMotionPause();
            CurrentTask.Status = TaskItem.TaskStatusEnum.Running;
            _logger.Info(_localization.GetResourceOrDefault("PSE_Log_TaskResumed", "[ProcessSequence] 任务已恢复"));
        }

        // ========== 方法级控制（独立执行单个方法） ==========

        /// <summary> 是否有方法正在执行（与任务级执行共享 _isExecuting 互斥锁） </summary>
        public bool IsMethodExecuting => _isMethodExecuting;

        /// <summary> 当前正在执行的方法 </summary>
        public ProcessMethod ExecutingMethod => _executingMethod;

        /// <summary>
        /// 启动指定方法的独立执行。
        /// 仅执行该方法的启用步骤，与任务级执行互斥（共享 _isExecuting 锁，避免工站运动轴冲突）。
        /// 安全策略：启动前重置所有工站暂停信号，清除步骤报警标记，设置方法状态为 Running。
        /// </summary>
        /// <param name="method">要执行的方法</param>
        public void StartMethod(ProcessMethod method)
        {
            if (method == null)
            {
                _logger.Warn(_localization.GetResourceOrDefault("PSE_Log_StartMethodFailedNull", "[ProcessSequence] 启动方法失败：方法为空"));
                return;
            }
            if (!EnsureMachineReadyForSequence(string.Format(
                _localization.GetResourceOrDefault("PSE_Log_Operation_StartMethod", "启动方法 [{0}]"),
                method.Name)))
                return;
            // 互斥检查：任务级或方法级执行正在进行时拒绝启动
            if (_isExecuting)
            {
                _logger.Warn(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_RejectStartMethodBusy", "[ProcessSequence] 已有任务/方法正在执行，拒绝启动方法 [{0}]"),
                    method.Name));
                return;
            }
            if (!method.IsEnabled)
            {
                _logger.Warn(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_MethodDisabled", "[ProcessSequence] 方法 [{0}] 已禁用，无法执行"),
                    method.Name));
                return;
            }
            // Stopped 为停止后遗留状态，启动前规范为 Idle
            if (method.Status == TaskItem.TaskStatusEnum.Stopped)
                method.Status = TaskItem.TaskStatusEnum.Idle;
            var stationTask = FindStationTask();
            if (stationTask == null) return;
            EnsureStationReadyForSequence(stationTask);

            var steps = FlattenMethodSteps(method);
            if (steps == null || steps.Count == 0)
            {
                _logger.Warn(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_MethodNoSteps", "[ProcessSequence] 方法 [{0}] 没有可执行步骤，无法启动"),
                    method.Name));
                return;
            }

            _executionCts = new CancellationTokenSource();
            EnsureStationReadyForSequence(stationTask);
            SetExecutingFlag(true);
            _isMethodExecuting = true;
            _executingMethod = method;
            _methodExecutionSteps = steps;
            _activeStationTask = stationTask;
            method.Status = TaskItem.TaskStatusEnum.Running;
            // 同步任务状态为 Running（便于 UI 统一显示）
            if (CurrentTask != null)
                CurrentTask.Status = TaskItem.TaskStatusEnum.Running;
            // 启动时重置所有工站的暂停信号
            foreach (var station in _stationRegistry.GetAllStations().OfType<StationTaskBase>())
                station.ResetMotionPause();
            // 清除上次运行遗留的报警标记与 DISPENSE 检查点
            foreach (var step in steps)
                step.HasActiveAlarm = false;
            ClearDispenseCheckpoints(method);
            _logger.Info(string.Format(
                _localization.GetResourceOrDefault("PSE_Log_MethodStarted", "[ProcessSequence] 启动方法: [{0}]，共 {1} 个步骤，目标工站: {2}"),
                method.Name, steps.Count, stationTask.TaskName));

            // 异步执行方法步骤序列
            _ = ExecuteMethodAsync(stationTask, method, steps, _executionCts.Token);
        }

        /// <summary> 异步执行方法步骤序列，完成后更新方法状态 </summary>
        private async Task ExecuteMethodAsync(StationTaskBase stationTask, ProcessMethod method, ObservableCollection<ProcessStep> steps, CancellationToken token)
        {
            try
            {
                await stationTask.RunCustomSequenceAsync(async (ct) =>
                {
                    var actions = CreateStepActions();
                    var alarmService = (IAlarmService)_containerProvider.Resolve(typeof(IAlarmService));
                    var formulaEvaluator = (IFormulaEvaluator)_containerProvider.Resolve(typeof(IFormulaEvaluator));
                    var executor = new ProcessStepExecutor(stationTask, stationTask.TaskLogger, actions, alarmService, formulaEvaluator, _recipePoolService, this);
                    executor.CallStack = new Stack<string>();
                    // 方法级执行调用栈起点：压入方法名，便于 RUNTASK 循环检测
                    executor.CallStack.Push(method.Name);

                    // 单步模式支持
                    if (_isSingleStepMode)
                    {
                        executor.StepGate = async (gateToken) =>
                        {
                            _stepNextTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                            using (gateToken.Register(() => _stepNextTcs.TrySetCanceled()))
                            {
                                await _stepNextTcs.Task;
                            }
                        };
                    }

                    WireExecutorStepSync(executor);

                    await executor.ExecuteAsync(steps, ct);
                }, token);
            }
            catch (InvalidOperationException ex)
            {
                _logger.Error(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_StartMethodFailed", "[ProcessSequence] 启动方法失败: {0}"),
                    ex.Message));
            }
            finally
            {
                SetExecutingFlag(false);
                _isMethodExecuting = false;
                _executingMethod = null;
                _activeStationTask = null;
                _stepNextTcs = null;
                _methodExecutionSteps = null;
                _executionCts?.Dispose();
                _executionCts = null;
                method.Status = TaskItem.TaskStatusEnum.Idle;
                if (CurrentTask != null)
                    CurrentTask.Status = TaskItem.TaskStatusEnum.Idle;
                // 方法结束后重置步骤高亮到执行列表的第一步
                ResetStepHighlight(steps);
                _logger.Info(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_MethodEnded", "[ProcessSequence] 方法 [{0}] 执行结束"),
                    method.Name));
            }
        }

        /// <summary> 暂停当前正在执行的方法（安全关键：停止运动轴并取消暂停令牌） </summary>
        public void PauseMethod()
        {
            if (!_isMethodExecuting || _executingMethod == null) return;
            if (_executingMethod.Status != TaskItem.TaskStatusEnum.Running) return;
            // 暂停主工站
            _activeStationTask?.PauseAsync();
            // 遍历所有工站调用 CancelMotionPause，确保跨工站运动轴立即响应暂停
            foreach (var station in _stationRegistry.GetAllStations().OfType<StationTaskBase>())
                station.CancelMotionPause();
            _executingMethod.Status = TaskItem.TaskStatusEnum.Paused;
            if (CurrentTask != null)
                CurrentTask.Status = TaskItem.TaskStatusEnum.Paused;
            _logger.Info(string.Format(
                _localization.GetResourceOrDefault("PSE_Log_MethodPaused", "[ProcessSequence] 方法 [{0}] 已暂停"),
                _executingMethod.Name));
        }

        /// <summary> 恢复当前被暂停的方法（重建暂停令牌，跨工站轴通过重试自动恢复） </summary>
        public void ResumeMethod()
        {
            if (!_isMethodExecuting || _executingMethod == null) return;
            if (_executingMethod.Status != TaskItem.TaskStatusEnum.Paused) return;
            // 恢复主工站
            _activeStationTask?.ResumeAsync();
            // 遍历所有工站调用 ResetMotionPause，确保跨工站 _pauseCts 被重建
            foreach (var station in _stationRegistry.GetAllStations().OfType<StationTaskBase>())
                station.ResetMotionPause();
            _executingMethod.Status = TaskItem.TaskStatusEnum.Running;
            if (CurrentTask != null)
                CurrentTask.Status = TaskItem.TaskStatusEnum.Running;
            _logger.Info(string.Format(
                _localization.GetResourceOrDefault("PSE_Log_MethodResumed", "[ProcessSequence] 方法 [{0}] 已恢复"),
                _executingMethod.Name));
        }

        /// <summary>
        /// 停止指定方法（安全关键：取消执行令牌并停止所有工站运动轴）。
        /// 若方法未在执行但 Status 仍为 Running/Paused/Stopped（如重启后遗留），则重置为 Idle 以便再次 Run。
        /// </summary>
        /// <param name="method">要停止或重置的方法</param>
        public void StopMethod(ProcessMethod method)
        {
            if (method == null) return;

            // 正在执行：取消令牌并停止运动轴，最终由 ExecuteMethodAsync 的 finally 重置为 Idle
            if (_isMethodExecuting && _executingMethod == method)
            {
                _executionCts?.Cancel();
                // 解除单步模式等待，避免执行线程永久阻塞
                _stepNextTcs?.TrySetCanceled();
                // 遍历所有工站调用 StopAsync（无State守卫）：停止所有轴 + 取消 _cts/_pauseCts
                foreach (var station in _stationRegistry.GetAllStations().OfType<StationTaskBase>())
                {
                    station.StopAsync();
                    station.ResetMotionPause();
                }
                ClearDispenseCheckpoints(method);
                ResetStepHighlight(_methodExecutionSteps);
                _logger.Info(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_MethodStopped", "[ProcessSequence] 方法 [{0}] 已停止"),
                    method.Name));
                return;
            }

            // 非执行中的遗留状态：重置为 Idle，解除 Run 按钮不可用
            if (method.Status != TaskItem.TaskStatusEnum.Idle)
            {
                ResetMethodRuntimeState(method);
                _logger.Info(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_MethodStateResetIdle", "[ProcessSequence] 方法 [{0}] 运行状态已重置为 Idle"),
                    method.Name));
            }
        }

        /// <summary> 重置单个方法的运行时状态（Status/步骤高亮），加载序列或 Stop 恢复时使用 </summary>
        private void ResetMethodRuntimeState(ProcessMethod method)
        {
            if (method == null) return;
            method.Status = TaskItem.TaskStatusEnum.Idle;
            method.LastElapsedMs = 0;
            ClearDispenseCheckpoints(method);
            var steps = FlattenMethodSteps(method);
            if (steps.Count > 0)
            {
                ClearStepHighlightsRecursive(steps);
                // 含 IF 嵌套子步骤的完整清除
                if (method.Steps != null)
                    ClearStepHighlightsRecursive(method.Steps);
                steps[0].IsCurrent = true;
            }
            if (CurrentTask != null && !_isExecuting && !_isMethodExecuting)
                CurrentTask.Status = TaskItem.TaskStatusEnum.Idle;
        }

        /// <summary> 是否启用单步模式（每步执行后等待用户确认再继续） </summary>
        public bool IsSingleStepMode
        {
            get => _isSingleStepMode;
            set
            {
                if (_isSingleStepMode != value)
                {
                    _isSingleStepMode = value;
                    RaisePropertyChanged();
                    _logger.Info(_localization.GetResourceOrDefault(
                        value ? "PSE_Log_SingleStepEnabled" : "PSE_Log_SingleStepDisabled",
                        value ? "[ProcessSequence] 单步模式: 已启用" : "[ProcessSequence] 单步模式: 已关闭"));
                }
            }
        }

        /// <summary> 单步模式下触发下一步执行（解除 StepGate 等待） </summary>
        public void StepNext()
        {
            var tcs = _stepNextTcs;
            if (tcs != null && !tcs.Task.IsCompleted)
            {
                tcs.TrySetResult(true);
                _logger.Info(_localization.GetResourceOrDefault("PSE_Log_SingleStepNext", "[ProcessSequence] 单步模式：用户确认下一步"));
            }
        }

        /// <summary>
        /// 绑定执行器步骤变更回调：同步 SelectedMethod/SelectedStep，确保方法4 IF/ELSE 子步骤执行时 UI 上下文正确。
        /// </summary>
        private void WireExecutorStepSync(ProcessStepExecutor executor)
        {
            executor.ExecutingStepChanged = step =>
            {
                if (step == null) return;
                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        SelectedStep = step;
                        var loc = LocateStep(step);
                        if (loc.HasValue)
                        {
                            SelectedMethod = loc.Value.Method;
                            if (CurrentTask != loc.Value.Task)
                                CurrentTask = loc.Value.Task;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(string.Format(
                            _localization.GetResourceOrDefault("PSE_Log_SyncStepUiFailed", "[ProcessSequence] 同步执行步骤 UI 失败: {0}"),
                            ex.Message));
                    }
                }));
            };
        }

        /// <summary> 递归清除步骤及 IF 嵌套子步骤的 IsCurrent 高亮 </summary>
        private static void ClearStepHighlightsRecursive(IEnumerable<ProcessStep> steps)
        {
            if (steps == null) return;
            foreach (var step in steps)
            {
                step.IsCurrent = false;
                if (step.IfBranches == null) continue;
                foreach (var branch in step.IfBranches)
                {
                    if (branch.Steps != null)
                        ClearStepHighlightsRecursive(branch.Steps);
                }
            }
        }

        /// <summary> 重置步骤高亮到执行列表的第一步（不清除 HasActiveAlarm，报警标记在下次启动时清除） </summary>
        private void ResetStepHighlight(ObservableCollection<ProcessStep> executedSteps = null)
        {
            _logger.Info(_localization.GetResourceOrDefault("PSE_Log_ResetStepHighlightCalled", "[ProcessSequenceService] ResetStepHighlight 被调用"));

            if (CurrentTask?.Methods != null)
            {
                foreach (var method in CurrentTask.Methods)
                {
                    if (method.Steps != null)
                        ClearStepHighlightsRecursive(method.Steps);
                }
            }
            else if (CurrentTask?.Steps != null)
            {
                ClearStepHighlightsRecursive(CurrentTask.Steps);
            }

            var first = executedSteps?.FirstOrDefault()
                        ?? CurrentTask?.Methods?.FirstOrDefault(m => m.Steps?.Count > 0)?.Steps?.FirstOrDefault()
                        ?? CurrentTask?.Steps?.FirstOrDefault();
            if (first != null)
                first.IsCurrent = true;

            _logger.Info(_localization.GetResourceOrDefault("PSE_Log_ResetStepHighlightDone", "[ProcessSequenceService] ResetStepHighlight 完成"));
        }

        /// <summary> 单独执行指定步骤（用于步骤编辑器中的调试运行） </summary>
        public async Task RunSingleStepAsync(ProcessStep step)
        {
            if (step == null || _isExecuting) return;
            if (!EnsureMachineReadyForSequence(_localization.GetResourceOrDefault("PSE_Log_Operation_SingleStep", "单步执行")))
                return;

            var stationTask = _activeStationTask ?? FindStationTask();
            if (stationTask == null)
            {
                _logger.Warn(_localization.GetResourceOrDefault("PSE_Log_NoStationForSingleStep", "[ProcessSequenceService] 未找到可用的工站任务，无法单独执行步骤"));
                return;
            }

            var actions = CreateStepActions();
            var alarmService = _containerProvider.Resolve(typeof(AlarmModule.Interfaces.IAlarmService)) as AlarmModule.Interfaces.IAlarmService;
            var formulaEvaluator = _containerProvider.Resolve(typeof(Core.Abstraction.IFormulaEvaluator)) as Core.Abstraction.IFormulaEvaluator;
            var executor = new ProcessStepExecutor(stationTask, stationTask.TaskLogger, actions, alarmService, formulaEvaluator, _recipePoolService);

            try
            {
                SetExecutingFlag(true);
                CurrentTask.Status = TaskItem.TaskStatusEnum.Running;
                await executor.ExecuteSingleStepAsync(step, CancellationToken.None);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.Error(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_RunSingleStepError", "[ProcessSequenceService] 单独执行步骤异常: {0}"),
                    ex.Message));
            }
            finally
            {
                SetExecutingFlag(false);
                CurrentTask.Status = TaskItem.TaskStatusEnum.Idle;
            }
        }

        // ========== IRunTaskExecutor 实现（被动任务调用） ==========

        /// <summary>
        /// 执行指定名称的被动任务（IRunTaskExecutor 实现）。
        /// 在当前工站上下文中按方法顺序执行目标 Passive 任务的启用方法。
        /// 通过 callStack 检测循环引用，检测到循环时触发报警并终止。
        /// </summary>
        public async Task ExecutePassiveTaskAsync(string targetTaskName, StationTaskBase callerTask, Stack<string> callStack, CancellationToken token)
        {
            if (string.IsNullOrEmpty(targetTaskName))
            {
                _logger.Warn(_localization.GetResourceOrDefault("PSE_Log_RunTaskNameEmpty", "[ProcessSequence] RUNTASK: 目标任务名称为空"));
                return;
            }
            // 循环引用检测
            if (callStack.Contains(targetTaskName))
            {
                var chain = string.Join(" → ", callStack.Reverse().Concat(new[] { targetTaskName }));
                _logger.Error(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_CircularCall", "[ProcessSequence] 检测到循环调用: {0}"),
                    chain));
                var alarmService = (IAlarmService)_containerProvider.Resolve(typeof(IAlarmService));
                var alarmMsg = string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_CircularCallAlarm", "循环调用: {0}"),
                    chain);
                await alarmService.TriggerAlarmAsync("PSE_CIRCULAR_CALL", AlarmLevel.Serious, alarmMsg);
                throw new InvalidOperationException(alarmMsg);
            }
            // 查找目标任务
            var targetTask = Tasks.FirstOrDefault(t => t.Name == targetTaskName && t.RunMode == TaskRunMode.Passive);
            if (targetTask == null)
            {
                _logger.Warn(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_RunTaskNotFound", "[ProcessSequence] RUNTASK: 未找到被动任务 '{0}'"),
                    targetTaskName));
                return;
            }
            if (!targetTask.IsEnabled)
            {
                _logger.Info(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_SkippedDisabledTask", "[ProcessSequence] 跳过禁用任务: {0}"),
                    targetTaskName));
                return;
            }
            var steps = FlattenEnabledSteps(targetTask);
            if (steps.Count == 0)
            {
                _logger.Warn(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_RunTaskNoSteps", "[ProcessSequence] RUNTASK: 目标任务 '{0}' 没有可执行步骤"),
                    targetTaskName));
                return;
            }
            // 压入调用栈并递归执行
            callStack.Push(targetTaskName);
            try
            {
                var actions = CreateStepActions();
                var alarmService = (IAlarmService)_containerProvider.Resolve(typeof(IAlarmService));
                var formulaEvaluator = (IFormulaEvaluator)_containerProvider.Resolve(typeof(IFormulaEvaluator));
                var executor = new ProcessStepExecutor(callerTask, callerTask.TaskLogger, actions, alarmService, formulaEvaluator, _recipePoolService, this);
                executor.CallStack = callStack;
                _logger.Info(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_RunTaskStarted", "[ProcessSequence] RUNTASK: 开始执行被动任务 '{0}'，共 {1} 步"),
                    targetTaskName, steps.Count));
                await executor.ExecuteAsync(steps, token);
                _logger.Info(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_RunTaskCompleted", "[ProcessSequence] RUNTASK: 被动任务 '{0}' 执行完成"),
                    targetTaskName));
            }
            finally
            {
                callStack.Pop();
            }
        }

        // ========== 验证 ==========
        public ObservableCollection<ValidationItem> Validate()
        {
            var results = new ObservableCollection<ValidationItem>();
            if (CurrentTask == null || CurrentTask.Steps.Count == 0)
            {
                results.Add(new ValidationItem("No steps defined", false));
                return results;
            }
            bool startsHome = CurrentTask.Steps.First().Step == StepType.GOTO && string.Equals(CurrentTask.Steps.First().SiteFeature, "HOME", StringComparison.OrdinalIgnoreCase);
            results.Add(new ValidationItem("Starts with GOTO → HOME", startsHome));
            bool endsHome = CurrentTask.Steps.Last().Step == StepType.GOTO && string.Equals(CurrentTask.Steps.Last().SiteFeature, "HOME", StringComparison.OrdinalIgnoreCase);
            results.Add(new ValidationItem("Ends with GOTO → HOME", endsHome));
            int pickCount = CurrentTask.Steps.Count(s => s.Step == StepType.PICK);
            results.Add(new ValidationItem("Exactly one PICK step", pickCount == 1));
            int releaseCount = CurrentTask.Steps.Count(s => s.Step == StepType.RELEASE);
            results.Add(new ValidationItem("Exactly one RELEASE step", releaseCount == 1));
            return results;
        }

        // ========== JSON 文件操作（保存/加载所有任务） ==========

        /// <summary>
        /// 保存工序序列到指定路径，并在 ExtensionData 中记录该路径
        /// </summary>
        public Task SaveSequenceToPathAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            var allTasks = Tasks.Select(t => new SequenceTaskData
            {
                Name = t.Name,
                IsDefault = t.IsDefault,
                Status = t.Status,
                RunMode = t.RunMode,
                IsEnabled = t.IsEnabled,
                Comment = t.Comment,
                IsExpanded = t.IsExpanded,
                Methods = t.Methods?.ToList(),
                Steps = null  // 新格式不序列化 Steps
            }).ToList();
            var data = new SequenceData
            {
                Tasks = allTasks,
                GripperManualOperationSpeed = _gripperService.ManualOperationSpeed
            };
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            var identifier = Path.GetFileNameWithoutExtension(filePath);
            _parameterStorage.Save(identifier, data, dir);
            RecordRecentFile(filePath);
            CurrentFilePath = filePath;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 自动保存工序序列到默认目录，文件名格式：ProcessSequences_yyyyMMdd_HHmmss.json
        /// 保存后按最大文件数量清理旧文件（由 IConfigFileRetentionService 统一管理）
        /// </summary>
        /// <param name="stationId">工站标识（保留参数兼容性，不再用于文件名）</param>
        public Task SaveSequenceAsync(string stationId = null)
        {
            // 生成时间戳：yyyyMMdd_HHmmss
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"ProcessSequences_{timestamp}.json";
            // 确保目录存在
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ProcessSequenceDirectory);
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, fileName);
            var result = SaveSequenceToPathAsync(filePath);
            // 后台按数量清理旧文件，避免阻塞UI
            _ = _configRetentionService.CleanupFolderByCountAsync("ProcessSequences", "ProcessSequences_*.json", filePath);
            return result;
        }

        /// <summary>
        /// 将当前工序序列文件路径保存到配方池 ExtensionData（参考 CadAlignment/VisionCapture 模式）
        /// </summary>
        private async Task SaveCurrentFileToRecipePoolAsync(string filePath)
        {
            try
            {
                var poolName = _recipePoolService.CurrentPoolName ?? "Default";
                await _recipePoolService.SetExtensionDataAsync(poolName, ProcessSequenceCurrentFileKey,
                    new ProcessSequenceFileRecord { FilePath = filePath });
                _logger.Info(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_SequencePathSavedToRecipePool",
                        "[ProcessSequence] 已保存工序序列路径到配方池: {0}"),
                    filePath));
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_SaveSequencePathToRecipePoolFailed",
                        "[ProcessSequence] 保存工序序列路径到配方池失败: {0}"),
                    ex.Message));
            }
        }

        /// <summary>
        /// 从配方池 ExtensionData 读取当前配方池关联的工序序列文件路径
        /// </summary>
        private async Task<string> GetLastSequencePathFromRecipePoolAsync()
        {
            try
            {
                var poolName = _recipePoolService.CurrentPoolName ?? "Default";
                var extData = await _recipePoolService.GetExtensionDataAsync<ProcessSequenceFileRecord>(
                    poolName, ProcessSequenceCurrentFileKey);
                if (extData?.FilePath != null && File.Exists(extData.FilePath))
                {
                    _logger.Info(string.Format(
                        _localization.GetResourceOrDefault("PSE_Log_ReadLastPathFromRecipePool",
                            "[ProcessSequence] 从配方池读取上次工序序列路径: {0}"),
                        extData.FilePath));
                    return extData.FilePath;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_ReadLastPathFromRecipePoolFailed",
                        "[ProcessSequence] 从配方池读取工序序列路径失败: {0}"),
                    ex.Message));
            }
            return null;
        }

        /// <summary>
        /// 将上次保存路径记录到 IAppSettingService.ExtensionData
        /// </summary>
        private void RecordLastSequencePath(string filePath)
        {
            try
            {
                // 使用 JsonSerializer.Serialize 正确转义路径中的反斜杠
                var json = System.Text.Json.JsonSerializer.Serialize(filePath);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                _appSettingService.Settings.ExtensionData[LastPathKey] = doc.RootElement.Clone();
                _appSettingService.Save();
                _logger.Info(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_SequencePathSaved", "[ProcessSequence] 已保存工序序列路径: {0}"),
                    filePath));
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_SaveSequencePathFailed", "[ProcessSequence] 保存工序序列路径失败: {0}"),
                    ex.Message));
            }
        }

        /// <summary>
        /// 从 ExtensionData 中读取上次保存的路径
        /// </summary>
        private string GetLastSequencePath()
        {
            try
            {
                if (_appSettingService.Settings.ExtensionData.TryGetValue(LastPathKey, out var element)
                    && element.ValueKind == JsonValueKind.String)
                {
                    var path = element.GetString();
                    _logger.Info(string.Format(
                        _localization.GetResourceOrDefault("PSE_Log_ReadLastPath", "[ProcessSequence] 从配置读取上次工序序列路径: {0}"),
                        path));
                    return path;
                }
                _logger.Info(_localization.GetResourceOrDefault("PSE_Log_LastPathNotFound", "[ProcessSequence] 配置中未找到上次工序序列路径 (ExtensionData 中无键或类型非字符串)"));
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_ReadLastPathFailed", "[ProcessSequence] 读取上次工序序列路径失败: {0}"),
                    ex.Message));
            }
            return null;
        }

        public Task LoadSequenceFromPathAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            var dir = Path.GetDirectoryName(filePath);
            var identifier = Path.GetFileNameWithoutExtension(filePath);
            var data = _parameterStorage.Load<SequenceData>(identifier, dir);
            if (data != null)
            {
                // 从工序序列 JSON 恢复电爪速度，与操作面板及自动流程共用
                ApplyGripperSpeedFromSequence(data.GripperManualOperationSpeed);

                if (data.Tasks != null && data.Tasks.Any())
                {
                Tasks.Clear();
                foreach (var taskData in data.Tasks)
                {
                    var task = new TaskItem(taskData.Name)
                    {
                        IsDefault = taskData.IsDefault,
                        Status = TaskItem.TaskStatusEnum.Idle,
                        RunMode = taskData.RunMode,
                        IsEnabled = taskData.IsEnabled,
                        // 恢复任务级注释与展开状态（旧文件无此字段时取默认值）
                        Comment = taskData.Comment,
                        IsExpanded = taskData.IsExpanded
                    };
                    // 加载 Methods（新格式），或从旧格式 Steps 迁移
                    if (taskData.Methods != null && taskData.Methods.Count > 0)
                    {
                        task.Methods.Clear();
                        foreach (var m in taskData.Methods)
                            task.Methods.Add(m);
                    }
                    else if (taskData.Steps != null && taskData.Steps.Count > 0)
                    {
                        // 旧格式迁移：将 Steps 包装为单个默认方法
                        task.Methods.Clear();
                        task.Methods.Add(new ProcessMethod(_localization.GetResourceOrDefault("PSE_DefaultMethodName", "默认方法"), taskData.Steps));
                    }
                    // 重置运行时状态（方法 Status 可能来自旧版 JSON 持久化，须强制 Idle）
                    foreach (var method in task.Methods)
                    {
                        method.Status = TaskItem.TaskStatusEnum.Idle;
                        method.LastElapsedMs = 0;
                        foreach (var step in method.Steps)
                        {
                            // 完整重置步骤运行时状态（含 IF 分支子步骤的 IsCurrent/LastElapsedMs 等）
                            ResetRuntimeState(step);
                            // 确保结构初始化（AlarmConfig/BranchConfig/IfDetail/IfBranches）
                            step.EnsureAlarmConfigInitialized();
                        }
                    }
                    task.SyncStepsFromMethods();
                    if (task.Steps.Count > 0)
                        task.Steps[0].IsCurrent = true;
                    Tasks.Add(task);
                }
                // 选中第一个非默认的任务或第一个任务
                CurrentTask = Tasks.FirstOrDefault(t => t.IsDefault) ?? Tasks.First();
                SelectedStep = null;
                CurrentFilePath = filePath;
                RecordRecentFile(filePath);
                }
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 将工序序列文件中的电爪速度应用到 IGripperService（无效值时保持默认 30%）
        /// </summary>
        private void ApplyGripperSpeedFromSequence(double speed)
        {
            if (speed >= 1 && speed <= 100)
            {
                _gripperService.ManualOperationSpeed = speed;
                _logger.Info(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_GripperSpeedLoaded", "[ProcessSequence] 已加载电爪速度: {0}%"),
                    speed));
            }
        }

        // ========== 配方池数据加载 ==========
        private bool _isInitialized = false;
        public async Task LoadWorkOrderDataAsync()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            try
            {
                string pool = _recipePoolService.CurrentPoolName ?? "Default";
                var workOrderData = await _recipePoolService.GetExtensionDataAsync<WorkOrderData>(pool, "WorkOrderData");
                if (workOrderData != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        CameraOptions.Clear();
                        foreach (var c in workOrderData.Cameras) CameraOptions.Add(c.Name);
                        PurposeOptions.Clear();
                        foreach (var p in workOrderData.Purposes) PurposeOptions.Add(p.Name);
                        Components.Clear();
                        foreach (var comp in workOrderData.Components) Components.Add(comp);
                        Sites.Clear();
                        foreach (var site in workOrderData.Sites) Sites.Add(site);
                    });
                    WorkOrderDataRefreshed?.Invoke(this, EventArgs.Empty);
                }

                // 自动加载上次保存的工序序列文件
                await AutoLoadLastSequenceAsync();
            }
            catch { }
        }

        /// <summary>
        /// 检查 ExtensionData 中是否有上次保存的路径，若有且文件存在则自动加载
        /// </summary>
        private async Task AutoLoadLastSequenceAsync()
        {
            try
            {
                var recentList = LoadRecentFilesFromSettings();
                RecentFiles.Clear();
                foreach (var p in recentList)
                    RecentFiles.Add(p);

                // 优先从配方池恢复（按配方隔离）；无记录时回退到 appsettings 全局路径
                var lastPath = await GetLastSequencePathFromRecipePoolAsync();
                if (string.IsNullOrEmpty(lastPath))
                    lastPath = GetLastSequencePath();
                if (!string.IsNullOrEmpty(lastPath) && File.Exists(lastPath))
                {
                    _logger.Info(string.Format(
                        _localization.GetResourceOrDefault("PSE_Log_AutoLoadSequence", "[ProcessSequence] 自动加载上次工序序列: {0}"),
                        lastPath));
                    await LoadSequenceFromPathAsync(lastPath);
                }
                else
                {
                    _logger.Info(_localization.GetResourceOrDefault("PSE_Log_AutoLoadSkipped", "[ProcessSequence] 未找到上次工序序列文件，跳过自动加载"));
                }
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format(
                    _localization.GetResourceOrDefault("PSE_Log_AutoLoadFailed", "[ProcessSequence] 自动加载工序序列失败: {0}"),
                    ex.Message));
            }
        }

        public async Task ReloadWorkOrderDataAsync()
        {
            _isInitialized = false;
            await LoadWorkOrderDataAsync();
            // 重新加载后，重置 SelectedSite/SelectedComponent 以触发 SiteFeatureOptions/ComponentFeatureOptions 联动刷新
            if (Sites.Count > 0)
                SelectedSite = Sites.FirstOrDefault(s => s.Id == SelectedSite?.Id) ?? Sites.FirstOrDefault();
            if (Components.Count > 0)
                SelectedComponent = Components.FirstOrDefault(c => c.Name == SelectedComponent?.Name) ?? Components.FirstOrDefault();
        }

        // ========== 特征选项联动 ==========
        private Component _selectedComponent;
        public Component SelectedComponent
        {
            get => _selectedComponent;
            set
            {
                if (!SetProperty(ref _selectedComponent, value)) return;
                ComponentFeatureOptions.Clear();
                if (value?.Features != null)
                    foreach (var f in value.Features)
                        ComponentFeatureOptions.Add(f.Name);
            }
        }
        private Site _selectedSite;
        public Site SelectedSite
        {
            get => _selectedSite;
            set
            {
                if (!SetProperty(ref _selectedSite, value)) return;
                SiteFeatureOptions.Clear();
                if (value?.Features != null)
                    foreach (var f in value.Features)
                        SiteFeatureOptions.Add(f.Name);
            }
        }

        public ObservableCollection<string> CameraOptions { get; }
        public ObservableCollection<string> PurposeOptions { get; }
        public ObservableCollection<string> ComponentFeatureOptions { get; }
        public ObservableCollection<string> SiteFeatureOptions { get; }

        /// <summary>
        /// 对当前任务重编号（供 AddStep/DeleteStep/MoveStepUp 等使用）
        /// </summary>
        private void RenumberSteps()
        {
            if (CurrentTask == null) return;
            RenumberSteps(CurrentTask);
        }

        /// <summary>
        /// 对指定任务的所有方法重新编号（Seq 按方法内独立编号，每个方法从 1 开始），
        /// 并更新条件表达式和跳转目标中的序号引用。
        /// 拖拽排序时通过此方法确保对正确的任务执行重编号。
        /// </summary>
        private void RenumberSteps(TaskItem task)
        {
            if (task?.Methods == null) return;
            // 构建旧序号→新序号映射（Seq 按方法内独立编号，每个方法从 1 开始）
            var seqMap = new Dictionary<int, int>();
            foreach (var method in task.Methods)
            {
                for (int i = 0; i < method.Steps.Count; i++)
                {
                    int oldSeq = method.Steps[i].Seq;
                    int newSeq = i + 1;
                    if (oldSeq != newSeq)
                        seqMap[oldSeq] = newSeq;
                    method.Steps[i].Seq = newSeq;
                }
            }
            // 更新条件表达式和跳转目标中的旧序号引用
            if (seqMap.Count > 0)
                UpdateStepReferences(task, seqMap);
            task.SyncStepsFromMethods();
        }

        /// <summary>
        /// 根据序号映射表更新指定任务所有步骤中的条件表达式和跳转目标引用
        /// 匹配模式：@Output:步骤{N}_ → @Output:步骤{newN}_ 和 TargetStepSeq / DefaultTargetStepSeq
        /// </summary>
        private void UpdateStepReferences(TaskItem task, Dictionary<int, int> seqMap)
        {
            foreach (var step in task.Steps)
            {
                // 更新 BranchConfig 中的条件表达式和跳转目标
                if (step.BranchConfig != null)
                {
                    if (step.BranchConfig.Conditions != null)
                    {
                        foreach (var cond in step.BranchConfig.Conditions)
                        {
                            if (!string.IsNullOrEmpty(cond.ConditionExpression))
                                cond.ConditionExpression = ReplaceStepSeqInExpression(cond.ConditionExpression, seqMap);

                            if (seqMap.TryGetValue(cond.TargetStepSeq, out int newTargetSeq))
                                cond.TargetStepSeq = newTargetSeq;
                        }
                    }

                    if (seqMap.TryGetValue(step.BranchConfig.DefaultTargetStepSeq, out int newDefaultSeq))
                        step.BranchConfig.DefaultTargetStepSeq = newDefaultSeq;
                }

                // 更新 CheckDetail 中的跳转目标序号
                if (step.CheckDetail != null)
                {
                    if (seqMap.TryGetValue(step.CheckDetail.OnPassJumpStepSeq, out int newPassSeq))
                        step.CheckDetail.OnPassJumpStepSeq = newPassSeq;
                    if (seqMap.TryGetValue(step.CheckDetail.OnFailJumpStepSeq, out int newFailSeq))
                        step.CheckDetail.OnFailJumpStepSeq = newFailSeq;
                }
            }
        }

        /// <summary>
        /// 替换表达式中 @Output:步骤{N}_ 和 @Output:步骤{N}_CheckResult 等引用的序号
        /// </summary>
        private static string ReplaceStepSeqInExpression(string expression, Dictionary<int, int> seqMap)
        {
            foreach (var kv in seqMap)
            {
                expression = expression.Replace($"步骤{kv.Key}_", $"步骤{kv.Value}_");
            }
            return expression;
        }
    }

    // JSON 序列化辅助类
    public class SequenceData
    {
        public List<SequenceTaskData> Tasks { get; set; }

        /// <summary>电爪手动操作速度（1-100%），与 IGripperService.ManualOperationSpeed 同步持久化</summary>
        public double GripperManualOperationSpeed { get; set; } = 30;
    }

    public class SequenceTaskData
    {
        public string Name { get; set; }
        public bool IsDefault { get; set; }
        public TaskItem.TaskStatusEnum Status { get; set; }
        public TaskRunMode RunMode { get; set; }
        public bool IsEnabled { get; set; } = true;
        /// <summary>任务注释（用户备注，需持久化保存/恢复）</summary>
        public string Comment { get; set; }
        /// <summary>TreeView 展开状态（持久化以保持用户折叠/展开偏好）</summary>
        public bool IsExpanded { get; set; } = true;
        public List<ProcessMethod> Methods { get; set; }
        // 向后兼容：旧格式只有 Steps，加载时迁移为 Methods
        public List<ProcessStep> Steps { get; set; }
    }

    /// <summary> 配方池中记录当前工序序列文件路径的扩展数据 </summary>
    public class ProcessSequenceFileRecord
    {
        public string FilePath { get; set; }
    }
}

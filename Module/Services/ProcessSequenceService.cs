using AlarmModule.Interfaces;
using Core.Abstraction;
using Core.Utilities;
using Module.Models;
using Newtonsoft.Json;
using Prism.Events;
using Prism.Mvvm;
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
    public class ProcessSequenceService : BindableBase, IProcessSequenceService
    {
        private readonly IRecipePoolService _recipePoolService;
        private readonly IParameterStorage _parameterStorage;
        private readonly IAppSettingService _appSettingService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILoggerService _logger;
        private readonly IStationRegistry _stationRegistry;
        private readonly Prism.Ioc.IContainerProvider _containerProvider;
        private CancellationTokenSource _executionCts;
        private bool _isExecuting;
        private StationTaskBase _activeStationTask;
        /// <summary> 单步模式开关：启用时每个步骤执行后等待用户确认 </summary>
        private bool _isSingleStepMode;
        /// <summary> 单步模式下的“下一步”等待令牌，用户点击“下一步”时设置结果解除等待 </summary>
        private TaskCompletionSource<bool> _stepNextTcs;

        // 工序序列文件默认保存目录
        private const string ProcessSequenceDirectory = "Config\\ProcessSequences";
        private const string LastPathKey = "LastProcessSequencePath";
        private const string RecentPathsKey = "RecentProcessSequencePaths";
        private const int MaxRecentFiles = 10;
        /// <summary> 配置文件保留天数，超过此天数的旧文件在保存时自动清理 </summary>
        private const int ConfigRetentionDays = 30;

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
            Prism.Ioc.IContainerProvider containerProvider)
        {
            _recipePoolService = recipePoolService;
            _parameterStorage = parameterStorage;
            _appSettingService = appSettingService;
            _eventAggregator = eventAggregator;
            _logger = logger;
            _stationRegistry = stationRegistry;
            _containerProvider = containerProvider;
            Tasks = new ObservableCollection<TaskItem>();
            RecentFiles = new ObservableCollection<string>();
            CameraOptions = new ObservableCollection<string>();
            PurposeOptions = new ObservableCollection<string>();
            ComponentFeatureOptions = new ObservableCollection<string>();
            SiteFeatureOptions = new ObservableCollection<string>();
            // 创建默认任务
            AddTask(isDefault: true);
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
                _logger.Error($"[ProcessSequence] 读取最近文件列表失败: {ex.Message}");
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
                _logger.Error($"[ProcessSequence] 保存最近文件列表失败: {ex.Message}");
            }
        }

        public void AddStep(ProcessStep step)
        {
            if (CurrentTask == null) return;
            step.Seq = CurrentTask.Steps.Count + 1;
            CurrentTask.Steps.Add(step);
        }

        public void DeleteStep()
        {
            if (SelectedStep == null) return;
            CurrentTask.Steps.Remove(SelectedStep);
            RenumberSteps();
        }

        public void MoveStepUp()
        {
            if (SelectedStep == null) return;
            int idx = CurrentTask.Steps.IndexOf(SelectedStep);
            if (idx <= 0) return;
            CurrentTask.Steps.Move(idx, idx - 1);
            RenumberSteps();
            SelectedStep = CurrentTask.Steps[idx - 1];
        }

        public void MoveStepDown()
        {
            if (SelectedStep == null) return;
            int idx = CurrentTask.Steps.IndexOf(SelectedStep);
            if (idx >= CurrentTask.Steps.Count - 1) return;
            CurrentTask.Steps.Move(idx, idx + 1);
            RenumberSteps();
            SelectedStep = CurrentTask.Steps[idx + 1];
        }

        public void AddTask(bool isDefault = false)
        {
            var newTask = new TaskItem($"Task {Tasks.Count + 1}", new ObservableCollection<ProcessStep>())
            {
                IsDefault = isDefault,
                Status = TaskItem.TaskStatusEnum.Idle
            };
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
                new ProcessStep { Seq = 3, Step = StepType.CHECK, CompFeature = "—", SiteFeature = "sid_ccd" },
                new ProcessStep { Seq = 4, Step = StepType.RELEASE, CompFeature = "—", SiteFeature = "TAB_001" },
                new ProcessStep { Seq = 5, Step = StepType.GOTO, CompFeature = "—", SiteFeature = "HOME" }
            };
            foreach (var s in steps) CurrentTask.Steps.Add(s);
        }

        // ========== 任务控制 ==========
        /// <summary> 指示是否有任务正在执行 </summary>
        public bool IsExecuting => _isExecuting;

        /// <summary> 获取第一个可用的 StationTaskBase 作为序列执行宿主（提供暂停/急停/单步保护等运行时基础设施） </summary>
        private StationTaskBase FindStationTask()
        {
            var firstStation = _stationRegistry.GetAllStations().OfType<StationTaskBase>().FirstOrDefault();
            if (firstStation == null)
                _logger.Warn("[ProcessSequence] 未找到任何已注册的工站任务");
            else
                _logger.Info($"[ProcessSequence] 使用执行宿主: {firstStation.TaskName}");
            return firstStation;
        }

        /// <summary> 启动当前任务：通过 IStationRegistry 获取目标工站，调用 RunCustomSequenceAsync 执行步骤序列 </summary>
        public void StartTask()
        {
            if (CurrentTask == null) return;
            if (_isExecuting)
            {
                _logger.Warn("[ProcessSequence] 已有任务正在执行，拒绝启动新任务");
                return;
            }
            var stationTask = FindStationTask();
            if (stationTask == null) return;
            var steps = CurrentTask.Steps;
            if (steps == null || steps.Count == 0)
            {
                _logger.Warn("[ProcessSequence] 当前任务没有步骤，无法启动");
                return;
            }
            _executionCts = new CancellationTokenSource();
            _isExecuting = true;
            _activeStationTask = stationTask;
            CurrentTask.Status = TaskItem.TaskStatusEnum.Running;
            // 启动时清除上次运行遗留的报警标记
            foreach (var step in steps)
                step.HasActiveAlarm = false;
            _logger.Info($"[ProcessSequence] 启动任务: {CurrentTask.Name}，共 {steps.Count} 个步骤，目标工站: {stationTask.TaskName}");

            // 异步执行步骤序列
            _ = ExecuteSequenceAsync(stationTask, steps, _executionCts.Token);
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
                    var executor = new ProcessStepExecutor(stationTask, stationTask.TaskLogger, actions, alarmService, formulaEvaluator, _recipePoolService);

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

                    await executor.ExecuteAsync(steps, ct);
                }, token);
            }
            catch (InvalidOperationException ex)
            {
                _logger.Error($"[ProcessSequence] 启动失败: {ex.Message}");
            }
            finally
            {
                _isExecuting = false;
                _activeStationTask = null;
                _stepNextTcs = null;
                _executionCts?.Dispose();
                _executionCts = null;
                if (CurrentTask != null)
                {
                    CurrentTask.Status = TaskItem.TaskStatusEnum.Idle;
                    // 任务结束后重置步骤高亮到第一步
                    ResetStepHighlight();
                }
                _logger.Info("[ProcessSequence] 任务执行结束");
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
            _logger.Info("[ProcessSequence] 任务已停止");
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
            _logger.Info("[ProcessSequence] 任务已暂停");
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
            _logger.Info("[ProcessSequence] 任务已恢复");
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
                    _logger.Info($"[ProcessSequence] 单步模式: {(value ? "已启用" : "已关闭")}");
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
                _logger.Info("[ProcessSequence] 单步模式：用户确认下一步");
            }
        }

        /// <summary> 重置步骤高亮到第一步（不清除HasActiveAlarm，报警标记在下次启动时清除） </summary>
        private void ResetStepHighlight()
        {
            _logger.Info("[ProcessSequenceService] ResetStepHighlight 被调用");
            if (CurrentTask?.Steps == null) return;
            foreach (var step in CurrentTask.Steps)
            {
                step.IsCurrent = false;
            }
            if (CurrentTask.Steps.Count > 0)
                CurrentTask.Steps[0].IsCurrent = true;
            _logger.Info("[ProcessSequenceService] ResetStepHighlight 完成");
        }

        /// <summary> 单独执行指定步骤（用于步骤编辑器中的调试运行） </summary>
        public async Task RunSingleStepAsync(ProcessStep step)
        {
            if (step == null || _isExecuting) return;

            var stationTask = _activeStationTask ?? FindStationTask();
            if (stationTask == null)
            {
                _logger.Warn("[ProcessSequenceService] 未找到可用的工站任务，无法单独执行步骤");
                return;
            }

            var actions = CreateStepActions();
            var alarmService = _containerProvider.Resolve(typeof(AlarmModule.Interfaces.IAlarmService)) as AlarmModule.Interfaces.IAlarmService;
            var formulaEvaluator = _containerProvider.Resolve(typeof(Core.Abstraction.IFormulaEvaluator)) as Core.Abstraction.IFormulaEvaluator;
            var executor = new ProcessStepExecutor(stationTask, stationTask.TaskLogger, actions, alarmService, formulaEvaluator, _recipePoolService);

            try
            {
                _isExecuting = true;
                CurrentTask.Status = TaskItem.TaskStatusEnum.Running;
                await executor.ExecuteSingleStepAsync(step, CancellationToken.None);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.Error($"[ProcessSequenceService] 单独执行步骤异常: {ex.Message}");
            }
            finally
            {
                _isExecuting = false;
                CurrentTask.Status = TaskItem.TaskStatusEnum.Idle;
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
            int checkCount = CurrentTask.Steps.Count(s => s.Step == StepType.CHECK);
            results.Add(new ValidationItem("At least one CHECK step", checkCount >= 1));
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
                Steps = t.Steps.ToList()
            }).ToList();
            var data = new SequenceData { Tasks = allTasks };
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
        /// 保存后自动清理超过保留天数的旧文件
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
            // 后台清理过期文件，避免阻塞UI
            QueueCleanupOldFiles(dir, filePath);
            return result;
        }

        /// <summary> 后台异步清理过期配置文件，避免阻塞UI线程 </summary>
        private void QueueCleanupOldFiles(string configDir, string currentFilePath)
        {
            _ = Task.Run(() => CleanupOldFiles(configDir, currentFilePath));
        }

        /// <summary>
        /// 清理超过保留天数的旧配置文件。
        /// 仅删除匹配 ProcessSequences_*.json 模式的文件，跳过当前刚保存的文件。
        /// 清理失败仅记录日志，不影响主流程。
        /// </summary>
        private void CleanupOldFiles(string configDir, string currentFilePath)
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-ConfigRetentionDays);
                var cleanedCount = 0;

                foreach (var file in Directory.EnumerateFiles(configDir, "ProcessSequences_*.json"))
                {
                    if (string.Equals(file, currentFilePath, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        try
                        {
                            File.Delete(file);
                            cleanedCount++;
                            _logger.Info($"[ProcessSequence] 已清理过期配置文件: {file} (超过{ConfigRetentionDays}天)");
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn($"[ProcessSequence] 清理过期配置文件失败: {file}, {ex.Message}");
                        }
                    }
                }

                if (cleanedCount > 0)
                    _logger.Info($"[ProcessSequence] 本次清理了 {cleanedCount} 个过期配置文件 (保留{ConfigRetentionDays}天)");
            }
            catch (Exception ex)
            {
                _logger.Warn($"[ProcessSequence] 清理过期配置文件异常: {ex.Message}");
            }
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
                _logger.Info($"[ProcessSequence] 已保存工序序列路径: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.Error($"[ProcessSequence] 保存工序序列路径失败: {ex.Message}");
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
                    _logger.Info($"[ProcessSequence] 从配置读取上次工序序列路径: {path}");
                    return path;
                }
                _logger.Info($"[ProcessSequence] 配置中未找到上次工序序列路径 (ExtensionData 中无键或类型非字符串)");
            }
            catch (Exception ex)
            {
                _logger.Error($"[ProcessSequence] 读取上次工序序列路径失败: {ex.Message}");
            }
            return null;
        }

        public Task LoadSequenceFromPathAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            var dir = Path.GetDirectoryName(filePath);
            var identifier = Path.GetFileNameWithoutExtension(filePath);
            var data = _parameterStorage.Load<SequenceData>(identifier, dir);
            if (data != null && data.Tasks != null && data.Tasks.Any())
            {
                Tasks.Clear();
                foreach (var taskData in data.Tasks)
                {
                    var task = new TaskItem(taskData.Name, new ObservableCollection<ProcessStep>(taskData.Steps ?? new List<ProcessStep>()))
                    {
                        IsDefault = taskData.IsDefault,
                        Status = TaskItem.TaskStatusEnum.Idle
                    };
                    // 加载后重置所有步骤的运行时状态
                    foreach (var step in task.Steps)
                    {
                        step.IsCurrent = false;
                        // JSON反序列化后强制刷新IsAlarmEnabled，确保UI DataTrigger正确绑定
                        step.EnsureAlarmConfigInitialized();
                    }
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
            return Task.CompletedTask;
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

                var lastPath = GetLastSequencePath();
                if (!string.IsNullOrEmpty(lastPath) && File.Exists(lastPath))
                {
                    _logger.Info($"[ProcessSequence] 自动加载上次工序序列: {lastPath}");
                    await LoadSequenceFromPathAsync(lastPath);
                }
                else
                {
                    _logger.Info($"[ProcessSequence] 未找到上次工序序列文件，跳过自动加载");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[ProcessSequence] 自动加载工序序列失败: {ex.Message}");
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

        private void RenumberSteps()
        {
            // 构建旧序号→新序号映射
            var seqMap = new Dictionary<int, int>();
            for (int i = 0; i < CurrentTask.Steps.Count; i++)
            {
                int oldSeq = CurrentTask.Steps[i].Seq;
                int newSeq = i + 1;
                if (oldSeq != newSeq)
                    seqMap[oldSeq] = newSeq;
            }

            // 更新步骤序号
            for (int i = 0; i < CurrentTask.Steps.Count; i++)
                CurrentTask.Steps[i].Seq = i + 1;

            // 同步更新条件表达式和跳转目标中的旧序号引用
            if (seqMap.Count > 0)
                UpdateStepReferences(seqMap);
        }

        /// <summary>
        /// 根据序号映射表更新所有步骤中的条件表达式和跳转目标引用
        /// 匹配模式：@Output:步骤{N}_ → @Output:步骤{newN}_ 和 TargetStepSeq / DefaultTargetStepSeq
        /// </summary>
        private void UpdateStepReferences(Dictionary<int, int> seqMap)
        {
            foreach (var step in CurrentTask.Steps)
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
    }

    public class SequenceTaskData
    {
        public string Name { get; set; }
        public bool IsDefault { get; set; }
        public TaskItem.TaskStatusEnum Status { get; set; }
        public List<ProcessStep> Steps { get; set; }
    }
}
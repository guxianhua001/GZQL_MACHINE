using Core.Abstraction;
using Core.Models;
using Module.Models;
using Module.Services;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Recipe.Events;
using Recipe.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows;
using System.Windows.Data;

namespace Module.ViewModels
{
    /// <summary>
    /// 点涂点位编辑器视图模型——管理点涂模式下的点位集合、工艺参数、分组过滤及执行控制
    /// </summary>
    public class DotPointEditorViewModel : BindableBase
    {
        #region 字段

        private readonly IDotDispenseService _dotDispenseService;
        private readonly IDialogService _dialogService;
        private readonly IRecipePoolService _recipePoolService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ILocalizationService _localizationService;
        private CancellationTokenSource _cts;

        #endregion

        #region 数据属性

        private ObservableCollection<DotPoint> _points;
        private DotPoint _selectedPoint;
        private string _status;
        private string _progressText;
        private bool _isExecuting;
        private bool _isError;
        private int _progressCurrent;
        private int _progressTotal;

        public ObservableCollection<DotPoint> Points
        {
            get => _points;
            set => SetProperty(ref _points, value);
        }

        public DotProcessParams ProcessParams { get; } = new DotProcessParams();

        public DotPoint SelectedPoint
        {
            get => _selectedPoint;
            set => SetProperty(ref _selectedPoint, value);
        }

        public string Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    IsError = value.Contains(L("Exception")) || value.Contains(L("Failed")) || value.Contains(L("Error"));
                }
            }
        }

        /// <summary>
        /// 当前状态是否为错误状态（用于UI红色报警显示）
        /// </summary>
        public bool IsError
        {
            get => _isError;
            set => SetProperty(ref _isError, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            set => SetProperty(ref _isExecuting, value);
        }

        public int ProgressCurrent
        {
            get => _progressCurrent;
            set => SetProperty(ref _progressCurrent, value);
        }

        public int ProgressTotal
        {
            get => _progressTotal;
            set => SetProperty(ref _progressTotal, value);
        }

        #endregion

        /// <summary>
        /// 获取多语言文本（便捷方法）
        /// </summary>
        private string L(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (_localizationService != null)
                return _localizationService.GetResource(key);

            var resource = Application.Current?.TryFindResource(key);
            return resource?.ToString() ?? $"[{key}]";
        }

        #region 步骤导航

        private int _currentStep = 1;
        /// <summary>
        /// 当前工作流步骤（1=工艺参数, 2=点位编辑, 3=执行控制），驱动步骤指示器状态和内容区Visibility切换
        /// </summary>
        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                if (SetProperty(ref _currentStep, value))
                {
                    RaisePropertyChanged(nameof(Step1State));
                    RaisePropertyChanged(nameof(Step2State));
                    RaisePropertyChanged(nameof(Step3State));
                    RaisePropertyChanged(nameof(CurrentStepTitle));
                    RaisePropertyChanged(nameof(IsStep1Active));
                    RaisePropertyChanged(nameof(IsStep2Active));
                    RaisePropertyChanged(nameof(IsStep3Active));
                    GoPrevCommand?.RaiseCanExecuteChanged();
                    GoNextCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public StepState Step1State => GetStepState(1);
        public StepState Step2State => GetStepState(2);
        public StepState Step3State => GetStepState(3);

        public bool IsStep1Active => CurrentStep == 1;
        public bool IsStep2Active => CurrentStep == 2;
        public bool IsStep3Active => CurrentStep == 3;

        public string CurrentStepTitle => CurrentStep switch
        {
            1 => L("Dispensing_Dot_Step1_Title"),
            2 => L("Dispensing_Dot_Step2_Title"),
            3 => L("Dispensing_Dot_Step3_Title"),
            _ => L("Dispensing_Dot_Step1_Title")
        };

        private StepState GetStepState(int step)
        {
            if (step < CurrentStep) return StepState.Done;
            if (step == CurrentStep) return StepState.Active;
            return StepState.Pending;
        }

        #endregion

        #region 分组属性

        private string _selectedGroupFilter;

        public ObservableCollection<string> Groups { get; }

        public string SelectedGroupFilter
        {
            get => _selectedGroupFilter;
            set
            {
                if (SetProperty(ref _selectedGroupFilter, value))
                    FilteredPoints.Refresh();
            }
        }

        public ObservableCollection<string> GroupFilters { get; }

        public ICollectionView FilteredPoints { get; }

        #endregion

        #region 命令

        public DelegateCommand AddPointCommand { get; }
        public DelegateCommand DeleteSelectedCommand { get; }
        public DelegateCommand SelectAllCommand { get; }
        public DelegateCommand DeselectAllCommand { get; }
        public DelegateCommand<DotPoint> TeachPointCommand { get; }
        public DelegateCommand ApplyProcessParamsCommand { get; }
        public DelegateCommand DryRunCommand { get; }
        public DelegateCommand ExecuteDotDispenseCommand { get; }
        public DelegateCommand StopExecutionCommand { get; }
        public DelegateCommand SaveDataCommand { get; }
        public DelegateCommand LoadDataCommand { get; }
        public DelegateCommand GoPrevCommand { get; }
        public DelegateCommand GoNextCommand { get; }

        #endregion

        #region 构造函数

        public DotPointEditorViewModel(
            IDotDispenseService dotDispenseService,
            IDialogService dialogService,
            IRecipePoolService recipePoolService,
            IEventAggregator eventAggregator,
            ILocalizationService localizationService)
        {
            _dotDispenseService = dotDispenseService;
            _dialogService = dialogService;
            _recipePoolService = recipePoolService;
            _eventAggregator = eventAggregator;
            _localizationService = localizationService;

            _dotDispenseService.ProgressChanged += OnProgressChanged;
            _dotDispenseService.StatusChanged += OnStatusChanged;

            Points = new ObservableCollection<DotPoint>();
            Groups = new ObservableCollection<string>();
            GroupFilters = new ObservableCollection<string> { L("Dispensing_Dot_Filter_All") };

            FilteredPoints = CollectionViewSource.GetDefaultView(Points);
            FilteredPoints.Filter = o => o is DotPoint p &&
                (string.IsNullOrEmpty(SelectedGroupFilter) || SelectedGroupFilter == L("Dispensing_Dot_Filter_All") || p.Group == SelectedGroupFilter);

            _eventAggregator.GetEvent<RecipeChangedEvent>().Subscribe(OnRecipeChanged);
            _eventAggregator.GetEvent<RecipePoolChangedEvent>().Subscribe(OnRecipePoolChanged);

            LoadGroupsFromRecipePool();

            AddSamplePoints();

            _status = L("Dispensing_Dot_Status_Ready");
            _selectedGroupFilter = L("Dispensing_Dot_Filter_All");

            AddPointCommand = new DelegateCommand(OnAddPoint);
            DeleteSelectedCommand = new DelegateCommand(OnDeleteSelected);
            SelectAllCommand = new DelegateCommand(OnSelectAll);
            DeselectAllCommand = new DelegateCommand(OnDeselectAll);
            TeachPointCommand = new DelegateCommand<DotPoint>(OnTeachPoint);
            ApplyProcessParamsCommand = new DelegateCommand(OnApplyProcessParams);
            DryRunCommand = new DelegateCommand(OnDryRun, () => !IsExecuting).ObservesProperty(() => IsExecuting);
            ExecuteDotDispenseCommand = new DelegateCommand(OnExecuteDotDispense, () => !IsExecuting).ObservesProperty(() => IsExecuting);
            StopExecutionCommand = new DelegateCommand(OnStopExecution);
            SaveDataCommand = new DelegateCommand(OnSaveData);
            LoadDataCommand = new DelegateCommand(OnLoadData);

            GoPrevCommand = new DelegateCommand(
                () => { if (CurrentStep > 1) CurrentStep--; },
                () => CurrentStep > 1
            );
            GoNextCommand = new DelegateCommand(
                () => { if (CurrentStep < 3) CurrentStep++; },
                () => CurrentStep < 3
            );
        }

        #endregion

        #region 分组动态加载

        /// <summary>
        /// 从配方池的 WorkOrderData.Sites 动态加载 Group 列表
        /// </summary>
        private async void LoadGroupsFromRecipePool()
        {
            try
            {
                var poolId = _recipePoolService.CurrentPoolId;
                if (string.IsNullOrEmpty(poolId)) return;

                var workOrderData = await _recipePoolService.GetExtensionDataAsync<WorkOrderData>(poolId, "WorkOrderData");
                if (workOrderData?.Sites == null) return;

                var siteNames = workOrderData.Sites.Select(s => s.Name).Where(n => !string.IsNullOrEmpty(n)).ToList();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    Groups.Clear();
                    GroupFilters.Clear();
                    GroupFilters.Add(L("Dispensing_Dot_Filter_All"));
                    foreach (var name in siteNames)
                    {
                        Groups.Add(name);
                        GroupFilters.Add(name);
                    }
                });
            }
            catch (Exception)
            {
                Status = L("Dispensing_Dot_Error_LoadGroupFailed");
            }
        }

        private void OnRecipeChanged(string recipeName)
        {
            LoadGroupsFromRecipePool();
        }

        private void OnRecipePoolChanged(string poolId)
        {
            LoadGroupsFromRecipePool();
        }

        #endregion

        #region 示例数据

        private void AddSamplePoints()
        {
            Points.Add(new DotPoint { Group = "ASSY_001", PointId = "DOT_001", Dx = 10.5, Dy = 20.3, Dz2 = 5.011, Dz3 = 3.2 });
            Points.Add(new DotPoint { Group = "ASSY_001", PointId = "DOT_002", Dx = 15.0, Dy = 25.1, Dz2 = 5.008, Dz3 = 3.15 });
            Points.Add(new DotPoint { Group = "ASSY_002", PointId = "DOT_003", Dx = 20.5, Dy = 30.0, Dz2 = 5.003, Dz3 = 3.1 });
            Points.Add(new DotPoint { Group = "ASSY_002", PointId = "DOT_004", Dx = 25.0, Dy = 35.2, Dz2 = 5.007, Dz3 = 3.05 });
        }

        #endregion

        #region 数据管理

        private void OnAddPoint()
        {
            var nextId = GetNextPointId();
            var defaultGroup = Groups.Count > 0 ? Groups[0] : "DEFAULT";
            Points.Add(new DotPoint
            {
                Group = defaultGroup,
                PointId = nextId
            });
        }

        private void OnDeleteSelected()
        {
            var toDelete = Points.Where(p => p.IsSelected).ToList();
            foreach (var point in toDelete)
                Points.Remove(point);
            RenumberPointIds();
        }

        private void OnSelectAll()
        {
            foreach (var point in Points)
                point.IsSelected = true;
        }

        private void OnDeselectAll()
        {
            foreach (var point in Points)
                point.IsSelected = false;
        }

        private string GetNextPointId()
        {
            var maxId = Points
                .Select(p => p.PointId)
                .Where(id => id.StartsWith("DOT_"))
                .Select(id => int.TryParse(id.Substring(4), out int num) ? num : 0)
                .DefaultIfEmpty(0)
                .Max();
            return $"DOT_{(maxId + 1):000}";
        }

        private void RenumberPointIds()
        {
            int index = 1;
            foreach (var point in Points)
            {
                point.PointId = $"DOT_{index:000}";
                index++;
            }
        }

        #endregion

        #region 工艺参数应用

        private int _applyTargetZ = 2;
        /// <summary>
        /// 应用目标轴选择：2=Dz2, 3=Dz3
        /// </summary>
        public int ApplyTargetZ
        {
            get => _applyTargetZ;
            set => SetProperty(ref _applyTargetZ, value);
        }

        public ObservableCollection<int> ApplyTargetOptions { get; } = new ObservableCollection<int> { 2, 3 };

        private void OnApplyProcessParams()
        {
            var selected = Points.Where(p => p.IsSelected).ToList();
            if (selected.Count == 0) return;

            double teachHeight = ProcessParams.TeachHeight;
            double compensation = ProcessParams.HeightCompensation;

            foreach (var point in selected)
            {
                if (ApplyTargetZ == 2)
                {
                    point.Dz2 = teachHeight;
                    point.Dz2Compensation = compensation;
                }
                else
                {
                    point.Dz3 = teachHeight;
                    point.Dz3Compensation = compensation;
                }
            }

            string targetLabel = ApplyTargetZ == 2 ? "Dz2" : "Dz3";
            Status = $"已将工艺参数高度应用到 {selected.Count} 个选中点 ({targetLabel})";
        }

        #endregion

        #region 示教

        private async void OnTeachPoint(DotPoint point)
        {
            if (point == null) return;
            try
            {
                await _dotDispenseService.TeachPointAsync(point);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Status = $"示教失败: {ex.Message}";
            }
        }

        #endregion

        #region 执行控制

        private async void OnDryRun()
        {
            _cts = new CancellationTokenSource();
            IsExecuting = true;
            try
            {
                await _dotDispenseService.DryRunAsync(Points, ProcessParams, _cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Status = $"空跑异常: {ex.Message}";
            }
            finally
            {
                IsExecuting = false;
            }
        }

        private async void OnExecuteDotDispense()
        {
            _cts = new CancellationTokenSource();
            IsExecuting = true;
            try
            {
                await _dotDispenseService.ExecuteDotDispenseAsync(Points, ProcessParams, _cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Status = $"执行异常: {ex.Message}";
            }
            finally
            {
                IsExecuting = false;
            }
        }

        private async void OnStopExecution()
        {
            _cts?.Cancel();
            try
            {
                await _dotDispenseService.StopAsync();
            }
            catch (Exception)
            {
            }
            IsExecuting = false;
        }

        #endregion

        #region 事件处理

        private void OnProgressChanged(string text, int current, int total)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressText = text;
                ProgressCurrent = current;
                ProgressTotal = total;
            });
        }

        private void OnStatusChanged(string status)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Status = status;
            });
        }

        #endregion

        #region 保存/加载

        private void OnSaveData()
        {
            var dateTag = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON 文件|*.json",
                DefaultExt = ".json",
                FileName = $"DotPointData_{dateTag}"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var data = new DotPointDataFile
                {
                    ProcessParams = ProcessParams,
                    Points = Points.ToList(),
                    SavedAt = DateTime.Now,
                    Version = "1.0"
                };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json);
                Status = L("Dispensing_Dot_Success_DataSaved");
            }
            catch (Exception ex)
            {
                Status = $"保存失败: {ex.Message}";
            }
        }

        private void OnLoadData()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON 文件|*.json",
                DefaultExt = ".json"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var json = File.ReadAllText(dlg.FileName);
                var data = JsonSerializer.Deserialize<DotPointDataFile>(json);
                if (data == null) return;

                Points.Clear();
                foreach (var p in data.Points)
                    Points.Add(p);

                ApplyLoadedProcessParams(data.ProcessParams);
                Status = L("Dispensing_Dot_Success_DataLoaded");
            }
            catch (Exception ex)
            {
                Status = $"加载失败: {ex.Message}";
            }
        }

        private void ApplyLoadedProcessParams(DotProcessParams loaded)
        {
            if (loaded == null) return;
            ProcessParams.MoveSpeed = loaded.MoveSpeed;
            ProcessParams.SafeHeight = loaded.SafeHeight;
            ProcessParams.ApproachHeight = loaded.ApproachHeight;
            ProcessParams.CornerDecel = loaded.CornerDecel;
            ProcessParams.DispenseTime = loaded.DispenseTime;
            ProcessParams.PostDelay = loaded.PostDelay;
            ProcessParams.DotGlueTriggerOffsetMm = loaded.DotGlueTriggerOffsetMm;
            ProcessParams.DispensingPressure = loaded.DispensingPressure;
            ProcessParams.SuckBackTime = loaded.SuckBackTime;
            ProcessParams.TeachHeight = loaded.TeachHeight;
            ProcessParams.HeightCompensation = loaded.HeightCompensation;
        }

        #endregion
    }

    /// <summary>
    /// 点涂点位数据文件结构，用于 JSON 序列化/反序列化
    /// </summary>
    public class DotPointDataFile
    {
        [JsonPropertyName("ProcessParams")]
        public DotProcessParams ProcessParams { get; set; }

        [JsonPropertyName("Points")]
        public List<DotPoint> Points { get; set; }

        [JsonPropertyName("SavedAt")]
        public DateTime SavedAt { get; set; }

        [JsonPropertyName("Version")]
        public string Version { get; set; }
    }
}

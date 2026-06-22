using Core.Models;
using MotionControl.Interfaces;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Recipe.Events;
using Recipe.Interfaces;
using Core.Abstraction;  // ILocalizationService / IADValueConverter 接口
using StationTasks.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows;

namespace Module.ViewModels
{
    /// <summary>
    /// SEEK 步骤编辑器 ViewModel，支持通道行 CRUD、实时力值刷新、导入导出、全局变量绑定
    /// </summary>
    public class SeekDetailViewModel : BindableBase, IDisposable, IDialogCloseable
    {
        private readonly IMotionService _motionService;
        private readonly IRecipePoolService _recipePoolService;
        private ProcessStep _step;
        private DispatcherTimer _refreshTimer;
        private bool _isRefreshing;
        private readonly ILocalizationService _localizationService;
        /// <summary> AD值转换器（Singleton），用于获取通道配置（名称、单位等） </summary>
        private readonly IADValueConverter _adConverter;
        private readonly IEventAggregator _eventAggregator;

        /// <summary>请求关闭对话框时触发</summary>
        public event Action<object> RequestClose;

        /// <summary>是否可以关闭对话框</summary>
        public bool CanCloseDialog() => true;

        /// <summary> 当前编辑的工艺步骤，设置时自动初始化通道行 </summary>
        public ProcessStep Step
        {
            get => _step;
            set
            {
                if (SetProperty(ref _step, value))
                    InitializeFromStep();
            }
        }

        private ObservableCollection<SeekChannelRow> _channelRows = new ObservableCollection<SeekChannelRow>();
        public ObservableCollection<SeekChannelRow> ChannelRows
        {
            get => _channelRows;
            set => SetProperty(ref _channelRows, value ?? new ObservableCollection<SeekChannelRow>());
        }

        private SeekChannelRow _selectedChannelRow;
        public SeekChannelRow SelectedChannelRow
        {
            get => _selectedChannelRow;
            set => SetProperty(ref _selectedChannelRow, value);
        }

        private ObservableCollection<string> _globalVariableOptions;
        public ObservableCollection<string> GlobalVariableOptions
        {
            get => _globalVariableOptions;
            set => SetProperty(ref _globalVariableOptions, value);
        }

        /// <summary> 可链接的全局变量列表（仅 Double 类型），供 GlobalVariableLinkControl 使用 </summary>
        private ObservableCollection<GlobalVariable> _linkableGlobalVariables;
        public ObservableCollection<GlobalVariable> LinkableGlobalVariables
        {
            get => _linkableGlobalVariables;
            set => SetProperty(ref _linkableGlobalVariables, value);
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

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

        public ICommand AddChannelRowCommand { get; }
        public ICommand DeleteChannelRowCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand StartRefreshCommand { get; }
        public ICommand StopRefreshCommand { get; }
        /// <summary> 关闭弹窗命令 </summary>
        public ICommand CloseCommand { get; }
        /// <summary> 保存到当前步骤（不关闭弹窗） </summary>
        public ICommand SaveOnlyCommand { get; }
        /// <summary> 保存并关闭弹窗命令 </summary>
        public ICommand SaveCommand { get; }

        /// <summary> 取消链接全局变量命令 </summary>
        public ICommand UnlinkVariableCommand { get; }

        public SeekDetailViewModel(
            IMotionService motionService,
            IRecipePoolService recipePoolService,
            ILocalizationService localizationService,
            IADValueConverter adConverter,
            IEventAggregator eventAggregator)
        {
            _motionService = motionService;
            _recipePoolService = recipePoolService;
            _localizationService = localizationService;
            _adConverter = adConverter;
            _eventAggregator = eventAggregator;

            AddChannelRowCommand = new DelegateCommand(OnAddChannelRow);
            DeleteChannelRowCommand = new DelegateCommand(OnDeleteChannelRow, () => SelectedChannelRow != null)
                .ObservesProperty(() => SelectedChannelRow);
            ImportCommand = new DelegateCommand(OnImport);
            ExportCommand = new DelegateCommand(OnExport, () => ChannelRows.Count > 0)
                .ObservesProperty(() => ChannelRows);
            StartRefreshCommand = new DelegateCommand(OnStartRefresh);
            StopRefreshCommand = new DelegateCommand(OnStopRefresh, () => IsRefreshing)
                .ObservesProperty(() => IsRefreshing);
            CloseCommand = new DelegateCommand(OnClose);
            SaveOnlyCommand = new DelegateCommand(OnSaveOnly);
            SaveCommand = new DelegateCommand(OnSave);
            UnlinkVariableCommand = new DelegateCommand(OnUnlinkVariable);

            // 订阅全局变量变更事件，使用 UIThread 确保在 UI 线程刷新显示
            _eventAggregator.GetEvent<GlobalVariablesChangedEvent>()
                .Subscribe(OnGlobalVariablesChanged, ThreadOption.UIThread);

            LoadGlobalVariablesAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// 从 Step 初始化通道行列表，若 SeekDetail 为空则创建默认通道
        /// </summary>
        private void InitializeFromStep()
        {
            if (_step == null) return;

            if (_step.SeekDetail == null)
            {
                _step.SeekDetail = new SeekDetail
                {
                    ChannelRows = new ObservableCollection<SeekChannelRow>
                    {
                        new SeekChannelRow { Sub = 1, LinkedChannel = 0, TargetForce = 0.3, ForceMin = -2.0, ForceMax = 2.0, Description = L("SeekDetail_DefaultDesc") }
                    }
                };
            }

            ChannelRows = new ObservableCollection<SeekChannelRow>(
                _step.SeekDetail.ChannelRows.Select(r => new SeekChannelRow
                {
                    Sub = r.Sub,
                    LinkedChannel = r.LinkedChannel,
                    TargetForce = r.TargetForce,
                    ForceMin = r.ForceMin,
                    ForceMax = r.ForceMax,
                    LinkedVariableName = r.LinkedVariableName,
                    Description = r.Description
                }));

            // 从 AD 通道配置填充通道名称和单位
            RefreshChannelConfigInfo();
        }

        /// <summary>
        /// 从 IRecipePoolService 加载全局变量，构建可链接变量列表（仅 Double 类型）
        /// </summary>
        private async Task LoadGlobalVariablesAsync()
        {
            try
            {
                var poolId = _recipePoolService.CurrentPoolName;
                if (string.IsNullOrEmpty(poolId)) return;

                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);

                // 构建字符串下拉选项（向后兼容）
                var options = new ObservableCollection<string> { "" };
                foreach (var v in variables)
                    options.Add(v.Name);
                GlobalVariableOptions = options;

                // 筛选 Double 类型变量，供 GlobalVariableLinkControl 使用
                var doubleVars = variables
                    .Where(v => v.Type == GlobalVariableType.Double)
                    .ToList();

                // 取消旧变量的值变更订阅
                UnsubscribeVariableValueChanges();

                // 就地更新集合而非替换引用，避免行的绑定失效
                if (LinkableGlobalVariables == null)
                    LinkableGlobalVariables = new ObservableCollection<GlobalVariable>(doubleVars);
                else
                {
                    LinkableGlobalVariables.Clear();
                    foreach (var v in doubleVars)
                        LinkableGlobalVariables.Add(v);
                }

                SubscribeVariableValueChanges();

                // 无论首次加载还是事件触发，均刷新所有行的链接显示值
                RefreshLinkedVariableDisplayValues();
            }
            catch
            {
                GlobalVariableOptions = new ObservableCollection<string> { "" };
                LinkableGlobalVariables = new ObservableCollection<GlobalVariable>();
            }
        }

        /// <summary>
        /// 全局变量变更事件回调，重新加载变量列表并刷新显示
        /// </summary>
        private void OnGlobalVariablesChanged(string poolId)
        {
            LoadGlobalVariablesAsync().ConfigureAwait(false);
        }

        /// <summary> 订阅 Double 类型变量的 PropertyChanged，值变化时实时刷新行显示 </summary>
        private void SubscribeVariableValueChanges()
        {
            if (LinkableGlobalVariables == null) return;
            foreach (var v in LinkableGlobalVariables)
                v.PropertyChanged += OnGlobalVariablePropertyChanged;
        }

        /// <summary> 取消所有 Double 变量的 PropertyChanged 订阅 </summary>
        private void UnsubscribeVariableValueChanges()
        {
            if (LinkableGlobalVariables == null) return;
            foreach (var v in LinkableGlobalVariables)
                v.PropertyChanged -= OnGlobalVariablePropertyChanged;
        }

        /// <summary>
        /// 全局变量 Value 属性变化时，刷新链接了该变量的行的 CurrentForce 显示值
        /// </summary>
        private void OnGlobalVariablePropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(GlobalVariable.Value)) return;
            if (sender is GlobalVariable gv)
            {
                foreach (var row in ChannelRows)
                {
                    if (string.Equals(row.LinkedVariableName, gv.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (double.TryParse(gv.Value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double val))
                            row.CurrentForce = val;
                    }
                }
            }
        }

        /// <summary>
        /// 刷新所有行的链接显示值（解决构造函数竞态问题）
        /// </summary>
        private void RefreshLinkedVariableDisplayValues()
        {
            if (LinkableGlobalVariables == null) return;
            foreach (var row in ChannelRows)
            {
                if (!string.IsNullOrEmpty(row.LinkedVariableName))
                {
                    var gv = LinkableGlobalVariables
                        .Cast<GlobalVariable>()
                        .FirstOrDefault(v => string.Equals(v.Name, row.LinkedVariableName, StringComparison.OrdinalIgnoreCase));
                    if (gv != null && double.TryParse(gv.Value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double val))
                        row.CurrentForce = val;
                }
            }
        }

        /// <summary> 取消链接选中行的全局变量 </summary>
        private void OnUnlinkVariable()
        {
            if (SelectedChannelRow != null)
                SelectedChannelRow.LinkedVariableName = null;
        }

        /// <summary> 新增通道行，Sub 自动递增 </summary>
        private void OnAddChannelRow()
        {
            int nextSub = ChannelRows.Count > 0 ? ChannelRows.Max(r => r.Sub) + 1 : 1;
            ChannelRows.Add(new SeekChannelRow
            {
                Sub = nextSub,
                LinkedChannel = ChannelRows.Count,
                TargetForce = 0.3,
                ForceMin = -2.0,
                ForceMax = 2.0,
                Description = ""
            });
        }

        /// <summary> 删除选中通道行并重排序号 </summary>
        private void OnDeleteChannelRow()
        {
            if (SelectedChannelRow == null) return;
            ChannelRows.Remove(SelectedChannelRow);
            ReorderSubNumbers();
        }

        /// <summary> 重排通道行 Sub 序号，保持连续 </summary>
        private void ReorderSubNumbers()
        {
            for (int i = 0; i < ChannelRows.Count; i++)
                ChannelRows[i].Sub = i + 1;
        }

        /// <summary> 从 JSON 文件导入通道配置 </summary>
        private void OnImport()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = L("SeekDetail_JsonFileFilter"),
                Title = L("SeekDetail_ImportDialogTitle")
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var rows = JsonConvert.DeserializeObject<ObservableCollection<SeekChannelRow>>(json);
                if (rows != null)
                {
                    ChannelRows = rows;
                    ReorderSubNumbers();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format(L("SeekDetail_Error_ImportFailed"), ex.Message));
            }
        }

        /// <summary> 将通道配置导出为 JSON 文件 </summary>
        private void OnExport()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = L("SeekDetail_JsonFileFilter"),
                Title = L("SeekDetail_ExportDialogTitle"),
                FileName = "SeekChannelConfig"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var json = JsonConvert.SerializeObject(ChannelRows, Formatting.Indented);
                File.WriteAllText(dialog.FileName, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format(L("SeekDetail_Error_ExportFailed"), ex.Message));
            }
        }

        /// <summary> 启动力值实时刷新定时器（100ms 间隔） </summary>
        private void OnStartRefresh()
        {
            if (_refreshTimer != null) return;

            IsRefreshing = true;
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _refreshTimer.Tick += async (s, e) => await RefreshForceValuesAsync();
            _refreshTimer.Start();
        }

        private void OnStopRefresh()
        {
            StopRefreshTimer();
        }

        /// <summary>
        /// 刷新所有通道的实时力值，根据通道号计算从站号和通道偏移
        /// </summary>
        private async Task RefreshForceValuesAsync()
        {
            try
            {
                foreach (var row in ChannelRows)
                {
                    double force = await _motionService.ReadAnalogChannelAsync(0, row.LinkedChannel);
                    row.CurrentForce = force;
                    row.IsForceInRange = force >= row.ForceMin && force <= row.ForceMax;
                }
                // 同步通道配置信息（名称、单位）
                RefreshChannelConfigInfo();
            }
            catch
            {
            }
        }

        /// <summary>
        /// 从 IADValueConverter 获取已加载的通道配置，更新每行的名称和单位
        /// </summary>
        private void RefreshChannelConfigInfo()
        {
            if (_adConverter == null) return;
            foreach (var row in ChannelRows)
            {
                var cfg = _adConverter.GetChannelConfig(row.LinkedChannel);
                if (cfg != null)
                {
                    row.ChannelUnit = cfg.Unit ?? "N";
                    row.ChannelName = cfg.Name ?? string.Empty;
                }
            }
        }

        /// <summary> 停止刷新定时器并重置刷新状态 </summary>
        private void StopRefreshTimer()
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer = null;
            }
            IsRefreshing = false;
        }

        /// <summary> 关闭弹窗，停止力值刷新 </summary>
        private void OnClose()
        {
            StopRefreshTimer();
            RequestClose?.Invoke(false);
        }

        /// <summary> 保存通道行到 Step.SeekDetail（不关闭弹窗） </summary>
        private void OnSaveOnly()
        {
            if (_step != null)
            {
                _step.SeekDetail = new SeekDetail
                {
                    ChannelRows = new ObservableCollection<SeekChannelRow>(ChannelRows)
                };
            }
        }

        /// <summary> 保存通道行到 Step.SeekDetail 并关闭弹窗 </summary>
        private void OnSave()
        {
            if (_step != null)
            {
                _step.SeekDetail = new SeekDetail
                {
                    ChannelRows = new ObservableCollection<SeekChannelRow>(ChannelRows)
                };
            }
            OnClose();
        }

        public void Dispose()
        {
            StopRefreshTimer();
            UnsubscribeVariableValueChanges();
            _eventAggregator.GetEvent<GlobalVariablesChangedEvent>()
                .Unsubscribe(OnGlobalVariablesChanged);
        }
    }
}

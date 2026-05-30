using Core.Models;
using MotionControl.Interfaces;
using Newtonsoft.Json;
using Prism.Commands;
using Prism.Mvvm;
using Recipe.Interfaces;
using Core.Abstraction;  // ILocalizationService 接口
using StationTasks.Models;
using System;
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
    public class SeekDetailViewModel : BindableBase, IDisposable
    {
        private readonly IMotionService _motionService;
        private readonly IRecipePoolService _recipePoolService;
        private ProcessStep _step;
        private DispatcherTimer _refreshTimer;
        private bool _isRefreshing;
        private readonly ILocalizationService _localizationService;

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

        public SeekDetailViewModel(
            IMotionService motionService,
            IRecipePoolService recipePoolService,
            ILocalizationService localizationService)
        {
            _motionService = motionService;
            _recipePoolService = recipePoolService;
            _localizationService = localizationService;

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
        }

        /// <summary>
        /// 从 IRecipePoolService 加载全局变量名称列表，构建下拉选项
        /// </summary>
        private async Task LoadGlobalVariablesAsync()
        {
            try
            {
                var poolId = _recipePoolService.CurrentPoolName;
                if (string.IsNullOrEmpty(poolId)) return;

                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
                var options = new ObservableCollection<string> { "" };
                foreach (var v in variables)
                    options.Add(v.Name);
                GlobalVariableOptions = options;
            }
            catch
            {
                GlobalVariableOptions = new ObservableCollection<string> { "" };
            }
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
            }
            catch
            {
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
            try
            {
                var session = MaterialDesignThemes.Wpf.DialogHost.GetDialogSession("MainDialogHost");
                session?.Close(false);
            }
            catch (InvalidOperationException) { }
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
        }
    }
}

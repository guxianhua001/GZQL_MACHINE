using Prism.Commands;
using Prism.Mvvm;
using StationTasks.Models;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Module.ViewModels
{
    /// <summary>
    /// WAIT/DELAY 步骤编辑器 ViewModel，支持延时时长配置、时间单位切换、实时换算显示
    /// </summary>
    public class WaitDetailViewModel : BindableBase
    {
        private ProcessStep _step;

        /// <summary> 当前编辑的工艺步骤，设置时自动初始化延时配置 </summary>
        public ProcessStep Step
        {
            get => _step;
            set
            {
                if (SetProperty(ref _step, value))
                {
                    InitializeFromStep();
                    RaisePropertyChanged(nameof(StepDescription));
                }
            }
        }

        /// <summary> 步骤描述文本（用于标题栏显示） </summary>
        public string StepDescription => _step == null ? "—" : $"Seq{_step.Seq} → {_step.CompFeature ?? "—"}";

        private double _delayValue = 1000;
        /// <summary> 延时数值 </summary>
        public double DelayValue
        {
            get => _delayValue;
            set
            {
                if (SetProperty(ref _delayValue, value))
                    UpdateEstimatedDisplay();
            }
        }

        private string _selectedTimeUnit = "ms";
        /// <summary> 选中的时间单位 </summary>
        public string SelectedTimeUnit
        {
            get => _selectedTimeUnit;
            set
            {
                if (SetProperty(ref _selectedTimeUnit, value))
                    UpdateEstimatedDisplay();
            }
        }

        /// <summary> 时间单位选项 </summary>
        public ObservableCollection<string> TimeUnitOptions { get; } = new ObservableCollection<string> { "ms", "s", "min" };

        private string _description;
        /// <summary> 延时说明 </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private string _estimatedDisplay;
        /// <summary> 估算显示文本（如 "≈ 1.000 s"） </summary>
        public string EstimatedDisplay
        {
            get => _estimatedDisplay;
            set => SetProperty(ref _estimatedDisplay, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand CloseCommand { get; }

        public WaitDetailViewModel()
        {
            SaveCommand = new DelegateCommand(OnSave);
            CloseCommand = new DelegateCommand(OnClose);
        }

        /// <summary>
        /// 从 Step.WaitDetail 加载配置，为空则创建默认值
        /// </summary>
        private void InitializeFromStep()
        {
            if (_step == null) return;

            if (_step.WaitDetail == null)
            {
                _step.WaitDetail = new WaitDetail();
            }

            var detail = _step.WaitDetail;
            DelayValue = detail.DelayMs;
            SelectedTimeUnit = detail.TimeUnit ?? "ms";
            Description = detail.Description;
            UpdateEstimatedDisplay();
        }

        /// <summary>
        /// 根据当前数值和单位换算为实际毫秒数
        /// </summary>
        private double GetActualDelayMs()
        {
            return SelectedTimeUnit switch
            {
                "s" => DelayValue * 1000,
                "min" => DelayValue * 60000,
                _ => DelayValue
            };
        }

        /// <summary>
        /// 更新估算显示文本
        /// </summary>
        private void UpdateEstimatedDisplay()
        {
            double actualMs = GetActualDelayMs();

            if (actualMs >= 60000)
                EstimatedDisplay = $"≈ {actualMs / 60000.0:F2} min ({actualMs / 1000.0:F1} s)";
            else if (actualMs >= 1000)
                EstimatedDisplay = $"≈ {actualMs / 1000.0:F3} s ({actualMs:F0} ms)";
            else
                EstimatedDisplay = $"≈ {actualMs:F0} ms";
        }

        /// <summary>
        /// 保存当前配置到 Step.WaitDetail 并关闭弹窗
        /// </summary>
        private void OnSave()
        {
            if (_step == null) return;

            if (_step.WaitDetail == null)
                _step.WaitDetail = new WaitDetail();

            _step.WaitDetail.DelayMs = DelayValue;
            _step.WaitDetail.TimeUnit = SelectedTimeUnit;
            _step.WaitDetail.Description = Description;

            try
            {
                var session = MaterialDesignThemes.Wpf.DialogHost.GetDialogSession("MainDialogHost");
                session?.Close(true);
            }
            catch (InvalidOperationException) { }
        }

        /// <summary>
        /// 关闭弹窗不保存
        /// </summary>
        private void OnClose()
        {
            try
            {
                var session = MaterialDesignThemes.Wpf.DialogHost.GetDialogSession("MainDialogHost");
                session?.Close(false);
            }
            catch (InvalidOperationException) { }
        }
    }
}

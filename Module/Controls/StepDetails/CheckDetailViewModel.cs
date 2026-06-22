using System;
using Core.Models;
using Core.Abstraction;
using StationTasks.Models;
using Prism.Commands;
using Prism.Mvvm;
using System.Collections.ObjectModel;
using System.Linq;

namespace Module.ViewModels
{
    public class CheckDetailViewModel : BindableBase, IDialogCloseable
    {
        private ProcessStep _currentStep;
        private ObservableCollection<CheckItem> _checkItems;
        private bool _overallResult;

        /// <summary>请求关闭对话框时触发</summary>
        public event Action<object> RequestClose;

        /// <summary>是否可以关闭对话框</summary>
        public bool CanCloseDialog() => true;

        // ---------- 新增：PASS/FAIL 相关属性 ----------
        private ObservableCollection<ProcessStep> _allSteps;
        private OnPassAction _selectedPassAction;
        private int _passJumpStepSeq;
        private OnFailAction _selectedFailAction;
        private int _failJumpStepSeq;
        private int _maxRetries;
        private OnMaxExceededAction _selectedMaxExceededAction;

        public ObservableCollection<ProcessStep> AllSteps
        {
            get => _allSteps;
            set => SetProperty(ref _allSteps, value);
        }

        public OnPassAction SelectedPassAction
        {
            get => _selectedPassAction;
            set
            {
                if (SetProperty(ref _selectedPassAction, value))
                    RaisePropertyChanged(nameof(ShowPassJumpStep));
            }
        }

        public bool ShowPassJumpStep => SelectedPassAction == OnPassAction.SkipTo;

        public int PassJumpStepSeq
        {
            get => _passJumpStepSeq;
            set => SetProperty(ref _passJumpStepSeq, value);
        }

        public OnFailAction SelectedFailAction
        {
            get => _selectedFailAction;
            set
            {
                if (SetProperty(ref _selectedFailAction, value))
                    RaisePropertyChanged(nameof(ShowFailJumpStep));
            }
        }

        public bool ShowFailJumpStep => SelectedFailAction == OnFailAction.SkipTo;

        public int FailJumpStepSeq
        {
            get => _failJumpStepSeq;
            set => SetProperty(ref _failJumpStepSeq, value);
        }

        public int MaxRetries
        {
            get => _maxRetries;
            set => SetProperty(ref _maxRetries, value);
        }

        public ObservableCollection<OnMaxExceededAction> MaxExceededActions { get; } = new ObservableCollection<OnMaxExceededAction>
        {
            OnMaxExceededAction.Alarm, OnMaxExceededAction.Stop, OnMaxExceededAction.Continue
        };

        public OnMaxExceededAction SelectedMaxExceededAction
        {
            get => _selectedMaxExceededAction;
            set => SetProperty(ref _selectedMaxExceededAction, value);
        }

        // 原有表格属性
        public ObservableCollection<CheckItem> CheckItems
        {
            get => _checkItems;
            set => SetProperty(ref _checkItems, value);
        }

        public bool OverallResult
        {
            get => _overallResult;
            set => SetProperty(ref _overallResult, value);
        }

        public DelegateCommand SaveCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public CheckDetailViewModel()
        {
            SaveCommand = new DelegateCommand(SaveToStep);
            CancelCommand = new DelegateCommand(() => { /* 关闭逻辑 */ });
            LoadAllSteps();      // 加载步骤列表
            InitializeCheckItems();
            InitializePassFailSettings();
        }

        private void LoadAllSteps()
        {
            // 模拟步骤数据（实际应从 IDataService 获取）
            AllSteps = new ObservableCollection<ProcessStep>
            {
                new ProcessStep { Seq = 1, Step = StepType.GOTO, CompFeature = "HOME", SiteFeature = "HOME" },
                new ProcessStep { Seq = 2, Step = StepType.PICK, CompFeature = "ACTUATOR", SiteFeature = "PICK_POS" },
                new ProcessStep { Seq = 3, Step = StepType.VISION, CompFeature = "slot", SiteFeature = "TAB_001" },
                new ProcessStep { Seq = 4, Step = StepType.VISION, CompFeature = "3d_scan", SiteFeature = "SCAN_AREA" },
                new ProcessStep { Seq = 5, Step = StepType.GOTO, CompFeature = "ASSY_001", SiteFeature = "PIN_ADJ" },
                new ProcessStep { Seq = 6, Step = StepType.GOTO, CompFeature = "ASSY_001", SiteFeature = "DISPENSE" },
            };
        }

        private void InitializeCheckItems()
        {
            CheckItems = new ObservableCollection<CheckItem>
            {
                new CheckItem { Index = 1, IsChecked = true, DataLink = "Slot.Z", Value = 0.0, Status = false,
                                LowerLimit = -0.05, UpperLimit = 0.05, LowerTolerance = -0.02, UpperTolerance = 0.02 },
                new CheckItem { Index = 2, IsChecked = true, DataLink = "Tab.Z", Value = 0.0, Status = false,
                                LowerLimit = -0.05, UpperLimit = 0.05, LowerTolerance = -0.02, UpperTolerance = 0.02 }
            };
            foreach (var item in CheckItems)
                item.PropertyChanged += OnCheckItemPropertyChanged;
            EvaluateOverallResult();
        }

        private void InitializePassFailSettings()
        {
            SelectedPassAction = OnPassAction.Continue;
            PassJumpStepSeq = 24;
            SelectedFailAction = OnFailAction.Retry;
            FailJumpStepSeq = 18;
            MaxRetries = 3;
            SelectedMaxExceededAction = OnMaxExceededAction.Alarm;
        }

        private void OnCheckItemPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CheckItem.IsChecked) || e.PropertyName == nameof(CheckItem.Value) ||
                e.PropertyName == nameof(CheckItem.LowerLimit) || e.PropertyName == nameof(CheckItem.UpperLimit))
            {
                var item = sender as CheckItem;
                if (item != null)
                    item.Status = EvaluateItemStatus(item);
                EvaluateOverallResult();
            }
        }

        private bool EvaluateItemStatus(CheckItem item)
        {
            if (!item.IsChecked) return true;
            return item.Value >= item.LowerLimit && item.Value <= item.UpperLimit;
        }

        private void EvaluateOverallResult()
        {
            OverallResult = CheckItems.Where(i => i.IsChecked).All(i => i.Status);
        }

        public void LoadFromStep(ProcessStep step)
        {
            _currentStep = step;
            if (step.CheckDetail != null)
            {
                // 加载表格数据
                if (step.CheckDetail.CheckItems != null)
                {
                    CheckItems.Clear();
                    foreach (var ci in step.CheckDetail.CheckItems)
                    {
                        var item = new CheckItem
                        {
                            Index = ci.Index,
                            IsChecked = ci.IsChecked,
                            DataLink = ci.DataLink,
                            Value = ci.Value,
                            Status = ci.Status,
                            LowerLimit = ci.LowerLimit,
                            UpperLimit = ci.UpperLimit,
                            LowerTolerance = ci.LowerTolerance,
                            UpperTolerance = ci.UpperTolerance
                        };
                        item.PropertyChanged += OnCheckItemPropertyChanged;
                        CheckItems.Add(item);
                    }
                    EvaluateOverallResult();
                }
                else
                {
                    InitializeCheckItems();
                }

                // 加载 PASS/FAIL 配置
                SelectedPassAction = step.CheckDetail.OnPassAction;
                PassJumpStepSeq = step.CheckDetail.OnPassJumpStepSeq;
                SelectedFailAction = step.CheckDetail.OnFailAction;
                FailJumpStepSeq = step.CheckDetail.OnFailJumpStepSeq;
                MaxRetries = step.CheckDetail.MaxRetries;
                SelectedMaxExceededAction = step.CheckDetail.OnMaxExceeded;
            }
            else
            {
                InitializeCheckItems();
                InitializePassFailSettings();
            }
        }

        public void SaveToStep()
        {
            if (_currentStep == null) return;
            if (_currentStep.CheckDetail == null)
                _currentStep.CheckDetail = new CheckDetail();

            // 保存表格数据
            _currentStep.CheckDetail.CheckItems = CheckItems.Select(item => new CheckItem
            {
                Index = item.Index,
                IsChecked = item.IsChecked,
                DataLink = item.DataLink,
                Value = item.Value,
                Status = item.Status,
                LowerLimit = item.LowerLimit,
                UpperLimit = item.UpperLimit,
                LowerTolerance = item.LowerTolerance,
                UpperTolerance = item.UpperTolerance
            }).ToList();

            // 保存 PASS/FAIL 配置
            _currentStep.CheckDetail.OnPassAction = SelectedPassAction;
            _currentStep.CheckDetail.OnPassJumpStepSeq = PassJumpStepSeq;
            _currentStep.CheckDetail.OnFailAction = SelectedFailAction;
            _currentStep.CheckDetail.OnFailJumpStepSeq = FailJumpStepSeq;
            _currentStep.CheckDetail.MaxRetries = MaxRetries;
            _currentStep.CheckDetail.OnMaxExceeded = SelectedMaxExceededAction;
        }
    }
}
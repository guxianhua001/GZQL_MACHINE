using Core.Abstraction;
using Core.Models;
using Core.Utilities;
using Recipe.Interfaces;
using StationTasks.Events;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using StationTasks.Models;
using System.Windows;

namespace Module.ViewModels
{
    /// <summary> 数据看板弹窗 ViewModel </summary>
    public class DataDashboardViewModel : BindableBase
    {
        private readonly IEventAggregator _ea;
        private readonly ILoggerService _logger;
        private readonly IFormulaEvaluator _formulaEvaluator;
        private readonly IRecipePoolService _recipePoolService;
        private readonly ILocalizationService _localizationService;
        private SubscriptionToken _showToken;

        private const string DialogIdentifier = "MainDialogHost";

        /// <summary> 当前关联的步骤（用于持久化 ImagePath 等属性） </summary>
        private ProcessStep _currentStep;

        public ObservableCollection<DashboardField> Fields { get; } = new();
        public ObservableCollection<DashboardAnnotation> Annotations { get; } = new();

        /// <summary> 全局变量列表（供表达式编辑器选择） </summary>
        public ObservableCollection<GlobalVariable> GlobalVariables { get; } = new();

        private DashboardField _selectedField;
        public DashboardField SelectedField
        {
            get => _selectedField;
            set
            {
                if (SetProperty(ref _selectedField, value))
                    (DeleteFieldCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            }
        }

        private string _imagePath;
        public string ImagePath
        {
            get => _imagePath;
            set
            {
                if (_imagePath != value)
                {
                    _imagePath = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(ImagePath)));
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(DiagramImage)));
                }
            }
        }

        private BitmapImage _diagramImage;
        public BitmapImage DiagramImage
        {
            get => _diagramImage;
            set
            {
                if (_diagramImage != value)
                {
                    _diagramImage = value;
                    OnPropertyChanged(new PropertyChangedEventArgs(nameof(DiagramImage)));
                }
            }
        }

        // ========== 命令 ==========
        public DelegateCommand ConfirmCommand { get; }
        public DelegateCommand ConfirmNGCommand { get; }
        public DelegateCommand AddFieldCommand { get; }
        public DelegateCommand DeleteFieldCommand { get; }
        public DelegateCommand RefreshValuesCommand { get; }
        public DelegateCommand LoadImageCommand { get; }
        public DelegateCommand<DashboardField> EditFormulaCommand { get; }

        // 表达式编辑弹窗相关
        private bool _isFormulaEditorOpen;
        public bool IsFormulaEditorOpen
        {
            get => _isFormulaEditorOpen;
            set => SetProperty(ref _isFormulaEditorOpen, value);
        }

        private DashboardField _editingField;
        public DashboardField EditingField
        {
            get => _editingField;
            set => SetProperty(ref _editingField, value);
        }

        private string _editingFormula;
        public string EditingFormula
        {
            get => _editingFormula;
            set => SetProperty(ref _editingFormula, value);
        }

        /// <summary> 当前编辑字段的类型（数值型/条件型） </summary>
        private DashboardFieldType _editingFieldType;
        public DashboardFieldType EditingFieldType
        {
            get => _editingFieldType;
            set => SetProperty(ref _editingFieldType, value);
        }

        public DelegateCommand InsertGlobalVariableCommand { get; }
        public DelegateCommand ConfirmFormulaCommand { get; }
        public DelegateCommand CancelFormulaCommand { get; }

        private GlobalVariable _selectedGlobalVariable;
        public GlobalVariable SelectedGlobalVariable
        {
            get => _selectedGlobalVariable;
            set
            {
                if (SetProperty(ref _selectedGlobalVariable, value))
                    (InsertGlobalVariableCommand as DelegateCommand)?.RaiseCanExecuteChanged();
            }
        }

        /// <summary> 是否启用人工确认（绑定到 DashboardStepDetail.RequireManualConfirm） </summary>
        private bool _requireManualConfirm = true;
        public bool RequireManualConfirm
        {
            get => _requireManualConfirm;
            set => SetProperty(ref _requireManualConfirm, value);
        }

        /// <summary> 是否为执行模式（运行时弹出 vs 编辑器预览） </summary>
        private bool _isExecutionMode;
        public bool IsExecutionMode
        {
            get => _isExecutionMode;
            set => SetProperty(ref _isExecutionMode, value);
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

        public DataDashboardViewModel(
            IEventAggregator ea,
            ILoggerService logger,
            IFormulaEvaluator formulaEvaluator,
            IRecipePoolService recipePoolService,
            ILocalizationService localizationService)
        {
            _ea = ea;
            _logger = logger;
            _formulaEvaluator = formulaEvaluator;
            _recipePoolService = recipePoolService;
            _localizationService = localizationService;

            ConfirmCommand = new DelegateCommand(OnConfirm);
            ConfirmNGCommand = new DelegateCommand(OnConfirmNG);
            AddFieldCommand = new DelegateCommand(OnAddField);
            DeleteFieldCommand = new DelegateCommand(OnDeleteField, () => SelectedField != null);
            RefreshValuesCommand = new DelegateCommand(OnRefreshValues);
            LoadImageCommand = new DelegateCommand(OnLoadImage);
            EditFormulaCommand = new DelegateCommand<DashboardField>(OnEditFormula);

            InsertGlobalVariableCommand = new DelegateCommand(OnInsertGlobalVariable, () => SelectedGlobalVariable != null);
            ConfirmFormulaCommand = new DelegateCommand(OnConfirmFormula);
            CancelFormulaCommand = new DelegateCommand(OnCancelFormula);

            SelectedField = null;
            (DeleteFieldCommand as DelegateCommand)?.RaiseCanExecuteChanged();

            _showToken = _ea.GetEvent<ShowDashboardEvent>().Subscribe(OnShowDashboard);

            LoadGlobalVariables();
        }

        // ========== 数据加载 ==========

        private void OnShowDashboard(ShowDashboardPayload payload)
        {
            _currentStep = payload.Step;

            Fields.Clear();
            foreach (var f in payload.Fields) Fields.Add(f);

            Annotations.Clear();
            foreach (var a in payload.Annotations) Annotations.Add(a);

            ImagePath = payload.ImagePath;
            LoadDiagramImage(ImagePath);

            RequireManualConfirm = _currentStep?.DashboardDetail?.RequireManualConfirm ?? true;
            IsExecutionMode = payload.IsExecutionMode;

            LoadGlobalVariables();
            RefreshFieldValues();

            _logger.Info(string.Format(L("DataDetail_Log_DataLoaded"), Fields.Count, RequireManualConfirm));
        }

        /// <summary>
        /// 加载全局变量列表（供表达式编辑器选择）
        /// 通过 IRecipePoolService 直接获取当前配方池的全局变量
        /// </summary>
        private async void LoadGlobalVariables()
        {
            GlobalVariables.Clear();
            try
            {
                var poolId = _recipePoolService?.CurrentPoolId;
                if (string.IsNullOrEmpty(poolId))
                {
                    _logger.Warn(L("DataDetail_Log_PoolIdEmpty"));
                    return;
                }

                var variables = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
                if (variables != null)
                {
                    foreach (var v in variables.OrderBy(v => v.Name))
                    {
                        GlobalVariables.Add(new GlobalVariable { Name = v.Name, Type = v.Type, Value = v.Value });
                    }
                }
                _logger.Info(string.Format(L("DataDetail_Log_VarLoaded"), GlobalVariables.Count));
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(L("DataDetail_Log_VarLoadFailed"), ex.Message));
            }
        }

        /// <summary>
        /// 加载示意图
        /// </summary>
        public void LoadDiagramImage(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                DiagramImage = null;
                return;
            }

            try
            {
                string fullPath = path;
                if (!Path.IsPathRooted(path))
                    fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);

                if (File.Exists(fullPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    DiagramImage = bitmap;
                    _logger.Info(string.Format(L("DataDetail_Log_DiagramSuccess"), fullPath));
                }
                else
                {
                    DiagramImage = null;
                    _logger.Warn(string.Format(L("DataDetail_Log_DiagramNotExist"), fullPath));
                }
            }
            catch (Exception ex)
            {
                DiagramImage = null;
                _logger.Error(string.Format(L("DataDetail_Log_DiagramLoadFailed"), ex.Message));
            }
        }

        /// <summary>
        /// 持久化 ImagePath 到步骤配置
        /// </summary>
        private void SaveImagePath(string path)
        {
            ImagePath = path;
            if (_currentStep?.DashboardDetail != null)
            {
                _currentStep.DashboardDetail.ImagePath = path;
            }
        }

        // ========== 命令实现 ==========

        /// <summary> 双击示意图区域加载图片 </summary>
        private void OnLoadImage()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = L("DataDetail_SelectDiagramTitle"),
                Filter = L("DataDetail_ImageFileFilter"),
                FilterIndex = 1
            };

            if (dialog.ShowDialog() == true)
            {
                string selectedPath = dialog.FileName;

                // 复制到应用 Images 目录
                string imagesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "Dashboard");
                Directory.CreateDirectory(imagesDir);

                string fileName = Path.GetFileName(selectedPath);
                string destPath = Path.Combine(imagesDir, fileName);

                // 避免文件名冲突
                int counter = 1;
                while (File.Exists(destPath) && destPath != selectedPath)
                {
                    fileName = $"{Path.GetFileNameWithoutExtension(selectedPath)}_{counter}{Path.GetExtension(selectedPath)}";
                    destPath = Path.Combine(imagesDir, fileName);
                    counter++;
                }

                if (selectedPath != destPath)
                    File.Copy(selectedPath, destPath, true);

                // 使用相对路径持久化
                string relativePath = Path.Combine("Images", "Dashboard", fileName);
                SaveImagePath(relativePath);
                LoadDiagramImage(destPath);

                _logger.Info(string.Format(L("DataDetail_Log_DiagramSaved"), relativePath));
            }
        }

        /// <summary> 添加数据行 </summary>
        private void OnAddField()
        {
            int maxSeq = Fields.Count > 0 ? Fields.Max(f => f.Seq) : 0;
            var newField = new DashboardField
            {
                Seq = maxSeq + 1,
                DisplayName = string.Format(L("DataDetail_DefaultFieldName"), maxSeq + 1),
                Formula = "",
                Format = "F3"
            };
            Fields.Add(newField);
            SelectedField = newField;

            // 持久化到步骤配置
            SyncFieldsToStep();
        }

        /// <summary> 删除选中数据行 </summary>
        private void OnDeleteField()
        {
            if (SelectedField == null) return;

            int index = Fields.IndexOf(SelectedField);
            Fields.Remove(SelectedField);

            // 重新编号
            for (int i = 0; i < Fields.Count; i++)
                Fields[i].Seq = i + 1;

            // 选中相邻行
            if (Fields.Count > 0)
                SelectedField = Fields[Math.Min(index, Fields.Count - 1)];

            SyncFieldsToStep();
        }

        /// <summary> 刷新数值 </summary>
        private void OnRefreshValues()
        {
            LoadGlobalVariables();
            RefreshFieldValues();
            _logger.Info(L("DataDetail_Log_ValuesRefreshed"));
        }

        /// <summary> 打开表达式编辑器 </summary>
        private void OnEditFormula(DashboardField field)
        {
            if (field == null) return;

            EditingField = field;
            EditingFormula = field.Formula ?? "";
            EditingFieldType = field.FieldType;
            IsFormulaEditorOpen = true;
        }

        /// <summary> 插入全局变量到表达式 </summary>
        private void OnInsertGlobalVariable()
        {
            if (SelectedGlobalVariable == null) return;
            EditingFormula += $"@GV:{SelectedGlobalVariable.Name}";
            (InsertGlobalVariableCommand as DelegateCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary> 确认表达式 </summary>
        private void OnConfirmFormula()
        {
            if (EditingField != null)
            {
                EditingField.FieldType = EditingFieldType;
                EditingField.Formula = EditingFormula;
                RefreshFieldValues();
                SyncFieldsToStep();
            }
            IsFormulaEditorOpen = false;
        }

        /// <summary> 取消表达式编辑 </summary>
        private void OnCancelFormula()
        {
            IsFormulaEditorOpen = false;
        }

        /// <summary>
        /// 刷新所有字段的数值：数值型用 Evaluate，条件型用 EvaluateCondition
        /// </summary>
        private void RefreshFieldValues()
        {
            if (_formulaEvaluator == null) return;

            var variables = GetGlobalVariables();

            foreach (var field in Fields)
            {
                try
                {
                    if (string.IsNullOrEmpty(field.Formula))
                    {
                        field.CurrentValue = 0;
                        field.ConditionResult = null;
                        continue;
                    }

                    if (field.FieldType == DashboardFieldType.Condition)
                    {
                        field.ConditionResult = _formulaEvaluator.EvaluateCondition(field.Formula, variables);
                        field.CurrentValue = field.ConditionResult == true ? 1 : 0;
                    }
                    else
                    {
                        field.CurrentValue = _formulaEvaluator.Evaluate(field.Formula, variables);
                        if (!string.IsNullOrEmpty(field.ConditionFormula))
                            field.ConditionResult = _formulaEvaluator.EvaluateCondition(field.ConditionFormula, variables);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(string.Format(L("DataDetail_Log_EvalFailed"), field.DisplayName, ex.Message));
                }
            }
        }

        /// <summary>
        /// 获取全局变量字典（供公式求值器使用）
        /// </summary>
        private Dictionary<string, string> GetGlobalVariables()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var poolId = _recipePoolService?.CurrentPoolId;
                if (string.IsNullOrEmpty(poolId)) return result;

                var variables = _recipePoolService.LoadGlobalVariablesAsync(poolId).GetAwaiter().GetResult();
                if (variables != null)
                {
                    foreach (var v in variables)
                        result[v.Name] = v.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(string.Format(L("DataDetail_Log_GetVarFailed"), ex.Message));
            }
            return result;
        }

        /// <summary>
        /// 同步字段到步骤配置（持久化）
        /// </summary>
        private void SyncFieldsToStep()
        {
            if (_currentStep?.DashboardDetail != null)
            {
                _currentStep.DashboardDetail.Fields = Fields.ToList();
                _currentStep.DashboardDetail.RequireManualConfirm = RequireManualConfirm;
            }
        }

        /// <summary> 确认继续 </summary>
        private void OnConfirm()
        {
            SyncFieldsToStep();
            _ea.GetEvent<DashboardConfirmedEvent>().Publish(DashboardConfirmResult.Continue);
            _logger.Info(L("DataDetail_Log_UserConfirmed"));
            try
            {
                var session = MaterialDesignThemes.Wpf.DialogHost.GetDialogSession(DialogIdentifier);
                session?.Close(true);
            }
            catch (InvalidOperationException) { }
        }

        /// <summary> 确认NG </summary>
        private void OnConfirmNG()
        {
            SyncFieldsToStep();
            _ea.GetEvent<DashboardConfirmedEvent>().Publish(DashboardConfirmResult.NG);
            _logger.Info(L("DataDetail_Log_UserConfirmedNG"));
            try
            {
                var session = MaterialDesignThemes.Wpf.DialogHost.GetDialogSession(DialogIdentifier);
                session?.Close(true);
            }
            catch (InvalidOperationException) { }
        }
    }
}

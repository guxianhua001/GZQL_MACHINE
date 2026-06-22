using Core.Models;
using Newtonsoft.Json.Linq;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using Recipe.Events;
using Recipe.Interfaces;
using Recipe.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Collections.Specialized;

namespace Recipe.ViewModels
{
    /// <summary>
    /// 全局变量页面 ViewModel：支持分组管理与分组过滤。
    /// 分组以字符串标识，空字符串（GlobalVariable.DefaultGroupKey）为默认分组，
    /// 向后兼容旧数据（Group 为 null 时归入默认分组）。
    /// </summary>
    public class GlobalVariablesViewModel : BindableBase
    {
        /// <summary>"全部"分组的 sentinel 值（不可与真实分组名冲突，用零宽空格 ZWSP）</summary>
        private const string AllGroupsKey = "\u200B";

        private readonly IRecipePoolService _recipePoolService; // 获取当前配方池ID
        private readonly IEventAggregator _eventAggregator;
        private readonly IDialogService _dialogService; // 用于弹窗输入分组名
        private ObservableCollection<GlobalVariable> _variables;
        private GlobalVariable _selectedVariable;
        private ObservableCollection<string> _groups;
        private string _selectedGroupFilter; // AllGroupsKey=显示全部, ""=默认分组, 其他=自定义分组名

        public string CurrentPoolName => _recipePoolService.CurrentPoolName;

        public GlobalVariablesViewModel(IRecipePoolService recipePoolManager,
            IEventAggregator eventAggregator,
            IDialogService dialogService)
        {
            _recipePoolService = recipePoolManager;
            _eventAggregator = eventAggregator;
            _dialogService = dialogService;
            // Save Pool 保存前同步当前页面编辑的全局变量，避免保存后重载旧值覆盖新建/修改内容
            _eventAggregator.GetEvent<SaveGlobalVariablesEvent>().Subscribe(OnSavePoolRequested);
            // 订阅配方池切换事件，触发重新加载
            _eventAggregator.GetEvent<RecipePoolChangedEvent>().Subscribe(OnPoolChanged);
            // 订阅全局变量被外部更新事件（如Vision/SCAN数据解析后自动写入），重新加载最新数据
            _eventAggregator.GetEvent<GlobalVariablesChangedEvent>().Subscribe(OnGlobalVariablesChanged);

            AddCommand = new DelegateCommand(OnAdd);
            DeleteCommand = new DelegateCommand(OnDelete, () => SelectedVariable != null);
            MoveUpCommand = new DelegateCommand(OnMoveUp, () => CanMoveUp());
            MoveDownCommand = new DelegateCommand(OnMoveDown, () => CanMoveDown());
            SaveCommand = new DelegateCommand(OnUserSave);
            AddGroupCommand = new DelegateCommand(OnAddGroup);
            DeleteGroupCommand = new DelegateCommand(OnDeleteGroup, () => CanDeleteSelectedGroup());
            RenameGroupCommand = new DelegateCommand(OnRenameGroup, () => CanRenameSelectedGroup());

            // 初始化分组集合，默认分组始终存在
            Groups = new ObservableCollection<string> { GlobalVariable.DefaultGroupKey };
            // 默认显示全部变量
            SelectedGroupFilter = AllGroupsKey;

            LoadVariables();
        }

        /// <summary>
        /// 完整变量列表（用于持久化与上移/下移操作）。
        /// </summary>
        public ObservableCollection<GlobalVariable> Variables
        {
            get => _variables;
            set
            {
                if (SetProperty(ref _variables, value))
                {
                    RefreshGroups();
                    // 集合实例变更后重新获取默认视图并应用过滤
                    VariablesView = CollectionViewSource.GetDefaultView(_variables);
                    VariablesView.Filter = FilterVariable;
                    RaisePropertyChanged(nameof(VariablesView));
                }
            }
        }

        /// <summary>
        /// 过滤后的变量视图（根据 SelectedGroupFilter 过滤），DataGrid 绑定此视图。
        /// </summary>
        public ICollectionView VariablesView { get; private set; }

        public GlobalVariable SelectedVariable
        {
            get => _selectedVariable;
            set
            {
                if (SetProperty(ref _selectedVariable, value))
                {
                    (DeleteCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    (MoveUpCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    (MoveDownCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>
        /// 所有分组名集合（始终包含默认分组 DefaultGroupKey=""）。
        /// 自定义分组为非空字符串，用户可通过 AddGroup/DeleteGroup/RenameGroup 管理。
        /// </summary>
        public ObservableCollection<string> Groups
        {
            get => _groups;
            set
            {
                // 解除旧集合变更订阅，绑定新集合并刷新过滤选项
                if (_groups != null)
                    _groups.CollectionChanged -= OnGroupsCollectionChanged;
                if (SetProperty(ref _groups, value))
                {
                    if (_groups != null)
                        _groups.CollectionChanged += OnGroupsCollectionChanged;
                    RefreshGroupFilterOptions();
                }
            }
        }

        /// <summary>
        /// 分组过滤下拉框选项集合。第一项为 null（显示"全部"），后续为 Groups 中的分组名。
        /// 通过 ItemTemplate + DataTrigger 在 UI 中将 null 显示为"全部"、空字符串显示为"默认分组"。
        /// </summary>
        public ObservableCollection<string> GroupFilterOptions { get; } = new ObservableCollection<string>();

        /// <summary>
        /// 当前选中的分组过滤器。
        /// AllGroupsKey = 显示全部分组变量；"" = 默认分组；其他 = 对应自定义分组。
        /// </summary>
        public string SelectedGroupFilter
        {
            get => _selectedGroupFilter;
            set
            {
                if (SetProperty(ref _selectedGroupFilter, value))
                {
                    VariablesView?.Refresh();
                    (DeleteGroupCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                    (RenameGroupCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand AddCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand AddGroupCommand { get; }
        public ICommand DeleteGroupCommand { get; }
        public ICommand RenameGroupCommand { get; }

        // 类型下拉选项
        public ObservableCollection<GlobalVariableType> TypeOptions { get; } =
            new ObservableCollection<GlobalVariableType>(System.Enum.GetValues(typeof(GlobalVariableType)).Cast<GlobalVariableType>());

        private void RefreshIndices()
        {
            for (int i = 0; i < Variables.Count; i++)
                Variables[i].Index = i + 1;
        }

        private void OnAdd()
        {
            // 新变量归入当前选中的分组；若选了"全部"则归入默认分组
            string targetGroup = SelectedGroupFilter == AllGroupsKey ? GlobalVariable.DefaultGroupKey : SelectedGroupFilter;

            string baseName = "NewVar";
            string newName = baseName;
            int counter = 1;
            while (Variables.Any(v => v.Name == newName))
            {
                newName = $"{baseName}{counter++}";
            }
            var newVar = new GlobalVariable
            {
                Index = Variables.Count + 1,
                Name = newName,
                Type = GlobalVariableType.Double,      // 默认类型
                Value = "0",                        // 默认值 0
                Comment = "",
                Group = targetGroup
            };
            Variables.Add(newVar);
            SelectedVariable = newVar;
            RefreshIndices();
        }

        private void OnDelete()
        {
            if (SelectedVariable != null)
            {
                Variables.Remove(SelectedVariable);
                RefreshIndices();
            }
        }

        private bool CanMoveUp() => SelectedVariable != null && Variables.IndexOf(SelectedVariable) > 0;

        private void OnMoveUp()
        {
            int idx = Variables.IndexOf(SelectedVariable);
            Variables.Move(idx, idx - 1);
            RefreshIndices();
        }

        private bool CanMoveDown() => SelectedVariable != null && Variables.IndexOf(SelectedVariable) < Variables.Count - 1;

        private void OnMoveDown()
        {
            int idx = Variables.IndexOf(SelectedVariable);
            Variables.Move(idx, idx + 1);
            RefreshIndices();
        }

        /// <summary>
        /// ICollectionView 过滤回调：根据 SelectedGroupFilter 过滤变量。
        /// AllGroupsKey 显示全部；其他值精确匹配分组名。
        /// </summary>
        private bool FilterVariable(object obj)
        {
            if (SelectedGroupFilter == AllGroupsKey) return true;
            var v = (GlobalVariable)obj;
            return (v.Group ?? GlobalVariable.DefaultGroupKey) == SelectedGroupFilter;
        }

        /// <summary>
        /// 从变量列表同步分组集合：确保默认分组存在，并补充变量中出现的所有分组。
        /// 只增不减，用户手动创建的空分组不会被自动移除。
        /// </summary>
        private void RefreshGroups()
        {
            if (Variables == null) return;
            // 确保默认分组始终在第一位
            if (!Groups.Contains(GlobalVariable.DefaultGroupKey))
                Groups.Insert(0, GlobalVariable.DefaultGroupKey);
            // 补充变量中存在但 Groups 中缺失的分组
            foreach (var v in Variables)
            {
                var g = v.Group ?? GlobalVariable.DefaultGroupKey;
                if (!Groups.Contains(g))
                    Groups.Add(g);
            }
        }

        /// <summary>
        /// Groups 集合变更时同步刷新过滤选项下拉框。
        /// </summary>
        private void OnGroupsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshGroupFilterOptions();
        }

        /// <summary>
        /// 重建分组过滤下拉框选项：第一项为 AllGroupsKey（"全部"），后续为 Groups 中的分组名。
        /// 尽量保留当前选中项；若当前选中项已不存在则回退到"全部"。
        /// </summary>
        private void RefreshGroupFilterOptions()
        {
            var previouslySelected = SelectedGroupFilter;
            GroupFilterOptions.Clear();
            GroupFilterOptions.Add(AllGroupsKey); // "全部"
            foreach (var g in Groups)
                GroupFilterOptions.Add(g);
            // 恢复之前的选择；若已不存在则默认"全部"
            if (previouslySelected == AllGroupsKey || GroupFilterOptions.Contains(previouslySelected))
                SelectedGroupFilter = previouslySelected;
            else
                SelectedGroupFilter = AllGroupsKey;
        }

        /// <summary>
        /// 添加新分组：弹窗输入分组名，校验非空、不与默认分组冲突、不重复。
        /// </summary>
        private void OnAddGroup()
        {
            var parameters = new DialogParameters();
            _dialogService.ShowDialog("SimpleInputDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var name = result.Parameters.GetValue<string>("value")?.Trim();
                    // 不允许空名称（与默认分组标识冲突）
                    if (string.IsNullOrEmpty(name)) return;
                    // 不允许重复
                    if (Groups.Contains(name)) return;
                    Groups.Add(name);
                    // 自动切换到新分组
                    SelectedGroupFilter = name;
                }
            });
        }

        /// <summary>
        /// 默认分组不可删除；删除自定义分组时，其中变量移至默认分组。
        /// </summary>
        private bool CanDeleteSelectedGroup() =>
            SelectedGroupFilter != AllGroupsKey && SelectedGroupFilter != GlobalVariable.DefaultGroupKey;

        private void OnDeleteGroup()
        {
            if (!CanDeleteSelectedGroup()) return;
            var groupToDelete = SelectedGroupFilter;
            // 将该分组下所有变量移至默认分组
            foreach (var v in Variables)
            {
                if ((v.Group ?? GlobalVariable.DefaultGroupKey) == groupToDelete)
                    v.Group = GlobalVariable.DefaultGroupKey;
            }
            Groups.Remove(groupToDelete);
            // 切换到默认分组
            SelectedGroupFilter = GlobalVariable.DefaultGroupKey;
        }

        /// <summary>
        /// 默认分组不可重命名；重命名自定义分组时同步更新其中变量的 Group 字段。
        /// </summary>
        private bool CanRenameSelectedGroup() =>
            SelectedGroupFilter != AllGroupsKey && SelectedGroupFilter != GlobalVariable.DefaultGroupKey;

        private void OnRenameGroup()
        {
            if (!CanRenameSelectedGroup()) return;
            var oldName = SelectedGroupFilter;
            var parameters = new DialogParameters { { "value", oldName } };
            _dialogService.ShowDialog("SimpleInputDialog", parameters, result =>
            {
                if (result.Result == ButtonResult.OK)
                {
                    var newName = result.Parameters.GetValue<string>("value")?.Trim();
                    if (string.IsNullOrEmpty(newName)) return;
                    if (newName == oldName) return;
                    if (Groups.Contains(newName)) return;
                    // 更新变量分组
                    foreach (var v in Variables)
                    {
                        if ((v.Group ?? GlobalVariable.DefaultGroupKey) == oldName)
                            v.Group = newName;
                    }
                    // 更新 Groups 集合（保持位置）
                    var idx = Groups.IndexOf(oldName);
                    Groups[idx] = newName;
                    SelectedGroupFilter = newName;
                }
            });
        }

        /// <summary>
        /// 统一加载逻辑：加载后规范化 Group 字段（null → 默认分组），并刷新分组集合与视图。
        /// </summary>
        private async Task ReloadVariables(string poolId)
        {
            if (string.IsNullOrEmpty(poolId)) return;

            var list = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
            // 向后兼容：旧数据 Group 为 null，统一归入默认分组
            foreach (var v in list)
            {
                if (v.Group == null)
                    v.Group = GlobalVariable.DefaultGroupKey;
            }
            Variables = new ObservableCollection<GlobalVariable>(list);
            RefreshIndices();
        }

        /// <summary>
        /// 配方池切换时触发
        /// </summary>
        private async void OnPoolChanged(string newPoolName)
        {
            // 直接用事件传过来的池名称加载，不依赖 CurrentPoolId（避免时序问题）
            await ReloadVariables(newPoolName);
        }

        /// <summary>
        /// 全局变量被外部更新时触发（如SCAN数据解析后、脚本执行后自动写入全局变量）
        /// 重新从存储加载最新数据，确保窗口始终显示最新值
        /// </summary>
        private async void OnGlobalVariablesChanged(string poolId)
        {
            // 事件发布者可能传 poolName 或 poolId，需同时匹配 CurrentPoolName 和 CurrentPoolId
            if (poolId == _recipePoolService.CurrentPoolName || poolId == _recipePoolService.CurrentPoolId)
            {
                await ReloadVariables(_recipePoolService.CurrentPoolName);
            }
        }

        private async void LoadVariables()
        {
            await ReloadVariables(_recipePoolService.CurrentPoolName);
        }

        private async Task Save()
        {
            try
            {
                var poolId = _recipePoolService.CurrentPoolName;
                await _recipePoolService.SaveGlobalVariablesAsync(poolId, Variables);
                _eventAggregator.GetEvent<GlobalVariablesChangedEvent>().Publish(poolId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存全局变量失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 用户手动点击保存按钮：将当前编辑的数据持久化
        /// </summary>
        private async void OnUserSave()
        {
            try { await Save(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnUserSave异常: {ex.Message}");
            }
        }

        /// <summary>
        /// Save Pool 触发时，将当前页面内存中的全局变量同步到待保存配方池对象。
        /// </summary>
        private void OnSavePoolRequested(RecipePool pool)
        {
            if (pool == null || pool.Name != _recipePoolService.CurrentPoolName)
                return;

            try
            {
                RefreshIndices();
                pool.GlobalVariables = Variables?.Select(CloneGlobalVariable).ToList() ?? new System.Collections.Generic.List<GlobalVariable>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnSavePoolRequested异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 克隆全局变量，避免保存对象与界面编辑对象共享引用导致后续编辑误改已保存池对象。
        /// </summary>
        private static GlobalVariable CloneGlobalVariable(GlobalVariable source)
        {
            return new GlobalVariable
            {
                Index = source.Index,
                Name = source.Name,
                Type = source.Type,
                Value = source.Value,
                Comment = source.Comment,
                Group = source.Group ?? GlobalVariable.DefaultGroupKey
            };
        }
    }
}

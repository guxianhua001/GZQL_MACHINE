using Core.Models;
using Newtonsoft.Json.Linq;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Recipe.Events;
using Recipe.Interfaces;
using Recipe.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Recipe.ViewModels
{
    public class GlobalVariablesViewModel : BindableBase
    {
        private readonly IRecipePoolService _recipePoolService; // 获取当前配方池ID
        private ObservableCollection<GlobalVariable> _variables;
        private GlobalVariable _selectedVariable;
        public string CurrentPoolName => _recipePoolService.CurrentPoolName;
        private readonly IEventAggregator _eventAggregator;

        public GlobalVariablesViewModel(IRecipePoolService recipePoolManager,
            IEventAggregator eventAggregator)
        {
            _recipePoolService = recipePoolManager;
            _eventAggregator = eventAggregator;
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
            LoadVariables();
        }

        public ObservableCollection<GlobalVariable> Variables
        {
            get => _variables;
            set => SetProperty(ref _variables, value);
        }

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

        public ICommand AddCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand SaveCommand { get; }

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
                Comment = ""
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
        /// 统一加载逻辑
        /// </summary>
        private async Task ReloadVariables(string poolId)
        {
            if (string.IsNullOrEmpty(poolId)) return;

            var list = await _recipePoolService.LoadGlobalVariablesAsync(poolId);
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
                Comment = source.Comment
            };
        }
    }
}
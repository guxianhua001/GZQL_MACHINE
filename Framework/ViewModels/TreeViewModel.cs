using Core.Abstraction;
using Core.Models;
using Core.Services;
using Framework.Mvvm;
using Prism.Commands;
using Prism.Regions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Framework.ViewModels
{
    public class TreeViewModel : RegionViewModelBase
    {
        private readonly ITreeConfigService _treeConfigService;
        private readonly IRegionManager _regionManager;
        private readonly ILocalizationService _localizationService;

        private string _title = "设备树";
        private TreeNode _selectedItem;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public ObservableCollection<TreeNode> TreeData { get; set; }

        public TreeNode SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    NavigationTreeView_SelectedItemChanged(value);
                }
            }
        }

        public DelegateCommand LoadTreeCommand { get; }
        public DelegateCommand<TreeNode> NodeDoubleClickCommand { get; }

        public TreeViewModel(IRegionManager regionManager,
                           ITreeConfigService treeConfigService,
                           ILocalizationService localizationService)
            : base(regionManager)
        {
            _regionManager = regionManager;
            _treeConfigService = treeConfigService;
            _localizationService = localizationService;

            TreeData = new ObservableCollection<TreeNode>();

            LoadTreeCommand = new DelegateCommand(async () => await LoadTreeDataAsync());
            NodeDoubleClickCommand = new DelegateCommand<TreeNode>(OnNodeDoubleClick);

            // 订阅语言变化事件
            _localizationService.LanguageChanged += OnLanguageChanged;

            // 初始化加载树数据
            LoadTreeCommand.Execute();


            int abc = _localizationService.GetHashCode();
        }

        private async Task LoadTreeDataAsync()
        {
            try
            {
                var nodes = await _treeConfigService.LoadTreeStructureAsync();
                TreeData.Clear();
                foreach (var node in nodes)
                {
                    // 处理本地化显示名称
                    ProcessNodeLocalization(node);
                    TreeData.Add(node);
                }

                // 通知树数据已加载，需要重新绑定显示名称
                NotifyTreeDataLocalizationChanged();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"加载树配置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 处理节点本地化
        private void ProcessNodeLocalization(TreeNode node)
        {
            // 如果没有设置本地化键，使用默认的生成规则
            if (string.IsNullOrEmpty(node.LocalizationKey))
            {
                // 使用路径作为本地化键：将路径中的斜杠替换为下划线
                node.LocalizationKey = $"Tree_{node.Path?.Replace("/", "_")}";
            }
            // 设置显示名称（调用GetLocalizedNodeName方法）
            node.DisplayName = GetLocalizedNodeName(node);
            // 递归处理子节点
            foreach (var child in node.Children)
            {
                ProcessNodeLocalization(child);
            }
        }

        public string GetLocalizedNodeName(TreeNode node)
        {
            if (node == null) return string.Empty;

            // 如果有本地化键，使用本地化服务获取翻译 
            if (!string.IsNullOrEmpty(node.LocalizationKey))
            {
                return _localizationService.GetResourceOrDefault(node.LocalizationKey, node.Name);
            }

            // 否则使用原名称 
            return node.Name;
        }

        // 语言变化处理
        private void OnLanguageChanged(object sender, LanguageChangedEventArgs e)
        {
            // 1. 更新所有节点的 DisplayName 值
            UpdateAllNodesDisplayName();
        }

        private void UpdateAllNodesDisplayName()
        {
            foreach (var node in GetAllNodes(TreeData))
            {
                // 重新计算 DisplayName（这会触发 SetProperty，自动通知 UI）
                node.DisplayName = GetLocalizedNodeName(node);
            }
        }


        // 通知树数据本地化变化
        private void NotifyTreeDataLocalizationChanged()
        {
            // 遍历所有节点，触发DisplayName属性变化通知
            foreach (var node in GetAllNodes(TreeData))
            {
                node.NotifyDisplayNameChanged();
            }
        }

        // 获取所有节点（递归）
        private IEnumerable<TreeNode> GetAllNodes(IEnumerable<TreeNode> nodes)
        {
            if (nodes == null) yield break;

            foreach (var node in nodes)
            {
                yield return node;

                foreach (var child in GetAllNodes(node.Children))
                {
                    yield return child;
                }
            }
        }

        private void NavigationTreeView_SelectedItemChanged(TreeNode selectedNode)
        {
            if (selectedNode == null || string.IsNullOrEmpty(selectedNode.ViewType))
                return;

            // 使用ViewType进行导航
            _regionManager.RequestNavigate("TreeRegion", selectedNode.ViewType, result =>
            {
                if ((bool)!result.Result)
                {
                    // 记录或弹出错误信息
                    System.Diagnostics.Debug.WriteLine($"导航失败: {result.Error?.Message}");
                    MessageBox.Show($"导航失败: {result.Error?.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }

        private void OnNodeDoubleClick(TreeNode node)
        {
            if (node != null)
            {
                node.IsExpanded = !node.IsExpanded;
            }
        }

        // 添加节点的方法
        private DelegateCommand<TreeNode> _addChildNodeCommand;
        public DelegateCommand<TreeNode> AddChildNodeCommand =>
            _addChildNodeCommand ??= new DelegateCommand<TreeNode>(ExecuteAddChildNodeCommand);

        private void ExecuteAddChildNodeCommand(TreeNode parentNode)
        {
            if (parentNode == null) return;

            var newNode = new TreeNode("新节点")
            {
                Path = $"{parentNode.Path}/NewNode",
                // 为新节点生成本地化键
                LocalizationKey = $"Tree_{parentNode.Path?.Replace("/", "_")}_NewNode"
            };

            // 设置新节点的显示名称
            newNode.DisplayName = GetLocalizedNodeName(newNode);

            parentNode.Children.Add(newNode);

            // 通知显示名称变化
            newNode.NotifyDisplayNameChanged();

            // 保存配置
            _ = _treeConfigService.SaveTreeStructureAsync(TreeData.ToList());
        }

        // 删除节点的方法
        private DelegateCommand<TreeNode> _removeNodeCommand;
        public DelegateCommand<TreeNode> RemoveNodeCommand =>
            _removeNodeCommand ??= new DelegateCommand<TreeNode>(ExecuteRemoveNodeCommand);

        private void ExecuteRemoveNodeCommand(TreeNode nodeToRemove)
        {
            if (nodeToRemove == null) return;

            if (RemoveNodeFromParent(TreeData, nodeToRemove))
            {
                // 保存配置
                _ = _treeConfigService.SaveTreeStructureAsync(TreeData.ToList());
            }
        }

        // 方法1：使用 IList<TreeNode> 作为参数类型
        private bool RemoveNodeFromParent(IList<TreeNode> nodes, TreeNode nodeToRemove)
        {
            if (nodes.Contains(nodeToRemove))
            {
                nodes.Remove(nodeToRemove);
                return true;
            }

            foreach (var node in nodes)
            {
                if (RemoveNodeFromParent(node.Children, nodeToRemove))
                    return true;
            }

            return false;
        }

        // 方法2：或者重载方法，同时支持 ObservableCollection 和 IList
        //private bool RemoveNodeFromParent(ObservableCollection<TreeNode> nodes, TreeNode nodeToRemove)
        //{
        //    return RemoveNodeFromParent((IList<TreeNode>)nodes, nodeToRemove);
        //}

        // 清理资源
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);

            if (_localizationService != null)
            {
                //_localizationService.LanguageChanged -= OnLanguageChanged; // 移除事件订阅 否则会导致语言不切换
            }
        }
    }
}
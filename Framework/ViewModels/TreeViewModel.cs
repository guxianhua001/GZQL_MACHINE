// Framework/ViewModels/TreeViewViewModel.cs
using Core.Abstraction;
using Core.Models;
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

        public TreeViewModel(IRegionManager regionManager, ITreeConfigService treeConfigService)
            : base(regionManager)
        {
            _regionManager = regionManager;
            _treeConfigService = treeConfigService;

            TreeData = new ObservableCollection<TreeNode>();

            LoadTreeCommand = new DelegateCommand(async () => await LoadTreeDataAsync());
            NodeDoubleClickCommand = new DelegateCommand<TreeNode>(OnNodeDoubleClick);

            // 初始化加载树数据
            LoadTreeCommand.Execute();
        }

        private async Task LoadTreeDataAsync()
        {
            try
            {
                var nodes = await _treeConfigService.LoadTreeStructureAsync();
                TreeData.Clear();
                foreach (var node in nodes)
                {
                    TreeData.Add(node);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"加载树配置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NavigationTreeView_SelectedItemChanged(TreeNode selectedNode)
        {
            if (selectedNode == null || string.IsNullOrEmpty(selectedNode.ViewType))
                return;

            // 使用ViewType进行导航
            _regionManager.RequestNavigate("TreeRegion", selectedNode.ViewType);
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
                Path = $"{parentNode.Path}/NewNode"
            };

            parentNode.Children.Add(newNode);
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
        private bool RemoveNodeFromParent(ObservableCollection<TreeNode> nodes, TreeNode nodeToRemove)
        {
            return RemoveNodeFromParent((IList<TreeNode>)nodes, nodeToRemove);
        }
    }
}
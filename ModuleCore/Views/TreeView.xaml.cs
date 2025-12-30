using ModuleCore.ViewModels;
using Prism.Regions;
using SmarterMotion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;

namespace ModuleCore.Views
{
    /// <summary>
    /// TreeView.xaml 的交互逻辑
    /// </summary>
    public partial class TreeView : UserControl
    {
        TreeViewViewModel _viewModel;
        public TreeView(IRegionManager regionManager)
        {
            _viewModel = new TreeViewViewModel(regionManager);
            DataContext = _viewModel;
            InitializeComponent();
            RegionManager.SetRegionManager(TreeRegion, regionManager);
            this.Loaded += TreeViewView_Loaded;
        }
        /// <summary>
        /// TreesView's SelectedItem is read-only. Hence we can't bind it. There is a way to obtain a selected item.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) => _viewModel.SelectedItem = e.NewValue;

        private void TreeViewView_Loaded(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() => ExpandAllTreeViewItems(myTreeView)), DispatcherPriority.Loaded);
        }

        private void ExpandAllTreeViewItems(ItemsControl itemsControl)
        {
            if (itemsControl == null) return;

            foreach (var item in itemsControl.Items)
            {
                var treeViewItem = itemsControl.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (treeViewItem != null)
                {
                    // 展开当前节点
                    treeViewItem.IsExpanded = true;

                    // 强制更新布局以生成子容器
                    treeViewItem.UpdateLayout();

                    // 延迟递归处理子项
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ExpandAllTreeViewItems(treeViewItem);
                    }), DispatcherPriority.Loaded);
                }
            }
        }

    }
}

using Core.Abstraction;
using Framework.ViewModels;
using Prism.Regions;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Framework.Views
{
    /// <summary>
    /// TreeView.xaml 的交互逻辑
    /// </summary>
    public partial class TreeView : UserControl
    {
        private readonly TreeViewModel _viewModel;

        public TreeView(IRegionManager regionManager, ITreeConfigService treeConfigService, ILocalizationService localizationService)
        {
            _viewModel = new TreeViewModel(regionManager, treeConfigService, localizationService);
            DataContext = _viewModel;
            InitializeComponent();
            RegionManager.SetRegionManager(TreeRegion, regionManager);
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            Loaded += TreeViewView_Loaded;
        }

        /// <summary>
        /// TreesView's SelectedItem is read-only. Hence we can't bind it. There is a way to obtain a selected item.
        /// </summary>
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) =>
            _viewModel.SelectedItem = (Core.Models.TreeNode)e.NewValue;

        private void TreeViewView_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTreePanelLayout(_viewModel.IsTreePanelExpanded);
            Dispatcher.BeginInvoke((Action)(() => ExpandAllTreeViewItems(myTreeView)), DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 根据展开/折叠状态调整左侧列宽（折叠时宽度为 0，展开时恢复上次宽度）
        /// </summary>
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TreeViewModel.IsTreePanelExpanded))
            {
                ApplyTreePanelLayout(_viewModel.IsTreePanelExpanded);
            }
        }

        private void ApplyTreePanelLayout(bool expanded)
        {
            if (TreePanelColumn == null)
                return;

            if (expanded)
            {
                TreePanelColumn.MinWidth = 200;
                TreePanelColumn.MaxWidth = 600;
                TreePanelColumn.Width = new GridLength(
                    _viewModel.TreePanelWidth > 0 ? _viewModel.TreePanelWidth : 280,
                    GridUnitType.Pixel);
            }
            else
            {
                var currentWidth = TreePanelColumn.ActualWidth;
                if (currentWidth > 0)
                    _viewModel.TreePanelWidth = currentWidth;

                TreePanelColumn.MinWidth = 0;
                TreePanelColumn.MaxWidth = double.PositiveInfinity;
                TreePanelColumn.Width = new GridLength(0);
            }
        }

        private void ExpandAllTreeViewItems(ItemsControl itemsControl)
        {
            if (itemsControl == null) return;

            foreach (var item in itemsControl.Items)
            {
                var treeViewItem = itemsControl.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (treeViewItem != null)
                {
                    treeViewItem.IsExpanded = true;
                    treeViewItem.UpdateLayout();

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ExpandAllTreeViewItems(treeViewItem);
                    }), DispatcherPriority.Loaded);
                }
            }
        }
    }
}

using System.Windows.Controls;
using System.Windows;
using System.Collections;
using System.Linq;

namespace ModuleCore.Extensions
{

    public static class SelectExtension
    {
        private static readonly DependencyProperty DataGridSelectionWatcherProperty =
            DependencyProperty.RegisterAttached(
                "DataGridSelectionWatcher",
                typeof(SelectionWatcher),
                typeof(SelectExtension),
                new PropertyMetadata(null));

        public static readonly DependencyProperty SelectedItemsProperty =
            DependencyProperty.RegisterAttached(
                "SelectedItems",
                typeof(IList),
                typeof(SelectExtension),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedItemsChanged));

        public static IList GetSelectedItems(DependencyObject obj) =>
            (IList)obj.GetValue(SelectedItemsProperty);

        public static void SetSelectedItems(DependencyObject obj, IList value) =>
            obj.SetValue(SelectedItemsProperty, value);

        private static void OnSelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DataGrid dataGrid) return;

            // 获取或创建监视器
            var watcher = (SelectionWatcher)dataGrid.GetValue(DataGridSelectionWatcherProperty);
            if (watcher == null)
            {
                watcher = new SelectionWatcher(dataGrid);
                dataGrid.SetValue(DataGridSelectionWatcherProperty, watcher);
            }

            // 更新绑定
            watcher.Bind(e.NewValue as IList);
        }

        private class SelectionWatcher
        {
            private readonly DataGrid _dataGrid;
            private IList _boundList;
            private bool _isUpdatingUI;
            private bool _isUpdatingBound;

            public SelectionWatcher(DataGrid dataGrid)
            {
                _dataGrid = dataGrid;
                _dataGrid.SelectionChanged += OnSelectionChanged;
            }

            public void Bind(IList list)
            {
                _boundList = list;
                if (_boundList == null) return;

                // 初始同步
                UpdateUISelection();
            }

            private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
            {
                if (_isUpdatingUI || _boundList == null) return;

                _isUpdatingBound = true;
                try
                {
                    // 添加新增项
                    foreach (var addedItem in e.AddedItems)
                    {
                        if (!_boundList.Contains(addedItem))
                        {
                            _boundList.Add(addedItem);
                        }
                    }

                    // 移除删除项
                    foreach (var removedItem in e.RemovedItems)
                    {
                        _boundList.Remove(removedItem);
                    }
                }
                finally
                {
                    _isUpdatingBound = false;
                }
            }

            public void UpdateUISelection()
            {
                if (_isUpdatingBound || _boundList == null) return;

                _isUpdatingUI = true;
                try
                {
                    // 清除不在绑定列表中的选择
                    var itemsToRemove = _dataGrid.SelectedItems.Cast<object>()
                        .Where(item => !_boundList.Contains(item))
                        .ToList();

                    foreach (var item in itemsToRemove)
                    {
                        _dataGrid.SelectedItems.Remove(item);
                    }

                    // 添加绑定列表中的新选择
                    var itemsToAdd = _boundList.Cast<object>()
                        .Where(item => !_dataGrid.SelectedItems.Contains(item))
                        .ToList();

                    foreach (var item in itemsToAdd)
                    {
                        _dataGrid.SelectedItems.Add(item);
                    }
                }
                finally
                {
                    _isUpdatingUI = false;
                }
            }
        }
    }

}

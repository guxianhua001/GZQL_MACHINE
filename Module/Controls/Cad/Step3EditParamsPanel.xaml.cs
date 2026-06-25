using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Module.Controls
{
    /// <summary>Step3：编辑参数面板——轨迹段 DataGrid + 批量操作 + ROI 工具 + 参数编辑</summary>
    public partial class Step3EditParamsPanel : UserControl
    {
        private INotifyPropertyChanged _currentViewModel;

        public Step3EditParamsPanel()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        /// <summary>
        /// DataContext 变化时切换 PropertyChanged 订阅，
        /// 用于监听 SelectedSegment 变化并自动滚动 DataGrid 到选中行
        /// </summary>
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 取消旧 ViewModel 的订阅
            if (_currentViewModel != null)
            {
                _currentViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            // 订阅新 ViewModel 的 PropertyChanged
            if (e.NewValue is INotifyPropertyChanged newVm)
            {
                _currentViewModel = newVm;
                _currentViewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
            else
            {
                _currentViewModel = null;
            }
        }

        /// <summary>选中段变化时，自动滚动 DataGrid 到对应行（便于在众多线段中定位高亮项）</summary>
        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 仅处理 SelectedSegment 变化（全属性刷新时 PropertyName 为空，也一并处理）
            if (e.PropertyName != "SelectedSegment" && !string.IsNullOrEmpty(e.PropertyName))
                return;

            // 延迟到后台优先级执行，确保 DataGrid 已完成选中项的视觉更新
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var item = SegmentsDataGrid.SelectedItem;
                if (item != null)
                {
                    SegmentsDataGrid.ScrollIntoView(item);
                }
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// 点位序号列的 TextBlock.Loaded 事件——通过行索引显示序号
        /// </summary>
        private void OnPointNumberLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock tb)
            {
                var row = FindAncestor<DataGridRow>(tb);
                if (row != null)
                {
                    int idx = row.GetIndex();
                    tb.Text = (idx + 1).ToString();
                }
            }
        }

        /// <summary>向上查找指定类型的祖先元素</summary>
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T result)
                    return result;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}

using Framework.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace Framework.Views
{
    /// <summary>
    /// PositionView.xaml 的交互逻辑
    /// </summary>
    public partial class PositionView : UserControl
    {
        public static readonly DependencyProperty TaskIdProperty =
       DependencyProperty.Register("TaskId", typeof(int), typeof(PositionView),
           new FrameworkPropertyMetadata(0,
               FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, // 添加双向绑定
               OnTaskIdChanged));

        public static readonly DependencyProperty AxisIdGroupProperty =
            DependencyProperty.Register("AxisIdGroup", typeof(int[]), typeof(PositionView),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnAxisIdGroupChanged)); // 添加变更回调
        public PositionView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }
        public int TaskId
        {
            get => (int)GetValue(TaskIdProperty);
            set => SetValue(TaskIdProperty, value);
        }

        public int[] AxisIdGroup
        {
            get => (int[])GetValue(AxisIdGroupProperty);
            set => SetValue(AxisIdGroupProperty, value);
        }
        private static void OnTaskIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = d as PositionView;
            if (view?.ViewModel == null) return;

            // 同步到ViewModel
            view.ViewModel.TaskId = (int)e.NewValue;
        }

        private static void OnAxisIdGroupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var view = d as PositionView;
            if (view?.ViewModel == null) return;

            // 同步到ViewModel
            view.ViewModel.AxisIdGroup = (int[])e.NewValue;
        }
        private void DataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 获取选中的行
            var selectedRow = DataGrid.SelectedItem as PositionDisplayItem;
            if (selectedRow != null)
            {
                // 获取第一列的值（Name 属性）
                var currentPointName = selectedRow.Name;

                // 将值赋给 ViewModel 中的 m_CurrentPointName
                var viewModel = DataContext as PositionViewModel;
                if (viewModel != null)
                {
                    viewModel.CurrentPointName = currentPointName;
                }
            }
        }
        private void DataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            // 获取当前单元格信息
            var currentCell = DataGrid.CurrentCell;
            if (currentCell != null && currentCell.Column != null)
            {
                int columnIndex = currentCell.Column.DisplayIndex;
                // 获取当前列的头名称
                var columnName = DataGrid.Columns[currentCell.Column.DisplayIndex].Header.ToString();
                var viewModel = DataContext as PositionViewModel;
                if (viewModel != null)
                {
                    viewModel.SelectedPositionIndex = columnIndex;
                    viewModel.CurrentAxisName = columnName;
                }
            }
        }
        private PositionViewModel ViewModel => DataContext as PositionViewModel;



        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is PositionViewModel viewModel)
            {
                // 订阅加载完成事件
                viewModel.LoadCompleted += OnViewModelLoadCompleted;
                GenerateColumns(viewModel.AxisHeaders);
            }
            if (e.OldValue is PositionViewModel oldViewModel)
            {
                oldViewModel.LoadCompleted -= OnViewModelLoadCompleted;
            }
        }
        private void OnViewModelLoadCompleted()
        {
            if (DataContext is PositionViewModel viewModel)
            {
                // 数据加载完成后重新生成列
                GenerateColumns(viewModel.AxisHeaders);
            }
        }
        private void AxisHeaders_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (DataContext is PositionViewModel viewModel)
            {
                GenerateColumns(viewModel.AxisHeaders);
            }
        }
        // 动态生成列
        private void GenerateColumns(IEnumerable<string> axisHeaders)
        {
            DataGrid.Columns.Clear();

            // 固定列：名称
            DataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "名称",
                Binding = new Binding("Name"),
                Width = 150
            });
            // 动态列：轴位置
            int index = 0;
            foreach (var header in axisHeaders)
            {
                DataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = header,
                    Binding = new Binding($"Positions[{index}]") { StringFormat = "F3" },
                    Width = 120
                });
                index++;
            }
            // 固定列：注释
            DataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "注释",
                Binding = new Binding("Comment"),
                Width = 150
            });
            // 强制刷新 DataGrid
            DataGrid.Items.Refresh();
            // 输出列信息
            Console.WriteLine($"生成的列数量：{DataGrid.Columns.Count}");
            foreach (var column in DataGrid.Columns)
            {
                Console.WriteLine($"[{column.Header}] {column.GetType().Name}");
            }
        }

    }
}

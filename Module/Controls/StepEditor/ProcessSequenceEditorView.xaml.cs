using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Module.Models;
using Module.ViewModels;
using StationTasks.Models;

namespace Module.Views
{
    public partial class ProcessSequenceEditorView : UserControl
    {
        // ========== 拖拽排序状态字段 ==========
        /// <summary> 拖拽起始鼠标位置（用于判断是否超过系统拖拽阈值） </summary>
        private Point _dragStartPoint;
        /// <summary> 待拖拽的动作步骤（鼠标按下时记录） </summary>
        private ProcessStep _draggedStep;
        /// <summary> 待拖拽的任务（鼠标按下时记录，与 _draggedStep 互斥） </summary>
        private TaskItem _draggedTask;
        /// <summary> 是否正在执行拖拽（避免重复触发） </summary>
        private bool _isDragging;

        public ProcessSequenceEditorView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 视图卸载时清理：取消 DataGrid 编辑状态、折叠 TreeViewItem，
        /// 防止 MaterialDesign 动画在卸载过程中遇到 NaN 尺寸导致导航崩溃。
        /// 注意：StepsDataGrid 定义在 DataTemplate 内部，无法通过 x:Name 直接访问，
        /// 需通过可视化树递归查找。
        /// </summary>
        private void ProcessSequenceEditorView_Unloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 递归查找所有 DataGrid 并取消编辑状态，
                // 避免编辑模板中的 ComboBox 卸载时 Underline 动画遇到 NaN
                CancelAllDataGridEdits(this);

                // 折叠所有 TreeViewItem，终止可能进行中的展开/折叠动画
                if (TaskTreeView != null)
                {
                    CollapseAllTreeViewItems(TaskTreeView);
                }
            }
            catch { /* 卸载清理失败不应影响导航流程 */ }
        }

        /// <summary>递归查找并取消所有 DataGrid 的编辑状态</summary>
        private static void CancelAllDataGridEdits(System.Windows.DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.DataGrid dg)
                {
                    try
                    {
                        dg.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
                        dg.CancelEdit();
                    }
                    catch { /* DataGrid 可能已卸载，忽略 */ }
                }
                CancelAllDataGridEdits(child);
            }
        }

        /// <summary>递归折叠所有 TreeViewItem，终止展开动画</summary>
        private static void CollapseAllTreeViewItems(System.Windows.DependencyObject parent)
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Controls.TreeViewItem tvi)
                {
                    tvi.IsExpanded = false;
                }
                CollapseAllTreeViewItems(child);
            }
        }

        /// <summary>
        /// 树形节点选中变化：同步到 ViewModel.SelectedNode
        /// TreeView.SelectedItem 是只读属性，通过事件回调同步到 ViewModel
        /// </summary>
        private void TaskTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is ProcessSequenceEditorViewModel vm)
            {
                vm.SelectedNode = e.NewValue;
            }
        }

        /// <summary>
        /// 双击步骤行时打开详细编辑弹窗
        /// </summary>
        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ProcessSequenceEditorViewModel vm)
            {
                var selectedStep = vm.SelectedStep;
                if (selectedStep == null) return;
                vm.OpenStepDetail();
            }
        }

        /// <summary>
        /// 动作详情面板"编辑"按钮点击：打开当前选中步骤的详细配置弹窗
        /// </summary>
        private void OpenStepDetailButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ProcessSequenceEditorViewModel vm)
            {
                vm.OpenStepDetail();
            }
        }

        #region 拖拽排序（Task 任务节点 + 同方法内 Step 动作节点）

        /// <summary>
        /// 鼠标左键按下：记录起始位置和可能的拖拽对象（Task 或 Step）
        /// </summary>
        private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedStep = null;
            _draggedTask = null;
            _isDragging = false;

            if (e.OriginalSource is DependencyObject source)
            {
                var treeViewItem = FindAncestor<TreeViewItem>(source);
                if (treeViewItem?.DataContext is ProcessStep step)
                {
                    _draggedStep = step;
                }
                else if (treeViewItem?.DataContext is TaskItem task)
                {
                    _draggedTask = task;
                }
            }
        }

        /// <summary>
        /// 鼠标移动：超过阈值时启动拖拽操作（Task 或 Step）
        /// </summary>
        private void TreeView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;

            Point currentPosition = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPosition;

            // 超过系统拖拽阈值才启动
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                // 优先处理 Step 拖拽，其次处理 Task 拖拽
                if (_draggedStep != null)
                {
                    _isDragging = true;
                    DragDrop.DoDragDrop((DependencyObject)sender, _draggedStep, DragDropEffects.Move);
                    _isDragging = false;
                    _draggedStep = null;
                }
                else if (_draggedTask != null)
                {
                    _isDragging = true;
                    DragDrop.DoDragDrop((DependencyObject)sender, _draggedTask, DragDropEffects.Move);
                    _isDragging = false;
                    _draggedTask = null;
                }
            }
        }

        /// <summary>
        /// 拖拽经过：验证目标有效性，设置拖拽效果
        /// - Step 拖拽：目标必须为同方法内的 ProcessStep
        /// - Task 拖拽：目标必须为另一个 TaskItem
        /// </summary>
        private void TreeView_DragOver(object sender, DragEventArgs e)
        {
            // Step 拖拽验证
            if (e.Data.GetDataPresent(typeof(ProcessStep)))
            {
                var draggedStep = (ProcessStep)e.Data.GetData(typeof(ProcessStep));
                var targetStep = GetStepFromDropTarget(e);
                if (targetStep != null && targetStep != draggedStep && IsSameMethod(draggedStep, targetStep))
                    e.Effects = DragDropEffects.Move;
                else
                    e.Effects = DragDropEffects.None;
            }
            // Task 拖拽验证
            else if (e.Data.GetDataPresent(typeof(TaskItem)))
            {
                var draggedTask = (TaskItem)e.Data.GetData(typeof(TaskItem));
                var targetTask = GetTaskFromDropTarget(e);
                if (targetTask != null && targetTask != draggedTask)
                    e.Effects = DragDropEffects.Move;
                else
                    e.Effects = DragDropEffects.None;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        /// <summary>
        /// 放置：执行重排序
        /// - Step 拖拽：同方法内步骤重排序
        /// - Task 拖拽：任务重排序
        /// </summary>
        private void TreeView_Drop(object sender, DragEventArgs e)
        {
            if (DataContext is ProcessSequenceEditorViewModel vm)
            {
                // Step 拖拽放置
                if (e.Data.GetDataPresent(typeof(ProcessStep)))
                {
                    var draggedStep = (ProcessStep)e.Data.GetData(typeof(ProcessStep));
                    var targetStep = GetStepFromDropTarget(e);
                    if (targetStep == null || draggedStep == targetStep) return;
                    if (!IsSameMethod(draggedStep, targetStep)) return;
                    var targetMethod = FindMethodContainingStep(targetStep);
                    if (targetMethod == null) return;
                    int targetIndex = targetMethod.Steps.IndexOf(targetStep);
                    vm.MoveStepTo(draggedStep, targetMethod, targetIndex);
                    e.Handled = true;
                }
                // Task 拖拽放置
                else if (e.Data.GetDataPresent(typeof(TaskItem)))
                {
                    var draggedTask = (TaskItem)e.Data.GetData(typeof(TaskItem));
                    var targetTask = GetTaskFromDropTarget(e);
                    if (targetTask == null || draggedTask == targetTask) return;
                    int targetIndex = vm.Tasks.IndexOf(targetTask);
                    vm.MoveTaskTo(draggedTask, targetIndex);
                    e.Handled = true;
                }
            }
        }

        /// <summary> 从拖放目标获取 ProcessStep </summary>
        private ProcessStep GetStepFromDropTarget(DragEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                var treeViewItem = FindAncestor<TreeViewItem>(source);
                if (treeViewItem?.DataContext is ProcessStep step)
                    return step;
            }
            return null;
        }

        /// <summary> 从拖放目标获取 TaskItem </summary>
        private TaskItem GetTaskFromDropTarget(DragEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                var treeViewItem = FindAncestor<TreeViewItem>(source);
                if (treeViewItem?.DataContext is TaskItem task)
                    return task;
            }
            return null;
        }

        /// <summary> 判断两个步骤是否属于同一个方法 </summary>
        private bool IsSameMethod(ProcessStep step1, ProcessStep step2)
        {
            return FindMethodContainingStep(step1) == FindMethodContainingStep(step2);
        }

        /// <summary> 查找包含指定步骤的方法 </summary>
        private Module.Models.ProcessMethod FindMethodContainingStep(ProcessStep step)
        {
            if (DataContext is ProcessSequenceEditorViewModel vm)
            {
                foreach (var task in vm.Tasks)
                {
                    if (task.Methods == null) continue;
                    foreach (var method in task.Methods)
                    {
                        if (method.Steps.Contains(step))
                            return method;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 向上查找指定类型的祖先元素。
        /// 混合使用可视树与逻辑树：Visual/Visual3D 用 VisualTreeHelper，
        /// 其他（如 Run、TextBlock 内嵌元素）用 LogicalTreeHelper，避免 InvalidOperationException。
        /// </summary>
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T result)
                    return result;
                // Visual/Visual3D 走可视树
                if (current is System.Windows.Media.Visual ||
                    current is System.Windows.Media.Media3D.Visual3D)
                {
                    current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                }
                else
                {
                    // FrameworkContentElement（如 Run、Span）走逻辑树
                    current = LogicalTreeHelper.GetParent(current);
                }
            }
            return null;
        }

        #endregion
    }
}

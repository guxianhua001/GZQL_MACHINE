using Recipe.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Windows.Threading;

namespace Recipe.Views
{
    /// <summary>
    /// Interaction logic for RecipeManagerView.xaml - 支持触摸手势选择节点
    /// </summary>
    public partial class RecipeManagerView : UserControl
    {
        private Point _gestureStartPoint;
        private bool _isGestureActive;
        private DispatcherTimer _longPressTimer;
        private TreeViewItem _pressedItem;
        private const double SwipeThreshold = 30;
        private const int LongPressDelayMs = 500;

        public RecipeManagerView()
        {
            InitializeComponent();
            InitLongPressTimer();
        }

        private void InitLongPressTimer()
        {
            _longPressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(LongPressDelayMs) };
            _longPressTimer.Tick += (s, e) =>
            {
                _longPressTimer.Stop();
                if (_pressedItem != null && DataContext is RecipeManagerViewModel vm)
                {
                    var node = _pressedItem.DataContext as ViewModels.TreeNode;
                    if (node != null) vm.ToggleMenuCommand.Execute(node);
                }
            };
        }

        private void Popup_Closed(object sender, EventArgs e)
        {
            if (DataContext is RecipeManagerViewModel vm)
            {
                vm.IsMenuOpen = false;
            }
        }

        private void TreeViewItem_StylusDown(object sender, StylusEventArgs e)
        {
            var item = sender as TreeViewItem;
            if (item == null) return;

            _pressedItem = item;
            _gestureStartPoint = e.GetPosition(item);
            _isGestureActive = true;
            _longPressTimer.Start();
        }

        private void TreeViewItem_PreviewStylusMove(object sender, StylusEventArgs e)
        {
            if (!_isGestureActive || _pressedItem == null) return;

            var currentPos = e.GetPosition(_pressedItem);
            double deltaX = currentPos.X - _gestureStartPoint.X;
            double deltaY = currentPos.Y - _gestureStartPoint.Y;

            if (Math.Abs(deltaX) > SwipeThreshold || Math.Abs(deltaY) > SwipeThreshold)
            {
                _longPressTimer.Stop();

                if (Math.Abs(deltaY) < Math.Abs(deltaX) * 0.5 && Math.Abs(deltaX) > SwipeThreshold)
                {
                    if (deltaX > 0 && !_pressedItem.IsExpanded)
                        _pressedItem.IsExpanded = true;
                    else if (deltaX < 0 && _pressedItem.IsExpanded)
                        _pressedItem.IsExpanded = false;
                }
                _isGestureActive = false;
                _pressedItem = null;
            }
        }

        private void TreeViewItem_StylusUp(object sender, StylusEventArgs e)
        {
            _longPressTimer.Stop();
            _isGestureActive = false;
            _pressedItem = null;
        }
    }
}

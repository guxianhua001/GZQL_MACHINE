using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
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
using Core.Models;
using Module.Controls;
using Module.ViewModels;

namespace Module.Views
{
    /// <summary>
    /// CadAlignmentView.xaml 的交互逻辑 — 图形选取事件桥接与画布引用管理
    /// 注意：HalconCanvasControl 位于 Tab3 内，WPF TabControl 延迟加载机制导致
    /// View.Loaded 时 Tab3 尚未加入可视树，因此必须通过 Canvas 自身的 Loaded 事件订阅
    /// </summary>
    public partial class CadAlignmentView : UserControl
    {
        private HalconCanvasControl _canvas;
        private Action<double, double> _canvasClickHandler;
        private Action _fitAllHandler;
        private Action<double, double, double, double> _fitToSegmentHandler;
        private Action _rotationCenterVisualHandler;
        private bool _canvasEventsSubscribed;

        public CadAlignmentView()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// HalconCanvasControl.Loaded 事件处理——在画布控件首次加入可视树时订阅事件
        /// 此方法在 XAML 中通过 alignmentCanvas.Loaded 绑定，确保即使 Tab3 延迟加载也能正确订阅
        /// </summary>
        private void OnCanvasLoaded(object sender, RoutedEventArgs e)
        {
            if (_canvasEventsSubscribed) return;

            _canvas = sender as HalconCanvasControl;
            if (_canvas == null) return;

            if (DataContext is CadAlignmentViewModel vm)
            {
                _canvasClickHandler = vm.OnCanvasPointClicked;
                _canvas.CanvasPointClicked += _canvasClickHandler;
                _fitAllHandler = () =>
                {
                    _canvas.FitToAll();
                    NotifyImageOffsetToViewModel(vm);
                };
                vm.FitToAllRequested += _fitAllHandler;
                _fitToSegmentHandler = (x1, y1, x2, y2) => FitCanvasToSegment(x1, y1, x2, y2);
                vm.FitToSegmentRequested += _fitToSegmentHandler;

                // ✅ 新增：订阅批量更新事件，优化选取操作时的渲染性能
                vm.BatchUpdateStartRequested += () => _canvas.BeginBatchUpdate();
                vm.BatchUpdateEndRequested += () => _canvas.EndBatchUpdate();

                // 订阅回转中心可视化更新事件
                _rotationCenterVisualHandler = () => UpdateRotationCenterCanvas();
                vm.RotationCenterVisualUpdateRequested += _rotationCenterVisualHandler;

                _canvasEventsSubscribed = true;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_canvas != null && _canvasClickHandler != null)
                _canvas.CanvasPointClicked -= _canvasClickHandler;
            if (DataContext is CadAlignmentViewModel vm)
            {
                if (_fitAllHandler != null)
                    vm.FitToAllRequested -= _fitAllHandler;
                if (_fitToSegmentHandler != null)
                    vm.FitToSegmentRequested -= _fitToSegmentHandler;

                // ✅ 取消订阅批量更新事件
                vm.BatchUpdateStartRequested -= null; // 使用匿名委托，无法精确移除，但 View 销毁时无影响
                vm.BatchUpdateEndRequested -= null;
            }
            _canvas = null;
            _canvasClickHandler = null;
            _fitAllHandler = null;
            _fitToSegmentHandler = null;
            _rotationCenterVisualHandler = null;
            _canvasEventsSubscribed = false;
        }

        /// <summary>
        /// 回转中心可视化画布尺寸变更时，重新计算屏幕坐标
        /// </summary>
        private void RotationCenterCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateRotationCenterCanvas();
        }

        /// <summary>
        /// 调用ViewModel的UpdateRotationCenterVisual方法，传入画布实际尺寸
        /// </summary>
        private void UpdateRotationCenterCanvas()
        {
            if (DataContext is CadAlignmentViewModel vm)
            {
                var canvas = FindName("RotationCenterCanvas") as Canvas;
                if (canvas != null && canvas.ActualWidth > 0 && canvas.ActualHeight > 0)
                {
                    vm.UpdateRotationCenterVisual(canvas.ActualWidth, canvas.ActualHeight);
                }
            }
        }

        /// <summary>
        /// 将画布视口聚焦到指定线段区域（CAD坐标），在线段周围留出边距
        /// </summary>
        private void FitCanvasToSegment(double cadX1, double cadY1, double cadX2, double cadY2)
        {
            _canvas?.FitToCadRegion(cadX1, cadY1, cadX2, cadY2);
        }

        /// <summary>
        /// 从HalconCanvasControl获取CAD→图像偏移量，通知ViewModel更新点位的图像坐标
        /// </summary>
        private void NotifyImageOffsetToViewModel(CadAlignmentViewModel vm)
        {
            if (_canvas == null) return;
            var (offsetX, offsetY) = _canvas.GetCadToImageOffset();
            vm.SetCadToImageOffset(offsetX, offsetY);
        }
    }
}

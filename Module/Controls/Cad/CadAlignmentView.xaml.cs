#if HAS_HALCON
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Core.Models;
using Module.Controls;
using Module.ViewModels;

namespace Module.Views
{
    /// <summary>
    /// CadAlignmentView.xaml 的交互逻辑 — 图形选取事件桥接与画布引用管理
    /// 注意：HalconCanvasControl 位于 Tab2/Tab3 内，WPF TabControl 延迟加载机制导致
    /// View.Loaded 时画布可能尚未加入可视树，因此必须通过 Canvas 自身的 Loaded 事件订阅
    /// </summary>
    public partial class CadAlignmentView : UserControl
    {
        private readonly List<HalconCanvasControl> _canvases = new();
        private Action<double, double> _canvasClickHandler;
        private Action _fitAllHandler;
        private Action<double, double, double, double> _fitToSegmentHandler;
        private Action _rotationCenterVisualHandler;
        private bool _viewModelEventsSubscribed;

        public CadAlignmentView()
        {
            InitializeComponent();
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// HalconCanvasControl.Loaded 事件处理——在画布控件首次加入可视树时订阅事件
        /// Tab2（仿射标定）与 Tab3（旋转角度）各有一个画布，均通过此方法桥接
        /// </summary>
        private void OnCanvasLoaded(object sender, RoutedEventArgs e)
        {
            var canvas = sender as HalconCanvasControl;
            if (canvas == null || _canvases.Contains(canvas)) return;

            _canvases.Add(canvas);

            if (DataContext is CadAlignmentViewModel vm)
            {
                _canvasClickHandler ??= vm.OnCanvasPointClicked;
                canvas.CanvasPointClicked += _canvasClickHandler;

                if (!_viewModelEventsSubscribed)
                {
                    _fitAllHandler = () =>
                    {
                        foreach (var c in _canvases)
                            c?.FitToAll();
                        NotifyImageOffsetToViewModel(vm);
                    };
                    vm.FitToAllRequested += _fitAllHandler;

                    _fitToSegmentHandler = (x1, y1, x2, y2) => FitCanvasToSegment(x1, y1, x2, y2);
                    vm.FitToSegmentRequested += _fitToSegmentHandler;

                    vm.BatchUpdateStartRequested += () =>
                    {
                        foreach (var c in _canvases)
                            c?.BeginBatchUpdate();
                    };
                    vm.BatchUpdateEndRequested += () =>
                    {
                        foreach (var c in _canvases)
                            c?.EndBatchUpdate();
                    };

                    _rotationCenterVisualHandler = () => UpdateRotationCenterCanvas();
                    vm.RotationCenterVisualUpdateRequested += _rotationCenterVisualHandler;

                    _viewModelEventsSubscribed = true;
                }
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_canvasClickHandler != null)
            {
                foreach (var canvas in _canvases)
                    canvas.CanvasPointClicked -= _canvasClickHandler;
            }

            if (DataContext is CadAlignmentViewModel vm && _viewModelEventsSubscribed)
            {
                if (_fitAllHandler != null)
                    vm.FitToAllRequested -= _fitAllHandler;
                if (_fitToSegmentHandler != null)
                    vm.FitToSegmentRequested -= _fitToSegmentHandler;
                if (_rotationCenterVisualHandler != null)
                    vm.RotationCenterVisualUpdateRequested -= _rotationCenterVisualHandler;
            }

            _canvases.Clear();
            _canvasClickHandler = null;
            _fitAllHandler = null;
            _fitToSegmentHandler = null;
            _rotationCenterVisualHandler = null;
            _viewModelEventsSubscribed = false;
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
            foreach (var canvas in _canvases)
                canvas?.FitToCadRegion(cadX1, cadY1, cadX2, cadY2);
        }

        /// <summary>
        /// 从任一已加载的 HalconCanvasControl 获取 CAD→图像偏移量，通知 ViewModel 更新点位图像坐标
        /// </summary>
        private void NotifyImageOffsetToViewModel(CadAlignmentViewModel vm)
        {
            foreach (var canvas in _canvases)
            {
                if (canvas == null) continue;
                var (offsetX, offsetY) = canvas.GetCadToImageOffset();
                vm.SetCadToImageOffset(offsetX, offsetY);
                return;
            }
        }
    }
}
#endif

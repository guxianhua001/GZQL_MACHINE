using Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Module.Controls.ZMap
{
    /// <summary>
    /// ZMAP轨迹ROI交互画布。负责图像坐标交互与等距采样，不包含标定、Z修正或运动逻辑。
    /// 直线采用拖拽两点，圆弧采用依次点击起点/弧中点/终点，折线采用点击添加顶点并右键结束。
    /// </summary>
    public partial class ZMapRoiCanvas : UserControl
    {
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register(
                nameof(Source),
                typeof(BitmapSource),
                typeof(ZMapRoiCanvas),
                new PropertyMetadata(null, OnVisualPropertyChanged));

        public static readonly DependencyProperty RoiTypeProperty =
            DependencyProperty.Register(
                nameof(RoiType),
                typeof(ZMapRoiType),
                typeof(ZMapRoiCanvas),
                new PropertyMetadata(ZMapRoiType.Polyline, OnRoiTypeChanged));

        public static readonly DependencyProperty HintTextProperty =
            DependencyProperty.Register(
                nameof(HintText),
                typeof(string),
                typeof(ZMapRoiCanvas),
                new PropertyMetadata(string.Empty));

        private readonly List<ZMapPixelPoint> _controlPoints = new List<ZMapPixelPoint>();
        private bool _isDraggingLine;
        private bool _isComplete;

        public ZMapRoiCanvas()
        {
            InitializeComponent();
            SizeChanged += (_, __) => Redraw();
            UpdateHint();
        }

        public BitmapSource Source
        {
            get => (BitmapSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public ZMapRoiType RoiType
        {
            get => (ZMapRoiType)GetValue(RoiTypeProperty);
            set => SetValue(RoiTypeProperty, value);
        }

        public string HintText
        {
            get => (string)GetValue(HintTextProperty);
            private set => SetValue(HintTextProperty, value);
        }

        public bool IsRoiComplete => _isComplete;

        public event EventHandler RoiChanged;

        /// <summary>清空当前ROI，切换线段时不会残留上一次轨迹。</summary>
        public void ClearRoi()
        {
            _controlPoints.Clear();
            _isDraggingLine = false;
            _isComplete = false;
            ReleaseMouseCapture();
            UpdateHint();
            Redraw();
            RoiChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 按指定点数沿当前ROI路径长度等距采样，确保与选中CadPoint逐点一一对应。
        /// </summary>
        public IReadOnlyList<ZMapPixelPoint> GetSamplePoints(int count)
        {
            if (!_isComplete || count <= 0)
                return Array.Empty<ZMapPixelPoint>();

            switch (RoiType)
            {
                case ZMapRoiType.Line:
                    return SamplePolyline(_controlPoints.Take(2).ToList(), count);
                case ZMapRoiType.CircularArc:
                    return SampleArc(_controlPoints, count);
                case ZMapRoiType.Polyline:
                    return SamplePolyline(_controlPoints, count);
                default:
                    return Array.Empty<ZMapPixelPoint>();
            }
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ZMapRoiCanvas)d).Redraw();
        }

        private static void OnRoiTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ZMapRoiCanvas)d).ClearRoi();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Source == null)
                return;
            var point = ScreenToPixel(e.GetPosition(RootGrid));

            if (RoiType == ZMapRoiType.Line)
            {
                _controlPoints.Clear();
                _controlPoints.Add(point);
                _controlPoints.Add(new ZMapPixelPoint { Col = point.Col, Row = point.Row });
                _isDraggingLine = true;
                _isComplete = false;
                CaptureMouse();
            }
            else if (RoiType == ZMapRoiType.CircularArc)
            {
                // 已完成或三点共线导致构圆失败时，下一次点击自动开始一条新圆弧。
                if (_isComplete || _controlPoints.Count >= 3)
                    _controlPoints.Clear();
                if (_controlPoints.Count < 3)
                    _controlPoints.Add(point);
                _isComplete = _controlPoints.Count == 3 && TryGetCircle(_controlPoints, out _, out _, out _);
            }
            else
            {
                if (_isComplete)
                {
                    _controlPoints.Clear();
                    _isComplete = false;
                }
                // 双击的第二次MouseDown不重复添加顶点。
                if (e.ClickCount == 1)
                    _controlPoints.Add(point);
                else if (_controlPoints.Count >= 2)
                    _isComplete = true;
            }

            UpdateHint();
            Redraw();
            RoiChanged?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingLine || _controlPoints.Count != 2 || e.LeftButton != MouseButtonState.Pressed)
                return;
            _controlPoints[1] = ScreenToPixel(e.GetPosition(RootGrid));
            Redraw();
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDraggingLine)
                return;
            _controlPoints[1] = ScreenToPixel(e.GetPosition(RootGrid));
            _isDraggingLine = false;
            _isComplete = Distance(_controlPoints[0], _controlPoints[1]) > 0.5;
            ReleaseMouseCapture();
            UpdateHint();
            Redraw();
            RoiChanged?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (RoiType == ZMapRoiType.Polyline && _controlPoints.Count >= 2)
            {
                _isComplete = true;
                UpdateHint();
                Redraw();
                RoiChanged?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        }

        /// <summary>把控件坐标转换为真实图像像素，正确处理Uniform缩放产生的留黑边区域。</summary>
        private ZMapPixelPoint ScreenToPixel(Point screen)
        {
            GetImageTransform(out double scale, out double offsetX, out double offsetY);
            double col = (screen.X - offsetX) / scale;
            double row = (screen.Y - offsetY) / scale;
            return new ZMapPixelPoint
            {
                Col = Math.Max(0, Math.Min(Source.PixelWidth - 1, col)),
                Row = Math.Max(0, Math.Min(Source.PixelHeight - 1, row))
            };
        }

        private Point PixelToScreen(ZMapPixelPoint pixel)
        {
            GetImageTransform(out double scale, out double offsetX, out double offsetY);
            return new Point(offsetX + pixel.Col * scale, offsetY + pixel.Row * scale);
        }

        private void GetImageTransform(out double scale, out double offsetX, out double offsetY)
        {
            if (Source == null || Source.PixelWidth <= 0 || Source.PixelHeight <= 0 ||
                RootGrid.ActualWidth <= 0 || RootGrid.ActualHeight <= 0)
            {
                scale = 1;
                offsetX = 0;
                offsetY = 0;
                return;
            }
            scale = Math.Min(
                RootGrid.ActualWidth / Source.PixelWidth,
                RootGrid.ActualHeight / Source.PixelHeight);
            offsetX = (RootGrid.ActualWidth - Source.PixelWidth * scale) / 2.0;
            offsetY = (RootGrid.ActualHeight - Source.PixelHeight * scale) / 2.0;
        }

        private void Redraw()
        {
            if (OverlayCanvas == null)
                return;
            OverlayCanvas.Children.Clear();
            if (Source == null || _controlPoints.Count == 0)
                return;

            IReadOnlyList<ZMapPixelPoint> displayPoints = _controlPoints;
            if (RoiType == ZMapRoiType.CircularArc && _controlPoints.Count == 3 &&
                TryGetCircle(_controlPoints, out _, out _, out _))
            {
                displayPoints = SampleArc(_controlPoints, 80);
            }

            if (displayPoints.Count >= 2)
            {
                var line = new Polyline
                {
                    Stroke = Brushes.Lime,
                    StrokeThickness = 2,
                    StrokeLineJoin = PenLineJoin.Round
                };
                foreach (var point in displayPoints)
                    line.Points.Add(PixelToScreen(point));
                OverlayCanvas.Children.Add(line);
            }

            foreach (var point in _controlPoints)
            {
                Point screen = PixelToScreen(point);
                var handle = new Ellipse
                {
                    Width = 8,
                    Height = 8,
                    Fill = Brushes.White,
                    Stroke = Brushes.Lime,
                    StrokeThickness = 2
                };
                Canvas.SetLeft(handle, screen.X - 4);
                Canvas.SetTop(handle, screen.Y - 4);
                OverlayCanvas.Children.Add(handle);
            }
        }

        private void UpdateHint()
        {
            switch (RoiType)
            {
                case ZMapRoiType.Line:
                    HintText = _isComplete ? "直线ROI已完成，可重新拖拽绘制" : "按住左键拖拽绘制直线ROI";
                    break;
                case ZMapRoiType.CircularArc:
                    HintText = _isComplete
                        ? "圆弧ROI已完成，可重新依次点击3点"
                        : $"依次点击圆弧起点、中间点、终点（{_controlPoints.Count}/3）";
                    break;
                default:
                    HintText = _isComplete
                        ? "折线ROI已完成，左键开始重绘"
                        : "左键添加折线顶点，右键或双击结束";
                    break;
            }
        }

        /// <summary>沿折线累计长度等距采样，点数可与CadPoint数量严格一致。</summary>
        private static IReadOnlyList<ZMapPixelPoint> SamplePolyline(
            IReadOnlyList<ZMapPixelPoint> vertices,
            int count)
        {
            if (vertices == null || vertices.Count < 2 || count <= 0)
                return Array.Empty<ZMapPixelPoint>();
            if (count == 1)
                return new[] { Clone(vertices[0]) };

            var cumulative = new double[vertices.Count];
            for (int i = 1; i < vertices.Count; i++)
                cumulative[i] = cumulative[i - 1] + Distance(vertices[i - 1], vertices[i]);
            double total = cumulative[cumulative.Length - 1];
            if (total < 1e-9)
                return Array.Empty<ZMapPixelPoint>();

            var result = new List<ZMapPixelPoint>(count);
            for (int i = 0; i < count; i++)
            {
                double target = total * i / (count - 1);
                int segment = 1;
                while (segment < cumulative.Length - 1 && cumulative[segment] < target)
                    segment++;
                double segmentLength = cumulative[segment] - cumulative[segment - 1];
                double t = segmentLength < 1e-12
                    ? 0
                    : (target - cumulative[segment - 1]) / segmentLength;
                result.Add(new ZMapPixelPoint
                {
                    Col = vertices[segment - 1].Col + t * (vertices[segment].Col - vertices[segment - 1].Col),
                    Row = vertices[segment - 1].Row + t * (vertices[segment].Row - vertices[segment - 1].Row)
                });
            }
            return result;
        }

        /// <summary>通过起点/弧中点/终点确定圆弧方向并等角采样。</summary>
        private static IReadOnlyList<ZMapPixelPoint> SampleArc(
            IReadOnlyList<ZMapPixelPoint> points,
            int count)
        {
            if (points == null || points.Count < 3 || count <= 0 ||
                !TryGetCircle(points, out double centerCol, out double centerRow, out double radius))
                return Array.Empty<ZMapPixelPoint>();
            if (count == 1)
                return new[] { Clone(points[0]) };

            double start = Math.Atan2(points[0].Row - centerRow, points[0].Col - centerCol);
            double middle = Math.Atan2(points[1].Row - centerRow, points[1].Col - centerCol);
            double end = Math.Atan2(points[2].Row - centerRow, points[2].Col - centerCol);
            double ccwSpan = NormalizeAngle(end - start);
            double ccwMiddle = NormalizeAngle(middle - start);
            double span = ccwMiddle <= ccwSpan ? ccwSpan : ccwSpan - Math.PI * 2;

            var result = new List<ZMapPixelPoint>(count);
            for (int i = 0; i < count; i++)
            {
                double angle = start + span * i / (count - 1);
                result.Add(new ZMapPixelPoint
                {
                    Col = centerCol + radius * Math.Cos(angle),
                    Row = centerRow + radius * Math.Sin(angle)
                });
            }
            return result;
        }

        /// <summary>由不共线三点求圆心和半径。</summary>
        private static bool TryGetCircle(
            IReadOnlyList<ZMapPixelPoint> points,
            out double centerCol,
            out double centerRow,
            out double radius)
        {
            centerCol = 0;
            centerRow = 0;
            radius = 0;
            if (points == null || points.Count < 3)
                return false;

            double x1 = points[0].Col;
            double y1 = points[0].Row;
            double x2 = points[1].Col;
            double y2 = points[1].Row;
            double x3 = points[2].Col;
            double y3 = points[2].Row;
            double determinant = 2 * (x1 * (y2 - y3) + x2 * (y3 - y1) + x3 * (y1 - y2));
            if (Math.Abs(determinant) < 1e-9)
                return false;

            double q1 = x1 * x1 + y1 * y1;
            double q2 = x2 * x2 + y2 * y2;
            double q3 = x3 * x3 + y3 * y3;
            centerCol = (q1 * (y2 - y3) + q2 * (y3 - y1) + q3 * (y1 - y2)) / determinant;
            centerRow = (q1 * (x3 - x2) + q2 * (x1 - x3) + q3 * (x2 - x1)) / determinant;
            radius = Math.Sqrt((x1 - centerCol) * (x1 - centerCol) + (y1 - centerRow) * (y1 - centerRow));
            return radius > 1e-9;
        }

        private static double NormalizeAngle(double angle)
        {
            double twoPi = Math.PI * 2;
            angle %= twoPi;
            return angle < 0 ? angle + twoPi : angle;
        }

        private static double Distance(ZMapPixelPoint first, ZMapPixelPoint second)
        {
            double dx = second.Col - first.Col;
            double dy = second.Row - first.Row;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static ZMapPixelPoint Clone(ZMapPixelPoint point) =>
            new ZMapPixelPoint { Col = point.Col, Row = point.Row };
    }
}

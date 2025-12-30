// PinMapViewModel.cs
using Interfaces;
using Prism.Commands;
using Prism.Mvvm;
using Stations;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace Framework.ViewModels
{
    public class PinMapViewModel : BindableBase, IDisposable
    {
        #region 属性

        private const double BaseCanvasSize = 500;
        private readonly ITaskWithPoints _boundTask;
        private double _scale = 0.9;

        public string Title => _boundTask?.TaskName ?? "未绑定任务";
        public ObservableCollection<PointViewModel> DisplayPoints { get; } = new();
        public double CanvasWidth => BaseCanvasSize * _scale;
        public double CanvasHeight => BaseCanvasSize * _scale;

        public double Scale
        {
            get => _scale;
            set
            {
                if (SetProperty(ref _scale, Math.Clamp(value, 0.5, 3.0)))
                {
                    UpdateLayout();
                    RaisePropertyChanged(nameof(CanvasWidth));
                    RaisePropertyChanged(nameof(CanvasHeight));
                }
            }
        }
        public void UpdatePointStatus(int index, bool isOk)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var point = DisplayPoints.FirstOrDefault(p => p.Index == index);
                if (point != null)
                {
                    point.IsOk = isOk;
                    // 触发集合更新通知
                    var pointIndex = DisplayPoints.IndexOf(point);
                    DisplayPoints[pointIndex] = point; // 触发Replace事件
                }
            });
        }
        #endregion

        #region 命令

        public DelegateCommand ZoomInCommand => new(() => Scale *= 1.1);
        public DelegateCommand ZoomOutCommand => new(() => Scale *= 0.9);
        public DelegateCommand ResetZoomCommand => new(() => Scale = 1.0);
        public DelegateCommand<PointViewModel> PointSelectedCommand { get; set; }
        public DelegateCommand<PointViewModel> ShowContextMenuCommand { get; set; }

        #endregion

        #region 构造函数

        public PinMapViewModel(ITaskWithPoints task)
        {
            _boundTask = task ?? throw new ArgumentNullException(nameof(task));
            _boundTask.PointsChanged += OnPointsChanged;
            InitializePoints();
            UpdateLayout();
        }

        private void InitializePoints()
        {
            DisplayPoints.Clear();
            foreach (var sourcePoint in _boundTask.PinPoints) // 源数据点
            {
                // 1. 监听源头数据点的属性变更
                sourcePoint.PropertyChanged += OnSourcePointPropertyChanged;

                // 2. 克隆到显示集合中
                DisplayPoints.Add(ClonePoint(sourcePoint));
            }

        }
        private void OnSourcePointPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 当源头数据点属性变更时，更新显示点
            if (sender is PointViewModel sourcePoint)
            {
                var displayPoint = DisplayPoints.FirstOrDefault(p => p.Index == sourcePoint.Index);
                if (displayPoint != null)
                {
                    // 同步 IsOk 属性（根据需要可扩展其他属性）
                    displayPoint.IsOk = sourcePoint.IsOk;
                }
            }
        }

        #endregion

        #region 布局更新

        private void UpdateLayout()
        {
            if (!DisplayPoints.Any()) return;
            // 计算点集边界
            var minX = DisplayPoints.Min(p => p.OriginalX);
            var minY = DisplayPoints.Min(p => p.OriginalY);
            var maxX = DisplayPoints.Max(p => p.OriginalX);
            var maxY = DisplayPoints.Max(p => p.OriginalY);
            // ⭐ 关键修复：考虑点的半径和边界偏移
            const double pointRadius = 7; // 点半径(14/2)
            const double edgePadding = 4; // 容器内边距

            // 计算可用的无边缘区域
            var availableWidth = CanvasWidth - 2 * (pointRadius + edgePadding);
            var availableHeight = CanvasHeight - 2 * (pointRadius + edgePadding);

            // 防止除以零
            if (maxX - minX < double.Epsilon || maxY - minY < double.Epsilon)
                return;

            // 计算缩放比例
            var scaleX = availableWidth / (maxX - minX);
            var scaleY = availableHeight / (maxY - minY);
            var actualScale = Math.Min(scaleX, scaleY);
            // 应用点半径偏移
            var offsetX = pointRadius + edgePadding;
            var offsetY = pointRadius + edgePadding;
            // 如果有多余空间则居中
            if (actualScale == scaleX && availableHeight > (maxY - minY) * actualScale)
            {
                offsetY += (availableHeight - (maxY - minY) * actualScale) / 2;
            }
            else if (actualScale == scaleY && availableWidth > (maxX - minX) * actualScale)
            {
                offsetX += (availableWidth - (maxX - minX) * actualScale) / 2;
            }
            // 更新所有点位置
            foreach (var point in DisplayPoints)
            {
                point.X = (point.OriginalX - minX) * actualScale + offsetX;
                point.Y = (point.OriginalY - minY) * actualScale + offsetY;
            }

            // 確保UI更新
            RaisePropertyChanged(nameof(CanvasWidth));
            RaisePropertyChanged(nameof(CanvasHeight));
        }

        #endregion

        #region 事件处理

        private void OnPointsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        foreach (PointViewModel newPoint in e.NewItems)
                        {
                            // 1. 订阅新添加的源点事件
                            newPoint.PropertyChanged += OnSourcePointPropertyChanged;

                            // 2. 克隆到显示集合
                            DisplayPoints.Add(ClonePoint(newPoint));
                        }
                        break;

                    case NotifyCollectionChangedAction.Remove:
                        foreach (PointViewModel oldPoint in e.OldItems)
                        {
                            // 1. 取消事件订阅
                            oldPoint.PropertyChanged -= OnSourcePointPropertyChanged;

                            // 2. 从显示集合中移除
                            var displayPoint = DisplayPoints.First(p => p.Index == oldPoint.Index);
                            DisplayPoints.Remove(displayPoint);
                        }
                        break;

                    case NotifyCollectionChangedAction.Reset:
                        // 1. 清空所有事件订阅
                        foreach (var point in _boundTask.PinPoints)
                        {
                            point.PropertyChanged -= OnSourcePointPropertyChanged;
                        }
                        // 2. 清空显示集合
                        DisplayPoints.Clear();
                        break;
                }
                UpdateLayout();
            });
        }


        private PointViewModel ClonePoint(PointViewModel source)
        {
            return new PointViewModel
            {
                Index = source.Index,
                OriginalX = source.X,
                OriginalY = source.Y,
                IsOk = null  //source.IsOk
            };
        }

        #endregion

        #region 资源清理

        public void Dispose()
        {
            if (_boundTask != null)
            {
                _boundTask.PointsChanged -= OnPointsChanged;
            }
            DisplayPoints.Clear();
        }

        #endregion
    }
}
using Core.Abstraction;
using Core.Models;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;

namespace Module.ViewModels
{
    /// <summary>
    /// 旋转后坐标查看弹窗 ViewModel——展示所有段的 CAD 原始坐标与旋转后机械坐标对照
    /// </summary>
    public class DispenseRotatedCoordsViewModel : BindableBase, IDialogCloseable
    {
        #region 属性

        /// <summary>坐标对照列表</summary>
        public ObservableCollection<DispenseRotatedCoordItem> CoordItems { get; } = new ObservableCollection<DispenseRotatedCoordItem>();

        private double _rotationAngle;
        /// <summary>当前使用的旋转角度（度数）</summary>
        public double RotationAngle
        {
            get => _rotationAngle;
            set => SetProperty(ref _rotationAngle, value);
        }

        private double _rotationCenterX;
        /// <summary>回转中心 X 坐标</summary>
        public double RotationCenterX
        {
            get => _rotationCenterX;
            set => SetProperty(ref _rotationCenterX, value);
        }

        private double _rotationCenterY;
        /// <summary>回转中心 Y 坐标</summary>
        public double RotationCenterY
        {
            get => _rotationCenterY;
            set => SetProperty(ref _rotationCenterY, value);
        }

        /// <summary>坐标点总数</summary>
        private int _pointCount;
        public int PointCount { get => _pointCount; set => SetProperty(ref _pointCount, value); }

        #endregion

        #region 对话框关闭

        /// <summary>请求关闭对话框时触发</summary>
        public event Action<object> RequestClose;

        /// <summary>是否可以关闭对话框</summary>
        public bool CanCloseDialog() => true;

        /// <summary>关闭命令</summary>
        public DelegateCommand CloseCommand { get; }

        #endregion

        public DispenseRotatedCoordsViewModel()
        {
            CloseCommand = new DelegateCommand(() => RequestClose?.Invoke(null));
        }

        /// <summary>
        /// 初始化坐标对照列表与变换参数
        /// </summary>
        /// <param name="coordList">坐标对照列表</param>
        /// <param name="rotationAngle">旋转角度</param>
        /// <param name="mox">回转中心 X</param>
        /// <param name="moy">回转中心 Y</param>
        public void Initialize(System.Collections.Generic.List<DispenseRotatedCoordItem> coordList, double rotationAngle, double mox, double moy)
        {
            CoordItems.Clear();
            foreach (var item in coordList)
                CoordItems.Add(item);

            RotationAngle = rotationAngle;
            RotationCenterX = mox;
            RotationCenterY = moy;

            PointCount = coordList.Count;
        }
    }
}

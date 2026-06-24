using Core.Abstraction;
using Core.Constants;
using Core.Models;
using Core.Utilities;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Module.ViewModels
{
    /// <summary>
    /// 旋转后坐标查看弹窗 ViewModel——展示 CAD 原始坐标、旋转后相机中心坐标，
    /// 并叠加各类补偿计算最终点胶针头坐标。
    /// FinalX = RotatedX + Camera-Needle Offset(Link勾选) + Needle Alignment Comp
    ///          + X Comp(校准器)(Enable Calibration) + X Compensation(Enable Comp)（Y 同理）。
    /// </summary>
    public class DispenseRotatedCoordsViewModel : BindableBase, IDialogCloseable
    {
        private readonly ILoggerService _logger;

        /// <summary>被编辑的点胶详情（补偿配置持久化目标）</summary>
        private DispenseDetail _detail;

        /// <summary>避免初始化批量赋值过程中反复重算</summary>
        private bool _suspendRecalc;

        #region 列表与变换参数

        /// <summary>坐标对照列表</summary>
        public ObservableCollection<DispenseRotatedCoordItem> CoordItems { get; } = new ObservableCollection<DispenseRotatedCoordItem>();

        /// <summary>可链接的全局变量集合（由父级传入）</summary>
        public ObservableCollection<GlobalVariable> AvailableGlobalVariables { get; } = new ObservableCollection<GlobalVariable>();

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

        private int _pointCount;
        /// <summary>坐标点总数</summary>
        public int PointCount { get => _pointCount; set => SetProperty(ref _pointCount, value); }

        #endregion

        #region 针头偏移补偿——总开关与各分量

        /// <summary>是否启用针头偏移补偿（相机中心坐标→实际针头坐标）</summary>
        public bool EnableNeedleOffsetComp
        {
            get => _detail?.EnableNeedleOffsetComp ?? false;
            set
            {
                if (_detail == null || _detail.EnableNeedleOffsetComp == value) return;
                _detail.EnableNeedleOffsetComp = value;
                RaisePropertyChanged();
                RecalculateFinalCoords();
            }
        }

        /// <summary>是否链接相机与针头固定距离（勾选时取全局变量值，不勾选则为 0）</summary>
        public bool LinkCameraNeedleOffsetToCalibration
        {
            get => _detail?.LinkCameraNeedleOffsetToCalibration ?? false;
            set
            {
                if (_detail == null || _detail.LinkCameraNeedleOffsetToCalibration == value) return;
                _detail.LinkCameraNeedleOffsetToCalibration = value;
                RaisePropertyChanged();
                RaiseCameraNeedleDisplayChanged();
                RecalculateFinalCoords();
            }
        }

        // —— 相机与针头固定距离 X/Y ——
        public double CameraNeedleOffsetX
        {
            get => _detail?.CameraNeedleOffsetX ?? 0.0;
            set { if (_detail != null && _detail.CameraNeedleOffsetX != value) { _detail.CameraNeedleOffsetX = value; RaisePropertyChanged(); RaiseCameraNeedleDisplayChanged(); RecalculateFinalCoords(); } }
        }

        public double CameraNeedleOffsetY
        {
            get => _detail?.CameraNeedleOffsetY ?? 0.0;
            set { if (_detail != null && _detail.CameraNeedleOffsetY != value) { _detail.CameraNeedleOffsetY = value; RaisePropertyChanged(); RaiseCameraNeedleDisplayChanged(); RecalculateFinalCoords(); } }
        }

        public string CameraNeedleOffsetXLinkedVar
        {
            get => _detail?.CameraNeedleOffsetXLinkedVar;
            set
            {
                if (_detail == null || string.Equals(_detail.CameraNeedleOffsetXLinkedVar, value, StringComparison.Ordinal)) return;
                _detail.CameraNeedleOffsetXLinkedVar = value;
                RaisePropertyChanged(nameof(CameraNeedleOffsetXLinkedVar));
                RaisePropertyChanged(nameof(IsCameraNeedleOffsetXLinked));
                RaiseCameraNeedleDisplayChanged();
                RecalculateFinalCoords();
            }
        }

        public string CameraNeedleOffsetYLinkedVar
        {
            get => _detail?.CameraNeedleOffsetYLinkedVar;
            set
            {
                if (_detail == null || string.Equals(_detail.CameraNeedleOffsetYLinkedVar, value, StringComparison.Ordinal)) return;
                _detail.CameraNeedleOffsetYLinkedVar = value;
                RaisePropertyChanged(nameof(CameraNeedleOffsetYLinkedVar));
                RaisePropertyChanged(nameof(IsCameraNeedleOffsetYLinked));
                RaiseCameraNeedleDisplayChanged();
                RecalculateFinalCoords();
            }
        }

        public bool IsCameraNeedleOffsetXLinked => !string.IsNullOrEmpty(_detail?.CameraNeedleOffsetXLinkedVar);
        public bool IsCameraNeedleOffsetYLinked => !string.IsNullOrEmpty(_detail?.CameraNeedleOffsetYLinkedVar);

        /// <summary>相机与针头固定距离 X 的实时显示值（Link 勾选时取全局变量，否则 0）</summary>
        public double CameraNeedleOffsetXDisplayValue => ResolveCameraNeedleOffsetX();
        /// <summary>相机与针头固定距离 Y 的实时显示值（Link 勾选时取全局变量，否则 0）</summary>
        public double CameraNeedleOffsetYDisplayValue => ResolveCameraNeedleOffsetY();

        // —— 对针补偿 X/Y ——
        public double NeedleAlignCompX
        {
            get => _detail?.NeedleAlignCompX ?? 0.0;
            set
            {
                if (_detail == null || _detail.NeedleAlignCompX == value) return;
                _detail.NeedleAlignCompX = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(NeedleAlignCompXDisplayValue));
                RaisePropertyChanged(nameof(NeedleAlignCompXPrefix));
                RecalculateFinalCoords();
            }
        }

        public double NeedleAlignCompY
        {
            get => _detail?.NeedleAlignCompY ?? 0.0;
            set
            {
                if (_detail == null || _detail.NeedleAlignCompY == value) return;
                _detail.NeedleAlignCompY = value;
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(NeedleAlignCompYDisplayValue));
                RaisePropertyChanged(nameof(NeedleAlignCompYPrefix));
                RecalculateFinalCoords();
            }
        }

        /// <summary>对针补偿 X 手动输入正号前缀（非负显示 +，负值由数值自带负号）</summary>
        public string NeedleAlignCompXPrefix => NeedleAlignCompX >= 0 ? "+" : string.Empty;

        /// <summary>对针补偿 Y 手动输入正号前缀（非负显示 +，负值由数值自带负号）</summary>
        public string NeedleAlignCompYPrefix => NeedleAlignCompY >= 0 ? "+" : string.Empty;

        public string NeedleAlignCompXLinkedVar
        {
            get => _detail?.NeedleAlignCompXLinkedVar;
            set
            {
                if (_detail == null || string.Equals(_detail.NeedleAlignCompXLinkedVar, value, StringComparison.Ordinal)) return;
                _detail.NeedleAlignCompXLinkedVar = value;
                RaisePropertyChanged(nameof(NeedleAlignCompXLinkedVar));
                RaisePropertyChanged(nameof(IsNeedleAlignCompXLinked));
                RaisePropertyChanged(nameof(NeedleAlignCompXDisplayValue));
                RecalculateFinalCoords();
            }
        }

        public string NeedleAlignCompYLinkedVar
        {
            get => _detail?.NeedleAlignCompYLinkedVar;
            set
            {
                if (_detail == null || string.Equals(_detail.NeedleAlignCompYLinkedVar, value, StringComparison.Ordinal)) return;
                _detail.NeedleAlignCompYLinkedVar = value;
                RaisePropertyChanged(nameof(NeedleAlignCompYLinkedVar));
                RaisePropertyChanged(nameof(IsNeedleAlignCompYLinked));
                RaisePropertyChanged(nameof(NeedleAlignCompYDisplayValue));
                RecalculateFinalCoords();
            }
        }

        public bool IsNeedleAlignCompXLinked => !string.IsNullOrEmpty(_detail?.NeedleAlignCompXLinkedVar);
        public bool IsNeedleAlignCompYLinked => !string.IsNullOrEmpty(_detail?.NeedleAlignCompYLinkedVar);

        /// <summary>对针补偿 X 的实时显示值</summary>
        public double NeedleAlignCompXDisplayValue => ResolveLinked(NeedleAlignCompX, NeedleAlignCompXLinkedVar);
        /// <summary>对针补偿 Y 的实时显示值</summary>
        public double NeedleAlignCompYDisplayValue => ResolveLinked(NeedleAlignCompY, NeedleAlignCompYLinkedVar);

        // —— 合计偏移 ——
        private double _totalOffsetX;
        /// <summary>参与 Final 坐标计算的 X 方向补偿合计</summary>
        public double TotalOffsetX { get => _totalOffsetX; private set => SetProperty(ref _totalOffsetX, value); }

        private double _totalOffsetY;
        /// <summary>参与 Final 坐标计算的 Y 方向补偿合计</summary>
        public double TotalOffsetY { get => _totalOffsetY; private set => SetProperty(ref _totalOffsetY, value); }

        #endregion

        #region 解除链接命令

        public DelegateCommand UnlinkCameraNeedleOffsetXCommand { get; }
        public DelegateCommand UnlinkCameraNeedleOffsetYCommand { get; }
        public DelegateCommand UnlinkNeedleAlignCompXCommand { get; }
        public DelegateCommand UnlinkNeedleAlignCompYCommand { get; }

        #endregion

        #region 对话框关闭

        /// <summary>请求关闭对话框时触发</summary>
        public event Action<object> RequestClose;

        /// <summary>是否可以关闭对话框</summary>
        public bool CanCloseDialog() => true;

        /// <summary>关闭命令</summary>
        public DelegateCommand CloseCommand { get; }

        #endregion

        public DispenseRotatedCoordsViewModel(INeedleCameraCalibrationProvider calibProvider, ILoggerService logger)
        {
            _ = calibProvider;
            _logger = logger;

            CloseCommand = new DelegateCommand(() => RequestClose?.Invoke(null));
            UnlinkCameraNeedleOffsetXCommand = new DelegateCommand(() => UnlinkLinkedVariable(
                () => CameraNeedleOffsetXDisplayValue, v => CameraNeedleOffsetX = v, v => CameraNeedleOffsetXLinkedVar = v));
            UnlinkCameraNeedleOffsetYCommand = new DelegateCommand(() => UnlinkLinkedVariable(
                () => CameraNeedleOffsetYDisplayValue, v => CameraNeedleOffsetY = v, v => CameraNeedleOffsetYLinkedVar = v));
            UnlinkNeedleAlignCompXCommand = new DelegateCommand(() => UnlinkLinkedVariable(
                () => NeedleAlignCompXDisplayValue, v => NeedleAlignCompX = v, v => NeedleAlignCompXLinkedVar = v));
            UnlinkNeedleAlignCompYCommand = new DelegateCommand(() => UnlinkLinkedVariable(
                () => NeedleAlignCompYDisplayValue, v => NeedleAlignCompY = v, v => NeedleAlignCompYLinkedVar = v));
        }

        /// <summary>
        /// 取消全局变量链接：将当前显示值保留到手动字段，再清空链接变量名。
        /// 使用 string.Empty 作为「已显式取消链接」标记，避免 Initialize 再次自动链接。
        /// </summary>
        private void UnlinkLinkedVariable(
            Func<double> getDisplayValue,
            Action<double> setManualValue,
            Action<string> setLinkedVar)
        {
            setManualValue(getDisplayValue());
            setLinkedVar(string.Empty);
        }

        /// <summary>
        /// 初始化坐标对照列表、变换参数与补偿配置。
        /// </summary>
        /// <param name="coordList">坐标对照列表（仅含 Cad/Rotated 坐标）</param>
        /// <param name="rotationAngle">旋转角度</param>
        /// <param name="mox">回转中心 X</param>
        /// <param name="moy">回转中心 Y</param>
        /// <param name="detail">点胶详情（补偿配置持久化目标）</param>
        /// <param name="needleIndex">当前针头索引（0/1）</param>
        /// <param name="globalVariables">可链接的全局变量集合</param>
        public void Initialize(
            List<DispenseRotatedCoordItem> coordList,
            double rotationAngle,
            double mox,
            double moy,
            DispenseDetail detail,
            int needleIndex,
            IEnumerable<GlobalVariable> globalVariables)
        {
            _suspendRecalc = true;

            _detail = detail;

            AvailableGlobalVariables.Clear();
            if (globalVariables != null)
                foreach (var v in globalVariables)
                    AvailableGlobalVariables.Add(v);

            // 首次使用时为对针补偿设置默认链接目标（null=从未配置；Empty=用户已显式取消链接）
            if (_detail != null)
            {
                if (_detail.NeedleAlignCompXLinkedVar == null)
                    _detail.NeedleAlignCompXLinkedVar = NeedleAlignerGlobalVariableNames.DefaultCompXLinkedVar;
                if (_detail.NeedleAlignCompYLinkedVar == null)
                    _detail.NeedleAlignCompYLinkedVar = NeedleAlignerGlobalVariableNames.DefaultCompYLinkedVar;
            }

            CoordItems.Clear();
            foreach (var item in coordList)
                CoordItems.Add(item);

            RotationAngle = rotationAngle;
            RotationCenterX = mox;
            RotationCenterY = moy;
            PointCount = coordList.Count;

            _suspendRecalc = false;

            // 通知所有补偿相关绑定刷新
            RaiseAllCompProperties();
            RecalculateFinalCoords();
        }

        /// <summary>解析相机与针头固定距离 X：Link 勾选时取全局变量，否则为 0</summary>
        private double ResolveCameraNeedleOffsetX()
        {
            if (_detail == null || !_detail.LinkCameraNeedleOffsetToCalibration)
                return 0;
            return ResolveLinked(CameraNeedleOffsetX, CameraNeedleOffsetXLinkedVar);
        }

        /// <summary>解析相机与针头固定距离 Y：Link 勾选时取全局变量，否则为 0</summary>
        private double ResolveCameraNeedleOffsetY()
        {
            if (_detail == null || !_detail.LinkCameraNeedleOffsetToCalibration)
                return 0;
            return ResolveLinked(CameraNeedleOffsetY, CameraNeedleOffsetYLinkedVar);
        }

        /// <summary>链接变量名非空时取全局变量值，否则取手动值</summary>
        private double ResolveLinked(double manualValue, string linkedVarName)
        {
            if (string.IsNullOrEmpty(linkedVarName)) return manualValue;
            var gv = AvailableGlobalVariables.FirstOrDefault(v => v.Name == linkedVarName);
            return gv != null && double.TryParse(gv.Value, out var val) ? val : manualValue;
        }

        /// <summary>重算合计偏移与每个点的最终坐标</summary>
        private void RecalculateFinalCoords()
        {
            if (_suspendRecalc) return;

            double offsetX = 0;
            double offsetY = 0;

            if (_detail != null && _detail.EnableNeedleOffsetComp)
            {
                // Camera-Needle Offset：Link 勾选时取全局变量
                offsetX += ResolveCameraNeedleOffsetX();
                offsetY += ResolveCameraNeedleOffsetY();
                // Needle Alignment Comp
                offsetX += NeedleAlignCompXDisplayValue;
                offsetY += NeedleAlignCompYDisplayValue;
            }

            // X/Y Comp（校准器）
            if (_detail != null && _detail.EnableZCalibration)
            {
                offsetX += ResolveLinked(_detail.XCompensationCalibrator, _detail.XCompensationCalibratorLinkedVar);
                offsetY += ResolveLinked(_detail.YCompensationCalibrator, _detail.YCompensationCalibratorLinkedVar);
            }

            // X/Y Compensation
            if (_detail != null && _detail.EnableComp)
            {
                offsetX += ResolveLinked(_detail.XCompensation, _detail.XCompensationLinkedVar);
                offsetY += ResolveLinked(_detail.YCompensation, _detail.YCompensationLinkedVar);
            }

            TotalOffsetX = Math.Round(offsetX, 3);
            TotalOffsetY = Math.Round(offsetY, 3);

            foreach (var item in CoordItems)
            {
                item.FinalX = Math.Round(item.RotatedX + offsetX, 3);
                item.FinalY = Math.Round(item.RotatedY + offsetY, 3);
            }
        }

        /// <summary>通知相机-针头固定距离显示相关绑定刷新</summary>
        private void RaiseCameraNeedleDisplayChanged()
        {
            RaisePropertyChanged(nameof(CameraNeedleOffsetXDisplayValue));
            RaisePropertyChanged(nameof(CameraNeedleOffsetYDisplayValue));
        }

        /// <summary>通知全部补偿绑定刷新（用于初始化后一次性刷新）</summary>
        private void RaiseAllCompProperties()
        {
            RaisePropertyChanged(nameof(EnableNeedleOffsetComp));
            RaisePropertyChanged(nameof(LinkCameraNeedleOffsetToCalibration));
            RaisePropertyChanged(nameof(CameraNeedleOffsetX));
            RaisePropertyChanged(nameof(CameraNeedleOffsetY));
            RaisePropertyChanged(nameof(CameraNeedleOffsetXLinkedVar));
            RaisePropertyChanged(nameof(CameraNeedleOffsetYLinkedVar));
            RaisePropertyChanged(nameof(IsCameraNeedleOffsetXLinked));
            RaisePropertyChanged(nameof(IsCameraNeedleOffsetYLinked));
            RaiseCameraNeedleDisplayChanged();
            RaisePropertyChanged(nameof(NeedleAlignCompX));
            RaisePropertyChanged(nameof(NeedleAlignCompY));
            RaisePropertyChanged(nameof(NeedleAlignCompXLinkedVar));
            RaisePropertyChanged(nameof(NeedleAlignCompYLinkedVar));
            RaisePropertyChanged(nameof(IsNeedleAlignCompXLinked));
            RaisePropertyChanged(nameof(IsNeedleAlignCompYLinked));
            RaisePropertyChanged(nameof(NeedleAlignCompXDisplayValue));
            RaisePropertyChanged(nameof(NeedleAlignCompYDisplayValue));
            RaisePropertyChanged(nameof(NeedleAlignCompXPrefix));
            RaisePropertyChanged(nameof(NeedleAlignCompYPrefix));
        }
    }
}

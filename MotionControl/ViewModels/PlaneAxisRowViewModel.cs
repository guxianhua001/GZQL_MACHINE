using MotionControl.Models;
using Prism.Mvvm;
using System;

namespace MotionControl.ViewModels
{
    /// <summary>
    /// 平面锁定轴（如 Dx/Dy）配置行的可绑定包装：代理底层 AxisDangerZoneConfig（危险区边界），
    /// 使 UI 可动态增删任意数量的平面轴，而非固定 Dx/Dy 两个
    /// </summary>
    public class PlaneAxisRowViewModel : BindableBase
    {
        private readonly AxisDangerZoneConfig _model;
        private readonly Action<string, string> _onRenamed;
        private readonly Action _onChanged;

        public PlaneAxisRowViewModel(AxisDangerZoneConfig model, Action<string, string> onRenamed, Action onChanged)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _onRenamed = onRenamed;
            _onChanged = onChanged;
        }

        /// <summary>底层配置对象，供父 ViewModel 增删操作时定位</summary>
        public AxisDangerZoneConfig Model => _model;

        /// <summary>轴名称（从硬件轴列表中选择）；改名时同步更新规则的 LockedAxes 引用</summary>
        public string AxisName
        {
            get => _model.AxisName;
            set
            {
                if (string.Equals(_model.AxisName, value, StringComparison.Ordinal)) return;
                string old = _model.AxisName;
                _model.AxisName = value ?? string.Empty;
                RaisePropertyChanged();
                _onRenamed?.Invoke(old, _model.AxisName);
                _onChanged?.Invoke();
            }
        }

        /// <summary>危险区下限（mm）</summary>
        public double DangerMin
        {
            get => _model.Min;
            set
            {
                if (_model.Min.Equals(value)) return;
                _model.Min = value;
                RaisePropertyChanged();
                _onChanged?.Invoke();
            }
        }

        /// <summary>危险区上限（mm）</summary>
        public double DangerMax
        {
            get => _model.Max;
            set
            {
                if (_model.Max.Equals(value)) return;
                _model.Max = value;
                RaisePropertyChanged();
                _onChanged?.Invoke();
            }
        }

        #region 实时状态（只读，由定时刷新写入，不回写配置文件）

        private double _currentPosition;
        /// <summary>当前实时位置（mm）</summary>
        public double CurrentPosition { get => _currentPosition; set => SetProperty(ref _currentPosition, value); }

        private bool _isInDanger;
        /// <summary>当前是否处于危险区内</summary>
        public bool IsInDanger { get => _isInDanger; set => SetProperty(ref _isInDanger, value); }

        #endregion
    }
}

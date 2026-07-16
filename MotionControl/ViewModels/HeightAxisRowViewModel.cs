using MotionControl.Models;
using Prism.Mvvm;
using System;

namespace MotionControl.ViewModels
{
    /// <summary>
    /// 高度轴（Z）配置行的可绑定包装：直接代理底层 HeightAxisSafeConfig 的读写，
    /// 使 UI 可动态增删任意数量的高度轴，而非固定 Dz₁/Dz₂/Dz₃ 三个
    /// </summary>
    public class HeightAxisRowViewModel : BindableBase
    {
        private readonly HeightAxisSafeConfig _model;
        private readonly Action _onChanged;

        public HeightAxisRowViewModel(HeightAxisSafeConfig model, Action onChanged)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _onChanged = onChanged;
        }

        /// <summary>底层配置对象，供父 ViewModel 增删操作时定位</summary>
        public HeightAxisSafeConfig Model => _model;

        /// <summary>轴名称（从硬件轴列表中选择，避免手动输入拼写错误）</summary>
        public string AxisName
        {
            get => _model.AxisName;
            set
            {
                if (string.Equals(_model.AxisName, value, StringComparison.Ordinal)) return;
                _model.AxisName = value ?? string.Empty;
                RaisePropertyChanged();
                _onChanged?.Invoke();
            }
        }

        public double SafeHeight
        {
            get => _model.SafeHeight;
            set
            {
                if (_model.SafeHeight.Equals(value)) return;
                _model.SafeHeight = value;
                RaisePropertyChanged();
                _onChanged?.Invoke();
            }
        }

        public bool InvertedDirection
        {
            get => _model.InvertedDirection;
            set
            {
                if (_model.InvertedDirection == value) return;
                _model.InvertedDirection = value;
                RaisePropertyChanged();
                _onChanged?.Invoke();
            }
        }

        /// <summary>是否参与互锁判断；关闭后该轴永远不会导致平面轴被锁定</summary>
        public bool Enabled
        {
            get => _model.Enabled;
            set
            {
                if (_model.Enabled == value) return;
                _model.Enabled = value;
                RaisePropertyChanged();
                _onChanged?.Invoke();
            }
        }

        #region 实时状态（只读，由定时刷新写入，不回写配置文件）

        private double _currentPosition;
        /// <summary>当前实时位置（mm）</summary>
        public double CurrentPosition { get => _currentPosition; set => SetProperty(ref _currentPosition, value); }

        private bool _isBelowSafe;
        /// <summary>当前是否未达安全高度</summary>
        public bool IsBelowSafe { get => _isBelowSafe; set => SetProperty(ref _isBelowSafe, value); }

        #endregion
    }
}

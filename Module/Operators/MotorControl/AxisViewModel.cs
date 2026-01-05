using Core.Services;
using Framework.ViewModels;
using Prism.Commands;
using Prism.Ioc;
using Prism.Regions;
using SmarterMotion;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MaterialDesignThemes.Wpf;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using SmarterMotion.Framework.Plc;
using Framework.Mvvm;
using Core.Abstraction;
using System.ComponentModel;

namespace Framework.ViewModels
{
    public class AxisViewModel: RegionViewModelBase, INavigationAware
    {
        public AxisViewModel(IAxis axisService, IRegionManager regionManager, ILocalizationService localizationService) : base(regionManager)
        {
            _axisService = axisService;
            _localizationService = localizationService;
            InitializeData();
            SetupStatusTimer();
            // 监听语言变化事件
            _localizationService.LanguageChanged += OnLocalizationServiceLanguageChanged;
        }
        private readonly IAxis _axisService;
        private readonly ILocalizationService _localizationService;
        private DispatcherTimer _statusTimer;

        public string AxisName => _axisService?.Name;

        // 本地化轴名称 - 使用资源键格式: Axis_{原始轴名称}
        public string LocalizedAxisName
        {
            get
            {
                if (string.IsNullOrEmpty(AxisName))
                    return string.Empty;

                string resourceKey = $"Axis_{AxisName}";
                return _localizationService.GetResourceOrDefault(resourceKey, AxisName);
            }
        }
        public XAxisDirection AxisDirection => _axisService.AxisDirection;
        public int AxisId => _axisService.ActId;
        public ObservableCollection<IAxis> Axises { get; } = new();
        public ObservableCollection<double> DistanceOptions { get; } = new();

        //private IAxis _selectedAxis;
        //public IAxis SelectedAxis
        //{
        //    get => _selectedAxis;
        //    set => SetProperty(ref _selectedAxis, value, OnAxisChanged);
        //}
        private double _selectedDistance;
        public double SelectedDistance
        {
            get => _selectedDistance;
            set
            {
                if (SetProperty(ref _selectedDistance, value))
                {
                    // 当 SelectedDistance 变化时，更新 Distance 属性
                    Distance = value;
                }
            }
        }
        private double maximum = 60;
        public double Maximum
        {
            get => maximum;
            set => SetProperty(ref maximum, value);
        }
        private double minimum = 1;
        public double Minimum
        {
            get => minimum;
            set => SetProperty(ref minimum, value);
        }
        private int _tickFrequency = 1;
        public int TickFrequency
        {
            get => _tickFrequency;
            set => SetProperty(ref _tickFrequency, value);
        }
        private double _currentPosition;
        public double CurrentPosition
        {
            get => _currentPosition;
            set => SetProperty(ref _currentPosition, value);
        }
        private double m_Distance = 0.1;
        public double Distance
        {
            get => m_Distance;
            set => SetProperty(ref m_Distance, value);
        }
        private double _velocity = 10;
        public double Velocity
        {
            get => _velocity;
            set => SetProperty(ref _velocity, value);
        }
        private double _acceleration = 0.1;
        public double Acceleration
        {
            get => _acceleration;
            set => SetProperty(ref _acceleration, value);
        }

        private DelegateCommand _MovePositiveCommand;
        public DelegateCommand MovePositiveCommand =>
             _MovePositiveCommand ??= new DelegateCommand(() => ExecuteMove(m_Distance));

        private DelegateCommand _MoveNegativeCommand;
        public DelegateCommand MoveNegativeCommand =>
             _MoveNegativeCommand ??= new DelegateCommand(() => ExecuteMove(-m_Distance));

        private DelegateCommand _HomeCommand;
        public DelegateCommand HomeCommand =>
             _HomeCommand ??= new DelegateCommand(ExecuteHome);

        private DelegateCommand _StopCommand;
        public DelegateCommand StopCommand =>
             _StopCommand ??= new DelegateCommand(ExecuteStop);

        private DelegateCommand _ClearPosition;
        public DelegateCommand ClearPosition =>
             _ClearPosition ??= new DelegateCommand(ExecuteClearPosition);
        private DelegateCommand _ClearAlarm;
        public DelegateCommand ClearAlarm =>
             _ClearAlarm ??= new DelegateCommand(ExecuteClearAlarm);

        private DelegateCommand _ToggleServoOnCommand;
        public DelegateCommand ToggleServoOnCommand =>
             _ToggleServoOnCommand ??= new DelegateCommand(ExecuteToggleOnServo);

        private DelegateCommand _ToggleServoOffCommand;
        public DelegateCommand ToggleServoOffCommand =>
             _ToggleServoOffCommand ??= new DelegateCommand(ExecuteToggleOffServo);

        private DelegateCommand<MouseButtonEventArgs> _JogPositiveCommand;
        public DelegateCommand<MouseButtonEventArgs> JogPositiveCommand =>
             _JogPositiveCommand ??= new DelegateCommand<MouseButtonEventArgs>(e => StartJog(1));

        private DelegateCommand<MouseButtonEventArgs> _JogNegativeCommand;
        public DelegateCommand<MouseButtonEventArgs> JogNegativeCommand =>
             _JogNegativeCommand ??= new DelegateCommand<MouseButtonEventArgs>(e => StartJog(0));

        private async void ExecuteMove(double distance)
        {
            if (!ValidateAxisReady()) return;
            _axisService.SetAxisAccAndDec(Acceleration, Acceleration);
            _axisService.MoveRel(distance, Velocity);
        }

        private async void ExecuteHome()
        {
            if (!_axisService.IsSVON) return;

            _axisService.GoHome();
        }
        public void ExecuteStop()
        {
            _isJogging = false;
            _axisService.Stop();
        }

        private void ExecuteToggleOnServo()
        {
            _axisService.SetServo(true);
        }
        private void ExecuteToggleOffServo()
        {
            _axisService.SetServo(false);
        }
        private bool _isJogging;
        public void StartJog(int direction)
        {
            if (!ValidateAxisReady()) return;
            if (_isJogging) return;
            _isJogging = true;
            _axisService.SetAxisJogParam(Acceleration, Acceleration, Velocity);
            _axisService.MoveJog(direction);
        }
        private void ExecuteClearPosition()
        {
            if (!ValidateAxisReady()) return;

            _axisService.ClearPosition();
        }
        private void ExecuteClearAlarm()
        {
            //if (!ValidateAxisReady()) return;

            _axisService.CleanALM();
        }
        private bool ValidateAxisReady()
        {
            return _axisService?.IsSVON == true;//&& _axisService.IsHomeOk;
        }
        private void InitializeData()
        {
            // 初始化距离选项
            DistanceOptions.AddRange(new[] { 0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5, 1, 2, 5, 10, 20 });

            // 加载轴列表
            //Axises.AddRange(_axisService.GetAllAxises());
            //SelectedAxis = Axises.FirstOrDefault();

            SelectedDistance = m_Distance; // 初始值
        }
        private void SetupStatusTimer()
        {
            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _statusTimer.Tick += (s, e) => UpdateStatus();
            //_statusTimer.Start();
        }
        private void UpdateStatus()
        {
            if (_axisService == null) return;

            CurrentPosition = _axisService.IsFeedback
                ? _axisService.POS
                : _axisService.CommandPOS;

            // 更新状态指示属性
            RaisePropertyChanged(nameof(IsServoOn));
            RaisePropertyChanged(nameof(IsMEL));
            RaisePropertyChanged(nameof(IsORG));
            RaisePropertyChanged(nameof(IsPEL));
            RaisePropertyChanged(nameof(IsALM));
            RaisePropertyChanged(nameof(IsHomeOk));
            RaisePropertyChanged(nameof(AxisName));
            RaisePropertyChanged(nameof(AxisDirection));
            RaisePropertyChanged(nameof(AxisId));
            //RaisePropertyChanged(nameof(HomeStatus));
        }

        // 状态指示属性
        public bool IsServoOn => _axisService?.IsSVON ?? false;
        public bool IsMEL => _axisService?.IsMEL ?? false;
        public bool IsORG => _axisService?.IsORG ?? false;
        public bool IsPEL => _axisService?.IsPEL ?? false;
        public bool IsALM => _axisService?.IsALM ?? false;
        public bool IsHomeOk => _axisService?.IsHomeOk ?? false;
        public bool IsASTP => _axisService?.IsASTP ?? false;
        //public string HomeStatus => IsHomeOk ? "已初始化" : "未初始化";

        // 本地化的Home状态
        public string LocalizedHomeStatus
        {
            get
            {
                if (_axisService == null)
                    return _localizationService?.GetResourceOrDefault("HomeStatus_NotInitialized", "未初始化");

                string resourceKey = IsHomeOk ? "HomeStatus_Initialized" : "HomeStatus_NotInitialized";
                return _localizationService?.GetResourceOrDefault(resourceKey,
                    IsHomeOk ? "已初始化" : "未初始化");
            }
        }
        // 语言变化处理
        private void OnLocalizationServiceLanguageChanged(object sender, LanguageChangedEventArgs e)
        {
            // 更新本地化属性
            RaisePropertyChanged(nameof(LocalizedAxisName));
            RaisePropertyChanged(nameof(LocalizedHomeStatus));
        }

        #region 导航生命周期管理
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            // 启动定时器
            if (_statusTimer != null && !_statusTimer.IsEnabled)
                _statusTimer.Start();
        }

        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            // 停止定时器
            if (_statusTimer != null && _statusTimer.IsEnabled)
                _statusTimer.Stop();
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void Dispose()
        {
            if (_statusTimer != null)
            {
                _statusTimer.Stop();
                _statusTimer = null;
            }

            if (_localizationService != null)
            {
                _localizationService.LanguageChanged -= OnLocalizationServiceLanguageChanged;
            }
        }
        #endregion
    }
}

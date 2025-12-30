using Prism.Commands;
using Prism.Mvvm;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Drawing;
using Interfaces.SharedInterfaces;
using System.Linq;
using Prism.Regions;
using System;
using System.Windows;
using ModuleCore.Services;

namespace Framework.ViewModels
{
    public class ControlPanelViewModel : BindableBase
    {
        private string _synchronizationStatus = "同步关闭";
        // 添加系统状态相关属性
        private string _statusText = "系统就绪";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private string _statusColor = "Green";
        public string StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        // 添加基准位置显示属性
        private string _upperBasePosition = "未记录";
        public string UpperBasePosition
        {
            get => _upperBasePosition;
            set => SetProperty(ref _upperBasePosition, value);
        }

        private string _lowerBasePosition = "未记录";
        public string LowerBasePosition
        {
            get => _lowerBasePosition;
            set => SetProperty(ref _lowerBasePosition, value);
        }

        public bool IsSynchronizing
        {
            get => _controlPanelService.CurrentSystem?.IsSynchronizing ?? false;
            set
            {
                if (_controlPanelService.CurrentSystem != null)
                {
                    _controlPanelService.CurrentSystem.EnableSynchronization(value);
                    SynchronizationStatus = value ? "同步开启" : "同步关闭";
                    RaisePropertyChanged();
                }
            }
        }

        public string SynchronizationStatus
        {
            get => _synchronizationStatus;
            set => SetProperty(ref _synchronizationStatus, value);
        }

        public ObservableCollection<string> GantryTypes { get; } =
            new ObservableCollection<string> { "上龙门", "下龙门" };

        private string _selectedGantryType = "上龙门";
        public string SelectedGantryType
        {
            get => _selectedGantryType;
            set => SetProperty(ref _selectedGantryType, value);
        }

        private double _targetX;
        public double TargetX
        {
            get => _targetX;
            set => SetProperty(ref _targetX, value);
        }

        private double _targetY;
        public double TargetY
        {
            get => _targetY;
            set => SetProperty(ref _targetY, value);
        }

        private double _moveSpeed = 50;
        public double MoveSpeed
        {
            get => _moveSpeed;
            set => SetProperty(ref _moveSpeed, value);
        }

        private double _syncError;
        public double SyncError
        {
            get => _syncError;
            set => SetProperty(ref _syncError, value);
        }

        public DelegateCommand MoveToTargetCommand { get; }
        public DelegateCommand RecordBaseCommand { get; }
        public DelegateCommand StopAllCommand { get; }
        public DelegateCommand ResetSystemCommand { get; }

        private readonly IControlPanelService _controlPanelService;

        GantrySystemInfo _selectedSystem;
        public ObservableCollection<GantrySystemInfo> SystemOptions { get; set; } = new ObservableCollection<GantrySystemInfo>();

        public GantrySystemInfo SelectedSystem
        {
            get => _selectedSystem;
            set
            {
                if (SetProperty(ref _selectedSystem, value))
                {
                    _controlPanelService.SelectSystem(value.Id);
                }
            }
        }
        private void UpdateAllFromCurrentSystem()
        {
            if (_controlPanelService.CurrentSystem == null) return;

            var system = _controlPanelService.CurrentSystem;
            system.LoadBasePositions();
            // 更新基准位置显示
            UpperBasePosition = system.BasePositionUpper == PointF.Empty
                ? "未记录"
                : $"{system.BasePositionUpper.X:F1}, {system.BasePositionUpper.Y:F1}";

            LowerBasePosition = system.BasePositionLower == PointF.Empty
                ? "未记录"
                : $"{system.BasePositionLower.X:F1}, {system.BasePositionLower.Y:F1}";

            // 更新同步状态
            SynchronizationStatus = system.IsSynchronizing ? "同步开启" : "同步关闭";
            RaisePropertyChanged(nameof(IsSynchronizing));

            // 更新其他需要响应的属性...
        }
        private double _jogSpeed = 30;
        public double JogSpeed
        {
            get => _jogSpeed;
            set => SetProperty(ref _jogSpeed, value);
        }

        private PointF _upperPosition = PointF.Empty;
        public PointF UpperPosition
        {
            get => _upperPosition;
            set => SetProperty(ref _upperPosition, value, () =>
            {
                RaisePropertyChanged(nameof(UpperPositionDisplay));
            });
        }

        private PointF _lowerPosition = PointF.Empty;
        public PointF LowerPosition
        {
            get => _lowerPosition;
            set => SetProperty(ref _lowerPosition, value, () =>
            {
                RaisePropertyChanged(nameof(LowerPositionDisplay));
            });
        }

        public string UpperPositionDisplay => $"{UpperPosition.X:F1}, {UpperPosition.Y:F1}";
        public string LowerPositionDisplay => $"{LowerPosition.X:F1}, {LowerPosition.Y:F1}";

        // Jog命令
        public DelegateCommand<JogDirection?> JogCommand { get; }
        public DelegateCommand StopJogCommand { get; }

        public ControlPanelViewModel(IControlPanelService controlPanelService)
        {
            _controlPanelService = controlPanelService;
            _controlPanelService.SystemChanged += OnSystemChanged;

            // 初始化系统选项
            foreach (var system in _controlPanelService.AvailableSystems)
            {
                SystemOptions.Add(system);
            }

            // 初始化选定系统
            SelectedSystem = SystemOptions.FirstOrDefault(s =>
                s.Id == _controlPanelService.GetCurrentSystemInfo()?.Id);
            // 初始化命令
            MoveToTargetCommand = new DelegateCommand(ExecuteMoveToTarget);
            RecordBaseCommand = new DelegateCommand(ExecuteRecordBase);
            StopAllCommand = new DelegateCommand(ExecuteStopAll);
            ResetSystemCommand = new DelegateCommand(ExecuteResetSystem);
            JogCommand = new DelegateCommand<JogDirection?>(ExecuteJog);
            StopJogCommand = new DelegateCommand(ExecuteStopJog);
            // 订阅当前系统的事件
            SubscribeToCurrentSystemEvents();
        }
        private void SubscribeToCurrentSystemEvents()
        {
            var currentSystem = _controlPanelService.CurrentSystem;
            if (currentSystem != null)
            {
                currentSystem.StatusChanged += OnStatusChanged;
                currentSystem.PositionUpdated += OnPositionUpdated;
            }
        }
        private void UnsubscribeFromCurrentSystemEvents()
        {
            var currentSystem = _controlPanelService.CurrentSystem;
            if (currentSystem != null)
            {
                currentSystem.StatusChanged -= OnStatusChanged;
                currentSystem.PositionUpdated -= OnPositionUpdated;
            }
        }
        private void OnBasePositionUpdated()
        {
            Application.Current.Dispatcher.Invoke(UpdateBasePositionsDisplay);
        }

        private string FormatPosition(PointF position)
        {
            return position == PointF.Empty
                ? "未记录"
                : $"{position.X:F1}, {position.Y:F1}";
        }

        private void ExecuteRecordBase()
        {
            if (_controlPanelService.CurrentSystem != null)
            {
                // 记录并触发事件
                _controlPanelService.CurrentSystem.RecordBasePositions();
            }
        }

        private void LoadCurrentSystemConfig()
        {
            var system = _controlPanelService.CurrentSystem;
            if (system != null)
            {
                // 显示从配置文件加载的位置
                UpdateBasePositionsDisplay();
            }
        }
        private void OnSystemChanged(object sender, EventArgs e)
        {
            // 系统切换时更新事件订阅
            UnsubscribeFromCurrentSystemEvents();
            SubscribeToCurrentSystemEvents();

            // UI更新必须在主线程执行
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 1. 更新选定系统与当前系统一致
                var currentInfo = _controlPanelService.GetCurrentSystemInfo();

                //if (SelectedSystem?.Id != currentInfo?.Id)
                //{
                    _selectedSystem = SystemOptions.FirstOrDefault(s => s.Id == currentInfo.Id);
                    RaisePropertyChanged(nameof(SelectedSystem));
                //}

                // 2. 更新UI显示
                UpdateAllFromCurrentSystem();
            });
        }
        private void UpdateControlsFromCurrentSystem()
        {
            var system = _controlPanelService.CurrentSystem;
            if (system == null) return;

            // 更新UI状态
            UpperBasePosition = system.BasePositionUpper == PointF.Empty
                ? "未记录"
                : $"{system.BasePositionUpper.X:F1}, {system.BasePositionUpper.Y:F1}";

            LowerBasePosition = system.BasePositionLower == PointF.Empty
                ? "未记录"
                : $"{system.BasePositionLower.X:F1}, {system.BasePositionLower.Y:F1}";

            // 更新同步状态显示
            SynchronizationStatus = system.IsSynchronizing ? "同步开启" : "同步关闭";
            RaisePropertyChanged(nameof(IsSynchronizing));
        }
        private void OnStatusChanged(string message)
        {
            // 当状态变化时更新基准位置显示
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 更新状态消息
                StatusText = message;

                // 根据消息类型改变状态灯颜色
                if (message.Contains("错误"))
                    StatusColor = "Red";
                else if (message.Contains("警告"))
                    StatusColor = "Yellow";
                else if (message.Contains("就绪"))
                    StatusColor = "Green";
                else
                    StatusColor = "Blue";

                // 当状态变化时更新基准位置显示
                if (message.Contains("基准位置") || message.Contains("完成"))
                {
                    UpdateControlsFromCurrentSystem();
                }
                RaisePropertyChanged(nameof(StatusColor));
            });
        }
        private void UpdateBasePositionsDisplay()
        {
            if (_controlPanelService.CurrentSystem != null)
            {
                var upper = _controlPanelService.CurrentSystem.BasePositionUpper;
                var lower = _controlPanelService.CurrentSystem.BasePositionLower;

                UpperBasePosition = upper == PointF.Empty
                    ? "未记录"
                    : $"{upper.X:F1}, {upper.Y:F1}";

                LowerBasePosition = lower == PointF.Empty
                    ? "未记录"
                    : $"{lower.X:F1}, {lower.Y:F1}";
            }
        }
        private void OnPositionUpdated(GantryState state)
        {
            SyncError = state.SyncError;
            UpperPosition = new PointF(state.UpperPosition.X, state.UpperPosition.X);
            LowerPosition = new PointF(state.LowerPosition.X, state.LowerPosition.X);
        }

        private async void ExecuteMoveToTarget()
        {
            if (_controlPanelService.CurrentSystem == null) return;

            GantryType gantryType = SelectedGantryType == "上龙门"
                ? GantryType.Upper : GantryType.Lower;
            await _controlPanelService.CurrentSystem.MoveBothToTarget(
                new PointF((float)TargetX, (float)TargetY),
                gantryType,
                MoveSpeed
            );
        }

        private void ExecuteStopAll()
        {
            if (_controlPanelService.CurrentSystem != null)
            {
                _controlPanelService.CurrentSystem.StopAllMotion();
            }
        }

        private void ExecuteResetSystem()
        {
            if (_controlPanelService.CurrentSystem != null)
            {
                // 假设归零作为安全位置
                _controlPanelService.CurrentSystem.ResetSystem(new PointF(0, 0));
            }
        }
        private void ExecuteJog(JogDirection? directionNullable)
        {
            if (_controlPanelService.CurrentSystem == null) return;

            // 获取实际的方向值
            var direction = directionNullable.Value;

            _controlPanelService.CurrentSystem.Jog(
                SelectedGantryType == "上龙门" ? GantryType.Upper : GantryType.Lower,
                direction,
                JogSpeed,
                IsSynchronizing
            );
        }

        private void ExecuteStopJog()
        {
            if (_controlPanelService.CurrentSystem != null)
            {
                _controlPanelService.CurrentSystem.StopJog();
            }
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            // 1. 优先从导航参数获取系统ID
            int? targetSystemId = null;
            if (navigationContext.Parameters.TryGetValue("system", out int sid))
                targetSystemId = sid;
            else if (navigationContext.Parameters.TryGetValue("systemId", out sid))
                targetSystemId = sid;

            // 2. 更新服务层当前系统
            if (targetSystemId.HasValue)
            {
                _controlPanelService.SelectSystem(targetSystemId.Value);
            }

            // 3. 同步视图模型的选择状态
            var currentInfo = _controlPanelService.GetCurrentSystemInfo();
            if (SelectedSystem?.Id != currentInfo?.Id && SystemOptions.Any(w => w.Id == currentInfo?.Id))
            {
                SelectedSystem = SystemOptions.First(s => s.Id == currentInfo.Id);
            }
            else
            {
                // 强制更新UI（即使选择没变，数据可能已更新）
                UpdateControlsFromCurrentSystem();
            }
        }
        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            // 从导航参数提取系统ID
            int? navigatedSystemId = null;
            navigationContext.Parameters.TryGetValue("system", out int sid);
            navigatedSystemId = sid;

            navigationContext.Parameters.TryGetValue("systemId", out sid);
            navigatedSystemId = sid;

            // 仅当导航到同一系统时复用实例
            return navigatedSystemId.HasValue &&
                   navigatedSystemId == SelectedSystem?.Id;
        }
    }
}


using Framework.Mvvm;
using Interfaces.SharedInterfaces;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Regions;
using System;

namespace Framework.ViewModels
{
    public class ShellViewModel : RegionViewModelBase
    {
        private readonly IRegionManager _regionManager;
        private readonly IGantrySyncService _syncService;

        private string _statusMessage = "就绪";
        public string StatusText
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _statusColor = "Green";
        public string StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        public DelegateCommand<string> NavigateCommand { get; private set; }
        private readonly IControlPanelService _controlPanelService;

        public string CurrentSystemName => _controlPanelService.CurrentSystemName;

        public ShellViewModel(IRegionManager regionManager, IControlPanelService controlPanelService) : base(regionManager)
        {
            _regionManager = regionManager;
            _controlPanelService = controlPanelService;
            _controlPanelService.SystemChanged += OnSystemChanged;

            _controlPanelService.SystemManager.CurrentSystem.StatusChanged += OnServiceStatusChanged;

            NavigateCommand = new DelegateCommand<string>(Navigate);
            Navigate("StatusDashboardView");
        }
        private void OnServiceStatusChanged(string statusMessage)
        {
            StatusText = statusMessage;
            StatusColor = statusMessage.Contains("错误") ? "Red" : "Green";
        }
        private void OnSystemChanged(object sender, EventArgs e)
        {
            // 取消旧系统的订阅
            if (_controlPanelService.CurrentSystem != null)
                _controlPanelService.CurrentSystem.StatusChanged -= OnServiceStatusChanged;

            // 订阅新系统的状态变化
            _controlPanelService.CurrentSystem.StatusChanged += OnServiceStatusChanged;

            // 更新状态显示
            //StatusText = _controlPanelService.CurrentSystem.StatusMessage;
            StatusColor = StatusText.Contains("错误") ? "Red" : "Green";

            // 通知UI更新
            RaisePropertyChanged(nameof(CurrentSystemName));
        }
        private void Navigate(string navigatePath)
        {
            if (navigatePath != null)
                _regionManager.RequestNavigate("ContentRegion", navigatePath);
        }
    }
    
}


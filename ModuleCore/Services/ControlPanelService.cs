using Interfaces.SharedInterfaces;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;

namespace ModuleCore.Services
{
    public class ControlPanelService : BindableBase, IControlPanelService
    {
        private bool _isInternalUpdate = false;
        private GantrySystemInfo _selectedSystem;
        private readonly ICurrentSystemService _systemManager;

        public event EventHandler SystemChanged;
        public event Action UpdateBasePositionEvent;

        public PointF BasePositionUpper { get; } = new PointF(0, 0);
        public PointF BasePositionLower { get; } = new PointF(0, 0);
        public ICurrentSystemService SystemManager => _systemManager;
        public ObservableCollection<GantrySystemInfo> AvailableSystems { get; }

        public GantrySystemInfo SelectedSystem
        {
            get => _selectedSystem;
            set
            {
                if (SetProperty(ref _selectedSystem, value) && value != null)
                {
                    // 仅当非内部更新时才切换系统
                    if (!_isInternalUpdate)
                    {
                        _systemManager.SelectSystem(value.Id);
                    }
                }
            }
        }

        public string CurrentSystemName => _selectedSystem?.Name;
        public IGantrySyncService CurrentSystem => _systemManager.CurrentSystem;

        public ControlPanelService(ICurrentSystemService systemManager)
        {
            _systemManager = systemManager;
            AvailableSystems = new ObservableCollection<GantrySystemInfo>
            {
                new GantrySystemInfo(1, "系统1"),
                new GantrySystemInfo(2, "系统2")
            };

            // 初始选择与系统管理器同步
            _isInternalUpdate = true;
            _selectedSystem = AvailableSystems.First(s => s.Id == _systemManager.CurrentSystem.SystemId);
            _isInternalUpdate = false;

            _systemManager.SystemChanged += OnSystemManagerChanged;
        }

        private void OnSystemManagerChanged(object sender, EventArgs e)
        {
            // 同步更新选中系统
            var currentSystemId = _systemManager.CurrentSystem.SystemId;
            var newSystem = AvailableSystems.FirstOrDefault(s => s.Id == currentSystemId);

            if (newSystem != null && newSystem != _selectedSystem)
            {
                _isInternalUpdate = true;
                SelectedSystem = newSystem;
                _isInternalUpdate = false;
            }

            SystemChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SelectSystem(int systemId)
        {
            // 通过系统管理器切换
            _systemManager.SelectSystem(systemId);
        }

        public GantrySystemInfo GetCurrentSystemInfo()
        {
            // 直接从系统管理器获取信息
            var systemId = _systemManager.CurrentSystem.SystemId;
            return AvailableSystems.FirstOrDefault(s => s.Id == systemId);
        }

    }
}
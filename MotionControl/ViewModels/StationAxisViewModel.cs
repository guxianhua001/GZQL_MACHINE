using Core.Abstraction;
using MotionControl.Interfaces;
using MotionControl.Models;
using MotionControl.Views;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace MotionControl.ViewModels
{
    /// <summary>
    /// 单个工站的轴控制 ViewModel
    /// 管理该工站下所有轴的列表
    /// </summary>
    public class StationAxisViewModel : BindableBase, IDisposable
    {
        private readonly IMotionService _motionService;
        private readonly ILocalizationService _localizationService;
        private readonly ISafetyZoneMonitor _safetyZoneMonitor;
        private readonly int _stationId;
        private readonly string _stationName;

        // 预创建并缓存的 View 实例，避免 TabControl 切换 Tab 时重建整个视觉树
        private StationAxisView _cachedView;

        public int StationId => _stationId;
        public string StationName => _stationName;

        // 该工站下的所有轴
        public ObservableCollection<SingleAxisViewModel> Axes { get; } = new();

        // 预缓存的 View（懒加载），TabControl 切换时复用同一实例，避免重建视觉树
        public StationAxisView View
        {
            get
            {
                if (_cachedView == null)
                {
                    Application.Current.Dispatcher.VerifyAccess();
                    _cachedView = new StationAxisView { DataContext = this };
                }
                return _cachedView;
            }
        }

        // 紧急停止命令
        public DelegateCommand EmergencyStopCommand { get; }

        public StationAxisViewModel(int stationId, string stationName, 
                                   IEnumerable<AxisConfig> axisConfigs,
                                   IMotionService motionService, 
                                   ILocalizationService localizationService,
                                   ISafetyZoneMonitor safetyZoneMonitor = null)
        {
            _stationId = stationId;
            _stationName = stationName ?? $"Station_{stationId}";
            _motionService = motionService ?? throw new ArgumentNullException(nameof(motionService));
            _localizationService = localizationService;
            _safetyZoneMonitor = safetyZoneMonitor;

            EmergencyStopCommand = new DelegateCommand(ExecuteEmergencyStop);

            // 初始化所有轴
            InitializeAxes(axisConfigs);
        }

        /// <summary>
        /// 从配置列表初始化所有轴
        /// </summary>
        private void InitializeAxes(IEnumerable<AxisConfig> axisConfigs)
        {
            if (axisConfigs == null) return;

            foreach (var axisConfig in axisConfigs.OrderBy(a => a.LogicalId))
            {
                var axisVM = new SingleAxisViewModel(axisConfig, _motionService, _localizationService, _safetyZoneMonitor);
                Axes.Add(axisVM);
            }
        }

        /// <summary>
        /// 紧急停止该工站的所有轴
        /// </summary>
        private void ExecuteEmergencyStop()
        {
            try
            {
                // 并发停止所有轴（不等待完成）
                foreach (var axis in Axes)
                {
                    axis.ExecuteStop();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Emergency stop error for station {_stationName}: {ex.Message}");
            }
        }

        public void Dispose()
        {
            foreach (var axis in Axes)
            {
                axis.Dispose();
            }
            Axes.Clear();
        }
    }
}

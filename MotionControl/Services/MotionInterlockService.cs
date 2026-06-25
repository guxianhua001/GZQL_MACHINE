using Core.Abstraction;
using MotionControl.Events;
using MotionControl.Exceptions;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Events;

namespace MotionControl.Services
{
    /// <summary>
    /// 工艺手动运动互锁：订阅状态变更，仅 WAITRUN 允许（轴操作面板除外）。
    /// </summary>
    public class MotionInterlockService : IMotionInterlockService
    {
        private readonly ILocalizationService _localization;
        private volatile StationState _currentState = StationState.WAITRESET;
        private SubscriptionToken _stateToken;

        public MotionInterlockService(IEventAggregator ea, ILocalizationService localization)
        {
            _localization = localization;
            _stateToken = ea.GetEvent<StationStateChangedEvent>()
                .Subscribe(payload => _currentState = payload.State, ThreadOption.PublisherThread, false);
        }

        /// <inheritdoc />
        public bool CanExecuteManualMotion => _currentState == StationState.WAITRUN;

        /// <inheritdoc />
        public string GetBlockedMessage()
        {
            string resourceKey = _currentState switch
            {
                StationState.ESTOP => "MotionBlocked_EStop",
                StationState.WAITRESET => "MotionBlocked_RequireInit",
                StationState.RESETING => "MotionBlocked_Resetting",
                StationState.RUNNING => "MotionBlocked_Running",
                StationState.PAUSE => "MotionBlocked_Paused",
                StationState.STOP => "MotionBlocked_Stopped",
                StationState.ALARM => "MotionBlocked_Alarm",
                StationState.CLEAR => "MotionBlocked_Clearing",
                StationState.TIP => "MotionBlocked_TipAlarm",
                _ => "MotionBlocked_NotReady"
            };
            return _localization.GetResourceOrDefault(resourceKey,
                "Manual motion is not allowed in the current machine state.");
        }

        /// <inheritdoc />
        public void EnsureManualMotionAllowed()
        {
            if (!CanExecuteManualMotion)
                throw new MotionInterlockException(GetBlockedMessage());
        }
    }
}

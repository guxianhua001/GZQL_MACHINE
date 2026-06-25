using System;
using System.Threading;
using System.Threading.Tasks;
using Core.Utilities;
using MotionControl.Card;
using MotionControl.Events;
using MotionControl.Exceptions;
using MotionControl.Interfaces;
using MotionControl.Models;
using Prism.Events;

namespace MotionControl.Services
{
    public class GripperService : IGripperService
    {
        private readonly ILoggerService _logger;
        private readonly IEventAggregator _eventAggregator;
        private readonly GripperState _state = new GripperState();
        private Timer _monitorTimer;
        private bool _isMonitoring;

        // EtherCAT PDO 通讯参数
        private const ushort CardNo = 0;
        private const ushort NodeId = 2;

        public bool IsMoving => _state.Status == GripperStatus.Moving ||
                               _state.Status == GripperStatus.Clamping ||
                               _state.Status == GripperStatus.Releasing;
        public bool IsInitialized { get; private set; }

        /// <summary>电爪手动操作速度（1-100%），面板与快捷夹紧/释放按钮共享</summary>
        public double ManualOperationSpeed { get; set; } = 30;

        public GripperService(
            ILoggerService logger,
            IEventAggregator eventAggregator)
        {
            _logger = logger;
            _eventAggregator = eventAggregator;
        }

        public async Task InitializeAsync(CancellationToken token = default)
        {
            _logger.Info("Initializing gripper service...");

            UpdateStateFromHardware();

            IsInitialized = true;
            _logger.Info($"Gripper service initialized. Current pos: {_state.CurrentPosition}");
        }

        public void StartMonitoring(int intervalMs = 200)
        {
            if (_isMonitoring) return;
            _isMonitoring = true;
            _monitorTimer = new Timer(_ =>
            {
                try
                {
                    UpdateStateFromHardware();
                    PublishStateChange();
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Gripper monitor error: {ex.Message}");
                }
            }, null, 0, intervalMs);

            _logger.Info($"Gripper monitoring started (interval={intervalMs}ms)");
        }

        public void StopMonitoring()
        {
            _isMonitoring = false;
            _monitorTimer?.Dispose();
            _monitorTimer = null;
        }

        #region 快速操作实现

        public async Task ClampAsync(double position, CancellationToken token = default, double? speed = null)
        {
            token.ThrowIfCancellationRequested();
            ValidateInitialized();

            var effectiveSpeed = speed ?? ManualOperationSpeed;
            _logger.Info($"Clamping to position: {position}, speed: {effectiveSpeed}");
            _state.Status = GripperStatus.Clamping;
            _state.TargetPosition = position;
            PublishStateChange();

            try
            {
                LTDMC.nmc_write_rxpdo_extra_uint(CardNo, NodeId, 4, 1, (uint)effectiveSpeed); // 设置速度
                LTDMC.nmc_write_rxpdo_extra_uint(CardNo, NodeId, 3, 1, (uint)position); // 设置夹紧位置

                _state.Status = GripperStatus.Clamped;
                _state.CurrentPosition = position;
                _logger.Info($"Clamp completed, position: {position}, speed: {effectiveSpeed}");
            }
            catch (Exception ex)
            {
                _state.Status = GripperStatus.Error;
                _state.ErrorMessage = ex.Message;
                _logger.Error($"Clamp failed: {ex.Message}");
                throw new RecoverableException(
                    $"夹紧失败: {ex.Message}",
                    "请检查夹爪或物料是否卡住");
            }
            finally
            {
                PublishStateChange();
            }
        }

        public async Task ReleaseAsync(double position, CancellationToken token = default, double? speed = null)
        {
            token.ThrowIfCancellationRequested();
            ValidateInitialized();

            var effectiveSpeed = speed ?? ManualOperationSpeed;
            _logger.Info($"Releasing to position: {position}, speed: {effectiveSpeed}");
            _state.Status = GripperStatus.Releasing;
            _state.TargetPosition = position;
            PublishStateChange();

            try
            {
                LTDMC.nmc_write_rxpdo_extra_uint(CardNo, NodeId, 4, 1, (uint)effectiveSpeed); // 设置速度
                LTDMC.nmc_write_rxpdo_extra_uint(CardNo, NodeId, 3, 1, (uint)position); // 设置松开位置

                _state.Status = GripperStatus.Idle;
                _state.CurrentPosition = position;
                _logger.Info($"Release completed, position: {position}, speed: {effectiveSpeed}");
            }
            catch (Exception ex)
            {
                _state.Status = GripperStatus.Error;
                _state.ErrorMessage = ex.Message;
                _logger.Error($"Release failed: {ex.Message}");
                throw new RecoverableException(
                    $"释放失败: {ex.Message}",
                    "请检查夹爪机械结构");
            }
            finally
            {
                PublishStateChange();
            }
        }

        #endregion

        #region 运动控制实现

        public async Task MoveToPositionAsync(double position, double speed, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            ValidateInitialized();

            _logger.Info($"Moving to position: {position}, speed: {speed}");
            _state.Status = GripperStatus.Moving;
            _state.TargetPosition = position;
            PublishStateChange();

            try
            {
                LTDMC.nmc_write_rxpdo_extra_uint(CardNo, NodeId, 4, 1, (uint)speed); // 设置速度
                LTDMC.nmc_write_rxpdo_extra_uint(CardNo, NodeId, 3, 1, (uint)position); // 设置目标位置

                _state.CurrentPosition = position;
                _state.Status = GripperStatus.Idle;
                _logger.Info($"Move completed, position: {position}");
            }
            catch (Exception ex)
            {
                _state.Status = GripperStatus.Error;
                _state.ErrorMessage = ex.Message;
                _logger.Error($"Move failed: {ex.Message}");
                throw;
            }
            finally
            {
                PublishStateChange();
            }
        }

        public async Task JogLeftAsync(double step, double speed, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            ValidateInitialized();

            try
            {
                uint actPos = 0;
                LTDMC.nmc_read_txpdo_extra_uint(CardNo, NodeId, 2, 1, ref actPos); // 获取实时位置
                uint tarPos = (uint)(actPos - step);
                LTDMC.nmc_write_rxpdo_extra_uint(CardNo, NodeId, 4, 1, (uint)speed); // 设置速度
                LTDMC.nmc_write_rxpdo_extra_uint(CardNo, NodeId, 3, 1, tarPos); // 设置目标位置
                _logger.Info($"JogLeft: actPos={actPos}, tarPos={tarPos}, speed={speed}");
            }
            catch (Exception ex)
            {
                _logger.Error($"JogLeft failed: {ex.Message}");
                throw;
            }
        }

        public async Task JogRightAsync(double step, double speed, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            ValidateInitialized();

            try
            {
                uint actPos = 0;
                LTDMC.nmc_read_txpdo_extra_uint(CardNo, NodeId, 2, 1, ref actPos); // 获取实时位置
                uint tarPos = (uint)(actPos + step);
                LTDMC.nmc_write_rxpdo_extra_uint(CardNo, NodeId, 4, 1, (uint)speed); // 设置速度
                LTDMC.nmc_write_rxpdo_extra_uint(CardNo, NodeId, 3, 1, tarPos); // 设置目标位置
                _logger.Info($"JogRight: actPos={actPos}, tarPos={tarPos}, speed={speed}");
            }
            catch (Exception ex)
            {
                _logger.Error($"JogRight failed: {ex.Message}");
                throw;
            }
        }

        public void Stop()
        {
            LTDMC.nmc_write_rxpdo_extra_uint(CardNo, NodeId, 3, 1, 1); // 停止电爪
            _state.Status = GripperStatus.Idle;
            PublishStateChange();
            _logger.Info("Gripper stopped by user");
        }

        #endregion

        #region 力矩控制

        public void SetTorque(double percentage)
        {
            if (percentage < 0 || percentage > 100)
                throw new ArgumentOutOfRangeException(nameof(percentage), "力矩必须在 0-100% 之间");

            LTDMC.nmc_write_rxpdo_extra_uint(CardNo, NodeId, 1, 1, (uint)percentage); // 力矩设置1-100%
            _state.TargetTorque = percentage;
            _logger.Info($"Gripper torque set to {percentage}%");
        }

        #endregion

        #region 系统操作

        public async Task HomeAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            ValidateInitialized();

            _state.Status = GripperStatus.Homing;
            PublishStateChange();

            try
            {
                LTDMC.nmc_write_rxpdo_extra_uint(CardNo, NodeId, 0, 1, 165); // 触发回零
                _state.IsAtHome = true;
                _state.CurrentPosition = 0;
                _state.Status = GripperStatus.Idle;
                _logger.Info("Gripper homing completed");
            }
            catch (Exception ex)
            {
                _state.Status = GripperStatus.Error;
                _state.ErrorMessage = ex.Message;
                _logger.Error($"Home failed: {ex.Message}");
                throw new RecoverableException(
                    $"回零失败: {ex.Message}",
                    "请检查夹爪状态后重试");
            }
            finally
            {
                PublishStateChange();
            }
        }

        public void ResetAlarm()
        {
            // TODO: 电夹爪暂无报警清除功能，留空
        }

        #endregion

        #region 状态查询

        public GripperState GetState() => _state;

        public double GetCurrentPosition()
        {
            uint actPos = 0;
            LTDMC.nmc_read_txpdo_extra_uint(CardNo, NodeId, 2, 1, ref actPos); // 获取实时位置
            _state.CurrentPosition = actPos;
            return _state.CurrentPosition;
        }

        #endregion

        #region 私有辅助方法

        private void ValidateInitialized()
        {
            if (!IsInitialized)
                throw new InvalidOperationException("Gripper service not initialized. Call InitializeAsync first.");
        }

        private void UpdateStateFromHardware()
        {
            try
            {
                uint actPos = 0;
                LTDMC.nmc_read_txpdo_extra_uint(CardNo, NodeId, 2, 1, ref actPos);
                _state.CurrentPosition = actPos;
                _state.LastUpdateTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                _logger.Debug($"Update state error: {ex.Message}");
            }
        }

        private void PublishStateChange()
        {
            try
            {
                var evt = _eventAggregator.GetEvent<GripperStateChangedEvent>();
                evt.Publish(_state);
            }
            catch (Exception ex)
            {
                _logger.Debug($"Publish state error: {ex.Message}");
            }
        }

        #endregion
    }
}

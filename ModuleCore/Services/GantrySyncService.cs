
using SmarterMotion;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Interfaces.SharedInterfaces;
using SmarterMotion.Framework.Plc;
using MaterialDesignThemes.Wpf;
using System.Linq;
using Core.Abstraction;

namespace ModuleCore.Services
{
    public class GantrySyncService : IGantrySyncService, IDisposable
    {
        private SystemStatus _currentStatus = SystemStatus.Ready;

        public SystemStatus GetSystemStatus()
        {
            // 实际实现中应该从硬件获取状态
            return _currentStatus;
        }
        public int SystemId { get; set; }  // 实现接口属性
        // 轴号配置
        private readonly IControlSystemConfig Config;
        public ushort UPPER_X_AXIS => Config.UpperXAxis;
        public ushort UPPER_Y_AXIS => Config.UpperYAxis;
        public ushort LOWER_X_AXIS => Config.LowerXAxis;
        public ushort LOWER_Y_AXIS => Config.LowerYAxis;
        // 基准位置记录
        private PointF _basePositionUpper; // 上龙门基准位置
        private PointF _basePositionLower; // 下龙门基准位置
        // 同步方向系数（根据机械结构设置）
        public const float DIRECTION_FACTOR_X = 1;   // X轴同向
        public const float DIRECTION_FACTOR_Y = 1;  // Y轴反向
        // 运动完成事件处理字典
        private readonly Dictionary<ushort, ManualResetEvent> _axisMovementEvents;
        private const double STATUS_POLL_INTERVAL = 20; // ms - 每20ms轮询一次状态
        private const int MAX_STATUS_CHECKS = 3000;     // 最大轮询次数 (1分钟超时)
        private const int _cardNo = 0; // 卡号
        public double Acceleration { get; set; } = 0.1; 
        public bool IsSynchronizing => _syncEnabled;

        public PointF BasePositionUpper => _basePositionUpper;

        public PointF BasePositionLower => _basePositionLower;

        public event ServiceStatusChangedHandler StatusChanged;
        public event PositionUpdatedHandler PositionUpdated;

        // 内部状态
        private bool _syncEnabled;
        private bool _isInitialized;
        private Timer _updateTimer;
        private readonly object _updateLock = new object();
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private bool _isUpdating = false;
        private const int MIN_UPDATE_INTERVAL = 40; // 最小更新间隔(毫秒)
        // 配置服务
        private string _basePositionFilePath(string systemId) =>
           $"GantrySystem_{systemId}.json";

        // 获取位置
        public PointF GetUpperGantryPosition()
        {
            // 实际获取上龙门位置的逻辑
            return new PointF(ReadAxisPosition("UpperX"), ReadAxisPosition("UpperY"));
        }

        public PointF GetLowerGantryPosition()
        {
            // 实际获取下龙门位置的逻辑
            return new PointF(ReadAxisPosition("LowerX"), ReadAxisPosition("LowerY"));
        }

        private float ReadAxisPosition(string axisName)
        {
            // 获取相应的轴配置ID
            ushort axisId;

            switch (axisName)
            {
                case "UpperX":
                    axisId = UPPER_X_AXIS;
                    break;
                case "UpperY":
                    axisId = UPPER_Y_AXIS;
                    break;
                case "LowerX":
                    axisId = LOWER_X_AXIS;
                    break;
                case "LowerY":
                    axisId = LOWER_Y_AXIS;
                    break;
                default:
                    throw new ArgumentException($"未知的轴名称: {axisName}", nameof(axisName));
            }

            // 调用API获取位置
            double position = 0;
            short retCode = LTDMC.dmc_get_position_unit(_cardNo, axisId, ref position);

            if (retCode != 0)
            {
                StatusChanged?.Invoke($"错误: 轴:{axisId}位置读取失败");
            }
            return (float)position;
        }

        public GantrySyncService(int systemId, IControlSystemConfig config)
        {
            SystemId = systemId;
            Config = config;
            // 初始化字典（放在构造函数开头）
            _axisMovementEvents = new Dictionary<ushort, ManualResetEvent>
            {
                { UPPER_X_AXIS, new ManualResetEvent(true) },
                { UPPER_Y_AXIS, new ManualResetEvent(true) },
                { LOWER_X_AXIS, new ManualResetEvent(true) },
                { LOWER_Y_AXIS, new ManualResetEvent(true) }
            };
            InitializeHardware();
            SetupTimer();
            LoadBasePositions();
        }

        private void InitializeHardware()
        {
            // 初始化硬件连接
            _isInitialized = true;
            StatusChanged?.Invoke("硬件连接成功");
        }

        private void SetupTimer()
        {
            _updateTimer = new Timer(UpdatePositions, null, 0, 50); // 20Hz更新
        }

        private void UpdatePositions(object stateInfo)
        {
            // 防止重入
            if (_isUpdating) return;

            // 检查是否达到最小更新间隔
            double elapsedMs = (DateTime.Now - _lastUpdateTime).TotalMilliseconds;
            if (elapsedMs < MIN_UPDATE_INTERVAL) return;

            // 安全锁防止多线程问题
            lock (_updateLock)
            {
                try
                {
                    _isUpdating = true;

                    if (!_isInitialized) return;

                    // 读取实时位置
                    var gantryState = new GantryState
                    {
                        UpperPosition = new PointF(GetAxisPosition(UPPER_X_AXIS), GetAxisPosition(UPPER_Y_AXIS)),
                        LowerPosition = new PointF(GetAxisPosition(LOWER_X_AXIS), GetAxisPosition(LOWER_Y_AXIS))
                    };

                    // 计算同步误差
                    CalculateSyncError(ref gantryState);

                    // 触发更新事件
                    PositionUpdated?.Invoke(gantryState);

                    // 记录最后更新时间
                    _lastUpdateTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke($"更新位置时出错: {ex.Message}");
                }
                finally
                {
                    _isUpdating = false;
                }
            }
        }
        // 实现同步误差计算方法
        private void CalculateSyncError(ref GantryState state)
        {
            if (_basePositionUpper == PointF.Empty || _basePositionLower == PointF.Empty)
            {
                state.SyncError = 0;
                state.UpperSyncError = 0;
                state.LowerSyncError = 0;
                return;
            }
            // 计算上龙门相对于基准的偏移
            float upperOffsetX = state.UpperPosition.X - _basePositionUpper.X;
            float upperOffsetY = state.UpperPosition.Y - _basePositionUpper.Y;
            state.UpperSyncError = (float)Math.Sqrt(upperOffsetX * upperOffsetX + upperOffsetY * upperOffsetY);
            // 计算下龙门相对于基准的偏移
            float lowerOffsetX = state.LowerPosition.X - _basePositionLower.X;
            float lowerOffsetY = state.LowerPosition.Y - _basePositionLower.Y;
            state.LowerSyncError = (float)Math.Sqrt(lowerOffsetX * lowerOffsetX + lowerOffsetY * lowerOffsetY);
            // 计算理论下龙门位置（基于上龙门位置）
            float expectedLowerX = _basePositionLower.X + upperOffsetX * DIRECTION_FACTOR_X;
            float expectedLowerY = _basePositionLower.Y + upperOffsetY * DIRECTION_FACTOR_Y;
            // 计算实际下龙门位置与理论位置的差异
            float diffX = state.LowerPosition.X - expectedLowerX;
            float diffY = state.LowerPosition.Y - expectedLowerY;
            state.SyncError = (float)Math.Sqrt(diffX * diffX + diffY * diffY);
        }
        // 记录对齐基准坐标（在平板与光源对齐时调用）
        public bool RecordBasePositions()
        {
            if (!_isInitialized)
            {
                StatusChanged?.Invoke("错误: 硬件未初始化");
                return false;
            }
            try
            {
                _basePositionUpper = new PointF(GetAxisPosition(UPPER_X_AXIS), GetAxisPosition(UPPER_Y_AXIS));
                _basePositionLower = new PointF(GetAxisPosition(LOWER_X_AXIS), GetAxisPosition(LOWER_Y_AXIS));
                SaveBasePositions(); // 保存到文件
                StatusChanged?.Invoke("基准位置已记录");
                return true;
            }
            catch (DllNotFoundException ex)
            {
                StatusChanged?.Invoke($"错误: {ex.Message}");
                return false;
            }
        }
        // 开启/关闭同步
        public void EnableSynchronization(bool enable)
        {
            _syncEnabled = enable;
            StatusChanged?.Invoke($"同步模式: {(enable ? "开启" : "关闭")}");
        }
        // 移动到目标位置
        public async Task MoveBothToTarget(PointF targetPosition, GantryType sourceGantry, double speed)
        {
            try
            {
                StatusChanged?.Invoke($"开始移动到目标位置: ({targetPosition.X:F2}, {targetPosition.Y:F2})");
                // 检查是否已设置基准位置
                if (_basePositionUpper == PointF.Empty || _basePositionLower == PointF.Empty)
                {
                    StatusChanged?.Invoke("错误: 请先设置基准位置");
                    return;
                }
                // 根据来源龙门计算目标位置
                PointF upperTarget;
                PointF lowerTarget;
                // 计算上龙门目标位置
                if (sourceGantry == GantryType.Upper)
                {
                    upperTarget = targetPosition;
                    // 基于基准位置计算下龙门位置
                    float offsetX = targetPosition.X - _basePositionUpper.X;
                    float offsetY = targetPosition.Y - _basePositionUpper.Y;
                    lowerTarget = new PointF(
                        _basePositionLower.X + offsetX * DIRECTION_FACTOR_X,
                        _basePositionLower.Y + offsetY * DIRECTION_FACTOR_Y
                    );
                }
                else // 下龙门
                {
                    lowerTarget = targetPosition;
                    // 基于基准位置计算上龙门位置
                    float offsetX = targetPosition.X - _basePositionLower.X;
                    float offsetY = targetPosition.Y - _basePositionLower.Y;
                    // 注意Y方向的相反关系
                    upperTarget = new PointF(
                        _basePositionUpper.X + offsetX * DIRECTION_FACTOR_X,
                        _basePositionUpper.Y + offsetY * -DIRECTION_FACTOR_Y
                    );
                }
                StatusChanged?.Invoke($"上龙门目标: ({upperTarget.X:F2}, {upperTarget.Y:F2})");
                StatusChanged?.Invoke($"下龙门目标: ({lowerTarget.X:F2}, {lowerTarget.Y:F2})");
                // 设置速度
                SetAxisSpeed(UPPER_X_AXIS, speed);
                SetAxisSpeed(UPPER_Y_AXIS, speed);
                SetAxisSpeed(LOWER_X_AXIS, speed);
                SetAxisSpeed(LOWER_Y_AXIS, speed);
                // 移动双龙门到目标位置
                await Task.WhenAll(
                    MoveUpperToAsync(0, upperTarget, speed),
                    MoveLowerToAsync(1, lowerTarget, speed)
                );
                StatusChanged?.Invoke("目标位置移动完成");
            }
            catch (Exception ex)
            {
                StopAllMotion();
                StatusChanged?.Invoke($"移动错误: {ex.Message}");
            }
        }
        // 移动选择目标位置
        public async Task MoveToTarget(PointF upperTarget, PointF lowerTarget, double speed)
        {
            try
            {
                StatusChanged?.Invoke($"上龙门目标: ({upperTarget.X:F2}, {upperTarget.Y:F2})");
                StatusChanged?.Invoke($"下龙门目标: ({lowerTarget.X:F2}, {lowerTarget.Y:F2})");
                // 设置速度
                SetAxisSpeed(UPPER_X_AXIS, speed);
                SetAxisSpeed(UPPER_Y_AXIS, speed);
                SetAxisSpeed(LOWER_X_AXIS, speed);
                SetAxisSpeed(LOWER_Y_AXIS, speed);
                // 移动双龙门到目标位置
                await Task.WhenAll(
                    MoveUpperToAsync(0, upperTarget, speed),
                    MoveLowerToAsync(1, lowerTarget, speed)
                );
                StatusChanged?.Invoke("目标位置移动完成");
            }
            catch (Exception ex)
            {
                StopAllMotion();
                StatusChanged?.Invoke($"移动错误: {ex.Message}");
            }
        }
        // 设置单个轴的速度
        private void SetAxisSpeed(ushort axis, double speed)
        {
            try
            {
                double accel = speed * Acceleration;
                double decel = speed * Acceleration;
                LTDMC.dmc_set_profile_unit(_cardNo, axis, 0, speed, accel, decel, 0.1);
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"设置轴{axis}速度时出错: {ex.Message}");
            }
        }
        // 获取单轴位置
        private float GetAxisPosition(ushort axis)
        {
            double pos = 0;
            var ret = LTDMC.dmc_get_position_unit(0, axis, ref pos);
            if (ret != 0)
                throw new ApplicationException($"轴 {axis} 获取位置失败");

            return (float)pos;
        }
        // 获取两个轴的位置
        private PointF? GetAxisPosition(ushort xAxis, ushort yAxis)
        {
            double x = 0, y = 0;
            if (GetAxisPosition_Unit(xAxis, ref x) &&
                GetAxisPosition_Unit(yAxis, ref y))
            {
                return new PointF((float)x, (float)y);
            }
            return null;
        }
        // 内部方法：获取单轴位置
        private bool GetAxisPosition_Unit(ushort axis, ref double position)
        {
            try
            {
                short result = LTDMC.dmc_get_position_unit(_cardNo, axis, ref position);
                return result == 0;
            }
            catch
            {
                position = 0;
                return false;
            }
        }
        // 移动上龙门到指定位置
        public Task MoveUpperToAsync(int coordId, PointF position, double speed)
        {
            return Task.Run(() =>
            {
                // 准备插补运动参数
                int[] axes = { UPPER_X_AXIS, UPPER_Y_AXIS };
                double[] positions = { position.X, position.Y };

                // 启动插补运动
                MoveLineAbs(_cardNo, coordId, axes, positions, speed);

                // 等待插补运动完成
                WaitForInterpolationSync(_cardNo, coordId);
            });
        }
        // 移动下龙门到指定位置
        public Task MoveLowerToAsync(int coordId, PointF position, double speed)
        {
            return Task.Run(() =>
            {
                // 准备插补运动参数
                int[] axes = { LOWER_X_AXIS, LOWER_Y_AXIS };
                double[] positions = { position.X, position.Y };

                // 启动插补运动
                MoveLineAbs(_cardNo, coordId, axes, positions, speed);

                // 等待插补运动完成
                WaitForInterpolationSync(_cardNo, coordId);
            });
        }
        // 绝对位置移动
        private void MoveAxisAbs(ushort axis, float position)
        {
            // 开始移动
            _axisMovementEvents[axis].Reset();
            short result = LTDMC.dmc_pmove_unit(_cardNo, axis, position, 1); // 1: 绝对运动

            if (result != 0)
            {
                _axisMovementEvents[axis].Set();
                throw new ApplicationException($"轴 {axis} 移动到位置 {position} 失败 (错误码: {result})");
            }
        }
        private void MoveLineAbs(ushort cardNo, int coordId, int[] axisIds, double[] positions, double velocity)
        {
            // 参数校验
            if (axisIds == null || positions == null || axisIds.Length == 0 || axisIds.Length != positions.Length)
            {
                throw new ArgumentException("轴ID数组与位置数组长度必须相同且不为空");
            }

            // 设置坐标系参数（速度、加速度等）
            double acceleration = velocity * Acceleration;
            double deceleration = velocity * Acceleration;

            // 设置插补参数
            //LTDMC.dmc_set_vector_profile_unit(
            //    cardNo,
            //    (ushort)coordId,
            //    velocity,
            //    acceleration,
            //    deceleration
            //);

            // 准备轴列表和目标位置
            ushort[] axisList = axisIds.Select(id => (ushort)id).ToArray();

            // 执行直线插补运动
            short ret = LTDMC.dmc_line_unit(
                cardNo,
                (ushort)coordId,
                (ushort)axisList.Length,
                axisList,
                positions,
                1 // 1表示绝对位置模式
            );

            if (ret != 0)
            {
                throw new ApplicationException($"插补运动启动失败 (坐标系: {coordId}, 错误码: {ret})");
            }
        }

        // 插补运动等待方法 (同步版)
        private void WaitForInterpolationSync(ushort cardNo, int coordId, int timeoutMs = 300000) // 默认5分钟超时
        {
            int startTime = Environment.TickCount;

            while (true)
            {
                // 0 = 运动中, 1 = 运动完成
                int moveStatus = LTDMC.dmc_check_done_multicoor(cardNo, (ushort)coordId);

                if (moveStatus == 1) // 运动完成
                {
                    // 检查所有轴运动完成状态
                    bool allAxesDone = true;
                    foreach (var axis in new List<ushort> { UPPER_X_AXIS, UPPER_Y_AXIS, LOWER_X_AXIS, LOWER_Y_AXIS })
                    {
                        if (LTDMC.dmc_check_done(_cardNo, axis) == 0)
                        {
                            allAxesDone = false;
                            break;
                        }
                    }

                    if (allAxesDone)
                    {
                        PositionUpdated?.Invoke(new GantryState
                        {
                            UpperPosition = GetUpperGantryPosition(),
                            LowerPosition = GetLowerGantryPosition()
                        });
                        return;
                    }
                }

                // 检查超时
                if (Environment.TickCount - startTime > timeoutMs)
                {
                    // 超时处理 - 停止该坐标系的运动
                    short stopRet = LTDMC.dmc_stop_multicoor(cardNo, (ushort)coordId, 2); // 2 = 平滑停止
                    if (stopRet != 0)
                    {
                        LTDMC.dmc_stop_multicoor(cardNo, (ushort)coordId, 1); // 1 = 紧急停止
                    }
                    throw new TimeoutException($"插补运动 (坐标系 {coordId}) 超时: {timeoutMs / 1000}秒");
                }

                // 短暂延迟避免CPU占用过高
                Thread.Sleep(50);
            }
        }

        // 插补运动等待方法 (异步版)
        private async Task WaitForInterpolationAsync(ushort cardNo, int coordId, int timeoutMs = 300000)
        {
            int startTime = Environment.TickCount;

            while (true)
            {
                // 0 = 运动中, 1 = 运动完成
                int moveStatus = LTDMC.dmc_check_done_multicoor(cardNo, (ushort)coordId);

                if (moveStatus == 1) // 运动完成
                {
                    // 附加检查：确保所有轴都完成运动
                    bool allAxesDone = true;
                    for (int i = 0; i < 100; i++) // 连续检查100次确保完成
                    {
                        allAxesDone = true;
                        foreach (var axis in new List<ushort> { UPPER_X_AXIS, UPPER_Y_AXIS, LOWER_X_AXIS, LOWER_Y_AXIS })
                        {
                            if (LTDMC.dmc_check_done(_cardNo, axis) == 0)
                            {
                                allAxesDone = false;
                                break;
                            }
                        }

                        if (allAxesDone)
                        {
                            // 更新界面位置显示
                            PositionUpdated?.Invoke(new GantryState
                            {
                                UpperPosition = GetUpperGantryPosition(),
                                LowerPosition = GetLowerGantryPosition()
                            });
                            return;
                        }
                        await Task.Delay(5);
                    }

                    if (!allAxesDone)
                    {
                        throw new ApplicationException($"检测到运动完成信号，但存在轴尚未到位 (坐标系 {coordId})");
                    }
                }

                // 检查超时
                if (Environment.TickCount - startTime > timeoutMs)
                {
                    // 超时处理
                    short stopRet = LTDMC.dmc_stop_multicoor(cardNo, (ushort)coordId, 2); // 2 = 平滑停止
                    if (stopRet != 0)
                    {
                        LTDMC.dmc_stop_multicoor(cardNo, (ushort)coordId, 1); // 1 = 紧急停止
                    }
                    throw new TimeoutException($"插补运动 (坐标系 {coordId}) 超时: {timeoutMs / 1000}秒");
                }

                // 异步等待100ms
                await Task.Delay(100);
            }
        }
        public void MoveAxisJog(AxisType axis,int dir, float speed)
        {
            // 根据枚举选择轴
            int axisId = -1;

            switch (axis)
            {
                case AxisType.UpperX:
                    axisId = UPPER_X_AXIS;
                    break;
                case AxisType.UpperY:
                    axisId = UPPER_Y_AXIS;
                    break;
                case AxisType.LowerX:
                    axisId = LOWER_X_AXIS;
                    break;
                case AxisType.LowerY:
                    axisId = LOWER_Y_AXIS;
                    break;
            }
            IAxis targetAxis = XDevice.Instance.FindAxisById(axisId);

            if (targetAxis == null)
            {
                //OnStatusChanged("轴定位失败");
                return;
            }

            try
            {
                // 设置速度
                targetAxis.SetAxisJogVel(Math.Abs(speed));

                targetAxis.MoveJog(dir);
            }
            catch (Exception ex)
            {
                //OnStatusChanged($"轴运动错误: {ex.Message}");
            }
        }

        // 安全停止所有运动
        public void StopAllMotion()
        {
            EnableSynchronization(false);
            StopAllAxes();
        }
        // 停止所有轴
        public void StopAllAxes()
        {
            var axes = new[] { UPPER_X_AXIS, UPPER_Y_AXIS, LOWER_X_AXIS, LOWER_Y_AXIS };
            foreach (var axis in axes)
            {
                try
                {
                    // 1 表示紧急停止
                    LTDMC.dmc_stop(_cardNo, axis, 1);
                }
                catch
                {
                    // 忽略单轴停止错误
                }
            }
        }
        // 重置系统（需安全位置初始化）
        public void ResetSystem(PointF safePosition)
        {
            try
            {
                StopAllMotion();
                MoveBothToTarget(safePosition, GantryType.Upper, 30).Wait();
                RecordBasePositions();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("系统重置失败", ex);
            }
        }
        private void ShowErrorMessage(string title, string message)
        {
            var result = Framework.Services.DialogService.ShowBlockingDialog(
                          title: title,
                          message: message + "\r\n",
                          yesButtonText: "确定",
                          noButtonText: "",
                          extraButtonText: "",
                          showExtraButton: false,
                          showYesButton: true,
                          showNoButton: false,
                          icon: PackIconKind.ClockAlert
                        );
        }

        private void ShowErrorMessage(string title, Exception ex)
        {
            ShowErrorMessage(title, $"错误: {ex.Message}");
        }
        // 辅助类用于序列化
        private class PositionData
        {
            public float UpperX { get; set; }
            public float UpperY { get; set; }
            public float LowerX { get; set; }
            public float LowerY { get; set; }
        }
        public void LoadBasePositions()
        {
            try
            {
                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Config", _basePositionFilePath(Config.SystemId.ToString()));
                if (File.Exists(configDir))
                {
                    string json = File.ReadAllText(configDir);
                    var data = JsonSerializer.Deserialize<PositionData>(json);

                    _basePositionUpper = new PointF(data.UpperX, data.UpperY);
                    _basePositionLower = new PointF(data.LowerX, data.LowerY);
                    StatusChanged?.Invoke("基准位置已加载");
                }
                else
                {
                    StatusChanged?.Invoke("未找到保存的基准位置");
                }
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"加载基准位置失败: {ex.Message}");
            }
        }
        // 实现持久化方法
        public void SaveBasePositions()
        {
            try
            {
                var positions = new
                {
                    UpperX = _basePositionUpper.X,
                    UpperY = _basePositionUpper.Y,
                    LowerX = _basePositionLower.X,
                    LowerY = _basePositionLower.Y
                };

                string json = JsonSerializer.Serialize(positions);
                string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", _basePositionFilePath(Config.SystemId.ToString()));
                File.WriteAllText(configDir, json);
                StatusChanged?.Invoke("基准位置已保存");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"保存基准位置失败: {ex.Message}");
            }
        }
        public void Dispose()
        {
            _updateTimer?.Dispose();
            StopAllMotion();

            // 释放所有ManualResetEvent
            foreach (var resetEvent in _axisMovementEvents.Values)
            {
                resetEvent.Dispose();
            }
        }

        public void Jog(GantryType gantry, JogDirection? direction, double speed, bool synchronize)
        {
            // 转换为实际速度 (mm/s)
            float actualSpeed = (float)(speed * 0.1); // 10%的speed对应1mm/s

            // 根据方向和龙门类型决定操作的轴
            switch (direction)
            {
                case JogDirection.Left:
                    // 操作X轴负方向
                    MoveAxisJog(AxisType.UpperX, 0, actualSpeed);
                    if (synchronize && gantry == GantryType.Upper)
                    {
                        MoveAxisJog(AxisType.LowerX, 0, actualSpeed);
                    }
                    else if (synchronize && gantry == GantryType.Lower)
                    {
                        MoveAxisJog(AxisType.UpperX, 0, actualSpeed);
                    }
                    break;

                case JogDirection.Right:
                    // 操作X轴正方向
                    MoveAxisJog(AxisType.UpperX, 1, actualSpeed);
                    if (synchronize && gantry == GantryType.Upper)
                    {
                        MoveAxisJog(AxisType.LowerX, 1, actualSpeed);
                    }
                    else if (synchronize && gantry == GantryType.Lower)
                    {
                        MoveAxisJog(AxisType.UpperX, 1, actualSpeed);
                    }
                    break;

                case JogDirection.Up:
                    // 操作Y轴正方向
                    if (gantry == GantryType.Upper)
                    {
                        MoveAxisJog(AxisType.UpperY, 1, actualSpeed);
                        if (synchronize)
                        {
                            MoveAxisJog(AxisType.LowerY, 1, actualSpeed);
                        }
                    }
                    else
                    {
                        MoveAxisJog(AxisType.LowerY, 1, actualSpeed);
                        if (synchronize)
                        {
                           MoveAxisJog(AxisType.UpperY, 1, actualSpeed);
                        }
                    }
                    break;

                case JogDirection.Down:
                    // 操作Y轴负方向
                    if (gantry == GantryType.Upper)
                    {
                        MoveAxisJog(AxisType.UpperY, 0, actualSpeed);
                        if (synchronize)
                        {
                            MoveAxisJog(AxisType.LowerY, 0, actualSpeed);
                        }
                    }
                    else
                    {
                        MoveAxisJog(AxisType.LowerY, 0, actualSpeed);
                        if (synchronize)
                        {
                            MoveAxisJog(AxisType.UpperY, 0, actualSpeed);
                        }
                    }
                    break;
            }
        }

        public void StopJog()
        {
              StopAllMotion();
        }

    }

}

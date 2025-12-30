using Core.Abstraction;
using Framework.Services;
using MaterialDesignThemes.Wpf;
using SmarterMotion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Stations
{
    public partial class LoadingStation
    {
        private enum HomingState
        {
            Start,
            MoveYAxisHome,
            MoveUAxisHome,
            MoveRAxisHome,
            MoveYAxisInitPos,
            MoveUAxisInitPos,
            MoveRAxisInitPos,
            Finalize,
            Error
        }
        protected override void InitProcessVar()
        {
            _currentPhotoGroup = 0;
            _totalPhotoGroups = 0;
            _currentAssemblyPosition = 0;
            _currentDispensingPosition = 1;
        }

        protected override void OnErrorOccurred()
        {

        }
        protected override void ExecuteHoming()
        {
            if (this.Station.State != XStationState.RESETING)
            {
                return;
            }
            if (SetServo(PlatY.ActId, true) != 0)
            {
                ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.上料Y轴使能超时, (int)XSysAlarmId.AXIS_ALM, AlarmCategory.SYSTEM.ToString(), "【上料模组】上料Y轴使能超时");
                return;
            }
            if (SetServo(PlatU.ActId, true) != 0)
            {
                ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.装配U轴使能超时, (int)XSysAlarmId.AXIS_ALM, AlarmCategory.SYSTEM.ToString(), "【上料模组】装配U轴使能超时");
                return;
            }
            if (SetServo(PlatR.ActId, true) != 0)
            {
                ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.装配R轴使能超时, (int)XSysAlarmId.AXIS_ALM, AlarmCategory.SYSTEM.ToString(), "【上料模组】装配R轴使能超时");
                return;
            }
            Delay(3000);
            HomingState currentState = HomingState.Start;
            bool isHomingSuccessful = false;

            while (!isHomingSuccessful && currentState != HomingState.Error && this.Station.State == XStationState.RESETING)
            {
                switch (currentState)
                {
                    case HomingState.Start:
                        TaskHomeOK = false;
                        currentState = HomingState.MoveYAxisHome;
                        break;
                    case HomingState.MoveYAxisHome:
                        {
                            currentState = MoveYAxisHome() ?
                                HomingState.MoveUAxisHome :
                                HomingState.Error;
                            Goto((int)HomingState.MoveYAxisHome, "Y轴寻原点");
                            break;
                        }
                    case HomingState.MoveUAxisHome:
                        {
                            currentState = MoveUAxisHome() ?
                                HomingState.MoveRAxisHome :
                                HomingState.Error;
                            Goto((int)HomingState.MoveUAxisHome, "U轴寻原点");
                            break;
                        }
                    case HomingState.MoveRAxisHome:
                        {
                            currentState = MoveRAxisHome() ?
                                HomingState.MoveUAxisInitPos :
                                HomingState.Error;
                            Goto((int)HomingState.MoveRAxisHome, "R轴寻原点");
                            break;
                        }
                    case HomingState.MoveUAxisInitPos:
                        currentState = MoveUAxisStandbyPos() ?
                             HomingState.MoveRAxisInitPos :
                             HomingState.Error;
                        Goto((int)HomingState.MoveUAxisInitPos, "U轴回待机位");
                        break;
                    case HomingState.MoveRAxisInitPos:
                        currentState = MoveRAxisStandbyPos() ?
                            HomingState.MoveYAxisInitPos :
                            HomingState.Error;
                        Goto((int)HomingState.MoveRAxisInitPos, "R轴回待机位");
                        break;
                    case HomingState.MoveYAxisInitPos:
                        currentState = MoveYAxisStandbyPos() ?
                          HomingState.Finalize :
                          HomingState.Error;
                        Goto((int)HomingState.MoveYAxisInitPos, "Y轴回待机位");
                        break;
                    case HomingState.Error:

                        isHomingSuccessful = false;
                        break;

                    case HomingState.Finalize:
                        // 完成处理
                        isHomingSuccessful = true;
                        TaskHomeOK = true;
                        this.Station.SetState(XStationState.RESETING);
                        Goto((int)HomingState.Finalize, "上料工位回零完成");
                        break;
                }

                // 添加适当延迟避免CPU忙等待
                Thread.Sleep(10);
            }
        }

        #region 轴运动基础方法
        private bool MoveAxisToPosition(IAxis axis, string positionName, double baseVelocity)
        {
            double pos = GetPosition(axis.ActId, positionName);
            if (pos == -1)
            {
                _logger.Error($"【{Identifier}】{axis.Name}轴获取位置失败：{positionName}");
                return false;
            }

            double vel = baseVelocity * axis.MotionSpeedRatio;
            MoveAbs(axis.ActId, pos, vel);

            string logPrefix = $"【{Identifier}】{axis.Name}轴运动到{positionName}";
            if (WaitMoveDone())
            {
                _logger.Info($"{logPrefix}：{pos}，完成，速度：{vel}");
                return true;
            }
            else
            {
                _logger.Warn($"{logPrefix}：{pos}，超时，速度：{vel}");
                return false;
            }
        }
        private bool MoveMultiAxisToPosition(IAxis[] axes, string positionName, double baseVelocity)
        {
            // 参数校验
            if (axes == null || axes.Length == 0)
            {
                _logger.Error("【龙门搬运】轴集合为空");
                return false;
            }

            // 获取所有轴位置
            var positions = new List<double>();
            var axisNames = new List<string>();

            foreach (var axis in axes)
            {
                double pos = GetPosition(axis.ActId, positionName);
                if (pos == -1)
                {
                    _logger.Error($"【龙门搬运】{axis.Name}轴获取位置失败：{positionName}");
                    return false;
                }
                positions.Add(pos);
                axisNames.Add(axis.Name);
            }

            // 准备运动参数
            int[] axisIds = axes.Select(a => a.ActId).ToArray();
            double[] posArray = positions.ToArray();

            // 计算合成速度
            double vel = baseVelocity * axes[0].MotionSpeedRatio;

            MoveAbs(axisIds, posArray, vel);

            // 记录运动参数
            string posDetails = string.Join("|", axisNames.Zip(posArray, (n, p) => $"{n}:{p:F3}"));
            string logPrefix = $"【龙门搬运】{string.Join("+", axisNames)}轴运动到{positionName}";

            if (WaitMoveDone())
            {
                _logger.Info($"{logPrefix} 完成，坐标：{posDetails}，速度：{vel:F2}");
                return true;
            }
            else
            {
                _logger.Warn($"{logPrefix} 超时，坐标：{posDetails}，速度：{vel:F2}");
                return false;
            }
        }

        private bool MoveYAxisHome()
        {
            MoveHome(PlatY.ActId);
            return WaitMoveDone();
        }

        private bool MoveUAxisHome()
        {
            MoveHome(PlatU.ActId);
            return WaitMoveDone();
        }

        private bool MoveRAxisHome()
        {
            MoveHome(PlatR.ActId);
            return WaitMoveDone();
        }

        private bool MoveYAxisStandbyPos()
        {
            return MoveAxisToPosition(PlatY, "待机位", _axisConfigService.GetAxisSpeed(0, PlatY.ActId));
        }

        private bool MoveUAxisStandbyPos()
        {
            return MoveAxisToPosition(PlatU, "待机位", _axisConfigService.GetAxisSpeed(0, PlatU.ActId));
        }

        private bool MoveRAxisStandbyPos()
        {
            return MoveAxisToPosition(PlatR, "待机位", _axisConfigService.GetAxisSpeed(0, PlatR.ActId));
        }
        public async Task<bool> MoveToScanPosition()
        {
            _logger.Info("移动3D扫描位...");
            if (!MoveAxisToPosition(PlatY, "3D扫描位",_axisConfigService.GetAxisSpeed(0, PlatY.ActId)))
                return false;
           return true;
        }
        public bool MoveToPrePickPosition()
        {
            _logger.Info("移动Y轴到取料位置...");
            return  MoveAxisToPosition(PlatY, "取料位",
                _axisConfigService.GetAxisSpeed(0, PlatY.ActId));
        }
        /// <summary>
        /// 移动到上料位
        /// </summary>
        public bool MoveToLoadPosition()
        {
            _logger.Info("移动Y轴到上料位...");
            return  MoveAxisToPosition(PlatY, "上料位",
                _axisConfigService.GetAxisSpeed(0, PlatY.ActId));
        }
        /// <summary>
        /// 移动到出料位
        /// </summary>
        public bool MoveToUnloadPosition()
        {
            _logger.Info("移动Y轴到出料位...");
            return  MoveAxisToPosition(PlatY, "出料位",
                _axisConfigService.GetAxisSpeed(0, PlatY.ActId));
        }
        /// <summary>
        /// UR轴转到目标位置
        /// </summary>
        public bool MoveToAssemblyPosition(int assemblyIndex)
        {
            string positionName = $"装配位{assemblyIndex}";
            _logger.Info($"移动UR轴到{positionName}...");
            return  MoveMultiAxisToPosition(new IAxis[] { PlatU, PlatR }, positionName,
                _axisConfigService.GetAxisSpeed(0, PlatY.ActId));
        }
        /// <summary>
        /// 执行平台归零
        /// </summary>
        public bool ResetPlatform()
        {
            string positionName = $"待机位";
            _logger.Info($"移动UR轴到{positionName}...");
            return MoveMultiAxisToPosition(new IAxis[] { PlatU, PlatR }, positionName,
                _axisConfigService.GetAxisSpeed(0, PlatY.ActId));
        }
        // 辅助方法：获取轴位置
        public double GetAxisPosition(int axisID)
        {
            double _position = 0;
            LTDMC.dmc_get_position_unit(0, (ushort)axisID, ref _position);
            return _position;
        }
        #endregion

        #region 上料流程步骤实现
        // 上料流程步骤
        // 1. 初始化上料流程
        // 2. 检查物料
        // 3. 移动到3D扫描位置
        // 4. 移到1号装配等待位 等待装配信号
        // 5. 移动到1号装配位置 等待装配完成信号
        // 6. PlatY后退到装配等待位
        // 7. 等待下一工位信号 
        // 7. 移动到2号装配等待位
        // 8. 移动到2号装配信号  等待装配完成信号 共6个装配工位 循环执行直到装配完成信号为空 或者 达到最大次数
        // 9. 完成上料流程
        // 10.开始点胶流程
        // 11. 移动到1号点胶起始位 开始XY插补运动连续点胶
        // 12. 移动到2号点胶起始位 开始XY插补运动连续点胶
        // 13. 移动到3号点胶起始位 开始XY插补运动连续点胶
        // 14. 移动到4号点胶起始位 开始XY插补运动连续点胶
        // 15. 移动到5号点胶起始位 开始XY插补运动连续点胶
        // 16. 移动到6号点胶起始位 开始XY插补运动连续点胶
        // 17. 点胶完成 移动到UV工位位
        // 18. 打开UV灯 等待设定时间后关闭UV灯
        // 19. 退出上料流程
        // 20. 退出到待机位
        #endregion

        #region 完整上料流程实现

        /// <summary>
        /// 步骤0: 初始化上料流程
        /// </summary>
        private async Task InitializeLoading()
        {
            _logger.Info("【上料流程】步骤0: 初始化");

            if (!CheckAllAxesReady())
            {
                throw new InvalidOperationException("轴未就绪，无法开始上料流程");
            }

            if (!CheckVacuumSystem())
            {
                throw new InvalidOperationException("真空系统异常，无法开始上料流程");
            }

            _currentLoadingState = LoadingState.CheckMaterial;
            UpdateStepStatus("步骤0: 初始化完成", false);
        }
        /// <summary>
        /// 步骤1: 检查物料
        /// </summary>
        private async Task CheckMaterialAction()
        {
            _logger.Info("【上料流程】步骤1: 检查物料");

            bool materialExists = await CheckMaterialExists();
            if (!materialExists)
            {
                if (!await RetryMaterialCheck())
                {
                    throw new InvalidOperationException("物料检查失败，取料位置无物料");
                }
            }
            _currentLoadingState = LoadingState.MoveToPickPosition;
            UpdateStepStatus("步骤1: 物料检查完成", false);
        }
        /// <summary>
        /// 步骤2: 移动到3D扫描位置
        /// </summary>
        private async Task MoveTo3DScanPosition()
        {
            _logger.Info("【上料流程】步骤2: 移动到3D扫描位置");

            if (!MoveAxisToPosition(PlatY, "3D扫描位", _axisConfigService.GetAxisSpeed(0, PlatY.ActId)))
                throw new InvalidOperationException("Y轴移动到3D扫描位置失败");

            _currentLoadingState = LoadingState.Notify3DScanStart;
            UpdateStepStatus("步骤2: 移动到3D扫描位置完成", false);
        }
        private async Task Notify3DVisionSystemForScan()
        {
            // 通知视觉系统开始Tab拍照
            await Notify3DVisionSystemForTabScan(_currentPhotoGroup);
            _currentLoadingState = LoadingState.WaitAssemblyPhotoComplete;
        }
        /// <summary>
        /// 步骤3: 等待3D扫描完成
        /// </summary>
        private async Task Perform3DScan()
        {
            _logger.Info("【上料流程】步骤3: 执行3D扫描");

            // 触发3D扫描
            bool scanResult = await Execute3DScan();
            if (!scanResult)
            {
                throw new InvalidOperationException("3D扫描失败");
            }

            // 等待扫描结果处理
            await Task.Delay(200);

            _currentLoadingState = LoadingState.MoveToPickPosition;
            _currentAssemblyPosition = 1; // 从第一个装配位置开始
            UpdateStepStatus("步骤3: 3D扫描完成", false);
        }
        /// <summary>
        /// 步骤4: 移动到取料拍照位，通知装配站拍照
        /// </summary>
        private async Task MoveToPhotoPosition()
        {
            _logger.Info($"【上料流程】步骤4: 移动到取料拍照位，通知装配站{_currentAssemblyPosition}号取料位拍照");

            if (!MoveAxisToPosition(PlatY, "取料拍照位", _axisConfigService.GetAxisSpeed(0, PlatY.ActId)))
                throw new InvalidOperationException("Y轴移动到取料拍照位失败");

            if (!MoveAxisToPosition(PlatU, "取料拍照位", _axisConfigService.GetAxisSpeed(0, PlatU.ActId)))
                throw new InvalidOperationException("U轴调整到取料拍照角度失败");

            // 通知装配站移到对应取料位拍照
            await NotifyAssemblyStationForPickupPhoto(_currentAssemblyPosition);

            _currentLoadingState = LoadingState.WaitForTopPhotoComplete;
            UpdateStepStatus($"步骤4: 移动到取料拍照位完成，已通知装配站{_currentAssemblyPosition}号取料位拍照", true);
        }
        /// <summary>
        /// 步骤5: 等待取料拍照完成信号
        /// </summary>
        private async Task WaitForTopPhotoCompleteAction()
        {
            _logger.Info($"【上料流程】步骤5: 等待{_currentAssemblyPosition}号取料拍照完成信号");

            bool photoComplete = await WaitForAssemblyPhotoComplete(_currentAssemblyPosition);
            if (!photoComplete)
            {
                throw new InvalidOperationException($"{_currentAssemblyPosition}号取料拍照完成信号超时");
            }

            _currentLoadingState = LoadingState.MoveToPickPosition;
            UpdateStepStatus($"步骤5: {_currentAssemblyPosition}号取料拍照完成", false);
        }
        /// <summary>
        /// 步骤6: 移动到取料拍照位，通知装配站拍照
        /// </summary>
        private async Task MoveToPickPosition()
        {
            _logger.Info($"【上料流程】步骤6: 通知装配站{_currentAssemblyPosition}号取料位");

            if (!MoveAxisToPosition(PlatY, $"取料位", _axisConfigService.GetAxisSpeed(0, PlatY.ActId)))
                throw new InvalidOperationException($"Y轴移动到取料位失败");

            // 通知装配站移到对应取料位
            await NotifyAssemblyStationForPickup(_currentAssemblyPosition);

            _currentLoadingState = LoadingState.RotateToAssemblyPosition;
            UpdateStepStatus($"步骤6: 移动到取料位完成，已通知装配站{_currentAssemblyPosition}号取料位到达", false);
        }
        private async Task RotateToAssemblyPosition()
        {
            _logger.Info($"【上料流程】步骤6: 旋转到组装工位{_currentAssemblyPosition}角度");

            // 根据当前组装工位序号旋转U轴和R轴到对应角度
            if (_currentAssemblyPosition >= 1 && _currentAssemblyPosition <= 6)
            {
                _logger.Info($"【上料流程】旋转到组装工位{_currentAssemblyPosition}角度");

                // U轴旋转到对应工位角度
                if (!MoveAxisToPosition(PlatU, $"装配位{_currentAssemblyPosition}",
                    _axisConfigService.GetAxisSpeed(0, PlatU.ActId)))
                {
                    _logger.Warn($"U轴旋转到组装工位{_currentAssemblyPosition}角度失败");
                    throw new InvalidOperationException("U轴旋转到组装工位角度失败");
                }

                // R轴旋转到对应工位角度
                if (!MoveAxisToPosition(PlatR, $"装配位{_currentAssemblyPosition}",
                    _axisConfigService.GetAxisSpeed(0, PlatR.ActId)))
                {
                    _logger.Warn($"R轴旋转到组装工位{_currentAssemblyPosition}角度失败");
                    throw new InvalidOperationException("R轴旋转到组装工位角度失败");
                }
            }
            else
            {
                _logger.Error($"无效的组装工位序号: {_currentAssemblyPosition}");
                throw new InvalidOperationException("组装工位序号无效");
            }

            _currentLoadingState = LoadingState.WaitForPickupComplete;
            UpdateStepStatus($"步骤6: 移动到3D扫描位置完成（工位{_currentAssemblyPosition}）", false);
        }

        /// <summary>
        /// 步骤7: 等待装配站取料完成信号
        /// </summary>
        private async Task<bool> WaitForPickupCompleteSignal()
        {
            _logger.Info($"【上料流程】步骤7: 等待{_currentAssemblyPosition}号取料完成信号");

            // 等待取料完成信号
            bool signalReceived = StationEvents.WaitForSignal(
                    StationEvents.AssemblyPickupCompleted,
                    -1
                 );
            if (!signalReceived)
            {
                _logger.Warn($"未收到{_currentAssemblyPosition}号装配站取料完成信号");
                throw new InvalidOperationException($"未收到{_currentAssemblyPosition}号装配站取料完成信号");
            }

            _currentLoadingState = LoadingState.Notify3DScanStart;
            UpdateStepStatus($"步骤7: {_currentAssemblyPosition}号取料完成，准备进入3D扫描流程", false);
            return true;
        }

        /// <summary>
        /// 步骤8: 移动到Tab拍照位
        /// </summary>
        private async Task MoveToTabPhotoPosition()
        {
            _logger.Info($"【上料流程】步骤8: 移动到Tab{_currentPhotoGroup}拍照位");

            if (!MoveAxisToPosition(PlatY, $"tab{_currentPhotoGroup}", _axisConfigService.GetAxisSpeed(0, PlatY.ActId)))
                throw new InvalidOperationException($"Y轴移动到Tab{_currentPhotoGroup}拍照位失败");

            // 通知视觉系统开始Tab拍照
            await Notify3DVisionSystemForTabScan(_currentPhotoGroup);

            _currentLoadingState = LoadingState.WaitTabPhotoComplete;
            UpdateStepStatus($"步骤8: 移动到Tab{_currentPhotoGroup}拍照位完成", true);
        }

        /// <summary>
        /// 步骤9: 等待Tab拍照完成信号
        /// </summary>
        private async Task WaitTabPhotoComplete()
        {
            _logger.Info($"【上料流程】步骤9: 等待Tab{_currentPhotoGroup}拍照完成信号");

            bool photoComplete = await WaitForTabPhotoComplete(_currentPhotoGroup);
            if (!photoComplete)
            {
                throw new InvalidOperationException($"Tab{_currentPhotoGroup}拍照完成信号超时");
            }

            _currentLoadingState = LoadingState.MoveToPillar1Photo;
            UpdateStepStatus($"步骤9: Tab{_currentPhotoGroup}拍照完成", false);
        }

        /// <summary>
        /// 步骤10: 移动到Pillar1拍照位
        /// </summary>
        private async Task MoveToPillar1PhotoPosition()
        {
            _logger.Info($"【上料流程】步骤10: 移动到Pillar1_{_currentPhotoGroup}拍照位");

            if (!MoveAxisToPosition(PlatY, $"pillar1_{_currentPhotoGroup}", _axisConfigService.GetAxisSpeed(0, PlatY.ActId)))
                throw new InvalidOperationException($"Y轴移动到Pillar1_{_currentPhotoGroup}拍照位失败");

            // 通知视觉系统开始Pillar1拍照
            await NotifyVisionSystemForPillar1Photo(_currentPhotoGroup);

            _currentLoadingState = LoadingState.WaitPillar1PhotoComplete;
            UpdateStepStatus($"步骤10: 移动到Pillar1_{_currentPhotoGroup}拍照位完成", true);
        }

        /// <summary>
        /// 步骤11: 等待Pillar1拍照完成信号
        /// </summary>
        private async Task WaitPillar1PhotoComplete()
        {
            _logger.Info($"【上料流程】步骤11: 等待Pillar1_{_currentPhotoGroup}拍照完成信号");

            bool photoComplete = await WaitForPillar1PhotoComplete(_currentPhotoGroup);
            if (!photoComplete)
            {
                throw new InvalidOperationException($"Pillar1_{_currentPhotoGroup}拍照完成信号超时");
            }

            _currentLoadingState = LoadingState.MoveToPillar2Photo;
            UpdateStepStatus($"步骤11: Pillar1_{_currentPhotoGroup}拍照完成", false);
        }

        /// <summary>
        /// 步骤12: 移动到Pillar2拍照位
        /// </summary>
        private async Task MoveToPillar2PhotoPosition()
        {
            _logger.Info($"【上料流程】步骤12: 移动到Pillar2_{_currentPhotoGroup}拍照位");

            if (!MoveAxisToPosition(PlatY, $"pillar2_{_currentPhotoGroup}", _axisConfigService.GetAxisSpeed(0, PlatY.ActId)))
                throw new InvalidOperationException($"Y轴移动到Pillar2_{_currentPhotoGroup}拍照位失败");

            // 通知视觉系统开始Pillar2拍照
            await NotifyVisionSystemForPillar2Photo(_currentPhotoGroup);

            _currentLoadingState = LoadingState.WaitAssemblyPhotoComplete;
            UpdateStepStatus($"步骤12: 移动到Pillar2_{_currentPhotoGroup}拍照位完成", true);
        }

        /// <summary>
        /// 步骤13: 等待Pillar2拍照完成信号
        /// </summary>
        private async Task WaitPillar2PhotoComplete()
        {
            _logger.Info($"【上料流程】步骤13: 等待Pillar2_{_currentPhotoGroup}拍照完成信号");

            bool photoComplete = await WaitForPillar2PhotoComplete(_currentPhotoGroup);
            if (!photoComplete)
            {
                throw new InvalidOperationException($"Pillar2_{_currentPhotoGroup}拍照完成信号超时");
            }

            // 检查是否需要继续下一组拍照
            if (_currentPhotoGroup < _totalPhotoGroups)
            {
                _currentPhotoGroup++;
                _currentLoadingState = LoadingState.PostAssembly; 
                UpdateStepStatus($"步骤13: Pillar2_{_currentPhotoGroup - 1}拍照完成，开始第{_currentPhotoGroup}组拍照", false);
            }
            else
            {
                // 所有拍照完成，进入装配流程
                _currentPhotoGroup = 1; // 重置为第一组
                _currentLoadingState = LoadingState.PostAssembly;
                UpdateStepStatus("步骤13: 所有拍照完成，开始装配流程", false);
            }
        }
        private async Task RotateToNextAssemblyPosition()
        {

        }
        private async Task WaitAssemblyComplete()
        {

        }
        private async Task PostAssembly()
        {

        }

        /// <summary>
        /// 步骤14: 移到装配等待位置（应用偏移值）
        /// </summary>
        private async Task MoveToAssemblyWaitPosition()
        {
            _logger.Info($"【上料流程】步骤7: 移到{_currentAssemblyPosition}号装配等待位（应用偏移值）");

            // 获取基础位置
            double basePos = GetPosition(PlatY.ActId, $"装配等待位");
            if (basePos == -1)
            {
                throw new InvalidOperationException($"获取{_currentAssemblyPosition}号装配等待位失败");
            }

            // 应用偏移值
            //double targetPos = basePos + _currentOffsetY;

            // 移动到目标位置
            double vel = _axisConfigService.GetAxisSpeed(0, PlatY.ActId) * PlatY.MotionSpeedRatio;
            MoveAbs(PlatY.ActId, basePos, vel);

            if (!WaitMoveDone())
                throw new InvalidOperationException($"Y轴移动到{_currentAssemblyPosition}号装配等待位（应用偏移）失败");

            _currentLoadingState = LoadingState.WaitAssemblyComplete;
            UpdateStepStatus($"步骤7: 移动到{_currentAssemblyPosition}号装配等待位完成（应用偏移）", true);
        }
        /// </summary>
        /// 通知物料到达装配等待位
        /// </summary>
        private async Task NotifyMaterialArrived()
        {
            _logger.Info($"【上料流程】步骤8: 通知物料到达装配等待位");

            StationEvents.SendSignal(
                        StationEvents.MaterialReadySignal,
                        _currentAssemblyPosition,
                        $"通知物料到达装配等待位"
                    );

            UpdateStepStatus("步骤8: 已通知装配系统物料已到位", true);
        }

        /// <summary>
        /// 步骤16: 移动到装配位置
        /// </summary>
        private async Task MoveToAssemblyPosition()
        {
            _logger.Info($"【上料流程】步骤9: 移动到{_currentAssemblyPosition}号装配位置");

            if (!MoveAxisToPosition(PlatY, $"装配位{_currentAssemblyPosition}", _axisConfigService.GetAxisSpeed(0, PlatY.ActId)))
                throw new InvalidOperationException($"Y轴移动到{_currentAssemblyPosition}号装配位置失败");

            _currentLoadingState = LoadingState.WaitAssemblyComplete;
            UpdateStepStatus($"步骤9: 移动到{_currentAssemblyPosition}号装配位置完成", true);
        }

        /// <summary>
        /// 步骤15: 等待装配完成
        /// </summary>
        private async Task WaitForAssemblyComplete()
        {
            _logger.Info($"【上料流程】步骤10: 等待{_currentAssemblyPosition}号装配完成");

            bool assemblyComplete = await WaitForAssemblyCompleteSignal(_currentAssemblyPosition);
            if (!assemblyComplete)
            {
                throw new InvalidOperationException($"装配工位{_currentAssemblyPosition}完成信号超时");
            }

            _currentLoadingState = LoadingState.MoveBackFromAssembly;
            UpdateStepStatus($"步骤10: {_currentAssemblyPosition}号装配完成", false);
        }

        /// <summary>
        /// 步骤17: 后退到装配等待位
        /// </summary>
        private async Task MoveBackFromAssembly()
        {
            _logger.Info($"【上料流程】步骤11: 从{_currentAssemblyPosition}号装配位置后退");

            if (!MoveAxisToPosition(PlatY, $"装配等待位", _axisConfigService.GetAxisSpeed(0, PlatY.ActId)))
                throw new InvalidOperationException($"Y轴从{_currentAssemblyPosition}号装配位置后退失败");

            _currentLoadingState = LoadingState.CheckNextAssembly;
            UpdateStepStatus($"步骤11: 从{_currentAssemblyPosition}号装配位置后退完成", false);
        }

        /// <summary>
        /// 步骤18: 检查下一个装配位置
        /// </summary>
        private async Task CheckNextAssemblyPosition()
        {
            _logger.Info("【上料流程】步骤11: 检查下一个装配位置");

            if (_currentAssemblyPosition < 6)
            {
                _currentAssemblyPosition++;

                // 循环回到取料拍照步骤
                _currentLoadingState = LoadingState.StartDispensing;
                UpdateStepStatus($"步骤11: 切换到{_currentAssemblyPosition}号装配工位", false);
            }
            else
            {
                // 所有装配完成，Y轴退到上料位
                _logger.Info("【上料流程】所有装配完成，Y轴退回上料位");

                if (!MoveToLoadPosition())
                    throw new InvalidOperationException("Y轴退回上料位失败");

                // 开始点胶流程
                _currentLoadingState = LoadingState.StartDispensing;
                _currentDispensingPosition = 1;
                UpdateStepStatus("步骤11: 所有装配完成，开始点胶流程", false);
            }
        }

        /// <summary>
        /// 步骤19: 开始点胶流程
        /// </summary>
        private async Task StartDispensingProcess()
        {
            _logger.Info("【上料流程】步骤13: 开始点胶流程");

            // 初始化点胶设备
            bool dispensingReady = await InitializeDispensingSystem();
            if (!dispensingReady)
            {
                throw new InvalidOperationException("点胶系统初始化失败");
            }

            _currentLoadingState = LoadingState.MoveToDispensingStart;
            UpdateStepStatus("步骤13: 点胶系统就绪", false);
        }

        /// <summary>
        /// 步骤20: 移动到点胶起始位
        /// </summary>
        private async Task MoveToDispensingStartPosition()
        {
            _logger.Info($"【上料流程】步骤14: 移动到{_currentDispensingPosition}号点胶起始位");

            if (!MoveAxisToPosition(PlatY, $"点胶起始位{_currentDispensingPosition}", _axisConfigService.GetAxisSpeed(0, PlatY.ActId)))
                throw new InvalidOperationException($"Y轴移动到{_currentDispensingPosition}号点胶起始位失败");

            _currentLoadingState = LoadingState.PerformDispensing;
            UpdateStepStatus($"步骤14: 移动到{_currentDispensingPosition}号点胶起始位完成", false);
        }

        /// <summary>
        /// 步骤21: 执行点胶
        /// </summary>
        private async Task PerformDispensing()
        {
            _logger.Info($"【上料流程】步骤21: 执行{_currentDispensingPosition}号点胶");

            // 开始点胶
            bool dispensingResult = await StartDispensingOperation(_currentDispensingPosition);
            if (!dispensingResult)
            {
                throw new InvalidOperationException($"{_currentDispensingPosition}号点胶操作失败");
            }

            // 执行XY插补运动连续点胶
            bool interpolationResult = await PerformXYInterpolationDispensing(_currentDispensingPosition);
            if (!interpolationResult)
            {
                throw new InvalidOperationException($"{_currentDispensingPosition}号点胶插补运动失败");
            }

            // 等待点胶完成
            int dispensingTime = TypedParameters?.DispensingTime != null ? (int)TypedParameters.DispensingTime : 2000;
            await Task.Delay(dispensingTime);

            _currentLoadingState = LoadingState.CheckNextDispensing;
            UpdateStepStatus($"步骤21: {_currentDispensingPosition}号点胶完成", false);
        }

        /// <summary>
        /// 步骤22: 检查下一个点胶位置
        /// </summary>
        private async Task CheckNextDispensingPosition()
        {
            _logger.Info("【上料流程】步骤22: 检查下一个点胶位置");

            if (_currentDispensingPosition < 3)
            {
                _currentDispensingPosition++;
                _currentLoadingState = LoadingState.MoveToDispensingStart;
                UpdateStepStatus($"步骤16: 切换到{_currentDispensingPosition}号点胶工位", false);
            }
            else
            {
                // 所有点胶完成，移动到UV工位
                _currentLoadingState = LoadingState.MoveToUvStation;
                UpdateStepStatus("步骤22: 所有点胶完成，移动到UV工位", false);
            }
        }

        /// <summary>
        /// 步骤23: 移动到UV工位
        /// </summary>
        private async Task MoveToUvStation()
        {
            _logger.Info("【上料流程】步骤23: 移动到UV工位");

            if (!MoveAxisToPosition(PlatY, $"UV工位{_currentAssemblyPosition}", _axisConfigService.GetAxisSpeed(0, PlatY.ActId)))
                throw new InvalidOperationException("Y轴移动到UV工位失败");

            //if (!MoveAxisToPosition(PlatU, "UV固化角度", _axisConfigService.GetAxisSpeed(0, PlatU.ActId)))
            //    throw new InvalidOperationException("U轴调整到UV固化角度失败");

            _currentLoadingState = LoadingState.StartUvCuring;
            UpdateStepStatus("步骤23: 移动到UV工位完成", false);
        }

        /// <summary>
        /// 步骤24: 开始UV固化
        /// </summary>
        private async Task StartUvCuring()
        {
            _logger.Info("【上料流程】步骤24: 开始UV固化");

            // 打开UV灯
            if (!TurnOnUvLamp1())
                throw new InvalidOperationException("打开UV灯失败");

            // 等待UV固化时间
            int uvCuringTime = TypedParameters?.UvCuringTime != null ? (int)TypedParameters.UvCuringTime : 5000;
            await Task.Delay(uvCuringTime);

            _currentLoadingState = LoadingState.StopUvCuring;
            UpdateStepStatus("步骤24: UV固化进行中", true);
        }

        /// <summary>
        /// 步骤25: 停止UV固化
        /// </summary>
        private async Task StopUvCuring()
        {
            _logger.Info("【上料流程】步骤25: 停止UV固化");

            // 关闭UV灯
            if (!TurnOffUvLamp1())
                throw new InvalidOperationException("关闭UV灯失败");

            _currentLoadingState = LoadingState.MoveToStandby;
            UpdateStepStatus("步骤25: UV固化完成", false);
        }

        /// <summary>
        /// 步骤26: 移动到待机位
        /// </summary>
        private async Task MoveToStandbyPosition()
        {
            _logger.Info("【上料流程】步骤20: 移动到待机位");

            if (!MoveYAxisStandbyPos())
                throw new InvalidOperationException("Y轴移动到待机位失败");

            if (!MoveUAxisStandbyPos())
                throw new InvalidOperationException("U轴移动到待机角度失败");

            if (!MoveRAxisStandbyPos())
                throw new InvalidOperationException("R轴旋转到待机姿态失败");

            // 释放真空
            if (!ReleaseVacuum())
                throw new InvalidOperationException("释放真空失败");

            _currentLoadingState = LoadingState.Complete;
            UpdateStepStatus("步骤20: 上料流程完成", false);
        }

        #endregion

        #region 新增辅助方法

        /// <summary>
        /// 执行3D扫描
        /// </summary>
        private async Task<bool> Execute3DScan()
        {
            try
            {
                // 通知点胶系统3D扫描开始
                StationEvents.SendSignal(
                       StationEvents.Material3DScanReady,
                       -1,
                       $"准备进行3D扫描",
                       true
                   );
                // 等待扫描完成
                await Task.Delay(200); // 模拟扫描时间

                // 使用集中式事件管理器等待信号
                bool scanValid = StationEvents.WaitForSignal(
                    StationEvents.Dispensing3DScanCompleted,
                    30 * 1000, // 30秒超时
                    _loadingCTS.Token
                );

                // 检查扫描结果
                if (!scanValid)
                {
                    _logger.Error("3D扫描结果无效");
                    return false;
                }

                return scanValid;
            }
            catch (Exception ex)
            {
                _logger.Error($"3D扫描失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 等待装配就绪信号
        /// </summary>
        private async Task<bool> WaitForAssemblyReadySignal(int stationIndex)
        {
            int timeout = TypedParameters?.AssemblyReadyTimeout != null ? (int)TypedParameters.AssemblyReadyTimeout : 30000;
            int elapsed = 0;

            while (elapsed < timeout)
            {
                if (_loadingCTS.Token.IsCancellationRequested)
                    return false;

                // 检查装配工位就绪信号
                // bool ready = ReadAssemblyReadySignal(stationIndex);
                bool ready = true; // 模拟信号

                if (ready) return true;

                await Task.Delay(100);
                elapsed += 100;
            }

            return false;
        }

        /// <summary>
        /// 等待装配完成信号
        /// </summary>
        private async Task<bool> WaitForAssemblyCompleteSignal(int stationIndex)
        {
            int timeout = TypedParameters?.AssemblyReadyTimeout != null ? (int)TypedParameters.AssemblyReadyTimeout : 30000;
            int elapsed = 0;


            while (elapsed < timeout)
            {
                if (_loadingCTS.Token.IsCancellationRequested)
                    return false;

                // 检查装配完成信号
                // bool complete = ReadAssemblyCompleteSignal(stationIndex);
                bool complete = true; // 模拟信号

                if (complete) return true;

                await Task.Delay(100);
                elapsed += 100;
            }

            return false;
        }

        /// <summary>
        /// 初始化点胶系统
        /// </summary>
        private async Task<bool> InitializeDispensingSystem()
        {
            try
            {
                // 初始化点胶设备
                // bool initSuccess = DispensingSystem.Initialize();
                bool initSuccess = true; // 模拟初始化成功
                await Task.Delay(500);
                return initSuccess;
            }
            catch (Exception ex)
            {
                _logger.Error($"点胶系统初始化失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 开始点胶操作
        /// </summary>
        private async Task<bool> StartDispensingOperation(int stationIndex)
        {
            try
            {
                // 启动点胶
                // bool started = DispensingSystem.StartDispensing(stationIndex);
                bool started = true; // 模拟启动成功

                await Task.Delay(100);
                return started;
            }
            catch (Exception ex)
            {
                _logger.Error($"点胶操作失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 执行XY插补运动点胶
        /// </summary>
        private async Task<bool> PerformXYInterpolationDispensing(int stationIndex)
        {
            try
            {
                // 执行XY插补运动
                // bool interpolationSuccess = DispensingSystem.PerformInterpolation(stationIndex);
                bool interpolationSuccess = true; // 模拟插补成功

                int dispensingTime = TypedParameters?.DispensingTime != null ? (int)TypedParameters.DispensingTime : 2000;
                await Task.Delay(dispensingTime);
                return interpolationSuccess;
            }
            catch (Exception ex)
            {
                _logger.Error($"XY插补点胶失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 打开UV灯
        /// </summary>
        private bool TurnOnUvLamp1()
        {
            try
            {
                UvLamp1.SetDo(1);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"打开UV灯1失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 关闭UV灯
        /// </summary>
        private bool TurnOffUvLamp1()
        {
            try
            {
                UvLamp1.SetDo(0);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"关闭UV灯1失败: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// 打开UV灯
        /// </summary>
        private bool TurnOnUvLamp2()
        {
            try
            {
                UvLamp2.SetDo(1);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"打开UV灯2失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 关闭UV灯
        /// </summary>
        private bool TurnOffUvLamp2()
        {
            try
            {
                UvLamp2.SetDo(0);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"关闭UV灯2失败: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// 打开平台真空
        /// </summary>
        public bool TurnOnVacuum()
        {
            try
            {
                PlatVacValve.SetDo(1);
                PlatBreakVacValve.SetDo(0);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"打开平台真空失败: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// 打开平台破真空
        /// </summary>
        public bool TurnOnBreakVacuum()
        {
            try
            {
                PlatVacValve.SetDo(0);
                PlatBreakVacValve.SetDo(1);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"打开平台破真空失败: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// 关闭平台真空
        /// </summary>
        public bool TurnOffVacuum()
        {
            try
            {
                PlatVacValve.SetDo(0);
                PlatBreakVacValve.SetDo(1);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"关闭平台真空失败: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region 辅助方法实现

        /// <summary>
        /// 检查所有轴是否就绪
        /// </summary>
        private bool CheckAllAxesReady()
        {
            return PlatY.IsHomeOk && PlatU.IsHomeOk && PlatR.IsHomeOk;
        }

        /// <summary>
        /// 检查真空系统状态
        /// </summary>
        private bool CheckVacuumSystem()
        {
            // 检查真空发生器状态
            return ReadSensorWithDebounce(0, 39);
        }

        /// <summary>
        /// 检查物料是否存在
        /// </summary>
        private async Task<bool> CheckMaterialExists()
        {
            try
            {
                _logger.Info("【上料流程】开始检查物料是否存在");

                // 激活真空
                if (!ActivateVacuum())
                {
                    _logger.Error("激活真空失败，无法检测物料");
                    return false;
                }

                // 等待真空建立
                await Task.Delay(500); // 给真空系统一点时间建立

                // 使用真空传感器检测物料
                bool exists = await ReadMaterialSensor();

                if (!exists)
                {
                    _logger.Warn("物料检测失败：真空未建立或物料不存在");
                    ReportAlarm(XAlarmLevel.PAUSE,
                               (int)MachineAlarmCode.物料检测失败,
                               (int)XSysAlarmId.SENSOR_ALM,
                               AlarmCategory.SYSTEM.ToString(),
                               "【上料模组】物料检测失败：真空未建立或物料不存在");
                    var result = DialogService.ShowBlockingDialog(
                                 title: "⚠️警告",
                                 message: "【上料模组】物料检测失败：真空未建立或物料不存在" + "\r\n",
                                 yesButtonText: "重试",
                                 noButtonText: "继续",
                                 extraButtonText: "",
                                 showExtraButton: false,
                                 showYesButton: true,
                                 showNoButton: true,
                                 icon: PackIconKind.ClockAlert
                               );
                    if ((int)result == 0)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    _logger.Info("物料检测成功：真空建立，物料存在");
                }

                return exists;
            }
            catch (Exception ex)
            {
                _logger.Error($"物料检测失败: {ex.Message}");
                ReportAlarm(XAlarmLevel.STOP,
                           (int)MachineAlarmCode.物料检测异常,
                           (int)XSysAlarmId.SENSOR_ALM,
                           AlarmCategory.SYSTEM.ToString(),
                           $"【上料模组】物料检测异常: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// 读取物料传感器 - 检测真空信号持续时间（使用WaitUntil实现）
        /// </summary>
        private async Task<bool> ReadMaterialSensor()
        {
            try
            {
                _logger.Info("开始检测真空传感器信号...");

                int requiredDuration = 3000; // 需要持续3秒
                int checkInterval = 100;     // 每100ms检查一次
                int maxTotalTime = 10000;    // 总检测超时时间10秒

                int consecutiveSuccessCount = 0;
                int requiredConsecutiveChecks = requiredDuration / checkInterval;

                // 使用 WaitUntil 实现持续检测
                bool success = WaitUntil(() =>
                {
                    if (_loadingCTS?.Token.IsCancellationRequested == true)
                        return true; // 返回true让WaitUntil退出，但我们会检查取消状态

                    // 检查真空传感器信号（带滤波）
                    bool currentVacuumStatus = ReadSensorWithDebounce(0, 25);

                    if (currentVacuumStatus)
                    {
                        consecutiveSuccessCount++;
                        _logger.Debug($"真空信号正常，连续计数: {consecutiveSuccessCount}/{requiredConsecutiveChecks}");

                        // 检查是否达到要求的持续时间
                        if (consecutiveSuccessCount >= requiredConsecutiveChecks)
                        {
                            _logger.Info($"真空信号持续{requiredDuration}ms，物料检测成功");
                            return true;
                        }
                    }
                    else
                    {
                        // 信号中断，重置计数器
                        if (consecutiveSuccessCount > 0)
                        {
                            _logger.Warn($"真空信号中断，重置连续计数（之前: {consecutiveSuccessCount}）");
                            consecutiveSuccessCount = 0;
                        }
                    }

                    return false;
                }, maxTotalTime, checkInterval);

                // 检查是否因为取消而退出
                if (_loadingCTS?.Token.IsCancellationRequested == true)
                {
                    _logger.Warn("物料检测被取消");
                    return false;
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Error($"读取物料传感器异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 重试物料检查
        /// </summary>
        private async Task<bool> RetryMaterialCheck()
        {
            int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                _logger.Info($"物料检查重试 {i + 1}/{maxRetries}");

                await Task.Delay(1000); // 等待1秒后重试

                if (await CheckMaterialExists())
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检查物料对齐
        /// </summary>
        private async Task<bool> CheckMaterialAlignment()
        {
            // 使用视觉系统或传感器检查物料位置
            // 这里需要根据实际硬件实现
            await Task.Delay(100);
            return true; // 模拟对齐正常
        }

        /// <summary>
        /// 调整物料位置
        /// </summary>
        private async Task<bool> AdjustMaterialPosition()
        {
            // 通过微调轴位置来对齐物料
            // 这里需要根据实际硬件实现
            await Task.Delay(200);
            return true; // 模拟调整成功
        }

        /// <summary>
        /// 激活真空
        /// </summary>
        private bool ActivateVacuum()
        {
            try
            {
                // 打开真空电磁阀
                PlatVacValve.SetDo(1);
                PlatBreakVacValve.SetDo(0);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"激活真空失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查真空传感器
        /// </summary>
        private async Task<bool> CheckVacuumSensor()
        {
            int timeout = 5000; // 5秒超时
            int elapsed = 0;

            while (elapsed < timeout)
            {
                if (_loadingCTS.Token.IsCancellationRequested)
                    return false;

                // 检查真空传感器信号
                // bool vacuumOK = PlatVacSensor.IsOn();
                bool vacuumOK = true; // 模拟真空建立

                if (vacuumOK) return true;

                await Task.Delay(100);
                elapsed += 100;
            }

            return false;
        }

        /// <summary>
        /// 重试拾取物料
        /// </summary>
        private async Task<bool> RetryPickMaterial()
        {
            int maxRetries = 2;
            for (int i = 0; i < maxRetries; i++)
            {
                _logger.Info($"拾取物料重试 {i + 1}/{maxRetries}");

                // 先释放真空
                ReleaseVacuum();
                await Task.Delay(300);

                // 重新激活真空
                if (ActivateVacuum())
                {
                    await Task.Delay(500);//Parameters.PickDelayTime

                    if (await CheckVacuumSensor())
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 小距离提升确认
        /// </summary>
        private bool LiftSmallDistanceToConfirm()
        {
            // 小距离提升确认物料已吸附
            // 这里需要根据实际硬件实现
            return true; // 模拟成功
        }

        /// <summary>
        /// 等待装配工位就绪信号
        /// </summary>
        private async Task<bool> WaitForAssemblyReadySignal()
        {
            int timeout = 30000; // 30秒超时
            int elapsed = 0;

            while (elapsed < timeout)
            {
                if (_loadingCTS.Token.IsCancellationRequested)
                    return false;

                // 检查装配工位就绪信号
                // bool ready = ReadAssemblyReadySignal();
                bool ready = true; // 模拟信号

                if (ready) return true;

                await Task.Delay(100);
                elapsed += 100;
            }

            return false;
        }

        /// <summary>
        /// 释放真空
        /// </summary>
        private bool ReleaseVacuum()
        {
            try
            {
                // 关闭真空电磁阀，打开破真空电磁阀
                // PlatVacValve.SetOff();
                // PlatBreakVacValve.SetOn();
                // Delay(Parameters.BreakVacuumTime);
                // PlatBreakVacValve.SetOff();
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"释放真空失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查物料释放
        /// </summary>
        private async Task<bool> CheckMaterialReleased()
        {
            // 使用传感器确认物料已释放
            // 这里需要根据实际硬件实现
            await Task.Delay(100);
            return true; // 模拟释放成功
        }

        /// <summary>
        /// 通知物料已放置
        /// </summary>
        private void NotifyMaterialPlaced()
        {
            // 设置信号通知装配工位物料已放置
            // WriteMaterialPlacedSignal(true);
        }

        // 单步控制相关
        private Action<string, bool> _stepStatusCallback;

        /// <summary>
        /// 设置步骤状态回调
        /// </summary>
        public void SetStepStatusCallback(Action<string, bool> callback)
        {
            _stepStatusCallback = callback;
        }

        /// <summary>
        /// 更新步骤状态
        /// </summary>
        private void UpdateStepStatus(string description, bool isWaiting = false)
        {
            _stepStatusCallback?.Invoke(description, isWaiting);

            if (isWaiting)
            {
                _logger.Info($"单步等待: {description}");
            }
            else
            {
                _logger.Info($"单步执行: {description}");
            }
        }
        #endregion

        #region 各工站通信方法

        // 添加偏移值存储字段
        private double _currentOffsetX = 0;
        private double _currentOffsetY = 0;

        /// <summary>
        /// 通知装配站进行取料
        /// </summary>
        private async Task NotifyAssemblyStationForPickup(int stationIndex)
        {
            try
            {
                // 使用集中式事件管理器等待信号
                StationEvents.SendSignal(
                                       StationEvents.MaterialReadySignal,
                                       -1,
                                       $"通知物料{stationIndex}号到达取料位"
                                   );

                _logger.Info($"已通知装配站{stationIndex}号取料位开始取料");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error($"通知装配站拍照失败: {ex.Message}");
                throw;
            }
        }
        /// <summary>
        /// 通知装配站进行取料拍照
        /// </summary>
        private async Task NotifyAssemblyStationForPickupPhoto(int stationIndex)
        {
            try
            {
                _logger.Info($"通知装配站{stationIndex}号取料位拍照");

                // 使用集中式事件管理器发送信号
                //StationEvents.SendSignal(
                //    StationEvents.LoadingStationReadyForPickup,
                //    stationIndex,
                //    $"取料位{stationIndex}就绪"
                //);

                _logger.Info($"已发送装配站拍照信号，站号: {stationIndex}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error($"通知装配站拍照失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 等待装配站拍照完成
        /// </summary>
        private async Task<bool> WaitForAssemblyPhotoComplete(int _currentAssemblyPosition)
        {
            _logger.Info($"【上料流程】等待装配站{_currentAssemblyPosition}号位拍照完成");

            try
            {
                bool signalReceived = StationEvents.WaitForSignal(
                    StationEvents.AssemblyPhotoCompleted,
                    30000, // 30秒超时
                    _loadingCTS.Token
                );

                if (signalReceived)
                {
                    if (StationEvents.OperationResult)
                    {
                        _logger.Info($"装配站{StationEvents.CurrentStationIndex}号位拍照完成");
                        _currentLoadingState = LoadingState.MoveToAssembly;
                        UpdateStepStatus($"装配站拍照完成，准备移动到装配位", true);
                        return true;
                    }
                    else
                    {
                        _logger.Error($"装配站拍照失败: {StationEvents.ErrorMessage}");
                        return false;
                    }
                }
                else
                {
                    _logger.Error("等待装配站拍照完成信号超时");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info("等待装配站拍照完成被取消");
                return false;
            }
        }

        /// <summary>
        /// 等待取料完成并获取偏移值
        /// </summary>
        private async Task<(bool success, double offsetX, double offsetY)> WaitForPickupCompleteWithOffset(int stationIndex)
        {
            int timeout = TypedParameters?.PickupCompleteTimeout ?? 30000;
            int elapsed = 0;

            while (elapsed < timeout)
            {
                if (_loadingCTS.Token.IsCancellationRequested)
                    return (false, 0, 0);

                // 检查取料完成信号并获取偏移值
                // var result = GetPickupCompleteWithOffset(stationIndex);
                bool signalReceived = StationEvents.WaitForSignal(
                   StationEvents.AssemblyPickupCompleted,
                   60000, // 30秒超时
                   _loadingCTS.Token
                );
                if (!signalReceived)
                {

                    _logger.Error("等待取料完成信号超时");
                    return (false, 0, 0);
                }

                var result = (success: true, offsetX: 0.1, offsetY: 0.05); // 使用命名元组

                if (result.success) return result; // 现在可以正确访问success字段

                await Task.Delay(100);
                elapsed += 100;
            }

            return (false, 0, 0);
        }

        // 模拟拍照完成信号检查方法
        private bool CheckPickupPhotoCompleteSignal(int stationIndex)
        {
            // 实际实现应该检查硬件信号或视觉系统反馈
            return true;
        }
        #endregion

        #region 视觉系统通信方法

        /// <summary>
        /// 通知3D视觉系统进行Tab扫描
        /// </summary>
        private async Task Notify3DVisionSystemForTabScan(int photoGroup)
        {
            try
            {
                _logger.Info($"通知3D视觉系统进行Tab{photoGroup}扫描");

                // 使用集中式事件管理器发送视觉请求
                StationEvents.SendSignal(
                       StationEvents.Material3DScanReady,
                       -1,
                       $"通知点胶站物料到达3D扫描位"
                   );

                _logger.Info($"已发送Tab{photoGroup}拍照请求给视觉系统");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error($"通知视觉系统Tab拍照失败: {ex.Message}");
                throw;
            }
        }
        /// <summary>
        /// 等待Tab拍照完成
        /// </summary>
        private async Task<bool> WaitForTabPhotoComplete(int photoGroup)
        {
            _logger.Info($"【上料流程】等待Tab{photoGroup}拍照完成");

            try
            {
                bool signalReceived = StationEvents.WaitForSignal(
                    StationEvents.TabPhotoCompleted,
                    30000, // 30秒超时
                    _loadingCTS.Token
                );

                if (signalReceived)
                {
                    if (StationEvents.OperationResult)
                    {
                        _logger.Info($"Tab{photoGroup}拍照完成");
                        return true;
                    }
                    else
                    {
                        _logger.Error($"Tab{photoGroup}拍照失败: {StationEvents.ErrorMessage}");
                        return false;
                    }
                }
                else
                {
                    _logger.Error($"等待Tab{photoGroup}拍照完成信号超时");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"等待Tab{photoGroup}拍照完成被取消");
                return false;
            }
        }
        // <summary>
        /// 通知视觉系统进行Pillar1拍照
        /// </summary>
        private async Task NotifyVisionSystemForPillar1Photo(int photoGroup)
        {
            try
            {
                _logger.Info($"通知视觉系统进行Pillar1_{photoGroup}拍照");

                // 使用集中式事件管理器发送视觉请求
                StationEvents.SendVisionRequest(
                    StationEvents.VisionRequestType.Pillar1,
                    photoGroup,
                    _currentAssemblyPosition,
                    $"Pillar1_{photoGroup}拍照请求"
                );

                _logger.Info($"已发送Pillar1_{photoGroup}拍照请求给视觉系统");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error($"通知视觉系统Pillar1拍照失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 等待Pillar1拍照完成
        /// </summary>
        private async Task<bool> WaitForPillar1PhotoComplete(int photoGroup)
        {
            _logger.Info($"【上料流程】等待Pillar1_{photoGroup}拍照完成");

            try
            {
                bool signalReceived = StationEvents.WaitForSignal(
                    StationEvents.Pillar1PhotoCompleted,
                    30000, // 30秒超时
                    _loadingCTS.Token
                );

                if (signalReceived)
                {
                    if (StationEvents.OperationResult)
                    {
                        _logger.Info($"Pillar1_{photoGroup}拍照完成");
                        return true;
                    }
                    else
                    {
                        _logger.Error($"Pillar1_{photoGroup}拍照失败: {StationEvents.ErrorMessage}");
                        return false;
                    }
                }
                else
                {
                    _logger.Error($"等待Pillar1_{photoGroup}拍照完成信号超时");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"等待Pillar1_{photoGroup}拍照完成被取消");
                return false;
            }
        }

        /// <summary>
        /// 通知视觉系统进行Pillar2拍照
        /// </summary>
        private async Task NotifyVisionSystemForPillar2Photo(int photoGroup)
        {
            try
            {
                _logger.Info($"通知视觉系统进行Pillar2_{photoGroup}拍照");

                // 使用集中式事件管理器发送视觉请求
                StationEvents.SendVisionRequest(
                    StationEvents.VisionRequestType.Pillar2,
                    photoGroup,
                    _currentAssemblyPosition,
                    $"Pillar2_{photoGroup}拍照请求"
                );

                _logger.Info($"已发送Pillar2_{photoGroup}拍照请求给视觉系统");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error($"通知视觉系统Pillar2拍照失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 等待Pillar2拍照完成
        /// </summary>
        private async Task<bool> WaitForPillar2PhotoComplete(int photoGroup)
        {
            _logger.Info($"【上料流程】等待Pillar2_{photoGroup}拍照完成");

            try
            {
                bool signalReceived = StationEvents.WaitForSignal(
                    StationEvents.AssemblyPhotoCompleted,
                    -1, // 30秒超时
                    _loadingCTS.Token
                );

                if (signalReceived)
                {
                    if (StationEvents.OperationResult)
                    {
                        _logger.Info($"Pillar2_{photoGroup}拍照完成");
                        return true;
                    }
                    else
                    {
                        _logger.Error($"Pillar2_{photoGroup}拍照失败: {StationEvents.ErrorMessage}");
                        return false;
                    }
                }
                else
                {
                    _logger.Error($"等待Pillar2_{photoGroup}拍照完成信号超时");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"等待Pillar2_{photoGroup}拍照完成被取消");
                return false;
            }
        }


        #endregion

        #region 事件定义

        // 定义取料拍照请求事件
        public class PickupPhotoRequestEvent : Prism.Events.PubSubEvent<PickupPhotoRequest> { }

        public class PickupPhotoRequest
        {
            public int StationIndex { get; set; }
            public DateTime RequestTime { get; set; }
        }

        // 定义视觉拍照请求事件
        public class TabPhotoRequestEvent : Prism.Events.PubSubEvent<TabPhotoRequest> { }

        public class TabPhotoRequest
        {
            public int PhotoGroup { get; set; }
            public int StationIndex { get; set; }
            public DateTime RequestTime { get; set; }
        }

        public class Pillar1PhotoRequestEvent : Prism.Events.PubSubEvent<Pillar1PhotoRequest> { }

        public class Pillar1PhotoRequest
        {
            public int PhotoGroup { get; set; }
            public int StationIndex { get; set; }
            public DateTime RequestTime { get; set; }
        }

        public class Pillar2PhotoRequestEvent : Prism.Events.PubSubEvent<Pillar2PhotoRequest> { }

        public class Pillar2PhotoRequest
        {
            public int PhotoGroup { get; set; }
            public int StationIndex { get; set; }
            public DateTime RequestTime { get; set; }
        }
        #endregion
    }
}

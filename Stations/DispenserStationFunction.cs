using Core.Abstraction;
using Core.Models;
using Framework.Services;
using MaterialDesignThemes.Wpf;
using SmarterMotion;
using Stations.Services;
using Stations.TaskParameters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Stations
{
    public partial class DispenserStation
    {
        private enum HomingState
        {
            Start,
            MoveDispZ1AxisHome,
            MoveDispZ2AxisHome,
            MoveDispZ3AxisHome,
            MoveDispYAxisHome,
            MoveDispXAxisHome,
            MoveAxisToSafePos,
            MoveDispYAxisInitPos,
            MoveDispXAxisInitPos,
            Finalize,
            Error
        }
        protected override void InitProcessVar()
        {

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
            if (SetServo(DispZ1.ActId, true) != 0)
            {
                ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.点胶工位Z轴1使能超时, (int)XSysAlarmId.AXIS_ALM, AlarmCategory.SYSTEM.ToString(), "【点胶模组】点胶工位Z轴1使能超时");
                return;
            }
            if (SetServo(DispZ2.ActId, true) != 0)
            {
                ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.点胶工位Z轴2使能超时, (int)XSysAlarmId.AXIS_ALM, AlarmCategory.SYSTEM.ToString(), "【点胶模组】点胶工位Z轴2使能超时");
                return;
            }
            if (SetServo(DispZ3.ActId, true) != 0)
            {
                ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.点胶工位Z轴3使能超时, (int)XSysAlarmId.AXIS_ALM, AlarmCategory.SYSTEM.ToString(), "【点胶模组】点胶工位Z轴3使能超时");
                return;
            }
            if (SetServo(DispY_1.ActId, true) != 0)
            {
                ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.点胶工位Y轴使能超时, (int)XSysAlarmId.AXIS_ALM, AlarmCategory.SYSTEM.ToString(), "【点胶模组】点胶工位Y轴主轴使能超时");
                return;
            }
            if (SetServo(DispY_2.ActId, true) != 0)
            {
                ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.点胶工位Y轴使能超时, (int)XSysAlarmId.AXIS_ALM, AlarmCategory.SYSTEM.ToString(), "【点胶模组】点胶工位Y轴从轴使能超时");
                return;
            }
            if (SetServo(DispX.ActId, true) != 0)
            {
                ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.点胶工位X轴使能超时, (int)XSysAlarmId.AXIS_ALM, AlarmCategory.SYSTEM.ToString(), "【点胶模组】点胶工位X轴使能超时");
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
                        currentState = HomingState.MoveDispZ1AxisHome;
                        break;
                    case HomingState.MoveDispZ1AxisHome:
                        {
                            currentState = MoveDispZ1AxisHome() ?
                                HomingState.MoveDispZ2AxisHome :
                                HomingState.Error;
                            Goto((int)HomingState.MoveDispZ1AxisHome, "点胶工位Z轴1寻原点");
                            break;
                        }
                    case HomingState.MoveDispZ2AxisHome:
                        {
                            currentState = MoveDispZ2AxisHome() ?
                                HomingState.MoveDispZ3AxisHome :
                                HomingState.Error;
                            Goto((int)HomingState.MoveDispZ2AxisHome, "点胶工位Z轴2寻原点");
                            break;
                        }
                    case HomingState.MoveDispZ3AxisHome:
                        {
                            currentState = MoveDispZ3AxisHome() ?
                                HomingState.MoveAxisToSafePos :
                                HomingState.Error;
                            Goto((int)HomingState.MoveDispZ3AxisHome, "点胶工位Z轴3寻原点");
                            break;
                        }
                    case HomingState.MoveAxisToSafePos:
                        currentState = MoveToSafePosition() ?
                           HomingState.MoveDispXAxisHome :
                           HomingState.Error;
                        Goto((int)HomingState.MoveAxisToSafePos, "点胶工位Z轴回待机位");
                        break;
                    case HomingState.MoveDispXAxisHome:
                        currentState = MoveDispXAxisHome() ?
                             HomingState.MoveDispXAxisInitPos :
                             HomingState.Error;
                        Goto((int)HomingState.MoveDispXAxisHome, "点胶工位X轴寻原点");
                        break;
                    case HomingState.MoveDispXAxisInitPos:
                        currentState = MoveDispXAxisStandbyPos() ?
                             HomingState.MoveDispYAxisHome :
                             HomingState.Error;
                        Goto((int)HomingState.MoveDispXAxisInitPos, "点胶工位X轴回待机位");
                        break;
                    case HomingState.MoveDispYAxisHome:
                        currentState = MoveDispYAxisHome() ?
                             HomingState.MoveDispYAxisInitPos :
                             HomingState.Error;
                        Goto((int)HomingState.MoveDispYAxisHome, "点胶工位Y轴寻原点");
                        break;
                    case HomingState.MoveDispYAxisInitPos:
                        StationEvents.SendSignal(
                                          StationEvents.DispensingStationZeroCompleted
                                      );
                        currentState = MoveDispYAxisStandbyPos() ?
                             HomingState.Finalize :
                             HomingState.Error;
                        Goto((int)HomingState.MoveDispYAxisInitPos, "点胶工位Y轴回待机位");
                        break;
                    case HomingState.Finalize:
                        // 完成处理
                        isHomingSuccessful = true;
                        TaskHomeOK = true;
                        this.Station.SetState(XStationState.RESETING);
                        Goto((int)HomingState.Finalize, "点胶工位回零完成");
                        break;
                    case HomingState.Error:
                        isHomingSuccessful = false;
                        break;
                }

                // 添加适当延迟避免CPU忙等待
                Thread.Sleep(10);
            }
        }

        // 开启UV灯 经过参数设定的时长后关闭UV灯
        public async Task StartUVLight(int uvIndex = 1)
        {
            if (TypedParameters.UVFixTime <= 0) return;

            if (uvIndex == 1)
            {
                m_UVLight1.SetDo(1);  // 开灯
                await Task.Delay((int)_recipeService.Parameters.UVFixTime * 1000);
                m_UVLight1.SetDo(0);  // 关灯
            }
            else if (uvIndex == 2)
            {
                m_UVLight2.SetDo(1);  // 开灯
                await Task.Delay((int)_recipeService.Parameters.UVFixTime * 1000);
                m_UVLight2.SetDo(0);  // 关灯
            }
        }
        // 关闭UV灯
        public async Task StopUVLight()
        {
            m_UVLight1.SetDo(0);
            m_UVLight2.SetDo(0);
            await Task.Delay((int)10);
        }
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
        /// <summary>
        /// 多轴运动（支持多位置、多速度）
        /// </summary>
        private bool MoveMultiAxisToPosition(IAxis[] axes, string[] positionNames, double[] baseVelocities)
        {
            // 参数校验
            if (axes == null || axes.Length == 0)
            {
                _logger.Error("【龙门搬运】轴集合为空");
                return false;
            }

            if (positionNames == null || positionNames.Length != axes.Length)
            {
                _logger.Error($"【龙门搬运】位置名称数组长度不匹配，轴数：{axes.Length}，位置数：{positionNames?.Length ?? 0}");
                return false;
            }

            if (baseVelocities == null || baseVelocities.Length != axes.Length)
            {
                _logger.Error($"【龙门搬运】速度数组长度不匹配，轴数：{axes.Length}，速度数：{baseVelocities?.Length ?? 0}");
                return false;
            }

            // 获取所有轴位置和计算实际速度
            var positions = new List<double>();
            var actualVelocities = new List<double>();
            var axisDetails = new List<string>();

            for (int i = 0; i < axes.Length; i++)
            {
                var axis = axes[i];
                string positionName = positionNames[i];
                double baseVelocity = baseVelocities[i];

                // 获取位置
                double pos = GetPosition(axis.ActId, positionName);
                if (pos == -1)
                {
                    _logger.Error($"【龙门搬运】{axis.Name}轴获取位置失败：{positionName}");
                    return false;
                }
                positions.Add(pos);

                // 计算实际速度
                double actualVel = baseVelocity * axis.MotionSpeedRatio;
                actualVelocities.Add(actualVel);

                // 记录详细信息
                axisDetails.Add($"{axis.Name}:{positionName}({pos:F3})@{actualVel:F2}");
            }

            // 准备运动参数
            int[] axisIds = axes.Select(a => a.ActId).ToArray();
            double[] posArray = positions.ToArray();
            double[] velArray = actualVelocities.ToArray();

            // 执行多轴运动
            MoveAbs(axisIds, posArray, velArray);

            // 记录运动信息
            string logPrefix = $"【龙门搬运】多轴运动";
            string detailInfo = string.Join(" | ", axisDetails);

            if (WaitMoveDone())
            {
                _logger.Info($"{logPrefix} 完成，详情：{detailInfo}");
                return true;
            }
            else
            {
                _logger.Warn($"{logPrefix} 超时，详情：{detailInfo}");
                return false;
            }
        }

        /// <summary>
        /// 多轴运动重载方法（所有轴到同一位置，使用相同基准速度）
        /// </summary>
        private bool MoveMultiAxisToPosition(IAxis[] axes, string positionName, double baseVelocity)
        {
            string[] positionNames = Enumerable.Repeat(positionName, axes.Length).ToArray();
            double[] baseVelocities = Enumerable.Repeat(baseVelocity, axes.Length).ToArray();
            return MoveMultiAxisToPosition(axes, positionNames, baseVelocities);
        }

        /// <summary>
        /// 多轴运动重载方法（所有轴到同一位置，使用各自基准速度）
        /// </summary>
        private bool MoveMultiAxisToPosition(IAxis[] axes, string positionName, double[] baseVelocities)
        {
            string[] positionNames = Enumerable.Repeat(positionName, axes.Length).ToArray();
            return MoveMultiAxisToPosition(axes, positionNames, baseVelocities);
        }

        /// <summary>
        /// 多轴运动重载方法（所有轴使用相同基准速度）
        /// </summary>
        private bool MoveMultiAxisToPosition(IAxis[] axes, string[] positionNames, double baseVelocity)
        {
            double[] baseVelocities = Enumerable.Repeat(baseVelocity, axes.Length).ToArray();
            return MoveMultiAxisToPosition(axes, positionNames, baseVelocities);
        }
        /// <summary>
        /// 多轴运动 - 直接使用传入的位置值和速度数组
        /// </summary>
        public bool MoveMultiAxisToPosition(IAxis[] axes, double[] positions, double[] baseVelocities)
        {
            // 参数校验
            if (axes == null || axes.Length == 0)
            {
                _logger.Error("【龙门搬运】轴集合为空");
                return false;
            }

            if (positions == null || positions.Length != axes.Length)
            {
                _logger.Error($"【龙门搬运】位置数组长度不匹配，轴数：{axes.Length}，位置数：{positions?.Length ?? 0}");
                return false;
            }

            if (baseVelocities == null || baseVelocities.Length != axes.Length)
            {
                _logger.Error($"【龙门搬运】速度数组长度不匹配，轴数：{axes.Length}，速度数：{baseVelocities?.Length ?? 0}");
                return false;
            }

            // 验证速度和位置值的有效性
            for (int i = 0; i < axes.Length; i++)
            {
                if (baseVelocities[i] <= 0)
                {
                    _logger.Error($"【龙门搬运】{axes[i].Name}轴速度无效：{baseVelocities[i]}");
                    return false;
                }
            }

            // 准备运动参数
            int[] axisIds = axes.Select(a => a.ActId).ToArray();
            double[] actualVelocities = new double[axes.Length];
            var axisDetails = new List<string>();

            for (int i = 0; i < axes.Length; i++)
            {
                // 计算实际速度（考虑轴的速度比例）
                actualVelocities[i] = baseVelocities[i] * axes[i].MotionSpeedRatio;

                // 记录详细信息
                axisDetails.Add($"{axes[i].Name}:{positions[i]:F3}@{actualVelocities[i]:F2}");
            }

            // 执行多轴运动
            MoveAbs(axisIds, positions, actualVelocities);

            // 记录运动信息
            string logPrefix = $"【龙门搬运】多轴同步运动";
            string detailInfo = string.Join(" | ", axisDetails);

            if (WaitMoveDone())
            {
                _logger.Info($"{logPrefix} 完成，详情：{detailInfo}");
                return true;
            }
            else
            {
                _logger.Warn($"{logPrefix} 超时，详情：{detailInfo}");
                return false;
            }
        }

        private bool MoveDispZ1AxisHome()
        {
            MoveHome(DispZ1.ActId);
            if (WaitMoveDone())
                return true;
            return false;
        }
        private bool MoveDispZ2AxisHome()
        {
            MoveHome(DispZ2.ActId);
            if (WaitMoveDone())
                return true;
            return false;
        }
        private bool MoveDispZ3AxisHome()
        {
            MoveHome(DispZ3.ActId);
            if (WaitMoveDone())
                return true;
            return false;
        }
        private bool MoveDispYAxisHome()
        {
            MoveHome(DispY_1.ActId);
            if (WaitMoveDone())
                return true;
            return false;
        }
        private bool MoveDispXAxisHome()
        {
            MoveHome(DispX.ActId);
            if (WaitMoveDone())
                return true;
            return false;
        }
        private bool MoveDispXAxisStandbyPos()
        {
            return MoveAxisToPosition(DispX, "待机位", _axisConfigService.GetAxisSpeed(0, DispX.ActId));
        }
        public bool MoveDispYAxisStandbyPos()
        {
            return MoveAxisToPosition(DispY_1, "待机位", _axisConfigService.GetAxisSpeed(0, DispY_1.ActId));
        }
        /// <summary>
        /// 移动到安全高度位置（Z轴就位）
        /// </summary>
        private bool MoveToSafePosition()
        {
            _logger.Info("准备扫描位置...");

            // Z1,Z2,Z3轴移动到待机高度
            var zAxes = new[] { DispZ1, DispZ2, DispZ3 };
            string[] positionNames = { "待机位", "待机位", "待机位" };
            double[] baseVelocities = { _axisConfigService.GetAxisSpeed(0, DispZ1.ActId), _axisConfigService.GetAxisSpeed(0, DispZ2.ActId), _axisConfigService.GetAxisSpeed(0, DispZ3.ActId) };

            return MoveMultiAxisToPosition(zAxes, positionNames, baseVelocities);
        }
        // 辅助方法：获取轴位置
        public double GetAxisPosition(int axisID)
        {
            double _position = 0;
            LTDMC.dmc_get_position_unit(0, (ushort)axisID, ref _position);
            return _position;
        }

        #region 统一组拍照方法
        public async Task ReturnToInitialPositionAsync()
        {
            string positionName = "待机位";
            IAxis[] axes = new[] { DispY_1 };
            var velocities = new[] { _axisConfigService.GetAxisSpeed(0, DispY_1.ActId) };
            var positions = new[] { GetPosition(DispY_1.ActId, positionName) };

            if (!MoveAxisToPosition(DispZ3, positionName, _axisConfigService.GetAxisSpeed(0, DispZ3.ActId)))
            {
                throw new InvalidOperationException($"移动到{positionName}失败");
            }
            if (!MoveMultiAxisToPosition(axes, positions, velocities))
            {
                throw new InvalidOperationException($"移动到{positionName}失败");
            }
        }

        /// <summary>
        /// 移动到组拍照位(TAB)
        /// </summary>
        private async Task MoveToGroupPhotoPosition()
        {
            string positionName = GetPhotoPositionName(_currentPhotoGroup, _currentPhotoPosition);
            _logger.Info($"【组装流程】移动到{positionName}");

            IAxis[] axes = new[] { DispX, DispY_1, PlatY };
            var velocities = new[] { _axisConfigService.GetAxisSpeed(0, DispX.ActId), _axisConfigService.GetAxisSpeed(0, DispY_1.ActId), _axisConfigService.GetAxisSpeed(0, PlatY.ActId) };
            var positions = new[] { GetPosition(DispX.ActId, positionName), GetPosition(DispY_1.ActId, positionName), _loadingStation.GetPosition(PlatY.ActId, positionName) };

            if (!MoveMultiAxisToPosition(axes, positions, velocities))
            {
                throw new InvalidOperationException($"移动到{positionName}失败");
            }

            if (!MoveAxisToPosition(DispZ3, positionName, _axisConfigService.GetAxisSpeed(0, DispZ3.ActId)))
            {
                throw new InvalidOperationException($"移动到{positionName}失败");
            }

            _currentDispensingState = DispensingState.FirstCleanGlue;
            UpdateStepStatus($"移动到{positionName}", true);
        }
        /// <summary>
        /// 移动到连续轨迹的起始点
        /// </summary>
        public async Task MoveToContinuousTrajectoryStart(double startPositionX, double startPositionY)
        {
            _logger.Info($"【组装流程】移动到连续轨迹起始点");

            IAxis[] axes = new[] { DispX, DispY_1 };
            var velocities = new[] { _axisConfigService.GetAxisSpeed(0, DispX.ActId), _axisConfigService.GetAxisSpeed(0, DispY_1.ActId) };
            var positions = new[] { startPositionX, startPositionY };

            if (!MoveMultiAxisToPosition(axes, positions, velocities))
            {
                throw new InvalidOperationException($"移动到连续轨迹起始点失败");
            }

            UpdateStepStatus("移动到连续轨迹起始点", true);
        }

        /// <summary>
        /// 触发组拍照
        /// </summary>
        private async Task TriggerGroupTakePhotoAsync()
        {
            string positionName = GetPhotoPositionName(_currentPhotoGroup, _currentPhotoPosition);
            _logger.Info($"【组装流程】在{positionName}触发拍照");

            try
            {
                // 根据当前位置类型发送相应的视觉请求
                if (_currentPhotoPosition == 1) // Tab拍照
                {
                    await NotifyVisionSystemForTabPhoto(_currentPhotoGroup);
                }
                else // Pillar拍照
                {
                    await NotifyVisionSystemForPillarPhoto(_currentPhotoGroup, _currentPhotoPosition);
                }

                // 等待视觉系统完成拍照
                bool visionSuccess = await WaitForVisionSystemPhotoComplete();

                if (visionSuccess)
                {
                    _currentDispensingState = DispensingState.FirstCleanGlue;
                    _logger.Info($"{positionName}拍照成功");
                    _currentDispensingState = DispensingState.MoveToWaitPosition;
                    UpdateStepStatus($"{positionName}拍照成功", true);
                }
                else
                {
                    throw new InvalidOperationException($"{positionName}拍照失败");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"{positionName}拍照异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 等待组拍照完成
        /// </summary>
        private async Task WaitForTakeGroupPhotoCompleteAsync()
        {
            // 判断是否完成所有拍照
            if (_currentPhotoPosition < _positionsPerGroup)
            {
                // 同一组内，移动到下一个拍照位
                _currentPhotoPosition++;
                _currentDispensingState = DispensingState.ExtractPathAndDispensing;
                UpdateStepStatus($"{_currentPhotoPosition - 1}拍照完成，准备下一个拍照位", true);
            }
            else if (_currentPhotoGroup < _totalPhotoGroups)
            {
                // 当前组完成，移动到下一组
                _currentPhotoGroup++;
                _currentPhotoPosition = 1;
                _currentDispensingState = DispensingState.ExtractPathAndDispensing;
                UpdateStepStatus($"第{_currentPhotoGroup - 1}组拍照完成，开始第{_currentPhotoGroup}组", true);
            }
            else
            {
                // 所有拍照完成
                _currentDispensingState = DispensingState.WaitForPillar1Dispensing;
                UpdateStepStatus("所有拍照完成，准备组装", true);
                _logger.Info("【组装流程】所有组拍照完成");
            }

            SendPhotoCompleteToLoadingStation(true, $"第{_currentPhotoGroup - 1}组拍照完成");
        }

        /// <summary>
        /// 根据组号和位置号获取拍照位名称
        /// </summary>
        private string GetPhotoPositionName(int group, int position)
        {
            // position: 1=Tab, 2=Pillar1, 3=Pillar2

            if (position == 1)
            {
                return $"tab_{group}拍照位";  // 例如: tab_1拍照位
            }
            else if (position == 2)
            {
                return $"Pillar{group}_1拍照位";  // 例如: pillar1_1拍照位
            }
            else if (position == 3)
            {
                return $"Pillar{group}_2拍照位";  // 例如: pillar1_2拍照位
            }
            else
            {
                return "unknown拍照位";
            }
        }
        /// <summary>
        /// 通知视觉系统进行Tab拍照
        /// </summary>
        private async Task NotifyVisionSystemForTabPhoto(int photoGroup)
        {
            try
            {
                _logger.Info($"通知视觉系统进行Tab{photoGroup}拍照");

                // 使用相机控制器直接触发拍照
                bool success = await _cameraController.TakePhotoAsync("DispensingCamera", "T1", 10000);

                if (success)
                {
                    _logger.Info($"已成功触发{photoGroup}组Tab拍照");
                }
                else
                {
                    throw new InvalidOperationException($"触发{photoGroup}组Tab,拍照失败");
                }

                _logger.Info($"已发送{photoGroup}组Tab拍照请求给视觉系统");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error($"通知视觉系统Tab拍照失败: {ex.Message}");
                throw;
            }
        }
        /// <summary>
        /// 通知视觉系统进行Pillar拍照
        /// </summary>
        private async Task NotifyVisionSystemForPillarPhoto(int photoGroup, int pillarIndex)
        {
            try
            {
                string pillarType = pillarIndex == 2 ? "Pillar1" : "Pillar2";
                _logger.Info($"通知视觉系统进行{photoGroup}组{pillarType}拍照");

                int positionIndex = pillarIndex + 1; // pillarIndex:1->Pillar1，2->Pillar2
                string command = "";

                if (pillarType == "Pillar1")
                {
                    command = "T2";
                }
                else if (pillarType == "Pillar2")
                {
                    command = "T3";
                }

                // 使用相机控制器直接触发拍照
                bool success = await _cameraController.TakePhotoAsync("DispensingCamera", command, 10000);

                if (success)
                {
                    _logger.Info($"已成功触发{photoGroup}组{pillarType}拍照");
                }
                else
                {
                    throw new InvalidOperationException($"触发{photoGroup}组{pillarType}拍照失败");
                }

                _logger.Info($"已发送{photoGroup}组{pillarType}拍照请求给视觉系统");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error($"通知视觉系统Pillar拍照失败: {ex.Message}");
                throw;
            }
        }
        /// <summary>
        /// 发送拍照完成信号给上料站
        /// </summary>
        private void SendPhotoCompleteToLoadingStation(bool success, string message = "")
        {
            try
            {
                if (success)
                {
                    StationEvents.SendSignal(
                        StationEvents.AssemblyPhotoCompleted,
                        _currentDispensingStep,
                        $"取料位{_currentDispensingStep}拍照完成",
                        true
                    );
                    _logger.Info($"发送拍照完成信号给上料站，站号: {_currentDispensingStep}");
                }
                else
                {
                    StationEvents.SendErrorSignal(
                        StationEvents.AssemblyPhotoCompleted,
                        message,
                        _currentDispensingStep
                    );
                    _logger.Error($"发送拍照失败信号给上料站，站号: {_currentDispensingStep}, 错误: {message}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"发送拍照完成信号失败: {ex.Message}");
            }
        }
        /// <summary>
        /// 发送点胶完成信号给组装工站
        /// </summary>
        private async Task SendDispensingCompleteToAssemblyStation(bool success, string message = "")
        {
            try
            {
                if (success)
                {
                    StationEvents.SendSignal(
                        StationEvents.DispensingCompleted,
                        _currentDispensingStep,
                        $"点胶完成",
                        true
                    );
                    _logger.Info($"发送点胶完成信号给组装站，站号: {_currentDispensingStep}");
                }
                else
                {
                    StationEvents.SendErrorSignal(
                        StationEvents.DispensingCompleted,
                        message,
                        _currentDispensingStep
                    );
                    _logger.Error($"发送点胶完成信号给组装站，站号: {_currentDispensingStep}, 错误: {message}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"发送点胶完成信号失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 等待视觉系统拍照完成
        /// </summary>
        private async Task<bool> WaitForVisionSystemPhotoComplete()
        {
            _logger.Info("【组装流程】等待视觉系统拍照完成");

            try
            {
                // 使用视觉数据服务等待视觉数据
                var visionData = await _visionDataService.WaitForVisionDataAsync("DispensingCamera", 30000); // 30秒超时

                if (visionData.Contains("Success"))
                {
                    _logger.Info($"视觉系统拍照完成，返回数据: {visionData}");

                    // 处理视觉结果
                    await ProcessVisionResult(visionData);

                    return true;
                }
                else
                {
                    _logger.Error($"视觉系统拍照失败: {visionData}");
                    throw new InvalidOperationException($"视觉系统拍照失败: {visionData}");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info("等待视觉系统拍照完成被取消");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"等待视觉系统响应异常: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> MoveToActuatorPhotoPositionAsync(int groupIndex,int actuatorIndex, double zHeight)
        {
            string positionName = $"Actuator{groupIndex}_{actuatorIndex}拍照位";
            var speedXY = 20.0;
            var axes = new[] { DispX, DispY_1 };
            var velocities = new[] { speedXY, speedXY };
            var positions = new[] { GetPosition(DispX.ActId, positionName), GetPosition(DispY_1.ActId, positionName) };

            if (!MoveMultiAxisToPosition(axes, positions, velocities))
            {
                throw new InvalidOperationException($"移动到{positionName}失败");
            }

            // 2. 移动Z轴到拍照高度
            _logger.Info($"移动DispZ3到高度{zHeight:F2}mm");

            double speedZ = 10.0;
            MoveAbs(DispZ3.ActId, zHeight, speedZ);

            if (!WaitMoveDone())
            {
                _logger.Error("移动Z3轴到拍照位置失败");
                return false;
            }
            return true;
        }
        public async Task<bool> ReturnXYAxisToHomeAsync()
        {
            string positionName = $"待机位";
            var axes = new[] { DispX, DispY_1 };
            var velocities = new[] { _axisConfigService.GetAxisSpeed(0, DispX.ActId), _axisConfigService.GetAxisSpeed(0, DispY_1.ActId) };
            var positions = new[] { GetPosition(DispX.ActId, positionName), GetPosition(DispY_1.ActId, positionName) };

            if (!MoveMultiAxisToPosition(axes, positions, velocities))
            {
                throw new InvalidOperationException($"移动到{positionName}失败");
            }
            return true;
        }
        /// <summary>
        /// 发送拍照命令给相机
        /// </summary>
        public async Task<bool> TakePhotoAsync(string cameraType = "DispensingCamera", string command = "TAKE_PHOTO")
        {
            try
            {
                string cameraName = cameraType;

                _logger.Info($"向{cameraName}发送拍照命令: {command}");

                bool success = await _tcpEventService.SendCommandAsync(cameraType, command, 3000);
                if (success)
                {
                    _logger.Info($"{cameraName}拍照命令发送成功");
                }
                else
                {
                    _logger.Error($"{cameraName}拍照命令发送失败");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Error($"拍照命令发送异常: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region 3D相机扫描流程
        /// <summary>
        /// 执行3D相机扫描流程
        /// </summary>
        public async Task<bool> Perform3DScanAsync(CancellationToken cancellationToken = default,
            IProgress<(int progress, string status)> progressCallback = null)
        {
            try
            {
                _logger.Info("【点胶工站】开始3D相机扫描流程");
                progressCallback?.Report((20, "扫描准备完成"));
                // 1. 准备扫描位置
                cancellationToken.ThrowIfCancellationRequested();
                if (!await PrepareForSafePositionAsync())//ReturnToSafePositionAsync
                {
                    _logger.Error("扫描准备失败");
                    return false;
                }

                // 2. 移动到扫描起始位置
                cancellationToken.ThrowIfCancellationRequested();
                progressCallback?.Report((25, "移动到扫描起始位置..."));
                if (!await MoveToScanStartPositionAsync())
                {
                    _logger.Error("移动到扫描起始位置失败");
                    return false;
                }
                // 2.5 准备扫描高度
                cancellationToken.ThrowIfCancellationRequested();
                progressCallback?.Report((35, "移动到扫描高度..."));
                if (!await PrepareForScanningPositionAsync())
                {
                    _logger.Error("扫描高度准备失败");
                    return false;
                }
                // 3. 执行扫描过程
                cancellationToken.ThrowIfCancellationRequested();
                progressCallback?.Report((45, "执行扫描过程..."));
                if (!await ExecuteScanningProcessAsync())
                {
                    _logger.Error("扫描过程执行失败");
                    return false;
                }

                // 4. 返回到安全位置
                cancellationToken.ThrowIfCancellationRequested();
                progressCallback?.Report((95, "返回到安全位置..."));
                if (!await ReturnToSafePositionAsync())
                {
                    _logger.Warn("返回到安全位置失败，但扫描已完成");
                }

                _logger.Info("【点胶工站】3D相机扫描流程完成");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("3D扫描被用户取消");
                // 确保停止所有轴运动
                MoveStop();
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "3D相机扫描流程异常");
                return false;
            }
        }

        /// <summary>
        /// 准备扫描位置 - Z轴就位
        /// </summary>
        private async Task<bool> PrepareForSafePositionAsync()
        {
            _logger.Info("准备扫描位置...");

            // Z1轴移动到扫描高度，Z2、Z3轴移动到待机高度
            var zAxes = new[] { DispZ1, DispZ2, DispZ3 };
            string[] positionNames = { "待机位", "待机位", "待机位" };

            return await MoveMultipleAxesToPositionsAsync(zAxes, positionNames, "扫描准备");
        }

        /// <summary>
        /// Z1轴移动到扫描高度，Z2、Z3轴移动到待机高度
        /// </summary>
        private async Task<bool> PrepareForScanningPositionAsync()
        {
            _logger.Info("准备扫描位置...");

            // Z1轴移动到扫描高度，Z2、Z3轴移动到待机高度
            var zAxes = new[] { DispZ1, DispZ2, DispZ3 };
            string[] positionNames = { "3D扫描位", "待机位", "待机位" };

            return await MoveMultipleAxesToPositionsAsync(zAxes, positionNames, "扫描准备");
        }

        /// <summary>
        /// 移动到安全高度位置（Z轴就位）
        /// </summary>
        private async Task<bool> MoveToSafeHeightAsync()
        {
            _logger.Info("准备扫描位置...");

            // Z1轴移动到扫描高度，Z2、Z3轴移动到待机高度
            var zAxes = new[] { DispZ1, DispZ2, DispZ3 };
            string[] positionNames = { "待机位", "待机位", "待机位" };

            return await MoveMultipleAxesToPositionsAsync(zAxes, positionNames, "点胶准备");
        }
        /// <summary>
        /// 移动到寻针高度位置
        /// </summary>
        private async Task<bool> MoveToSearchNeedledHeightAsync()
        {
            _logger.Info("准备寻针...");

            // Z1轴移动到扫描高度，Z2、Z3轴移动到待机高度
            var zAxes = new[] { DispZ1, DispZ2, DispZ3 };
            string[] positionNames = { "待机位", "寻针位", "待机位" };

            return await MoveMultipleAxesToPositionsAsync(zAxes, positionNames, "准备寻针");
        }
        /// <summary>
        /// 移动Z轴相对距离
        /// </summary>
        private async Task<bool> MoveZAxisRelativeAsync(double distance, double vel)
        {
            _logger.Info($"相对移动Z轴：{distance}mm，速度：{vel}");
            MoveRel(DispZ3.ActId, distance, vel);
            return WaitMoveDone();
        }
        /// <summary>
        /// 移动到扫描起始位置
        /// </summary>
        public async Task<bool> MoveToScanStartPositionAsync()
        {
            _logger.Info("移动到扫描起始位置...");
            var xyAxes = new[] { DispX, DispY_1 };
            return await MoveMultipleAxesToPositionsAsync(xyAxes, "3D扫描位", "XY轴移动扫描位");
        }
        /// <summary>
        /// 移动到扫描起始位置
        /// </summary>
        public async Task<bool> PlatMoveToScanPositionAsync()
        {
            _logger.Info("移动到扫描位置...");
            var yAxes = new[] { PlatY, PlatR };
            double pos1 = _loadingStation.GetPosition(PlatY.ActId, "3D扫描位");
            double pos2 = _loadingStation.GetPosition(PlatR.ActId, "待机位");
            double[] positions = { pos1, pos2 };
            double vel1 = _axisConfigService.GetAxisSpeed(0, PlatY.ActId);
            double vel2 = _axisConfigService.GetAxisSpeed(0, PlatR.ActId);
            double[] vels = { vel1, vel2 };
            return MoveMultiAxisToPosition(yAxes, positions, vels);
        }
        public async Task<bool> PlatRMoveToScanPositionAsync()
        {
            _logger.Info("移动到扫描起始位置...");
            var rAxes = new[] { PlatR };
            return await MoveMultipleAxesToPositionsAsync(rAxes, "待机", "R轴移动扫描位");
        }
        /// <summary>
        /// 执行扫描过程
        /// </summary>
        private async Task<bool> ExecuteScanningProcessAsync()
        {
            _logger.Info("开始执行扫描...");

            // 触发相机拍照
            if (!await TriggerCameraCaptureAsync())
            {
                _logger.Error("相机触发失败");
                return false;
            }

            // 移动到扫描结束位置（边移动边扫描）
            if (!await MoveToScanEndPositionAsync())
            {
                _logger.Error("移动到扫描结束位置失败");
                return false;
            }

            // 等待扫描完成
            await WaitForScanCompletionAsync();
            ResetCameraTrigger();
            _logger.Info("扫描过程完成");
            return true;
        }

        /// <summary>
        /// 返回到安全位置
        /// </summary>
        public async Task<bool> ReturnToSafePositionAsync()
        {
            _logger.Info("返回到安全位置...");

            bool allSuccess = true;

            // 所有Z轴回到待机高度
            var zAxes = new[] { DispZ1, DispZ2, DispZ3 };
            if (!await MoveMultipleAxesToPositionsAsync(zAxes, "待机位", "Z轴安全复位"))
            {
                _logger.Warn("Z轴返回到安全位置失败");
                allSuccess = false;
            }
            return allSuccess;
        }

        /// <summary>
        /// 触发相机拍照
        /// </summary>
        private async Task<bool> TriggerCameraCaptureAsync()
        {
            try
            {
                _logger.Debug("触发相机拍照...");

                // 发送触发信号
                m_CameraExtTrigger.SetDo(1);

                _logger.Debug("相机触发完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "相机触发异常");
                return false;
            }
        }
        private void ResetCameraTrigger()
        {
            m_CameraExtTrigger.SetDo(0);
        }
        /// <summary>
        /// 移动到扫描结束位置
        /// </summary>
        private async Task<bool> MoveToScanEndPositionAsync()
        {
            try
            {
                double startPos = GetPosition(DispX.ActId, "3D扫描位");
                if (startPos == -1)
                {
                    _logger.Error("获取扫描起始位置失败");
                    return false;
                }

                double scanLength = ((DispenserStationParams)Parameters).CameraFOVLength;
                double endPos = startPos + scanLength;

                _logger.Info($"开始扫描移动: {startPos:F3} → {endPos:F3}, 长度: {scanLength:F3}");

                // 使用相对移动进行扫描
                MoveRel(DispX.ActId, scanLength, _axisConfigService.GetAxisSpeed(0, DispX.ActId));
                // 等待移动完成
                if (WaitMoveDone())
                {
                    _logger.Info("扫描移动完成");
                    return true;
                }
                else
                {
                    _logger.Warn("扫描移动超时");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "扫描移动过程异常");
                return false;
            }
        }

        /// <summary>
        /// 等待扫描完成
        /// </summary>
        private async Task WaitForScanCompletionAsync()
        {
            int timeoutMs = 6000;
            int elapsed = 0;
            int checkInterval = 100;

            while (elapsed < timeoutMs)
            {
                // 检查扫描是否完成（通过视觉系统状态或信号）
                StationEvents.SendSignal(
                           StationEvents.Dispensing3DScanCompleted,
                           -1,
                           $"执行3D扫描完成",
                           true
                       );
                bool scanCompleted = true; // 模拟完成

                if (scanCompleted)
                {
                    _logger.Debug("扫描数据处理完成");
                    return;
                }

                await Task.Delay(checkInterval);
                elapsed += checkInterval;
            }

            _logger.Warn("扫描完成等待超时");
        }

        /// <summary>
        /// 异步移动到指定位置
        /// </summary>
        private async Task<bool> MoveAxisToPositionAsync(IAxis axis, string positionName, double velocity)
        {
            try
            {
                double position = GetPosition(axis.ActId, positionName);
                if (position == -1)
                {
                    _logger.Error($"【{Identifier}】{axis.Name}轴获取位置失败：{positionName}");
                    return false;
                }

                return await MoveAxisToAbsolutePositionAsync(axis, position, velocity, positionName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"移动到位置失败: {positionName}");
                return false;
            }
        }

        /// <summary>
        /// 异步移动到绝对位置
        /// </summary>
        private async Task<bool> MoveAxisToAbsolutePositionAsync(IAxis axis, double position, double velocity, string description = "")
        {
            try
            {
                double actualVelocity = velocity * axis.MotionSpeedRatio;

                _logger.Info($"移动 {axis.Name}轴 到 {description}: {position:F3}, 速度: {actualVelocity:F2}");

                MoveAbs(axis.ActId, position, actualVelocity);

                // 异步等待移动完成
                return await WaitForMoveCompletionAsync(axis, description);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"轴移动异常: {axis.Name}");
                return false;
            }
        }

        /// <summary>
        /// 异步等待移动完成
        /// </summary>
        private async Task<bool> WaitForMoveCompletionAsync(IAxis axis, string operationDescription)
        {
            int timeoutMs = 30000; // 30秒超时
            int checkInterval = 50;
            int elapsed = 0;

            while (elapsed < timeoutMs)
            {
                if (WaitMoveDone(timeoutMs))
                {
                    _logger.Debug($"{axis.Name}轴 {operationDescription} 移动完成");
                    return true;
                }

                await Task.Delay(checkInterval);
                elapsed += checkInterval;
            }

            _logger.Warn($"{axis.Name}轴 {operationDescription} 移动超时");
            return false;
        }

        /// <summary>
        /// 多轴移动到相同位置
        /// </summary>
        private async Task<bool> MoveMultipleAxesToPositionsAsync(IAxis[] axes, string positionName, string operationDescription)
        {
            string[] positionNames = Enumerable.Repeat(positionName, axes.Length).ToArray();
            return await MoveMultipleAxesToPositionsAsync(axes, positionNames, operationDescription);
        }

        /// <summary>
        /// 多轴移动到各自位置
        /// </summary>
        private async Task<bool> MoveMultipleAxesToPositionsAsync(IAxis[] axes, string[] positionNames, string operationDescription)
        {
            if (axes == null || axes.Length == 0)
            {
                _logger.Error("轴集合为空");
                return false;
            }

            if (axes.Length != positionNames.Length)
            {
                _logger.Error("轴数量与位置名称数量不匹配");
                return false;
            }

            try
            {
                _logger.Info($"开始多轴移动: {operationDescription}");

                // 获取所有轴位置
                var positions = new List<double>();
                var axisDescriptions = new List<string>();

                for (int i = 0; i < axes.Length; i++)
                {
                    double pos = GetPosition(axes[i].ActId, positionNames[i]);
                    if (pos == -1)
                    {
                        _logger.Error($"{axes[i].Name}轴获取位置失败: {positionNames[i]}");
                        return false;
                    }
                    positions.Add(pos);
                    axisDescriptions.Add($"{axes[i].Name}:{positionNames[i]}");
                }

                // 执行多轴移动
                int[] axisIds = axes.Select(a => a.ActId).ToArray();
                double[] posArray = positions.ToArray();
                double velocity = _axisConfigService.GetAxisSpeed(0, axes[0].ActId) * axes[0].MotionSpeedRatio;

                MoveAbs(axisIds, posArray, velocity);

                _logger.Info($"多轴移动中: {string.Join(", ", axisDescriptions)}");

                // 等待移动完成
                if (await WaitForMultiAxisMoveCompletionAsync(axes, operationDescription))
                {
                    _logger.Info($"多轴移动完成: {operationDescription}");
                    return true;
                }
                else
                {
                    _logger.Warn($"多轴移动超时: {operationDescription}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"多轴移动异常: {operationDescription}");
                return false;
            }
        }

        /// <summary>
        /// 异步等待多轴移动完成
        /// </summary>
        private async Task<bool> WaitForMultiAxisMoveCompletionAsync(IAxis[] axes, string operationDescription)
        {
            int timeoutMs = 30000;

            bool allDone = WaitMoveDone(timeoutMs);

            if (allDone)
            {
                return true;
            }

            return false;
        }

        #endregion

        #region 3D相机标定流程
        // 标定相关的字段
        private CalibrationParameters _calibParams = new CalibrationParameters();
        private CancellationTokenSource _calibrationCancellationTokenSource;
        public double CalibrationProgress; // 标定进度属性
        public string CalibrationStatus = "就绪";   // 标定状态属性

        // 标定参数类
        public class CalibrationParameters
        {
            public double RStepAngle { get; set; } = 10.0;        // R轴步进角度
            public int RScanCount { get; set; } = 36;             // R轴扫描次数（360°/10°）
            public double UStepAngle { get; set; } = 5.0;         // U轴步进角度
            public int UScanCountPerSide { get; set; } = 5;       // U轴每边扫描次数
            public double ScanSpeed { get; set; } = 30.0;         // 扫描速度
        }

        /// <summary>
        /// 执行3D相机标定流程
        /// </summary>
        /// <returns>标定是否成功</returns>
        public async Task<bool> Perform3DCalibrationAsync()
        {
            try
            {
                _calibrationCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _calibrationCancellationTokenSource.Token;

                CalibrationStatus = "标定中...";
                CalibrationProgress = 0;

                _logger.Info("【点胶工站】开始3D相机标定流程");

                // 1. 初始化标定参数
                if (!await InitializeCalibrationParametersAsync())
                {
                    _logger.Error("标定参数初始化失败");
                    CalibrationStatus = "初始化失败";
                    return false;
                }

                // 2. R轴360度扫描（每10°一次）
                CalibrationProgress = 25;
                CalibrationStatus = "R轴标定扫描中...";
                if (!await PerformRAxisCalibrationAsync(cancellationToken))
                {
                    _logger.Error("R轴标定扫描失败");
                    CalibrationStatus = "R轴标定失败";
                    return false;
                }

                // 3. R轴0°时U轴正负方向扫描
                CalibrationProgress = 50;
                CalibrationStatus = "R轴0° U轴标定中...";
                if (!await PerformUAxisCalibrationAtRAngleAsync(0, cancellationToken))
                {
                    _logger.Error("R轴0°时U轴标定扫描失败");
                    CalibrationStatus = "R轴0°标定失败";
                    return false;
                }

                // 4. R轴180°时U轴正负方向扫描
                CalibrationProgress = 75;
                CalibrationStatus = "R轴180° U轴标定中...";
                if (!await PerformUAxisCalibrationAtRAngleAsync(180, cancellationToken))
                {
                    _logger.Error("R轴180°时U轴标定扫描失败");
                    CalibrationStatus = "R轴180°标定失败";
                    return false;
                }

                // 5. 标定完成，回到安全位置
                CalibrationProgress = 90;
                CalibrationStatus = "返回安全位置...";
                if (!await ReturnToSafePositionAfterCalibrationAsync())
                {
                    _logger.Warn("标定完成后返回安全位置失败");
                }

                CalibrationProgress = 100;
                CalibrationStatus = "标定完成";

                _logger.Info("【点胶工站】3D相机标定流程完成");
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Warn("3D标定被用户取消");
                CalibrationStatus = "已取消";
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "3D相机标定流程异常");
                CalibrationStatus = "标定异常";
                return false;
            }
            finally
            {
                _calibrationCancellationTokenSource?.Dispose();
                _calibrationCancellationTokenSource = null;
            }
        }
        // 停止标定命令
        public void Stop3DCalibration()
        {
            _calibrationCancellationTokenSource?.Cancel();
            CalibrationStatus = "已取消";
        }
        /// <summary>
        /// 初始化标定参数
        /// </summary>
        private async Task<bool> InitializeCalibrationParametersAsync()
        {
            try
            {
                _logger.Info("初始化标定参数...");

                // 从参数中获取标定设置
                var stationParams = (DispenserStationParams)Parameters;

                _calibParams.RStepAngle = stationParams.RStepAngle;
                _calibParams.RScanCount = stationParams.RScanCount;
                _calibParams.UStepAngle = stationParams.UStepAngle;
                _calibParams.UScanCountPerSide = stationParams.UScanCountPerSide;
                _calibParams.ScanSpeed = stationParams.CalibrationScanSpeed;

                _logger.Info($"标定参数: R步进={_calibParams.RStepAngle}°, R次数={_calibParams.RScanCount}, " +
                           $"U步进={_calibParams.UStepAngle}°, U单边次数={_calibParams.UScanCountPerSide}");

                // 移动到标定起始位置
                return await MoveToCalibrationStartPositionAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "初始化标定参数异常");
                return false;
            }
        }

        /// <summary>
        /// 移动到标定起始位置
        /// </summary>
        private async Task<bool> MoveToCalibrationStartPositionAsync()
        {
            _logger.Info("移动到标定起始位置...");

            bool allSuccess = true;

            // Z轴移动到标定高度
            var zAxes = new[] { DispZ1, DispZ2, DispZ3 };
            if (!await MoveMultipleAxesToPositionsAsync(zAxes, "标定位", "Z轴标定准备"))
            {
                _logger.Warn("Z轴移动到标定位失败");
                allSuccess = false;
            }

            // R轴回到0度
            if (!await MoveAxisToPositionAsync(PlatR, "0度", _calibParams.ScanSpeed))
            {
                _logger.Warn("R轴回到0度失败");
                allSuccess = false;
            }

            // U轴回到0度
            if (!await MoveAxisToPositionAsync(PlatU, "0度", _calibParams.ScanSpeed))
            {
                _logger.Warn("U轴回到0度失败");
                allSuccess = false;
            }

            return allSuccess;
        }

        /// <summary>
        /// 执行R轴标定扫描
        /// </summary>
        private async Task<bool> PerformRAxisCalibrationAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.Info("开始R轴标定扫描...");

                // R轴从0度开始，每次增加_stepAngle，共_scanCount次
                for (int i = 0; i < _calibParams.RScanCount; i++)
                {
                    // 检查取消请求
                    cancellationToken.ThrowIfCancellationRequested();

                    double targetAngle = i * _calibParams.RStepAngle;

                    _logger.Info($"R轴标定第 {i + 1}/{_calibParams.RScanCount} 次: {targetAngle:F1}°");

                    // 移动到目标角度
                    if (!await MoveRAxisToAngleAsync(targetAngle))
                    {
                        _logger.Error($"R轴移动到 {targetAngle:F1}° 失败");
                        return false;
                    }

                    // 等待稳定
                    var stationParams = (DispenserStationParams)Parameters;
                    await Task.Delay((int)stationParams.CalibrationStableTime, cancellationToken);

                    // 执行相机扫描
                    if (!await PerformCalibrationScanAsync($"R_{targetAngle:F1}"))
                    {
                        _logger.Error($"R轴 {targetAngle:F1}° 扫描失败");
                        return false;
                    }

                    // 更新进度
                    CalibrationProgress = 25 + (i * 20 / _calibParams.RScanCount);
                }

                _logger.Info("R轴标定扫描完成");
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "R轴标定扫描异常");
                return false;
            }
        }

        /// <summary>
        /// 在指定R轴角度下执行U轴标定
        /// </summary>
        /// <param name="rAngle">R轴角度</param>
        private async Task<bool> PerformUAxisCalibrationAtRAngleAsync(double rAngle, CancellationToken cancellationToken)
        {
            try
            {
                _logger.Info($"开始在R轴 {rAngle}° 下执行U轴标定");

                // 移动R轴到指定角度
                if (!await MoveRAxisToAngleAsync(rAngle))
                {
                    _logger.Error($"移动R轴到 {rAngle}° 失败");
                    return false;
                }

                int totalUScans = _calibParams.UScanCountPerSide * 2;
                int currentScan = 0;

                // U轴正向扫描（0° → +角度）
                for (int i = 1; i <= _calibParams.UScanCountPerSide; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    double targetAngle = i * _calibParams.UStepAngle;

                    _logger.Info($"U轴正向标定第 {i}/{_calibParams.UScanCountPerSide} 次: +{targetAngle:F1}°");

                    if (!await MoveUAxisToAngleAsync(targetAngle))
                    {
                        _logger.Error($"U轴移动到 +{targetAngle:F1}° 失败");
                        return false;
                    }

                    var stationParams = (DispenserStationParams)Parameters;
                    await Task.Delay((int)stationParams.CalibrationStableTime, cancellationToken);

                    if (!await PerformCalibrationScanAsync($"R{rAngle}_U+{targetAngle:F1}"))
                    {
                        _logger.Error($"U轴 +{targetAngle:F1}° 扫描失败");
                        return false;
                    }

                    currentScan++;
                }

                // U轴回到0度
                if (!await MoveUAxisToAngleAsync(0))
                {
                    _logger.Warn("U轴回到0度失败");
                }

                // U轴负向扫描（0° → -角度）
                for (int i = 1; i <= _calibParams.UScanCountPerSide; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    double targetAngle = -i * _calibParams.UStepAngle;

                    _logger.Info($"U轴负向标定第 {i}/{_calibParams.UScanCountPerSide} 次: {targetAngle:F1}°");

                    if (!await MoveUAxisToAngleAsync(targetAngle))
                    {
                        _logger.Error($"U轴移动到 {targetAngle:F1}° 失败");
                        return false;
                    }

                    var stationParams = (DispenserStationParams)Parameters;
                    await Task.Delay((int)stationParams.CalibrationStableTime, cancellationToken);

                    if (!await PerformCalibrationScanAsync($"R{rAngle}_U{targetAngle:F1}"))
                    {
                        _logger.Error($"U轴 {targetAngle:F1}° 扫描失败");
                        return false;
                    }

                    currentScan++;
                }

                // U轴回到0度
                if (!await MoveUAxisToAngleAsync(0))
                {
                    _logger.Warn("U轴回到0度失败");
                }

                _logger.Info($"R轴 {rAngle}° 下U轴标定完成");
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"R轴 {rAngle}° 下U轴标定异常");
                return false;
            }
        }

        /// <summary>
        /// 移动R轴到指定角度
        /// </summary>
        private async Task<bool> MoveRAxisToAngleAsync(double angle)
        {
            try
            {
                string positionName = $"{angle:F1}度";
                double position = GetPosition(PlatR.ActId, positionName);

                if (position == -1)
                {
                    // 如果位置表中没有定义，使用相对计算
                    position = angle; // 假设角度直接对应位置值
                }

                return await MoveAxisToAbsolutePositionAsync(PlatR, position, _calibParams.ScanSpeed, $"{angle:F1}度");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"移动R轴到 {angle:F1}° 异常");
                return false;
            }
        }

        /// <summary>
        /// 移动U轴到指定角度
        /// </summary>
        private async Task<bool> MoveUAxisToAngleAsync(double angle)
        {
            try
            {
                string positionName = $"{angle:F1}度";
                double position = GetPosition(PlatU.ActId, positionName);

                if (position == -1)
                {
                    position = angle; // 假设角度直接对应位置值
                }

                return await MoveAxisToAbsolutePositionAsync(PlatU, position, _calibParams.ScanSpeed, $"{angle:F1}度");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"移动U轴到 {angle:F1}° 异常");
                return false;
            }
        }

        /// <summary>
        /// 执行标定扫描
        /// </summary>
        private async Task<bool> PerformCalibrationScanAsync(string scanId)
        {
            try
            {
                _logger.Debug($"执行标定扫描: {scanId}");

                // 触发相机拍照
                if (!await TriggerCameraCaptureAsync())
                {
                    _logger.Error($"标定扫描 {scanId} 相机触发失败");
                    return false;
                }

                // 等待扫描数据处理（可根据实际视觉系统调整）
                await Task.Delay(500);

                _logger.Debug($"标定扫描 {scanId} 完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"标定扫描 {scanId} 异常");
                return false;
            }
        }

        /// <summary>
        /// 标定完成后返回安全位置
        /// </summary>
        private async Task<bool> ReturnToSafePositionAfterCalibrationAsync()
        {
            _logger.Info("标定完成，返回安全位置...");

            bool allSuccess = true;

            // U轴回到0度
            if (!await MoveUAxisToAngleAsync(0))
            {
                _logger.Warn("U轴回到0度失败");
                allSuccess = false;
            }

            // R轴回到0度
            if (!await MoveRAxisToAngleAsync(0))
            {
                _logger.Warn("R轴回到0度失败");
                allSuccess = false;
            }

            // Z轴回到待机高度
            var zAxes = new[] { DispZ1, DispZ2, DispZ3 };
            if (!await MoveMultipleAxesToPositionsAsync(zAxes, "待机位", "Z轴标定后复位"))
            {
                _logger.Warn("Z轴返回到待机位失败");
                allSuccess = false;
            }

            return allSuccess;
        }
        #endregion

        /// <summary>
        /// 从字符列表解析点胶路径
        /// </summary>
        private List<PointF> ParseDispensingPath(List<char> charPath)
        {
            var path = new List<PointF>();

            if (charPath == null || charPath.Count == 0)
                return path;

            try
            {
                // 假设字符列表的格式为: "X1,Y1;X2,Y2;X3,Y3;..."
                string pathString = new string(charPath.ToArray());
                var points = pathString.Split(';');

                foreach (var point in points)
                {
                    var coordinates = point.Split(',');
                    if (coordinates.Length == 2)
                    {
                        if (float.TryParse(coordinates[0], out float x) &&
                            float.TryParse(coordinates[1], out float y))
                        {
                            path.Add(new PointF(x, y));
                        }
                    }
                }

                _logger.Info($"解析点胶路径完成，共 {path.Count} 个点");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "解析点胶路径失败");
            }

            return path;
        }

        /// <summary>
        /// 创建默认点胶路径
        /// </summary>
        private List<PointF> CreateDefaultDispensingPath()
        {
            var defaultPath = new List<PointF>
            {
                new PointF(0, 0),
                new PointF(100, 0),
                new PointF(100, 50),
                new PointF(0, 50),
                new PointF(0, 0)
            };

            _logger.Info("使用默认点胶路径");
            return defaultPath;
        }

        /// <summary>
        /// 0. 初始化点胶参数
        /// </summary>
        private async Task<bool> InitializeDispensingParametersAsync()
        {
            try
            {
                _logger.Info("初始化点胶参数...");

                var stationParams = (DispenserStationParams)Parameters;

                // 获取点胶路径
                _dispensingPath = stationParams.DispensingPath?.ToList() ?? CreateDefaultDispensingPath();

                if (_dispensingPath.Count == 0)
                {
                    _logger.Error("点胶路径为空");
                    //return true;
                }

                // 获取点胶参数
                double dispensingTime = stationParams.DispensingTime;
                double dispensingPressure = stationParams.DispensingPressure;
                double dispensingVacuum = stationParams.DispensingVacuum;

                _logger.Info($"点胶参数: 路径点={_dispensingPath.Count}, 时间={dispensingTime}s, " +
                           $"压力={dispensingPressure}MPa, 负压={dispensingVacuum}MPa");

                // 记录路径点详细信息
                for (int i = 0; i < _dispensingPath.Count; i++)
                {
                    _logger.Info($"路径点 {i}: X={_dispensingPath[i].X:F3}, Y={_dispensingPath[i].Y:F3}");
                }

                // 应用点胶参数到硬件
                await ApplyDispensingParametersToHardwareAsync(dispensingPressure, dispensingVacuum);

                // 重置点胶索引
                _currentDispensingPosition = 0;
                _currentDispensingState = DispensingState.WaitForPillarCorrectionTrigger;
                _logger.Info("点胶参数初始化完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "初始化点胶参数异常");
                return false;
            }
        }
        /// <summary>
        /// 1.等待被触发纠正Pillar
        /// </summary>
        private async Task<bool> WaitForPillarCorrectionTriggerAsync()
        {
            try
            {
                _logger.Info("步骤1: 等待Pillar纠正触发信号...");

                // 更新进度显示
                UpdateStepStatus("等待Pillar纠正触发信号", true);

                bool signalReceived = StationEvents.WaitForSignal(
                                        StationEvents.Material3DScanReady,
                                        -1, // 30秒超时
                                        _dispensingCTS.Token
                                    );

                if (!signalReceived)
                {
                    _logger.Error($"Pillar纠正触发信号超时");
                    return false;
                }

                _logger.Info("Pillar纠正触发信号已收到");
                UpdateStepStatus("Pillar纠正触发完成", true);

                // 更新状态到下一步
                _currentDispensingState = DispensingState.CorrectPillar1;
                _currentDispensingStep = 2;

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"等待Pillar纠正触发异常: {ex.Message}");
                UpdateStepStatus($"等待触发异常: {ex.Message}", false);
                return false;
            }
        }
        /// <summary>
        /// 2.纠正Pillar1
        /// </summary>
        private async Task<bool> CorrectPillar1Async()
        {
            try
            {
                _logger.Info("步骤2: 开始纠正Pillar1角度...");
                UpdateStepStatus("纠正Pillar1角度", true);

                // 设置纠正参数
                int pillarIndex = 1;

                // 调用现有的CorrectPillarAngleAsync方法
                bool correctionResult = await CorrectPillarAngleAsync(
                    pillarIndex: pillarIndex,
                    cancellationToken: _dispensingCTS.Token,
                    null);

                if (!correctionResult)
                {
                    _logger.Error("Pillar1角度纠正失败");
                    UpdateStepStatus("Pillar1角度纠正失败", false);

                    // 弹窗提示是否继续流程
                    var result = DialogService.ShowBlockingDialog(
                                 title: "⚠️警告",
                                 message: "【点胶模组】Pillar1角度纠正失败：是否继续或重新纠正?" + "\r\n",
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
                        _logger.Warn("Pillar1纠正失败，但配置允许继续流程");
                    }
                }
                else
                {
                    _logger.Info("Pillar1角度纠正成功");
                    UpdateStepStatus("Pillar1角度纠正完成", true);
                }

                // 更新状态到下一步
                _currentDispensingState = DispensingState.CaptureTabOffset;
                _currentDispensingStep = 3;

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Pillar1纠正被取消");
                UpdateStepStatus("Pillar1纠正已取消", false);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"Pillar1纠正异常: {ex.Message}");
                UpdateStepStatus($"Pillar1纠正异常: {ex.Message}", false);
                return false;
            }
        }
        /// <summary>
        /// 3.拍Tab获得偏移量
        /// </summary>
        private async Task<bool> CaptureTabOffsetAsync()
        {
            try
            {
                _logger.Info("步骤3: 开始Tab位置纠正...");
                UpdateStepStatus("Tab位置纠正", true);

                // 获取Tab索引
                _currentDispensingPosition++;
                int tabIndex = _currentDispensingPosition;

                // 调用现有的CalculateTabCompensationAsync方法
                bool tabCorrectionResult = await CalculateTabCompensationAsync(
                    tabIndex: tabIndex,
                    cancellationToken: _dispensingCTS.Token,
                    progressCallback: new Progress<(int progress, string status)>(
                        (progress) =>
                        {
                            UpdateStepStatus($"Tab纠正: {progress.status}", true);
                        }));

                if (!tabCorrectionResult)
                {
                    _currentDispensingPosition--;
                    _logger.Error("Tab位置纠正失败");
                    UpdateStepStatus("Tab位置纠正失败", false);

                    // 弹窗提示是否继续流程
                    var result = DialogService.ShowBlockingDialog(
                                 title: "⚠️警告",
                                 message: "【点胶模组】Tab位置纠正失败：是否继续或重新纠正?" + "\r\n",
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
                        _logger.Warn("Tab位置纠正失败，但配置允许继续流程");
                    }
                }
                else
                {
                    _logger.Info("Tab位置纠正成功");
                    UpdateStepStatus("Tab位置纠正完成", true);
                }

                // 更新状态到下一步
                _currentDispensingState = DispensingState.ReturnToScanPosition;
                _currentDispensingStep = 4;

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("Tab位置纠正被取消");
                UpdateStepStatus("Tab位置纠正已取消", false);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"Tab位置纠正异常: {ex.Message}");
                UpdateStepStatus($"Tab位置纠正异常: {ex.Message}", false);
                return false;
            }
        }
        /// <summary>
        /// 4.各轴回待机位
        /// </summary>
        private async Task<bool> ReturnToStandbyPositionAsync()
        {
            try
            {
                _logger.Info("步骤4: 各轴回待机位...");
                UpdateStepStatus("各轴回待机位", true);

                // 移动Z轴待机位置
                if (!await ReturnToSafePositionAsync())
                {
                    _logger.Error("Z轴回待机位失败");
                    return false;
                }

                // 移动到Y轴待机位置
                //string standbyPositionName = "待机位";
                //if (!await MoveAxisToPositionAsync(DispY_1, standbyPositionName, _axisConfigService.GetAxisSpeed(0, DispY_1.ActId)))
                //{
                //    _logger.Error("Y轴回待机位失败");
                //    return false;
                //}

                _logger.Info("各轴已回到待机位");
                UpdateStepStatus("各轴已回到待机位", true);

                // 更新状态到下一步
                _currentDispensingState = DispensingState.Perform3DScan;
                _currentDispensingStep = 5;

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"回待机位异常: {ex.Message}");
                UpdateStepStatus($"回待机位异常: {ex.Message}", false);
                return false;
            }
        }

        /// <summary>
        /// 4.轴Y回到3D扫描位
        /// </summary>
        /// <returns></returns>
        private async Task<bool> ReturnToScanPosition()
        {
            try
            {
                _logger.Info("移动到扫描位置...");
                UpdateStepStatus("移动到扫描位置", true);

                // 调用MoveToScanPosition方法
                bool moveResult = await _loadingStation.MoveToScanPosition();

                if (!moveResult)
                {
                    _logger.Error("移动到扫描位置失败");
                    return false;
                }

                _logger.Info("已成功移动到扫描位置");
                UpdateStepStatus("已成功移动到扫描位置", true);

                // 更新状态到下一步
                _currentDispensingState = DispensingState.Perform3DScan;
                _currentDispensingStep = 5;
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"移动到扫描位置异常: {ex.Message}");
                UpdateStepStatus($"移动到扫描位置异常: {ex.Message}", false);
                return false;
            }
        }

        /// <summary>
        /// 5. 3D相机扫描定位（并行处理）
        /// </summary>
        private async Task<bool> Perform3DScanForDispensingAsync()
        {
            try
            {
                _logger.Info("执行3D相机扫描定位...");

                // 1. 触发3D相机拍照
                var photoResult = await _cameraController.TakePhotoAsync("3DCAMERA", "TRIGGER", 180 * 1000);
                if (!photoResult)
                {
                    _logger.Error("3D相机拍照失败");
                    return false;
                }

                _logger.Info("3D相机拍照成功，开始并行处理扫描和数据接收");

                // 2. 并行执行扫描和等待数据
                var scanCompleted = false;
                var dataReceived = false;
                var scanTask = Task.Run(async () =>
                {
                    bool result = await Perform3DScanAsync();
                    scanCompleted = true;
                    _logger.Info($"3D扫描完成，结果: {result}");
                    return result;
                });

                var dataTask = Task.Run(async () =>
                {
                    bool result = await WaitForScanDataProcessingAsync();
                    dataReceived = true;
                    _logger.Info($"扫描数据处理完成，结果: {result}");
                    return result;
                });

                // 3. 等待两个任务都完成
                await Task.WhenAll(scanTask, dataTask);

                bool scanSuccess = await scanTask;
                bool dataProcessed = await dataTask;

                if (!scanSuccess)
                {
                    _logger.Error("3D扫描失败");
                    return false;
                }

                if (!dataProcessed)
                {
                    _logger.Error("3D扫描接收数据失败");
                    return false;
                }

                // 4. 获取扫描结果（Tab高度）
                if (!await UpdateDispensingPathFromScanResultAsync())
                {
                    _logger.Warn("无法从扫描结果更新点胶路径，使用预设路径");
                }

                dispensingPathIndex++;
                _currentDispensingState = DispensingState.ExtractPathAndDispensing;
                _logger.Info("3D相机扫描定位完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "3D相机扫描定位异常" + ex.Message);
                return false;
            }
        }
        /// <summary>
        /// 8.XY轴退到待机位
        /// </summary>

        private async Task<bool> MoveToWaitPositionAsync()
        {
            _logger.Info("xy轴退回待机位...");
            if (!await ReturnXYAxisToHomeAsync())
            {
                _logger.Error("xy轴退回待机位失败");
                return false;
            }
            // 通知组装站开始组装       
            _currentDispensingState = DispensingState.WaitForPillar1Dispensing;
            return true;
        }
        /// <summary>
        /// 9. 等待Pillar点胶信号
        /// </summary>
        private async Task<bool> WaitForPillar1DispensingAsync()
        {
            _logger.Info($"【上料流程】等待Pillar点胶信号");

            try
            {
                // 发送点胶完成信号
                StationEvents.SendSignal(
                                      StationEvents.DispensingCompleted
                                  );
                bool signalReceived = StationEvents.WaitForSignal(
                    StationEvents.AssemblyCompleted,
                    -1, // 30秒超时
                    _dispensingCTS.Token
                );

                if (signalReceived)
                {
                    if (StationEvents.OperationResult)
                    {
                        _currentDispensingState = DispensingState.Perform3DScan;
                        _logger.Info($"Pillar点胶信号被触发");
                        return true;
                    }
                    else
                    {
                        _logger.Error($"Pillar点胶信号超时: {StationEvents.ErrorMessage}");
                        return false;
                    }
                }
                else
                {
                    _logger.Error($"Pillar点胶信号超时");
                    return false;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"等待Pillar点胶信号被取消");
                return false;
            }
        }
        /// <summary>
        /// 10. Pillar1点胶动作
        /// </summary>
        private async Task<bool> Pillar1DispensingAsync()
        {
            try
            {
                _logger.Info("Pillar1点胶动作开始...");
                int pillarIndex = _currentDispensingStep;
                int selectedIndex = 1;
                double PillarDispensingHeight = 0;
                double PillarHeightDeltaZ = 0;
                double PillarDispensingTime = 0;
                bool AutoDescendForDispensing = true;
                double CalibrationDeltaX = 0;
                double CalibrationDeltaY = 0;
                double CompensationX = 0;
                double CompensationY = 0;
                return await DispensePillarAsync(
                           pillarIndex,
                           selectedIndex,
                           PillarDispensingHeight,
                           PillarHeightDeltaZ,
                           PillarDispensingTime,
                           AutoDescendForDispensing,
                           CalibrationDeltaX,
                           CalibrationDeltaY,
                           CompensationX,
                           CompensationY
                       );
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Pillar1点胶动作异常");
                return false;
            }
        }
        /// <summary>
        /// 11. Pillar2点胶动作
        /// </summary>
        private async Task<bool> Pillar2DispensingAsync()
        {
            try
            {
                _logger.Info("Pillar2点胶动作开始...");
                int pillarIndex = _currentDispensingStep;
                int selectedIndex = 2;
                double PillarDispensingHeight = 0;
                double PillarHeightDeltaZ = 0;
                double PillarDispensingTime = 0;
                bool AutoDescendForDispensing = true;
                double CalibrationDeltaX = 0;
                double CalibrationDeltaY = 0;
                double CompensationX = 0;
                double CompensationY = 0;
                bool dispResult = await DispensePillarAsync(
                           pillarIndex,
                           selectedIndex,
                           PillarDispensingHeight,
                           PillarHeightDeltaZ,
                           PillarDispensingTime,
                           AutoDescendForDispensing,
                           CalibrationDeltaX,
                           CalibrationDeltaY,
                           CompensationX,
                           CompensationY
                       );
                if (dispResult)
                {
                    _logger.Info("Pillar2点胶动作完成");
                    _currentDispensingState = DispensingState.SecondCleanGlue;
                    return true;
                }
                else
                {
                    _logger.Info("Pillar2点胶动作失败");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Pillar2点胶动作异常");
                return false;
            }
        }
        private async Task PostPillarDispensingAsync()
        {
            _currentDispensingStep++;
            if (_currentDispensingStep >= 6 )
            {
                _currentDispensingStep = 0;
            }
            _currentDispensingState = DispensingState.SecondCleanGlue;
        }
        /// <summary>
        /// 13. 清理胶头
        /// </summary>
        private async Task<bool> SecondCleanGlueAsync()
        {
            try
            {
                _logger.Info("开始清理胶头...");

                var stationParams = (DispenserStationParams)Parameters;

                // Z轴抬升到安全高度
                if (!await MoveZAxesToSafeHeightAsync())
                {
                    _logger.Warn("Z轴抬升到安全高度失败");
                }

                // 移动到清理位置
                if (!await MoveToCleaningPositionAsync())
                {
                    _logger.Warn("移动到清理位置失败");
                }

                _logger.Info("胶头清理完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "清理胶头异常");
                return false;
            }
        }
        /// <summary>
        /// 14. 执行点胶循环
        /// </summary>
        private async Task<bool> ExecuteDispensingCycleAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.Info("开始点胶循环...");

                var stationParams = (DispenserStationParams)Parameters;
                double dispensingTime = stationParams.DispensingTime;
                double moveSpeed = stationParams.DispensingMoveSpeed;

                for (_currentDispensingPosition = 0;
                     _currentDispensingPosition < _dispensingPath.Count;
                     _currentDispensingPosition++)
                {
                    // 检查取消请求
                    cancellationToken.ThrowIfCancellationRequested();

                    PointF currentPoint = _dispensingPath[_currentDispensingPosition];

                    _logger.Info($"点胶第 {_currentDispensingPosition + 1}/{_dispensingPath.Count} 点: " +
                               $"X={currentPoint.X:F3}, Y={currentPoint.Y:F3}");

                    // 移动到当前点（第一个点已经在起始位置）
                    if (_currentDispensingPosition > 0)
                    {
                        if (!await MoveToXYPositionAsync(currentPoint, moveSpeed))
                        {
                            _logger.Error($"移动到点胶点 {_currentDispensingPosition} 失败");
                            return false;
                        }
                    }

                    // 执行点胶动作
                    if (!await PerformSingleDispensingAsync(dispensingTime))
                    {
                        _logger.Error($"点胶点 {_currentDispensingPosition} 点胶失败");
                        return false;
                    }

                    // 点胶间隔（如果需要）
                    if (stationParams.DispensingInterval > 0)
                    {
                        await Task.Delay((int)stationParams.DispensingInterval, cancellationToken);
                    }
                }

                _logger.Info("点胶循环完成");
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "点胶循环异常");
                return false;
            }
        }

        #region 点胶辅助方法

        /// <summary>
        /// 应用点胶参数到硬件
        /// </summary>
        private async Task ApplyDispensingParametersToHardwareAsync(double pressure, double vacuum)
        {
            try
            {
                _logger.Debug($"应用点胶参数到硬件: 压力={pressure}MPa, 负压={vacuum}MPa");

                // 设置压力控制器
                // PressureController.SetPressure(pressure);

                // 设置负压控制器
                // VacuumController.SetVacuum(vacuum);

                await Task.Delay(100); // 等待参数设置完成
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "应用点胶参数到硬件异常");
                throw;
            }
        }

        /// <summary>
        /// 等待扫描数据处理完成
        /// </summary>
        private async Task<bool> WaitForScanDataProcessingAsync()
        {
            int timeoutMs = 60000;

            _logger.Debug("等待扫描数据处理...");

            var processingComplete = await _visionDataService.WaitForVisionDataAsync("3DCAMERA", timeoutMs);

            if (!processingComplete.Contains("SUCCESS"))
            {
                _logger.Warn("扫描数据处理等待超时");
                var result = DialogService.ShowBlockingDialog(
                                               title: "⚠️警告",
                                               message: "【点胶工站】获取3D相机结果,超时" + "\r\n",
                                               yesButtonText: "重试",
                                               noButtonText: "继续",
                                               extraButtonText: "",
                                               showExtraButton: false,
                                               showYesButton: true,
                                               showNoButton: true,
                                               icon: PackIconKind.ClockAlert
                                             );
                return false;
            }
            // 处理数据
            await ProcessVisionResult(processingComplete);
            return true;
        }

        /// <summary>
        /// 处理视觉结果数据
        /// </summary>
        private async Task ProcessVisionResult(string visionData)
        {
            try
            {
                // 数据格式 "Camera=3DCAMERA;VISION_RESULT:SUCCESS:14.164,10.713,9.399,11.682,13.871,11.75,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0"
                // 解析数据
                int colonPos = visionData.LastIndexOf(':');
                if (colonPos == -1)
                {
                    _logger.Error("视觉数据格式错误：找不到坐标起始冒号");
                    return;
                }
                string coordSection = visionData.Substring(colonPos + 1); // "14.164,10.713,9.399,...."

                string[] dataParts = coordSection
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (dataParts.Length < 6)
                {
                    _logger.Error($"视觉数据格式错误，期望至少6个数据，实际得到{dataParts.Length}");
                    return;
                }

                // 将字符串数据转换为double
                double[] realTimeHeights = new double[6];
                for (int i = 0; i < 6; i++)
                {
                    if (double.TryParse(dataParts[i], out double height))
                    {
                        realTimeHeights[i] = height;
                    }
                    else
                    {
                        _logger.Error($"解析第{i + 1}个高度数据失败: {dataParts[i]}");
                        realTimeHeights[i] = 0;
                    }
                }

                // 获取当前组装位置对应的实时高度
                double actualPlaneZ = 0;
                if (_currentDispensingPosition >= 1 && _currentDispensingPosition <= 6)
                {
                    // _currentDispensingPosition从1开始，需要减1转换为0-based索引
                    actualPlaneZ = realTimeHeights[_currentDispensingPosition - 1];
                    _logger.Info($"位置{_currentDispensingPosition}的实时高度: {actualPlaneZ:F3} mm");
                }
                else
                {
                    _logger.Error($"无效的组装位置: {_currentDispensingPosition}");
                    return;
                }

                // 获取基准面高度 
                string _customDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                        "Config",
                                        "Calibration");
                var parameters = _parameterStorage?.Load<NeedleCalibrationParameters>(
                    "NeedleCalibration",
                    _customDirectory
                );
                double deltaZ = 0;
                if (parameters != null)
                {
                    // 计算基准面高度的变化量
                    deltaZ = actualPlaneZ - parameters.BasePlaneZ;

                    // 同步调整针尖高度：基准面加多少，针尖就加多少；基准面减多少，针尖就减多少
                    needleTipZ = parameters.NeedleTipZ +  -deltaZ;

                    _logger.Info($"基准面变化量: {deltaZ:F3} mm, 新针尖高度: {needleTipZ:F3} mm");

                    // 可以选择是否保存调整后的参数
                    // _parameterStorage?.Save("NeedleCalibration", updatedParameters, _customDirectory);
                }
                else
                {
                    _logger.Error("未找到校准参数");
                }

                // 给组装Z轴加补偿
                if (Math.Abs(deltaZ) > 0.01) // 只在实际变化量超过0.01mm时才添加补偿
                {
                    _compensationService.UpdateCompensation(_currentDispensingPosition, CompensationType.PressZ,
                               new CompensationData
                               {
                                   CompensationZ = deltaZ,
                                   Source = "DispenserStation"
                               });
                    _logger.Info($"为位置{_currentDispensingPosition}添加Z轴补偿: {deltaZ:F3} mm");
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error($"处理视觉结果异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 从扫描结果更新点胶路径
        /// </summary>
        private async Task<bool> UpdateDispensingPathFromScanResultAsync()
        {
            try
            {
                // 从视觉系统获取校正后的点胶路径
                // List<PointF> correctedPath = VisionSystem.GetCorrectedDispensingPath();

                // 暂时使用模拟数据
                List<PointF> correctedPath = _dispensingPath; // 使用原始路径

                if (correctedPath != null && correctedPath.Count > 0)
                {
                    _dispensingPath = correctedPath;
                    _logger.Info($"从扫描结果更新点胶路径，新路径点数量: {_dispensingPath.Count}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "从扫描结果更新点胶路径异常");
                return false;
            }
        }

        /// <summary>
        /// 获取轴当前位置
        /// </summary>
        private double GetAxisCurrentPosition(int axisId)
        {
            try
            {
                // 从运动控制器读取当前位置
                double position = 0.0;
                LTDMC.dmc_get_position_unit(0, (ushort)axisId, ref position);
                return position;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"获取轴{axisId}当前位置异常");
                return 0.0;
            }
        }

        /// <summary>
        /// 检查胶头堵塞
        /// </summary>
        private async Task<bool> CheckGunCloggingAsync()
        {
            try
            {
                _logger.Debug("检查胶头堵塞...");

                // 通过负压传感器检测胶头是否堵塞
                // double vacuumValue = VacuumSensor.GetCurrentValue();
                double vacuumValue = -0.2; // 模拟值

                double threshold = -0.1; // 堵塞阈值
                if (vacuumValue > threshold)
                {
                    _logger.Error($"胶头可能堵塞，当前负压值: {vacuumValue}MPa");
                    return false;
                }

                _logger.Debug("胶头堵塞检查通过");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "检查胶头堵塞异常");
                return false;
            }
        }

        /// <summary>
        /// 移动到XY位置
        /// </summary>
        private async Task<bool> MoveToXYPositionAsync(PointF point, double speed = 0)
        {
            if (speed == 0)
            {
                speed = ((DispenserStationParams)Parameters).DispensingMoveSpeed;
            }

            _logger.Debug($"移动到XY位置: X={point.X:F3}, Y={point.Y:F3}, 速度={speed}");

            int[] axisIds = { DispX.ActId, DispY_1.ActId };
            double[] positions = { point.X, point.Y };
            double[] speeds = { speed, speed };

            MoveAbs(axisIds, positions, speeds); // ← 立即返回，不阻塞

            // 异步等待所有轴到位
            return await WaitForMultiAxisMoveCompletionAsync(
                new[] { DispX, DispY_1 },
                "移动到XY位置");
        }

        /// <summary>
        /// Z轴下降到点胶高度
        /// </summary>
        private async Task<bool> MoveZAxesToDispensingHeightAsync()
        {
            try
            {
                _logger.Debug("Z轴下降到点胶高度...");

                var zAxes = new[] { DispZ3 };
                return await MoveMultipleAxesToPositionsAsync(zAxes, "点胶高度", "Z轴下降");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Z轴下降到点胶高度异常");
                return false;
            }
        }

        /// <summary>
        /// Z轴抬升到安全高度
        /// </summary>
        private async Task<bool> MoveZAxesToSafeHeightAsync()
        {
            try
            {
                _logger.Debug("Z轴抬升到安全高度...");

                var zAxes = new[] { DispZ1, DispZ2, DispZ3 };
                return await MoveMultipleAxesToPositionsAsync(zAxes, "待机位", "Z轴抬升");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Z轴抬升到安全高度异常");
                return false;
            }
        }

        /// <summary>
        /// 执行单次点胶
        /// </summary>
        private async Task<bool> PerformSingleDispensingAsync(double dispensingTime)
        {
            try
            {
                _logger.Debug($"执行单次点胶，时间: {dispensingTime}s");

                // 打开胶阀
                m_ShotGlueSolenoid.SetDo(1);

                // 等待点胶时间
                await Task.Delay((int)(dispensingTime * 1000));

                // 关闭胶阀
                m_ShotGlueSolenoid.SetDo(0);

                _logger.Debug("单次点胶完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "执行单次点胶异常");
                return false;
            }
        }

        /// <summary>
        /// 执行负压回吸
        /// </summary>
        private async Task PerformVacuumRetractionAsync(double retractionTime)
        {
            try
            {
                _logger.Debug($"执行负压回吸，时间: {retractionTime}s");

                // 打开负压回吸阀
                // VacuumRetractionValve.SetDo(1);

                await Task.Delay((int)(retractionTime * 1000));

                // 关闭负压回吸阀
                // VacuumRetractionValve.SetDo(0);

                _logger.Debug("负压回吸完成");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "执行负压回吸异常");
            }
        }

        /// <summary>
        /// 移动到清理位置
        /// </summary>
        private async Task<bool> MoveToCleaningPositionAsync()
        {
            try
            {
                _logger.Debug("移动到清理位置...");

                // 移动到专门的清理位置
                return await MoveAxisToPositionAsync(DispX, "清理位置",
                    _axisConfigService.GetAxisSpeed(0, DispX.ActId));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "移动到清理位置异常");
                return false;
            }
        }

        /// <summary>
        /// 执行吹气清理
        /// </summary>
        private async Task PerformAirBlowCleaningAsync(double cleaningTime)
        {
            try
            {
                _logger.Debug($"执行吹气清理，时间: {cleaningTime}s");

                // 打开吹气阀
                // AirBlowValve.SetDo(1);

                await Task.Delay((int)(cleaningTime * 1000));

                // 关闭吹气阀
                // AirBlowValve.SetDo(0);

                _logger.Debug("吹气清理完成");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "执行吹气清理异常");
            }
        }

        /// <summary>
        /// 获取当前点胶状态
        /// </summary>
        public string GetCurrentDispensingStatus()
        {
            return _currentDispensingState.ToString();
        }

        /// <summary>
        /// 获取点胶进度
        /// </summary>
        public double GetDispensingProgress()
        {
            if (_dispensingPath.Count == 0) return 0;

            return (_currentDispensingPosition * 100.0) / _dispensingPath.Count;
        }
        #endregion

        #region 点胶功能测试
        /// <summary>
        /// DispX轴移动一段距离,起始点处打开胶阀，移动到终点处关闭胶阀。胶阀打开要在轴运动时打开，胶阀关闭要在轴运动停止之前关闭。
        /// </summary>
        public async Task TestDispensingFunction1Async()
        {

        }
        /// <summary>
        /// 控制DispX轴和DispY_1轴移动行列阵列(行列可设置)，在行列阵列的每个点上执行DispensingFunction。
        /// </summary>
        public async Task TestDispensingFunction2Async()
        {

        }
        #endregion

        #region 擦胶功能
        // 擦胶功能相关字段
        private bool _isWiping = false;
        private CancellationTokenSource _wipingCancellationTokenSource;

        private async Task<bool> FirstCleanGlueAsync()
        {
            try
            {
                _logger.Info("开始清理胶头...");

                var stationParams = (DispenserStationParams)Parameters;

                // Z轴抬升到安全高度
                if (!await MoveZAxesToSafeHeightAsync())
                {
                    _logger.Warn("Z轴抬升到安全高度失败");
                }

                // 移动到清理位置
                if (!await MoveToCleaningPositionAsync())
                {
                    _logger.Warn("移动到清理位置失败");
                }

                _logger.Info("胶头清理完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "清理胶头异常");
                return false;
            }
        }

        /// <summary>
        /// 执行擦胶流程
        /// </summary>
        public async Task<bool> PerformWipingAsync()
        {
            try
            {
                _isWiping = true;
                _logger.Info("【点胶工站】开始擦胶流程");

                // 0. Z1/Z2/Z3轴移到安全高度
                if (!await MoveZAxesToSafeHeightAsync())
                {
                    _logger.Error("Z轴移动到安全高度失败");
                    return false;
                }

                // 1. XY轴移到擦胶位置
                if (!await MoveToWipingPositionAsync())
                {
                    _logger.Error("移动到擦胶位置失败");
                    return false;
                }

                // 2. 打开擦胶阀
                await OpenWipingValveAsync();

                // 3. Z3轴下降到擦胶高度
                if (!await MoveZ3ToWipingHeightAsync())
                {
                    _logger.Error("Z3轴下降到擦胶高度失败");
                    await CloseWipingValveAsync();
                    return false;
                }

                // 4. 关闭擦胶阀，延时擦胶时间后打开擦胶阀，Z3轴抬起到安全高度
                if (!await PerformWipingActionAsync())
                {
                    _logger.Error("擦胶动作执行失败");
                    return false;
                }

                // 5. 擦胶电机启动
                await StartWipingMotorAsync();

                _logger.Info("【点胶工站】擦胶流程完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "擦胶流程异常");
                return false;
            }
            finally
            {
                _isWiping = false;
            }
        }

        /// <summary>
        /// 停止擦胶流程
        /// </summary>
        public void StopWiping()
        {
            _wipingCancellationTokenSource?.Cancel();
            _isWiping = false;

            // 立即停止所有动作
            CloseWipingValveAsync();
            StopWipingMotorAsync();
            MoveStop(); // 停止所有轴运动
        }

        /// <summary>
        /// 移动到擦胶位置
        /// </summary>
        private async Task<bool> MoveToWipingPositionAsync()
        {
            try
            {
                _logger.Info("移动到擦胶位置...");

                // XY轴移动到擦胶位置
                var xyAxes = new[] { DispX, DispY_1 };
                return await MoveMultipleAxesToPositionsAsync(xyAxes, "擦胶位", "移动到擦胶位置");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "移动到擦胶位置异常");
                return false;
            }
        }

        /// <summary>
        /// Z3轴下降到擦胶高度
        /// </summary>
        private async Task<bool> MoveZ3ToWipingHeightAsync()
        {
            try
            {
                _logger.Info("Z3轴下降到擦胶高度...");
                return await MoveAxisToPositionAsync(DispZ3, "擦胶位",
                    _axisConfigService.GetAxisSpeed(0, DispZ3.ActId));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Z3轴下降到擦胶高度异常");
                return false;
            }
        }

        /// <summary>
        /// 打开擦胶阀
        /// </summary>
        private async Task OpenWipingValveAsync()
        {
            try
            {
                _logger.Debug("打开擦胶阀");
                m_WipeGlueValve.SetDo(0);
                m_WipeMotorReset.SetDo(1);
                await Task.Delay(100);
                m_WipeMotorReset.SetDo(0);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "打开擦胶阀异常");
                throw;
            }
        }

        /// <summary>
        /// 关闭擦胶阀
        /// </summary>
        private async Task CloseWipingValveAsync()
        {
            try
            {
                _logger.Debug("关闭擦胶阀");
                m_WipeGlueValve.SetDo(1);
                await Task.Delay(2000);
                // Z3轴抬起到安全高度
                if (!await MoveZ3ToSafeHeightAsync())
                {
                    _logger.Warn("Z3轴抬起到安全高度失败");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "关闭擦胶阀异常");
                throw;
            }
        }

        /// <summary>
        /// 执行擦胶动作
        /// </summary>
        private async Task<bool> PerformWipingActionAsync()
        {
            try
            {
                var stationParams = (DispenserStationParams)Parameters;
                double wipeTime = stationParams.CleaningTime; // 擦胶时间参数

                _logger.Info($"执行擦胶动作，时间: {wipeTime}ms");

                // 关闭擦胶阀
                await CloseWipingValveAsync();

                // 延时擦胶时间
                await Task.Delay((int)wipeTime);

                // 打开擦胶阀
                await OpenWipingValveAsync();

                _logger.Info("擦胶动作完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "执行擦胶动作异常");
                return false;
            }
        }

        /// <summary>
        /// Z3轴抬起到安全高度
        /// </summary>
        private async Task<bool> MoveZ3ToSafeHeightAsync()
        {
            try
            {
                _logger.Debug("Z3轴抬起到安全高度...");
                return await MoveAxisToPositionAsync(DispZ3, "待机位",
                    _axisConfigService.GetAxisSpeed(0, DispZ3.ActId));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Z3轴抬起到安全高度异常");
                return false;
            }
        }

        /// <summary>
        /// 启动擦胶电机
        /// </summary>
        private async Task StartWipingMotorAsync()
        {
            try
            {
                _logger.Debug("启动擦胶电机");

                var stationParams = (DispenserStationParams)Parameters;
                //double motorRunTime = stationParams.WipeMotorRunTime; // 电机运行时间

                // 启动擦胶电机
                m_WipeMotor.SetDo(1);

                // 运行指定时间后停止
                //if (motorRunTime > 0)
                //{
                //    await Task.Delay((int)motorRunTime);
                //    await StopWipingMotorAsync();
                //}
                await Task.Delay(100);

                m_WipeMotor.SetDo(0);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "启动擦胶电机异常");
                throw;
            }
        }

        /// <summary>
        /// 停止擦胶电机
        /// </summary>
        private async Task StopWipingMotorAsync()
        {
            try
            {
                _logger.Debug("停止擦胶电机");
                m_WipeMotor.SetDo(0);
                await Task.Delay(10);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "停止擦胶电机异常");
                throw;
            }
        }

        /// <summary>
        /// 获取擦胶状态
        /// </summary>
        public bool GetWipingStatus()
        {
            return _isWiping;
        }
        #endregion

        #region 点阵点胶功能
        private bool _isDotArrayDispensing = false;
        private CancellationTokenSource _dotArrayCancellationTokenSource;

        /// <summary>
        /// 执行点阵点胶
        /// </summary>
        public async Task<bool> PerformDotArrayDispensingAsync(
            double startX,
            double startY,
            int rows,
            int columns,
            double rowSpacing,
            double columnSpacing,
            double dispensingTimeMs,
            double moveSpeed)
        {
            if (_isDotArrayDispensing)
            {
                _logger.Warn("点阵点胶已在执行中");
                return false;
            }

            try
            {
                _isDotArrayDispensing = true;
                _dotArrayCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _dotArrayCancellationTokenSource.Token;

                _logger.Info($"开始点阵点胶: 起始点({startX:F3}, {startY:F3}), {rows}行{columns}列, " +
                           $"行距{rowSpacing:F3}mm, 列距{columnSpacing:F3}mm, 点胶时间{dispensingTimeMs}ms");

                // 1. 移动到安全高度
                if (!await MoveZAxesToSafeHeightAsync())
                {
                    _logger.Error("移动到安全高度失败");
                    return false;
                }

                // 2. 移动到起始点
                if (!await MoveToXYPositionAsync(new PointF((float)startX, (float)startY), moveSpeed))
                {
                    _logger.Error("移动到起始点失败");
                    return false;
                }

                // 3. 执行点阵点胶（包含Z轴升降）
                bool success = await ExecuteDotArrayDispensingWithZLiftAsync(
                    startX, startY, rows, columns, rowSpacing, columnSpacing,
                    dispensingTimeMs, moveSpeed, cancellationToken);

                // 4. 最终抬升到安全高度
                if (!await MoveZAxesToSafeHeightAsync())
                {
                    _logger.Warn("最终抬升到安全高度失败");
                }

                _logger.Info($"点阵点胶{(success ? "完成" : "失败")}");
                return success;
            }
            catch (OperationCanceledException)
            {
                _logger.Info("点阵点胶被用户取消");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "点阵点胶异常");
                return false;
            }
            finally
            {
                _isDotArrayDispensing = false;
                _dotArrayCancellationTokenSource?.Dispose();
                _dotArrayCancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 执行带Z轴抬升的点阵点胶循环
        /// </summary>
        private async Task<bool> ExecuteDotArrayDispensingWithZLiftAsync(
            double startX, double startY, int rows, int columns,
            double rowSpacing, double columnSpacing, double dispensingTimeMs,
            double moveSpeed, CancellationToken cancellationToken)
        {
            try
            {
                double liftHeight = 3.0; // Z轴抬升高度3mm

                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < columns; col++)
                    {
                        // 检查取消请求
                        cancellationToken.ThrowIfCancellationRequested();

                        double targetX = startX + col * columnSpacing;
                        double targetY = startY + row * rowSpacing;
                        int currentPoint = row * columns + col + 1;
                        int totalPoints = rows * columns;

                        _logger.Info($"点阵点胶第 {currentPoint}/{totalPoints} 点: " +
                                   $"({targetX:F3}, {targetY:F3})");

                        // 如果是第一个点，需要先下降到点胶高度
                        if (currentPoint == 1)
                        {
                            if (!await MoveZAxesToDispensingHeightAsync())
                            {
                                _logger.Error("Z轴下降到点胶高度失败");
                                return false;
                            }
                        }

                        // 执行单点点胶
                        if (!await DispenseAtSinglePointAsync(
                            targetX, targetY, dispensingTimeMs, cancellationToken))
                        {
                            _logger.Error($"点 ({targetX:F3}, {targetY:F3}) 点胶失败");
                            return false;
                        }

                        // 如果不是最后一个点，抬升Z轴并移动到下个点
                        if (currentPoint < totalPoints)
                        {
                            // 抬升Z轴3mm
                            if (!await MoveZAxesRelativeAsync(liftHeight))
                            {
                                _logger.Error("Z轴抬升失败");
                                return false;
                            }

                            // 计算下一个点坐标
                            double nextX, nextY;
                            if (col < columns - 1)
                            {
                                nextX = startX + (col + 1) * columnSpacing;
                                nextY = startY + row * rowSpacing;
                            }
                            else
                            {
                                nextX = startX;
                                nextY = startY + (row + 1) * rowSpacing;
                            }

                            // 移动到下一个点（在安全高度）
                            if (!await MoveToXYPositionAsync(new PointF((float)nextX, (float)nextY), moveSpeed))
                            {
                                _logger.Error($"移动到下一个点 ({nextX:F3}, {nextY:F3}) 失败");
                                return false;
                            }

                            // 下降到点胶高度
                            if (!await MoveZAxesToDispensingHeightAsync())
                            {
                                _logger.Error("Z轴下降到点胶高度失败");
                                return false;
                            }
                        }

                        // 点胶间隔
                        await Task.Delay(100, cancellationToken);
                    }
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "点阵点胶循环异常");
                return false;
            }
        }

        /// <summary>
        /// 在单个点执行点胶
        /// </summary>
        private async Task<bool> DispenseAtSinglePointAsync(
            double targetX, double targetY, double dispensingTimeMs,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.Debug($"在点 ({targetX:F3}, {targetY:F3}) 开始点胶，时间: {dispensingTimeMs}ms");

                // 确保在正确的位置（二次确认）
                if (!await MoveToXYPositionAsync(new PointF((float)targetX, (float)targetY), 10.0))
                {
                    _logger.Warn($"位置微调失败，但继续点胶");
                }

                // 开启胶阀
                m_ShotGlueSolenoid.SetDo(1);

                // 等待设定的点胶时间
                await Task.Delay((int)dispensingTimeMs, cancellationToken);

                // 关闭胶阀
                m_ShotGlueSolenoid.SetDo(0);

                _logger.Debug($"点 ({targetX:F3}, {targetY:F3}) 点胶完成");
                return true;
            }
            catch (OperationCanceledException)
            {
                m_ShotGlueSolenoid.SetDo(0);
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"单点点胶异常 at ({targetX:F3}, {targetY:F3})");
                m_ShotGlueSolenoid.SetDo(0);
                return false;
            }
        }

        /// <summary>
        /// Z轴相对移动
        /// </summary>
        private async Task<bool> MoveZAxesRelativeAsync(double delta)
        {
            try
            {
                _logger.Debug($"Z轴相对移动: {delta:F3}mm");

                // 获取当前Z轴位置
                double currentZ3 = GetAxisPosition(DispZ3.ActId);

                // 计算目标位置
                double targetZ3 = currentZ3 - delta;

                // 执行相对移动
                int[] axisIds = { DispZ3.ActId };
                double[] positions = { targetZ3 };
                double[] speeds = { 10.0 }; // 较慢的安全速度

                MoveAbs(axisIds, positions, speeds);

                // 等待移动完成
                return await WaitForMultiAxisMoveCompletionAsync(new[] { DispZ3 }, "Z轴相对移动");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Z轴相对移动异常");
                return false;
            }
        }

        /// <summary>
        /// 停止点阵点胶
        /// </summary>
        public void StopDotArrayDispensing()
        {
            _dotArrayCancellationTokenSource?.Cancel();
            m_ShotGlueSolenoid.SetDo(0); // 确保胶阀关闭

            // 尝试停止所有轴运动
            try
            {
                MoveStop();
            }
            catch (Exception ex)
            {
                _logger.Warn($"停止轴运动时出现异常{ex.Message}");
            }

            _logger.Info("点阵点胶已停止");
        }

        /// <summary>
        /// 移动到点阵起始点
        /// </summary>
        public async Task<bool> MoveToDotArrayStartAsync(double startX, double startY, double moveSpeed)
        {
            try
            {
                _logger.Info($"移动到点阵起始点: ({startX:F3}, {startY:F3})");

                // 先移动到安全高度
                if (!await MoveZAxesToSafeHeightAsync())
                {
                    _logger.Warn("移动到安全高度失败");
                }

                // 移动到起始点
                return await MoveToXYPositionAsync(new PointF((float)startX, (float)startY), moveSpeed);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "移动到点阵起始点异常");
                return false;
            }
        }

        /// <summary>
        /// 获取点阵点胶状态
        /// </summary>
        public bool GetDotArrayDispensingStatus()
        {
            return _isDotArrayDispensing;
        }
        #endregion

        #region 拍照位置控制方法

        /// <summary>
        /// 移动到Tab拍照位置
        /// </summary>
        public async Task<bool> MoveToTabPhotoPositionAsync(int index)
        {
            try
            {
                _logger.Info($"移动到Tab{index}拍照位置...");

                // 根据索引设置当前拍照组
                _currentPhotoGroup = index;
                _currentPhotoPosition = 1; // Tab拍照

                // 调用现有的移动方法
                await MoveToGroupPhotoPosition();

                _logger.Info($"已移动到Tab{index}拍照位置");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"移动到Tab{index}拍照位置异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 移动到Pillar1拍照位置
        /// </summary>
        public async Task<bool> MoveToPillar1PhotoPositionAsync(int index)
        {
            try
            {
                _logger.Info($"移动到Pillar{index}-1拍照位置...");

                _currentPhotoGroup = index;
                _currentPhotoPosition = 2; // Pillar1拍照

                await MoveToGroupPhotoPosition();

                _logger.Info($"已移动到Pillar{index}-1拍照位置");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"移动到Pillar{index}-1拍照位置异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 移动到Pillar2拍照位置
        /// </summary>
        public async Task<bool> MoveToPillar2PhotoPositionAsync(int index)
        {
            try
            {
                _logger.Info($"移动到Pillar{index}-2拍照位置...");

                _currentPhotoGroup = index;
                _currentPhotoPosition = 3; // Pillar2拍照

                await MoveToGroupPhotoPosition();

                _logger.Info($"已移动到Pillar{index}-2拍照位置");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"移动到Pillar{index}-2拍照位置异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 执行Tab拍照
        /// </summary>
        public async Task TakeTabPhotoAsync(int index)
        {
            try
            {
                _logger.Info($"执行Tab{index}拍照...");

                _currentPhotoGroup = index;
                _currentPhotoPosition = 1;

                //await FirstCleanGlueAsync();
                //await WaitForGroupPhotoComplete();

                _logger.Info($"Tab{index}拍照完成");
            }
            catch (Exception ex)
            {
                _logger.Error($"Tab{index}拍照异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 执行Pillar1拍照
        /// </summary>
        public async Task TakePillar1PhotoAsync(int index)
        {
            try
            {
                _logger.Info($"执行Pillar{index}-1拍照...");

                _currentPhotoGroup = index;
                _currentPhotoPosition = 2;

                await FirstCleanGlueAsync();
                //await WaitForGroupPhotoComplete();

                _logger.Info($"Pillar{index}-1拍照完成");
            }
            catch (Exception ex)
            {
                _logger.Error($"Pillar{index}-1拍照异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行Pillar2拍照
        /// </summary>
        public async Task TakePillar2PhotoAsync(int index)
        {
            try
            {
                _logger.Info($"执行Pillar{index}-2拍照...");

                _currentPhotoGroup = index;
                _currentPhotoPosition = 3;

                await FirstCleanGlueAsync();
                //await WaitForGroupPhotoComplete();

                _logger.Info($"Pillar{index}-2拍照完成");
            }
            catch (Exception ex)
            {
                _logger.Error($"Pillar{index}-2拍照异常: {ex.Message}");
                throw;
            }
        }
        #endregion

        #region 针头点胶相关方法

        /// <summary>
        /// 移动到针头位置
        /// </summary>
        public async Task<bool> MoveToTargetPositionAsync(double x, double y, double speed)
        {
            try
            {
                _logger.Info($"移动到针头位置: X={x:F3}, Y={y:F3}, 速度={speed}");

                // 使用现有的XY轴移动方法
                int[] axisIds = { DispX.ActId, DispY_1.ActId };
                double[] positions = { x, y };
                double[] speeds = { speed, speed };

                MoveAbs(axisIds, positions, speeds);

                return await WaitForMoveCompletionAsync(DispX, "移动到针头位置");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"移动到针头位置异常: {x},{y}");
                return false;
            }
        }

        /// <summary>
        /// 移动到点胶高度
        /// </summary>
        public async Task<bool> MoveToDispensingHeightAsync(double height)
        {
            try
            {
                _logger.Info($"移动到点胶高度: {height:F3}mm");

                // 使用绝对位置移动Z2轴
                double velocity = _axisConfigService.GetAxisSpeed(0, DispZ2.ActId);
                MoveAbs(DispZ2.ActId, height - 3, 15);

                await WaitForMoveCompletionAsync(DispZ2, "移动到点胶高度上方");

                MoveAbs(DispZ2.ActId, height, 1);

                return await WaitForMoveCompletionAsync(DispZ2, "移动到点胶高度");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"移动到点胶高度异常: {height}");
                return false;
            }
        }


        /// <summary>
        /// 停止所有运动
        /// </summary>
        public void StopAllMotion()
        {
            try
            {
                _logger.Info("停止所有轴运动");

                // 停止所有轴
                MoveStop();

                // 关闭胶阀
                m_ShotGlueSolenoid.SetDo(0);

                _logger.Info("所有运动已停止");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "停止运动异常");
            }
        }

        #endregion
    }
}

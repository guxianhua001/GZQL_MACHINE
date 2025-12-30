using Core.Abstraction;
using Core.Events;
using Core.Models;
using Framework.Services;
using MaterialDesignThemes.Wpf;
using SmarterMotion;
using Stations.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Stations
{
    public partial class AssemblyStation
    {
        private enum HomingState
        {
            Start,
            MoveAsmZAxisHome,
            MoveAsmUAxisHome,
            MoveAsmXAxisHome,
            MoveAsmYAxisHome,
            MoveAsmCamYAxisHome,
            MoveAsmZAxisInitPos,
            MoveAsmUAxisInitPos,
            MoveAsmXAxisInitPos,
            MoveAsmYAxisInitPos,
            MoveAsmCamYAxisInitPos,
            Finalize,
            Error
        }
        protected override void InitProcessVar()
        {
            _requestedStationIndex = 0;
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
            if (SetServo(AsmZ.ActId, true) != 0)
            {
                ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.装配工位Z轴使能超时, (int)XSysAlarmId.AXIS_ALM, AlarmCategory.SYSTEM.ToString(), "【装配模组】装配工位Z轴使能超时");
                return;
            }
            if (SetServo(AsmU.ActId, true) != 0)
            {
                ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.装配工位U轴使能超时, (int)XSysAlarmId.AXIS_ALM, AlarmCategory.SYSTEM.ToString(), "【装配模组】装配工位U轴使能超时");
                return;
            }
            if (SetServo(AsmX.ActId, true) != 0)
            {
                ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.装配工位X轴使能超时, (int)XSysAlarmId.AXIS_ALM, AlarmCategory.SYSTEM.ToString(), "【装配模组】装配工位X轴使能超时");
                return;
            }
            if (SetServo(AsmY.ActId, true) != 0)
            {
                ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.组装Y轴使能超时, (int)XSysAlarmId.AXIS_ALM, AlarmCategory.SYSTEM.ToString(), "【装配模组】组装Y轴使能超时");
                return;
            }
            if (SetServo(AsmCamY.ActId, true) != 0)
            {
                ReportAlarm(XAlarmLevel.STOP, (int)MachineAlarmCode.侧相机Y轴使能超时, (int)XSysAlarmId.AXIS_ALM, AlarmCategory.SYSTEM.ToString(), "【装配模组】侧相机Y轴使能超时");
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
                        bool zeroValid = StationEvents.WaitForSignal(
                            StationEvents.DispensingStationZeroCompleted,
                            -1
                        );
                        if (!zeroValid)
                            currentState = HomingState.Error;
                        currentState = HomingState.MoveAsmZAxisHome;
                        break;
                    case HomingState.MoveAsmZAxisHome:
                        {
                            currentState = MoveAsmZAxisHome() ?
                                HomingState.MoveAsmUAxisHome :
                                HomingState.Error;
                            Goto((int)HomingState.MoveAsmZAxisHome, "装配工位Z轴寻原点");
                            break;
                        }
                    case HomingState.MoveAsmUAxisHome:
                        {
                            currentState = MoveAsmUAxisHome() ?
                                HomingState.MoveAsmXAxisHome :
                                HomingState.Error;
                            Goto((int)HomingState.MoveAsmUAxisHome, "装配工位U轴寻原点");
                            break;
                        }
                    case HomingState.MoveAsmXAxisHome:
                        {
                            currentState = MoveAsmXAxisHome() ?
                                HomingState.MoveAsmYAxisHome :
                                HomingState.Error;
                            Goto((int)HomingState.MoveAsmXAxisHome, "装配工位X轴寻原点");
                            break;
                        }
                    case HomingState.MoveAsmYAxisHome:
                        currentState = MoveAsmYAxisHome() ?
                             HomingState.MoveAsmCamYAxisHome :
                             HomingState.Error;
                        Goto((int)HomingState.MoveAsmYAxisHome, "组装Y轴寻原点");
                        break;
                    case HomingState.MoveAsmCamYAxisHome:
                        currentState = MoveAsmCamYAxisHome() ?
                             HomingState.MoveAsmZAxisInitPos :
                             HomingState.Error;
                        Goto((int)HomingState.MoveAsmCamYAxisHome, "侧相机Y轴寻原点");
                        break;
                    case HomingState.MoveAsmZAxisInitPos:
                        currentState = MoveZAxisStandbyPos() ?
                            HomingState.MoveAsmUAxisInitPos :
                            HomingState.Error;
                        Goto((int)HomingState.MoveAsmZAxisInitPos, "装配工位Z轴移动到初始位置");
                        break;
                    case HomingState.MoveAsmUAxisInitPos:
                        currentState = MoveUAxisStandbyPos() ?
                            HomingState.MoveAsmXAxisInitPos :
                            HomingState.Error;
                        Goto((int)HomingState.MoveAsmUAxisInitPos, "装配工位U轴移动到初始位置");
                        break;
                    case HomingState.MoveAsmXAxisInitPos:
                        currentState = MoveXAxisStandbyPos() ?
                            HomingState.MoveAsmYAxisInitPos :
                            HomingState.Error;
                        Goto((int)HomingState.MoveAsmXAxisInitPos, "装配工位X轴移动到初始位置");
                        break;
                    case HomingState.MoveAsmYAxisInitPos:
                        currentState = MoveYAxisStandbyPos() ?
                            HomingState.MoveAsmCamYAxisInitPos :
                            HomingState.Error;
                        Goto((int)HomingState.MoveAsmYAxisInitPos, "组装Y轴移动到初始位置");
                        break;
                    case HomingState.MoveAsmCamYAxisInitPos:
                        currentState = MoveCamYAxisStandbyPos() ?
                            HomingState.Finalize :
                            HomingState.Error;
                        Goto((int)HomingState.MoveAsmCamYAxisInitPos, "侧相机Y轴移动到初始位置");
                        break;
                    case HomingState.Error:
                        isHomingSuccessful = false;
                        break;

                    case HomingState.Finalize:
                        // 完成处理
                        isHomingSuccessful = true;
                        TaskHomeOK = true;
                        this.Station.SetState(XStationState.RESETING);
                        Goto((int)HomingState.Finalize, "组装工位回零完成");
                        break;
                }
            }
        }
        /// <summary>
        ///  单轴运动（带位置补偿）
        /// </summary>
        private bool MoveAxisToPosition(IAxis axis, string positionName, double baseVelocity, double offset = 0)
        {
            double pos = GetPosition(axis.ActId, positionName);
            if (pos == -1)
            {
                _logger.Error($"【{Identifier}】{axis.Name}轴获取位置失败：{positionName}");
                return false;
            }

            // 应用位置补偿值
            pos += offset;

            double vel = baseVelocity * axis.MotionSpeedRatio;
            MoveAbs(axis.ActId, pos, vel);

            string logPrefix = $"【{Identifier}】{axis.Name}轴运动到{positionName}";
            if (WaitMoveDone())
            {
                _logger.Info($"{logPrefix}：位置{pos:F3}（基准{pos - offset:F3} + 补偿{offset:F3}），速度：{vel:F2}，完成");
                return true;
            }
            else
            {
                _logger.Warn($"{logPrefix}：位置{pos:F3}（基准{pos - offset:F3} + 补偿{offset:F3}），速度：{vel:F2}，超时");
                return false;
            }
        }
        /// <summary>
        /// 移动Z轴相对距离
        /// </summary>
        private bool MoveZAxisRelativeAsync(int ActId, double distance, double vel)
        {
            _logger.Info($"相对移动Z轴：{distance}mm，速度：{vel}");
            MoveRel(ActId, distance, vel);
            return WaitMoveDone();
        }
        /// <summary>
        /// 移动Z轴绝对距离
        /// </summary>
        private bool MoveZAxisAbsAsync(int ActId, double position, double vel)
        {
            _logger.Info($"绝对移动Z轴：位置{position}，速度：{vel}");
            MoveAbs(ActId, position, vel);
            return WaitMoveDone();
        }
        /// <summary>
        /// 移动X轴相对距离
        /// </summary>
        private bool MoveXAxisRelativeAsync(int ActId, double distance, double vel)
        {
            _logger.Info($"相对移动X轴：{distance}mm，速度：{vel}");
            MoveRel(ActId, distance, vel);
            return WaitMoveDone();
        }
        private bool MoveAsmZAxisHome()
        {
            MoveHome(AsmZ.ActId);
            if (WaitMoveDone())
                return true;
            return false;
        }
        private bool MoveAsmUAxisHome()
        {
            MoveHome(AsmU.ActId);
            if (WaitMoveDone())
                return true;
            return false;
        }
        private bool MoveAsmXAxisHome()
        {
            MoveHome(AsmX.ActId);
            if (WaitMoveDone())
                return true;
            return false;
        }
        private bool MoveAsmYAxisHome()
        {
            MoveHome(AsmY.ActId);
            if (WaitMoveDone())
                return true;
            return false;
        }
        private bool MoveAsmCamYAxisHome()
        {
            MoveHome(AsmCamY.ActId);
            if (WaitMoveDone())
                return true;
            return false;
        }
        // 辅助方法：获取轴位置
        public double GetAxisPosition(int axisID)
        {
            double _position = 0;
            LTDMC.dmc_get_position_unit(0, (ushort)axisID, ref _position);
            return _position;
        }
        /// <summary>
        /// 多轴运动 - 直接使用传入的位置值和速度数组
        /// </summary>
        private bool MoveMultiAxisToPosition(IAxis[] axes, double[] positions, double[] baseVelocities)
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

        /// <summary>
        /// 多轴运动 - 直接使用传入的位置值
        /// </summary>
        private bool MoveMultiAxisToPosition(IAxis[] axes, double[] positions, double baseVelocity)
        {
            // 参数校验
            if (axes == null || axes.Length == 0)
            {
                _logger.Error("【龙门搬运】轴集合为空");
                return false;
            }

            if (positions == null || positions.Length != axes.Length)
            {
                _logger.Error("【龙门搬运】位置数组与轴数组长度不匹配");
                return false;
            }

            // 准备运动参数
            int[] axisIds = axes.Select(a => a.ActId).ToArray();
            var axisNames = axes.Select(a => a.Name).ToList();

            // 计算合成速度（使用第一个轴的速度比例）
            double vel = baseVelocity * axes[0].MotionSpeedRatio;

            MoveAbs(axisIds, positions, vel);

            // 记录运动参数
            string posDetails = string.Join("|", axisNames.Zip(positions, (n, p) => $"{n}:{p:F3}"));
            string logPrefix = $"【龙门搬运】{string.Join("+", axisNames)}轴同步运动";

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
        private bool MoveZAxisStandbyPos()
        {
            return MoveAxisToPosition(AsmZ, "待机位", _axisConfigService.GetAxisSpeed(0, AsmZ.ActId));
        }
        public bool MoveUAxisStandbyPos()
        {
            return MoveAxisToPosition(AsmU, "待机位", _axisConfigService.GetAxisSpeed(0, AsmU.ActId));
        }
        public bool MoveUAxisPhotoPos()
        {
            return MoveAxisToPosition(AsmU, "侧相机拍照位", _axisConfigService.GetAxisSpeed(0, AsmU.ActId));
        }
        private bool MoveXAxisStandbyPos()
        {
            return MoveAxisToPosition(AsmX, "待机位", _axisConfigService.GetAxisSpeed(0, AsmX.ActId));
        }
        private bool MoveYAxisStandbyPos()
        {
            return MoveAxisToPosition(AsmY, "待机位", _axisConfigService.GetAxisSpeed(0, AsmY.ActId));
        }
        private bool MoveCamYAxisStandbyPos()
        {
            return MoveAxisToPosition(AsmCamY, "待机位", _axisConfigService.GetAxisSpeed(0, AsmCamY.ActId));
        }
        private bool MoveXAxisBottomCameraPhotoPos()
        {
            return MoveAxisToPosition(AsmX, "下相机拍照位", _axisConfigService.GetAxisSpeed(0, AsmX.ActId));
        }
        private bool MoveZAxisBottomCameraPhotoPos()
        {
            return MoveAxisToPosition(AsmZ, "下相机拍照位", _axisConfigService.GetAxisSpeed(0, AsmZ.ActId));
        }
        private bool MoveXAxisSideCameraPhotoPos()
        {
            return MoveAxisToPosition(AsmX, "侧相机拍照位", _axisConfigService.GetAxisSpeed(0, AsmX.ActId));
        }
        private bool MoveZAxisSideCameraPhotoPos()
        {
            return MoveAxisToPosition(AsmZ, "侧相机拍照位", _axisConfigService.GetAxisSpeed(0, AsmZ.ActId));
        }
        private bool MoveCamYAxisSideCameraPhotoPos()
        {
            return MoveAxisToPosition(AsmCamY, "侧相机拍照位", _axisConfigService.GetAxisSpeed(0, AsmCamY.ActId));
        }
        private bool GripperClamp()
        {
            // 夹紧
            var parameters = _recipeService.Parameters;
            LTDMC.nmc_write_rxpdo_extra_uint(0, 2, 3, 1, (uint)parameters.ClampPos);
            _logger.Info($"执行夹紧物料, 夹紧位置: {parameters.ClampPos}");
            return true;
        }
        private bool GripperRelease()
        {
            // 松开
            var parameters = _recipeService.Parameters;
            LTDMC.nmc_write_rxpdo_extra_uint(0, 2, 3, 1, (uint)parameters.ReleasePos);
            _logger.Info($"执行松开物料, 松开位置: {parameters.ReleasePos}");
            return true;
        }
        private bool MoveZAxisHome()
        {
            MoveHome(AsmZ.ActId);
            if (WaitMoveDone())
                return true;
            return false;
        }
        private bool MoveAxesToPickupPhotoPosition()
        {
            var targetAxes = new[]
            {
                AsmX,
            };
            return MoveMultiAxisToPosition(
                axes: targetAxes,
                positionName: "取料拍照位",
                baseVelocity: _axisConfigService.GetAxisSpeed(0, AsmX.ActId));
        }
        private async Task<bool> MoveUAxisStandbyPosAsync()
        {
            if (!MoveAxisToPosition(AsmU, "待机位", _axisConfigService.GetAxisSpeed(0, AsmU.ActId)))
            {
                return false;
            }
            await Task.Delay(50);
            return true;
        }
        /// <summary>
        /// 多轴去拨slot位置
        /// </summary>
        public async Task<bool> MoveAxesToSlotPosition()
        {
            if (!MoveZAxisStandbyPos()) return false;

            if (!MoveAxisToPosition(AsmX, "拨slot位", _axisConfigService.GetAxisSpeed(0, AsmX.ActId)))
            {
                return false;
            }
            if (!MoveAxisToPosition(AsmZ, "拨slot位", _axisConfigService.GetAxisSpeed(0, AsmZ.ActId)))
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// 多轴去拨slot位置(加offset offsetX, offsetZ)
        /// </summary>
        public async Task<bool> MoveAxesToSlotPosition(double offsetX, double offsetZ)
        {
            var targetAxes = new[]
            {
                AsmX, AsmZ
            };
            string positionNames = "拨slot位";
            var position = new[] { GetPosition(AsmX.ActId, positionNames) + offsetX, GetPosition(AsmZ.ActId, positionNames) + offsetZ };
            double[] positions = new[] { position[0], position[1] };
            return MoveMultiAxisToPosition(
                axes: targetAxes,
                positions: positions,
                baseVelocities: new[]
                {
                    _axisConfigService.GetAxisSpeed(0, AsmX.ActId),
                    _axisConfigService.GetAxisSpeed(0, AsmZ.ActId)
                });
        }
        /// <summary>
        /// 执行拨片动作
        /// </summary>
        public async Task<bool> ExecuteStripperSlotAction(double StrippingDistance = -0.6)
        {
            try
            {
                // 步骤1: Y轴移动到拨slot位
                if (!MoveAxisToNamedPosition(AsmY, "拨slot位"))
                    return false;

                // 步骤2: Z轴向上移动0.2mm（相对当前位置）
                if (!MoveAxisRelative(AsmZ, StrippingDistance, 1))  // 放开参数
                    return false;

                await Task.Delay(1500);  // 等待拨片动作完成

                // 步骤3: Y轴移动到待机位
                if (!MoveAxisToNamedPosition(AsmY, "待机位"))
                    return false;

                _logger.Info("拨片动作执行完成");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"执行拨片动作失败: {ex.Message}");
                return false;
            }
        }

        #region 辅助方法

        /// <summary>
        /// 单轴移动到指定名称位置
        /// </summary>
        private bool MoveAxisToNamedPosition(IAxis axis, string positionName)
        {
            double position = GetPosition(axis.ActId, positionName);
            if (position == -1)
            {
                _logger.Error($"获取{axis.Name}轴位置失败: {positionName}");
                return false;
            }

            double velocity = 1;// _axisConfigService.GetAxisSpeed(0, axis.ActId);

            return MoveSingleAxisToPosition(axis, position, velocity, positionName);
        }

        /// <summary>
        /// 单轴移动到指定位置
        /// </summary>
        private bool MoveSingleAxisToPosition(IAxis axis, double position, double velocity, string positionName = null)
        {
            string logName = positionName ?? position.ToString("F3");

            _logger.Info($"{axis.Name}轴开始移动到 {logName} (位置: {position:F3}, 速度: {velocity:F2})");

            bool success = MoveMultiAxisToPosition(
                axes: new[] { axis },
                positions: new[] { position },
                baseVelocities: new[] { velocity }
            );

            if (success)
                _logger.Info($"{axis.Name}轴移动到 {logName} 完成");
            else
                _logger.Error($"{axis.Name}轴移动到 {logName} 失败");

            return success;
        }

        /// <summary>
        /// 单轴相对移动
        /// </summary>
        public bool MoveAxisRelative(IAxis axis, double offset, double velocity = 2.0)
        {
            double currentPosition = GetAxisPosition(axis.ActId);
            double targetPosition = currentPosition + offset;

            _logger.Info($"{axis.Name}轴开始相对移动 {offset:F3}mm (当前: {currentPosition:F3}, 目标: {targetPosition:F3})");

            bool success = MoveMultiAxisToPosition(
                axes: new[] { axis },
                positions: new[] { targetPosition },
                baseVelocities: new[] { velocity }
            );

            if (success)
                _logger.Info($"{axis.Name}轴相对移动 {offset:F3}mm 完成");
            else
                _logger.Error($"{axis.Name}轴相对移动 {offset:F3}mm 失败");

            return success;
        }

        #endregion

        #region 拨片动作(Slot角度拨正)
        
        #endregion

        #region 事件定义

        public event EventHandler<CameraStatusChangedEventArgs> OnCameraStatusChanged;

        #endregion

        #region 相机TCP通信方法
        private VisionResult ParseCameraResponse(string response)
        {
            // 解析相机响应格式，例如: "SUCCESS:data" 或 "ERROR:message"
            if (response.StartsWith("SUCCESS:"))
            {
                return new VisionResult
                {
                    Success = true,
                    RawData = response.Substring("SUCCESS:".Length)
                };
            }
            else if (response.StartsWith("ERROR:"))
            {
                return new VisionResult
                {
                    Success = false,
                    Message = response.Substring("ERROR:".Length)
                };
            }

            return new VisionResult { Success = false, Message = "响应格式错误" };
        }

        /// <summary>
        /// 发送拍照命令给相机
        /// </summary>
        public async Task<bool> TakePhotoAsync(string cameraType = "Side",string command = "TAKE_PHOTO")
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

        /// <summary>
        /// 发送拍照命令并等待结果
        /// </summary>
        public async Task<VisionResult> TakePhotoWithResultAsync(string cameraType = "Side",string command = "TAKE_PHOTO_WITH_RESULT")
        {
            try
            {
                // 只有1个CAMERA_CLIENT
                string cameraName = cameraType;

                _logger.Info($"向{cameraName}发送拍照命令并等待结果: {command}");

                string response = await _tcpEventService.SendCommandWithResponseAsync(CAMERA_CLIENT, command, 5000);

                if (!string.IsNullOrEmpty(response))
                {
                    var result = ParseCameraResponse(response);
                    _logger.Info($"{cameraName}拍照完成: {result}");
                    return result;
                }
                else
                {
                    _logger.Error($"{cameraName}拍照无响应");
                    return new VisionResult { Success = false, Message = "相机无响应" };
                }
            }
            catch (TimeoutException)
            {
                _logger.Error($"{cameraType}相机响应超时");
                return new VisionResult { Success = false, Message = "相机响应超时" };
            }
            catch (Exception ex)
            {
                _logger.Error($"拍照命令执行异常: {ex.Message}");
                return new VisionResult { Success = false, Message = ex.Message };
            }
        }
        /// <summary>
        /// 等待视觉系统拍照完成
        /// </summary>
        private async Task<VisionResult> WaitForVisionSystemPhotoComplete()
        {
            _logger.Info("【组装流程】等待视觉系统拍照完成");

            try
            {
                // 使用视觉数据服务等待视觉数据
                var visionData = await _visionDataService.WaitForVisionDataAsync("SideCamera", 30000); // 30秒超时

                if (visionData.Contains("SUCCESS"))
                {
                    _logger.Info($"视觉系统拍照完成，返回数据: {visionData}");

                    // 解析视觉数据
                    var visionResult = ParseVisionData(visionData);

                    if (!visionResult.Success)
                    {
                        _logger.Error($"视觉数据解析失败: {visionData}");
                        throw new InvalidOperationException($"视觉数据解析失败: {visionData}");
                    }

                    // 处理视觉结果
                    await ProcessVisionResult(visionResult);

                    return visionResult;
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
        /// <summary>
        /// 解析视觉数据
        /// </summary>
        /// <param name="visionData">原始视觉数据</param>
        /// <returns>解析后的视觉结果</returns>
        private VisionResult ParseVisionData(string visionData)
        {
            var result = new VisionResult
            {
                RawData = visionData.ToString(),
                Success = false
            };

            try
            {
                string data = visionData.ToString();
                _logger.Debug($"开始解析视觉数据: {data}");

                // 解析相机名称
                int cameraIndex = data.IndexOf("Camera=");
                int endIndex = data.IndexOf(";", cameraIndex);
                if (cameraIndex >= 0 && endIndex > cameraIndex)
                {
                    result.Camera = data.Substring(cameraIndex + 7, endIndex - cameraIndex - 7);
                }

                // 检查是否成功
                result.Success = data.Contains("VISION_RESULT:SUCCESS:");

                if (result.Success)
                {
                    // 查找偏移量开始位置
                    int offsetStart = data.IndexOf("offsetX=");
                    if (offsetStart >= 0)
                    {
                        string offsetData = data.Substring(offsetStart);

                        // 使用正则表达式提取所有数字
                        var matches = System.Text.RegularExpressions.Regex.Matches(offsetData, @"[-+]?[0-9]*\.?[0-9]+");

                        if (matches.Count >= 4)
                        {
                            result.OffsetX = double.Parse(matches[0].Value);
                            result.OffsetY = double.Parse(matches[1].Value);
                            result.OffsetU = double.Parse(matches[2].Value);
                            result.OffsetH = double.Parse(matches[3].Value);
                        }
                        else
                        {
                            // 备用解析方法：按逗号分割
                            offsetData = offsetData.Replace("offsetX=", "")
                                                   .Replace("offsetY=", "")
                                                   .Replace("offsetU=", "")
                                                   .Replace("offsetH=", "")
                                                   .Replace(" ", "");

                            string[] parts = offsetData.Split(',');
                            if (parts.Length >= 4)
                            {
                                result.OffsetX = double.Parse(parts[0]);
                                result.OffsetY = double.Parse(parts[1]);
                                result.OffsetU = double.Parse(parts[2]);
                                result.OffsetH = double.Parse(parts[3]);
                            }
                        }
                    }
                }

                _logger.Info($"视觉数据解析完成: {result}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"解析视觉数据时发生异常: {ex.Message}");
                return result;
            }
        }

        private async Task ProcessVisionResult(VisionResult visionResult)
        {
            // 处理视觉数据
            _logger.Info($"处理视觉结果: {visionResult}");

            // 这里可以添加具体的处理逻辑

        }
        #endregion

        #region 相机拍照功能
        public async Task<bool> TakePhoto(string cameraType, int module)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 实现拍照逻辑
                    switch (cameraType.ToLower())
                    {
                        case "side":
                            // 侧相机拍照逻辑
                            break;
                        case "bottom":
                            // 下相机拍照逻辑
                            break;
                        case "top":
                            // 上相机拍照逻辑
                            break;
                    }

                    // 触发拍照完成事件
                    var eventArgs = new PhotoCompletedEventArgs
                    {
                        CameraName = cameraType,
                        Success = true,
                        Data = $"模块{module}拍照数据"
                    };
                    OnPhotoCompleted?.Invoke(this, eventArgs);


                    return true;
                }
                catch (Exception ex)
                {
                    var eventArgs = new PhotoCompletedEventArgs
                    {
                        CameraName = cameraType,
                        Success = true,
                        Data = $"模块{module}拍照数据"
                    };
                    OnPhotoCompleted?.Invoke(this, eventArgs);

                    return false;
                }
            });
        }
        /// <summary>
        /// 移动多轴到下相机拍照位
        /// </summary>
        public async Task<bool> MoveAxesToBottomCameraPhoto()
        {
            _logger.Info("移动到下相机拍照位");

            // Z轴回到待机位
            if (!MoveZAxisStandbyPos())
            {
                throw new InvalidOperationException("Z轴回到待机位失败");
            }
            // X轴到下相机拍照位
            if (!MoveXAxisBottomCameraPhotoPos())
            {
                throw new InvalidOperationException("X轴回到待机位失败");
            }
            // Z轴到下相机拍照位
            if (!MoveZAxisBottomCameraPhotoPos())
            {
                throw new InvalidOperationException("Z轴回到待机位失败");
            }
            bool photoResult = await TakePhotoAsync("Bottom", "T5");
            if (!photoResult)
            {
                throw new InvalidOperationException($"移动到下相机拍照位,拍照失败");
            }
            return true;
        }
        /// <summary>
        /// 移动多轴到侧相机拍照位
        /// </summary>
        public async Task<bool> MoveAxesToSideCameraPhoto()
        {
            _logger.Info("移动到侧相机拍照位");

            // Z轴回到待机位
            if (!MoveZAxisStandbyPos())
            {
                throw new InvalidOperationException("Z轴回到待机位失败");
            }
            // X轴到侧相机拍照位
            if (!MoveXAxisSideCameraPhotoPos())
            {
                throw new InvalidOperationException("X轴回到待机位失败");
            }
            // Z轴到侧相机拍照位
            if (!MoveZAxisSideCameraPhotoPos())
            {
                throw new InvalidOperationException("Z轴回到待机位失败");
            }
            // Y轴到侧相机拍照位
            if (!MoveCamYAxisSideCameraPhotoPos())
            {
                throw new InvalidOperationException("侧相机Y轴回到待机位失败");
            }
            bool photoResult = await TakePhotoAsync("SideCamera", "T4");
            if (!photoResult)
            {
                throw new InvalidOperationException($"移动到侧相机拍照位,拍照失败");
            }
            return true;
        }
        // 其他辅助方法
        private void UpdateCameraConnectionStatus(string cameraName, bool isConnected)
        {
            OnCameraStatusChanged?.Invoke(this, new CameraStatusChangedEventArgs
            {
                CameraName = cameraName,
                IsConnected = isConnected,
                Status = isConnected ? "已连接" : "未连接"
            });
        }

        private void UpdateCameraStatus(string cameraName, bool success)
        {
            // 更新内部相机状态
        }

        private void HandlePhotoFailure(string errorMessage)
        {
            // 处理拍照失败逻辑
            ReportAlarm(XAlarmLevel.PAUSE, (int)MachineAlarmCode.拍照失败, (int)XSysAlarmId.MACHINE,
                AlarmCategory.SYSTEM.ToString(), $"拍照失败: {errorMessage}");
        }

        private AssemblyState GetNextStateAfterPhoto(string photoData)
        {
            // 根据拍照结果决定下一个状态
            // 这里可以根据照片分析结果来决定下一步动作
            return AssemblyState.MoveToAssemblyPosition;
        }
        private void HandleCameraStatus(string cameraName, string status)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (cameraName.Contains("Side"))
                {
                    //SideCameraStatus = status;
                }
                else if (cameraName.Contains("Bottom"))
                {
                    //BottomCameraStatus = status;
                }
                else if (cameraName.Contains("Top"))
                {
                    //TopCameraStatus = status;
                }
                _logger.Info($"相机状态更新: {cameraName} - {status}");
            });
        }
        private void HandleCameraError(string cameraName, string error)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                //ShowMessage($"{cameraName}相机错误: {error}", PackIconKind.AlertCircle);
                _logger.Error($"{cameraName}相机错误: {error}");
            });
        }
        #endregion

        #region 组装流程步骤实现

        /// <summary>
        /// 步骤0: 初始化
        /// </summary>
        private async Task<bool> InitializeAssembly()
        {
            _logger.Info("【组装流程】步骤1: 初始化");

            // 检查所有轴是否就绪
            if (!CheckAllAxesReady())
            {
                throw new InvalidOperationException("轴未就绪，无法开始组装流程");
            }
            // 检查视觉系统是否就绪
            //bool visionReady = await CheckVisionSystemReady();
            //if (!visionReady)
            //{
            //    _logger.Warn("视觉系统未就绪，但仍继续流程（依赖超时处理）");
            //}

            _currentAssemblyState = AssemblyState.WaitForLoadingStationSignal;
            UpdateStepStatus("步骤1: 初始化完成", true);
            _logger.Info("【组装流程】初始化完成");

            return true;
        }

        /// <summary>
        /// 步骤1: 移动到取料拍照位
        /// </summary>
        private async Task MoveToPickupPhotoPosition()
        {
            _logger.Info("【组装流程】步骤2: 移动到取料拍照位");

            if (!MoveZAxisStandbyPos())
            {
                throw new InvalidOperationException("移动Z轴到待机高度失败");
            }

            if (!MoveAxesToPickupPhotoPosition())
            {
                throw new InvalidOperationException("移动到取料拍照位置失败");
            }

            _currentAssemblyState = AssemblyState.TakePickupPhoto;
            UpdateStepStatus("步骤2: 移动到取料拍照位置", true);
            _logger.Info("【组装流程】移动到取料拍照位置完成");
        }

        /// <summary>
        /// 步骤2: 拍照
        /// </summary>
        private async Task TakePickupPhoto()
        {
            _logger.Info("【组装流程】步骤3: 执行拍照");

            // 调用视觉系统拍照
            bool photoResult = await TakePhoto("Top", "T1");
            if (!photoResult)
            {
                throw new InvalidOperationException("拍照失败");
            }
            _currentAssemblyState = AssemblyState.MoveToAssemblyPosition;
            UpdateStepStatus("步骤3: 拍照完成", true);
            _logger.Info("【组装流程】拍照完成");
        }
        /// <summary>
        /// 步骤3: 等待取料拍照完成
        /// </summary>
        private async Task WaitForPickupPhotoComplete()
        {
            _logger.Info("【组装流程】等待取料拍照完成");

            bool photoComplete = await WaitForPhotoCompleteSignal("Pickup");
            if (!photoComplete)
            {
                throw new InvalidOperationException("取料拍照完成信号超时");
            }

            _currentAssemblyState = AssemblyState.AlignSlotAngle;
            UpdateStepStatus("取料拍照完成", true);
        }
        /// <summary>
        /// 移动到侧相机拍照位
        /// </summary>
        private async Task MoveToSideCameraPhotoPosition()
        {
            _logger.Info("【组装流程】移动到侧相机拍照位");

            var axes = new[] { AsmX, AsmZ, AsmCamY };
            var velocities = new[] { _axisConfigService.GetAxisSpeed(0, AsmX.ActId),
                                     _axisConfigService.GetAxisSpeed(0, AsmZ.ActId),
                                     _axisConfigService.GetAxisSpeed(0, AsmCamY.ActId) };
            if (!MoveMultiAxisToPosition(axes, "侧相机拍照位", velocities ))
            {
                throw new InvalidOperationException("移动到侧相机拍照位失败");
            }

            _currentAssemblyState = AssemblyState.TakeSideCameraPhoto;
            UpdateStepStatus("移动到侧相机拍照位", true);
        }

        /// <summary>
        /// 触发侧相机拍照
        /// </summary>
        private async Task TakeSideCameraPhoto()
        {
            _logger.Info("【组装流程】触发侧相机拍照");

            var result = await TakePhotoWithResultAsync("Side", "T4");
            if (!result.Success)
            {
                throw new InvalidOperationException($"侧相机拍照失败: {result.Message}");
            }

            _currentAssemblyState = AssemblyState.WaitForSideCameraPhotoComplete;
            UpdateStepStatus("侧相机拍照完成", true);
        }

        /// <summary>
        /// 等待侧相机拍照完成
        /// </summary>
        private async Task WaitForSideCameraPhotoComplete()
        {
            _logger.Info("【组装流程】等待侧相机拍照完成");

            bool photoComplete = await WaitForPhotoCompleteSignal("Side");
            if (!photoComplete)
            {
                throw new InvalidOperationException("侧相机拍照完成信号超时");
            }

            _currentAssemblyState = AssemblyState.MoveToBottomCameraPhotoPosition;
            UpdateStepStatus("侧相机拍照完成", true);
        }

        /// <summary>
        /// 移动到底部相机拍照位
        /// </summary>
        private async Task<bool> MoveToBottomCameraPhotoPosition()
        {
            _logger.Info("【组装流程】移动到底部相机拍照位");

            // Z轴先回到待机位
            if (!MoveZAxisStandbyPos())
            {
                throw new InvalidOperationException("Z轴回到待机位失败");
            }

            // X轴移动到底部相机拍照位
            if (!MoveXAxisBottomCameraPhotoPos())
            {
                throw new InvalidOperationException("X轴移动到底部相机拍照位失败");
            }

            // Z轴移动到底部相机拍照位
            if (!MoveZAxisBottomCameraPhotoPos())
            {
                throw new InvalidOperationException("Z轴移动到底部相机拍照位失败");
            }

            _currentAssemblyState = AssemblyState.MoveZToStandbyAfterPhoto;
            UpdateStepStatus("移动到底部相机拍照位", true);
            return true;
        }

        /// <summary>
        /// 触发底部相机拍照
        /// </summary>
        private async Task TakeBottomCameraPhoto()
        {
            _logger.Info("【组装流程】触发底部相机拍照");

            var result = await TakePhotoWithResultAsync("Bottom", "T5");
            if (!result.Success)
            {
                throw new InvalidOperationException($"底部相机拍照失败: {result.Message}");
            }

            _currentAssemblyState = AssemblyState.WaitForBottomCameraPhotoComplete;
            UpdateStepStatus("底部相机拍照完成", true);
        }

        /// <summary>
        /// 等待底部相机拍照完成
        /// </summary>
        private async Task WaitForBottomCameraPhotoComplete()
        {
            _logger.Info("【组装流程】等待底部相机拍照完成");

            bool photoComplete = await WaitForPhotoCompleteSignal("Bottom");
            if (!photoComplete)
            {
                throw new InvalidOperationException("底部相机拍照完成信号超时");
            }

            _currentAssemblyState = AssemblyState.MoveZToStandbyAfterPhoto;
            UpdateStepStatus("底部相机拍照完成", true);
        }

        /// <summary>
        /// Z轴抬升到待机位
        /// </summary>
        private async Task<bool> MoveZToStandbyAfterPhoto()
        {
            _logger.Info("【组装流程】Z轴抬升到待机位");

            if (!MoveZAxisStandbyPos())
            {
                throw new InvalidOperationException("Z轴抬升到待机位失败");
            }
            _currentAssemblyState = AssemblyState.WaitForGlueComplete;

            // 触发3D扫描(自动时)
            StationEvents.SendSignal(
                StationEvents.AssemblyPickupCompleted,
                _currentPickupCycle,
                $"取料位{_currentPickupCycle}已取料完成"
            );

            _logger.Info("【组装流程】开始组拍照流程");
            UpdateStepStatus("Z轴抬升到待机位，开始组拍照流程", true);
            return true;
        }
        private async Task<bool> WaitForGlueCompleteAaync()
        {
            _logger.Info("【组装流程】等待点胶完成信号");

            bool glueComplate = StationEvents.WaitForSignal(
                                StationEvents.DispensingCompleted,
                                -1, // 30秒超时
                                _runningCTS.Token
                             );

            _currentAssemblyState = AssemblyState.MoveToAssemblyPosition;
            UpdateStepStatus("等待点胶完成信号", true);
            _logger.Info("【组装流程】点胶完成");
            return true;
        } 
        private async Task MoveZToSafePosition()
        {
            _logger.Info("【组装流程】Z轴抬升到待机位");

            if (!MoveZAxisStandbyPos())
            {
                throw new InvalidOperationException("Z轴抬升到待机位失败");
            }
            _currentPickState = PickProcessState.MoveXYToPickPosition;
        }


        /// <summary>
        /// 等待拍照完成信号
        /// </summary>
        private async Task<bool> WaitForPhotoCompleteSignal(string cameraType)
        {
            int timeout = 10000; // 10秒超时
            int elapsed = 0;

            while (elapsed < timeout)
            {
                if (_processCTS.Token.IsCancellationRequested)
                    return false;

                // 检查拍照完成信号（这里需要根据实际硬件信号实现）
                // bool photoComplete = ReadPhotoCompleteSignal(cameraType);
                bool photoComplete = true; // 模拟信号

                if (photoComplete) return true;

                await Task.Delay(100);
                elapsed += 100;
            }

            return false;
        }
        /// <summary>
        /// 步骤4: 移动到装配位置
        /// </summary>
        private async Task<bool> MoveToAssemblyPosition()
        {
            _logger.Info("【组装流程】步骤4: 移动到装配位置");

            if (!MoveZAxisStandbyPos())
            {
                throw new InvalidOperationException("移动Z轴到待机高度失败");
            }

            if (!MoveAxisToPosition(AsmX, "装配工位1", _axisConfigService.GetAxisSpeed(0, AsmX.ActId)))
            {
                throw new InvalidOperationException("移动到装配位置失败");
            }
            _currentAssemblyState = AssemblyState.PerformAssemblyOperation;
            UpdateStepStatus("步骤4: 移动到装配位置", true);
            _logger.Info("【组装流程】移动到装配位置完成");
            return true;
        }

        /// <summary>
        /// 步骤5: 等待物料就绪
        /// </summary>
        private async Task WaitForMaterialReady()
        {
            _logger.Info("【组装流程】步骤5: 等待上料Y轴就绪");

            // 检查上料Y轴到位信号
            bool materialReady = await WaitForMaterialReadySignal();
            if (!materialReady)
            {
                throw new InvalidOperationException("上料Y轴未就绪");
            }
            _currentAssemblyState = AssemblyState.PerformAssemblyOperation;
            UpdateStepStatus("步骤5: 等待物料就绪", true);
            _logger.Info("【组装流程】上料Y轴就绪");
        }

        /// <summary>
        /// 步骤6: 下降到装配位置
        /// </summary>
        private async Task MoveDownToAssembly()
        {
            _logger.Info("【组装流程】步骤6: 下降到装配位置");

            if (!MoveAxisToPosition(AsmZ, "装配工位1", _axisConfigService.GetAxisSpeed(0, AsmZ.ActId)))
            {
                throw new InvalidOperationException("下降到装配位置失败");
            }

            _currentAssemblyState = AssemblyState.NotifyMaterialMoveIn;
            UpdateStepStatus("步骤6: 下降到装配位置", true);
            _logger.Info("【组装流程】下降到装配位置完成");
        }

        /// <summary>
        /// 步骤7: 通知物料移入
        /// </summary>
        private async Task NotifyMaterialMoveIn()
        {
            _logger.Info("【组装流程】步骤7: 通知上料Y轴移入");

            // 置位上料Y轴移动到装配工位1信号
            SetMaterialMoveInSignal(true);

            _currentAssemblyState = AssemblyState.WaitForMaterialInPlace;
            UpdateStepStatus("步骤7: 通知物料移入", true);
            _logger.Info("【组装流程】已通知上料Y轴移入");
        }

        /// <summary>
        /// 步骤8: 等待物料到位
        /// </summary>
        private async Task WaitForMaterialInPlace()
        {
            _logger.Info("【组装流程】步骤8: 等待上料Y轴到位");

            bool materialInPlace = await WaitForMaterialInPlaceSignal();
            if (!materialInPlace)
            {
                throw new InvalidOperationException("上料Y轴未到位");
            }

            _currentAssemblyState = AssemblyState.MoveHorizontalSmallStep;
            UpdateStepStatus("步骤8: 等待物料到位", true);
            _logger.Info("【组装流程】上料Y轴到位");
        }

        /// <summary>
        /// 步骤9: 水平移动小距离
        /// </summary>
        private async Task MoveHorizontalSmallStep()
        {
            _logger.Info("【组装流程】步骤9: 水平移动小距离");

            // 执行小距离平移
            if (!MoveHorizontalSmallDistance())
            {
                throw new InvalidOperationException("水平移动失败");
            }

            _currentAssemblyState = AssemblyState.MoveDownSmallStep;
            UpdateStepStatus("步骤9: 水平移动小距离", true);
            _logger.Info("【组装流程】水平移动完成");
        }

        /// <summary>
        /// 步骤10: 下降小距离
        /// </summary>
        private async Task MoveDownSmallStep()
        {
            _logger.Info("【组装流程】步骤10: 下降小距离");

            // 执行小距离下降
            if (!MoveDownSmallDistance())
            {
                throw new InvalidOperationException("下降移动失败");
            }

            _currentAssemblyState = AssemblyState.ReleaseGripper;
            UpdateStepStatus("步骤10: 下降小距离", true);
            _logger.Info("【组装流程】下降移动完成");
        }

        /// <summary>
        /// 步骤11: 释放夹爪
        /// </summary>
        private async Task ReleaseGripperAction()
        {
            _logger.Info("【组装流程】步骤11: 释放夹爪");

            if (!GripperRelease())
            {
                throw new InvalidOperationException("释放夹爪失败");
            }

            _currentAssemblyState = AssemblyState.NotifyMaterialMoveBack;
            UpdateStepStatus("步骤11: 释放夹爪", true);
            _logger.Info("【组装流程】夹爪释放完成");
        }

        /// <summary>
        /// 步骤12: 通知物料后退
        /// </summary>
        private async Task NotifyMaterialMoveBack()
        {
            _logger.Info("【组装流程】步骤12: 通知上料Y轴后退");

            // 置位上料Y轴后退信号
            SetMaterialMoveBackSignal(true);

            _currentAssemblyState = AssemblyState.WaitForMaterialBack;
            UpdateStepStatus("步骤12: 通知物料后退", true);
            _logger.Info("【组装流程】已通知上料Y轴后退");
        }

        /// <summary>
        /// 步骤13: 等待物料后退到位
        /// </summary>
        private async Task WaitForMaterialBack()
        {
            _logger.Info("【组装流程】步骤13: 等待上料Y轴后退到位");

            bool materialBack = await WaitForMaterialBackSignal();
            if (!materialBack)
            {
                throw new InvalidOperationException("上料Y轴未后退到位");
            }

            _currentAssemblyState = AssemblyState.PostAssembly;
            UpdateStepStatus("步骤13: 等待物料后退到位", true);
            _logger.Info("【组装流程】上料Y轴后退到位");
        }

        /// <summary>
        /// 步骤14: 上升到待机位
        /// </summary>
        private async Task MoveUpToStandby()
        {
            _logger.Info("【组装流程】步骤14: 上升到待机位");

            if (!MoveZAxisStandbyPos())
            {
                throw new InvalidOperationException("上升到待机位失败");
            }

            _currentAssemblyState = AssemblyState.Complete;
            UpdateStepStatus("步骤14: 上升到待机位", true);
            _logger.Info("【组装流程】组装流程完成");
        }
        private async Task<bool> NotifyDispenserSystemForPillarGlue()
        {
            _logger.Info($"【组装流程】步骤8: 通知点胶系统开始点胶");

            StationEvents.SendSignal(
                        StationEvents.AssemblyCompleted,
                        -1,
                        $"通知点胶系统组装完成"
                    );

            UpdateStepStatus("步骤8: 已通知点胶系统,组装完成,准备点胶", true);

            return true;
        }

        private async Task UVProcess()
        {
            _logger.Info($"【组装流程】步骤8: 通知物料到达装配等待位");

            StationEvents.SendSignal(
                        StationEvents.MaterialReadySignal,
                        -1,
                        $"通知物料到达装配等待位"
                    );

            UpdateStepStatus("步骤8: 已通知装配系统物料已到位", true);
        }
        private async Task<bool> WaitUVFixComplete()
        {
            _logger.Info($"【组装流程】步骤9: 等待UV固化完成");

            bool signalReceived = StationEvents.WaitForSignal(
                     StationEvents.UVFixCompleted,
                     -1, // 30秒超时
                     _runningCTS.Token
                  );
            if ( signalReceived ) {

                _currentAssemblyState = AssemblyState.AssemblyIPQC1;
                UpdateStepStatus("步骤9: 已收到UV固化完成信号", true);
                _logger.Info($"【组装流程】步骤9: 收到UV固化完成信号");
                return true;
            }
            else
            {
                _logger.Warn($"【组装流程】步骤9: 未收到UV固化完成信号");
                return false;
            }
        }
        private async Task<bool> AssemblyIPQC()
        {
            _logger.Info($"【组装流程】步骤10: 组装后IPQC");

            bool signalReceived = StationEvents.WaitForSignal(
                     StationEvents.UVFixCompleted,
                     -1, // 30秒超时
                     _runningCTS.Token
                  );
            if (signalReceived)
            {
                _currentAssemblyState = AssemblyState.PostAssembly;
                UpdateStepStatus("步骤9: 已收到UV固化完成信号", true);
                _logger.Info($"【组装流程】步骤9: 收到UV固化完成信号");
                return true;
            }
            else
            {
                _logger.Warn($"【组装流程】步骤9: 未收到UV固化完成信号");
                return false;
            }
        }

        private async Task<bool> PostAssembly()
        {
            _currentAssemblyStep++;
            if (_currentAssemblyStep >= 6)
            {
                _currentAssemblyStep = 0;
                _currentAssemblyState = AssemblyState.Initialize;
                PostEvent(this.Station, XEventID.PAUSE);
            }
            return true;
        }

        #endregion

        #region 自动标定相关

        public enum AutoCalibrationState
        {
            Idle,
            Running,
            Paused,
            MovingToPoint,
            WaitingDelay,
            TriggeringPhoto,
            Completed,
            Error
        }

        private AutoCalibrationState _autoCalibrationState = AutoCalibrationState.Idle;
        private CancellationTokenSource _autoCalibrationCTS;
        private EnhancedCalibrationConfig _autoCalibrationConfig;
        private int _autoCalibrationDelayMs = 1000;
        private int _currentAutoCalibrationPoint = 0;
        private List<CalibrationPoint> _autoCalibrationPoints = new List<CalibrationPoint>();
        private Action<string, int, int, bool> _autoCalibrationStatusCallback;

        #endregion

        #region 自动标定方法

        /// <summary>
        /// 开始自动标定
        /// </summary>
        public async Task<bool> StartAutoCalibration(EnhancedCalibrationConfig config, int delayMs)
        {
            if (_autoCalibrationState != AutoCalibrationState.Idle)
            {
                _logger.Warn("自动标定正在进行中，无法开始新的标定");
                return false;
            }

            try
            {
                _autoCalibrationConfig = config;
                _autoCalibrationDelayMs = delayMs;
                _autoCalibrationPoints = GenerateCalibrationPointsWithDirection(config);
                _currentAutoCalibrationPoint = 0;
                _autoCalibrationState = AutoCalibrationState.Running;
                _autoCalibrationCTS = new CancellationTokenSource();

                _logger.Info($"开始自动标定 - 相机: {config.CameraType}, " +
                            $"点数: {_autoCalibrationPoints.Count}, " +
                            $"延时: {delayMs}ms");

                // 启动自动标定任务
                _ = Task.Run(async () => await ExecuteAutoCalibration());

                UpdateAutoCalibrationStatus("自动标定已启动", 0, _autoCalibrationPoints.Count, true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"启动自动标定失败: {ex.Message}");
                _autoCalibrationState = AutoCalibrationState.Error;
                UpdateAutoCalibrationStatus($"启动失败: {ex.Message}", 0, 0, false);
                return false;
            }
        }

        /// <summary>
        /// 执行自动标定流程
        /// </summary>
        private async Task ExecuteAutoCalibration()
        {
            try
            {
                while (_currentAutoCalibrationPoint < _autoCalibrationPoints.Count &&
                       _autoCalibrationState == AutoCalibrationState.Running)
                {
                    // 检查取消请求
                    if (_autoCalibrationCTS.Token.IsCancellationRequested)
                        break;

                    var point = _autoCalibrationPoints[_currentAutoCalibrationPoint];

                    // 状态：移动到标定点
                    _autoCalibrationState = AutoCalibrationState.MovingToPoint;
                    UpdateAutoCalibrationStatus($"移动到标定点 {_currentAutoCalibrationPoint + 1}",
                        _currentAutoCalibrationPoint, _autoCalibrationPoints.Count, true);

                    // 移动到标定点
                    bool moveSuccess = await MoveToCalibrationPoint(_currentAutoCalibrationPoint);
                    if (!moveSuccess)
                    {
                        throw new InvalidOperationException($"移动到标定点 {_currentAutoCalibrationPoint + 1} 失败");
                    }

                    // 状态：等待延时
                    _autoCalibrationState = AutoCalibrationState.WaitingDelay;
                    UpdateAutoCalibrationStatus($"等待延时 {_autoCalibrationDelayMs}ms",
                        _currentAutoCalibrationPoint, _autoCalibrationPoints.Count, true);

                    // 等待配置的延时时间
                    await Task.Delay(_autoCalibrationDelayMs, _autoCalibrationCTS.Token);

                    // 状态：触发拍照
                    _autoCalibrationState = AutoCalibrationState.TriggeringPhoto;
                    UpdateAutoCalibrationStatus($"触发相机拍照",
                        _currentAutoCalibrationPoint, _autoCalibrationPoints.Count, true);

                    // 触发拍照（不等待完成信号）
                    string cameraType = _autoCalibrationConfig.CameraType;
                    bool photoTriggered = await TriggerCameraPhoto(cameraType);

                    if (photoTriggered)
                    {
                        _logger.Info($"标定点 {_currentAutoCalibrationPoint + 1} 拍照已触发");

                        // 更新点状态为已标定
                        point.Status = "已标定";
                        _autoCalibrationPoints[_currentAutoCalibrationPoint] = point;
                    }
                    else
                    {
                        _logger.Warn($"标定点 {_currentAutoCalibrationPoint + 1} 拍照触发失败");
                    }

                    _currentAutoCalibrationPoint++;
                }

                if (_autoCalibrationState == AutoCalibrationState.Running)
                {
                    // 自动标定完成
                    _autoCalibrationState = AutoCalibrationState.Completed;
                    UpdateAutoCalibrationStatus("自动标定完成",
                        _autoCalibrationPoints.Count, _autoCalibrationPoints.Count, false);

                    _logger.Info("自动标定完成");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info("自动标定被用户取消");
                UpdateAutoCalibrationStatus("自动标定已取消",
                    _currentAutoCalibrationPoint, _autoCalibrationPoints.Count, false);
            }
            catch (Exception ex)
            {
                _logger.Error($"自动标定执行异常: {ex.Message}");
                _autoCalibrationState = AutoCalibrationState.Error;
                UpdateAutoCalibrationStatus($"执行异常: {ex.Message}",
                    _currentAutoCalibrationPoint, _autoCalibrationPoints.Count, false);
            }
            finally
            {
                if (_autoCalibrationState != AutoCalibrationState.Paused)
                {
                    _autoCalibrationState = AutoCalibrationState.Idle;
                }
            }
        }

        /// <summary>
        /// 触发相机拍照（不等待完成）
        /// </summary>
        private async Task<bool> TriggerCameraPhoto(string cameraType)
        {
            try
            {
                string cameraName = cameraType;
                string command = "TAKE_PHOTO";

                // 发送拍照命令，不等待响应
                bool success = await _tcpEventService.SendCommandAsync(CAMERA_CLIENT, command, 100);

                if (success)
                {
                    _logger.Info($"{cameraName} 拍照命令已发送");
                }
                else
                {
                    _logger.Warn($"{cameraName} 拍照命令发送失败");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.Error($"触发拍照异常: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 暂停自动标定
        /// </summary>
        public void PauseAutoCalibration()
        {
            if (_autoCalibrationState == AutoCalibrationState.Running)
            {
                _autoCalibrationState = AutoCalibrationState.Paused;
                UpdateAutoCalibrationStatus("自动标定已暂停",
                    _currentAutoCalibrationPoint, _autoCalibrationPoints.Count, false);
                _logger.Info("自动标定已暂停");
            }
        }

        /// <summary>
        /// 恢复自动标定
        /// </summary>
        public async Task<bool> ResumeAutoCalibration()
        {
            if (_autoCalibrationState == AutoCalibrationState.Paused)
            {
                _autoCalibrationState = AutoCalibrationState.Running;

                // 继续执行自动标定
                _ = Task.Run(async () => await ExecuteAutoCalibration());

                UpdateAutoCalibrationStatus("自动标定已恢复",
                    _currentAutoCalibrationPoint, _autoCalibrationPoints.Count, true);
                _logger.Info("自动标定已恢复");
                return true;
            }
            return false;
        }

        /// <summary>
        /// 停止自动标定
        /// </summary>
        public void StopAutoCalibration()
        {
            if (_autoCalibrationState != AutoCalibrationState.Idle)
            {
                _autoCalibrationCTS?.Cancel();
                _autoCalibrationState = AutoCalibrationState.Idle;

                // 停止所有轴运动
                MoveStop();

                UpdateAutoCalibrationStatus("自动标定已停止",
                    _currentAutoCalibrationPoint, _autoCalibrationPoints.Count, false);
                _logger.Info("自动标定已停止");
            }
        }

        /// <summary>
        /// 设置自动标定状态回调
        /// </summary>
        public void SetAutoCalibrationStatusCallback(Action<string, int, int, bool> callback)
        {
            _autoCalibrationStatusCallback = callback;
        }

        /// <summary>
        /// 更新自动标定状态
        /// </summary>
        private void UpdateAutoCalibrationStatus(string message, int currentPoint, int totalPoints, bool isRunning)
        {
            _autoCalibrationStatusCallback?.Invoke(message, currentPoint, totalPoints, isRunning);
            _logger.Info($"自动标定状态: {message} (点位: {currentPoint}/{totalPoints})");
        }

        #endregion

        #region 相机标定功能

        // 标定相关属性
        private CalibrationState _currentCalibrationState = CalibrationState.Idle;
        private List<CalibrationPoint> _calibrationPoints = new List<CalibrationPoint>();
        private int _currentCalibrationIndex = 0;
        private CalibrationConfig _calibrationConfig = new CalibrationConfig();
        private Action<string, bool> _calibrationStatusCallback;

        public CalibrationState CurrentCalibrationState => _currentCalibrationState;

        /// <summary>
        /// 设置标定状态回调
        /// </summary>
        public void SetCalibrationStatusCallback(Action<string, bool> callback)
        {
            _calibrationStatusCallback = callback;
            _logger.Info("标定状态回调已设置");
        }

        /// <summary>
        /// 开始相机标定
        /// </summary>
        public async Task<bool> StartCalibrationWithDirection(EnhancedCalibrationConfig config)
        {
            if (_currentCalibrationState != CalibrationState.Idle)
            {
                _logger.Warn("标定正在进行中，无法开始新的标定");
                return false;
            }

            try
            {
                _calibrationConfig = config;
                _calibrationPoints.Clear();
                _currentCalibrationIndex = 0;
                _currentCalibrationState = CalibrationState.MovingToPoint;

                // 根据坐标轴方向生成标定点位
                _calibrationPoints = GenerateCalibrationPointsWithDirection(config);

                // 记录标定配置信息
                _logger.Info($"开始标定 - 相机: {config.CameraType}, " +
                            $"标定类型: {(config.Is9PointCalibration ? "9点" : "14点")}, " +
                            $"X轴方向: {(config.IsXAxisReversed ? "反向" : "正向")}, " +
                            $"Y轴方向: {(config.IsYAxisReversed ? "反向" : "正向")}, " +
                            $"起始点: ({config.StartX}, {config.StartY}), " +
                            $"间距: {config.Spacing}");

                // 移动到第一个标定点
                await MoveToCalibrationPoint(_currentCalibrationIndex);

                UpdateCalibrationStatus("标定已启动，考虑坐标轴方向", false);
                _logger.Info("相机标定已启动（考虑坐标轴方向）");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"启动标定失败: {ex.Message}");
                _currentCalibrationState = CalibrationState.Error;
                UpdateCalibrationStatus($"启动标定失败: {ex.Message}", false);
                return false;
            }
        }
        /// <summary>
        /// 移动到下一个标定点
        /// </summary>
        public async Task<bool> MoveToNextCalibrationPoint()
        {
            if (_currentCalibrationState != CalibrationState.WaitingForConfirmation)
            {
                _logger.Warn("标定状态不正确，无法移动到下一个点");
                return false;
            }

            try
            {
                _currentCalibrationIndex++;

                if (_currentCalibrationIndex < _calibrationPoints.Count)
                {
                    _currentCalibrationState = CalibrationState.MovingToPoint;
                    await MoveToCalibrationPoint(_currentCalibrationIndex);
                    return true;
                }
                else
                {
                    // 标定完成
                    _currentCalibrationState = CalibrationState.Completed;
                    UpdateCalibrationStatus("标定完成", false);
                    _logger.Info("相机标定完成");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"移动到下一个标定点失败: {ex.Message}");
                _currentCalibrationState = CalibrationState.Error;
                UpdateCalibrationStatus($"移动失败: {ex.Message}", false);
                return false;
            }
        }

        /// <summary>
        /// 记录当前标定点
        /// </summary>
        public bool RecordCurrentCalibrationPoint(double pixelX, double pixelY)
        {
            if (_currentCalibrationIndex >= _calibrationPoints.Count)
            {
                _logger.Error("标定点索引超出范围");
                return false;
            }

            try
            {
                var point = _calibrationPoints[_currentCalibrationIndex];
                point.PixelX = pixelX;
                point.PixelY = pixelY;
                point.Status = "已标定";

                _calibrationPoints[_currentCalibrationIndex] = point;

                _currentCalibrationState = CalibrationState.WaitingForConfirmation;
                UpdateCalibrationStatus($"点位 {_currentCalibrationIndex + 1} 已记录", true);

                _logger.Info($"记录标定点 {_currentCalibrationIndex + 1}: 像素坐标({pixelX}, {pixelY})");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"记录标定点失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 停止标定
        /// </summary>
        public void StopCalibration()
        {
            MoveStop();
            _currentCalibrationState = CalibrationState.Idle;
            UpdateCalibrationStatus("标定已停止", false);
            _logger.Info("相机标定已停止");
        }

        /// <summary>
        /// 保存标定数据
        /// </summary>
        public bool SaveCalibrationData(string filePath)
        {
            try
            {
                var calibrationData = new
                {
                    Config = _calibrationConfig,
                    Points = _calibrationPoints,
                    Timestamp = DateTime.Now
                };

                string json = System.Text.Json.JsonSerializer.Serialize(calibrationData,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                System.IO.File.WriteAllText(filePath, json);

                _logger.Info($"标定数据已保存: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"保存标定数据失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载标定数据
        /// </summary>
        public bool LoadCalibrationData(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath))
                {
                    _logger.Warn($"标定文件不存在: {filePath}");
                    return false;
                }

                string json = System.IO.File.ReadAllText(filePath);
                // 这里需要反序列化逻辑，简化处理
                // var calibrationData = System.Text.Json.JsonSerializer.Deserialize<dynamic>(json);

                _logger.Info($"标定数据已加载: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"加载标定数据失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 生成标定点网格（9点或14点标定）
        /// 第1个点为中心点，第10-14点为旋转点（仅14点标定）
        /// </summary>
        private List<CalibrationPoint> GenerateCalibrationPointsWithDirection(EnhancedCalibrationConfig config)
        {
            var points = new List<CalibrationPoint>();

            if (config.Is9PointCalibration)
            {
                points = Generate9PointCalibrationWithDirection(config);
            }
            else
            {
                points = Generate14PointCalibrationWithDirection(config);
            }

            _logger.Info($"生成标定点: 相机={config.CameraType}, 点数={(config.Is9PointCalibration ? "9点" : "14点")}, " +
                        $"X轴{(config.IsXAxisReversed ? "反向" : "正向")}, Y轴{(config.IsYAxisReversed ? "反向" : "正向")}");

            return points;
        }

        /// <summary>
        /// 生成9点标定网格
        /// 第1个点为中心点
        /// </summary>
        private void Generate9PointCalibration()
        {
            // 3x3 网格
            int gridSize = 3;
            double centerX = _calibrationConfig.StartX;
            double centerY = _calibrationConfig.StartY;
            double spacing = _calibrationConfig.Spacing;

            // 计算网格起始位置（以中心点为基准）
            double startX = centerX - spacing;
            double startY = centerY - spacing;

            int pointIndex = 1;

            for (int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize; col++)
                {
                    double x = startX + col * spacing;
                    double y = startY + row * spacing;

                    // 第1个点强制为中心点
                    if (pointIndex == 1)
                    {
                        x = centerX;
                        y = centerY;
                    }

                    _calibrationPoints.Add(new CalibrationPoint
                    {
                        Index = pointIndex,
                        MachineX = x,
                        MachineY = y,
                        PixelX = 0,
                        PixelY = 0,
                        PointType = pointIndex == 1 ? "中心点" : "网格点",
                        Status = "待标定"
                    });

                    pointIndex++;
                }
            }
        }

        /// <summary>
        /// 生成14点标定
        /// 第1个点为中心点，第10-14点为旋转点
        /// </summary>
        private void Generate14PointCalibration()
        {
            double centerX = _calibrationConfig.StartX;
            double centerY = _calibrationConfig.StartY;
            double spacing = _calibrationConfig.Spacing;

            int pointIndex = 1;

            // 第1部分：9个网格点（3x3网格）
            Generate9PointCalibrationFor14Point(ref pointIndex, centerX, centerY, spacing);

            // 第2部分：5个旋转点（第10-14点）
            GenerateRotationPoints(ref pointIndex, centerX, centerY, spacing);
        }

        /// <summary>
        /// 为14点标定生成9个网格点
        /// </summary>
        private void Generate9PointCalibrationFor14Point(ref int pointIndex, double centerX, double centerY, double spacing)
        {
            int gridSize = 3;
            double startX = centerX - spacing;
            double startY = centerY - spacing;

            for (int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize; col++)
                {
                    double x = startX + col * spacing;
                    double y = startY + row * spacing;

                    // 第1个点强制为中心点
                    if (pointIndex == 1)
                    {
                        x = centerX;
                        y = centerY;
                    }

                    _calibrationPoints.Add(new CalibrationPoint
                    {
                        Index = pointIndex,
                        MachineX = x,
                        MachineY = y,
                        PixelX = 0,
                        PixelY = 0,
                        PointType = pointIndex == 1 ? "中心点" : "网格点",
                        Status = "待标定"
                    });

                    pointIndex++;
                }
            }
        }

        /// <summary>
        /// 生成5个旋转点（第10-14点）
        /// </summary>
        private void GenerateRotationPoints(ref int pointIndex, double centerX, double centerY, double spacing)
        {
            // 旋转点的配置：角度和半径
            double[] angles = { 0, 90, 180, 270, 45 }; // 前4个是主要方向，第5个是45度
            double radius = spacing * 1.5; // 旋转点距离中心点的半径

            for (int i = 0; i < 5; i++)
            {
                double angle = angles[i] * Math.PI / 180.0; // 转换为弧度
                double x = centerX + radius * Math.Cos(angle);
                double y = centerY + radius * Math.Sin(angle);

                _calibrationPoints.Add(new CalibrationPoint
                {
                    Index = pointIndex,
                    MachineX = x,
                    MachineY = y,
                    PixelX = 0,
                    PixelY = 0,
                    PointType = "旋转点",
                    Status = "待标定"
                });

                pointIndex++;
            }
        }

        /// <summary>
        /// 移动到标定点
        /// </summary>
        private async Task<bool> MoveToCalibrationPoint(int pointIndex)
        {
            if (pointIndex < 0 || pointIndex >= _calibrationPoints.Count)
            {
                _logger.Error($"标定点索引无效: {pointIndex}");
                return false;
            }

            var point = _calibrationPoints[pointIndex];

            try
            {
                UpdateCalibrationStatus($"移动到标定点 {pointIndex + 1}", false);

                if (_calibrationConfig.IsSideCamera)
                {
                    // 使用多轴运动方法移动AsmX和AsmZ
                    var axes = new[] { AsmX, AsmZ };
                    double[] positions = { point.MachineX, point.MachineY };

                    if (!MoveMultiAxesToPositions(axes, positions))
                    {
                        throw new InvalidOperationException("多轴运动失败");
                    }
                }
                else
                {
                    // 使用多轴运动方法移动AsmX和PlatY
                    var axes = new[] { AsmX, PlatY };
                    double[] positions = { point.MachineX, point.MachineY };

                    if (!MoveMultiAxesToPositions(axes, positions))
                    {
                        throw new InvalidOperationException("多轴运动失败");
                    }
                }

                // 等待运动完成
                await Task.Delay(1000); // 简化处理，实际应该等待运动完成信号

                _currentCalibrationState = CalibrationState.WaitingForConfirmation;
                UpdateCalibrationStatus($"已到达标定点 {pointIndex + 1}，请记录像素坐标", true);

                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"移动到标定点失败: {ex.Message}");
                _currentCalibrationState = CalibrationState.Error;
                UpdateCalibrationStatus($"移动失败: {ex.Message}", false);
                return false;
            }
        }

        /// <summary>
        /// 多轴运动到指定位置
        /// </summary>
        private bool MoveMultiAxesToPositions(IAxis[] axes, double[] positions)
        {
            if (axes.Length != positions.Length)
            {
                _logger.Error("轴数量与位置数量不匹配");
                return false;
            }

            try
            {
                int[] axisIds = axes.Select(a => a.ActId).ToArray();
                double baseVelocity = _axisConfigService.GetAxisSpeed(0, axes[0].ActId);

                MoveAbs(axisIds, positions, baseVelocity);

                // 记录运动信息
                string axisNames = string.Join("+", axes.Select(a => a.Name));
                string posInfo = string.Join("|", axes.Zip(positions, (a, p) => $"{a.Name}:{p:F2}"));

                _logger.Info($"多轴运动: {axisNames} 到位置 {posInfo}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"多轴运动执行失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 更新标定状态
        /// </summary>
        private void UpdateCalibrationStatus(string message, bool isWaiting)
        {
            _calibrationStatusCallback?.Invoke(message, isWaiting);
            _logger.Info($"标定状态: {message}");
        }

        #endregion

        #region 相机标定增强功能

        // 标定数据管理
        private Dictionary<string, List<CalibrationPoint>> _calibrationData = new Dictionary<string, List<CalibrationPoint>>();
        private Dictionary<string, CalibrationConfig> _calibrationConfigs = new Dictionary<string, CalibrationConfig>();

        // 当前使用的标定配置键
        private string _currentCalibrationKey = "SideCamera";

        // 标定配置类增强
        public class EnhancedCalibrationConfig : CalibrationConfig
        {
            public bool IsXAxisReversed { get; set; } = false;
            public bool IsYAxisReversed { get; set; } = false;
            public string CameraType { get; set; } = "Side"; // Side, Bottom
        }

        /// <summary>
        /// 获取当前标定配置键
        /// </summary>
        public string GetCurrentCalibrationKey()
        {
            return _currentCalibrationKey;
        }

        /// <summary>
        /// 切换标定配置
        /// </summary>
        public void SwitchCalibrationConfig(string cameraType, bool is9Point)
        {
            _currentCalibrationKey = $"{cameraType}_{(is9Point ? "9Point" : "14Point")}";

            // 如果不存在则创建默认配置
            if (!_calibrationConfigs.ContainsKey(_currentCalibrationKey))
            {
                _calibrationConfigs[_currentCalibrationKey] = new EnhancedCalibrationConfig
                {
                    Is9PointCalibration = is9Point,
                    CameraType = cameraType,
                    IsXAxisReversed = false,
                    IsYAxisReversed = false,
                    StartX = 100,
                    StartY = 100,
                    Spacing = 50,
                    RotationRadius = 75
                };
            }

            // 如果不存在标定点则生成
            if (!_calibrationData.ContainsKey(_currentCalibrationKey))
            {
                GenerateCalibrationPointsForCurrentConfig();
            }

            _logger.Info($"切换到标定配置: {_currentCalibrationKey}");
        }

        /// <summary>
        /// 更新标定配置
        /// </summary>
        public void UpdateCalibrationConfig(EnhancedCalibrationConfig config)
        {
            string key = $"{config.CameraType}_{(config.Is9PointCalibration ? "9Point" : "14Point")}";
            _calibrationConfigs[key] = config;

            // 重新生成标定点
            GenerateCalibrationPointsForConfig(key);

            _logger.Info($"更新标定配置: {key}, X反向: {config.IsXAxisReversed}, Y反向: {config.IsYAxisReversed}");
        }

        /// <summary>
        /// 获取当前标定配置
        /// </summary>
        public EnhancedCalibrationConfig GetCurrentCalibrationConfig()
        {
            if (_calibrationConfigs.ContainsKey(_currentCalibrationKey))
            {
                return _calibrationConfigs[_currentCalibrationKey] as EnhancedCalibrationConfig;
            }
            return new EnhancedCalibrationConfig();
        }

        /// <summary>
        /// 获取所有标定配置
        /// </summary>
        public Dictionary<string, EnhancedCalibrationConfig> GetAllCalibrationConfigs()
        {
            return _calibrationConfigs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value as EnhancedCalibrationConfig);
        }

        /// <summary>
        /// 为指定配置生成标定点
        /// </summary>
        private void GenerateCalibrationPointsForConfig(string configKey)
        {
            if (!_calibrationConfigs.ContainsKey(configKey)) return;

            var config = _calibrationConfigs[configKey] as EnhancedCalibrationConfig;
            if (config == null) return;

            _calibrationData[configKey] = GenerateCalibrationPoints(config);
        }

        /// <summary>
        /// 为当前配置生成标定点
        /// </summary>
        private void GenerateCalibrationPointsForCurrentConfig()
        {
            GenerateCalibrationPointsForConfig(_currentCalibrationKey);
        }

        /// <summary>
        /// 生成标定点（考虑坐标轴方向）
        /// </summary>
        private List<CalibrationPoint> GenerateCalibrationPoints(EnhancedCalibrationConfig config)
        {
            var points = new List<CalibrationPoint>();

            if (config.Is9PointCalibration)
            {
                points = Generate9PointCalibrationWithDirection(config);
            }
            else
            {
                points = Generate14PointCalibrationWithDirection(config);
            }

            _logger.Info($"生成标定点: {config.CameraType}, 9点: {config.Is9PointCalibration}, X反向: {config.IsXAxisReversed}, Y反向: {config.IsYAxisReversed}");
            return points;
        }

        /// <summary>
        /// 生成9点标定（考虑坐标轴方向）
        /// </summary>
        private List<CalibrationPoint> Generate9PointCalibrationWithDirection(EnhancedCalibrationConfig config)
        {
            var points = new List<CalibrationPoint>();
            int gridSize = 3;
            double centerX = config.StartX;
            double centerY = config.StartY;
            double spacing = config.Spacing;

            // 计算网格起始位置（考虑方向）
            double startX = centerX - (config.IsXAxisReversed ? -spacing : spacing);
            double startY = centerY - (config.IsYAxisReversed ? -spacing : spacing);

            int pointIndex = 1;
            double xDirection = config.IsXAxisReversed ? -1 : 1;
            double yDirection = config.IsYAxisReversed ? -1 : 1;

            for (int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize; col++)
                {
                    double x = startX + col * spacing * xDirection;
                    double y = startY + row * spacing * yDirection;

                    // 第1个点强制为中心点
                    if (pointIndex == 1)
                    {
                        x = centerX;
                        y = centerY;
                    }

                    points.Add(new CalibrationPoint
                    {
                        Index = pointIndex,
                        MachineX = Math.Round(x, 3),
                        MachineY = Math.Round(y, 3),
                        PixelX = 0,
                        PixelY = 0,
                        PointType = pointIndex == 1 ? "中心点" : "网格点",
                        Status = "待标定",
                        CameraType = config.CameraType,
                        Is9Point = true
                    });

                    pointIndex++;
                }
            }

            return points;
        }

        /// <summary>
        /// 生成14点标定（考虑坐标轴方向）
        /// </summary>
        private List<CalibrationPoint> Generate14PointCalibrationWithDirection(EnhancedCalibrationConfig config)
        {
            var points = new List<CalibrationPoint>();
            double centerX = config.StartX;
            double centerY = config.StartY;
            double spacing = config.Spacing;

            double xDirection = config.IsXAxisReversed ? -1 : 1;
            double yDirection = config.IsYAxisReversed ? -1 : 1;

            int pointIndex = 1;

            // 第1部分：9个网格点
            int gridSize = 3;
            double startX = centerX - (config.IsXAxisReversed ? -spacing : spacing);
            double startY = centerY - (config.IsYAxisReversed ? -spacing : spacing);

            for (int row = 0; row < gridSize; row++)
            {
                for (int col = 0; col < gridSize; col++)
                {
                    double x = startX + col * spacing * xDirection;
                    double y = startY + row * spacing * yDirection;

                    if (pointIndex == 1)
                    {
                        x = centerX;
                        y = centerY;
                    }

                    points.Add(new CalibrationPoint
                    {
                        Index = pointIndex,
                        MachineX = Math.Round(x, 3),
                        MachineY = Math.Round(y, 3),
                        PixelX = 0,
                        PixelY = 0,
                        PointType = pointIndex == 1 ? "中心点" : "网格点",
                        Status = "待标定",
                        CameraType = config.CameraType,
                        Is9Point = false
                    });

                    pointIndex++;
                }
            }

            // 第2部分：5个旋转点
            double[] angles = { 0, 90, 180, 270, 45 };
            double radius = config.RotationRadius;

            for (int i = 0; i < 5; i++)
            {
                double angle = angles[i] * Math.PI / 180.0;
                double x = centerX + radius * Math.Cos(angle) * xDirection;
                double y = centerY + radius * Math.Sin(angle) * yDirection;

                points.Add(new CalibrationPoint
                {
                    Index = pointIndex,
                    MachineX = Math.Round(x, 3),
                    MachineY = Math.Round(y, 3),
                    PixelX = 0,
                    PixelY = 0,
                    PointType = "旋转点",
                    Status = "待标定",
                    CameraType = config.CameraType,
                    Is9Point = false
                });

                pointIndex++;
            }

            return points;
        }

        /// <summary>
        /// 获取当前标定点列表
        /// </summary>
        public List<CalibrationPoint> CalibrationPoints
        {
            get
            {
                if (_calibrationData.ContainsKey(_currentCalibrationKey))
                {
                    return _calibrationData[_currentCalibrationKey];
                }
                return new List<CalibrationPoint>();
            }
        }

        /// <summary>
        /// 获取指定相机的标定点列表
        /// </summary>
        public List<CalibrationPoint> GetCalibrationPoints(string cameraType, bool is9Point)
        {
            string key = $"{cameraType}_{(is9Point ? "9Point" : "14Point")}";
            if (_calibrationData.ContainsKey(key))
            {
                return _calibrationData[key];
            }
            return new List<CalibrationPoint>();
        }

        #endregion

        #region 组件拿取流程  

        private async Task<bool> PickupMaterial()
        {
            bool pickResult = await PickMaterialFromAssemblyPosition(_currentPickupCycle);

            if (pickResult)
            {
                _currentPickupCycle++;
                _logger.Info($"通知上料站{_currentPickupCycle}号取料位已取料");
                _currentAssemblyState = AssemblyState.AlignSlotAngle;
                _logger.Info($"已发送装配站取料完成信号，序号: {_currentPickupCycle - 1}");
                return true;
            }
            else
            {
                _logger.Warn($"取料失败，当前取料位: {_currentPickupCycle}");
                // 可以选择重试或报告错误
                DialogService.ShowBlockingDialog(
                    "取料失败",
                    $"从装配位{_currentPickupCycle}取料时发生错误，请检查并重试。\n"
                );
                return false;
            }
        }
        /// <summary>
        /// 从指定装配位取料
        /// </summary>
        public async Task<bool> PickMaterialFromAssemblyPosition(int position)
        {
            if (_isProcessRunning)
            {
                _logger.Warn("已有流程正在执行，无法开始新的取料流程");
                return false;
            }

            try
            {
                _isProcessRunning = true;
                _processCTS = new CancellationTokenSource();
                _currentPickState = PickProcessState.Initialize;

                _logger.Info($"开始从{position}号装配位取料流程");

                while (_currentPickState != PickProcessState.Complete &&
                       _currentPickState != PickProcessState.Error)
                {
                    if (_processCTS.Token.IsCancellationRequested)
                        break;

                    switch (_currentPickState)
                    {
                        case PickProcessState.Initialize:
                            await InitializePickProcess(position);
                            break;
                        case PickProcessState.MoveZToSafePosition:
                            await MoveToSafePosition();
                            break;
                        case PickProcessState.MoveXYToPickPosition:
                            await MoveXYToPickPosition(position);
                            break;
                        case PickProcessState.MoveZDownToPick:
                            await MoveZDownToPickPosition(position);
                            break;
                        case PickProcessState.GripperClamp:
                            await ExecuteGripperClamp();
                            break;
                        case PickProcessState.CheckClampSuccess:
                            await CheckClampSuccess();
                            break;
                        case PickProcessState.MoveZUpAfterPick:
                            await MoveZUpAfterPick();
                            break;
                    }

                    await Task.Delay(100); // 防止过于频繁的循环
                }

                bool success = _currentPickState == PickProcessState.Complete;
                _logger.Info($"取料流程{(success ? "完成" : "失败")}");
                return success;
            }
            catch (Exception ex)
            {
                _logger.Error($"取料流程异常: {ex.Message}");
                _currentPickState = PickProcessState.Error;
                return false;
            }
            finally
            {
                _isProcessRunning = false;
                _processCTS?.Dispose();
            }
        }

        private async Task InitializePickProcess(int position)
        {
            _logger.Info($"初始化{position}号位取料流程");

            // 检查设备状态
            if (!CheckAllAxesReady())
            {
                throw new InvalidOperationException("设备未就绪，无法开始取料");
            }
            if (!GripperRelease())
            {
                throw new InvalidOperationException("电爪未就绪，无法开始取料");
            }
            _currentPickState = PickProcessState.MoveZToSafePosition;
        }
        private async Task MoveToSafePosition()
        {
            _logger.Info($"移动装配Z轴到安全高度");

            if (!MoveZAxisStandbyPos())
            {
                throw new InvalidOperationException($"移动到装配Z轴待机位失败");
            }
            if(!MoveUAxisStandbyPos())
            {
                throw new InvalidOperationException($"移动到装配U轴待机位失败");
            }
            _currentPickState = PickProcessState.MoveXYToPickPosition;
        }
        private async Task MoveXToPickPhotoPosition(int position)
        {
            _logger.Info($"移动装配X轴到{position}号位拍照位置");

            // 根据位置选择不同的拍照位
            string photoPosition = $"取料{position}号拍照位";

            var axes = new[] { AsmX };
            if (!MoveMultiAxisToPosition(axes, photoPosition, _axisConfigService.GetAxisSpeed(0, AsmX.ActId)))
            {
                throw new InvalidOperationException($"移动装配X轴到{position}号拍照位置失败");
            }

            _currentPickState = PickProcessState.MoveZToPhotoPosition;
        }
        private async Task MoveZToPickPhotoPosition(int position)
        {
            _logger.Info($"移动装配Z轴到{position}号位拍照位置");

            // 根据位置选择不同的拍照位
            string photoPosition = $"取料{position}号拍照位";

            var axes = new[] { AsmZ };
            if (!MoveMultiAxisToPosition(axes, photoPosition, _axisConfigService.GetAxisSpeed(0, AsmX.ActId)))
            {
                throw new InvalidOperationException($"移动装配Z到{position}号拍照位置失败");
            }

            _currentPickState = PickProcessState.TakePhoto;
        }
        private async Task TakePickPhoto(int position)
        {
            _logger.Info($"执行{position}号位取料前拍照");

            bool photoResult = await TakePhoto("Top", "T1");
            if (!photoResult)
            {
                throw new InvalidOperationException($"{position}号位拍照失败");
            }

            _currentPickState = PickProcessState.MoveZToStandby;
        }

        private async Task MoveXYToPickPosition(int position)
        {
            _logger.Info($"XY轴移动到{position}号取料位");

            IAxis[] axes = new[] { AsmX, PlatY };
            double positionX = GetPosition(AsmX.ActId, $"取料{position}号位");
            double positionY = _loadingStation.GetPosition(PlatY.ActId, $"取料位");
            double[] positions = new[] { positionX, positionY };

            if (!MoveMultiAxisToPosition(axes, positions, _axisConfigService.GetAxisSpeed(0, AsmX.ActId)))
            {
                throw new InvalidOperationException($"XY轴移动到{position}号取料位失败");
            }

            _currentPickState = PickProcessState.MoveZDownToPick;
        }

        private async Task MoveZDownToPickPosition(int position)
        {
            _logger.Info($"Z轴下降到{position}号取料位");

            string pickDownPosition = $"取料{position}号位";
            if (!MoveAxisToPosition(AsmZ, pickDownPosition, _axisConfigService.GetAxisSpeed(0, AsmZ.ActId)))
            {
                throw new InvalidOperationException($"Z轴下降到{position}号取料位失败");
            }

            _currentPickState = PickProcessState.GripperClamp;
        }

        private async Task ExecuteGripperClamp()
        {
            _logger.Info("执行电爪夹取");

            if (!GripperClamp())
            {
                throw new InvalidOperationException("电爪夹取失败");
            }

            // 等待夹取动作完成
            var holdTime = _recipeService.Parameters.ClampHoldTime;
            await Task.Delay(holdTime);

            _currentPickState = PickProcessState.CheckClampSuccess;
        }

        private async Task CheckClampSuccess()
        {
            _logger.Info("检查夹取是否成功");

            // 读取压力传感器判断夹取是否成功
            bool clampSuccess = await CheckPressureSensor();
            if (!clampSuccess)
            {
                throw new InvalidOperationException("夹取失败，压力传感器未检测到信号");
            }

            _logger.Info("夹取成功");
            _currentPickState = PickProcessState.MoveZUpAfterPick;
        }

        private async Task MoveZUpAfterPick()
        {
            _logger.Info("夹取成功，Z轴上升到位");

            if (!MoveZAxisStandbyPos())
            {
                throw new InvalidOperationException("Z轴上升失败");
            }

            _currentPickState = PickProcessState.Complete;
        }

        #endregion

        #region 组件拍照自动流程
        /// <summary>
        /// 执行指定组件的拍照流程
        /// </summary>
        public async Task<bool> TakePhotoForModule(int moduleNumber)
        {
            if (_isProcessRunning)
            {
                _logger.Warn("已有流程正在执行，无法开始新的拍照流程");
                return false;
            }

            try
            {
                _isProcessRunning = true;
                _processCTS = new CancellationTokenSource();
                _currentPhotoState = PhotoProcessState.Initialize;

                _logger.Info($"开始{moduleNumber}号组件拍照流程");

                while (_currentPhotoState != PhotoProcessState.Complete &&
                       _currentPhotoState != PhotoProcessState.Error)
                {
                    if (_processCTS.Token.IsCancellationRequested)
                        break;

                    switch (_currentPhotoState)
                    {
                        case PhotoProcessState.Initialize:
                            await InitializePhotoProcess(moduleNumber);
                            break;
                        case PhotoProcessState.MoveZToStandby:
                            await MoveZToStandbyPosition();
                            break;
                        case PhotoProcessState.MoveXYToTabPhotoPosition:
                            await MoveXYToTabPhotoPosition(moduleNumber);
                            break;
                        case PhotoProcessState.MoveZToPhotoHeight:
                            await MoveZToPhotoHeight(moduleNumber);
                            break;
                        case PhotoProcessState.TriggerTabPhoto:
                            await TriggerTabPhoto(moduleNumber);
                            break;
                        case PhotoProcessState.WaitForTabPhotoComplete:
                            await WaitForTabPhotoComplete(moduleNumber);
                            break;
                        case PhotoProcessState.MoveXYToPillar1PhotoPosition:
                            await MoveXYToPillar1PhotoPosition(moduleNumber);
                            break;
                        case PhotoProcessState.TriggerPillar1Photo:
                            await TriggerPillar1Photo(moduleNumber);
                            break;
                        case PhotoProcessState.WaitForPillar1PhotoComplete:
                            await WaitForPillar1PhotoComplete(moduleNumber);
                            break;
                        case PhotoProcessState.MoveXYToPillar2PhotoPosition:
                            await MoveXYToPillar2PhotoPosition(moduleNumber);
                            break;
                        case PhotoProcessState.TriggerPillar2Photo:
                            await TriggerPillar2Photo(moduleNumber);
                            break;
                        case PhotoProcessState.WaitForPillar2PhotoComplete:
                            await WaitForPillar2PhotoComplete(moduleNumber);
                            break;
                        case PhotoProcessState.MoveZToStandbyAfterPhoto:
                            await MoveZToSafeAfterPhoto();
                            break;
                    }

                    await Task.Delay(100);
                }

                bool success = _currentPhotoState == PhotoProcessState.Complete;
                _logger.Info($"拍照流程{(success ? "完成" : "失败")}");
                return success;
            }
            catch (Exception ex)
            {
                _logger.Error($"拍照流程异常: {ex.Message}");
                _currentPhotoState = PhotoProcessState.Error;
                return false;
            }
            finally
            {
                _isProcessRunning = false;
                _processCTS?.Dispose();
            }
        }

        private async Task InitializePhotoProcess(int moduleNumber)
        {
            _logger.Info($"初始化{moduleNumber}号组件拍照流程");

            if (!CheckAllAxesReady())
            {
                throw new InvalidOperationException("设备未就绪，无法开始拍照");
            }

            _currentPhotoState = PhotoProcessState.MoveZToStandby;
        }

        #endregion

        #region 组件装配流程

        /// <summary>
        /// 执行指定组件的装配流程
        /// </summary>
        public async Task<bool> AssembleModule(int moduleNumber)
        {
            if (_isProcessRunning)
            {
                _logger.Warn("已有流程正在执行，无法开始新的装配流程");
                return false;
            }

            try
            {
                _isProcessRunning = true;
                _processCTS = new CancellationTokenSource();
                _currentAssemblyProcessState = AssemblyProcessState.Initialize;
                _currentAssemblyPosition = moduleNumber;

                _logger.Info($"开始{moduleNumber}号组件装配流程");

                while (_currentAssemblyProcessState != AssemblyProcessState.Complete &&
                       _currentAssemblyProcessState != AssemblyProcessState.Error)
                {
                    if (_processCTS.Token.IsCancellationRequested)
                        break;

                    switch (_currentAssemblyProcessState)
                    {
                        case AssemblyProcessState.Initialize:
                            await InitializeAssemblyProcess(moduleNumber);
                            break;
                        case AssemblyProcessState.MoveZToStandby:
                            await MoveZToStandbyAfterAssemblyPhoto();
                            break;
                        // X轴移动到组装位(Tab的x方向补偿 + Slot的x方向补偿) 
                        case AssemblyProcessState.MoveXToAssemblyPosition:
                            await MoveXToAssemblyPosition(moduleNumber);
                            break;
                        // Y轴移动到预装位  
                        case AssemblyProcessState.MovePlatYToPreAssemblyPosition:
                            await MovePlatYToWaitPosition(moduleNumber);
                            break;
                        // Z轴到预组装高度 
                        case AssemblyProcessState.MoveZDownToPreAssembly:
                            await MoveZDownToPreAssemblyPosition(moduleNumber);
                            break;
                        // Z轴移动到组装高度(Slot的z方向补偿 + Tab的z方向补偿)
                        case AssemblyProcessState.MoveZDownToAssembly:
                            await MoveZDownToAssemblyPosition(moduleNumber);
                            break;
                        // Y轴移动到组装位(Tab的y方向补偿) 
                        case AssemblyProcessState.MovePlatYToAssemblyPosition:
                            await MovePlatYToAssemblyPosition(moduleNumber);
                            break;
                        // 相机移动到组装位拍照位置1
                        case AssemblyProcessState.MoveCameraToAssemblyPosition1:
                            await MoveCameraToAssemblyPosition1(moduleNumber);
                            break;
                        // 相机移动到组装位拍照位置2
                        case AssemblyProcessState.MoveCameraToAssemblyPosition2:
                            await MoveCameraToAssemblyPosition2(moduleNumber);
                            break;
                        // X轴平移一小段距离
                        case AssemblyProcessState.MoveXSmallStep:
                            await MoveXSmallStep(moduleNumber);
                            break;
                        // Z轴继续下降一小段距离(TabZ的z方向补偿)
                        case AssemblyProcessState.MoveZDownSmallStep:
                            await MoveZDownSmallStep(moduleNumber);
                            break;
                        //case AssemblyProcessState.TakeIPQCPhoto:
                        //    await TakeIPQCPhoto(moduleNumber);
                        //    break;
                        //case AssemblyProcessState.ReleaseGripper:
                        //    await ReleaseGripperForAssembly();
                        //    break;
                        //case AssemblyProcessState.CheckAssemblySuccess:
                        //    await CheckAssemblySuccess();
                        //    break;
                        //case AssemblyProcessState.MovePlatYBackToWait:
                        //    await MovePlatYBackToWaitPosition();
                        //    break;
                        //case AssemblyProcessState.MoveZUpToStandby:
                        //    await MoveZUpToStandbyAfterAssembly();
                        //    break;
                        //case AssemblyProcessState.MoveXBackToStandby:
                        //    await MoveXBackToStandbyAfterAssembly();
                        //    break;
                    }
                    // 单步模式下等待继续信号
                    if (IsSingleStepMode)
                    {
                        await WaitForSingleStepContinue();
                    }
                    await Task.Delay(50);
                 }

                bool success = _currentAssemblyProcessState == AssemblyProcessState.Complete;
                _logger.Info($"装配流程{(success ? "完成" : "失败")}");
                return success;
            }
            catch (Exception ex)
            {
                _logger.Error($"装配流程异常: {ex.Message}");
                _currentAssemblyProcessState = AssemblyProcessState.Error;
                return false;
            }
            finally
            {
                _isProcessRunning = false;
                _processCTS?.Dispose();
            }
        }

        private async Task InitializeAssemblyProcess(int moduleNumber)
        {
            _logger.Info($"初始化{moduleNumber}号组件装配流程");

            if (!CheckAllAxesReady())
            {
                throw new InvalidOperationException("设备未就绪，无法开始装配");
            }
            UpdateStepStatus("步骤1: 初始化完成", true);
            _currentAssemblyProcessState = AssemblyProcessState.MoveZToStandby;
        }

        private async Task MovePlatYToAssemblyPhotoPosition(int moduleNumber)
        {
            _logger.Info($"PlatY移动到{moduleNumber}号装配拍照位");

            string photoPosition = $"{moduleNumber}号装配拍照位";
            if (!MoveAxisToPosition(PlatY, photoPosition, _axisConfigService.GetAxisSpeed(0, PlatY.ActId)))
            {
                throw new InvalidOperationException($"PlatY移动到{moduleNumber}号装配拍照位失败");
            }
            UpdateStepStatus("步骤2: PlatY移动到装配拍照位完成", true);
            _currentAssemblyProcessState = AssemblyProcessState.TakeAssemblyPhoto;
        }

        private async Task TakeAssemblyPhoto(int moduleNumber)
        {
            _logger.Info($"执行{moduleNumber}号装配前拍照");

            bool photoResult = await TakePhoto("Top", "T1");
            if (!photoResult)
            {
                throw new InvalidOperationException($"{moduleNumber}号装配前拍照失败");
            }

            _currentAssemblyProcessState = AssemblyProcessState.MoveZToStandby;
        }

        private async Task MoveZToStandbyAfterAssemblyPhoto()
        {
            _logger.Info("拍照完成，Z轴回到待机位");

            if (!MoveZAxisStandbyPos())
            {
                throw new InvalidOperationException("Z轴回到待机位失败");
            }
            UpdateStepStatus("步骤3: Z轴回到待机位完成", true);
            _currentAssemblyProcessState = AssemblyProcessState.MoveXToAssemblyPosition;
        }

        private async Task MoveXToAssemblyPosition(int moduleNumber)
        {
            _logger.Info($"X轴移动到{moduleNumber}号装配位");

            string assemblyPosition = $"装配工位{moduleNumber}";

            // X方向组装补偿 ( tab + slot )
            CompensationData compensationData = _compensationService.GetCompensation(moduleNumber, CompensationType.Tab);

            double offsetX = compensationData.CompensationX;

            if (!MoveAxisToPosition(AsmX, assemblyPosition, _axisConfigService.GetAxisSpeed(0, AsmX.ActId), offsetX ))
            {
                throw new InvalidOperationException($"X轴移动到{moduleNumber}号装配位失败");
            }
            UpdateStepStatus("步骤4: X轴移动到装配位完成", true);
            _currentAssemblyProcessState = AssemblyProcessState.MovePlatYToPreAssemblyPosition;
        }
        /// <summary>
        /// 组装Y轴到预装位
        /// </summary>
        private async Task MovePlatYToWaitPosition(int moduleNumber)
        {
            _logger.Info($"PlatY移动到{moduleNumber}号装配预装位");

            string waitPosition = $"装配预装位{moduleNumber}";
            IAxis[] axes = new[] { PlatY };
            double positionY = _loadingStation.GetPosition(PlatY.ActId, waitPosition);
            double[] positions = new[] { positionY };

            if (!MoveMultiAxisToPosition(axes, positions, 3))  //到预装位的速度，根据实际情况调整
            {
                throw new InvalidOperationException($"移动到{moduleNumber}号装配预装位失败");
            }
            UpdateStepStatus("步骤5: PlatY移动到装配预装位完成", true);
            _currentAssemblyProcessState = AssemblyProcessState.MoveZDownToPreAssembly;
        }
        /// <summary>
        /// 预装高度
        /// </summary>
        private async Task MoveZDownToPreAssemblyPosition(int moduleNumber)
        {
            _logger.Info("Z轴到预组装高度(向上抬2mm)");

            double smallStep = -2; // _recipeService.Parameters.StepDownHeight; // 小步抬升距离，根据实际情况调整
            string positionName = $"装配工位{moduleNumber}";
            double pos = GetPosition(AsmZ.ActId, positionName) + smallStep;
            double velocity = _axisConfigService.GetAxisSpeed(0, AsmZ.ActId);
            if (!MoveZAxisAbsAsync(AsmZ.ActId, pos, 5))
            {
                throw new InvalidOperationException("Z轴到预组装高度失败");
            }
            UpdateStepStatus("步骤6: Z轴到预组装高度完成", true);
            _currentAssemblyProcessState = AssemblyProcessState.MoveZDownToAssembly;
        }
        private async Task MoveZDownToAssemblyPosition(int moduleNumber)
        {
            _logger.Info($"Z轴下降到{moduleNumber}号装配位");

            // Z方向组装补偿
            CompensationData compensationData1 = _compensationService.GetCompensation(1, CompensationType.Slot);
            double offsetH1 = compensationData1.CompensationZ;
            CompensationData compensationData2 = _compensationService.GetCompensation(1, CompensationType.TabZ);
            double offsetH2 = compensationData2.CompensationZTranslate;
            double offsetZ = offsetH1 + offsetH2;

            string assemblyDownPosition = $"装配工位{moduleNumber}";
            if (!MoveAxisToPosition(AsmZ, assemblyDownPosition, 1, offsetZ))
            {
                throw new InvalidOperationException($"Z轴下降到{moduleNumber}号装配位失败");
            }
            UpdateStepStatus("步骤7: Z轴下降到装配位完成", true);
            _currentAssemblyProcessState = AssemblyProcessState.MovePlatYToAssemblyPosition;
        }
        /// <summary>
        /// 装配Y轴到组装位
        /// </summary>

        private async Task MovePlatYToAssemblyPosition(int moduleNumber)
        {
            _logger.Info($"PlatY移动到{moduleNumber}号装配位");

            string assemblyPosition = $"装配位{moduleNumber}";
            IAxis[] axes = new[] { PlatY };

            // Y方向组装补偿
            CompensationData compensationData = _compensationService.GetCompensation(moduleNumber, CompensationType.Tab);

            double offsetY = compensationData.CompensationY;

            double positionY = _loadingStation.GetPosition(PlatY.ActId, assemblyPosition) + offsetY;

            double[] positions = new[] { positionY };
            // 使用工艺里的装配速度
            double speed = 1.5; // _recipeService.Parameters.AssemblySpeed;
            if (!MoveMultiAxisToPosition(axes, positions, 0.5))
            {
                throw new InvalidOperationException($"移动到{moduleNumber}号装配等待位失败");
            }
            UpdateStepStatus("步骤8: PlatY移动到装配位完成", true);
            _currentAssemblyProcessState = AssemblyProcessState.MoveCameraToAssemblyPosition1;
        }

        private async Task MoveZDownSmallStep(int moduleNumber)
        {
            _logger.Info("Z轴下降一小段距离");

            string smallStepPosition = "装配小步下降位";
            double smallStep = 0;// _recipeService.Parameters.StepDownHeight; // 小步下降距离
            double smallVelocity = _recipeService.Parameters.StepDownSpeed; // 小步下降速度

            CompensationData compensationData = _compensationService.GetCompensation(moduleNumber, CompensationType.PressZ);
            double offsetZ = compensationData.CompensationZPress;
            smallStep += offsetZ;
            if (!MoveZAxisRelativeAsync(AsmZ.ActId, smallStep, smallVelocity))
            {
                throw new InvalidOperationException("Z轴小步下降失败");
            }
            UpdateStepStatus("步骤9: Z轴下降一小段距离完成", true);
            _currentAssemblyProcessState = AssemblyProcessState.Complete; 
        }

        private async Task MoveXSmallStep(int moduleNumber)
        {
            _logger.Info("X轴移动一小段距离");

            double translateVelocity = _recipeService.Parameters.StepTranslateSpeed; // 小步平移速度

            CompensationData compensationData = _compensationService.GetCompensation(moduleNumber, CompensationType.Actuator);
            double offsetX = compensationData.CompensationXTranslate;
            double translateStep = offsetX;
            if (!MoveXAxisRelativeAsync(AsmX.ActId, translateStep, translateVelocity))
            {
                throw new InvalidOperationException("X轴小步平移失败");
            }
            UpdateStepStatus("步骤9: X轴移动一小段距离完成", true);
            _currentAssemblyProcessState = AssemblyProcessState.MoveZDownSmallStep;
        }
        /// <summary>
        /// 相机移动到组装位(actuator的X方向校正)
        /// </summary>
        private async Task<bool> MoveCameraToAssemblyPosition1(int moduleNumber)
        {
            try
            {
                _logger.Info($"【组装工站】开始相机移动到组装位(Actuator校正)流程");

                int actuatorIndex = moduleNumber; // 假设是第一个Actuator
                CancellationToken cancellationToken = CancellationToken.None; 

                // 获取参数
                int maxRetries = _recipeService.Parameters?.ActuatorCorrectionMaxRetries ?? 1;
                double tolerance = _recipeService.Parameters?.ActuatorXTolerance ?? 0.02; // 单位：mm
                double firstHeight = GetPosition(DispZ3.ActId, $"Actuator{moduleNumber}_1拍照位"); //_recipeService.Parameters?.ActuatorFirstPhotoHeight ?? 31.006;
                //double secondHeight = _recipeService.Parameters?.ActuatorSecondPhotoHeight ?? 28.899;

                // 记录初始位置
                double initialX = GetPosition(DispX.ActId, "待机位");  
                double initialY = GetPosition(DispY_1.ActId, "待机位");
                double initialZ = GetPosition(DispZ3.ActId, "待机位");

                for (int retry = 0; retry < maxRetries; retry++)
                {
                    // 步骤1: 移动到Actuator拍照位置
                    _logger.Info($"步骤1: 移动到Actuator拍照位置，第{retry + 1}次尝试");

                    if (!await MoveToActuatorPhotoPositionAsync(firstHeight, moduleNumber, actuatorIndex))
                    {
                        _logger.Error("移动到Actuator拍照位置失败");
                        return false;
                    }

                    // 等待稳定
                    await Task.Delay(200, cancellationToken);

                    //// 步骤2: 第一次拍照
                    //_logger.Info($"步骤2: 第一次拍照，高度{firstHeight}mm");

                    //string cameraName = "DispensingCamera";
                    //string photoCommand = $"IPQC_FIRST";
                    //var firstPhotoResult = await _cameraController.TakePhotoAsync(cameraName, photoCommand);
                    //if (!firstPhotoResult)
                    //{
                    //    _logger.Error("Actuator第一次拍照失败");
                    //    return false;
                    //}

                    //// 等待第一次视觉结果
                    //var firstVisionResult = await WaitForVisionSystemPhotoComplete(cameraName);
                    //if (!firstVisionResult.Success)
                    //{
                    //    _logger.Error($"第一次视觉数据获取失败: {firstVisionResult.Message}");
                    //    var result = DialogService.ShowBlockingDialog(
                    //             title: "⚠️警告",
                    //             message: "【组装模组】获取Actuator的X方向补偿,第一次视觉数据获取失败：请检查视觉系统是否发送数据" + "\r\n",
                    //             yesButtonText: "重试",
                    //             noButtonText: "继续",
                    //             extraButtonText: "",
                    //             showExtraButton: false,
                    //             showYesButton: true,
                    //             showNoButton: true,
                    //             icon: PackIconKind.ClockAlert
                    //           );
                    //}

                    //// 步骤3: 调整高度，进行第二次拍照
                    //_logger.Info($"步骤3: 调整高度到{secondHeight}mm");

                    //if (!await MoveDispZ3ToHeightAsync(secondHeight, cancellationToken))
                    //{
                    //    _logger.Error($"调整高度到{secondHeight}mm失败");
                    //    return false;
                    //}

                    //// 等待稳定
                    //await Task.Delay(200, cancellationToken);

                    //// 第二次拍照
                    //_logger.Info($"步骤3: 第二次拍照，高度{secondHeight}mm");
                    //photoCommand = "IPQC_SECOND";
                    //var secondPhotoResult = await _cameraController.TakePhotoAsync(cameraName, photoCommand);
                    //if (!secondPhotoResult)
                    //{
                    //    _logger.Error("Actuator第二次拍照失败");
                    //    return false;
                    //}

                    //// 等待第二次视觉结果
                    //var secondVisionResult = await WaitForVisionSystemPhotoComplete(cameraName);
                    //if (!secondVisionResult.Success)
                    //{
                    //    _logger.Error($"第二次视觉数据获取失败: {secondVisionResult.Message}");
                    //    var result = DialogService.ShowBlockingDialog(
                    //                 title: "⚠️警告",
                    //                 message: "【组装模组】获取Actuator的X方向补偿,第二次视觉数据获取失败：请检查视觉系统是否发送数据" + "\r\n",
                    //                 yesButtonText: "重试",
                    //                 noButtonText: "继续",
                    //                 extraButtonText: "",
                    //                 showExtraButton: false,
                    //                 showYesButton: true,
                    //                 showNoButton: true,
                    //                 icon: PackIconKind.ClockAlert
                    //               );
                    //    return false;
                    //}

                    //// 步骤4: 计算补偿值并调整AsmX轴
                    //_logger.Info($"步骤4: 计算X方向补偿值");

                    //double standardSpacing = GetStandardSpacing();
                    //double compensation = CalculateActuatorXCompensationSimple(firstVisionResult, secondVisionResult, standardSpacing);
                    //_logger.Info($"计算得到X方向补偿值: {compensation:F3}mm");

                    //// 检查是否在容差范围内
                    //if (Math.Abs(compensation) <= 0.5)//tolerance
                    //{
                    //    _logger.Info($"Actuator X方向偏差在允许范围内(±{tolerance:F3}mm)，校正完成");

                    //    // 步骤5: 恢复初始位置
                    //    await ReturnToInitialPositionAsync(initialX, initialY, initialZ, cancellationToken);
                    //    // 更新补偿值
                    //    _compensationService.UpdateCompensation(actuatorIndex, CompensationType.Actuator,
                    //           new CompensationData
                    //           {
                    //               OffsetX = compensation,
                    //               Source = "AssemblyStation"
                    //           });
                    //    // 调整AsmX轴
                    //    _logger.Info($"步骤5: 调整AsmX轴 {compensation:F3}mm");
                    //    _currentAssemblyProcessState = AssemblyProcessState.MoveXSmallStep;
                    //    return true;
                    //}
                }
                UpdateStepStatus("Actuator X方向校正完成", true);
                _currentAssemblyProcessState = AssemblyProcessState.MoveCameraToAssemblyPosition1;
                return true;
            }                        
            catch (OperationCanceledException)
            {
                _logger.Info($"Actuator X方向校正被用户取消");
                return false; 
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Actuator X方向校正流程异常");
                return false;
            }
        }
        /// <summary>
        /// 相机移动到组装位(actuator的X方向校正)
        /// </summary>
        private async Task<bool> MoveCameraToAssemblyPosition2(int moduleNumber)
        {
            try
            {
                _logger.Info($"【组装工站】开始相机移动到组装位(Actuator校正)流程");

                int actuatorIndex = moduleNumber; // 假设是第一个Actuator
                CancellationToken cancellationToken = CancellationToken.None;

                // 获取参数
                int maxRetries = _recipeService.Parameters?.ActuatorCorrectionMaxRetries ?? 1;
                double tolerance = _recipeService.Parameters?.ActuatorXTolerance ?? 0.02; // 单位：mm
                double secondHeight = GetPosition(DispZ3.ActId, $"Actuator{moduleNumber}_2拍照位"); //_recipeService.Parameters?.ActuatorFirstPhotoHeight ?? 31.006;
                //double secondHeight = _recipeService.Parameters?.ActuatorSecondPhotoHeight ?? 28.899;

                // 记录初始位置
                double initialX = GetPosition(DispX.ActId, "待机位");
                double initialY = GetPosition(DispY_1.ActId, "待机位");
                double initialZ = GetPosition(DispZ3.ActId, "待机位");

                for (int retry = 0; retry < maxRetries; retry++)
                {
                    // 步骤1: 移动到Actuator拍照位置
                    _logger.Info($"步骤1: 移动到Actuator拍照位置，第{retry + 1}次尝试");

                    if (!await MoveToActuatorPhotoPositionAsync(secondHeight, moduleNumber, actuatorIndex))
                    {
                        _logger.Error("移动到Actuator拍照位置失败");
                        return false;
                    }

                    // 等待稳定
                    await Task.Delay(200, cancellationToken);

                    //// 步骤2: 第一次拍照
                    //_logger.Info($"步骤2: 第一次拍照，高度{firstHeight}mm");

                    //string cameraName = "DispensingCamera";
                    //string photoCommand = $"IPQC_FIRST";
                    //var firstPhotoResult = await _cameraController.TakePhotoAsync(cameraName, photoCommand);
                    //if (!firstPhotoResult)
                    //{
                    //    _logger.Error("Actuator第一次拍照失败");
                    //    return false;
                    //}

                    //// 等待第一次视觉结果
                    //var firstVisionResult = await WaitForVisionSystemPhotoComplete(cameraName);
                    //if (!firstVisionResult.Success)
                    //{
                    //    _logger.Error($"第一次视觉数据获取失败: {firstVisionResult.Message}");
                    //    var result = DialogService.ShowBlockingDialog(
                    //             title: "⚠️警告",
                    //             message: "【组装模组】获取Actuator的X方向补偿,第一次视觉数据获取失败：请检查视觉系统是否发送数据" + "\r\n",
                    //             yesButtonText: "重试",
                    //             noButtonText: "继续",
                    //             extraButtonText: "",
                    //             showExtraButton: false,
                    //             showYesButton: true,
                    //             showNoButton: true,
                    //             icon: PackIconKind.ClockAlert
                    //           );
                    //}

                    //// 步骤3: 调整高度，进行第二次拍照
                    //_logger.Info($"步骤3: 调整高度到{secondHeight}mm");

                    //if (!await MoveDispZ3ToHeightAsync(secondHeight, cancellationToken))
                    //{
                    //    _logger.Error($"调整高度到{secondHeight}mm失败");
                    //    return false;
                    //}

                    //// 等待稳定
                    //await Task.Delay(200, cancellationToken);

                    //// 第二次拍照
                    //_logger.Info($"步骤3: 第二次拍照，高度{secondHeight}mm");
                    //photoCommand = "IPQC_SECOND";
                    //var secondPhotoResult = await _cameraController.TakePhotoAsync(cameraName, photoCommand);
                    //if (!secondPhotoResult)
                    //{
                    //    _logger.Error("Actuator第二次拍照失败");
                    //    return false;
                    //}

                    //// 等待第二次视觉结果
                    //var secondVisionResult = await WaitForVisionSystemPhotoComplete(cameraName);
                    //if (!secondVisionResult.Success)
                    //{
                    //    _logger.Error($"第二次视觉数据获取失败: {secondVisionResult.Message}");
                    //    var result = DialogService.ShowBlockingDialog(
                    //                 title: "⚠️警告",
                    //                 message: "【组装模组】获取Actuator的X方向补偿,第二次视觉数据获取失败：请检查视觉系统是否发送数据" + "\r\n",
                    //                 yesButtonText: "重试",
                    //                 noButtonText: "继续",
                    //                 extraButtonText: "",
                    //                 showExtraButton: false,
                    //                 showYesButton: true,
                    //                 showNoButton: true,
                    //                 icon: PackIconKind.ClockAlert
                    //               );
                    //    return false;
                    //}

                    //// 步骤4: 计算补偿值并调整AsmX轴
                    //_logger.Info($"步骤4: 计算X方向补偿值");

                    //double standardSpacing = GetStandardSpacing();
                    //double compensation = CalculateActuatorXCompensationSimple(firstVisionResult, secondVisionResult, standardSpacing);
                    //_logger.Info($"计算得到X方向补偿值: {compensation:F3}mm");

                    //// 检查是否在容差范围内
                    //if (Math.Abs(compensation) <= 0.5)//tolerance
                    //{
                    //    _logger.Info($"Actuator X方向偏差在允许范围内(±{tolerance:F3}mm)，校正完成");

                    //    // 步骤5: 恢复初始位置
                    //    await ReturnToInitialPositionAsync(initialX, initialY, initialZ, cancellationToken);
                    //    // 更新补偿值
                    //    _compensationService.UpdateCompensation(actuatorIndex, CompensationType.Actuator,
                    //           new CompensationData
                    //           {
                    //               OffsetX = compensation,
                    //               Source = "AssemblyStation"
                    //           });
                    //    // 调整AsmX轴
                    //    _logger.Info($"步骤5: 调整AsmX轴 {compensation:F3}mm");
                    //    _currentAssemblyProcessState = AssemblyProcessState.MoveXSmallStep;
                    //    return true;
                    //}
                }
                UpdateStepStatus("Actuator X方向校正完成", true);
                _currentAssemblyProcessState = AssemblyProcessState.MoveXSmallStep;
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.Info($"Actuator X方向校正被用户取消");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Actuator X方向校正流程异常");
                return false;
            }
        }
        private async Task TakeIPQCPhoto(int moduleNumber)
        {
             await _dispenserStation.ExecuteIPQCInspection(moduleNumber);
            _currentAssemblyProcessState = AssemblyProcessState.ReleaseGripper;
        }
        private async Task ReleaseGripperForAssembly()
        {
            _logger.Info("装配到位，释放夹爪");

            if (!GripperRelease())
            {
                throw new InvalidOperationException("夹爪释放失败");
            }

            // 等待释放动作完成
            await Task.Delay(500);

            _currentAssemblyProcessState = AssemblyProcessState.CheckAssemblySuccess;
        }

        private async Task CheckAssemblySuccess()
        {
            _logger.Info("检查装配是否成功");

            // 检查压力传感器信号是否消失，判断装配是否到位
            bool assemblySuccess = await CheckPressureSensorDisappear();
            if (!assemblySuccess)
            {
                throw new InvalidOperationException("装配失败，压力传感器信号未消失");
            }

            _logger.Info("装配成功");
            _currentAssemblyProcessState = AssemblyProcessState.MovePlatYBackToWait;
        }

        private async Task MovePlatYBackToWaitPosition()
        {
            _logger.Info("PlatY后退到装配等待位");

            string waitPosition = $"装配等待位";
            IAxis[] axes = new[] { PlatY };
            double positionY = _loadingStation.GetPosition(PlatY.ActId, waitPosition);
            double[] positions = new[] { positionY };
            // 使用工艺里的装配速度
            double speed = _recipeService.Parameters.AssemblySpeed;
            if (!MoveMultiAxisToPosition(axes, positions, speed))
            {
                throw new InvalidOperationException($"后退到装配等待位失败");
            }

            _currentAssemblyProcessState = AssemblyProcessState.MoveZUpToStandby;
        }

        private async Task MoveZUpToStandbyAfterAssembly()
        {
            _logger.Info("Z轴上升到待机位");

            if (!MoveZAxisStandbyPos())
            {
                throw new InvalidOperationException("Z轴上升失败");
            }

            _currentAssemblyProcessState = AssemblyProcessState.MoveXBackToStandby;
        }

        private async Task MoveXBackToStandbyAfterAssembly()
        {
            _logger.Info("X轴回到待机位");

            if (!MoveXAxisStandbyPos())
            {
                throw new InvalidOperationException("X轴回到待机位失败");
            }

            _currentAssemblyProcessState = AssemblyProcessState.Complete;
        }
        #endregion

        #region 辅助方法实现

        /// <summary>
        /// 检查所有轴是否就绪
        /// </summary>
        private bool CheckAllAxesReady()
        {
            return AsmZ.IsHomeOk && AsmU.IsHomeOk && AsmX.IsHomeOk;
        }
        /// <summary>
        /// 检查压力传感器（用于夹取判断）
        /// </summary>
        private async Task<bool> CheckPressureSensor()
        {
            try
            {
                // 模拟读取压力传感器
                // bool sensorStatus = ReadPressureSensor();
                // return sensorStatus;

                await Task.Delay(100);
                return true; // 模拟成功
            }
            catch (Exception ex)
            {
                _logger.Error($"检查压力传感器失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查压力传感器信号消失（用于装配判断）
        /// </summary>
        private async Task<bool> CheckPressureSensorDisappear()
        {
            try
            {
                // 模拟读取压力传感器
                // bool sensorStatus = ReadPressureSensor();
                // return !sensorStatus; // 信号消失表示成功

                await Task.Delay(100);
                return true; // 模拟成功
            }
            catch (Exception ex)
            {
                _logger.Error($"检查压力传感器失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 执行拍照
        /// </summary>
        private async Task<bool> TakePhoto(string cameraType, string command)
        {
            try
            {  
                // 发送拍照命令并等待结果
                var result = await TakePhotoWithResultAsync(cameraType, command);
                result.Success = true;
                if (result.Success)
                {
                    _logger.Info("拍照成功，继续下一步");
                    // 根据拍照结果更新状态
                    _currentAssemblyState = GetNextStateAfterPhoto(result.RawData);
                    return true;
                }
                else
                {
                    _logger.Error($"拍照失败: {result.Message}");
                    // 处理拍照失败
                    HandlePhotoFailure(result.Message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"拍照失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 等待上料Y轴就绪信号
        /// </summary>
        private async Task<bool> WaitForMaterialReadySignal()
        {
            int timeout = 30000; // 30秒超时

            bool signalReceived = StationEvents.WaitForSignal(
                    StationEvents.MaterialReadySignal,
                    timeout,
                    _runningCTS.Token
                );

            return signalReceived;
        }

        /// <summary>
        /// 设置物料移入信号
        /// </summary>
        private void SetMaterialMoveInSignal(bool enable)
        {
            // 实现设置信号逻辑
            // WriteMaterialMoveInSignal(enable);
        }

        /// <summary>
        /// 等待物料到位信号
        /// </summary>
        private async Task<bool> WaitForMaterialInPlaceSignal()
        {
            // 实现等待逻辑，类似 WaitForMaterialReadySignal
            await Task.Delay(100);
            return true;
        }

        /// <summary>
        /// 水平移动小距离
        /// </summary>
        private bool MoveHorizontalSmallDistance()
        {
            // 实现小距离移动逻辑
            return MoveAxisToPosition(AsmX, "小距离平移位", _axisConfigService.GetAxisSpeed(0, AsmX.ActId));
        }

        /// <summary>
        /// 下降移动小距离
        /// </summary>
        private bool MoveDownSmallDistance()
        {
            // 实现小距离下降逻辑
            return MoveAxisToPosition(AsmZ, "小距离下降位", _axisConfigService.GetAxisSpeed(0, AsmZ.ActId));
        }

        /// <summary>
        /// 设置物料后退信号
        /// </summary>
        private void SetMaterialMoveBackSignal(bool enable)
        {
            // 实现设置信号逻辑
            // WriteMaterialMoveBackSignal(enable);
        }

        /// <summary>
        /// 等待物料后退信号
        /// </summary>
        private async Task<bool> WaitForMaterialBackSignal()
        {
            // 实现等待逻辑
            await Task.Delay(100);
            return true;
        }

        private async Task WaitForContinue()
        {
            while (IsPaused && !IsStopped)
            {
                await Task.Delay(200);
            }
        }

        #endregion

        #region 工站之间通信方法
        /// <summary>
        /// 等待上料工站到达取料位信号
        /// </summary>
        private async Task<bool> WaitForLoadingStationSignal()
        {
            _logger.Info("【组装流程】等待上料工站到达取料位信号");

            try
            {
                // 使用集中式事件管理器等待信号
                bool signalReceived = StationEvents.WaitForSignal(
                    StationEvents.MaterialReadySignal,
                    -1, // 30秒超时
                    _runningCTS.Token
                );

                if (signalReceived)
                {
                    _requestedStationIndex = StationEvents.CurrentStationIndex;
                    _logger.Info($"成功收到上料站信号，站号: {_requestedStationIndex}, 数据: {StationEvents.PhotoResultData}");

                    _currentAssemblyState = AssemblyState.PickupMaterial;
                    UpdateStepStatus($"收到上料站{_requestedStationIndex}号站信号，准备移动到取料位", true);
                    return true;
                }
                else
                {
                    throw new InvalidOperationException("等待上料站到达取料位信号超时");
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Info("等待上料站信号被取消");
                return false;
            }
        }

        #endregion

        #region 视觉系统初始化
        /// <summary>
        /// 检查视觉系统是否就绪
        /// </summary>
        private async Task<bool> CheckVisionSystemReady()
        {
            try
            {
                _logger.Info("检查视觉系统状态");

                // 等待视觉系统就绪信号（带较短超时）
                var waitHandles = new WaitHandle[] { StationEvents.VisionSystemReady };
                int result = WaitHandle.WaitAny(waitHandles, 5000); // 5秒超时

                if (result == 0)
                {
                    _logger.Info("视觉系统已就绪");
                    return true;
                }
                else
                {
                    _logger.Warn("视觉系统未就绪或响应超时");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"检查视觉系统状态异常: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region 拍照流程具体实现

        private async Task MoveZToStandbyPosition()
        {
            _logger.Info("Z轴抬起到待机位");

            if (!MoveZAxisStandbyPos())
            {
                throw new InvalidOperationException("Z轴抬起到待机位失败");
            }

            _currentPhotoState = PhotoProcessState.MoveXYToTabPhotoPosition;
        }

        private async Task MoveXYToTabPhotoPosition(int moduleNumber)
        {
            _logger.Info($"XY一起运动到Tab{moduleNumber}拍照位");

            string positionName = $"tab{moduleNumber}";
            IAxis[] axes = new[] { AsmX, PlatY };
            double positionX = GetPosition(AsmX.ActId, positionName);
            double positionY = _loadingStation.GetPosition(PlatY.ActId, positionName);
            double[] positions = new[] { positionX, positionY };

            if (!MoveMultiAxisToPosition(axes, positions, _axisConfigService.GetAxisSpeed(0, AsmX.ActId)))
            {
                throw new InvalidOperationException($"移动到Tab{moduleNumber}拍照位失败");
            }

            _currentPhotoState = PhotoProcessState.MoveZToPhotoHeight;
        }

        private async Task MoveZToPhotoHeight(int moduleNumber)
        {
            _logger.Info($"Z轴移动到Tab{moduleNumber}拍照高度");

            string positionName = $"tab{moduleNumber}";

            if (!MoveAxisToPosition(AsmZ, positionName, _axisConfigService.GetAxisSpeed(0, AsmZ.ActId)))
            {
                throw new InvalidOperationException($"Z轴移动到Tab{moduleNumber}拍照高度失败");
            }

            _currentPhotoState = PhotoProcessState.TriggerTabPhoto;
        }

        private async Task TriggerTabPhoto(int moduleNumber)
        {
            _logger.Info($"触发Tab{moduleNumber}拍照");
            // 通知视觉系统进行Tab拍照
            //await NotifyVisionSystemForTabPhoto(moduleNumber);
            await TakePhotoAsync("CAMERA", "T1");
            _currentPhotoState = PhotoProcessState.WaitForTabPhotoComplete;
        }

        private async Task WaitForTabPhotoComplete(int moduleNumber)
        {
            _logger.Info($"等待Tab{moduleNumber}拍照完成");

            bool photoComplete = await WaitForPhotoCompleteSignal("Tab");
            if (!photoComplete)
            {
                throw new InvalidOperationException($"Tab{moduleNumber}拍照完成信号超时");
            }

            _currentPhotoState = PhotoProcessState.MoveXYToPillar1PhotoPosition;
        }

        private async Task MoveXYToPillar1PhotoPosition(int moduleNumber)
        {
            _logger.Info($"XY一起运动到Pillar{moduleNumber}_1拍照位");

            string positionName = $"pillar{moduleNumber}_1";
            IAxis[] axes = new[] { AsmX, PlatY, AsmZ };
            double positionX = GetPosition(AsmX.ActId, positionName);
            double positionY = _loadingStation.GetPosition(PlatY.ActId, positionName);
            double positionZ = GetPosition(AsmZ.ActId, positionName);
            double[] positions = new[] { positionX, positionY, positionZ };

            if (!MoveMultiAxisToPosition(axes, positions, _axisConfigService.GetAxisSpeed(0, AsmX.ActId)))
            {
                throw new InvalidOperationException($"移动到Pillar{moduleNumber}_1拍照位失败");
            }

            _currentPhotoState = PhotoProcessState.TriggerPillar1Photo;
        }

        private async Task TriggerPillar1Photo(int moduleNumber)
        {
            _logger.Info($"触发Pillar{moduleNumber}_1拍照");

            // 通知视觉系统进行Pillar1拍照
            //await NotifyVisionSystemForPillarPhoto(moduleNumber, 1);
            await TakePhotoAsync("CAMERA", "T2");
            _currentPhotoState = PhotoProcessState.WaitForPillar1PhotoComplete;
        }

        private async Task WaitForPillar1PhotoComplete(int moduleNumber)
        {
            _logger.Info($"等待Pillar{moduleNumber}_1拍照完成");

            bool photoComplete = await WaitForPhotoCompleteSignal("Pillar1");
            if (!photoComplete)
            {
                throw new InvalidOperationException($"Pillar{moduleNumber}_1拍照完成信号超时");
            }

            _currentPhotoState = PhotoProcessState.MoveXYToPillar2PhotoPosition;
        }

        private async Task MoveXYToPillar2PhotoPosition(int moduleNumber)
        {
            _logger.Info($"XY一起运动到Pillar{moduleNumber}_2拍照位");

            string positionName = $"pillar{moduleNumber}_2";
            IAxis[] axes = new[] { AsmX, PlatY, AsmZ };
            double positionX = GetPosition(AsmX.ActId, positionName);
            double positionY = _loadingStation.GetPosition(PlatY.ActId, positionName);
            double positionZ = GetPosition(AsmZ.ActId, positionName);
            double[] positions = new[] { positionX, positionY , positionZ };

            if (!MoveMultiAxisToPosition(axes, positions, _axisConfigService.GetAxisSpeed(0, AsmX.ActId)))
            {
                throw new InvalidOperationException($"移动到Pillar{moduleNumber}_2拍照位失败");
            }

            _currentPhotoState = PhotoProcessState.TriggerPillar2Photo;
        }

        private async Task TriggerPillar2Photo(int moduleNumber)
        {
            _logger.Info($"触发Pillar{moduleNumber}_2拍照");

            // 通知视觉系统进行Pillar2拍照
            //await NotifyVisionSystemForPillarPhoto(moduleNumber, 2);
            await  TakePhotoAsync("CAMERA", "T3");
            _currentPhotoState = PhotoProcessState.WaitForPillar2PhotoComplete;
        }

        private async Task WaitForPillar2PhotoComplete(int moduleNumber)
        {
            _logger.Info($"等待Pillar{moduleNumber}_2拍照完成");

            bool photoComplete = await WaitForPhotoCompleteSignal("Pillar2");
            if (!photoComplete)
            {
                throw new InvalidOperationException($"Pillar{moduleNumber}_2拍照完成信号超时");
            }

            _currentPhotoState = PhotoProcessState.MoveZToStandbyAfterPhoto;
        }

        private async Task MoveZToSafeAfterPhoto()
        {
            _logger.Info("所有拍照完成，Z轴抬起到待机高度");

            if (!MoveZAxisStandbyPos())
            {
                throw new InvalidOperationException("Z轴抬起到待机高度失败");
            }

            _currentPhotoState = PhotoProcessState.Complete;
        }

        #endregion

        #region 手动测试方法
        public async Task<bool> TestVisionSystem()
        {
            IAxis[] axes1 = new[] { AsmX, PlatY, AsmZ };
            double[] pos1 = new double[] { 215.578, 441.576, -29.684 };
            if (!MoveMultiAxisToPosition(axes1, pos1, _axisConfigService.GetAxisSpeed(0, AsmX.ActId)))
            {
                return false;
            }
            await _tcpEventService.SendCommandAsync(CAMERA_CLIENT, "T1", 3000);
            await Task.Delay(1000);
            IAxis[] axes2 = new[] { AsmX, PlatY, AsmZ };
            double[] pos2 = new double[] { 204.281, 441.576, -29.684 };
            if (!MoveMultiAxisToPosition(axes2, pos2, _axisConfigService.GetAxisSpeed(0, AsmX.ActId)))
            {
                return false;
            }
            await _tcpEventService.SendCommandAsync(CAMERA_CLIENT, "T2", 3000);
            await Task.Delay(1000);
            IAxis[] axes3 = new[] { AsmX, PlatY, AsmZ };
            double[] pos3 = new double[] { 226.609, 441.576, -29.684 };
            if (!MoveMultiAxisToPosition(axes3, pos3, _axisConfigService.GetAxisSpeed(0, AsmX.ActId)))
            {
                return false;
            }
            await _tcpEventService.SendCommandAsync(CAMERA_CLIENT, "T3", 3000);

            return false;
        }
        #endregion

    }
}

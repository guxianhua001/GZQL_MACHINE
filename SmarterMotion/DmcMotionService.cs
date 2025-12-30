using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmarterMotion
{
    /// <summary>
    /// DMC运动控制卡服务
    /// </summary>
    public class DmcMotionService
    {
        private ushort _cardNo = 0;  // 控制卡编号
        private ushort _crd = 0;     // 坐标系编号
        private ushort[] _axisList;  // 轴列表 [X轴, Y轴, ...]

        // 运动参数
        private double _maxSpeed = 100;     // 最大速度 mm/s
        private double _acceleration = 100; // 加速度 mm/s²
        private double _deceleration = 100; // 减速度 mm/s²

        public DmcMotionService()
        {
            // 假设X轴=8, Y轴=6
            _axisList = new ushort[] { 8, 6 };
        }

        /// <summary>
        /// 初始化连续插补
        /// </summary>
        public void InitializeContinuousInterpolation()
        {
            try
            {
                // 设置单段插补速度参数
                LTDMC.dmc_set_vector_profile_unit(_cardNo, _crd, 0, 5, 0.1, 0.1, 0);

                // 设置连续插补前瞻模式
                LTDMC.dmc_conti_set_lookahead_mode(_cardNo, _crd, 1, 200, 0, 0);

                // 设置S曲线参数
                LTDMC.dmc_set_vector_s_profile(_cardNo, _crd, 0, 0);

                // 设置圆弧限制
                LTDMC.dmc_set_arc_limit(_cardNo, _crd, 0, 0, 0);

                // 关闭PWM
                LTDMC.dmc_set_pwm_enable(_cardNo, 0);

                // 打开连续插补列表
                LTDMC.dmc_conti_open_list(_cardNo, _crd, 2, _axisList);
            }
            catch (Exception ex)
            {
                throw new Exception($"连续插补初始化失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 向连续插补列表中添加线段
        /// </summary>
        /// <param name="targetX">目标X坐标</param>
        /// <param name="targetY">目标Y坐标</param>
        /// <param name="posiMode">位置模式：0-相对，1-绝对</param>
        /// <param name="mark">标记号</param>
        public void AddLineSegment(double targetX, double targetY, ushort posiMode = 1, int mark = 0)
        {
            try
            {
                // 设置向量参数
                LTDMC.dmc_set_vector_profile_unit(_cardNo, _crd, 0, 0.1, 0.1, 0.1, 0);

                // 添加线段
                double[] targetPos = new double[] { targetX, targetY };
                short result = LTDMC.dmc_conti_line_unit(_cardNo, _crd, 2, _axisList, targetPos, posiMode, mark);

                if (result != 0)
                {
                    throw new Exception($"添加线段失败，错误码: {result}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"添加线段失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 执行连续插补
        /// </summary>
        public void ExecuteContinuousInterpolation()
        {
            try
            {
                // 启动连续插补列表
                LTDMC.dmc_conti_start_list(_cardNo, _crd);

                // 关闭连续插补列表
                LTDMC.dmc_conti_close_list(_cardNo, _crd);

            }
            catch (Exception ex)
            {
                throw new Exception($"执行连续插补失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 检查是否运动完成
        /// </summary>
        public bool IsMotionCompleted()
        {
            while (LTDMC.dmc_check_done_multicoor(_cardNo, _crd) == 0)
            //判断坐标系运动状态，等待运动完成
            {
                Thread.Sleep(2);
            }
            return true;
        }
        /// <summary>
        /// 检查所有轴是否运动完成
        /// </summary>
        public bool AreAllAxesMotionCompleted()
        {
            foreach (ushort axis in _axisList)
            {
                if (LTDMC.dmc_check_done(_cardNo, axis) != 0)
                {
                    return false;
                }
            }
            return true;
        }
        /// <summary>
        /// 等待运动完成（带超时）
        /// </summary>
        public async Task<bool> WaitForMotionCompletionAsync(TimeSpan timeout)
        {
            DateTime startTime = DateTime.Now;

            while (LTDMC.dmc_check_done_multicoor(_cardNo, _crd) == 0)
            {
                if (DateTime.Now - startTime > timeout)
                {
                    return false; // 超时
                }

                await Task.Delay(2); // 每50ms检查一次
            }

            return true;
        }
        /// <summary>
        /// 点胶控制
        /// </summary>
        public async Task ControlDispensing(ushort channel)
        {
            try
            {
                // 开启点胶
                LTDMC.dmc_write_outbit(_cardNo, channel, 0); 
            }
            catch (Exception ex)
            {
                throw new Exception($"点胶控制失败: {ex.Message}", ex);
            }
        }
        /// <summary>
        /// 停止点胶
        /// </summary>
        public async Task StopDispensing(ushort channel)
        {
            try
            {
                // 关闭点胶
                LTDMC.dmc_write_outbit(_cardNo, channel, 1);
            }
            catch (Exception ex)
            {
                throw new Exception($"停止点胶失败: {ex.Message}", ex);
            }
        }
        /// <summary>
        /// 紧急停止
        /// </summary>
        public void EmergencyStop()
        {
            try
            {
                // 调用紧急停止函数
                LTDMC.dmc_stop(_cardNo, _axisList[0], 1);
                LTDMC.dmc_stop(_cardNo, _axisList[1], 1);
            }
            catch (Exception ex)
            {
                throw new Exception($"紧急停止失败: {ex.Message}", ex);
            }
        }

    }
}
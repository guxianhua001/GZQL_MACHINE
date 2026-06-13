using Core.Models;
using Core.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Abstraction
{
    /// <summary>
    /// N点标定服务接口——提供自动标定流程、单点示教/移动、TCP视觉数据接收、仿射计算功能
    /// </summary>
    public interface INPointCalibrationService
    {
        /// <summary>是否正在自动标定中</summary>
        bool IsAutoCalibrating { get; }

        /// <summary>启动自动标定流程：依次移动到各点位 -> 示教 -> 触发视觉 -> 填充数据 -> 延时 -> 下一点</summary>
        /// <param name="points">标定点集合</param>
        /// <param name="delayMs">每点间延时（毫秒）</param>
        /// <param name="enableVisionData">是否启用视觉数据接收</param>
        /// <param name="tcpConnectionName">TCP连接名称</param>
        /// <param name="triggerCommand">触发视觉拍照命令</param>
        /// <param name="ct">取消令牌</param>
        Task StartAutoCalibrationAsync(
            IList<NPointCalibrationPoint> points,
            int delayMs,
            bool enableVisionData,
            string tcpConnectionName,
            string triggerCommand,
            CancellationToken ct);

        /// <summary>停止自动标定</summary>
        void StopAutoCalibration();

        /// <summary>示教指定点位的机械坐标（读取当前轴位置）</summary>
        Task<NPointCalibrationPoint> TeachPointAsync(int pointIndex);

        /// <summary>移动到指定点位的机械坐标</summary>
        Task MoveToPointAsync(NPointCalibrationPoint point);

        /// <summary>订阅TCP视觉数据</summary>
        void SubscribeVisionData(string connectionName);

        /// <summary>取消订阅TCP视觉数据</summary>
        void UnsubscribeVisionData();

        /// <summary>计算仿射标定结果（N点最小二乘法，>=3点）</summary>
        AffineCalibrationResult ComputeCalibration(IList<NPointCalibrationPoint> points);

        /// <summary>单点标定完成事件：(点序号, 标定点数据)</summary>
        event Action<int, NPointCalibrationPoint> PointCalibrated;

        /// <summary>视觉数据到达事件</summary>
        event Action<NPointCalibrationPoint> VisionDataReceived;

        /// <summary>全部标定完成事件</summary>
        event Action<AffineCalibrationResult> CalibrationCompleted;

        /// <summary>标定错误事件</summary>
        event Action<string> CalibrationError;
    }
}

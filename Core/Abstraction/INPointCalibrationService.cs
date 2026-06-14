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

        /// <summary>
        /// 启动自动标定流程
        /// 流程：移动到预定义机械点位 → 拍照(可选) → 读取当前位置 → 等待视觉数据 → 填充 → 延时 → 下一点
        /// </summary>
        /// <param name="points">标定点集合（需已填入机械坐标作为目标位置）</param>
        /// <param name="delayMs">每点间延时（毫秒）</param>
        /// <param name="enableVisionData">是否启用视觉数据接收</param>
        /// <param name="tcpConnectionName">TCP连接名称</param>
        /// <param name="triggerCommand">触发视觉拍照命令</param>
        /// <param name="stationIdentifier">工站标识</param>
        /// <param name="axisNameX">X轴名称</param>
        /// <param name="axisNameY">Y轴名称</param>
        /// <param name="enableAxisX">是否启用X轴</param>
        /// <param name="enableAxisY">是否启用Y轴</param>
        /// <param name="ct">取消令牌</param>
        Task StartAutoCalibrationAsync(
            IList<NPointCalibrationPoint> points,
            int delayMs,
            bool enableVisionData,
            string tcpConnectionName,
            string triggerCommand,
            string stationIdentifier,
            string axisNameX,
            string axisNameY,
            bool enableAxisX,
            bool enableAxisY,
            CancellationToken ct);

        /// <summary>停止自动标定</summary>
        void StopAutoCalibration();

        /// <summary>示教指定点位的机械坐标（读取当前轴位置）</summary>
        /// <param name="stationIdentifier">工站标识</param>
        /// <param name="axisNameX">X轴名称</param>
        /// <param name="axisNameY">Y轴名称</param>
        /// <param name="enableAxisX">是否启用X轴</param>
        /// <param name="enableAxisY">是否启用Y轴</param>
        Task<NPointCalibrationPoint> TeachPointAsync(string stationIdentifier, string axisNameX, string axisNameY, bool enableAxisX, bool enableAxisY);

        /// <summary>移动到指定点位的机械坐标</summary>
        /// <param name="point">目标点位</param>
        /// <param name="stationIdentifier">工站标识</param>
        /// <param name="axisNameX">X轴名称</param>
        /// <param name="axisNameY">Y轴名称</param>
        /// <param name="enableAxisX">是否启用X轴</param>
        /// <param name="enableAxisY">是否启用Y轴</param>
        Task MoveToPointAsync(NPointCalibrationPoint point, string stationIdentifier, string axisNameX, string axisNameY, bool enableAxisX, bool enableAxisY);

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
